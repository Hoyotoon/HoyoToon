#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Components
{
    internal sealed class ManagerHeader
    {
        private static class Layout
        {
            public const float BannerHeight = 160f;
            public const float LogoWidth = 348f;
            public const float LogoHeight = 114f;
        }

        private sealed class BannerAssets
        {
            public Texture2D Background;
            public Texture2D Logo;
        }

        private struct BannerLayout
        {
            public Rect BackgroundRect;
            public Rect LogoRect;
            public float OriginalY;
        }

        private readonly string _backgroundPath;
        private readonly string _logoPath;

        private BannerAssets _assets;

        public ManagerHeader(
            string backgroundPath = "UI/background",
            string logoPath = "UI/hoyotoon")
        {
            _backgroundPath = backgroundPath;
            _logoPath = logoPath;
        }

        public void Draw()
        {
            Rect contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(Layout.BannerHeight), GUILayout.ExpandWidth(true));
            BannerLayout layout = CreateLayout(contentRect);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            BannerAssets assets = LoadAssets();
            if (assets.Background != null)
            {
                GUI.DrawTexture(layout.BackgroundRect, assets.Background, ScaleMode.StretchToFill);
            }
            else
            {
                EditorGUI.DrawRect(layout.BackgroundRect, new Color(0.15f, 0.15f, 0.15f, 1f));
            }

            if (assets.Logo != null)
            {
                GUI.DrawTexture(layout.LogoRect, assets.Logo, ScaleMode.ScaleToFit);
            }
        }

        private BannerLayout CreateLayout(Rect contentRect)
        {
            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            Rect backgroundRect = new Rect(0f, contentRect.y, inspectorWidth, contentRect.height);
            float logoX = backgroundRect.x + (backgroundRect.width - Layout.LogoWidth) / 2f;
            float logoY = backgroundRect.y + (backgroundRect.height - Layout.LogoHeight) / 2f;

            return new BannerLayout
            {
                BackgroundRect = backgroundRect,
                LogoRect = new Rect(logoX, logoY, Layout.LogoWidth, Layout.LogoHeight),
                OriginalY = contentRect.y
            };
        }

        private BannerAssets LoadAssets()
        {
            if (_assets != null)
            {
                return _assets;
            }

            _assets = new BannerAssets
            {
                Background = LoadTexture(_backgroundPath),
                Logo = LoadTexture(_logoPath)
            };

            return _assets;
        }

        private Texture2D LoadTexture(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Banner texture not found at Resources/{resourcePath}");
            }
            return texture;
        }
    }
}
#endif
