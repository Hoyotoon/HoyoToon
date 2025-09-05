#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Dialog window for messages, prompts, and progress with optional themed banner.
    /// Static helpers provide common patterns (OK, Yes/No, custom buttons, progress).
    /// </summary>
    public class HoyoToonDialogWindow : EditorWindow
    {
        // Global UI sizing/layout constants to keep the window consistent everywhere
        private static class UiLayout
        {
            public const float WINDOW_MIN_WIDTH = 680f;     // Tight but readable
            public const float WINDOW_DEFAULT_WIDTH = 720f; // Initial width for all dialogs
            public const float WINDOW_MIN_HEIGHT = 560f;    // Enough for banner + content + buttons
            public const float WINDOW_MAX_HEIGHT = 880f;    // Avoid overly tall windows

            public const float CARD_MAX_VIEW_HEIGHT = 440f; // Max inner scroll height for text
            public const float CARD_MIN_VIEW_HEIGHT = 140f; // Min inner scroll height

            // Section paddings (vertical sum must align with auto-resize math)
            public const float PADDING_TOP = 8f;   // space under top bar
            public const float PADDING_MID1 = 4f;  // between header and card
            public const float PADDING_MID2 = 6f;  // between card and progress/buttons
            public const float PADDING_BOTTOM = 4f;// bottom pad under buttons

            // Controls
            public const float TOOLBAR_HEIGHT_FALLBACK = 20f;
            public const float BUTTONS_BLOCK_HEIGHT = 40f;
        }
        // Top bar config
        [Serializable]
        public class TopBarConfig
        {
            public string BackgroundResourcePath; // e.g., "UI/background"
            public string LogoResourcePath;       // e.g., "UI/hoyotoon"
            public string LeftCharacterPath;      // e.g., "UI/hsrl"
            public string RightCharacterPath;     // e.g., "UI/hsrr"

            public static TopBarConfig Default()
            {
                return new TopBarConfig
                {
                    BackgroundResourcePath = "UI/background",
                    LogoResourcePath = "UI/hoyotoon",
                    LeftCharacterPath = "UI/hsrl",
                    RightCharacterPath = "UI/hsrr"
                };
            }
        }

        // Button definition
        private class ButtonDef
        {
            public string Label;
            public int Index;
            public bool IsDefault;
            public bool IsCancel;
        }

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

        // Progress dialogs
        public static HoyoToonDialogWindow ShowProgress(string title, string message, MessageType type = MessageType.Info, TopBarConfig topBar = null, Action onCancel = null)
        {
            return ShowProgressWithCustomButtons(title, message, type, onCancel != null ? new[] { "Cancel" } : new string[0], topBar, onCancel != null ? i => onCancel() : null);
        }

        public static HoyoToonDialogWindow ShowProgressWithCustomButtons(string title, string message, MessageType type, string[] buttons, TopBarConfig topBar = null, Action<int> onResultIndex = null)
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

    // Internals
        private string _title;
        private string _message;
        private MessageType _type;
        private TopBarConfig _topBar;
        private List<ButtonDef> _buttons = new List<ButtonDef>();
        private Action<int> _onResultIndex;
        private Vector2 _scroll;

        // Progress bar support
        private bool _showProgressBar;
        private float _progress;
        private string _progressText;

    // Runtime setters for persistent progress window
        public void SetTitle(string title)
        {
            _title = title ?? string.Empty;
            _pendingResize = true;
            Repaint();
        }

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

    // Cached styles
    private GUIStyle _headerTitleStyle;
    private GUIStyle _cardStyle;
    private GUIStyle _messageLabelStyle;
    private GUIStyle _messageContainerStyle;
    
    // Optional content image (shown inside the message card)
    private string _contentImageResourcePath;
    private Texture2D _contentImage;
    private float _contentImageMaxHeight = 220f;

    // Cached layout data for auto-resize and scrolling
        private bool _pendingResize = true;
        private float _cachedContentWidth;
        private float _cachedHeaderHeight;
        private float _cachedMsgHeight;
        private float _cachedImgHeight;
        private float _cachedCardContentHeight;
        private float _cachedCardViewHeight;
        private bool _cachedNeedsScroll;

    // Markdown
    private bool _useMarkdown;
    private string _renderedMessageCache;
    private string _lastMessageSource;

    // Top bar layout constants
        private static class TopBarLayout
        {
            public const float BANNER_HEIGHT = 150f;
            public const float LOGO_WIDTH = 348f;
            public const float LOGO_HEIGHT = 114f;
            public const float CHARACTER_MAX_WIDTH = 256f;
            public const float CHARACTER_MAX_HEIGHT = 180f;
            public const float MIN_LOGO_DISTANCE = 5f;
        }

        private struct TopBarLayoutData
        {
            public Rect contentRect;
            public Rect bgRect;
            public Rect logoRect;
            public float originalY;
        }

        private void OnEnable()
        {
            // Ensure a default top bar if not provided
            if (_topBar == null) _topBar = TopBarConfig.Default();
            _pendingResize = true;
            // Auto-detect markdown on first open
            _useMarkdown = LooksLikeMarkdown(_message);
            _renderedMessageCache = null;
            _lastMessageSource = null;
        }

        // Progress bar API
        public void UpdateProgress(float progress, string text = null)
        {
            _progress = Mathf.Clamp01(progress);
            if (!string.IsNullOrEmpty(text))
                _progressText = text;
            Repaint();
        }

        public void SetProgressText(string text)
        {
            _progressText = text ?? "";
            Repaint();
        }

        public void CompleteProgress(string completionMessage = "Complete")
        {
            _progress = 1f;
            _progressText = completionMessage;
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();

            // Draw themed top bar
            DrawTopBar();

            // Edge padding and body container
            GUILayout.Space(UiLayout.PADDING_TOP);
            using (new EditorGUILayout.VerticalScope())
            {
                // Measure layout to allow auto-sizing and conditional scroll
                ComputeLayoutMeasurements();
                DrawHeaderRow();

                GUILayout.Space(UiLayout.PADDING_MID1);
                DrawMessageCard();

                GUILayout.Space(UiLayout.PADDING_MID2);
                
                // Draw progress bar if enabled
                if (_showProgressBar)
                {
                    DrawProgressBar();
                    GUILayout.Space(UiLayout.PADDING_MID2);
                }
                
                DrawButtons();
                GUILayout.Space(UiLayout.PADDING_BOTTOM);
            }

            HandleKeyboard();

            MaybeAutoResizeToContent();
        }

        private void EnsureStyles()
        {
            if (_headerTitleStyle == null)
            {
                _headerTitleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    richText = true
                };
            }

            // Message uses word-wrapped label style

            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(8, 8, 8, 6)
                };
            }

            if (_messageLabelStyle == null)
            {
                _messageLabelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    wordWrap = true
                };
            }
            _messageLabelStyle.richText = _useMarkdown;

            if (_messageContainerStyle == null)
            {
                _messageContainerStyle = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(8, 8, 6, 6)
                };
            }
        }

        private void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = GetIconForType(_type);
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                    GUILayout.Space(4);
                }
                GUILayout.Label(string.IsNullOrEmpty(_title) ? string.Empty : _title, _headerTitleStyle);
            }
        }

        private GUIContent GetIconForType(MessageType type)
        {
            string iconName = null;
            switch (type)
            {
                case MessageType.Error: iconName = "console.erroricon"; break;
                case MessageType.Warning: iconName = "console.warnicon"; break;
                case MessageType.Info: iconName = "console.infoicon"; break;
                default: iconName = null; break;
            }
            if (string.IsNullOrEmpty(iconName)) return null;
            var gc = EditorGUIUtility.IconContent(iconName);
            return gc != null && gc.image != null ? gc : null;
        }

        private void DrawMessageCard()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                // Compact toolbar (right aligned)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _message ?? string.Empty;
                    }
                }

                GUILayout.Space(4);

                // Scrollable content: message first, then optional image
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(_cachedCardViewHeight));

                // Wrapped, multi-line label inside a bordered area
                using (new EditorGUILayout.VerticalScope(_messageContainerStyle))
                {
                    var msg = GetRenderedMessage();
                    // IMGUI computes label height; measurements drive scroll sizing
                    EditorGUILayout.LabelField(msg, _messageLabelStyle, GUILayout.ExpandWidth(true));
                }

                if (HasContentImage())
                {
                    GUILayout.Space(6);
                    DrawDivider(1f, 2f);
                    GUILayout.Space(6);
                    DrawContentImage();
                }
                EditorGUILayout.EndScrollView();
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

            // Reserve a rect within layout
            Rect r = GUILayoutUtility.GetRect(_cachedContentWidth, maxH, GUILayout.ExpandWidth(false));

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

        private void DrawDivider(float thickness = 1f, float sidePadding = 0f)
        {
            Rect r = GUILayoutUtility.GetRect(1, thickness, GUILayout.ExpandWidth(true));
            if (sidePadding > 0f)
            {
                r.xMin += sidePadding;
                r.xMax -= sidePadding;
            }
            Color c = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.1f);
            EditorGUI.DrawRect(r, c);
        }

        private void ComputeLayoutMeasurements()
        {
            // Width available for text/image inside the card content
            // Account for both card and message container paddings
            int cardPad = _cardStyle != null ? (_cardStyle.padding.left + _cardStyle.padding.right) : 0;
            int msgPad = _messageContainerStyle != null ? (_messageContainerStyle.padding.left + _messageContainerStyle.padding.right) : 0;
            float paddingH = cardPad + msgPad;
            // Account for layout paddings and scrollbar gutter
            _cachedContentWidth = Mathf.Max(200f, position.width - paddingH - 24f);

            // Header height (icon 20 vs wrapped title)
            float headerTextWidth = _cachedContentWidth - 24f; // icon + spacing
            if (_headerTitleStyle == null) EnsureStyles();
            float headerLabelH = string.IsNullOrEmpty(_title) ? 0f : _headerTitleStyle.CalcHeight(new GUIContent(_title), headerTextWidth);
            _cachedHeaderHeight = Mathf.Max(20f, headerLabelH);

            // Message height with wrapping
            if (_messageLabelStyle == null) EnsureStyles();
            string measureText = GetRenderedMessage();
            _cachedMsgHeight = string.IsNullOrEmpty(measureText) ? EditorGUIUtility.singleLineHeight : _messageLabelStyle.CalcHeight(new GUIContent(measureText), _cachedContentWidth);

            // Image height given content width
            _cachedImgHeight = 0f;
            if (HasContentImage())
            {
                var tex = _contentImage;
                if (tex)
                {
                    float aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
                    float targetH = _cachedContentWidth / Mathf.Max(0.01f, aspect);
                    _cachedImgHeight = Mathf.Min(_contentImageMaxHeight, targetH);
                }
            }

            float dividerAndSpacing = _cachedImgHeight > 0f ? (6f + 1f + 6f) : 0f;
            _cachedCardContentHeight = _cachedMsgHeight + dividerAndSpacing + _cachedImgHeight;

            // Determine scroll need and cap view height
            float maxCardViewHeight = UiLayout.CARD_MAX_VIEW_HEIGHT;
            float minCardViewHeight = UiLayout.CARD_MIN_VIEW_HEIGHT;
            _cachedNeedsScroll = _cachedCardContentHeight > maxCardViewHeight;
            _cachedCardViewHeight = _cachedNeedsScroll ? maxCardViewHeight : _cachedCardContentHeight;
            if (_cachedCardViewHeight < minCardViewHeight)
                _cachedCardViewHeight = minCardViewHeight;
        }

        private void MaybeAutoResizeToContent()
        {
            // Re-evaluate after content/markdown changes that affect text size
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                // If toggles changed this frame, request a resize on next pass
            }

            if (_pendingResize && e.type == EventType.Repaint)
            {
                // Compose desired total height from pieces
                float topBar = TopBarLayout.BANNER_HEIGHT;
                float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM;
                float toolbarH = (EditorStyles.toolbar != null && EditorStyles.toolbar.fixedHeight > 0) ? EditorStyles.toolbar.fixedHeight : UiLayout.TOOLBAR_HEIGHT_FALLBACK;
                float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
                float cardH = toolbarH + 4f + _cachedCardViewHeight;
                float desired = topBar + paddings + _cachedHeaderHeight + cardH + buttonsH;

                // Clamp sensible bounds
                float minH = UiLayout.WINDOW_MIN_HEIGHT;
                float maxH = UiLayout.WINDOW_MAX_HEIGHT;
                float newH = Mathf.Clamp(desired, minH, maxH);

                // Resize window height without changing width/position
                var p = position;
                if (Mathf.Abs(p.height - newH) > 1f)
                {
                    p.height = newH;
                    position = p;
                }

                // Keep min/max sizes aligned with desired bounds
                minSize = new Vector2(Mathf.Max(UiLayout.WINDOW_MIN_WIDTH, minSize.x), newH);
                maxSize = new Vector2(Mathf.Max(UiLayout.WINDOW_DEFAULT_WIDTH, maxSize.x), UiLayout.WINDOW_MAX_HEIGHT);

                _pendingResize = false;
            }
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
        private void PreSizeBeforeShow(float initialWidth)
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
                float cardContentH = msgH + dividerAndSpacing + imgH;
                float maxCardViewHeight = UiLayout.CARD_MAX_VIEW_HEIGHT;
                float minCardViewHeight = UiLayout.CARD_MIN_VIEW_HEIGHT;
                float cardViewH = Mathf.Min(cardContentH, maxCardViewHeight);
                if (cardViewH < minCardViewHeight) cardViewH = minCardViewHeight;

                float topBar = TopBarLayout.BANNER_HEIGHT;
                float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM; // align with MaybeAutoResizeToContent
                float toolbarH = (EditorStyles.toolbar != null && EditorStyles.toolbar.fixedHeight > 0) ? EditorStyles.toolbar.fixedHeight : UiLayout.TOOLBAR_HEIGHT_FALLBACK;
                float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
                float cardH = toolbarH + 4f + cardViewH;
                float desired = topBar + paddings + headerH + cardH + buttonsH;

                float newH = Mathf.Clamp(desired, UiLayout.WINDOW_MIN_HEIGHT, UiLayout.WINDOW_MAX_HEIGHT);

                var p = position;
                if (p.width <= 0f) p.width = width; else p.width = Mathf.Max(width, p.width);
                p.height = newH;
                position = p;

                minSize = new Vector2(Mathf.Max(UiLayout.WINDOW_MIN_WIDTH, minSize.x, width), newH);
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
            float aspectRatio = (float)texture.width / texture.height;
            float characterWidth = Mathf.Min(TopBarLayout.CHARACTER_MAX_WIDTH, TopBarLayout.CHARACTER_MAX_HEIGHT * aspectRatio);
            float characterHeight = Mathf.Min(TopBarLayout.CHARACTER_MAX_HEIGHT, TopBarLayout.CHARACTER_MAX_WIDTH / aspectRatio);

            float characterY = layout.originalY + TopBarLayout.BANNER_HEIGHT - characterHeight;

            float characterX;
            if (isLeftSide)
            {
                float maxAllowedX = layout.logoRect.x - characterWidth - TopBarLayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Min(layout.bgRect.x, maxAllowedX);
            }
            else
            {
                float minAllowedX = layout.logoRect.xMax + TopBarLayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Max(layout.bgRect.xMax - characterWidth, minAllowedX);
            }

            return new Rect(characterX, characterY, characterWidth, characterHeight);
        }

        private void DrawProgressBar()
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

        private void CloseWithResult(int index)
        {
            try { _onResultIndex?.Invoke(index); }
            catch (Exception ex) { HoyoToonLogger.Always("Manager", $"Dialog callback exception: {ex}", LogType.Exception); }
            Close();
        }
    }
}
#endif
