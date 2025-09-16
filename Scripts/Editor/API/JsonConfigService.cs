#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Utf8Json;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Collections.Concurrent;

namespace HoyoToon.API.Services
{
    /// <summary>
    /// JSON-backed implementation of IConfigService.
    /// </summary>
    public class JsonConfigService : IConfigService
    {
        // Main thread id captured for safe logging / Unity API access
        private static readonly int MainThreadId;
        static JsonConfigService()
        {
            MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        private const string PackageName = "com.meliverse.hoyotoon";
        private static readonly string ApiFolderRelative = "Scripts/Editor";
        private static readonly string FileName = "HoyoToonAPIConfig.json";
        private const string WebSocketEndpoint = "wss://ws.api.hoyotoon.com/"; // Live updates endpoint

        [Serializable]
        public class APIModel
        {
            public List<GameConfig> Resources { get; set; } = new List<GameConfig>();
            public List<GameMetadata> Games { get; set; } = new List<GameMetadata>();
        }

        private string _configPath;
        private APIModel _modelCache;
        private readonly object _modelLock = new object();
        private DateTime _lastRemoteUpdateUtc;
        private bool _hasRemoteData;
        private DateTime _lastDiskWriteUtc;
        private static readonly TimeSpan RemoteWriteThrottle = TimeSpan.FromSeconds(10); // avoid hammering disk if server pushes frequently

        // WebSocket state
        private ClientWebSocket _ws;
        private CancellationTokenSource _wsCts;
        private bool _wsConnecting;

        // Static instance for bootstrap flush
        private static JsonConfigService _instance;

        // Pending write flag (main thread flush)
        private bool _pendingWrite;

        // Thread-safe log queue to avoid touching Unity logging off main thread
        private struct PendingLog { public string Level; public string Message; }
        private static readonly ConcurrentQueue<PendingLog> _logQueue = new ConcurrentQueue<PendingLog>();

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

        public JsonConfigService()
        {
            // Preload so calls before first WS message have data
            try { LoadModel(); } catch { }
            InitializeWebSocket();
            _instance = this;
        }

        public IReadOnlyDictionary<string, GameConfig> GetGames()
        {
            EnsureLoaded();
            var map = new Dictionary<string, GameConfig>(StringComparer.OrdinalIgnoreCase);
            List<GameConfig> list;
            lock (_modelLock)
            {
                list = _modelCache?.Resources ?? new List<GameConfig>();
            }
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                map[g.Key] = g;
            }
            return map;
        }

        public IReadOnlyDictionary<string, GameMetadata> GetGameMetadata()
        {
            EnsureLoaded();
            var map = new Dictionary<string, GameMetadata>(StringComparer.OrdinalIgnoreCase);
            List<GameMetadata> list;
            lock (_modelLock)
            {
                list = _modelCache?.Games ?? new List<GameMetadata>();
            }
            foreach (var g in list)
            {
                if (string.IsNullOrWhiteSpace(g?.Key)) continue;
                map[g.Key] = g;
            }
            return map;
        }

