#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    public static class EditorUtil
    {
        private static readonly Lazy<string> s_ProjectRoot = new Lazy<string>(() => Path.GetFullPath(Path.Combine(Application.dataPath, "..")));

        public static bool LooksLikeJson(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) continue;
                return c == '{' || c == '[';
            }
            return false;
        }

        public static string GetProjectRoot()
        {
            return s_ProjectRoot.Value;
        }

        public static string ToAbsolutePath(string assetOrFsPath)
        {
            if (string.IsNullOrEmpty(assetOrFsPath)) return assetOrFsPath;
            var p = assetOrFsPath.Replace('\\', '/');
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || p.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var combined = Path.Combine(GetProjectRoot(), p.Replace('/', Path.DirectorySeparatorChar));
                return Path.GetFullPath(combined);
            }
            return Path.GetFullPath(assetOrFsPath);
        }

        public static string AbsoluteToUnityPath(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return absPath;
            return ToUnityAssetPath(absPath) ?? Path.GetFullPath(absPath).Replace('\\', '/');
        }

        public static string ToUnityAssetPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return null;
            var projectRoot = GetProjectRoot().Replace('\\', '/');
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) return null;
            return normalized.Substring(projectRoot.Length + 1).Replace('\\', '/');
        }

        public static void EnsureDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            var abs = ToAbsolutePath(dir);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        public static string FindChildDirectoryIgnoreCase(string parentDir, string childName)
        {
            try
            {
                foreach (var d in Directory.EnumerateDirectories(parentDir))
                {
                    if (string.Equals(Path.GetFileName(d), childName, StringComparison.OrdinalIgnoreCase))
                        return d;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("EditorUtil.FindChildDirectoryIgnoreCase", $"Failed to enumerate directories under '{parentDir}': {ex.Message}");
            }
            return null;
        }
        
        public static int DirDistance(string startDir, string targetDir)
        {
            try
            {
                string a = Path.GetFullPath(startDir ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string b = Path.GetFullPath(targetDir ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 0;
                var rel = new Uri(a + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(b + Path.DirectorySeparatorChar)).ToString();
                return rel.Count(ch => ch == '/' || ch == '\\');
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("EditorUtil.DirDistance", $"Failed to compute directory distance from '{startDir}' to '{targetDir}': {ex.Message}");
                return int.MaxValue;
            }
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
#endif
