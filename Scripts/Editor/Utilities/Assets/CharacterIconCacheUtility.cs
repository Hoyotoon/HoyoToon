#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.AssetPipeline.Textures;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Assets
{
    public static class CharacterIconCacheUtility
    {
        private static readonly HttpClient Client = CreateClient();
        private static readonly HashSet<string> PendingIconDownloads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static SynchronizationContext editorSynchronizationContext;

        public static event Action<string> CharacterIconsCached;

        public static bool TryGetCachedCharacterIconPath(
            string contextAssetPath,
            string roundIconUrl,
            string avatarIconUrl,
            string splashIconUrl,
            out string cachedIconPath)
        {
            cachedIconPath = null;

            string[] iconKinds = { "Round", "Avatar", "Splash" };
            string[] iconUrls = { roundIconUrl, avatarIconUrl, splashIconUrl };

            for (int i = 0; i < iconUrls.Length; i++)
            {
                string iconUrl = ImageProxyUrlUtility.ToUnitySupportedPngUrl(iconUrls[i]);
                if (string.IsNullOrWhiteSpace(iconUrl)
                    || !TryGetCharacterIconCacheAbsolutePath(contextAssetPath, iconKinds[i], iconUrl, out string cacheAbsolutePath)
                    || !IsCacheFileValid(cacheAbsolutePath))
                {
                    continue;
                }

                ApplyCharacterIconImportSettings(cacheAbsolutePath, false);
                cachedIconPath = cacheAbsolutePath;
                return true;
            }

            return false;
        }

        public static bool TryEnsureCharacterIconsCached(
            string contextAssetPath,
            string gameKey,
            string characterId,
            string avatarIconUrl,
            string roundIconUrl,
            string splashIconUrl,
            out string cachedIconPath)
        {
            cachedIconPath = null;

            string[] iconKinds = { "Round", "Avatar", "Splash" };
            string[] iconUrls = { roundIconUrl, avatarIconUrl, splashIconUrl };
            string fallbackCachedPath = null;
            bool cachedAny = false;

            for (int i = 0; i < iconUrls.Length; i++)
            {
                string iconUrl = ImageProxyUrlUtility.ToUnitySupportedPngUrl(iconUrls[i]);
                if (string.IsNullOrWhiteSpace(iconUrl))
                {
                    continue;
                }

                if (!TryGetOrRequestCharacterIconCached(contextAssetPath, gameKey, characterId, iconKinds[i], iconUrl, out string iconCachedPath))
                {
                    continue;
                }

                cachedAny = true;
                if (string.IsNullOrWhiteSpace(fallbackCachedPath))
                {
                    fallbackCachedPath = iconCachedPath;
                }

                if (string.Equals(iconKinds[i], "Round", StringComparison.Ordinal))
                {
                    cachedIconPath = iconCachedPath;
                }
            }

            if (string.IsNullOrWhiteSpace(cachedIconPath))
            {
                cachedIconPath = fallbackCachedPath;
            }

            return cachedAny;
        }

        private static bool TryGetOrRequestCharacterIconCached(
            string contextAssetPath,
            string gameKey,
            string characterId,
            string iconKind,
            string iconUrl,
            out string cachedIconPath)
        {
            cachedIconPath = null;

            if (!TryGetCharacterIconCacheAbsolutePath(contextAssetPath, iconKind, iconUrl, out string cacheAbsolutePath))
            {
                return false;
            }

            RemoveStaleCharacterIconCacheFiles(cacheAbsolutePath, iconKind);

            if (IsCacheFileValid(cacheAbsolutePath))
            {
                ApplyTextureImportSettings(gameKey, cacheAbsolutePath, false);
                cachedIconPath = cacheAbsolutePath;
                return true;
            }

            QueueCharacterIconDownload(contextAssetPath, gameKey, characterId, iconKind, iconUrl, cacheAbsolutePath);
            return false;
        }

        private static void RemoveStaleCharacterIconCacheFiles(string cacheAbsolutePath, string iconKind)
        {
            string cacheDirectory = Path.GetDirectoryName(cacheAbsolutePath);
            if (string.IsNullOrWhiteSpace(cacheDirectory)
                || string.IsNullOrWhiteSpace(iconKind)
                || !Directory.Exists(cacheDirectory))
            {
                return;
            }

            string currentFileName = Path.GetFileName(cacheAbsolutePath);
            string currentMetaFileName = currentFileName + ".meta";
            foreach (string filePath in Directory.GetFiles(cacheDirectory, iconKind + "_*.*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (string.Equals(fileName, currentFileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, currentMetaFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void QueueCharacterIconDownload(
            string contextAssetPath,
            string gameKey,
            string characterId,
            string iconKind,
            string iconUrl,
            string cacheAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(cacheAbsolutePath) || string.IsNullOrWhiteSpace(iconUrl))
            {
                return;
            }

            editorSynchronizationContext = SynchronizationContext.Current ?? editorSynchronizationContext;

            string downloadKey = cacheAbsolutePath + "|" + iconUrl;
            lock (PendingIconDownloads)
            {
                if (!PendingIconDownloads.Add(downloadKey))
                {
                    return;
                }
            }

            _ = DownloadCharacterIconAsync(contextAssetPath, gameKey, characterId, iconKind, iconUrl, cacheAbsolutePath, downloadKey);
        }

        private static async Task DownloadCharacterIconAsync(
            string contextAssetPath,
            string gameKey,
            string characterId,
            string iconKind,
            string iconUrl,
            string cacheAbsolutePath,
            string downloadKey)
        {
            string tempFilePath = cacheAbsolutePath + ".download";

            try
            {
                string cacheDirectory = Path.GetDirectoryName(cacheAbsolutePath);
                if (string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    CompleteCharacterIconDownload(downloadKey);
                    return;
                }

                Directory.CreateDirectory(cacheDirectory);

                byte[] payload = await Client
                    .GetByteArrayAsync(iconUrl)
                    .ConfigureAwait(false);

                if (payload == null || payload.Length <= 0)
                {
                    CompleteCharacterIconDownload(downloadKey);
                    return;
                }

                File.WriteAllBytes(tempFilePath, payload);

                if (File.Exists(cacheAbsolutePath))
                {
                    File.Delete(cacheAbsolutePath);
                }

                File.Move(tempFilePath, cacheAbsolutePath);
                PostToEditorThread(() =>
                {
                    try
                    {
                        ApplyTextureImportSettings(gameKey, cacheAbsolutePath, true);
                        CharacterIconsCached?.Invoke(contextAssetPath ?? string.Empty);
                    }
                    finally
                    {
                        CompleteCharacterIconDownload(downloadKey);
                    }
                });
            }
            catch (Exception exception)
            {
                PostToEditorThread(() =>
                {
                    if (!IsCacheFileValid(cacheAbsolutePath))
                    {
                        HoyoToonLogger.Verbose(
                            HoyoToonLogCategory.Detection,
                            $"Character icon caching skipped for character '{characterId}' in game '{gameKey}' ({iconKind}). URL: {iconUrl}. {exception.GetType().Name}: {exception.Message}");
                    }

                    CompleteCharacterIconDownload(downloadKey);
                });
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private static void CompleteCharacterIconDownload(string downloadKey)
        {
            if (string.IsNullOrWhiteSpace(downloadKey))
            {
                return;
            }

            lock (PendingIconDownloads)
            {
                PendingIconDownloads.Remove(downloadKey);
            }
        }

        private static void PostToEditorThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            SynchronizationContext context = editorSynchronizationContext;
            if (context != null)
            {
                context.Post(_ => action(), null);
                return;
            }

            EditorApplication.delayCall += () => action();
        }

        private static bool TryGetCharacterIconCacheAbsolutePath(string contextAssetPath, string iconKind, string iconUrl, out string cacheAbsolutePath)
        {
            cacheAbsolutePath = null;

            if (string.IsNullOrWhiteSpace(contextAssetPath)
                || string.IsNullOrWhiteSpace(iconKind)
                || string.IsNullOrWhiteSpace(iconUrl)
                || !TryResolveIconsDirectory(contextAssetPath, out string iconsDirectory))
            {
                return false;
            }

            string extension = ResolveCacheExtension(iconUrl);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            cacheAbsolutePath = Path.Combine(iconsDirectory, $"{iconKind}_{ComputeStableHash(iconUrl)}{extension}");
            return true;
        }

        private static bool TryResolveIconsDirectory(string contextAssetPath, out string iconsDirectory)
        {
            iconsDirectory = null;

            if (!AssetContextJsonQueryUtility.TryResolveSearchRootDirectory(contextAssetPath, out string searchRootDirectory)
                || string.IsNullOrWhiteSpace(searchRootDirectory))
            {
                return false;
            }

            string currentDirectory = searchRootDirectory;
            string leafDirectoryName = Path.GetFileName(currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(leafDirectoryName, "Materials", StringComparison.OrdinalIgnoreCase)
                || string.Equals(leafDirectoryName, "Textures", StringComparison.OrdinalIgnoreCase)
                || string.Equals(leafDirectoryName, "Icons", StringComparison.OrdinalIgnoreCase))
            {
                string parentDirectory = Path.GetDirectoryName(currentDirectory);
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    currentDirectory = parentDirectory;
                }
            }

            iconsDirectory = Path.Combine(currentDirectory, "Icons");
            return true;
        }

        private static string ResolveCacheExtension(string iconUrl)
        {
            if (Uri.TryCreate(iconUrl, UriKind.Absolute, out Uri uri))
            {
                string formatOverrideExtension = ResolveFormatOverrideExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(formatOverrideExtension))
                {
                    return formatOverrideExtension;
                }

                string extension = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    return extension;
                }
            }

            return ".bin";
        }

        private static string ResolveFormatOverrideExtension(string urlPath)
        {
            if (string.IsNullOrWhiteSpace(urlPath))
            {
                return null;
            }

            string fileName = Path.GetFileName(urlPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            int overrideIndex = fileName.LastIndexOf('@');
            if (overrideIndex < 0 || overrideIndex >= fileName.Length - 1)
            {
                return null;
            }

            string overrideValue = fileName.Substring(overrideIndex + 1).Trim();
            if (string.IsNullOrWhiteSpace(overrideValue))
            {
                return null;
            }

            switch (overrideValue.TrimStart('.').ToLowerInvariant())
            {
                case "png":
                    return ".png";

                case "jpg":
                case "jpeg":
                    return ".jpg";

                case "webp":
                    return ".webp";

                default:
                    return null;
            }
        }

        private static void ApplyTextureImportSettings(string gameKey, string absoluteTexturePath, bool forceImport)
        {
            string textureAssetPath = AssetContextJsonQueryUtility.ToAssetPath(absoluteTexturePath);
            if (string.IsNullOrWhiteSpace(textureAssetPath))
            {
                return;
            }

            if (forceImport || AssetImporter.GetAtPath(textureAssetPath) == null)
            {
                AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
            }

            if (GameRegistry.TryGetGame(gameKey, out GameConfigSO game) && game != null)
            {
                TextureImportSettingsApplicator.Apply(game, textureAssetPath);
            }

            ApplyCharacterIconImportSettings(textureAssetPath, false);
        }

        private static void ApplyCharacterIconImportSettings(string texturePath, bool forceImport)
        {
            string textureAssetPath = AssetContextJsonQueryUtility.ToAssetPath(texturePath);
            if (string.IsNullOrWhiteSpace(textureAssetPath))
            {
                return;
            }

            if (forceImport || AssetImporter.GetAtPath(textureAssetPath) == null)
            {
                AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            changed |= SetImporterValue(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
            changed |= SetImporterValue(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
            changed |= SetImporterValue(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
            changed |= SetImporterValue(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);

            if (!changed)
            {
                return;
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static bool SetImporterValue<T>(T currentValue, T nextValue, Action<T> applyValue)
        {
            if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
            {
                return false;
            }

            applyValue(nextValue);
            return true;
        }

        private static bool IsCacheFileValid(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath)
                && File.Exists(filePath)
                && new FileInfo(filePath).Length > 0;
        }

        private static string ComputeStableHash(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(16);
            for (int index = 0; index < 8 && index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }

            return builder.ToString();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };

            client.DefaultRequestHeaders.Add("User-Agent", "HoyoToon-Unity-Editor");
            return client;
        }
    }

    internal static class ImageProxyUrlUtility
    {
        private const string ImgProxyPngPrefix = "https://imgproxy.hoyotoon.com/unsafe/plain/";
        private const string ImgProxyPngSuffix = "@png";

        internal static string ToUnitySupportedPngUrl(string iconUrl)
        {
            string normalizedUrl = iconUrl?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedUrl)
                || IsImgProxyUrl(normalizedUrl)
                || !IsWebpUrl(normalizedUrl))
            {
                return string.IsNullOrWhiteSpace(normalizedUrl) ? null : normalizedUrl;
            }

            return $"{ImgProxyPngPrefix}{normalizedUrl}{ImgProxyPngSuffix}";
        }

        private static bool IsImgProxyUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                && string.Equals(uri.Host, "imgproxy.hoyotoon.com", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebpUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return url.IndexOf(".webp", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return uri.AbsolutePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
