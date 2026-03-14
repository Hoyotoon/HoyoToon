#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TopBarConfig = HoyoToon.Editor.UI.Windows.BaseHoyoToonWindow.TopBarConfig;

using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.Windows
{
    public class DialogWindow : BaseHoyoToonWindow
    {
        private bool _isProgressDialog;
        private Action _progressCancelCallback;
        private bool _progressCancelInvoked;
        private bool _suppressProgressCloseCancel;
        private bool _closedViaButton;

        public static void ShowInfo(string title, string message, TopBarConfig topBar = null)
            => ShowOk(title, message, MessageType.Info, topBar);

        public static void ShowWarning(string title, string message, TopBarConfig topBar = null)
            => ShowOk(title, message, MessageType.Warning, topBar);

        public static void ShowError(string title, string message, TopBarConfig topBar = null)
            => ShowOk(title, message, MessageType.Error, topBar);

        public static void ShowOk(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action onOk = null)
        {
            ShowCustom(title, message, type, new[] { "OK" }, defaultIndex: 0, cancelIndex: 0, onResultIndex: i => onOk?.Invoke(), topBar: topBar);
        }

        public static void ShowYesNo(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action<bool> onResult = null)
        {
            ShowCustom(title, message, type, new[] { "Yes", "No" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: i => onResult?.Invoke(i == 0), topBar: topBar);
        }

        public static void ShowOkCancel(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action<bool> onResult = null)
        {
            ShowCustom(title, message, type, new[] { "OK", "Cancel" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: i => onResult?.Invoke(i == 0), topBar: topBar);
        }

        public static void ShowOkWithImage(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action onOk = null, string contentImageResourcePath = null, Texture2D contentImage = null, float contentImageMaxHeight = 220f)
        {
            ShowCustomWithImage(title, message, type, new[] { "OK" }, defaultIndex: 0, cancelIndex: 0, onResultIndex: i => onOk?.Invoke(), topBar: topBar, contentImageResourcePath: contentImageResourcePath, contentImage: contentImage, contentImageMaxHeight: contentImageMaxHeight);
        }

        public static void ShowYesNoWithImage(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action<bool> onResult = null, string contentImageResourcePath = null, Texture2D contentImage = null, float contentImageMaxHeight = 220f)
        {
            ShowCustomWithImage(title, message, type, new[] { "Yes", "No" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: i => onResult?.Invoke(i == 0), topBar: topBar, contentImageResourcePath: contentImageResourcePath, contentImage: contentImage, contentImageMaxHeight: contentImageMaxHeight);
        }

        public static void ShowOkCancelWithImage(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action<bool> onResult = null, string contentImageResourcePath = null, Texture2D contentImage = null, float contentImageMaxHeight = 220f)
        {
            ShowCustomWithImage(title, message, type, new[] { "OK", "Cancel" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: i => onResult?.Invoke(i == 0), topBar: topBar, contentImageResourcePath: contentImageResourcePath, contentImage: contentImage, contentImageMaxHeight: contentImageMaxHeight);
        }

        public static bool ShowYesNoWithImageModal(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, string contentImageResourcePath = null, Texture2D contentImage = null, float contentImageMaxHeight = 220f)
        {
            // Batch-mode/Headless fallback: log and auto-choose Yes
            if (TryHandleHeadlessFallback(title, message, type))
            {
                return true;
            }

            bool result = true;
            var window = CreateConfigured(title, message, type, topBar, i => { result = (i == 0); });

            window._contentImageResourcePath = contentImageResourcePath;
            window._contentImage = contentImage;
            window._contentImageMaxHeight = Mathf.Max(80f, contentImageMaxHeight);

            window._buttons = new List<ButtonDef>
            {
                new ButtonDef { Label = "Yes", Index = 0, IsDefault = true, IsCancel = false },
                new ButtonDef { Label = "No", Index = 1, IsDefault = false, IsCancel = true }
            };

            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowModalUtility();
            return result;
        }

        public static DialogWindow ShowProgress(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action onCancel = null)
        {
            var window = ShowProgressWithCustomButtons(title, message, type, onCancel != null ? new[] { "Cancel" } : new string[0], topBar, onCancel != null ? i => onCancel() : null);
            window?.ConfigureProgressCancellation(onCancel);
            return window;
        }

        private static DialogWindow CreateConfigured(
            string title,
            string message,
            MessageType type,
            TopBarConfig topBar,
            Action<int> onResultIndex,
            bool keepOpenOnClick = false)
        {
            var window = CreateInstance<DialogWindow>();
            window._title = title;
            window._message = message;
            window._type = type;
            window._topBar = topBar ?? TopBarConfig.Default();
            window._onResultIndex = onResultIndex;
            window._keepOpenOnClick = keepOpenOnClick;
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = new Vector2(UiLayout.WINDOW_MIN_WIDTH, UiLayout.WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(UiLayout.WINDOW_DEFAULT_WIDTH, UiLayout.WINDOW_MAX_HEIGHT);

            window._useMarkdown = MarkdownToRichText.LooksLikeMarkdown(message);
            window._renderedMessageCache = null;
            window._pendingResize = true;

            return window;
        }

        public static DialogWindow ShowProgressWithCustomButtons(string title, string message, MessageType type, string[] buttons, TopBarConfig topBar = null, Action<int> onResultIndex = null, bool keepOpenOnClick = false)
        {
            // Batch-mode/Headless fallback: log and return null
            if (TryHandleHeadlessFallback(title, message, type))
            {
                return null;
            }

            var window = CreateConfigured(title, message, type, topBar, onResultIndex, keepOpenOnClick);

            window._showProgressBar = true;
            window._progress = 0f;
            window._progressText = "Starting...";
            window._isProgressDialog = true;
            window._closedViaButton = false;
            window._progressCancelInvoked = false;
            window._suppressProgressCloseCancel = false;

            window._buttons = new List<ButtonDef>();
            if (buttons != null && buttons.Length > 0)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    window._buttons.Add(new ButtonDef
                    {
                        Label = buttons[i],
                        Index = i,
                        IsDefault = false,
                        IsCancel = (i == buttons.Length - 1)
                    });
                }
            }

            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();

            return window;
        }

        internal void ConfigureProgressCancellation(Action onCancel)
        {
            _isProgressDialog = true;
            _progressCancelCallback = onCancel;
        }

        internal void SuppressProgressCloseCancel()
        {
            _suppressProgressCloseCancel = true;
            _progressCancelInvoked = true;
            _progressCancelCallback = null;
        }

        public static void ShowCustom(
            string title,
            string message,
            MessageType type,
            string[] buttons,
            int defaultIndex = 0,
            int cancelIndex = -1,
            Action<int> onResultIndex = null,
            TopBarConfig topBar = null)
        {
            ShowCustomWithImage(title, message, type, buttons, defaultIndex, cancelIndex, onResultIndex, topBar);
        }

        public static void ShowCustomWithImage(
            string title,
            string message,
            MessageType type,
            string[] buttons,
            int defaultIndex = 0,
            int cancelIndex = -1,
            Action<int> onResultIndex = null,
            TopBarConfig topBar = null,
            string contentImageResourcePath = null,
            Texture2D contentImage = null,
            float contentImageMaxHeight = 220f)
        {
            // Batch-mode/Headless fallback: log + auto-resolve to default
            if (TryHandleHeadlessFallback(
                title,
                message,
                type,
                () => onResultIndex?.Invoke(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, (buttons?.Length ?? 1) - 1)))))
            {
                return;
            }

            var window = CreateConfigured(title, message, type, topBar, onResultIndex);

            if (contentImageResourcePath != null || contentImage != null)
            {
                window._contentImageResourcePath = contentImageResourcePath;
                window._contentImage = contentImage;
                window._contentImageMaxHeight = Mathf.Max(80f, contentImageMaxHeight);
            }

            window._buttons = new List<ButtonDef>();
            if (buttons == null || buttons.Length == 0) buttons = new[] { "OK" };
            for (int i = 0; i < buttons.Length; i++)
            {
                window._buttons.Add(new ButtonDef
                {
                    Label = buttons[i],
                    Index = i,
                    IsDefault = (i == defaultIndex),
                    IsCancel = (i == cancelIndex)
                });
            }

            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();
        }

        private static bool TryHandleHeadlessFallback(string title, string message, MessageType type, Action onHeadlessResolved = null)
        {
            if (!Application.isBatchMode)
            {
                return false;
            }

            HoyoToonLogger.Always($"{title}: {message}", GetFallbackLogType(type));
            onHeadlessResolved?.Invoke();
            return true;
        }

        private static LogType GetFallbackLogType(MessageType type)
        {
            switch (type)
            {
                case MessageType.Error:
                    return LogType.Error;
                case MessageType.Warning:
                    return LogType.Warning;
                default:
                    return LogType.Log;
            }
        }

        private string _message;
        private string _renderedMessageCache;
        private string _lastMessageSource;
        private bool _useMarkdown;

        public void SetMessage(string message)
        {
            _message = message ?? string.Empty;
            // Reset markdown cache so layout can recalc
            _renderedMessageCache = null;
            _lastMessageSource = null;
            _useMarkdown = MarkdownToRichText.LooksLikeMarkdown(_message);
            _pendingResize = true;
            Repaint();
        }

        private string _contentImageResourcePath;
        private Texture2D _contentImage;
        private float _contentImageMaxHeight = 220f;

        private float _cachedMsgHeight;
        private float _cachedImgHeight;

        protected override void OnEnable()
        {
            base.OnEnable();
            _useMarkdown = MarkdownToRichText.LooksLikeMarkdown(_message);
            _renderedMessageCache = null;
            _lastMessageSource = null;
        }

        protected virtual void OnDisable()
        {
            TryInvokeProgressCancelOnClose();
        }

        private void TryInvokeProgressCancelOnClose()
        {
            if (!_isProgressDialog || _suppressProgressCloseCancel || _progressCancelInvoked)
            {
                return;
            }

            if (_closedViaButton)
            {
                return;
            }

            _progressCancelInvoked = true;
            _progressCancelCallback?.Invoke();
        }

        protected override void EnsureStyles()
        {
            base.EnsureStyles();
            _messageLabelStyle.richText = _useMarkdown;
        }

        protected override float GetCardToolbarHeight()
        {
            return (EditorStyles.toolbar != null && EditorStyles.toolbar.fixedHeight > 0) ? EditorStyles.toolbar.fixedHeight : UiLayout.TOOLBAR_HEIGHT_FALLBACK;
        }

        protected override void DrawCardToolbar()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EditorGUIUtility.systemCopyBuffer = _message ?? string.Empty;
            }
        }

        protected override float MeasureBodyContentHeight(float contentWidth)
        {
            if (_messageLabelStyle == null) EnsureStyles();
            string measureText = GetRenderedMessage();
            _cachedMsgHeight = string.IsNullOrEmpty(measureText) ? EditorGUIUtility.singleLineHeight : _messageLabelStyle.CalcHeight(new GUIContent(measureText), contentWidth);
            int vpad = _messageContainerStyle != null ? (_messageContainerStyle.padding.top + _messageContainerStyle.padding.bottom) : 0;
            float msgBlockHeight = Mathf.Ceil(_cachedMsgHeight) + vpad + 2f; // +2f safety margin

            _cachedImgHeight = 0f;
            if (HasContentImage())
            {
                var tex = _contentImage;
                if (tex)
                {
                    float aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
                    float targetH = contentWidth / Mathf.Max(0.01f, aspect);
                    _cachedImgHeight = Mathf.Min(_contentImageMaxHeight, targetH);
                }
            }
            float dividerAndSpacing = _cachedImgHeight > 0f ? (6f + 1f + 6f) : 0f;
            return msgBlockHeight + dividerAndSpacing + _cachedImgHeight;
        }

        protected override void DrawBodyContent(float contentWidth)
        {
            using (new EditorGUILayout.VerticalScope(_messageContainerStyle))
            {
                var msg = GetRenderedMessage();
                GUILayout.Label(msg, _messageLabelStyle, GUILayout.Height(_cachedMsgHeight), GUILayout.ExpandWidth(true));
            }

            if (HasContentImage())
            {
                GUILayout.Space(6);
                DrawDivider(1f, 2f);
                GUILayout.Space(6);
                DrawContentImage();
            }
        }

        private bool HasContentImage()
        {
            if (_contentImage != null) return true;
            if (!string.IsNullOrEmpty(_contentImageResourcePath))
            {
                var loaded = Resources.Load<Texture2D>(_contentImageResourcePath);
                if (loaded != null)
                {
                    _contentImage = loaded;
                    _pendingResize = true;
                }
            }
            return _contentImage != null;
        }

        private void DrawContentImage()
        {
            var tex = _contentImage;
            if (!tex) return;

            float aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            float maxH = Mathf.Clamp(_contentImageMaxHeight, 40f, 2000f);

            float contentWidth = _cachedContentWidth > 0 ? _cachedContentWidth : Mathf.Max(200f, position.width - 48f);
            float targetMeasuredH = Mathf.Min(_cachedImgHeight > 0 ? _cachedImgHeight : maxH, maxH);
            Rect r = GUILayoutUtility.GetRect(contentWidth, targetMeasuredH, GUILayout.ExpandWidth(false));

            float targetW = r.width;
            float targetH = targetW / Mathf.Max(0.01f, aspect);
            if (targetH > maxH)
            {
                targetH = maxH;
                targetW = targetH * aspect;
            }

            float x = r.x + (r.width - targetW) * 0.5f;
            float y = r.y + (r.height - targetH) * 0.5f;

            Color bg = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0.03f);
            EditorGUI.DrawRect(r, bg);
            GUI.DrawTexture(new Rect(x, y, targetW, targetH), tex, ScaleMode.ScaleToFit);
        }

        private string GetRenderedMessage()
        {
            string src = _message ?? string.Empty;
            if (!_useMarkdown)
            {
                _lastMessageSource = src;
                return src;
            }
            if (_renderedMessageCache != null && _lastMessageSource == src) return _renderedMessageCache;
            _lastMessageSource = src;
            _renderedMessageCache = MarkdownToRichText.Convert(src);
            return _renderedMessageCache;
        }

        protected override void PreSizeBeforeShow(float initialWidth)
        {
            base.PreSizeBeforeShow(initialWidth);

            try
            {
                var p = position;
                p.height = Mathf.Clamp(p.height + UiLayout.FOOTER_EXTRA_HEIGHT, UiLayout.WINDOW_MIN_HEIGHT, UiLayout.WINDOW_MAX_HEIGHT);
                position = p;

                minSize = new Vector2(minSize.x, Mathf.Min(UiLayout.WINDOW_MAX_HEIGHT, minSize.y + UiLayout.FOOTER_EXTRA_HEIGHT));
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("DialogWindow.PreSize", $"Dialog window pre-size failed: {ex.Message}");
            }
        }

        protected override void CloseWithResult(int index)
        {
            _closedViaButton = true;

            if (_isProgressDialog && !_suppressProgressCloseCancel && !_progressCancelInvoked)
            {
                var clickedButton = _buttons.Find(b => b.Index == index);
                if (clickedButton != null && clickedButton.IsCancel)
                {
                    _progressCancelInvoked = true;
                    _progressCancelCallback?.Invoke();
                }
            }

            base.CloseWithResult(index);
        }
    }
}
#endif
