#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using System;

namespace HoyoToon.Editor.Utilities
{
    internal static class PackagePath
    {
        private static string s_cachedPath;

        public static string GetPackagePath(string packageName)
        {
            if (s_cachedPath != null) return s_cachedPath;

            var scriptGuid = ResolvePackagePathScriptGuid();

            try
            {
                if (!string.IsNullOrEmpty(scriptGuid))
                {
                    var scriptAssetPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(scriptAssetPath);
                    if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
                        return s_cachedPath = Normalize(info.resolvedPath);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackagePath.ResolveByScript", $"Failed to resolve package path from script asset: {ex.Message}");
            }

            try
            {
                var guess = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", packageName));
                if (Directory.Exists(guess)) return s_cachedPath = Normalize(guess);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackagePath.ResolveByGuess", $"Failed to resolve package path by direct guess: {ex.Message}");
            }

            try
            {
                if (!string.IsNullOrEmpty(scriptGuid))
                {
                    var scriptAssetPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                    var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scriptAssetPath));
                    var dir = new DirectoryInfo(full);
                    while (dir != null)
                    {
                        if (dir.Name == packageName)
                            return s_cachedPath = Normalize(dir.FullName);
                        dir = dir.Parent;
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackagePath.ResolveByAncestor", $"Failed to resolve package path by ancestor scan: {ex.Message}");
            }

            var fallback = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", packageName));
            return s_cachedPath = Normalize(fallback);
        }

        private static string ResolvePackagePathScriptGuid()
        {
            var guids = AssetDatabase.FindAssets("t:Script PackagePath");
            return guids != null && guids.Length > 0 ? guids[0] : null;
        }

        private static string Normalize(string p) => p.Replace("\\", "/");
    }
}
#endif
