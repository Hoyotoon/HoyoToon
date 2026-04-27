#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HoyoToon.Editor.Updater
{
    internal static class PackageUpdaterPlanBuilder
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        internal static UpdatePlan BuildPlan(string branch, UpdaterManifest manifest, bool cleanMissingFiles)
        {
            Dictionary<string, string> remoteFiles = manifest?.GetFiles() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (remoteFiles.Count == 0)
            {
                throw new InvalidOperationException("Updater manifest did not contain any files.");
            }

            string normalizedBranch = PackageUpdaterStorage.NormalizeBranch(branch);
            string localVersion = PackageUpdaterStorage.GetLocalPackageVersion();
            string remoteVersion = string.IsNullOrWhiteSpace(manifest?.version) ? "unknown" : manifest.version.Trim();
            int versionComparison = CompareVersionStrings(localVersion, remoteVersion);
            bool manifestIncludesMetaFiles = ManifestIncludesMetaFiles(remoteFiles);
            List<string> filesToCopy = BuildCopyList(remoteFiles);
            List<string> filesToDelete = cleanMissingFiles
                ? FindMissingLocalFiles(remoteFiles, manifestIncludesMetaFiles)
                : new List<string>();

            if (versionComparison > 0)
            {
                return new UpdatePlan
                {
                    AvailabilityState = UpdateAvailabilityState.LocalAhead,
                    Branch = normalizedBranch,
                    LocalVersion = localVersion,
                    RemoteVersion = remoteVersion,
                    StatusMessage = BuildLocalAheadStatusMessage(normalizedBranch, localVersion, remoteVersion, filesToCopy.Count, filesToDelete.Count),
                    CleanMissingFiles = cleanMissingFiles,
                    RemoteFiles = remoteFiles,
                    FilesToCopy = filesToCopy,
                    FilesToDelete = filesToDelete,
                };
            }

            if (filesToCopy.Count == 0 && filesToDelete.Count == 0)
            {
                return new UpdatePlan
                {
                    AvailabilityState = UpdateAvailabilityState.UpToDate,
                    Branch = normalizedBranch,
                    LocalVersion = localVersion,
                    RemoteVersion = remoteVersion,
                    StatusMessage = $"HoyoToon is up to date on '{normalizedBranch}' (local {localVersion}, remote {remoteVersion}).",
                    CleanMissingFiles = cleanMissingFiles,
                    RemoteFiles = remoteFiles,
                };
            }

            return new UpdatePlan
            {
                AvailabilityState = UpdateAvailabilityState.UpdateAvailable,
                Branch = normalizedBranch,
                LocalVersion = localVersion,
                RemoteVersion = remoteVersion,
                StatusMessage = BuildStatusMessage(normalizedBranch, localVersion, remoteVersion, versionComparison, filesToCopy.Count, filesToDelete.Count),
                CleanMissingFiles = cleanMissingFiles,
                RemoteFiles = remoteFiles,
                FilesToCopy = filesToCopy,
                FilesToDelete = filesToDelete,
            };
        }

        private static List<string> BuildCopyList(Dictionary<string, string> remoteFiles)
        {
            var filesToCopy = new List<string>();
            foreach (KeyValuePair<string, string> remoteFile in remoteFiles)
            {
                if (!PackageUpdaterStorage.TryNormalizeRelativePath(remoteFile.Key, out string relativePath, out string error))
                {
                    throw new InvalidOperationException($"Updater manifest contains an unsafe path '{remoteFile.Key}': {error}");
                }

                if (!PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out string localPath, out error))
                {
                    throw new InvalidOperationException($"Updater path '{relativePath}' escaped the package root: {error}");
                }

                if (!File.Exists(localPath))
                {
                    filesToCopy.Add(relativePath);
                    continue;
                }

                string localHash = ComputeManifestCompatibleSha1(localPath);
                if (!string.Equals(localHash, remoteFile.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    filesToCopy.Add(relativePath);
                }
            }

            return filesToCopy;
        }

        private static List<string> FindMissingLocalFiles(Dictionary<string, string> remoteFiles, bool manifestIncludesMetaFiles)
        {
            var extras = new List<string>();
            if (!Directory.Exists(PackageUpdaterStorage.PackageRootPath))
            {
                return extras;
            }

            UpdaterKeepRules keepRules = UpdaterKeepRules.Load(PackageUpdaterStorage.PackageRootPath);
            var remoteSet = new HashSet<string>(remoteFiles.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (string localFilePath in Directory.GetFiles(PackageUpdaterStorage.PackageRootPath, "*", SearchOption.AllDirectories))
            {
                if (!manifestIncludesMetaFiles && localFilePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!PackageUpdaterStorage.TryNormalizeRelativePath(GetRelativePath(PackageUpdaterStorage.PackageRootPath, localFilePath), out string relativePath, out _))
                {
                    continue;
                }

                if (keepRules.IsKept(relativePath))
                {
                    continue;
                }

                if (!remoteSet.Contains(relativePath))
                {
                    extras.Add(relativePath);
                }
            }

            return extras;
        }

        internal static string ComputeManifestCompatibleSha1(string filePath)
        {
            return ComputeManifestCompatibleSha1(File.ReadAllBytes(filePath));
        }

        internal static string ComputeManifestCompatibleSha1(byte[] bytes)
        {
            bytes = NormalizeBytesForHash(bytes);
            using SHA1 sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static byte[] NormalizeBytesForHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            bool hasUtf8Bom = HasUtf8Bom(bytes);
            int offset = hasUtf8Bom ? 3 : 0;
            int length = bytes.Length - offset;
            if (length <= 0 || ContainsNullByte(bytes, offset))
            {
                return bytes;
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(bytes, offset, length);
            }
            catch (DecoderFallbackException)
            {
                return bytes;
            }

            string normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
            byte[] normalizedBytes = StrictUtf8.GetBytes(normalizedText);
            if (!hasUtf8Bom)
            {
                return normalizedBytes;
            }

            var combined = new byte[Utf8Bom.Length + normalizedBytes.Length];
            Buffer.BlockCopy(Utf8Bom, 0, combined, 0, Utf8Bom.Length);
            Buffer.BlockCopy(normalizedBytes, 0, combined, Utf8Bom.Length, normalizedBytes.Length);
            return combined;
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= Utf8Bom.Length
                && bytes[0] == Utf8Bom[0]
                && bytes[1] == Utf8Bom[1]
                && bytes[2] == Utf8Bom[2];
        }

        private static bool ContainsNullByte(byte[] bytes, int offset)
        {
            for (int index = offset; index < bytes.Length; index++)
            {
                if (bytes[index] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareVersionStrings(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (TryParseVersionParts(left, out List<int> leftParts) && TryParseVersionParts(right, out List<int> rightParts))
            {
                int maxCount = Math.Max(leftParts.Count, rightParts.Count);
                for (int index = 0; index < maxCount; index++)
                {
                    int leftValue = index < leftParts.Count ? leftParts[index] : 0;
                    int rightValue = index < rightParts.Count ? rightParts[index] : 0;
                    if (leftValue != rightValue)
                    {
                        return leftValue.CompareTo(rightValue);
                    }
                }

                return 0;
            }

            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseVersionParts(string value, out List<int> parts)
        {
            parts = new List<int>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] segments = value.Split('.');
            foreach (string segment in segments)
            {
                if (!int.TryParse(segment, out int parsedValue))
                {
                    parts.Clear();
                    return false;
                }

                parts.Add(parsedValue);
            }

            return parts.Count > 0;
        }

        private static bool ManifestIncludesMetaFiles(Dictionary<string, string> remoteFiles)
        {
            if (remoteFiles == null)
            {
                return false;
            }

            foreach (string relativePath in remoteFiles.Keys)
            {
                if (!string.IsNullOrWhiteSpace(relativePath) && relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildLocalAheadStatusMessage(string branch, string localVersion, string remoteVersion, int fileCopyCount, int fileDeleteCount)
        {
            int totalChanges = fileCopyCount + fileDeleteCount;
            if (totalChanges <= 0)
            {
                return $"Local HoyoToon version {localVersion} is newer than remote branch '{branch}' version {remoteVersion}.";
            }

            return $"Local HoyoToon version {localVersion} is newer than remote branch '{branch}' version {remoteVersion}. The remote file set also differs by {fileCopyCount} update(s) and {fileDeleteCount} removal(s), so the updater will not apply it automatically.";
        }

        private static string BuildStatusMessage(string branch, string localVersion, string remoteVersion, int versionComparison, int fileCopyCount, int fileDeleteCount)
        {
            int totalChanges = fileCopyCount + fileDeleteCount;
            if (totalChanges <= 0)
            {
                return $"HoyoToon is up to date on '{branch}' (local {localVersion}, remote {remoteVersion}).";
            }

            if (versionComparison == 0)
            {
                return $"HoyoToon file repair available on '{branch}': local and remote are both {localVersion}, but {fileCopyCount} file(s) will be synced and {fileDeleteCount} file(s) will be removed.";
            }

            return $"Update available on '{branch}': local {localVersion}, remote {remoteVersion}, {fileCopyCount} file(s) will be updated and {fileDeleteCount} file(s) will be removed.";
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(rootPath));
            Uri fileUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
