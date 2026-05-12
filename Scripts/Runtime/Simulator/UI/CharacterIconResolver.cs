using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.Runtime.Simulator.UI
{
    internal static class CharacterIconResolver
    {
        private const string DefaultFallbackIconResourcePath = "UI/Game UI/Textures/999";
        private const string DefaultFallbackIconAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/Game UI/Textures/999.png";
        private const string CharacterAssetRoot = "Assets/HoyoToon/Characters/";

        private static Sprite s_DefaultFallbackIcon;

#if UNITY_EDITOR
        private static readonly Dictionary<string, Sprite> s_SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, string> s_SourceAssetPathCache = new Dictionary<int, string>();
        private static readonly Dictionary<string, string> s_SourceIconPathCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> s_DirectoryIconPathCache = new Dictionary<string, string>();
        private static readonly HashSet<Sprite> s_RuntimeSprites = new HashSet<Sprite>();
        private static readonly List<string> s_CandidateDirectories = new List<string>(8);
        private static readonly HashSet<string> s_CandidateDirectorySet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private static string s_ProjectRoot;
#endif

        public static Sprite ResolveIcon(GameObject model)
        {
            if (model == null)
                return ResolveDefaultFallbackIcon();

#if UNITY_EDITOR
            string iconPath = ResolveIconPath(model);
            if (string.IsNullOrEmpty(iconPath))
                return ResolveDefaultFallbackIcon();

            Sprite icon = LoadCroppedSprite(iconPath);
            return icon != null ? icon : ResolveDefaultFallbackIcon();
#else
            return ResolveDefaultFallbackIcon();
#endif
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ClearCache();
        }

        public static void ClearCache()
        {
            foreach (Sprite sprite in s_RuntimeSprites)
                DestroyRuntimeSprite(sprite);

            s_RuntimeSprites.Clear();
            s_SpriteCache.Clear();
            s_SourceAssetPathCache.Clear();
            s_SourceIconPathCache.Clear();
            s_DirectoryIconPathCache.Clear();
            s_CandidateDirectories.Clear();
            s_CandidateDirectorySet.Clear();
            s_DefaultFallbackIcon = null;
            s_ProjectRoot = null;
        }

        private static string ResolveIconPath(GameObject model)
        {
            string sourceAssetPath = ResolveSourceAssetPath(model);
            if (string.IsNullOrEmpty(sourceAssetPath))
                return null;

            if (s_SourceIconPathCache.TryGetValue(sourceAssetPath, out string cachedIconPath))
                return cachedIconPath;

            string iconPath = ResolveIconPathNearSource(sourceAssetPath);
            s_SourceIconPathCache[sourceAssetPath] = iconPath;
            return iconPath;
        }

        private static string ResolveSourceAssetPath(GameObject model)
        {
            int modelId = model.GetInstanceID();
            if (s_SourceAssetPathCache.TryGetValue(modelId, out string cachedAssetPath))
                return cachedAssetPath;

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model);
            if (!string.IsNullOrEmpty(assetPath))
                return CacheSourceAssetPath(modelId, assetPath);

            Object source = PrefabUtility.GetCorrespondingObjectFromSource(model);
            if (source != null)
            {
                assetPath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(assetPath))
                    return CacheSourceAssetPath(modelId, assetPath);
            }

            SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; ++i)
            {
                Mesh mesh = skinnedRenderers[i] != null ? skinnedRenderers[i].sharedMesh : null;
                assetPath = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(assetPath))
                    return CacheSourceAssetPath(modelId, assetPath);
            }

            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; ++i)
            {
                Mesh mesh = meshFilters[i] != null ? meshFilters[i].sharedMesh : null;
                assetPath = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(assetPath))
                    return CacheSourceAssetPath(modelId, assetPath);
            }

            return CacheSourceAssetPath(modelId, null);
        }

        private static string CacheSourceAssetPath(int modelId, string assetPath)
        {
            s_SourceAssetPathCache[modelId] = assetPath;
            return assetPath;
        }

        private static string ResolveIconPathNearSource(string sourceAssetPath)
        {
            if (string.IsNullOrEmpty(sourceAssetPath))
                return null;

            string sourceDirectory = Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sourceDirectory))
                return null;

            string characterRootDirectory = ResolveCharacterRootDirectory(sourceDirectory);
            s_CandidateDirectories.Clear();
            s_CandidateDirectorySet.Clear();

            try
            {
                string directoryName = Path.GetFileName(sourceDirectory);
                if (string.Equals(directoryName, "Meshes", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(directoryName, "Model", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(directoryName, "Models", System.StringComparison.OrdinalIgnoreCase))
                {
                    string parentDirectory = Path.GetDirectoryName(sourceDirectory)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(parentDirectory))
                        AddCandidateIconDirectory(parentDirectory + "/Icons");
                }

                AddCandidateIconDirectory(sourceDirectory + "/Icons");

                string currentDirectory = sourceDirectory;
                int guard = 0;
                while (!string.IsNullOrEmpty(currentDirectory)
                    && IsSameOrChildDirectory(currentDirectory, characterRootDirectory)
                    && guard++ < 6)
                {
                    AddCandidateIconDirectory(currentDirectory + "/Icons");
                    if (string.Equals(currentDirectory, characterRootDirectory, System.StringComparison.OrdinalIgnoreCase))
                        break;

                    currentDirectory = Path.GetDirectoryName(currentDirectory)?.Replace('\\', '/');
                }

                for (int i = 0; i < s_CandidateDirectories.Count; ++i)
                {
                    string iconPath = ResolveBestRoundIconPath(s_CandidateDirectories[i]);
                    if (!string.IsNullOrEmpty(iconPath))
                        return iconPath;
                }
            }
            finally
            {
                s_CandidateDirectories.Clear();
                s_CandidateDirectorySet.Clear();
            }

            return null;
        }

        private static string ResolveCharacterRootDirectory(string sourceDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory)
                || !sourceDirectory.StartsWith(CharacterAssetRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                return sourceDirectory;
            }

            int gameSlashIndex = sourceDirectory.IndexOf('/', CharacterAssetRoot.Length);
            if (gameSlashIndex < 0)
                return sourceDirectory;

            int characterSlashIndex = sourceDirectory.IndexOf('/', gameSlashIndex + 1);
            return characterSlashIndex < 0
                ? sourceDirectory
                : sourceDirectory.Substring(0, characterSlashIndex);
        }

        private static void AddCandidateIconDirectory(string iconsDirectory)
        {
            if (string.IsNullOrEmpty(iconsDirectory))
                return;

            if (s_CandidateDirectorySet.Add(iconsDirectory))
                s_CandidateDirectories.Add(iconsDirectory);
        }

        private static bool IsSameOrChildDirectory(string directory, string parentDirectory)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(parentDirectory))
                return false;

            return string.Equals(directory, parentDirectory, System.StringComparison.OrdinalIgnoreCase)
                || directory.StartsWith(parentDirectory + "/", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveBestRoundIconPath(string iconsDirectory)
        {
            if (string.IsNullOrEmpty(iconsDirectory))
                return null;

            if (s_DirectoryIconPathCache.TryGetValue(iconsDirectory, out string cachedPath))
                return cachedPath;

            if (!AssetDatabase.IsValidFolder(iconsDirectory))
            {
                s_DirectoryIconPathCache[iconsDirectory] = null;
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { iconsDirectory });
            string bestPath = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < guids.Length; ++i)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                int score = 0;
                if (fileName.StartsWith("Round_", System.StringComparison.OrdinalIgnoreCase))
                    score += 100;
                if (fileName.IndexOf("Round", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 50;
                score -= fileName.Length;

                if (score > bestScore)
                {
                    bestPath = path;
                    bestScore = score;
                }
            }

            s_DirectoryIconPathCache[iconsDirectory] = bestPath;
            return bestPath;
        }

        private static Sprite LoadCroppedSprite(string assetPath)
        {
            if (s_SpriteCache.TryGetValue(assetPath, out Sprite cachedSprite)
                && cachedSprite != null
                && cachedSprite.texture != null)
            {
                return cachedSprite;
            }

            if (cachedSprite != null)
                s_RuntimeSprites.Remove(cachedSprite);
            s_SpriteCache.Remove(assetPath);

            string projectRoot = ResolveProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
                return LoadImportedSprite(assetPath);

            string absolutePath;
            try
            {
                absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            }
            catch
            {
                return LoadImportedSprite(assetPath);
            }

            if (!File.Exists(absolutePath))
                return LoadImportedSprite(assetPath);

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(absolutePath);
            }
            catch
            {
                return LoadImportedSprite(assetPath);
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(assetPath) + "_RuntimeIcon",
                hideFlags = HideFlags.DontSave
            };

            if (!texture.LoadImage(bytes))
            {
                DestroyTexture(texture);
                return LoadImportedSprite(assetPath);
            }

            Rect cropRect = CalculateOpaqueSquareRect(texture);
            Sprite sprite = Sprite.Create(texture, cropRect, new Vector2(0.5f, 0.5f), Mathf.Max(cropRect.width, cropRect.height));
            texture.Apply(false, true);
            sprite.name = Path.GetFileNameWithoutExtension(assetPath) + "_RuntimeIcon";
            sprite.hideFlags = HideFlags.DontSave;
            s_SpriteCache[assetPath] = sprite;
            s_RuntimeSprites.Add(sprite);
            return sprite;
        }

        private static Sprite LoadImportedSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                for (int i = 0; i < assets.Length && sprite == null; ++i)
                    sprite = assets[i] as Sprite;
            }

            if (sprite != null)
                s_SpriteCache[assetPath] = sprite;

            return sprite;
        }

        private static string ResolveProjectRoot()
        {
            if (!string.IsNullOrEmpty(s_ProjectRoot))
                return s_ProjectRoot;

            DirectoryInfo projectRootDirectory = Directory.GetParent(Application.dataPath);
            s_ProjectRoot = projectRootDirectory?.FullName;
            return s_ProjectRoot;
        }

        private static void DestroyRuntimeSprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            Texture texture = sprite.texture;
            if (Application.isPlaying)
            {
                Object.Destroy(sprite);
                DestroyTexture(texture);
            }
            else
            {
                Object.DestroyImmediate(sprite);
                DestroyTexture(texture);
            }
        }

        private static void DestroyTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }

        private static Rect CalculateOpaqueSquareRect(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; ++y)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; ++x)
                {
                    if (pixels[row + x].a <= 8)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                return new Rect(0f, 0f, texture.width, texture.height);

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int size = Mathf.Max(width, height);
            int padding = Mathf.CeilToInt(size * 0.02f);
            size = Mathf.Min(size + padding * 2, Mathf.Max(texture.width, texture.height));

            float centerX = (minX + maxX + 1) * 0.5f;
            float centerY = (minY + maxY + 1) * 0.5f;
            size = Mathf.Min(size, texture.width, texture.height);

            float xMin = Mathf.Clamp(centerX - size * 0.5f, 0f, Mathf.Max(0f, texture.width - size));
            float yMin = Mathf.Clamp(centerY - size * 0.5f, 0f, Mathf.Max(0f, texture.height - size));
            return new Rect(Mathf.Round(xMin), Mathf.Round(yMin), size, size);
        }
#endif

        private static Sprite ResolveDefaultFallbackIcon()
        {
            if (s_DefaultFallbackIcon != null)
                return s_DefaultFallbackIcon;

            s_DefaultFallbackIcon = Resources.Load<Sprite>(DefaultFallbackIconResourcePath);
#if UNITY_EDITOR
            if (s_DefaultFallbackIcon == null)
                s_DefaultFallbackIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultFallbackIconAssetPath);
#endif
            return s_DefaultFallbackIcon;
        }

    }
}

