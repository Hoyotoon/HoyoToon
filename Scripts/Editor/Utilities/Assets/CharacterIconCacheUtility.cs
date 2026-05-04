#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
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
                string iconUrl = iconUrls[i];
                if (string.IsNullOrWhiteSpace(iconUrl)
                    || !TryGetCharacterIconCacheAbsolutePath(contextAssetPath, iconKinds[i], iconUrl, out string cacheAbsolutePath)
                    || !IsCacheFileValid(cacheAbsolutePath))
                {
                    continue;
                }

                cachedIconPath = cacheAbsolutePath;
                return true;
            }

            return false;
        }

        public static bool TryEnsureCharacterIconsCached(
            string contextAssetPath,
            string gameKey,
            int characterId,
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
                string iconUrl = iconUrls[i];
                if (string.IsNullOrWhiteSpace(iconUrl))
                {
                    continue;
                }

                if (!TryEnsureCharacterIconCached(contextAssetPath, gameKey, characterId, iconKinds[i], iconUrl, out string iconCachedPath))
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

        private static bool TryEnsureCharacterIconCached(
            string contextAssetPath,
            string gameKey,
            int characterId,
            string iconKind,
            string iconUrl,
            out string cachedIconPath)
        {
            cachedIconPath = null;

            if (!TryGetCharacterIconCacheAbsolutePath(contextAssetPath, iconKind, iconUrl, out string cacheAbsolutePath))
            {
                return false;
            }

            if (IsCacheFileValid(cacheAbsolutePath))
            {
                ApplyTextureImportSettings(gameKey, cacheAbsolutePath);
                cachedIconPath = cacheAbsolutePath;
                return true;
            }

            string tempFilePath = cacheAbsolutePath + ".download";

            try
            {
                string cacheDirectory = Path.GetDirectoryName(cacheAbsolutePath);
                if (string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    return false;
                }

                Directory.CreateDirectory(cacheDirectory);

                byte[] payload = Client
                    .GetByteArrayAsync(iconUrl)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (payload == null || payload.Length <= 0)
                {
                    return false;
                }

                File.WriteAllBytes(tempFilePath, payload);

                if (File.Exists(cacheAbsolutePath))
                {
                    File.Delete(cacheAbsolutePath);
                }

                File.Move(tempFilePath, cacheAbsolutePath);
                ApplyTextureImportSettings(gameKey, cacheAbsolutePath);
                cachedIconPath = cacheAbsolutePath;
                return true;
            }
            catch (Exception exception)
            {
                if (IsCacheFileValid(cacheAbsolutePath))
                {
                    cachedIconPath = cacheAbsolutePath;
                    return true;
                }

                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Detection,
                    $"Character icon caching skipped for character ID '{characterId}' in game '{gameKey}' ({iconKind}). URL: {iconUrl}. {exception.GetType().Name}: {exception.Message}");
                return false;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
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

            cacheAbsolutePath = Path.Combine(iconsDirectory, $"{iconKind}{extension}");
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

        private static void ApplyTextureImportSettings(string gameKey, string absoluteTexturePath)
        {
            string textureAssetPath = AssetContextJsonQueryUtility.ToAssetPath(absoluteTexturePath);
            if (string.IsNullOrWhiteSpace(textureAssetPath)
                || !GameRegistry.TryGetGame(gameKey, out GameConfigSO game)
                || game == null)
            {
                return;
            }

            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
            TextureImportSettingsApplicator.Apply(game, textureAssetPath);
        }

        private static bool IsCacheFileValid(string filePath)
        {
            return !string.IsNullOrWhiteSpace(filePath)
                && File.Exists(filePath)
                && new FileInfo(filePath).Length > 0;
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
}
#endif