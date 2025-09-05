#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    internal static class HoyoToonPackagePath
    {
        public static string GetPackagePath(string packageName)
        {
            // 1) Try resolving by this script's asset path (works across Unity versions)
            try
            {
                var guids = AssetDatabase.FindAssets("t:Script HoyoToonPackagePath");
                if (guids != null && guids.Length > 0)
                {
                    var scriptAssetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(scriptAssetPath);
                    if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
                        return Normalize(info.resolvedPath);
                }
            }
            catch { }

            // 2) Try direct Packages/<name>
            try
            {
                var guess = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", packageName));
                if (Directory.Exists(guess)) return Normalize(guess);
            }
            catch { }

            // 3) Fallback: parent of this script file up to package folder
            try
            {
                var guids = AssetDatabase.FindAssets("t:Script HoyoToonPackagePath");
                if (guids != null && guids.Length > 0)
                {
                    var scriptAssetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scriptAssetPath));
                    var dir = new DirectoryInfo(full);
                    while (dir != null)
                    {
                        if (dir.Name == packageName)
                            return Normalize(dir.FullName);
                        dir = dir.Parent;
                    }
                }
            }
            catch { }

            // 4) Last resort: return Packages/<name> even if not present
            var fallback = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", packageName));
            return Normalize(fallback);
        }

        private static string Normalize(string p) => p.Replace("\\", "/");
    }
}
#endif
