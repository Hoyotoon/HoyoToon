#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
// Alias the base TopBarConfig so this window uses the shared type
using TopBarConfig = HoyoToon.Utilities.BaseHoyoToonWindow.TopBarConfig;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Dialog window for messages, prompts, and progress with optional themed banner.
    /// Static helpers provide common patterns (OK, Yes/No, custom buttons, progress).
    /// </summary>
    public class HoyoToonDialogWindow : BaseHoyoToonWindow
    {
        // inherit UiLayout and TopBarConfig from base (aliased above)
        // inherit ButtonDef from base

    // Public API: lightweight wrappers
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

        // Image-enabled wrappers
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

        // Modal (blocking) helpers so callers can gate batch operations without Unity's EditorUtility
        public static bool ShowYesNoWithImageModal(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, string contentImageResourcePath = null, Texture2D contentImage = null, float contentImageMaxHeight = 220f)
        {
            // Batch-mode/Headless fallback: log and auto-choose Yes
            if (Application.isBatchMode)
            {
                HoyoToonLogCore.LogAlways($"{title}: {message}", type == MessageType.Error ? LogType.Error : type == MessageType.Warning ? LogType.Warning : LogType.Log);
                return true;
            }

            bool result = true; // default to Yes
            var window = CreateInstance<HoyoToonDialogWindow>();
            window._title = title;
            window._message = message;
            window._type = type;
            window._topBar = topBar ?? TopBarConfig.Default();
            window._onResultIndex = i => { result = (i == 0); };
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = new Vector2(UiLayout.WINDOW_MIN_WIDTH, UiLayout.WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(UiLayout.WINDOW_DEFAULT_WIDTH, UiLayout.WINDOW_MAX_HEIGHT);

            window._contentImageResourcePath = contentImageResourcePath;
            window._contentImage = contentImage;
            window._contentImageMaxHeight = Mathf.Max(80f, contentImageMaxHeight);

            window._buttons = new List<ButtonDef>
            {
                new ButtonDef { Label = "Yes", Index = 0, IsDefault = true, IsCancel = false },
                new ButtonDef { Label = "No", Index = 1, IsDefault = false, IsCancel = true }
            };

            // Auto-detect markdown per message content
            window._useMarkdown = window.LooksLikeMarkdown(message);
            window._renderedMessageCache = null;
            window._pendingResize = true;

            // Pre-size and show modally (blocks until closed)
            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowModalUtility();
            return result;
        }

        // Progress dialogs
        public static HoyoToonDialogWindow ShowProgress(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action onCancel = null)
        {
            return ShowProgressWithCustomButtons(title, message, type, onCancel != null ? new[] { "Cancel" } : new string[0], topBar, onCancel != null ? i => onCancel() : null);
        }

    public static HoyoToonDialogWindow ShowProgressWithCustomButtons(string title, string message, MessageType type, string[] buttons, TopBarConfig topBar = null, Action<int> onResultIndex = null, bool keepOpenOnClick = false)
        {
            // Batch-mode/Headless fallback: log and return null
            if (Application.isBatchMode)
            {
                HoyoToonLogCore.LogAlways($"{title}: {message}", type == MessageType.Error ? LogType.Error : type == MessageType.Warning ? LogType.Warning : LogType.Log);
                return null;
            }

            var window = CreateInstance<HoyoToonDialogWindow>();
            window._title = title;
            window._message = message;
            window._type = type;
            window._topBar = topBar ?? TopBarConfig.Default();
            window._onResultIndex = onResultIndex;
            window._keepOpenOnClick = keepOpenOnClick;
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = new Vector2(UiLayout.WINDOW_MIN_WIDTH, UiLayout.WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(UiLayout.WINDOW_DEFAULT_WIDTH, UiLayout.WINDOW_MAX_HEIGHT);
            
            // Enable progress mode
            window._showProgressBar = true;
            window._progress = 0f;
            window._progressText = "Starting...";

            // Auto-detect markdown per message content
            window._useMarkdown = window.LooksLikeMarkdown(message);
            window._renderedMessageCache = null;
            window._pendingResize = true;

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

            // Pre-size window so buttons are visible on first open
            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();

            return window;
        }

    /// <summary>Show a dialog with custom buttons and return clicked index via callback.</summary>
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
            // Batch-mode/Headless fallback: log + auto-resolve to default
            if (Application.isBatchMode)
            {
                HoyoToonLogCore.LogAlways($"{title}: {message}", type == MessageType.Error ? LogType.Error : type == MessageType.Warning ? LogType.Warning : LogType.Log);
                onResultIndex?.Invoke(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, (buttons?.Length ?? 1) - 1)));
                return;
            }

            var window = CreateInstance<HoyoToonDialogWindow>();
            window._title = title;
            window._message = message;
            window._type = type;
            window._topBar = topBar ?? TopBarConfig.Default();
            window._onResultIndex = onResultIndex;
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = new Vector2(UiLayout.WINDOW_MIN_WIDTH, UiLayout.WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(UiLayout.WINDOW_DEFAULT_WIDTH, UiLayout.WINDOW_MAX_HEIGHT);
            // Auto-detect markdown per message content
            window._useMarkdown = window.LooksLikeMarkdown(message);
            window._renderedMessageCache = null;
            window._pendingResize = true;

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

            // Pre-size window so buttons are visible on first open
            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();
        }

    /// <summary>Show a dialog with custom buttons and an optional content image.</summary>
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
            if (Application.isBatchMode)
            {
                HoyoToonLogCore.LogAlways($"{title}: {message}", type == MessageType.Error ? LogType.Error : type == MessageType.Warning ? LogType.Warning : LogType.Log);
                onResultIndex?.Invoke(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, (buttons?.Length ?? 1) - 1)));
                return;
            }

            var window = CreateInstance<HoyoToonDialogWindow>();
            window._title = title;
            window._message = message;
            window._type = type;
            window._topBar = topBar ?? TopBarConfig.Default();
            window._onResultIndex = onResultIndex;
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = new Vector2(UiLayout.WINDOW_MIN_WIDTH, UiLayout.WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(UiLayout.WINDOW_DEFAULT_WIDTH, UiLayout.WINDOW_MAX_HEIGHT);

            window._contentImageResourcePath = contentImageResourcePath;
            window._contentImage = contentImage;
            window._contentImageMaxHeight = Mathf.Max(80f, contentImageMaxHeight);

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

            // Auto-detect markdown per message content
            window._useMarkdown = window.LooksLikeMarkdown(message);
            window._renderedMessageCache = null;
            window._pendingResize = true;

            // Pre-size window so buttons are visible on first open
            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();
        }

    // Internals specific to this dialog
        private string _message;
        private string _renderedMessageCache;
        private string _lastMessageSource;
        private bool _useMarkdown;

    // Runtime setters for persistent progress window
        public void SetMessage(string message)
        {
            _message = message ?? string.Empty;
            // Reset markdown cache so layout can recalc
            _renderedMessageCache = null;
            _lastMessageSource = null;
            _useMarkdown = LooksLikeMarkdown(_message);
            _pendingResize = true;
            Repaint();
        }

    // Styles inherited from base: _headerTitleStyle, _cardStyle, _messageLabelStyle, _messageContainerStyle
    
    // Optional content image (shown inside the message card)
    private string _contentImageResourcePath;
    private Texture2D _contentImage;
    private float _contentImageMaxHeight = 220f;

        private float _cachedMsgHeight;
        private float _cachedImgHeight;

    // Top bar layout constants/types inherited from base

        protected override void OnEnable()
        {
            // Ensure a default top bar if not provided
            base.OnEnable();
            // Auto-detect markdown on first open
            _useMarkdown = LooksLikeMarkdown(_message);
            _renderedMessageCache = null;
            _lastMessageSource = null;
        }

        // Progress bar API inherited from base (UpdateProgress/SetProgressText/CompleteProgress)

        // GUI pipeline now inherited from base

        protected override void EnsureStyles()
        {
            base.EnsureStyles();
            _messageLabelStyle.richText = _useMarkdown;
        }

        // Header drawing/use inherited from base

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
            // Account for the message container's vertical padding and a small fudge to avoid last-line truncation
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
            // Wrapped, multi-line label inside a bordered area
            using (new EditorGUILayout.VerticalScope(_messageContainerStyle))
            {
                var msg = GetRenderedMessage();
                // Reserve the exact measured height so long content isn't clipped
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
                // Lazy load from Resources
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

            // Reserve a rect within layout with safe width fallback
            float contentWidth = _cachedContentWidth > 0 ? _cachedContentWidth : Mathf.Max(200f, position.width - 48f);
            // Use the measured image height to match the computed content height
            float targetMeasuredH = Mathf.Min(_cachedImgHeight > 0 ? _cachedImgHeight : maxH, maxH);
            Rect r = GUILayoutUtility.GetRect(contentWidth, targetMeasuredH, GUILayout.ExpandWidth(false));

            // Fit preserving aspect
            float targetW = r.width;
            float targetH = targetW / Mathf.Max(0.01f, aspect);
            if (targetH > maxH)
            {
                targetH = maxH;
                targetW = targetH * aspect;
            }

            float x = r.x + (r.width - targetW) * 0.5f;
            float y = r.y + (r.height - targetH) * 0.5f;

            // Subtle background to lift image
            Color bg = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0.03f);
            EditorGUI.DrawRect(r, bg);
            GUI.DrawTexture(new Rect(x, y, targetW, targetH), tex, ScaleMode.ScaleToFit);
        }

        // Divider helper now comes from base

        // Measurement/auto-resize now handled by base via overrides

        // Auto-resize handled by base

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
            _renderedMessageCache = ConvertMarkdownToRichText(src);
            return _renderedMessageCache;
        }

        private bool LooksLikeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            // Heuristics: headings, lists, bold/italic, code, links
            return text.Contains("\n# ") || text.StartsWith("# ") || text.Contains("**") || text.Contains("_") ||
                   Regex.IsMatch(text, "(^|\n)[0-9]+\\. ") || Regex.IsMatch(text, "(^|\n)[*-] ") || text.Contains("`") || text.Contains("](");
        }

        private string ConvertMarkdownToRichText(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;
            string s = md.Replace("<", "&lt;").Replace(">", "&gt;");

            // Headings (# to ######)
            for (int h = 6; h >= 1; h--)
            {
                string pattern = "^(" + new string('#', h) + ")\\s+(.+)$";
                s = Regex.Replace(s, pattern, m =>
                {
                    string title = m.Groups[2].Value.Trim();
                    int size = 22 - (h - 1) * 2; // 22,20,18,16,14,12
                    return $"<size={size}><b>{title}</b></size>";
                }, RegexOptions.Multiline);
            }

            // Bold **text**
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<b>$1</b>");
            // Italic *text* or _text_
            s = Regex.Replace(s, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "<i>$1</i>");
            s = Regex.Replace(s, @"_(.+?)_", "<i>$1</i>");
            // Inline code `code`
            s = Regex.Replace(s, @"`([^`]+)`", "<color=#CCCCCC>$1</color>");

            // Lists
            s = Regex.Replace(s, @"(?m)^(?:[*-] )(.+)$", "• $1");
            s = Regex.Replace(s, @"(?m)^[0-9]+\. (.+)$", "• $1");

            // Links (rendered as colored text)
            s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "<color=#4EA1FF>$1</color>");

            return s;
        }

    /// <summary>Estimate sizes and set an initial position/minSize before showing.</summary>
        protected override void PreSizeBeforeShow(float initialWidth)
        {
            try
            {
                EnsureStyles();

                float width = Mathf.Max(520f, initialWidth);

                // Compute inner content width similar to ComputeLayoutMeasurements
                int cardPad = _cardStyle != null ? (_cardStyle.padding.left + _cardStyle.padding.right) : 0;
                int msgPad = _messageContainerStyle != null ? (_messageContainerStyle.padding.left + _messageContainerStyle.padding.right) : 0;
                float paddingH = cardPad + msgPad;
                float contentWidth = Mathf.Max(200f, width - paddingH - 24f);

                // Header height
                float headerTextWidth = contentWidth - 24f; // account for icon + spacing
                float headerLabelH = string.IsNullOrEmpty(_title) ? 0f : _headerTitleStyle.CalcHeight(new GUIContent(_title), Mathf.Max(100f, headerTextWidth));
                float headerH = Mathf.Max(20f, headerLabelH);

                // Message height (use markdown if enabled)
                string txt = GetRenderedMessage();
                float msgH = string.IsNullOrEmpty(txt) ? EditorGUIUtility.singleLineHeight : _messageLabelStyle.CalcHeight(new GUIContent(txt), contentWidth);
                int vpad = _messageContainerStyle != null ? (_messageContainerStyle.padding.top + _messageContainerStyle.padding.bottom) : 0;
                float msgBlockHeight = Mathf.Ceil(msgH) + vpad + 2f; // mirror MeasureBodyContentHeight

                // Optional image height
                float imgH = 0f;
                if (HasContentImage())
                {
                    var tex = _contentImage;
                    if (tex)
                    {
                        float aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
                        float targetH = contentWidth / Mathf.Max(0.01f, aspect);
                        imgH = Mathf.Min(_contentImageMaxHeight, targetH);
                    }
                }

                float dividerAndSpacing = imgH > 0f ? (6f + 1f + 6f) : 0f;
                float cardContentH = msgBlockHeight + dividerAndSpacing + imgH;
                float maxCardViewHeight = UiLayout.CARD_MAX_VIEW_HEIGHT;
                float minCardViewHeight = UiLayout.CARD_MIN_VIEW_HEIGHT;
                float cardViewH = Mathf.Min(cardContentH, maxCardViewHeight);
                if (cardViewH < minCardViewHeight) cardViewH = minCardViewHeight;

                float topBar = TopBarLayout.BANNER_HEIGHT;
                float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM; // align with MaybeAutoResizeToContent
                float toolbarH = (EditorStyles.toolbar != null && EditorStyles.toolbar.fixedHeight > 0) ? EditorStyles.toolbar.fixedHeight : UiLayout.TOOLBAR_HEIGHT_FALLBACK;
                float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
                float cardH = toolbarH + 4f + cardViewH;
                // Include progress area here as well if enabled.
                float progressH = _showProgressBar ? (16f + UiLayout.PADDING_MID2) : 0f;
                float desired = topBar + paddings + headerH + cardH + buttonsH + progressH;

                float newH = Mathf.Clamp(desired, UiLayout.WINDOW_MIN_HEIGHT, UiLayout.WINDOW_MAX_HEIGHT);

                var p = position;
                if (p.width <= 0f) p.width = width; else p.width = Mathf.Max(width, p.width);
                p.height = newH;
                position = p;

                minSize = new Vector2(Mathf.Max(UiLayout.WINDOW_MIN_WIDTH, minSize.x, width), Mathf.Max(UiLayout.WINDOW_MIN_HEIGHT, newH));
                maxSize = new Vector2(Mathf.Max(UiLayout.WINDOW_DEFAULT_WIDTH, maxSize.x, width), UiLayout.WINDOW_MAX_HEIGHT);
            }
            catch { /* non-fatal sizing best-effort */ }
            finally
            {
                // Allow another resize pass after first repaint if measurements differ
                _pendingResize = true;
            }
        }

        private void DrawTopBar()
        {
            var cfg = _topBar ?? TopBarConfig.Default();
            var data = CreateTopBarLayout();

            // Background
            if (!string.IsNullOrEmpty(cfg.BackgroundResourcePath))
            {
                Texture2D bg = Resources.Load<Texture2D>(cfg.BackgroundResourcePath);
                if (bg)
                    GUI.DrawTexture(data.bgRect, bg, ScaleMode.StretchToFill);
            }

            // Characters
            DrawCharacterImage(cfg.LeftCharacterPath, data, true);
            DrawCharacterImage(cfg.RightCharacterPath, data, false);

            // Logo
            if (!string.IsNullOrEmpty(cfg.LogoResourcePath))
            {
                Texture2D logo = Resources.Load<Texture2D>(cfg.LogoResourcePath);
                if (logo)
                    GUI.DrawTexture(data.logoRect, logo, ScaleMode.ScaleToFit);
            }
        }

        private TopBarLayoutData CreateTopBarLayout()
        {
            var layout = new TopBarLayoutData();

            // Reserve banner height
            layout.contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(TopBarLayout.BANNER_HEIGHT));
            layout.originalY = layout.contentRect.y;

            // Full-width background across the window
            float width = position.width;
            layout.bgRect = new Rect(0, 0, width, layout.contentRect.height + layout.originalY);

            // Centered logo within original content band
            float logoY = layout.originalY + (TopBarLayout.BANNER_HEIGHT - TopBarLayout.LOGO_HEIGHT) / 2f;
            layout.logoRect = new Rect((layout.bgRect.width - TopBarLayout.LOGO_WIDTH) / 2f, logoY, TopBarLayout.LOGO_WIDTH, TopBarLayout.LOGO_HEIGHT);

            return layout;
        }

        private void DrawCharacterImage(string resourcePath, TopBarLayoutData layout, bool isLeftSide)
        {
            if (string.IsNullOrEmpty(resourcePath)) return;
            Texture2D tex = Resources.Load<Texture2D>(resourcePath);
            if (!tex) return;

            Rect r = CalculateCharacterRect(tex, layout, isLeftSide);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
        }

        private Rect CalculateCharacterRect(Texture2D texture, TopBarLayoutData layout, bool isLeftSide)
        {
            // Fit character into available horizontal slot to the left/right of the logo without overlap.
            float aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;

            float slotX, slotW;
            if (isLeftSide)
            {
                slotX = layout.bgRect.x;
                slotW = Mathf.Max(0f, layout.logoRect.x - TopBarLayout.MIN_LOGO_DISTANCE - slotX);
            }
            else
            {
                slotX = layout.logoRect.xMax + TopBarLayout.MIN_LOGO_DISTANCE;
                slotW = Mathf.Max(0f, layout.bgRect.xMax - slotX);
            }

            // Compute target width/height limited by both max constants and slot width
            float maxW = Mathf.Min(TopBarLayout.CHARACTER_MAX_WIDTH, slotW);
            float maxH = TopBarLayout.CHARACTER_MAX_HEIGHT;
            // Maintain aspect: try by width first, clamp height if needed
            float w = Mathf.Max(0f, maxW);
            float h = w / Mathf.Max(0.01f, aspect);
            if (h > maxH)
            {
                h = maxH;
                w = h * aspect;
                // Re-check slot bounds
                if (w > maxW) w = maxW;
            }

            // Baseline positions
            float y = layout.originalY + TopBarLayout.BANNER_HEIGHT - h; // bottom-align to banner base
            float x = isLeftSide ? slotX : (slotX + slotW - w);

            // If slot is extremely narrow, allow partial offscreen by clamping to bg rect
            if (isLeftSide) x = Mathf.Clamp(x, layout.bgRect.x - (w - 1f), layout.logoRect.x - TopBarLayout.MIN_LOGO_DISTANCE - w);
            else x = Mathf.Clamp(x, layout.logoRect.xMax + TopBarLayout.MIN_LOGO_DISTANCE, layout.bgRect.xMax - 1f);

            return new Rect(x, y, w, h);
        }

        protected override void DrawProgressBar()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                // Progress text
                if (!string.IsNullOrEmpty(_progressText))
                {
                    GUILayout.Label(_progressText, EditorStyles.centeredGreyMiniLabel);
                }

                // Progress bar
                var progressRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                progressRect.x += 6;
                progressRect.width -= 12;
                
                // Background
                EditorGUI.DrawRect(progressRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : new Color(0.7f, 0.7f, 0.7f, 0.8f));
                
                // Progress fill
                if (_progress > 0)
                {
                    var fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * _progress, progressRect.height);
                    EditorGUI.DrawRect(fillRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(0.2f, 0.5f, 0.9f, 0.8f));
                }
                
                // Progress percentage text overlay
                var progressPercent = Mathf.RoundToInt(_progress * 100);
                var percentText = $"{progressPercent}%";
                var textStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.05f, 0.05f, 0.05f, 1f) }
                };
                
                GUI.Label(progressRect, percentText, textStyle);
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int i = 0; i < _buttons.Count; i++)
                {
                    var b = _buttons[i];
                    using (new EditorGUIUtility.IconSizeScope(new Vector2(0, 0)))
                    {
                        if (GUILayout.Button(new GUIContent(b.Label), GUILayout.MinWidth(90), GUILayout.Height(24)))
                        {
                            CloseWithResult(b.Index);
                        }
                    }
                    GUILayout.Space(4);
                }
            }
        }

        private void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                var def = _buttons.Find(x => x.IsDefault) ?? (_buttons.Count > 0 ? _buttons[0] : null);
                if (def != null)
                {
                    CloseWithResult(def.Index);
                    e.Use();
                }
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                var cancel = _buttons.Find(x => x.IsCancel) ?? (_buttons.Count > 0 ? _buttons[_buttons.Count - 1] : null);
                if (cancel != null)
                {
                    CloseWithResult(cancel.Index);
                    e.Use();
                }
            }
        }

        // CloseWithResult is inherited from base
    }
}
#endif
