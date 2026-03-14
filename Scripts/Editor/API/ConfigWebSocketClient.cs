#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.API
{
    internal sealed class ConfigWebSocketClient : IDisposable
    {
        private const string WebSocketEndpoint = "wss://ws.api.hoyotoon.com/";
        private const int ReconnectBaseDelayMs = 5000;
        private const int ReconnectMaxDelayMs = 60000;
        private static readonly TimeSpan IgnoredTypeLogThrottle = TimeSpan.FromMinutes(5);

        private ClientWebSocket _ws;
        private CancellationTokenSource _wsCts;
        private bool _wsLoopRunning;
        private DateTime _lastEmptyTypeLogUtc;
        private DateTime _lastUnknownTypeLogUtc;
        private bool _disposed;

        private readonly ConcurrentQueue<PendingLog> _logQueue = new ConcurrentQueue<PendingLog>();

        internal struct PendingLog
        {
            public LogLevel Level;
            public string Message;
        }

        public event Action<JsonConfigService.ConvexMessage> OnConfigMessageReceived;
        public event Action<PopupSystem.PopupDocument> OnPopupReceived;
        public WebSocketState State => _ws?.State ?? WebSocketState.None;

        public void Connect()
        {
            if (_wsLoopRunning || _disposed) return;
            _wsLoopRunning = true;
            _wsCts = new CancellationTokenSource();
            Task.Run(() => ConnectLoop(_wsCts.Token));
        }

        public void Shutdown()
        {
            try { _wsCts?.Cancel(); }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("ConfigWebSocketClient.Shutdown", $"WebSocket cancellation failed: {ex.Message}");
            }
            finally
            {
                _wsCts?.Dispose();
                _wsCts = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
            _wsCts?.Dispose();
            _wsCts = null;
        }
        
        public void FlushLogs()
        {
            while (_logQueue.TryDequeue(out var entry))
            {
                switch (entry.Level)
                {
                    case LogLevel.Warning:
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Warning, entry.Message);
                        break;
                    case LogLevel.Error:
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Error, entry.Message);
                        break;
                    default:
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, entry.Message);
                        break;
                }
            }
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            try
            {
                int reconnectAttempt = 0;
                while (!token.IsCancellationRequested)
                {
                    _ws = new ClientWebSocket();
                    try
                    {
                        LogInfo($"Connecting to WS: {WebSocketEndpoint}");
                        await _ws.ConnectAsync(new Uri(WebSocketEndpoint), token);
                        LogInfo("HoyoToon WebSocket connected.");
                        reconnectAttempt = 0; // Reset backoff on successful connection
                        await ReceiveLoop(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogError($"WebSocket connection error: {ex.Message}");
                    }
                    finally
                    {
                        try { _ws?.Dispose(); }
                        catch (Exception ex)
                        {
                            HoyoToonLogger.ThrottleWarning("ConfigWebSocketClient.WSDispose", $"WebSocket dispose failed: {ex.Message}");
                        }
                        _ws = null;
                    }

                    if (token.IsCancellationRequested) break;
                    int delay = Math.Min(ReconnectBaseDelayMs * (1 << Math.Min(reconnectAttempt, 4)), ReconnectMaxDelayMs);
                    LogInfo($"Reconnecting WebSocket in {delay / 1000}s...");
                    try { await Task.Delay(delay, token); } catch { break; }
                    reconnectAttempt++;
                }
            }
            finally
            {
                _wsLoopRunning = false;
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
                        var msg = JsonSerializer.Deserialize<JsonConfigService.ConvexMessage>(payload);
                        if (msg == null)
                        {
                            LogError("WS message deserialized to null object.");
                            continue;
                        }

                        if (string.IsNullOrEmpty(msg.Type))
                        {
                            if (DateTime.UtcNow - _lastEmptyTypeLogUtc >= IgnoredTypeLogThrottle)
                            {
                                _lastEmptyTypeLogUtc = DateTime.UtcNow;
                                LogWarn("WS message received with empty Type (throttled).");
                            }
                        }

                        // Forward popups
                        if (msg.Popups != null && msg.Popups.Count > 0)
                        {
                            foreach (var popup in msg.Popups)
                                OnPopupReceived?.Invoke(popup);
                        }

                        // Forward init/update config messages
                        if (string.Equals(msg.Type, "init", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(msg.Type, "update", StringComparison.OrdinalIgnoreCase))
                        {
                            OnConfigMessageReceived?.Invoke(msg);
                        }
                        else if (!string.IsNullOrEmpty(msg.Type) &&
                                 !string.Equals(msg.Type, "popup", StringComparison.OrdinalIgnoreCase))
                        {
                            if (DateTime.UtcNow - _lastUnknownTypeLogUtc >= IgnoredTypeLogThrottle)
                            {
                                _lastUnknownTypeLogUtc = DateTime.UtcNow;
                                LogInfo($"WS ignored message Type='{msg.Type}' (throttled).");
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

        private void LogInfo(string msg) => _logQueue.Enqueue(new PendingLog { Level = LogLevel.Info, Message = msg });
        private void LogError(string msg) => _logQueue.Enqueue(new PendingLog { Level = LogLevel.Error, Message = msg });
        private void LogWarn(string msg) => _logQueue.Enqueue(new PendingLog { Level = LogLevel.Warning, Message = msg });
    }
}
#endif
