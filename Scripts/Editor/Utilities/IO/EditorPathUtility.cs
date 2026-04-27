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

        internal static string ToPlatformPath(string path)
        {
            return NormalizeRelativePath(path).Replace('/', Path.DirectorySeparatorChar);
        }

        internal static string GetAbsolutePath(string assetRelativePath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRootPath, ToPlatformPath(assetRelativePath)));
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

        internal static string SanitizeFolderName(string value, string fallback = "default")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
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
