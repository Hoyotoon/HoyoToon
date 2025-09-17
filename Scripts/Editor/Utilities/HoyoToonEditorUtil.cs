#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Centralized editor-only helper utilities used across Manager scripts.
    /// Consolidates common path, parsing, and enum helpers.
    /// </summary>
    public static class HoyoToonEditorUtil
    {
        // --- JSON heuristics ---
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

        // --- Path helpers ---
        public static string GetProjectRoot()
        {
            // Application.dataPath points to <proj>/Assets
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        /// <summary>
        /// Convert a Unity relative path (Assets/... or Packages/...) or filesystem path to absolute.
        /// </summary>
        public static string ToAbsolutePath(string assetOrFsPath)
        {
            if (string.IsNullOrEmpty(assetOrFsPath)) return assetOrFsPath;
            // Canonicalize separators early
            var p = assetOrFsPath.Replace('\\', '/');
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || p.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var combined = Path.Combine(GetProjectRoot(), p.Replace('/', Path.DirectorySeparatorChar));
                return Path.GetFullPath(combined);
            }
            return Path.GetFullPath(assetOrFsPath);
        }

        /// <summary>
        /// Convert a Unity path (Assets/... or Packages/...) to absolute; passthrough otherwise.
        /// </summary>
        public static string UnityPathToAbsolute(string unityPath)
        {
            if (string.IsNullOrEmpty(unityPath)) return unityPath;
            var p = unityPath.Replace('\\', '/');
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || p.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(GetProjectRoot(), p.Replace('/', Path.DirectorySeparatorChar)));
            }
            return Path.GetFullPath(unityPath);
        }

        /// <summary>
        /// Convert an absolute filesystem path under the project to a Unity project-relative path (e.g., Assets/...).
        /// Returns the input normalized if outside the project.
        /// </summary>
        public static string AbsoluteToUnityPath(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return absPath;
            var norm = Path.GetFullPath(absPath).Replace('\\', '/');
            var projectRoot = GetProjectRoot().Replace('\\', '/');
            var assetsRoot = Application.dataPath.Replace('\\', '/');

            if (norm.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                return "Assets" + norm.Substring(assetsRoot.Length);

            if (norm.StartsWith(projectRoot + "/Packages/", StringComparison.OrdinalIgnoreCase))
            {
                // Keep Packages/... paths when within project
                int idx = norm.IndexOf("/Packages/", StringComparison.OrdinalIgnoreCase);
                return norm.Substring(idx + 1); // strip leading '/'
            }
            return norm;
        }

        /// <summary>
        /// Convert absolute path to project-relative (Assets/... or pass-through when outside).
        /// </summary>
        public static string ToProjectRelative(string absolutePath) => AbsoluteToUnityPath(absolutePath);

        /// <summary>
        /// Ensure the directory exists for a given filesystem path (absolute or relative to project root).
        /// </summary>
        public static void EnsureDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            var abs = ToAbsolutePath(dir);
            if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
        }

        /// <summary>
        /// Find a direct child directory by name (case-insensitive); returns absolute path or null.
        /// </summary>
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
            catch { }
            return null;
        }

        /// <summary>
        /// Rough directory distance heuristic between two directories; used for proximity ranking.
        /// </summary>
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
            catch { return int.MaxValue; }
        }

        // --- Parsing helpers ---
        public static bool EnumTryParseIgnoreCase<TEnum>(string value, out TEnum result) where TEnum : struct
            => Enum.TryParse(value, ignoreCase: true, out result);

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
