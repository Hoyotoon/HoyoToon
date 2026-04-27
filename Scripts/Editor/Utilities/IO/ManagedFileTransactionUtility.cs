#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

namespace HoyoToon.Editor.Utilities.IO
{
    internal static class ManagedFileTransactionUtility
    {
        internal static void PrepareDirectory(string directoryPath)
        {
            DeleteDirectoryIfExists(directoryPath);
            Directory.CreateDirectory(directoryPath);
        }

        internal static void CopyFile(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new IOException($"Source file is missing: {sourcePath}");
            }

            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourcePath, destinationPath, true);
        }

        internal static void DeleteFileAndMeta(string filePath)
        {
            DeleteFileIfExists(filePath);
            DeleteFileIfExists(filePath + ".meta");
        }

        internal static void BackupTouchedPaths(
            IEnumerable<string> relativePaths,
            Func<string, string> destinationPathResolver,
            Func<string, string> backupPathResolver)
        {
            foreach (string relativePath in relativePaths ?? Array.Empty<string>())
            {
                string destinationPath = destinationPathResolver(relativePath);
                string backupPath = backupPathResolver(relativePath);
                BackupFileIfExists(destinationPath, backupPath);
                BackupFileIfExists(destinationPath + ".meta", backupPath + ".meta");
            }
        }

        internal static void RollbackTouchedPaths(
            string rootPath,
            IEnumerable<string> relativePaths,
            Func<string, string> destinationPathResolver,
            Func<string, string> backupPathResolver)
        {
            foreach (string relativePath in relativePaths ?? Array.Empty<string>())
            {
                string destinationPath = destinationPathResolver(relativePath);
                string backupPath = backupPathResolver(relativePath);
                RestoreOrDeleteFile(destinationPath, backupPath);
                RestoreOrDeleteFile(destinationPath + ".meta", backupPath + ".meta");
            }

            EditorPathUtility.DeleteEmptyDirectories(rootPath);
        }

        internal static void BackupFileIfExists(string sourcePath, string backupPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(backupPath) || !File.Exists(sourcePath))
            {
                return;
            }

            string backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            File.Copy(sourcePath, backupPath, true);
        }

        internal static void RestoreOrDeleteFile(string destinationPath, string backupPath)
        {
            if (File.Exists(backupPath))
            {
                CopyFile(backupPath, destinationPath);
                return;
            }

            DeleteFileIfExists(destinationPath);
        }

        internal static void DeleteDirectoryIfExists(string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }

        internal static void DeleteFileIfExists(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
#endif
