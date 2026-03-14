#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.Windows
{
    public static class PopupSystem
    {
        public static event Action<PopupDocument> PopupReceived;

        private const string ShownPrefsKey = PrefsKeys.PopupsShown;
        private static HashSet<string> _shownIds;
        private static readonly Queue<PopupDocument> _pendingPopups = new Queue<PopupDocument>();
        private static readonly HashSet<string> _pendingIds = new HashSet<string>();
        private static double _lastDrain;
        private const double DrainIntervalSeconds = 2.0;
        private static readonly int _mainThreadId;
        private static readonly Dictionary<string, PopupDocument> _shownPopupCache = new Dictionary<string, PopupDocument>();

        static PopupSystem()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [Serializable]
        public class PopupDocument
        {
            public string id;
            public string _id;
            public string title;
            public string message;
            public string type;
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
            public string mode;
            public float? value;
            public float? max;
            public string text;
        }

        internal static bool EnqueuePopup(PopupDocument doc)
        {
            if (doc == null) return false;
            if (!IsMainThread())
            {
                EditorApplication.delayCall += () => EnqueuePopup(doc);
                return false;
            }
            // Normalize Convex-style _id into id to keep dedupe/showOnce behavior stable across payload shapes.
            if (string.IsNullOrEmpty(doc.id) && !string.IsNullOrEmpty(doc._id)) doc.id = doc._id;
            if (_shownIds == null) LoadShown();
            if (string.IsNullOrEmpty(doc.id)) doc.id = Guid.NewGuid().ToString("N");
            bool showOnce = doc.showOnce ?? true;
            // Reject duplicates when showOnce is enabled, both for already-shown and currently-pending popups.
            if (showOnce && _shownIds != null && _shownIds.Contains(doc.id)) return false;
            if (showOnce && _pendingIds.Contains(doc.id)) return false;
            _pendingPopups.Enqueue(doc);
            if (showOnce) _pendingIds.Add(doc.id);
            PopupReceived?.Invoke(doc);
            EditorApplication.update -= Drain;
            EditorApplication.update += Drain;
            return true;
        }

        private static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

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
                DialogWindow.ShowOkWithImage(p.title, p.message, mt, null, null, p.imageResPath, null, maxH);
            }
            else
            {
                DialogWindow.ShowCustomWithImage(p.title, p.message, mt, buttons, 0, -1, null, null, p.imageResPath, null, maxH);
            }
            _shownPopupCache[p.id] = JsonUtility.FromJson<PopupDocument>(JsonUtility.ToJson(p));
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