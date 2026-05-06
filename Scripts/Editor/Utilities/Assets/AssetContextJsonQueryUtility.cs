#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.Utilities.IO;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Assets
{
    public static class AssetContextJsonQueryUtility
    {
        public static List<string> CollectJsonAssetPaths(IEnumerable<UnityEngine.Object> selectedAssets)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (selectedAssets == null)
            {
                return new List<string>();
            }

            foreach (UnityEngine.Object selectedAsset in selectedAssets)
            {
                string selectedAssetPath = AssetDatabase.GetAssetPath(selectedAsset);
                if (string.IsNullOrWhiteSpace(selectedAssetPath))
                {
                    continue;
                }

                if (selectedAssetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    uniquePaths.Add(NormalizeAssetPath(selectedAssetPath));
                    continue;
                }

                uniquePaths.UnionWith(EnumerateJsonAssetPaths(selectedAssetPath));
            }

            return uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static IEnumerable<string> EnumerateJsonAssetPaths(string contextAssetPath)
        {
            foreach (string assetPath in EnumerateJsonAssetPaths(contextAssetPath, int.MaxValue))
            {
                yield return assetPath;
            }
        }

        public static IEnumerable<string> EnumerateJsonAssetPaths(string contextAssetPath, int maxDepth)
        {
            if (maxDepth < 0 || !TryResolveSearchRootDirectory(contextAssetPath, out string searchRootDirectory))
            {
                yield break;
            }

            foreach (string absoluteJsonPath in EnumerateJsonFilePaths(searchRootDirectory, maxDepth))
            {
                string assetPath = ToAssetPath(absoluteJsonPath);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    yield return assetPath;
                }
            }
        }

        public static bool TryResolveSearchRootDirectory(string contextAssetPath, out string searchRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(contextAssetPath))
            {
                searchRootDirectory = null;
                return false;
            }

            if (AssetDatabase.IsValidFolder(contextAssetPath))
            {
                searchRootDirectory = ToAbsolutePath(contextAssetPath);
                return !string.IsNullOrWhiteSpace(searchRootDirectory) && Directory.Exists(searchRootDirectory);
            }

            string absoluteAssetPath = ToAbsolutePath(contextAssetPath);
            searchRootDirectory = string.IsNullOrWhiteSpace(absoluteAssetPath)
                ? null
                : Path.GetDirectoryName(absoluteAssetPath);
            return !string.IsNullOrWhiteSpace(searchRootDirectory) && Directory.Exists(searchRootDirectory);
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? assetPath
                : EditorPathUtility.NormalizeAssetPath(assetPath);
        }

        public static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            return EditorPathUtility.AbsoluteFromAssetPath(assetPath);
        }

        public static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return null;
            }

            return EditorPathUtility.TryAssetPathFromAbsolute(absolutePath, out string assetPath)
                ? assetPath
                : null;
        }

        private static IEnumerable<string> EnumerateJsonFilePaths(string directoryPath, int remainingDepth)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath) || remainingDepth < 0)
            {
                yield break;
            }

            foreach (string absoluteJsonPath in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                yield return absoluteJsonPath;
            }

            if (remainingDepth == 0)
            {
                yield break;
            }

            foreach (string childDirectoryPath in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string absoluteJsonPath in EnumerateJsonFilePaths(childDirectoryPath, remainingDepth - 1))
                {
                    yield return absoluteJsonPath;
                }
            }
        }
    }
}
#endif
