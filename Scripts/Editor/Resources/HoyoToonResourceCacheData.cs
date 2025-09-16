using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HoyoToon
{
    /// <summary>
    /// Data structure for tracking cached resources and their metadata
    /// </summary>
    [Serializable]
    [DataContract]
    public class HoyoToonResourceCacheData
    {
        [DataMember(Name = "version")]
        public string Version { get; set; } = "1.0.0";

        [DataMember(Name = "lastUpdateCheck")]
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        [DataMember(Name = "games")]
        public Dictionary<string, GameResourceData> Games { get; set; } = new Dictionary<string, GameResourceData>();

        [DataMember(Name = "updateCheckInterval")]
        public TimeSpan UpdateCheckInterval { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// Get game resource data, creating if doesn't exist
        /// </summary>
        public GameResourceData GetOrCreateGameData(string gameKey)
        {
            if (!Games.TryGetValue(gameKey, out var gameData))
            {
                gameData = new GameResourceData { GameKey = gameKey };
                Games[gameKey] = gameData;
            }
            return gameData;
        }

        /// <summary>
        /// Check if update check is needed based on interval
        /// </summary>
        public bool IsUpdateCheckNeeded()
        {
            return DateTime.UtcNow - LastUpdateCheck > UpdateCheckInterval;
        }

        /// <summary>
        /// Mark update check as completed
        /// </summary>
        public void MarkUpdateCheckCompleted()
        {
            LastUpdateCheck = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Resource data for a specific game
    /// </summary>
    [Serializable]
    [DataContract]
    public class GameResourceData
    {
        [DataMember(Name = "gameKey")]
        public string GameKey { get; set; }

        [DataMember(Name = "webdavUrl")]
        public string WebdavUrl { get; set; }

        [DataMember(Name = "lastSync")]
        public DateTime LastSync { get; set; } = DateTime.MinValue;

        [DataMember(Name = "files")]
        public Dictionary<string, CachedFileInfo> Files { get; set; } = new Dictionary<string, CachedFileInfo>();

        [DataMember(Name = "totalDownloaded")]
        public long TotalDownloaded { get; set; } = 0;
        [DataMember(Name = "downloadErrors")]
        public List<string> DownloadErrors { get; set; } = new List<string>();

        /// <summary>
        /// Add or update file information
        /// </summary>
        public void UpdateFileInfo(string relativePath, CachedFileInfo fileInfo)
        {
            Files[relativePath] = fileInfo;
        }

        /// <summary>
        /// Check if file exists and is valid
        /// </summary>
        public bool IsFileCached(string relativePath)
        {
            return Files.TryGetValue(relativePath, out var fileInfo) && fileInfo.IsValid();
        }

        /// <summary>
        /// Get cached file info
        /// </summary>
        public CachedFileInfo GetFileInfo(string relativePath)
        {
            return Files.TryGetValue(relativePath, out var fileInfo) ? fileInfo : null;
        }

        /// <summary>
        /// Remove file from cache data
        /// </summary>
        public void RemoveFile(string relativePath)
        {
            Files.Remove(relativePath);
        }

        /// <summary>
        /// Get files that need updating
        /// </summary>
        public List<string> GetFilesNeedingUpdate()
        {
            var needUpdate = new List<string>();
            foreach (var kvp in Files)
            {
                if (!kvp.Value.IsValid())
                {
                    needUpdate.Add(kvp.Key);
                }
            }
            return needUpdate;
        }

        /// <summary>
        /// Refresh metadata for all cached files to match current file state
        /// Updates size, timestamp, and checksum for maintenance purposes
        /// </summary>
        public void RefreshAllMetadata()
        {
            foreach (var cachedFile in Files.Values)
            {
                cachedFile.RefreshMetadata();
            }
        }

    }

    /// <summary>
    /// Information about a cached file
    /// </summary>
    [Serializable]
    [DataContract]
    public class CachedFileInfo
    {
        [DataMember(Name = "relativePath")]
        public string RelativePath { get; set; }

        [DataMember(Name = "localPath")]
        public string LocalPath { get; set; }

        [DataMember(Name = "fileSize")]
        public long FileSize { get; set; }

        [DataMember(Name = "checksum")]
        public string Checksum { get; set; }

        [DataMember(Name = "lastModified")]
        public DateTime LastModified { get; set; }

        [DataMember(Name = "downloadDate")]
        public DateTime DownloadDate { get; set; }

        [DataMember(Name = "remoteEtag")]
        public string RemoteEtag { get; set; }

        [DataMember(Name = "isValid")]
        public bool CachedValid { get; set; } = true;

        /// <summary>
        /// Check if cached file is still valid
        /// </summary>
        public bool IsValid()
        {
            if (!CachedValid)
                return false;

            // Check if local file exists - this is the primary validation
            if (!System.IO.File.Exists(LocalPath))
                return false;

            // If we have a remote ETag, it means the file was successfully downloaded
            // The file is valid as long as it exists locally - Unity processing doesn't invalidate it
            // The only time we need to re-download is when the remote ETag changes (server file updated)
            if (!string.IsNullOrEmpty(RemoteEtag))
            {
                return true; // File exists and was downloaded from repo, it's valid regardless of local processing
            }

            // Legacy validation for files without ETag - keep existing size check
            // This handles cases where we have older cache entries without ETags
            var fileInfo = new System.IO.FileInfo(LocalPath);
            return fileInfo.Length == FileSize;
        }



        /// <summary>
        /// Calculate checksum for validation
        /// </summary>
        public void CalculateChecksum()
        {
            if (System.IO.File.Exists(LocalPath))
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    using (var stream = System.IO.File.OpenRead(LocalPath))
                    {
                        var hash = md5.ComputeHash(stream);
                        Checksum = Convert.ToBase64String(hash);
                    }
                }
            }
        }

        /// <summary>
        /// Validate checksum against current file
        /// </summary>
        public bool ValidateChecksum()
        {
            if (string.IsNullOrEmpty(Checksum) || !System.IO.File.Exists(LocalPath))
                return false;

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = System.IO.File.OpenRead(LocalPath))
                {
                    var hash = md5.ComputeHash(stream);
                    var currentChecksum = Convert.ToBase64String(hash);
                    return currentChecksum == Checksum;
                }
            }
        }

        /// <summary>
        /// Refresh cached metadata to match current file state
        /// Updates size, timestamp, and checksum for maintenance purposes
        /// </summary>
        public void RefreshMetadata()
        {
            if (!System.IO.File.Exists(LocalPath))
            {
                CachedValid = false;
                return;
            }

            var fileInfo = new System.IO.FileInfo(LocalPath);
            FileSize = fileInfo.Length;
            LastModified = fileInfo.LastWriteTime;
            CachedValid = true;

            // Update checksum to match current file state
            // This accounts for any Unity processing that may have changed the file
            CalculateChecksum();
        }
    }

    /// <summary>
    /// Progress information for downloads
    /// </summary>
    public class DownloadProgress
    {
        public string GameKey { get; set; }
        public string CurrentFile { get; set; }
        public int FilesCompleted { get; set; }
        public int TotalFiles { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public float OverallProgress => TotalFiles > 0 ? (float)FilesCompleted / TotalFiles : 0f;
        public string StatusMessage { get; set; }
        public bool HasErrors { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Configuration for resource games; now backed by JSON via the local API
    /// for easy iteration and later remote switching.
    /// </summary>
    public static class HoyoToonResourceConfig
    {
        private static Dictionary<string, GameConfig> _games;

        /// <summary>
        /// Returns the games dictionary.
        /// The JSON starts empty and can be edited during development.
        /// </summary>
        public static Dictionary<string, GameConfig> Games
        {
            get
            {
                if (_games == null)
                {
                    var apiGames = HoyoToon.API.HoyoToonApi.GetGames();
                    _games = new Dictionary<string, GameConfig>(apiGames);
                }
                return _games;
            }
        }

        /// <summary>
        /// Force reload from the JSON file, discarding cached data.
        /// </summary>
        public static void Reload()
        {
            HoyoToon.API.HoyoToonApi.ReloadConfig();
            _games = null;
        }
    }

    /// <summary>
    /// Configuration for a specific game
    /// </summary>
    public class GameConfig
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string WebdavUrl { get; set; }
        public string LocalPath { get; set; }
    }
}