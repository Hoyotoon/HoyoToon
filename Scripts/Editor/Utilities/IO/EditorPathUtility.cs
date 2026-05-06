#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.IO
{
    internal static class EditorPathUtility
    {
        internal static string ProjectRootPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        internal static string NormalizeRelativePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimStart('/').Trim();
        }

        internal static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }

        internal static string ToPlatformPath(string path)
        {
            return NormalizeRelativePath(path).Replace('/', Path.DirectorySeparatorChar);
        }

        internal static string GetAbsolutePath(string assetRelativePath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRootPath, ToPlatformPath(assetRelativePath)));
        }

        internal static string AbsoluteFromAssetPath(string assetPath)
        {
            return GetAbsolutePath(NormalizeAssetPath(assetPath));
        }

        internal static bool TryAssetPathFromAbsolute(string absolutePath, out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            string normalizedProjectRoot = Path.GetFullPath(ProjectRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            string normalizedAbsolutePath = Path.GetFullPath(absolutePath).Replace('\\', '/');

            if (!string.Equals(normalizedAbsolutePath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase)
                && !normalizedAbsolutePath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = normalizedAbsolutePath.Length == normalizedProjectRoot.Length
                ? string.Empty
                : normalizedAbsolutePath.Substring(normalizedProjectRoot.Length + 1);
            string normalizedRelativePath = NormalizeAssetPath(relativePath);
            if (string.IsNullOrWhiteSpace(normalizedRelativePath))
            {
                return false;
            }

            assetPath = normalizedRelativePath;
            return true;
        }

        internal static bool IsPathWithinRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            string normalizedPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        internal static string EnsurePathWithinRoot(string path, string root)
        {
            string normalizedPath = Path.GetFullPath(path);
            if (!IsPathWithinRoot(normalizedPath, root))
            {
                throw new IOException($"Path '{path}' resolved outside root '{root}'.");
            }

            return normalizedPath;
        }

        internal static string SanitizeFolderName(string value, string fallback = "default")
        {
            return SanitizeFileName(value, fallback);
        }

        internal static string SanitizeFileName(string value, string fallback = "default")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) || sanitized == "." || sanitized == ".."
                ? fallback
                : sanitized;
        }

        internal static void DeleteEmptyDirectories(string rootAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(rootAbsolutePath) || !Directory.Exists(rootAbsolutePath))
            {
                return;
            }

            foreach (string directoryPath in Directory
                .GetDirectories(rootAbsolutePath, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length))
            {
                if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    continue;
                }

                Directory.Delete(directoryPath, false);

                string metaPath = directoryPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
        }
    }
}
#endif
