#if UNITY_EDITOR
using System;
using System.IO;

namespace HoyoToon.Editor.Utilities.IO
{
    internal static class PackagePathUtility
    {
        public static bool TryNormalizePackageRelativePath(string path, out string normalizedPath, out string error)
        {
            normalizedPath = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Empty paths are not allowed.";
                return false;
            }

            string candidate = path.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                error = "Empty paths are not allowed.";
                return false;
            }

            if (candidate.StartsWith("/", StringComparison.Ordinal)
                || candidate.Contains("://", StringComparison.Ordinal)
                || Path.IsPathRooted(candidate)
                || HasDriveSpecifier(candidate))
            {
                error = "Absolute, rooted, and URI paths are not allowed.";
                return false;
            }

            string[] segments = candidate.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                error = "The path must contain at least one segment.";
                return false;
            }

            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < segments.Length; ++i)
            {
                string segment = segments[i];
                if (segment == "." || segment == "..")
                {
                    error = $"The path segment '{segment}' is not allowed.";
                    return false;
                }

                if (segment.IndexOfAny(invalidFileNameChars) >= 0)
                {
                    error = $"The path segment '{segment}' contains invalid filename characters.";
                    return false;
                }
            }

            normalizedPath = string.Join("/", segments);
            return true;
        }

        public static bool TryResolveUnderRoot(
            string rootPath,
            string relativePath,
            out string absolutePath,
            out string error)
        {
            absolutePath = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                error = "The root path is empty.";
                return false;
            }

            if (!TryNormalizePackageRelativePath(relativePath, out string normalizedRelativePath, out error))
            {
                return false;
            }

            string normalizedRoot = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, ToPlatformPath(normalizedRelativePath)))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!IsPathWithinRoot(candidate, normalizedRoot))
            {
                error = "The resolved path escapes the target root.";
                return false;
            }

            absolutePath = candidate;
            return true;
        }

        public static string ToPlatformPath(string normalizedRelativePath)
        {
            return (normalizedRelativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        }

        public static bool IsPathWithinRoot(string path, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            string normalizedPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasDriveSpecifier(string value)
        {
            return value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
        }
    }
}
#endif
