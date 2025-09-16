#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Central popup ingestion & display system (migrated from HoyoToonApi.Popups).
    /// Lives alongside window utilities because it's purely an editor UI feature.
    /// </summary>
    public static class HoyoToonPopupSystem
    {
        public static event Action<PopupDocument> PopupReceived; // fired before display decision

        private const string ShownPrefsKey = "HoyoToon.Popups.Shown";
        private static HashSet<string> _shownIds;
        private static readonly Queue<PopupDocument> _pendingPopups = new Queue<PopupDocument>();
        private static readonly HashSet<string> _pendingIds = new HashSet<string>();
        private static double _lastDrain;
        private const double DrainIntervalSeconds = 2.0;
        private static int _mainThreadId;
        private static bool _mainThreadCaptured;
        private static readonly Dictionary<string, PopupDocument> _shownPopupCache = new Dictionary<string, PopupDocument>();

        static HoyoToonPopupSystem()
        {
            EditorApplication.update += CaptureMainThread;
        }

        private static void CaptureMainThread()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _mainThreadCaptured = true;
            EditorApplication.update -= CaptureMainThread;
        }

        [Serializable]
        public class PopupDocument
        {
            public string id; // server id
            public string _id; // Convex document id (normalize to id if id null)
            public string title;
            public string message;
            public string type; // info|warning|error|custom
            public long createdAt;
            public bool enabled;
            public long? publishAt;
            public long? expiresAt;
            public bool? showOnce;
            public bool? requireAck;
            public string[] buttons;
            public bool? markdown;
            public int? autoDismissSec;
            public string imageResPath;
            public string imageUrl;
            public float? imageMaxH;
            public string audience;
            public int? priority;
            public string[] tags;
            public string version;
            public ProgressBlock progress;
            public string[] dismissedBy;
            public bool? autoPatchOnDismiss;
        }

        [Serializable]
        public class ProgressBlock
        {
            public string mode; // indeterminate|value
            public float? value;
            public float? max;
            public string text;
        }

        internal static void EnqueuePopup(PopupDocument doc)
        {
            if (doc == null) return;
            if (!_mainThreadCaptured || !IsMainThread())
            {
                EditorApplication.delayCall += () => EnqueuePopup(doc);
                return;
            }
            if (string.IsNullOrEmpty(doc.id) && !string.IsNullOrEmpty(doc._id)) doc.id = doc._id;
            if (_shownIds == null) LoadShown();
            if (string.IsNullOrEmpty(doc.id)) doc.id = Guid.NewGuid().ToString("N");
            bool showOnce = doc.showOnce ?? true;
            if (showOnce && _shownIds != null && _shownIds.Contains(doc.id)) return;
            if (showOnce && _pendingIds.Contains(doc.id)) return;
            _pendingPopups.Enqueue(doc);
            if (showOnce) _pendingIds.Add(doc.id);
            PopupReceived?.Invoke(doc);
            EditorApplication.update -= Drain;
            EditorApplication.update += Drain;
        }

        private static bool IsMainThread() => _mainThreadCaptured && System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        private static void Drain()
        {
            if (EditorApplication.timeSinceStartup - _lastDrain < DrainIntervalSeconds) return;
            _lastDrain = EditorApplication.timeSinceStartup;
            if (_pendingPopups.Count == 0)
            {
                EditorApplication.update -= Drain; return;
            }
            while (_pendingPopups.Count > 0)
            {
                var p = _pendingPopups.Dequeue();
                TryShow(p);
                if (p != null && (p.showOnce ?? true)) _pendingIds.Remove(p.id);
            }
        }

        private static void TryShow(PopupDocument p)
        {
            if (p == null) return;
            if (!p.enabled) return;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (p.publishAt.HasValue && p.publishAt.Value > now) return;
            if (p.expiresAt.HasValue && p.expiresAt.Value <= now) return;
            bool showOnce = p.showOnce ?? true;
            if (showOnce && _shownIds.Contains(p.id)) return;
            ShowDialog(p);
            if (showOnce)
            {
                _shownIds.Add(p.id);
                SaveShown();
            }
        }

        private static void ShowDialog(PopupDocument p)
        {
            var buttons = (p.buttons != null && p.buttons.Length > 0) ? p.buttons : new[] { "OK" };
            var mt = MessageType.Info;
            switch (p.type)
            {
                case "warning": mt = MessageType.Warning; break;
                case "error": mt = MessageType.Error; break;
            }
            float maxH = p.imageMaxH ?? 220f;
            if (buttons.Length == 1 && buttons[0] == "OK")
            {
                HoyoToonDialogWindow.ShowOkWithImage(p.title, p.message, mt, null, null, p.imageResPath, null, maxH);
            }
            else
            {
                HoyoToonDialogWindow.ShowCustomWithImage(p.title, p.message, mt, buttons, 0, -1, null, null, p.imageResPath, null, maxH);
            }
            _shownPopupCache[p.id] = ClonePopupForCache(p);
        }

        private static PopupDocument ClonePopupForCache(PopupDocument p)
        {
            return new PopupDocument
            {
                id = p.id,
                title = p.title,
                message = p.message,
                type = p.type,
                createdAt = p.createdAt,
                enabled = p.enabled,
                publishAt = p.publishAt,
                expiresAt = p.expiresAt,
                showOnce = p.showOnce,
                requireAck = p.requireAck,
                buttons = p.buttons == null ? null : (string[])p.buttons.Clone(),
                markdown = p.markdown,
                autoDismissSec = p.autoDismissSec,
                imageResPath = p.imageResPath,
                imageUrl = p.imageUrl,
                imageMaxH = p.imageMaxH,
                audience = p.audience,
                priority = p.priority,
                tags = p.tags == null ? null : (string[])p.tags.Clone(),
                version = p.version,
                progress = p.progress == null ? null : new ProgressBlock { mode = p.progress.mode, value = p.progress.value, max = p.progress.max, text = p.progress.text },
                dismissedBy = p.dismissedBy == null ? null : (string[])p.dismissedBy.Clone(),
                autoPatchOnDismiss = p.autoPatchOnDismiss
            };
        }

        internal static void UpdatePopupProgress(PopupDocument updated)
        {
            if (updated == null || string.IsNullOrEmpty(updated.id)) return;
            if (!IsMainThread())
            {
                void Deferred() { EditorApplication.update -= Deferred; UpdatePopupProgress(updated); }
                EditorApplication.update += Deferred; return;
            }
            if (_shownPopupCache.TryGetValue(updated.id, out var existing))
            {
                if (updated.progress != null)
                {
                    existing.progress = new ProgressBlock { mode = updated.progress.mode, value = updated.progress.value, max = updated.progress.max, text = updated.progress.text };
                }
                if (!string.IsNullOrEmpty(updated.message)) existing.message = updated.message;
                ShowDialog(existing);
            }
            else
            {
                EnqueuePopup(updated);
            }
        }

        private static void LoadShown()
        {
            _shownIds = new HashSet<string>(EditorPrefs.GetString(ShownPrefsKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void SaveShown()
        {
            if (_shownIds == null) return;
            EditorPrefs.SetString(ShownPrefsKey, string.Join(";", _shownIds));
        }
    }
}
#endif