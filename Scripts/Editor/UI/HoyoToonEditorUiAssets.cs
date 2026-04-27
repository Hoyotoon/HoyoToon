#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI
{
    internal static class HoyoToonEditorUiAssets
    {
        internal const string ManagerAssetRoot = "Packages/com.hoyotoon.hoyotoon/Scripts/Editor/UI/Manager";
        internal const string DialogAssetRoot = "Packages/com.hoyotoon.hoyotoon/Scripts/Editor/UI/Dialogs";
        internal const string DialogShellUxmlAssetPath = DialogAssetRoot + "/UXML/HoyoToonDialogShell.uxml";
        internal const string DialogStyleAssetPath = DialogAssetRoot + "/USS/HoyoToonDialog.uss";
        internal const string ThemeAssetPath = ManagerAssetRoot + "/USS/HoyoToonTheme.uss";
        internal const string LayoutAssetPath = ManagerAssetRoot + "/USS/HoyoToonLayout.uss";
        internal const string ComponentsAssetPath = ManagerAssetRoot + "/USS/HoyoToonComponents.uss";
        internal const string HeaderBackgroundAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/background.png";
        internal const string HeaderLogoAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/hoyotoon.png";

        private const string WidthNarrowClass = "ht-width-narrow";
        private const string WidthStandardClass = "ht-width-standard";
        private const string WidthWideClass = "ht-width-wide";

        internal static bool TryApplyManagerStyleSheets(VisualElement target, out string error)
        {
            error = string.Empty;
            if (target == null)
            {
                error = "No UI root was available for HoyoToon styles.";
                return false;
            }

            StyleSheet theme = LoadAsset<StyleSheet>(ThemeAssetPath);
            StyleSheet layout = LoadAsset<StyleSheet>(LayoutAssetPath);
            StyleSheet components = LoadAsset<StyleSheet>(ComponentsAssetPath);

            if (theme == null || layout == null || components == null)
            {
                error = "The shared HoyoToon Manager style sheets could not be loaded.";
                return false;
            }

            target.styleSheets.Add(theme);
            target.styleSheets.Add(layout);
            target.styleSheets.Add(components);
            return true;
        }

        internal static bool TryApplyDialogStyleSheets(VisualElement target, out string error)
        {
            if (!TryApplyManagerStyleSheets(target, out error))
            {
                return false;
            }

            StyleSheet dialog = LoadAsset<StyleSheet>(DialogStyleAssetPath);
            if (dialog == null)
            {
                error = "The HoyoToon dialog style sheet could not be loaded.";
                return false;
            }

            target.styleSheets.Add(dialog);
            return true;
        }

        internal static void ApplyHeaderArtwork(Image bannerBackground, Image logoImage)
        {
            Texture2D backgroundTexture = LoadAsset<Texture2D>(HeaderBackgroundAssetPath);
            if (bannerBackground != null && backgroundTexture != null)
            {
                bannerBackground.image = backgroundTexture;
                bannerBackground.scaleMode = ScaleMode.ScaleAndCrop;
                bannerBackground.style.display = DisplayStyle.Flex;
            }

            Texture2D logoTexture = LoadAsset<Texture2D>(HeaderLogoAssetPath);
            if (logoImage != null)
            {
                logoImage.image = logoTexture;
                logoImage.scaleMode = ScaleMode.ScaleToFit;
                logoImage.style.display = logoTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        internal static void ApplyWidthClass(VisualElement root, float width)
        {
            if (root == null)
            {
                return;
            }

            root.RemoveFromClassList(WidthNarrowClass);
            root.RemoveFromClassList(WidthStandardClass);
            root.RemoveFromClassList(WidthWideClass);

            if (width <= 359f)
            {
                root.AddToClassList(WidthNarrowClass);
            }
            else if (width <= 619f)
            {
                root.AddToClassList(WidthStandardClass);
            }
            else
            {
                root.AddToClassList(WidthWideClass);
            }
        }

        internal static T LoadAsset<T>(string assetPath) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                Debug.LogError("HoyoToon UI could not load asset at path: " + assetPath);
            }

            return asset;
        }
    }
}
#endif
