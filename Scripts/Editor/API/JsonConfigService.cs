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
        private Dictionary<string, GameConfig> _gamesCache;
    private Dictionary<string, GameMetadata> _gameMetadataCache;

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
            if (_gamesCache != null) return _gamesCache;
            LoadModel();
            _gamesCache = new Dictionary<string, GameConfig>(StringComparer.Ordinal);
            var list = _modelCache.Resources ?? new List<GameConfig>();
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                _gamesCache[g.Key] = g;
            }
            return _gamesCache;
        }

        public IReadOnlyDictionary<string, GameMetadata> GetGameMetadata()
        {
            if (_gameMetadataCache != null) return _gameMetadataCache;
            LoadModel();
            _gameMetadataCache = new Dictionary<string, GameMetadata>(StringComparer.Ordinal);
            var list = _modelCache.Games ?? new List<GameMetadata>();
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                _gameMetadataCache[g.Key] = g;
            }
            return _gameMetadataCache;
        }

        public void SaveGames(IEnumerable<GameConfig> games)
        {
            LoadModel();
            var items = new List<GameConfig>(games ?? Array.Empty<GameConfig>());
            _modelCache.Resources = items; // write preferred section only
            WriteModel();
            _gamesCache = null;
        }

        public void SaveGameMetadata(IEnumerable<GameMetadata> games)
        {
            LoadModel();
            var items = new List<GameMetadata>(games ?? Array.Empty<GameMetadata>());
            _modelCache.Games = items;
            WriteModel();
            _gameMetadataCache = null;
        }

        public void Reload()
        {
            _modelCache = null;
            _gamesCache = null;
            _gameMetadataCache = null;
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
