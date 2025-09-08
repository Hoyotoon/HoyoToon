#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Utf8Json;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.API.Services
{
    /// <summary>
    /// JSON-backed implementation of IConfigService.
    /// </summary>
    public class JsonConfigService : IConfigService
    {
        private const string PackageName = "com.meliverse.hoyotoon";
        private static readonly string ApiFolderRelative = "Scripts/Editor";
        private static readonly string FileName = "HoyoToonAPIConfig.json";

        [Serializable]
        public class APIModel
        {
            public List<GameConfig> Resources { get; set; } = new List<GameConfig>();
            public List<GameMetadata> Games { get; set; } = new List<GameMetadata>();
        }

        private string _configPath;
        private APIModel _modelCache;

        public string ConfigPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_configPath)) return _configPath;
                var packagePath = HoyoToonPackagePath.GetPackagePath(PackageName);
                _configPath = Path.Combine(packagePath, ApiFolderRelative, FileName).Replace("\\", "/");
                EnsureFileExists(_configPath);
                return _configPath;
            }
        }

        public IReadOnlyDictionary<string, GameConfig> GetGames()
        {
            // Always reload from disk to ensure latest
            Reload();
            var map = new Dictionary<string, GameConfig>(StringComparer.OrdinalIgnoreCase);
            var list = _modelCache.Resources ?? new List<GameConfig>();
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                map[g.Key] = g;
            }
            return map;
        }

        public IReadOnlyDictionary<string, GameMetadata> GetGameMetadata()
        {
            // Always reload from disk to ensure latest
            Reload();
            var map = new Dictionary<string, GameMetadata>(StringComparer.OrdinalIgnoreCase);
            var list = _modelCache.Games ?? new List<GameMetadata>();
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                map[g.Key] = g;
            }
            return map;
        }

        public void SaveGames(IEnumerable<GameConfig> games)
        {
            LoadModel();
            var items = new List<GameConfig>(games ?? Array.Empty<GameConfig>());
            _modelCache.Resources = items; // write preferred section only
            WriteModel();
            // no cache retained
        }

        public void SaveGameMetadata(IEnumerable<GameMetadata> games)
        {
            LoadModel();
            var items = new List<GameMetadata>(games ?? Array.Empty<GameMetadata>());
            _modelCache.Games = items;
            WriteModel();
            // no cache retained
        }

        public void Reload()
        {
            _modelCache = null;
            LoadModel();
        }

        private void LoadModel()
        {
            if (_modelCache != null) return;
            try
            {
                var path = ConfigPath;
                var json = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
                if (json == null || json.Length == 0)
                {
                    _modelCache = new APIModel();
                    WriteModel();
                }
                else
                {
                    _modelCache = JsonSerializer.Deserialize<APIModel>(json) ?? new APIModel();
                }
                // timestamp not needed when reloading each call
            }
            catch (Exception ex)
            {
                HoyoToonLogger.APIError($"JsonConfigService load failed: {ex.Message}\nCreating a new default file.");
                _modelCache = new APIModel();
                WriteModel();
            }
        }

        private void WriteModel()
        {
            try
            {
                var path = ConfigPath;
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.PrettyPrint(JsonSerializer.Serialize(_modelCache));
                File.WriteAllText(path, json);
                AssetDatabase.Refresh();
                // timestamp not needed when reloading each call
            }
            catch (Exception ex)
            {
                HoyoToonLogger.APIError($"JsonConfigService write failed: {ex.Message}");
            }
        }

        private static void EnsureFileExists(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path))
            {
                var model = new APIModel();
                var json = JsonSerializer.PrettyPrint(JsonSerializer.Serialize(model));
                File.WriteAllText(path, json);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
