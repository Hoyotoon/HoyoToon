#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.IO;
using UnityEngine;

namespace HoyoToon.Editor.Resources
{
    internal static class ResourceSyncStorage
    {
        private const string ResourceSyncStorageRelativePath = "Library/HoyoToon/ResourceSync";
        private const string StageFolderName = "staged";
        private const string ManifestFileName = "manifest.json";
        private const string StatusFileName = "status.json";
        private const string LockFileName = "operation.lock.json";

        internal static string ProjectRootPath => EditorPathUtility.ProjectRootPath;

        internal static string GetStorageRootPath(string gameKey)
        {
            return Path.Combine(ProjectRootPath, ToPlatformPath($"{ResourceSyncStorageRelativePath}/{SanitizeFolderName(gameKey)}"));
        }

        internal static string GetStageRootPath(string gameKey)
        {
            return Path.Combine(GetStorageRootPath(gameKey), StageFolderName);
        }

        internal static string GetManifestPath(string gameKey)
        {
            return Path.Combine(GetStorageRootPath(gameKey), ManifestFileName);
        }

        internal static string GetStatusPath(string gameKey)
        {
            return Path.Combine(GetStorageRootPath(gameKey), StatusFileName);
        }

        internal static string GetLockPath(string gameKey)
        {
            return Path.Combine(GetStorageRootPath(gameKey), LockFileName);
        }

        internal static string GetStagedFilePath(string gameKey, string relativePath)
        {
            return Path.Combine(GetStageRootPath(gameKey), ToPlatformPath(relativePath));
        }

        internal static bool TryResolveDestinationRoot(
            Runtime.ScriptableObjects.Resources.HoyoToonResourcesSO resourceAsset,
            out string destinationAssetPath,
            out string destinationAbsolutePath,
            out string error)
        {
            destinationAssetPath = string.Empty;
            destinationAbsolutePath = string.Empty;
            error = null;

            if (resourceAsset == null)
            {
                error = "No HoyoToon resource definition was provided.";
                return false;
            }

            string rawLocalPath = string.IsNullOrWhiteSpace(resourceAsset.LocalPath)
                ? $"Resources/{resourceAsset.Key}"
                : resourceAsset.LocalPath.Trim();

            if (Path.IsPathRooted(rawLocalPath))
            {
                error = $"The local path '{rawLocalPath}' must be project-relative, not absolute.";
                return false;
            }

            string normalized = NormalizeRelativePath(rawLocalPath);
            if (!TryNormalizeManagedRelativePath(normalized, out normalized, out error))
            {
                error = $"The local path '{rawLocalPath}' is invalid: {error}";
                return false;
            }

            if (normalized.StartsWith(HoyoToonApi.PackageRootAssetPath + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, HoyoToonApi.PackageRootAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                destinationAssetPath = normalized;
            }
            else
            {
                destinationAssetPath = HoyoToonApi.PackageRootAssetPath + "/" + normalized;
            }

            string packageRootPath = GetAbsolutePath(HoyoToonApi.PackageRootAssetPath);
            destinationAbsolutePath = GetAbsolutePath(destinationAssetPath);
            if (!IsPathWithinRoot(destinationAbsolutePath, packageRootPath))
            {
                error = $"The local path '{rawLocalPath}' resolves outside the HoyoToon package root.";
                destinationAssetPath = string.Empty;
                destinationAbsolutePath = string.Empty;
                return false;
            }

            return true;
        }

        internal static void EnsureStorageRootExists(string gameKey)
        {
            Directory.CreateDirectory(GetStorageRootPath(gameKey));
        }

        internal static void PrepareStageRoot(string gameKey)
        {
            ClearStageRoot(gameKey);
            Directory.CreateDirectory(GetStageRootPath(gameKey));
        }

        internal static void ClearStageRoot(string gameKey)
        {
            if (string.IsNullOrWhiteSpace(gameKey))
            {
                return;
            }

            string stageRoot = GetStageRootPath(gameKey);
            if (Directory.Exists(stageRoot))
            {
                Directory.Delete(stageRoot, true);
            }
        }

        internal static ResourceSyncManifest ReadManifest(string gameKey)
        {
            ResourceSyncManifest manifest = ReadJson<ResourceSyncManifest>(GetManifestPath(gameKey));
            if (manifest == null)
            {
                return null;
            }

            manifest.shareUrl = manifest.shareUrl ?? string.Empty;
            manifest.resolvedRootUri = manifest.resolvedRootUri ?? string.Empty;
            manifest.files = manifest.files ?? new List<ResourceSyncManifestEntry>();
            return manifest;
        }

        internal static void WriteManifest(string gameKey, ResourceSyncManifest manifest)
        {
            EnsureStorageRootExists(gameKey);
            WriteJson(GetManifestPath(gameKey), manifest ?? new ResourceSyncManifest());
        }

        internal static ResourceSyncStatusSnapshot ReadStatus(string gameKey)
        {
            return ReadJson<ResourceSyncStatusSnapshot>(GetStatusPath(gameKey));
        }

        internal static void WriteStatus(string gameKey, ResourceSyncStatusSnapshot status)
        {
            EnsureStorageRootExists(gameKey);
            WriteJson(GetStatusPath(gameKey), status ?? new ResourceSyncStatusSnapshot());
        }

        internal static ResourceSyncLockState ReadLock(string gameKey)
        {
            return ReadJson<ResourceSyncLockState>(GetLockPath(gameKey));
        }

        internal static void WriteLock(string gameKey, ResourceSyncLockState lockState)
        {
            EnsureStorageRootExists(gameKey);
            WriteJson(GetLockPath(gameKey), lockState ?? new ResourceSyncLockState());
        }

        internal static void ClearLock(string gameKey)
        {
            if (string.IsNullOrWhiteSpace(gameKey))
            {
                return;
            }

            string lockPath = GetLockPath(gameKey);
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }

        internal static Dictionary<string, LocalResourceEntry> ReadLocalFiles(string rootAbsolutePath)
        {
            var files = new Dictionary<string, LocalResourceEntry>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rootAbsolutePath) || !Directory.Exists(rootAbsolutePath))
            {
                return files;
            }