        public void SaveGames(IEnumerable<GameConfig> games)
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                _modelCache.Resources = new List<GameConfig>(games ?? Array.Empty<GameConfig>()); // overwrite section only
                WriteModel();
            }
        }

        public void SaveGameMetadata(IEnumerable<GameMetadata> games)
        {
            EnsureLoaded();
            lock (_modelLock)
            {
                _modelCache.Games = new List<GameMetadata>(games ?? Array.Empty<GameMetadata>());
                WriteModel();
            }
        }

        /// <summary>
        /// Indicates whether we have received at least one remote (WebSocket) update this editor session.
        /// Callers can decide to trust remote-first when true.
        /// </summary>
        public bool HasRemoteData()
        {
            lock (_modelLock) return _hasRemoteData;
        }

        /// <summary>
        /// Last time (UTC) a remote update was applied. Returns DateTime.MinValue if none.
        /// </summary>
        public DateTime LastRemoteUpdateUtc()
        {
            lock (_modelLock) return _lastRemoteUpdateUtc;
        }

        public void Reload()
        {
            lock (_modelLock)
            {
                _modelCache = null;
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
                    lock (_modelLock) { _modelCache = model; }
                    WriteModel();
                }
                else
                {
                    model = JsonSerializer.Deserialize<APIModel>(json) ?? new APIModel();
                    lock (_modelLock) { _modelCache = model; }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.APIError($"JsonConfigService load failed: {ex.Message}\nCreating a new default file.");
                lock (_modelLock)
                {
                    _modelCache = new APIModel();
                }
                WriteModel();
            }
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
                LogError($"JsonConfigService write failed: {ex.Message}");
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

        #region WebSocket Integration
        [Serializable]
        public class ConvexMessage
        {
            // Primary canonical properties (PascalCase)
            public string Type { get; set; }
            public List<GameMetadata> Games { get; set; } = new List<GameMetadata>();
            public List<GameConfig> Resources { get; set; } = new List<GameConfig>();
            public List<HoyoToonPopupSystem.PopupDocument> Popups { get; set; } = new List<HoyoToonPopupSystem.PopupDocument>();

            // Lowercase variants mapped via DataMember so Utf8Json can bind when server sends camel/lower
            [DataMember(Name = "type")] public string Type_Lower { get => Type; set => Type = value; }
            [DataMember(Name = "games")] public List<GameMetadata> Games_Lower { get => Games; set => Games = value; }
            [DataMember(Name = "resources")] public List<GameConfig> Resources_Lower { get => Resources; set => Resources = value; }
            [DataMember(Name = "popups")] public List<HoyoToonPopupSystem.PopupDocument> Popups_Lower { get => Popups; set => Popups = value; }
        }

        private void InitializeWebSocket()
        {
            if (_ws != null || _wsConnecting) return;
            _wsConnecting = true;
            _wsCts = new CancellationTokenSource();
            Task.Run(async () => await ConnectLoop(_wsCts.Token));
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            const int ReconnectDelayMs = 5000;
            while (!token.IsCancellationRequested)
            {
                _ws = new ClientWebSocket();
                try
                {
                    LogInfo($"Connecting to WS: {WebSocketEndpoint}");
                    await _ws.ConnectAsync(new Uri(WebSocketEndpoint), token);
                    LogInfo("HoyoToon WebSocket connected.");
                    _wsConnecting = false;
                    await ReceiveLoop(token);
                }
                catch (Exception ex)
                {
                    LogError($"WebSocket connection error: {ex.Message}");
                }
                finally
                {
                    try { _ws?.Dispose(); } catch { }
                    _ws = null;
                }
                if (token.IsCancellationRequested) break;
                LogInfo("Reconnecting WebSocket in 5s...");
                try { await Task.Delay(ReconnectDelayMs, token); } catch { break; }
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            while (!token.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", token);
                            return;
                        }
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage && !token.IsCancellationRequested);

                    if (token.IsCancellationRequested) break;
                    var payload = ms.ToArray();
                    try
                    {
                        var msg = JsonSerializer.Deserialize<ConvexMessage>(payload);
                        if (msg == null)
                        {
                            LogError("WS message deserialized to null object.");
                        }
                        else if (string.IsNullOrEmpty(msg.Type))
                        {
                            var raw = System.Text.Encoding.UTF8.GetString(payload);
                            LogInfo($"WS message received with empty Type. Raw (truncated 200): {raw.Substring(0, Math.Min(200, raw.Length))}");
                        }
                        if (msg != null && (string.Equals(msg.Type, "init", StringComparison.OrdinalIgnoreCase) || string.Equals(msg.Type, "update", StringComparison.OrdinalIgnoreCase)))
                        {
                            int gamesCount, resourcesCount;
                            lock (_modelLock)
                            {
                                if (_modelCache == null) _modelCache = new APIModel();
                                _modelCache.Games = msg.Games ?? new List<GameMetadata>();
                                _modelCache.Resources = msg.Resources ?? new List<GameConfig>();
                                gamesCount = _modelCache.Games.Count;
                                resourcesCount = _modelCache.Resources.Count;
                                _hasRemoteData = true;
                                _lastRemoteUpdateUtc = DateTime.UtcNow;
                            }
                            LogInfo($"WS applied '{msg.Type}' update (Games={gamesCount}, Resources={resourcesCount}). Considering cache write.");
                            var now = DateTime.UtcNow;
                            var forceFirst = !_hasRemoteData; // after lock above _hasRemoteData true, so compute before? treat first always writes
                            var shouldWrite = forceFirst || (now - _lastDiskWriteUtc) >= RemoteWriteThrottle;
                            if (shouldWrite)
                            {
                                _pendingWrite = true; // main thread will flush
                            }
                            else
                            {
                                LogInfo("Skipping disk write (throttled) — in-memory model updated.");
                            }
                            // Forward any popups bundled with init/update
                            if (msg.Popups != null && msg.Popups.Count > 0)
                            {
                                foreach (var popup in msg.Popups)
                                {
                                    try { HoyoToon.Utilities.HoyoToonPopupSystem.EnqueuePopup(popup); } catch (Exception ex) { LogError($"Popup enqueue failed: {ex.Message}"); }
                                }
                            }
                        }
                        else if (msg != null && string.Equals(msg.Type, "popup", StringComparison.OrdinalIgnoreCase))
                        {
                            // Dedicated popup push message with popups array
                            if (msg.Popups != null && msg.Popups.Count > 0)
                            {
                                foreach (var popup in msg.Popups)
                                {
                                    try { HoyoToon.Utilities.HoyoToonPopupSystem.EnqueuePopup(popup); } catch (Exception ex) { LogError($"Popup enqueue failed: {ex.Message}"); }
                                }
                                LogInfo($"WS applied popup batch count={msg.Popups.Count}");
                            }
                        }
                        else if (msg != null)
                        {
                            if (!string.IsNullOrEmpty(msg.Type))
                            {
                                var raw = System.Text.Encoding.UTF8.GetString(payload);
                                LogInfo($"WS ignored message Type='{msg.Type}'. Raw (truncated 150): {raw.Substring(0, Math.Min(150, raw.Length))}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to parse WS message: {ex.Message}");
                    }
                }
            }
        }

        ~JsonConfigService()
        {
            try { _wsCts?.Cancel(); } catch { }
        }
        #endregion

        #region Logging Helpers
        private void LogInfo(string msg)  => EnqueueLog("INFO", msg);
        private void LogError(string msg) => EnqueueLog("ERROR", msg);
        private void LogWarn(string msg)  => EnqueueLog("WARN", msg);

        private void EnqueueLog(string level, string message)
        {
            _logQueue.Enqueue(new PendingLog { Level = level, Message = message });
        }

        internal static void FlushLogsAndWrites()
        {
            // Flush logs first
            while (_logQueue.TryDequeue(out var entry))
            {
                switch (entry.Level)
                {
                    case "INFO": HoyoToonLogger.APIInfo(entry.Message); break;
                    case "WARN": HoyoToonLogger.APIWarning(entry.Message); break;
                    case "ERROR": HoyoToonLogger.APIError(entry.Message); break;
                }
            }

            // Pending write
            var inst = _instance;
            if (inst != null && inst._pendingWrite)
            {
                // Double check throttle
                if (DateTime.UtcNow - inst._lastDiskWriteUtc >= RemoteWriteThrottle)
                {
                    lock (inst._modelLock)
                    {
                        inst.WriteModel();
                        inst._pendingWrite = false;
                        inst.LogInfo("Disk cache persisted (flush cycle).");
                    }
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Editor bootstrap to ensure the config service (and thus WebSocket) is alive without waiting
    /// for first API call, plus a lightweight health monitor.
    /// </summary>
    [InitializeOnLoad]
    internal static class JsonConfigServiceBootstrap
    {
        private static double _lastHealthCheck;
        private const double HealthIntervalSeconds = 10.0; // periodic check

        static JsonConfigServiceBootstrap()
        {
            // Force service creation
            var _ = HoyoToon.API.HoyoToonApi.Config;
            EditorApplication.update += HealthUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting += OnQuit;
        }

        private static void HealthUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastHealthCheck < HealthIntervalSeconds) return;
            _lastHealthCheck = now;
            // Always flush logs & writes each interval (could be tuned to every frame if desired)
            JsonConfigService.FlushLogsAndWrites();
            if (HoyoToon.API.HoyoToonApi.Config is JsonConfigService svc)
            {
                var state = svc.GetWebSocketState();
                if (state == WebSocketState.Closed || state == WebSocketState.Aborted)
                {
                    HoyoToonLogger.APIInfo("WS health check: socket not open; attempting reconnect.");
                    svc.ForceReconnect();
                }
            }
        }

        private static void OnBeforeReload()
        {
            if (HoyoToon.API.HoyoToonApi.Config is JsonConfigService svc)
            {
                svc.Shutdown();
            }
        }

        private static void OnQuit()
        {
            OnBeforeReload();
        }
    }

    // Partial extension methods (internal) for bootstrap to control service without exposing on interface
    internal static class JsonConfigServiceExtensions
    {
        internal static WebSocketState GetWebSocketState(this JsonConfigService svc)
        {
            var field = typeof(JsonConfigService).GetField("_ws", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ws = field?.GetValue(svc) as ClientWebSocket;
            return ws?.State ?? WebSocketState.None;
        }

        internal static void ForceReconnect(this JsonConfigService svc)
        {
            var init = typeof(JsonConfigService).GetMethod("InitializeWebSocket", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            init?.Invoke(svc, null);
        }

        internal static void Shutdown(this JsonConfigService svc)
        {
            var ctsField = typeof(JsonConfigService).GetField("_wsCts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ctsField?.GetValue(svc) is CancellationTokenSource cts)
            {
                try { cts.Cancel(); } catch { }
            }
        }
    }
}
#endif
