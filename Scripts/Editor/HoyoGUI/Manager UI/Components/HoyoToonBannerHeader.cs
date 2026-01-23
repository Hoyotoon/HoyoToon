#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.ManagerUI.Components
{
    internal sealed class HoyoToonBannerHeader
    {
        private static class Layout
        {
            public const float BannerHeight = 160f;
            public const float LogoWidth = 348f;
            public const float LogoHeight = 114f;
            public const float CharacterMaxWidth = 256f;
            public const float CharacterMaxHeight = 150f;
            public const float MinLogoDistance = 5f;
        }

        private sealed class BannerAssets
        {
            public Texture2D Background;
            public Texture2D Logo;
            public Texture2D LeftCharacter;
            public Texture2D RightCharacter;
        }

        private struct BannerLayout
        {
            public Rect BackgroundRect;
            public Rect LogoRect;
            public float OriginalY;
        }

        private readonly string _backgroundPath;
        private readonly string _logoPath;
        private readonly string _leftCharacterPath;
        private readonly string _rightCharacterPath;

        private BannerAssets _assets;

        public HoyoToonBannerHeader(
            string backgroundPath = "UI/background",
            string logoPath = "UI/hoyotoon",
            string leftCharacterPath = "UI/hsrl",
            string rightCharacterPath = "UI/hsrr")
        {
            _backgroundPath = backgroundPath;
            _logoPath = logoPath;
            _leftCharacterPath = leftCharacterPath;
            _rightCharacterPath = rightCharacterPath;
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

            if (assets.LeftCharacter != null)
            {
                GUI.DrawTexture(CalculateCharacterRect(assets.LeftCharacter, layout, true), assets.LeftCharacter, ScaleMode.ScaleToFit);
            }

            if (assets.RightCharacter != null)
            {
                GUI.DrawTexture(CalculateCharacterRect(assets.RightCharacter, layout, false), assets.RightCharacter, ScaleMode.ScaleToFit);
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

        private Rect CalculateCharacterRect(Texture2D texture, BannerLayout layout, bool isLeftSide)
        {
            float aspect = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;

            float slotX, slotW;
            if (isLeftSide)
            {
                slotX = layout.BackgroundRect.xMin;
                slotW = Mathf.Max(0f, layout.LogoRect.x - Layout.MinLogoDistance - slotX);
            }
            else
            {
                slotX = layout.LogoRect.xMax + Layout.MinLogoDistance;
                slotW = Mathf.Max(0f, layout.BackgroundRect.xMax - slotX);
            }

            float maxW = Mathf.Min(Layout.CharacterMaxWidth, slotW);
            float w = Mathf.Max(0f, maxW);
            float h = w / Mathf.Max(0.01f, aspect);
            if (h > Layout.CharacterMaxHeight)
            {
                h = Layout.CharacterMaxHeight;
                w = h * aspect;
                if (w > maxW)
                {
                    w = maxW;
                }
            }

            float y = layout.OriginalY + Layout.BannerHeight - h;
            float x = isLeftSide ? slotX : (slotX + slotW - w);
            if (isLeftSide)
            {
                x = Mathf.Clamp(x, layout.BackgroundRect.x - (w - 1f), layout.LogoRect.x - Layout.MinLogoDistance - w);
            }
            else
            {
                x = Mathf.Clamp(x, layout.LogoRect.xMax + Layout.MinLogoDistance, layout.BackgroundRect.xMax - 1f);
            }
            return new Rect(x, y, w, h);
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
                Logo = LoadTexture(_logoPath),
                LeftCharacter = LoadTexture(_leftCharacterPath),
                RightCharacter = LoadTexture(_rightCharacterPath)
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
                Debug.LogWarning($"HoyoToon banner texture not found at Resources/{resourcePath}");
            }
            return texture;
        }
    }
}
#endif // UNITY_EDITOR