            string normalizedRoot = Path.GetFullPath(rootAbsolutePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;

            foreach (string filePath in Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories))
            {
                if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string normalizedFilePath = Path.GetFullPath(filePath);
                string relativePath = normalizedFilePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? normalizedFilePath.Substring(rootPrefix.Length)
                    : Path.GetFileName(normalizedFilePath);

                if (!TryNormalizeManagedRelativePath(relativePath, out string normalizedRelativePath, out _))
                {
                    continue;
                }

                var fileInfo = new FileInfo(normalizedFilePath);
                files[normalizedRelativePath] = new LocalResourceEntry
                {
                    RelativePath = normalizedRelativePath,
                    Size = fileInfo.Length,
                    LastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
                };
            }

            return files;
        }

        internal static bool TryNormalizeManagedRelativePath(string path, out string normalizedPath, out string error)
        {
            normalizedPath = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Empty paths are not allowed.";
                return false;
            }

            string candidate = NormalizeRelativePath(path).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                error = "Empty paths are not allowed.";
                return false;
            }

            if (candidate.StartsWith("/", StringComparison.Ordinal) || candidate.Contains("://", StringComparison.Ordinal))
            {
                error = "Absolute paths are not allowed.";
                return false;
            }

            string[] segments = candidate.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 0)
            {
                error = "The path must contain at least one segment.";
                return false;
            }

            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                {
                    error = $"The path segment '{segment}' is not allowed.";
                    return false;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = $"The path segment '{segment}' contains invalid filename characters.";
                    return false;
                }
            }

            normalizedPath = string.Join("/", segments);
            return true;
        }

        internal static string NormalizeRelativePath(string path)
        {
            return EditorPathUtility.NormalizeRelativePath(path);
        }

        internal static string ToPlatformPath(string path)
        {
            return EditorPathUtility.ToPlatformPath(path);
        }

        internal static string GetManagedFileAbsolutePath(string rootAbsolutePath, string relativePath)
        {
            return Path.Combine(rootAbsolutePath, ToPlatformPath(relativePath));
        }

        internal static void DeleteEmptyDirectories(string rootAbsolutePath)
        {
            EditorPathUtility.DeleteEmptyDirectories(rootAbsolutePath);
        }

        internal static string GetAbsolutePath(string assetRelativePath)
        {
            return EditorPathUtility.GetAbsolutePath(assetRelativePath);
        }

        private static T ReadJson<T>(string filePath) where T : class
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonUtility.FromJson<T>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void WriteJson<T>(string filePath, T value)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(value, true);
            File.WriteAllText(filePath, json, new UTF8Encoding(false));
        }

        private static string SanitizeFolderName(string value)
        {
            return EditorPathUtility.SanitizeFolderName(value);
        }

        private static bool IsPathWithinRoot(string path, string root)
        {
            return EditorPathUtility.IsPathWithinRoot(path, root);
        }
    }
}
#endif
