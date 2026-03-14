#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Utf8Json;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;
using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace HoyoToon.Editor.API
{
    public class JsonConfigService : IConfigService, IDisposable
    {
        private const string PackageName = "com.hoyotoon.hoyotoon";
        private static readonly string ApiFolderRelative = "Scripts/Editor/Data";
        private static readonly string FileName = "HoyoToonAPIConfig.json";

        [Serializable]
        public class APIModel
        {
            public List<GameConfig> Resources { get; set; } = new List<GameConfig>();
            public List<GameMetadata> Games { get; set; } = new List<GameMetadata>();
            public List<ConverterProfile> Converters { get; set; } = new List<ConverterProfile>();
        }

        private string _configPath;
        private APIModel _modelCache;
        private IReadOnlyDictionary<string, GameConfig> _gamesSnapshot;
        private IReadOnlyDictionary<string, GameMetadata> _gameMetadataSnapshot;
        private IReadOnlyDictionary<string, ConverterProfile> _converterProfilesSnapshot;
        private readonly object _modelLock = new object();
        private DateTime _lastDiskWriteUtc;
        private long _lastAppliedRemoteGeneration;
        private static readonly TimeSpan RemoteWriteThrottle = TimeSpan.FromSeconds(60);
        private readonly ConfigWebSocketClient _wsClient = new ConfigWebSocketClient();
        private readonly object _pendingRemoteLock = new object();
        private ConvexMessage _pendingRemoteUpdate;
        private int _pendingRemoteUpdateCount;
        private string _pendingRemoteUpdateLastType;
        private long _pendingRemoteGeneration;
        private readonly ConcurrentQueue<PopupSystem.PopupDocument> _pendingPopups = new ConcurrentQueue<PopupSystem.PopupDocument>();
        private static JsonConfigService _instance;
        private bool _pendingWrite;

        public string ConfigPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_configPath)) return _configPath;
                var packagePath = PackagePath.GetPackagePath(PackageName);
                _configPath = Path.Combine(packagePath, ApiFolderRelative, FileName).Replace("\\", "/");
                EnsureFileExists(_configPath);
                return _configPath;
            }
        }

        public JsonConfigService()
        {
            // Preload so calls before first WS message have data
            try { LoadModel(); }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("JsonConfigService.LoadModel", $"Initial config load failed: {ex.Message}");
            }

            // Wire up WebSocket events and start connection
            _wsClient.OnConfigMessageReceived += OnWsConfigMessage;
            _wsClient.OnPopupReceived += OnWsPopupReceived;
            _wsClient.Connect();
            _instance = this;
        }

        public WebSocketState WebSocketState => _wsClient.State;
        public void Shutdown() => _wsClient.Shutdown();
        public void ForceReconnect() => _wsClient.Connect();

        public void Dispose()
        {
            _wsClient.OnConfigMessageReceived -= OnWsConfigMessage;
            _wsClient.OnPopupReceived -= OnWsPopupReceived;
            _wsClient.Dispose();
        }

        private void OnWsConfigMessage(ConvexMessage msg)
        {
            lock (_pendingRemoteLock)
            {
                _pendingRemoteUpdate = msg;
                _pendingRemoteUpdateCount++;
                _pendingRemoteUpdateLastType = msg.Type;
                _pendingRemoteGeneration++;
            }
        }

        private void OnWsPopupReceived(PopupSystem.PopupDocument popup)
        {
            _pendingPopups.Enqueue(popup);
        }

        public IReadOnlyDictionary<string, GameConfig> GetGames()
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                if (_gamesSnapshot != null)
                    return _gamesSnapshot;

                _gamesSnapshot = BuildSnapshot(_modelCache?.Resources, g => g?.Key);
                return _gamesSnapshot;
            }
        }

        public IReadOnlyDictionary<string, GameMetadata> GetGameMetadata()
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                if (_gameMetadataSnapshot != null)
                    return _gameMetadataSnapshot;

                _gameMetadataSnapshot = BuildSnapshot(_modelCache?.Games, g => g?.Key);
                return _gameMetadataSnapshot;
            }
        }

        public IReadOnlyDictionary<string, ConverterProfile> GetConverterProfiles()
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                if (_converterProfilesSnapshot != null)
                    return _converterProfilesSnapshot;

                _converterProfilesSnapshot = BuildSnapshot(_modelCache?.Converters, p => p?.Key);
                return _converterProfilesSnapshot;
            }
        }

        public void SaveGames(IEnumerable<GameConfig> games)
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                _modelCache.Resources = new List<GameConfig>(games ?? Array.Empty<GameConfig>()); // overwrite section only
                _gamesSnapshot = null;
                WriteModel();
            }
        }

        public void SaveGameMetadata(IEnumerable<GameMetadata> games)
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                _modelCache.Games = new List<GameMetadata>(games ?? Array.Empty<GameMetadata>());
                _gameMetadataSnapshot = null;
                WriteModel();
            }
        }

        public void SaveConverterProfiles(IEnumerable<ConverterProfile> profiles)
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                _modelCache.Converters = new List<ConverterProfile>(profiles ?? Array.Empty<ConverterProfile>());
                _converterProfilesSnapshot = null;
                WriteModel();
            }
        }

        public void Reload()
        {
            lock (_modelLock)
            {
                _modelCache = null;
                ClearSnapshots();
            }
            LoadModel();
        }

        private void EnsureLoaded()
        {
            if (_modelCache == null)
            {
                LoadModel();
            }
        }

        private void LoadModel()
        {
            if (_modelCache != null) return;
            try
            {
                var path = ConfigPath;
                var json = File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
                APIModel model;
                if (json == null || json.Length == 0)
                {
                    model = new APIModel();
                    lock (_modelLock)
                    {
                        _modelCache = model;
                        ClearSnapshots();
                    }
                    WriteModel();
                }
                else
                {
                    model = JsonSerializer.Deserialize<APIModel>(json) ?? new APIModel();
                    lock (_modelLock)
                    {
                        _modelCache = model;
                        ClearSnapshots();
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Error, $"JsonConfigService load failed: {ex.Message}\nUsing empty default model in memory (disk file NOT overwritten).");
                lock (_modelLock)
                {
                    _modelCache = new APIModel();
                    ClearSnapshots();
                }
            }
        }

        private static IReadOnlyDictionary<string, T> BuildSnapshot<T>(IEnumerable<T> source, Func<T, string> keySelector) where T : class
        {
            var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            if (source == null) return map;

            foreach (var item in source)
            {
                var key = keySelector(item);
                if (string.IsNullOrWhiteSpace(key)) continue;
                map[key] = item;
            }

            return map;
        }

        private void ClearSnapshots()
        {
            _gamesSnapshot = null;
            _gameMetadataSnapshot = null;
            _converterProfilesSnapshot = null;
        }

        private void WriteModel()
        {
            try
            {
                APIModel snapshot;
                lock (_modelLock)
                {
                    snapshot = _modelCache ?? new APIModel();
                }
                var path = ConfigPath;
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.PrettyPrint(JsonSerializer.Serialize(snapshot));
                File.WriteAllText(path, json);
                _lastDiskWriteUtc = DateTime.UtcNow;
                void Refresh() { AssetDatabase.Refresh(); }
                if (EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += Refresh;
                }
                else
                {
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Error, $"JsonConfigService write failed: {ex.Message}");
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

        #region WebSocket Message DTO
        [Serializable]
        public class ConvexMessage
        {
            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "games")]
            public List<GameMetadata> Games { get; set; } = new List<GameMetadata>();

            [DataMember(Name = "resources")]
            public List<GameConfig> Resources { get; set; } = new List<GameConfig>();

            [DataMember(Name = "converters")]
            public List<ConverterProfile> Converters { get; set; } = new List<ConverterProfile>();

            [DataMember(Name = "popups")]
            public List<PopupSystem.PopupDocument> Popups { get; set; } = new List<PopupSystem.PopupDocument>();
        }
        #endregion

        #region Pending Remote Application
        private void ApplyPendingRemote()
        {
            ConvexMessage msg;
            int pendingCount;
            string lastType;
            long generation;
            lock (_pendingRemoteLock)
            {
                msg = _pendingRemoteUpdate;
                pendingCount = _pendingRemoteUpdateCount;
                lastType = _pendingRemoteUpdateLastType;
                generation = _pendingRemoteGeneration;
                _pendingRemoteUpdate = null;
                _pendingRemoteUpdateCount = 0;
                _pendingRemoteUpdateLastType = null;
            }

            if (msg != null && (string.Equals(msg.Type, "init", StringComparison.OrdinalIgnoreCase) || string.Equals(msg.Type, "update", StringComparison.OrdinalIgnoreCase)))
            {
                bool changed = false;
                int gamesCount;
                int resourcesCount;
                int convertersCount;
                lock (_modelLock)
                {
                    if (generation > _lastAppliedRemoteGeneration)
                    {
                        if (_modelCache == null) _modelCache = new APIModel();

                        var newGames = msg.Games ?? new List<GameMetadata>();
                        var newResources = msg.Resources ?? new List<GameConfig>();
                        var newConverters = msg.Converters ?? new List<ConverterProfile>();
                        bool hasConverters = newConverters.Count > 0;

                        _modelCache.Games = newGames;
                        _modelCache.Resources = newResources;
                        if (hasConverters)
                            _modelCache.Converters = newConverters;
                        ClearSnapshots();
                        _lastAppliedRemoteGeneration = generation;
                        changed = true;
                    }

                    gamesCount = _modelCache.Games.Count;
                    resourcesCount = _modelCache.Resources.Count;
                    convertersCount = _modelCache.Converters.Count;
                }

                if (changed)
                {
                    _pendingWrite = true;
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"WS applied batched update (lastType='{lastType}', queued={pendingCount}) Games={gamesCount}, Resources={resourcesCount}, Converters={convertersCount}. Scheduling write.");
                }
            }

            // Apply queued popups on main thread.
            int queuedPopups = 0;
            while (_pendingPopups.TryDequeue(out var popup))
            {
                try
                {
                    if (PopupSystem.EnqueuePopup(popup))
                        queuedPopups++;
                }
                catch (Exception ex) { HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Error, $"Popup enqueue failed: {ex.Message}"); }
            }
            if (queuedPopups > 0)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"WS queued {queuedPopups} new popup(s).");
            }
        }
        #endregion

        #region Main Thread Flush
        internal static void FlushLogsAndWrites()
        {
            var inst = _instance;
            // Apply any pending remote updates/popups first so their logs are included this tick.
            if (inst != null)
                inst.ApplyPendingRemote();

            // Flush WS client logs
            inst?._wsClient.FlushLogs();

            // Pending write
            if (inst != null && inst._pendingWrite)
            {
                if (DateTime.UtcNow - inst._lastDiskWriteUtc >= RemoteWriteThrottle)
                {
                    bool shouldWrite;
                    lock (inst._modelLock)
                    {
                        shouldWrite = inst._pendingWrite;
                        inst._pendingWrite = false;
                    }

                    if (shouldWrite)
                    {
                        inst.WriteModel();
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, "Disk cache persisted (flush cycle).");
                    }
                }
            }

            // Flush WS logs again in case the write step triggered new messages.
            inst?._wsClient.FlushLogs();
        }
        #endregion

    }

    [InitializeOnLoad]
    internal static class JsonConfigServiceBootstrap
    {
        private static double _lastHealthCheck;
        private const double HealthIntervalSeconds = 60.0; // periodic check

        static JsonConfigServiceBootstrap()
        {
            // Force service creation
            var _ = HoyoToon.Editor.API.Api.Config;
            EditorApplication.update += HealthUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting += OnQuit;
        }

        private static void HealthUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastHealthCheck < HealthIntervalSeconds) return;
            _lastHealthCheck = now;
            // Flush logs & writes each interval
            JsonConfigService.FlushLogsAndWrites();
            if (HoyoToon.Editor.API.Api.Config is JsonConfigService svc)
            {
                var state = svc.WebSocketState;
                if (state == WebSocketState.Closed || state == WebSocketState.Aborted)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, "WS health check: socket not open; attempting reconnect.");
                    svc.ForceReconnect();
                }
            }
        }

        private static void OnBeforeReload()
        {
            if (HoyoToon.Editor.API.Api.Config is JsonConfigService svc)
            {
                svc.Shutdown();
            }
        }

        private static void OnQuit()
        {
            if (HoyoToon.Editor.API.Api.Config is JsonConfigService svc)
            {
                svc.Dispose();
            }
        }
    }
}
#endif
