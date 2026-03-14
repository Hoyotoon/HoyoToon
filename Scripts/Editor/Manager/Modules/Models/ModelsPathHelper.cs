#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal static class ModelsPathHelper
    {
        internal static List<string> FilterLocalFbxByChoice(List<string> fbxs, HsrFbxChoice choice)
        {
            if (fbxs == null || fbxs.Count == 0)
            {
                return fbxs ?? new List<string>();
            }

            if (choice == HsrFbxChoice.Both)
            {
                return fbxs;
            }

            var desired = choice == HsrFbxChoice.NoAnims ? FbxVariantType.NoAnims : FbxVariantType.WithAnims;
            var matches = fbxs.Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                return GetFbxVariantType(name) == desired;
            }).ToList();

            if (matches.Count > 0)
            {
                return matches;
            }

            return fbxs.Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                var type = GetFbxVariantType(name);
                return type == FbxVariantType.Unknown || type == desired;
            }).ToList();
        }

        internal static bool IsHonkaiStarRail(string gameKey, string gameName)
        {
            if (!string.IsNullOrEmpty(gameKey))
            {
                if (gameKey.IndexOf("StarRail", StringComparison.OrdinalIgnoreCase) >= 0
                    || gameKey.IndexOf("HonkaiStarRail", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(gameKey, "HSR", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(gameName))
            {
                return gameName.IndexOf(GameConstants.HonkaiStarRail, StringComparison.OrdinalIgnoreCase) >= 0
                       || gameName.IndexOf("Star Rail", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        internal static List<RemoteFileInfo> FilterFbxFilesByChoice(List<RemoteFileInfo> fbxs, HsrFbxChoice choice)
        {
            if (fbxs == null || fbxs.Count == 0)
            {
                return fbxs ?? new List<RemoteFileInfo>();
            }

            if (choice == HsrFbxChoice.Both)
            {
                return fbxs;
            }

            var desired = choice == HsrFbxChoice.NoAnims ? FbxVariantType.NoAnims : FbxVariantType.WithAnims;
            var matches = fbxs.Where(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty;
                return GetFbxVariantType(fileName) == desired;
            }).ToList();

            if (matches.Count > 0)
            {
                return matches;
            }

            return fbxs.Where(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty;
                var type = GetFbxVariantType(fileName);
                return type == FbxVariantType.Unknown || type == desired;
            }).ToList();
        }

        internal static string BuildFbxLabel(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path) ?? "FBX";
            var fbxType = GetFbxVariantType(fileName);
            string hint = fbxType switch
            {
                FbxVariantType.NoAnims => "(No Anims)",
                FbxVariantType.WithAnims => "(With Anims)",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(hint) ? fileName : $"{fileName} {hint}";
        }

        internal static FbxVariantType GetFbxVariantType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return FbxVariantType.Unknown;
            }

            var tokens = ExtractTokens(fileName);
            bool hasNo = tokens.Contains("no");
            bool hasWith = tokens.Contains("with");
            bool hasAnim = tokens.Contains("anim") || tokens.Contains("anims") || tokens.Contains("animation") || tokens.Contains("animations");

            if (tokens.Contains("noanim") || tokens.Contains("noanims") || tokens.Contains("noanimation") || tokens.Contains("noanimations") || (hasNo && hasAnim))
            {
                return FbxVariantType.NoAnims;
            }

            if (tokens.Contains("withanim") || tokens.Contains("withanims") || tokens.Contains("withanimation") || tokens.Contains("withanimations") || (hasWith && hasAnim) || hasAnim)
            {
                return FbxVariantType.WithAnims;
            }

            return FbxVariantType.Unknown;
        }

        internal static List<string> ExtractTokens(string value)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return tokens;
            }

            var buffer = new StringBuilder();
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    buffer.Append(char.ToLowerInvariant(character));
                }
                else if (buffer.Length > 0)
                {
                    tokens.Add(buffer.ToString());
                    buffer.Clear();
                }
            }

            if (buffer.Length > 0)
            {
                tokens.Add(buffer.ToString());
            }

            return tokens;
        }

        internal static string SelectBestCandidate(List<string> paths, string characterName, string variantName)
        {
            if (paths == null || paths.Count == 0)
            {
                return null;
            }

            string normalizedCharacter = NormalizeKey(characterName);
            string normalizedVariant = NormalizeKey(variantName);
            bool hasVariant = !string.IsNullOrEmpty(normalizedVariant) && !string.Equals(variantName, "Default", StringComparison.OrdinalIgnoreCase);

            string bestPath = null;
            int bestScore = int.MinValue;
            long bestSize = -1;
            int bestDepth = int.MaxValue;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                string normalizedName = NormalizeKey(fileName);

                int score = 0;
                if (!string.IsNullOrEmpty(normalizedCharacter) && normalizedName.Contains(normalizedCharacter))
                {
                    score += 100;
                }

                if (hasVariant && normalizedName.Contains(normalizedVariant))
                {
                    score += 50;
                }

                long size = 0;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch
                {
                    size = 0;
                }

                int depth = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;

                if (score > bestScore
                    || (score == bestScore && size > bestSize)
                    || (score == bestScore && size == bestSize && depth < bestDepth))
                {
                    bestScore = score;
                    bestSize = size;
                    bestDepth = depth;
                    bestPath = path;
                }
            }

            return bestPath ?? paths[0];
        }

        internal static string AbsoluteFromAssetsPath(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var normalized = assetsPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = normalized.Substring("Assets".Length).TrimStart('/');
            return Path.Combine(root, "Assets", relative);
        }

        internal static string EnsureAssetsRoot(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            var normalized = assetsPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized.TrimEnd('/');
        }

        internal static string TrimPrefix(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                return value.TrimStart('/');
            }

            var normalizedValue = value.Replace('\\', '/');
            var normalizedPrefix = prefix.Replace('\\', '/').TrimEnd('/');

            if (normalizedValue.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedValue.Substring(normalizedPrefix.Length).TrimStart('/');
            }

            return normalizedValue.TrimStart('/');
        }

        internal static bool IsRootVariantFile(string variantPath, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var relative = TrimPrefix(filePath, variantPath);
            if (string.IsNullOrEmpty(relative))
            {
                return false;
            }

            var normalized = relative.Replace('\\', '/').TrimStart('/');
            return normalized.IndexOf('/') < 0;
        }

        internal static bool TryConvertToAssetsPath(string absolutePath, out string assetsPath)
        {
            assetsPath = null;
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            var normalized = absolutePath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetsPath = "Assets" + normalized.Substring(dataPath.Length);
            return true;
        }

        internal static string BuildAssetTargetPath(string assetsRoot, GameOption game, string characterName, VariantOption variant)
        {
            var segments = new List<string> { assetsRoot, SanitizeFolderName(game.FolderName), SanitizeFolderName(characterName) };
            if (!string.Equals(variant.Name, "Default", StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(SanitizeFolderName(variant.Name));
            }

            return string.Join("/", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        internal static string SanitizeFolderName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim();
        }

        internal static string CombineSharePath(params string[] parts)
        {
            return string.Join("/", parts.Where(part => !string.IsNullOrEmpty(part))
                .Select(part => part.Trim().Trim('/'))
                .Where(part => !string.IsNullOrEmpty(part)));
        }

        internal static bool TryMatchFolder(List<string> folderNames, GameConfig game, out string folderName)
        {
            folderName = null;
            if (folderNames == null || game == null)
            {
                return false;
            }

            string matchKey = NormalizeKey(game.Key);
            string matchDisplay = NormalizeKey(game.DisplayName);

            foreach (var folder in folderNames)
            {
                var normalizedFolder = NormalizeKey(folder);
                if (!string.IsNullOrEmpty(matchKey) && string.Equals(normalizedFolder, matchKey, StringComparison.OrdinalIgnoreCase))
                {
                    folderName = folder;
                    return true;
                }

                if (!string.IsNullOrEmpty(matchDisplay) && string.Equals(normalizedFolder, matchDisplay, StringComparison.OrdinalIgnoreCase))
                {
                    folderName = folder;
                    return true;
                }
            }

            return false;
        }

        internal static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    normalized.Append(char.ToLowerInvariant(c));
                }
            }

            return normalized.ToString();
        }

        internal static string FindFolderName(IEnumerable<CloudreveClient.RemoteEntryInfo> entries, string target)
        {
            if (entries == null || string.IsNullOrEmpty(target))
            {
                return null;
            }

            return entries.Where(entry => entry.IsDirectory)
                .Select(entry => entry.Name)
                .FirstOrDefault(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif
