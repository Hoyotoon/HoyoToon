#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HoyoToon.Editor.Resources;
using HoyoToon.Editor.Utilities.Parsing;
using UnityEngine;
using HoyoToon.Editor.Assets;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class AssetDownloadPathUtility
    {
        internal const string DefaultVariantName = "Default";

        internal static bool TryMatchFolder(
            IReadOnlyList<AssetDownloadDirectoryEntry> folderEntries,
            string key,
            string displayName,
            out AssetDownloadDirectoryEntry matchedEntry)
        {
            matchedEntry = null;
            if (folderEntries == null || folderEntries.Count <= 0)
            {
                return false;
            }

            string normalizedKey = NormalizeKey(key);
            string normalizedDisplayName = NormalizeKey(displayName);

            for (int index = 0; index < folderEntries.Count; index++)
            {
                AssetDownloadDirectoryEntry entry = folderEntries[index];
                if (entry == null || !entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                string normalizedFolderName = NormalizeKey(entry.Name);
                if (!string.IsNullOrWhiteSpace(normalizedKey)
                    && string.Equals(normalizedFolderName, normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(normalizedDisplayName)
                    && string.Equals(normalizedFolderName, normalizedDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }
            }

            return false;
        }

        internal static AssetDownloadDirectoryEntry FindFolder(IReadOnlyList<AssetDownloadDirectoryEntry> entries, string targetName)
        {
            if (entries == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                AssetDownloadDirectoryEntry entry = entries[index];
                if (entry != null
                    && entry.IsDirectory
                    && string.Equals(entry.Name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        internal static bool IsHonkaiStarRail(string gameKey, string displayName)
        {
            return ContainsStarRailKey(gameKey) || ContainsStarRailKey(displayName);
        }

        internal static List<RemoteResourceEntry> FilterRemoteFbxByChoice(
            IReadOnlyList<RemoteResourceEntry> fbxFiles,
            AssetDownloadHsrFbxChoice choice)
        {
            var files = fbxFiles?.Where(file => file != null).ToList() ?? new List<RemoteResourceEntry>();
            if (files.Count <= 0 || choice == AssetDownloadHsrFbxChoice.Both)
            {
                return files;
            }

            AssetDownloadFbxVariantType desiredType = choice == AssetDownloadHsrFbxChoice.NoAnims
                ? AssetDownloadFbxVariantType.NoAnims
                : AssetDownloadFbxVariantType.WithAnims;

            List<RemoteResourceEntry> exactMatches = files
                .Where(file => GetFbxVariantType(Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty) == desiredType)
                .ToList();
            if (exactMatches.Count > 0)
            {
                return exactMatches;
            }

            return files
                .Where(file =>
                {
                    AssetDownloadFbxVariantType type = GetFbxVariantType(Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty);
                    return type == AssetDownloadFbxVariantType.Unknown || type == desiredType;
                })
                .ToList();
        }

        internal static AssetDownloadFbxVariantType GetFbxVariantType(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return AssetDownloadFbxVariantType.Unknown;
            }

            List<string> tokens = ExtractTokens(fileName);
            bool hasNo = tokens.Contains("no");
            bool hasWith = tokens.Contains("with");
            bool hasAnim = tokens.Contains("anim")
                || tokens.Contains("anims")
                || tokens.Contains("animation")
                || tokens.Contains("animations");

            if (tokens.Contains("noanim")
                || tokens.Contains("noanims")
                || tokens.Contains("noanimation")
                || tokens.Contains("noanimations")
                || (hasNo && hasAnim))
            {
                return AssetDownloadFbxVariantType.NoAnims;
            }

            if (tokens.Contains("withanim")
                || tokens.Contains("withanims")
                || tokens.Contains("withanimation")
                || tokens.Contains("withanimations")
                || (hasWith && hasAnim)
                || hasAnim)
            {
                return AssetDownloadFbxVariantType.WithAnims;
            }

            return AssetDownloadFbxVariantType.Unknown;
        }

        internal static string BuildAssetTargetPath(
            string assetsRoot,
            AssetDownloadGameOption game,
            string characterName,
            AssetDownloadVariantOption variant)
        {
            var segments = new List<string>
            {
                EnsureProjectAssetsRoot(assetsRoot),
                SanitizeFolderName(game?.FolderName),
                SanitizeFolderName(characterName),
            };

            if (variant != null)
            {
                segments.Add(SanitizeFolderName(variant.Name));
            }

            return string.Join("/", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        internal static string EnsureProjectAssetsRoot(string assetsPath)
        {
            if (string.IsNullOrWhiteSpace(assetsPath))
            {
                return null;
            }

            string normalized = NormalizeRelativePath(assetsPath).TrimEnd('/');
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : null;
        }

        internal static string AbsoluteFromAssetsPath(string assetsPath)
        {
            string normalized = EnsureProjectAssetsRoot(assetsPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            string relativeFromAssets = normalized.Length > "Assets".Length
                ? normalized.Substring("Assets".Length).TrimStart('/')
                : string.Empty;
            return Path.GetFullPath(Path.Combine(Application.dataPath, relativeFromAssets));
        }

        internal static bool TryConvertAbsolutePathToAssetsPath(string absolutePath, out string assetsPath)
        {
            assetsPath = null;
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(absolutePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(fullPath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                assetsPath = "Assets";
                return true;
            }

            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = fullPath.Substring(assetsRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            assetsPath = string.IsNullOrWhiteSpace(relativePath)
                ? "Assets"
                : $"Assets/{relativePath}";
            return true;
        }

        internal static string GetLocalFilePath(string absoluteTargetRoot, string remoteRelativePath)
        {
            string safeRelativePath = ResourceSyncStorage.ToPlatformPath(remoteRelativePath);
            return Path.Combine(absoluteTargetRoot, safeRelativePath);
        }

        internal static bool IsRootLevelFile(RemoteResourceEntry remoteFile)
        {
            return remoteFile != null
                && !string.IsNullOrWhiteSpace(remoteFile.RelativePath)
                && remoteFile.RelativePath.IndexOf('/') < 0
                && remoteFile.RelativePath.IndexOf('\\') < 0;
        }

        internal static string NormalizeKey(string value)
        {
            return StringTokenUtility.NormalizeAlphanumericLower(value);
        }

        internal static string SanitizeFolderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
            }

            return builder.ToString().Trim();
        }

        private static List<string> ExtractTokens(string value)
        {
            return StringTokenUtility.ExtractAlphanumericTokens(value);
        }

        private static string NormalizeRelativePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        private static bool ContainsStarRailKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = NormalizeKey(value);
            return normalized.IndexOf("starrail", StringComparison.Ordinal) >= 0
                || normalized.IndexOf("honkaistarrail", StringComparison.Ordinal) >= 0
                || string.Equals(normalized, "hsr", StringComparison.Ordinal);
        }
    }
}
#endif
