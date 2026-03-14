#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utf8Json;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.ResourceSystem
{
    internal static class ResourceCacheService
    {
        #region Paths

        private static readonly string PackagePath =
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "com.hoyotoon.hoyotoon"));
        internal static readonly string CacheDataPath =
            Path.Combine(PackagePath, "Scripts/Editor/Data", "ResourceCache.json");
        internal static readonly string ResourcesBasePath =
            Path.Combine(PackagePath, "Resources");

        #endregion

        #region State

        private static ResourceCacheData _cacheData;
        private static readonly object _cacheLock = new object();

        #endregion

        #region Public API

        public static ResourceCacheData GetCacheData() => LoadCacheData();

        public static void SaveCacheData()
        {
            lock (_cacheLock)
            {
                try
                {
                    string directoryPath = Path.GetDirectoryName(CacheDataPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    var pretty = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(_cacheData));
                    File.WriteAllBytes(CacheDataPath, pretty);
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource cache data saved successfully.");
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to save resource cache data: {ex.Message}");
                }
            }
        }

        public static void ClearCacheData()
        {
            List<string> pathsToDelete;
            lock (_cacheLock)
            {
                _cacheData = new ResourceCacheData();
                SaveCacheData();

                pathsToDelete = new List<string>();
                foreach (var gameConfig in ResourceConfig.Games.Values)
                {
                    if (string.IsNullOrEmpty(gameConfig.LocalPath)) continue;
                    var localPath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                    if (Directory.Exists(localPath))
                        pathsToDelete.Add(localPath);
                }
            }

            try
            {
                foreach (var localPath in pathsToDelete)
                {
                    Directory.Delete(localPath, true);
                }
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource cache cleared successfully.");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to clear resource cache: {ex.Message}");
            }
        }

        #endregion

        #region Internal

        private static ResourceCacheData LoadCacheData()
        {
            lock (_cacheLock)
            {
                if (_cacheData != null)
                    return _cacheData;

                try
                {
                    if (File.Exists(CacheDataPath))
                    {
                        if (!Api.Parser.TryParseFile<ResourceCacheData>(CacheDataPath, out var data, out var parseError) || data == null)
                        {
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"Failed to parse cache JSON with Utf8Json: {parseError}. Reinitializing cache.");
                            data = new ResourceCacheData();
                        }
                        _cacheData = data;

                        if (PruneCacheToCurrentConfig(_cacheData, out var removed, out var reset, out var removedThumbs))
                        {
                            SaveCacheData();
                            if (removed > 0 || reset > 0 || removedThumbs > 0)
                            {
                                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Resource cache pruned: removed {removed} obsolete game(s), reset {reset} game(s) due to endpoint change, removed {removedThumbs} thumbnail marker entr(y/ies).");
                            }
                        }

                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource cache data loaded successfully.");
                    }
                    else
                    {
                        _cacheData = new ResourceCacheData();
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Created new resource cache data.");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to load resource cache data: {ex.Message}");
                    _cacheData = new ResourceCacheData();
                }

                return _cacheData;
            }
        }

        private static bool PruneCacheToCurrentConfig(ResourceCacheData data, out int removedGames, out int resetGames, out int removedThumbEntries)
        {
            // Safety boundary: prune mutates cache JSON metadata only and never deletes local files on disk.
            removedGames = 0;
            resetGames = 0;
            removedThumbEntries = 0;
            if (data == null || data.Games == null) return false;

            bool changed = false;

            // Always purge thumb marker entries from cache (independent of config availability).
            foreach (var gd in data.Games.Values)
            {
                if (gd?.Files == null || gd.Files.Count == 0) continue;
                var fileKeys = gd.Files.Keys.Where(ResourcePathUtility.IsThumbMarkerFile).ToList();
                if (fileKeys.Count == 0) continue;
                foreach (var fk in fileKeys)
                {
                    gd.Files.Remove(fk);
                    removedThumbEntries++;
                }
                changed = true;
            }

            if (data.Games.Count == 0) return changed;

            Dictionary<string, GameConfig> configGames = null;
            try
            {
                configGames = ResourceConfig.Games;
            }
            catch
            {
                // If config isn't available (e.g., offline / not initialized), don't risk pruning.
                return false;
            }

            if (configGames == null || configGames.Count == 0)
            {
                // Empty config could be a transient state; do not purge cache.
                return false;
            }

            var keys = data.Games.Keys.ToList();
            foreach (var key in keys)
            {
                if (!configGames.ContainsKey(key))
                {
                    data.Games.Remove(key);
                    removedGames++;
                    changed = true;
                }
            }

            foreach (var kvp in configGames)
            {
                var key = kvp.Key;
                var cfg = kvp.Value;
                if (cfg == null) continue;

                if (!data.Games.TryGetValue(key, out var gameData) || gameData == null)
                    continue;

                var cfgUrl = cfg.WebdavUrl ?? string.Empty;
                var cachedUrl = gameData.WebdavUrl ?? string.Empty;
                if (!string.Equals(cfgUrl, cachedUrl, StringComparison.Ordinal))
                {
                    // Endpoint changed: cached ETags/file listing may be invalid.
                    gameData.WebdavUrl = cfg.WebdavUrl;
                    gameData.LastSync = DateTime.MinValue;
                    gameData.Files?.Clear();
                    gameData.TotalDownloaded = 0;
                    gameData.DownloadErrors?.Clear();
                    resetGames++;
                    changed = true;
                }
            }

            return changed;
        }

        #endregion
    }
}
#endif
