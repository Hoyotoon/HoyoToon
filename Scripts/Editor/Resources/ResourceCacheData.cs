using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace HoyoToon.Editor.ResourceSystem
{
    [Serializable]
    [DataContract]
    public class ResourceCacheData
    {
        [DataMember(Name = "version")]
        public string Version { get; set; } = "1.0.0";

        [DataMember(Name = "lastUpdateCheck")]
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

        [DataMember(Name = "games")]
        public Dictionary<string, GameResourceData> Games { get; set; } = new Dictionary<string, GameResourceData>();

        [DataMember(Name = "updateCheckInterval")]
        public TimeSpan UpdateCheckInterval { get; set; } = TimeSpan.FromHours(6);

        public GameResourceData GetOrCreateGameData(string gameKey)
        {
            if (!Games.TryGetValue(gameKey, out var gameData))
            {
                gameData = new GameResourceData { GameKey = gameKey };
                Games[gameKey] = gameData;
            }
            return gameData;
        }

        public bool IsUpdateCheckNeeded()
        {
            return DateTime.UtcNow - LastUpdateCheck > UpdateCheckInterval;
        }

        public void MarkUpdateCheckCompleted()
        {
            LastUpdateCheck = DateTime.UtcNow;
        }
    }

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

        public void UpdateFileInfo(string relativePath, CachedFileInfo fileInfo)
        {
            Files[relativePath] = fileInfo;
        }

        public bool IsFileCached(string relativePath)
        {
            return Files.TryGetValue(relativePath, out var fileInfo) && fileInfo.IsValid();
        }

        public CachedFileInfo GetFileInfo(string relativePath)
        {
            return Files.TryGetValue(relativePath, out var fileInfo) ? fileInfo : null;
        }

        public void RemoveFile(string relativePath)
        {
            Files.Remove(relativePath);
        }

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

        public void RefreshAllMetadata()
        {
            foreach (var cachedFile in Files.Values)
            {
                cachedFile.RefreshMetadata();
            }
        }

    }

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

        public bool IsValid()
        {
            if (!CachedValid)
                return false;

            if (!File.Exists(LocalPath))
                return false;

            // If we have a remote ETag, it means the file was successfully downloaded
            // The file is valid as long as it exists locally - Unity processing doesn't invalidate it
            // The only time we need to re-download is when the remote ETag changes (server file updated)
            if (!string.IsNullOrEmpty(RemoteEtag))
            {
                return true;
            }

            // Legacy validation for files without ETag - keep existing size check
            // This handles cases where we have older cache entries without ETags
            var fileInfo = new FileInfo(LocalPath);
            return fileInfo.Length == FileSize;
        }



        public void CalculateChecksum()
        {
            if (File.Exists(LocalPath))
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    using (var stream = File.OpenRead(LocalPath))
                    {
                        var hash = md5.ComputeHash(stream);
                        Checksum = Convert.ToBase64String(hash);
                    }
                }
            }
        }

        public void RefreshMetadata()
        {
            if (!File.Exists(LocalPath))
            {
                CachedValid = false;
                return;
            }

            var fileInfo = new FileInfo(LocalPath);
            FileSize = fileInfo.Length;
            LastModified = fileInfo.LastWriteTime;
            CachedValid = true;

            // Update checksum to match current file state
            // This accounts for any Unity processing that may have changed the file
            CalculateChecksum();
        }
    }
}
