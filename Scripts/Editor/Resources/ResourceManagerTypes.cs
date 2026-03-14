#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HoyoToon.Editor.ResourceSystem
{
    public class RemoteFileInfo
    {
        public string RelativePath { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public string ETag { get; set; }
        public bool IsDirectory { get; set; }
    }

    public class ResourceStatus
    {
        public string GameKey { get; set; }
        public string DisplayName { get; set; }
        public bool HasResources { get; set; }
        public bool IsUpToDate { get; set; }
        public DateTime LastSync { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
    }

    public class FileUpdateInfo
    {
        public string GameKey { get; set; }
        public List<string> MissingFiles { get; set; } = new List<string>();
        public List<string> OutdatedFiles { get; set; } = new List<string>();
        public List<string> DeletedFiles { get; set; } = new List<string>();

        public bool HasUpdates => MissingFiles.Count > 0 || OutdatedFiles.Count > 0;
        public bool HasDeletions => DeletedFiles.Count > 0;
        public bool HasChanges => HasUpdates || HasDeletions;
        public int TotalFiles => MissingFiles.Count + OutdatedFiles.Count;
        public int TotalChanges => MissingFiles.Count + OutdatedFiles.Count + DeletedFiles.Count;
    }

    public class GlobalDownloadProgress
    {
        public int TotalGames { get; set; }
        public int CompletedGames { get; set; }
        public Dictionary<string, DownloadProgress> GameProgresses { get; set; } = new Dictionary<string, DownloadProgress>();
        public bool IsCompleted { get; set; }

        public int GetTotalFiles()
        {
            return GameProgresses.Values.Sum(p => p.TotalFiles);
        }

        public int GetTotalCompletedFiles()
        {
            return GameProgresses.Values.Sum(p => p.FilesCompleted);
        }

        public float CalculateOverallProgress()
        {
            var totalFiles = GetTotalFiles();
            if (totalFiles == 0) return 0f;
            return (float)GetTotalCompletedFiles() / totalFiles;
        }

        public string GetProgressSummary()
        {
            var totalFiles = GetTotalFiles();
            if (totalFiles == 0)
            {
                var activeMessages = GameProgresses.Values
                    .Where(p => !string.IsNullOrEmpty(p.StatusMessage) && p.TotalFiles == 0)
                    .Select(p => p.StatusMessage)
                    .Take(2)
                    .ToArray();

                if (activeMessages.Length == 0)
                {
                    return "Initializing downloads...";
                }

                return activeMessages.Length == 1
                    ? activeMessages[0]
                    : $"{activeMessages[0]} +{activeMessages.Length - 1} more...";
            }

            var completedFiles = GetTotalCompletedFiles();
            var progressPercent = (int)(((float)completedFiles / totalFiles) * 100);
            return $"Downloading resources... {completedFiles:N0}/{totalFiles:N0} files ({progressPercent}%)";
        }

        public void UpdateCompletedGames()
        {
            CompletedGames = GameProgresses.Values.Count(p => p.TotalFiles > 0 && p.FilesCompleted >= p.TotalFiles);
        }

        public bool AreFileCountsReady()
        {
            return GameProgresses.Values.All(p => p.TotalFiles > 0);
        }
    }

    public class LocalResourceInfo
    {
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string FullPath { get; set; }
        public string GameKey { get; set; }
        public string FileType { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    internal sealed class SharedOperationProgress
    {
        public int Total { get; }
        public int Completed => _completed;
        public float Progress => Total > 0 ? (float)_completed / Total : 0f;

        private int _completed;

        public SharedOperationProgress(int total)
        {
            Total = Math.Max(0, total);
        }

        public void Increment()
        {
            Interlocked.Increment(ref _completed);
        }
    }

    internal sealed class PrecomputeResult
    {
        public Dictionary<string, List<RemoteFileInfo>> Plan = new Dictionary<string, List<RemoteFileInfo>>();
        public Dictionary<string, Exception> Errors = new Dictionary<string, Exception>();
        public object Gate = new object();
    }
}
#endif
