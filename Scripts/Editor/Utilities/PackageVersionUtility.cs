#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    internal static class PackageVersionUtility
    {
        private const string PackageName = "com.hoyotoon.hoyotoon";
        private static readonly Regex VersionPattern = new Regex("\"version\"\\s*:\\s*\"(?<version>[^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static string s_cachedPackageJsonPath;
        private static string s_cachedLocalVersion;
        private static DateTime s_cachedLastWriteUtc;

        public static string GetLocalPackageVersion(bool forceRefresh = false)
        {
            string packageJsonPath = GetPackageJsonPath();
            if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
            {
                s_cachedLocalVersion = "unknown";
                s_cachedLastWriteUtc = DateTime.MinValue;
                return s_cachedLocalVersion;
            }

            DateTime lastWriteUtc;
            try
            {
                lastWriteUtc = File.GetLastWriteTimeUtc(packageJsonPath);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackageVersionUtility.LastWrite", $"Failed to read package.json timestamp: {ex.Message}");
                lastWriteUtc = DateTime.MinValue;
            }

            if (!forceRefresh && !string.IsNullOrEmpty(s_cachedLocalVersion) && s_cachedLastWriteUtc == lastWriteUtc)
            {
                return s_cachedLocalVersion;
            }

            try
            {
                string manifestText = File.ReadAllText(packageJsonPath);
                Match versionMatch = VersionPattern.Match(manifestText);
                if (!versionMatch.Success)
                {
                    HoyoToonLogger.ThrottleWarning("PackageVersionUtility.Parse", "Failed to find a version field in package.json.");
                    s_cachedLocalVersion = "unknown";
                    s_cachedLastWriteUtc = lastWriteUtc;
                    return s_cachedLocalVersion;
                }

                string parsedVersion = versionMatch.Groups["version"].Value.Trim();
                s_cachedLocalVersion = string.IsNullOrWhiteSpace(parsedVersion) ? "unknown" : parsedVersion;
                s_cachedLastWriteUtc = lastWriteUtc;
                return s_cachedLocalVersion;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackageVersionUtility.Read", $"Failed to read local package version: {ex.Message}");
                s_cachedLocalVersion = "unknown";
                s_cachedLastWriteUtc = lastWriteUtc;
                return s_cachedLocalVersion;
            }
        }

        private static string GetPackageJsonPath()
        {
            if (!string.IsNullOrEmpty(s_cachedPackageJsonPath) && File.Exists(s_cachedPackageJsonPath))
            {
                return s_cachedPackageJsonPath;
            }

            try
            {
                string packagePath = PackagePath.GetPackagePath(PackageName);
                if (!string.IsNullOrEmpty(packagePath))
                {
                    string candidate = Path.Combine(packagePath, "package.json");
                    if (File.Exists(candidate))
                    {
                        s_cachedPackageJsonPath = candidate;
                        return s_cachedPackageJsonPath;
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackageVersionUtility.ResolvePath", $"Failed to resolve package.json path: {ex.Message}");
            }

            try
            {
                string fallbackPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", PackageName, "package.json"));
                if (File.Exists(fallbackPath))
                {
                    s_cachedPackageJsonPath = fallbackPath;
                    return s_cachedPackageJsonPath;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("PackageVersionUtility.ResolvePathFallback", $"Failed to resolve fallback package.json path: {ex.Message}");
            }

            return null;
        }
    }
}
#endif