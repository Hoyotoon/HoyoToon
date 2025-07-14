using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HoyoToon
{
    /// <summary>
    /// Data structure for tracking cached resources and their metadata
    /// </summary>
    [Serializable]
    public class HoyoToonResourceCacheData
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("lastUpdateCheck")]
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        [JsonProperty("games")]
        public Dictionary<string, GameResourceData> Games { get; set; } = new Dictionary<string, GameResourceData>();

        [JsonProperty("updateCheckInterval")]
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
    public class GameResourceData
    {
        [JsonProperty("gameKey")]
        public string GameKey { get; set; }

        [JsonProperty("webdavUrl")]
        public string WebdavUrl { get; set; }

        [JsonProperty("lastSync")]
        public DateTime LastSync { get; set; } = DateTime.MinValue;

        [JsonProperty("files")]
        public Dictionary<string, CachedFileInfo> Files { get; set; } = new Dictionary<string, CachedFileInfo>();

        [JsonProperty("totalDownloaded")]
        public long TotalDownloaded { get; set; } = 0;

        [JsonProperty("downloadErrors")]
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
    public class CachedFileInfo
    {
        [JsonProperty("relativePath")]
        public string RelativePath { get; set; }

        [JsonProperty("localPath")]
        public string LocalPath { get; set; }

        [JsonProperty("fileSize")]
        public long FileSize { get; set; }

        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonProperty("downloadDate")]
        public DateTime DownloadDate { get; set; }

        [JsonProperty("remoteEtag")]
        public string RemoteEtag { get; set; }

        [JsonProperty("isValid")]
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
    /// Configuration for resource games
    /// </summary>
    public static class HoyoToonResourceConfig
    {
        public static readonly Dictionary<string, GameConfig> Games = new Dictionary<string, GameConfig>
        {
            {
                "Genshin",
                new GameConfig
                {
                    Key = "Genshin",
                    DisplayName = "Genshin Impact",
                    WebdavUrl = "https://assets.hoyotoon.com/s/TQgY4mkTyZEat2b",
                    LocalPath = "Resources/GI"
                }
            },
            {
                "Hi3",
                new GameConfig
                {
                    Key = "Hi3",
                    DisplayName = "Honkai Impact 3rd",
                    WebdavUrl = "https://assets.hoyotoon.com/s/Q7KkrB69ny5kTBL",
                    LocalPath = "Resources/Hi3"
                }
            },
            {
                "StarRail",
                new GameConfig
                {
                    Key = "StarRail",
                    DisplayName = "Honkai: Star Rail",
                    WebdavUrl = "https://assets.hoyotoon.com/s/8RaZzPQyLLEFb7o",
                    LocalPath = "Resources/HSR"
                }
            },
            {
                "Wuwa",
                new GameConfig
                {
                    Key = "Wuwa",
                    DisplayName = "Wuthering Waves",
                    WebdavUrl = "https://assets.hoyotoon.com/s/Y6o9agnZ7xP84sS",
                    LocalPath = "Resources/Wuwa"
                }
            },
            {
                "ZZZ",
                new GameConfig
                {
                    Key = "ZZZ",
                    DisplayName = "Zenless Zone Zero",
                    WebdavUrl = "https://assets.hoyotoon.com/s/BWBcHTK7qmwTnfd",
                    LocalPath = "Resources/ZZZ"
                }
            }
        };
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