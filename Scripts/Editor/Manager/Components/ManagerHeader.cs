#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Updater;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Components
{
    internal sealed class ManagerHeader
    {
        private const float BannerHeight = 160f;
        private const float LogoWidth = 348f;
        private const float LogoHeight = 114f;
        private const float BadgeMargin = 12f;
        private const float BadgeHeight = 20f;
        private const float BadgePaddingX = 8f;
        private const float MinBadgeWidth = 74f;
        private const float BadgeShadowOffset = 1f;

        private readonly string _backgroundPath;
        private readonly string _logoPath;

        private Texture2D _background;
        private Texture2D _logo;
        private bool _assetsLoaded;
        private GUIStyle _badgeStyle;
        private GUIStyle _badgeShadowStyle;

        public ManagerHeader(
            string backgroundPath = "UI/background",
            string logoPath = "UI/hoyotoon")
        {
            _backgroundPath = backgroundPath;
            _logoPath = logoPath;
        }

        public void Draw()
        {
            Rect contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(BannerHeight), GUILayout.ExpandWidth(true));
            Rect backgroundRect = new Rect(0f, contentRect.y, EditorGUIUtility.currentViewWidth, contentRect.height);
            Rect logoRect = new Rect(
                backgroundRect.x + (backgroundRect.width - LogoWidth) / 2f,
                backgroundRect.y + (backgroundRect.height - LogoHeight) / 2f,
                LogoWidth,
                LogoHeight);

            if (Event.current.type == EventType.Repaint)
            {
                EnsureAssetsLoaded();

                if (_background != null)
                {
                    GUI.DrawTexture(backgroundRect, _background, ScaleMode.StretchToFill);
                }
                else
                {
                    EditorGUI.DrawRect(backgroundRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                }

                if (_logo != null)
                {
                    GUI.DrawTexture(logoRect, _logo, ScaleMode.ScaleToFit);
                }
            }

            DrawVersionBadge(backgroundRect);
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded)
            {
                return;
            }

            _background = LoadTexture(_backgroundPath);
            _logo = LoadTexture(_logoPath);
            _assetsLoaded = true;
        }

        private static Texture2D LoadTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Banner texture not found at Resources/{resourcePath}");
            }

            return texture;
        }

        private void DrawVersionBadge(Rect backgroundRect)
        {
            string localVersion = PackageVersionUtility.GetLocalPackageVersion();
            string versionText = string.IsNullOrEmpty(localVersion) || localVersion == "unknown"
                ? "?"
                : $"{localVersion}";

            PackageUpdater.UpdateStatusSnapshot status = PackageUpdater.GetStatusSnapshot();
            string indicator = "?";
            Color indicatorColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            string tooltip = "Click to check for updates.";

            if (status != null)
            {
                switch (status.State)
                {
                    case PackageUpdater.UpdateAvailabilityState.UpToDate:
                    case PackageUpdater.UpdateAvailabilityState.LocalAhead:
                        indicator = "✓";
                        indicatorColor = new Color(0.54f, 0.96f, 0.66f, 1f);
                        tooltip = "Up to date. Click to check again in the background.";
                        break;
                    case PackageUpdater.UpdateAvailabilityState.UpdateAvailable:
                        indicator = "!";
                        indicatorColor = new Color(1f, 0.78f, 0.34f, 1f);
                        tooltip = "Update available. Click to open the updater.";
                        break;
                    case PackageUpdater.UpdateAvailabilityState.Checking:
                        indicator = "…";
                        indicatorColor = new Color(0.58f, 0.82f, 1f, 1f);
                        tooltip = "Checking for updates...";
                        break;
                    case PackageUpdater.UpdateAvailabilityState.Error:
                        indicator = "!";
                        indicatorColor = new Color(1f, 0.56f, 0.48f, 1f);
                        tooltip = "Update check failed. Click to try again.";
                        break;
                }
            }

            string markup = $"<color=#{ColorUtility.ToHtmlStringRGB(indicatorColor)}>{indicator}</color> {versionText}";
            GUIContent content = EditorGUIUtility.TrTextContent(markup, tooltip);
            GUIStyle badgeStyle = GetBadgeStyle();
            float width = Mathf.Max(MinBadgeWidth, badgeStyle.CalcSize(new GUIContent($"{indicator} {versionText}")).x + (BadgePaddingX * 2f));
            Rect badgeRect = new Rect(
                backgroundRect.xMax - width - BadgeMargin,
                backgroundRect.yMax - BadgeHeight,
                width,
                BadgeHeight);

            GUI.Label(new Rect(badgeRect.x, badgeRect.y + BadgeShadowOffset, badgeRect.width, badgeRect.height), content, GetBadgeShadowStyle());
            GUI.Label(badgeRect, content, badgeStyle);
            EditorGUIUtility.AddCursorRect(badgeRect, MouseCursor.Link);

            if (GUI.Button(badgeRect, GUIContent.none, GUIStyle.none))
            {
                PackageUpdater.HandleManagerHeaderBadgeClick();
            }
        }

        private GUIStyle GetBadgeStyle()
        {
            if (_badgeStyle != null)
            {
                return _badgeStyle;
            }

            _badgeStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight,
                fixedHeight = BadgeHeight,
                fontSize = 12,
                richText = true,
                padding = new RectOffset((int)BadgePaddingX, (int)BadgePaddingX, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _badgeStyle.normal.textColor = Color.white;
            _badgeStyle.hover.textColor = Color.white;
            _badgeStyle.active.textColor = Color.white;
            _badgeStyle.focused.textColor = Color.white;
            return _badgeStyle;
        }

        private GUIStyle GetBadgeShadowStyle()
        {
            if (_badgeShadowStyle != null)
            {
                return _badgeShadowStyle;
            }

            _badgeShadowStyle = new GUIStyle(GetBadgeStyle());
            _badgeShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);
            _badgeShadowStyle.hover.textColor = _badgeShadowStyle.normal.textColor;
            _badgeShadowStyle.active.textColor = _badgeShadowStyle.normal.textColor;
            _badgeShadowStyle.focused.textColor = _badgeShadowStyle.normal.textColor;
            return _badgeShadowStyle;
        }
    }
}
#endif
