#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Reusable base EditorWindow that implements a consistent header/body/footer layout with
    /// themed top bar, measured scrollable body, optional progress block, and a fixed footer.
    /// Derivations provide body measurement and drawing via overrides.
    /// </summary>
    public abstract class BaseHoyoToonWindow : EditorWindow
    {
        // Global UI sizing/layout constants to keep the window consistent everywhere
        protected static class UiLayout
        {
            public const float WINDOW_MIN_WIDTH = 680f;     // Tight but readable
            public const float WINDOW_DEFAULT_WIDTH = 720f; // Initial width for all dialogs
            public const float WINDOW_MIN_HEIGHT = 560f;    // Enough for banner + content + buttons
            public const float WINDOW_MAX_HEIGHT = 880f;    // Avoid overly tall windows

            public const float CARD_MAX_VIEW_HEIGHT = 440f; // Max inner scroll height for text
            public const float CARD_MIN_VIEW_HEIGHT = 100f; // Min inner scroll height

            // Section paddings (vertical sum must align with auto-resize math)
            public const float PADDING_TOP = 8f;   // space under top bar
            public const float PADDING_MID1 = 4f;  // between header and card
            public const float PADDING_MID2 = 6f;  // between card and progress/buttons
            public const float PADDING_BOTTOM = 15f;// bottom pad under buttons

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
        protected class ButtonDef
        {
            public string Label;
            public int Index;
            public bool IsDefault;
            public bool IsCancel;
        }

        // Public API used by derivations/consumers
        public void SetTitle(string title)
        {
            _title = title ?? string.Empty;
            _pendingResize = true;
            Repaint();
        }

        public void SetButtons(string[] labels, int defaultIndex = 0, int cancelIndex = -1, Action<int> onResultIndex = null, bool keepOpenOnClick = false)
        {
            _buttons.Clear();
            if (labels != null)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    _buttons.Add(new ButtonDef
                    {
                        Label = labels[i],
                        Index = i,
                        IsDefault = (i == defaultIndex),
                        IsCancel = (i == cancelIndex)
                    });
                }
            }
            _onResultIndex = onResultIndex;
            _keepOpenOnClick = keepOpenOnClick;
            Repaint();
        }

        public void SetShowProgressBar(bool enabled)
        {
            _showProgressBar = enabled;
            Repaint();
        }

        public void UpdateProgress(float progress, string text = null)
        {
            _progress = Mathf.Clamp01(progress);
            if (!string.IsNullOrEmpty(text))
                _progressText = text;
            Repaint();
        }

        public void SetProgressText(string text)
        {
            _progressText = text ?? string.Empty;
            Repaint();
        }

        public void CompleteProgress(string completionMessage = "Complete")
        {
            _progress = 1f;
            _progressText = completionMessage;
            Repaint();
        }

        // Configurable header
        protected string _title;
        protected MessageType _type;
        protected TopBarConfig _topBar;

        // Buttons/footer
        protected List<ButtonDef> _buttons = new List<ButtonDef>();
        protected Action<int> _onResultIndex;
        protected bool _keepOpenOnClick;

        // Progress block
        protected bool _showProgressBar;
        protected float _progress;
        protected string _progressText;

        // Common styles
        protected GUIStyle _headerTitleStyle;
        protected GUIStyle _cardStyle;
        protected GUIStyle _messageLabelStyle;
        protected GUIStyle _messageContainerStyle;

        // Layout caches
        protected bool _pendingResize = true;
        protected float _cachedContentWidth;
        protected float _cachedHeaderHeight;
        protected float _cachedCardContentHeight;
        protected float _cachedCardViewHeight;
        protected bool _cachedNeedsScroll;
        protected Vector2 _scroll;

        // Top bar layout constants
        protected static class TopBarLayout
        {
            public const float BANNER_HEIGHT = 150f;
            public const float LOGO_WIDTH = 348f;
            public const float LOGO_HEIGHT = 114f;
            public const float CHARACTER_MAX_WIDTH = 256f;
            public const float CHARACTER_MAX_HEIGHT = 180f;
            public const float MIN_LOGO_DISTANCE = 5f;
        }

        protected struct TopBarLayoutData
        {
            public Rect contentRect;
            public Rect bgRect;
            public Rect logoRect;
            public float originalY;
        }

        protected virtual void OnEnable()
        {
            if (_topBar == null) _topBar = TopBarConfig.Default();
            _pendingResize = true;
        }

        protected virtual void EnsureStyles()
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

            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle(EditorStyles.helpBox)
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

            if (_messageContainerStyle == null)
            {
                _messageContainerStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(8, 8, 6, 6)
                };
            }
        }

        protected virtual void OnGUI()
        {
            EnsureStyles();

            DrawTopBar();

            GUILayout.Space(UiLayout.PADDING_TOP);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                ComputeLayoutMeasurements();
                DrawHeaderRow();

                GUILayout.Space(UiLayout.PADDING_MID1);
                DrawMessageCardFrame();

                GUILayout.Space(UiLayout.PADDING_MID2);

                if (_showProgressBar)
                {
                    DrawProgressBar();
                    GUILayout.Space(UiLayout.PADDING_MID2);
                }
                DrawDivider(1f, 4f);
                GUILayout.Space(2f);
                DrawButtons();
                GUILayout.Space(UiLayout.PADDING_BOTTOM);
            }

            HandleKeyboard();
            MaybeAutoResizeToContent();
        }

        // Hooks for derived classes
        protected virtual float GetCardToolbarHeight()
        {
            return 0f; // no toolbar by default
        }

        protected virtual void DrawCardToolbar() { }

        protected abstract float MeasureBodyContentHeight(float contentWidth);
        protected abstract void DrawBodyContent(float contentWidth);

        // Frame drawing
        private void DrawMessageCardFrame()
        {
            using (new EditorGUILayout.VerticalScope(_cardStyle))
            {
                float toolbarH = GetCardToolbarHeight();
                if (toolbarH > 0f)
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        DrawCardToolbar();
                    }
                    GUILayout.Space(4);
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(_cachedCardViewHeight));
                DrawBodyContent(_cachedContentWidth);
                EditorGUILayout.EndScrollView();
            }
        }

        // Measurements
        private void ComputeLayoutMeasurements()
        {
            // Horizontal inner padding = card + message container paddings
            int cardPad = _cardStyle != null ? (_cardStyle.padding.left + _cardStyle.padding.right) : 0;
            int msgPad = _messageContainerStyle != null ? (_messageContainerStyle.padding.left + _messageContainerStyle.padding.right) : 0;
            float paddingH = cardPad + msgPad;
            _cachedContentWidth = Mathf.Max(200f, position.width - paddingH - 24f);

            // Header height
            float headerTextWidth = _cachedContentWidth - 24f; // icon + spacing
            float headerLabelH = string.IsNullOrEmpty(_title) ? 0f : _headerTitleStyle.CalcHeight(new GUIContent(_title), headerTextWidth);
            _cachedHeaderHeight = Mathf.Max(20f, headerLabelH);

            // Body content height from derivation
            float bodyContentH = Mathf.Max(0f, MeasureBodyContentHeight(_cachedContentWidth));
            _cachedCardContentHeight = bodyContentH;

            // Compute available height for scroll view
            float maxCardViewHeight = UiLayout.CARD_MAX_VIEW_HEIGHT;
            float topBar = TopBarLayout.BANNER_HEIGHT;
            float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM;
            float toolbarH = Mathf.Max(0f, GetCardToolbarHeight());
            float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
            float progressH = _showProgressBar ? (16f + UiLayout.PADDING_MID2) : 0f;

            float usedStatic = topBar + paddings + _cachedHeaderHeight + (toolbarH > 0 ? (toolbarH + 4f) : 0f) + buttonsH + progressH;
            float availableForCard = Mathf.Max(0f, position.height - usedStatic);
            // Let the card view shrink to whatever space is actually available to preserve footer visibility.
            // We intentionally do NOT enforce CARD_MIN_VIEW_HEIGHT here; that min is only for ideal sizing, not tight layouts.
            float autoMaxCardView = Mathf.Max(0f, Mathf.Min(maxCardViewHeight, availableForCard));

            _cachedCardViewHeight = Mathf.Min(_cachedCardContentHeight, autoMaxCardView);
            _cachedNeedsScroll = _cachedCardContentHeight > _cachedCardViewHeight + 0.5f;
        }

        // Compute the minimal height that still keeps the footer buttons fully visible.
        // We allow the card (body) region to shrink to 0 in this calculation.
        private float ComputeDynamicMinWindowHeightUsingCached()
        {
            float topBar = TopBarLayout.BANNER_HEIGHT;
            float headerH = _cachedHeaderHeight > 0f ? _cachedHeaderHeight : 20f;

            // Progress block minimal height (optional text + 16f bar)
            float progressBlock = 0f;
            if (_showProgressBar)
            {
                float labelH = string.IsNullOrEmpty(_progressText) ? 0f : EditorGUIUtility.singleLineHeight;
                progressBlock = labelH + 16f;
            }

            // Sequence matches OnGUI: top->header->card->(optional progress)->divider->buttons
            float minHeight = 0f;
            minHeight += topBar;
            minHeight += UiLayout.PADDING_TOP;
            minHeight += headerH;
            minHeight += UiLayout.PADDING_MID1;       // space above card
            // Card can be 0 in min scenario
            minHeight += UiLayout.PADDING_MID2;       // space below card
            if (_showProgressBar)
            {
                minHeight += progressBlock;
                minHeight += UiLayout.PADDING_MID2;   // space below progress
            }
            minHeight += 1f;                          // divider thickness
            minHeight += 2f;                          // extra space after divider
            minHeight += UiLayout.BUTTONS_BLOCK_HEIGHT; // footer buttons block
            minHeight += UiLayout.PADDING_BOTTOM;     // bottom padding

            return minHeight;
        }

        // Auto-size pass (height only)
        private void MaybeAutoResizeToContent()
        {
            var e = Event.current;
            if (_pendingResize && e.type == EventType.Repaint)
            {
                float topBar = TopBarLayout.BANNER_HEIGHT;
                float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM;
                float toolbarH = Mathf.Max(0f, GetCardToolbarHeight());
                float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
                float cardH = (toolbarH > 0 ? (toolbarH + 4f) : 0f) + _cachedCardViewHeight;
                float progressH = _showProgressBar ? (16f + UiLayout.PADDING_MID2) : 0f;
                float desired = topBar + paddings + _cachedHeaderHeight + cardH + buttonsH + progressH;
                float dynamicMin = ComputeDynamicMinWindowHeightUsingCached();
                float newH = Mathf.Clamp(desired, dynamicMin, UiLayout.WINDOW_MAX_HEIGHT);
                var p = position;
                if (Mathf.Abs(p.height - newH) > 1f)
                {
                    p.height = newH;
                    position = p;
                }

                minSize = new Vector2(
                    Mathf.Max(UiLayout.WINDOW_MIN_WIDTH, minSize.x),
                    Mathf.Max(ComputeDynamicMinWindowHeightUsingCached(), minSize.y)
                );
                maxSize = new Vector2(Mathf.Max(UiLayout.WINDOW_DEFAULT_WIDTH, maxSize.x), UiLayout.WINDOW_MAX_HEIGHT);

                _pendingResize = false;
            }
        }

        /// <summary>Estimate sizes and set an initial position/minSize before showing.</summary>
        protected virtual void PreSizeBeforeShow(float initialWidth)
        {
            try
            {
                EnsureStyles();

                float width = Mathf.Max(520f, initialWidth);

                int cardPad = _cardStyle != null ? (_cardStyle.padding.left + _cardStyle.padding.right) : 0;
                int msgPad = _messageContainerStyle != null ? (_messageContainerStyle.padding.left + _messageContainerStyle.padding.right) : 0;
                float paddingH = cardPad + msgPad;
                float contentWidth = Mathf.Max(200f, width - paddingH - 24f);

                float headerTextWidth = contentWidth - 24f;
                float headerLabelH = string.IsNullOrEmpty(_title) ? 0f : _headerTitleStyle.CalcHeight(new GUIContent(_title), Mathf.Max(100f, headerTextWidth));
                float headerH = Mathf.Max(20f, headerLabelH);

                float bodyContentH = Mathf.Max(0f, MeasureBodyContentHeight(contentWidth));
                float maxCardViewHeight = UiLayout.CARD_MAX_VIEW_HEIGHT;
                float minCardViewHeight = UiLayout.CARD_MIN_VIEW_HEIGHT;
                float cardViewH = Mathf.Min(bodyContentH, maxCardViewHeight);
                if (cardViewH < minCardViewHeight) cardViewH = minCardViewHeight;

                float topBar = TopBarLayout.BANNER_HEIGHT;
                float paddings = UiLayout.PADDING_TOP + UiLayout.PADDING_MID1 + UiLayout.PADDING_MID2 + UiLayout.PADDING_BOTTOM;
                float toolbarH = Mathf.Max(0f, GetCardToolbarHeight());
                float buttonsH = UiLayout.BUTTONS_BLOCK_HEIGHT;
                float cardH = (toolbarH > 0 ? (toolbarH + 4f) : 0f) + cardViewH;
                float progressH = _showProgressBar ? (16f + UiLayout.PADDING_MID2) : 0f;
                float desired = topBar + paddings + headerH + cardH + buttonsH + progressH;

                // Compute a dynamic minimum that ensures footer visibility for the initial sizing
                float dynamicMin = 0f;
                {
                    float progressBlock = _showProgressBar ? ((string.IsNullOrEmpty(_progressText) ? 0f : EditorGUIUtility.singleLineHeight) + 16f) : 0f;
                    dynamicMin = TopBarLayout.BANNER_HEIGHT
                                 + UiLayout.PADDING_TOP
                                 + headerH
                                 + UiLayout.PADDING_MID1
                                 + UiLayout.PADDING_MID2
                                 + (_showProgressBar ? (progressBlock + UiLayout.PADDING_MID2) : 0f)
                                 + 1f + 2f
                                 + UiLayout.BUTTONS_BLOCK_HEIGHT
                                 + UiLayout.PADDING_BOTTOM;
                }
                float newH = Mathf.Clamp(desired, dynamicMin, UiLayout.WINDOW_MAX_HEIGHT);

                var p = position;
                if (p.width <= 0f) p.width = width; else p.width = Mathf.Max(width, p.width);
                p.height = newH;
                position = p;

                minSize = new Vector2(Mathf.Max(UiLayout.WINDOW_MIN_WIDTH, minSize.x, width), Mathf.Max(dynamicMin, newH));
                maxSize = new Vector2(Mathf.Max(UiLayout.WINDOW_DEFAULT_WIDTH, maxSize.x, width), UiLayout.WINDOW_MAX_HEIGHT);
            }
            catch { }
            finally
            {
                _pendingResize = true;
            }
        }

        // Header and top bar
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

        private void DrawTopBar()
        {
            var cfg = _topBar ?? TopBarConfig.Default();
            var data = CreateTopBarLayout();

            if (!string.IsNullOrEmpty(cfg.BackgroundResourcePath))
            {
                Texture2D bg = Resources.Load<Texture2D>(cfg.BackgroundResourcePath);
                if (bg)
                    GUI.DrawTexture(data.bgRect, bg, ScaleMode.StretchToFill);
            }

            DrawCharacterImage(cfg.LeftCharacterPath, data, true);
            DrawCharacterImage(cfg.RightCharacterPath, data, false);

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
            layout.contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(TopBarLayout.BANNER_HEIGHT));
            layout.originalY = layout.contentRect.y;
            float width = position.width;
            layout.bgRect = new Rect(0, 0, width, layout.contentRect.height + layout.originalY);
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

            float maxW = Mathf.Min(TopBarLayout.CHARACTER_MAX_WIDTH, slotW);
            float maxH = TopBarLayout.CHARACTER_MAX_HEIGHT;
            float w = Mathf.Max(0f, maxW);
            float h = w / Mathf.Max(0.01f, aspect);
            if (h > maxH)
            {
                h = maxH;
                w = h * aspect;
                if (w > maxW) w = maxW;
            }

            float y = layout.originalY + TopBarLayout.BANNER_HEIGHT - h;
            float x = isLeftSide ? slotX : (slotX + slotW - w);
            if (isLeftSide) x = Mathf.Clamp(x, layout.bgRect.x - (w - 1f), layout.logoRect.x - TopBarLayout.MIN_LOGO_DISTANCE - w);
            else x = Mathf.Clamp(x, layout.logoRect.xMax + TopBarLayout.MIN_LOGO_DISTANCE, layout.bgRect.xMax - 1f);
            return new Rect(x, y, w, h);
        }

        protected void DrawDivider(float thickness = 1f, float sidePadding = 0f)
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

        protected virtual void DrawProgressBar()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (!string.IsNullOrEmpty(_progressText))
                {
                    GUILayout.Label(_progressText, EditorStyles.centeredGreyMiniLabel);
                }

                var progressRect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                progressRect.x += 6;
                progressRect.width -= 12;

                EditorGUI.DrawRect(progressRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : new Color(0.7f, 0.7f, 0.7f, 0.8f));

                if (_progress > 0)
                {
                    var fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * _progress, progressRect.height);
                    EditorGUI.DrawRect(fillRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 1f, 0.8f) : new Color(0.2f, 0.5f, 0.9f, 0.8f));
                }

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

        protected void CloseWithResult(int index)
        {
            try { _onResultIndex?.Invoke(index); }
            catch (Exception ex) { HoyoToonLogger.Always("Manager", $"Dialog callback exception: {ex}", LogType.Exception); }
            if (!_keepOpenOnClick)
                Close();
        }
    }
}
#endif
