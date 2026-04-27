#if UNITY_EDITOR
using HoyoToon.Editor.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Dialogs
{
    internal sealed class HoyoToonDialogShell
    {
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;

        private HoyoToonDialogShell(
            VisualElement shellRoot,
            VisualElement bodyContent,
            VisualElement actionContent,
            Label titleLabel,
            Label subtitleLabel)
        {
            ShellRoot = shellRoot;
            BodyContent = bodyContent;
            ActionContent = actionContent;
            this.titleLabel = titleLabel;
            this.subtitleLabel = subtitleLabel;
        }

        internal VisualElement ShellRoot { get; }
        internal VisualElement BodyContent { get; }
        internal VisualElement ActionContent { get; }

        internal static bool TryBuild(
            EditorWindow owner,
            string title,
            string subtitle,
            out HoyoToonDialogShell shell,
            out string error)
        {
            shell = null;
            error = string.Empty;

            if (owner == null)
            {
                error = "No owner window was available for the HoyoToon dialog shell.";
                return false;
            }

            VisualTreeAsset tree = HoyoToonEditorUiAssets.LoadAsset<VisualTreeAsset>(HoyoToonEditorUiAssets.DialogShellUxmlAssetPath);
            if (tree == null)
            {
                error = "The HoyoToon dialog UXML asset could not be loaded.";
                return false;
            }

            VisualElement shellRoot = tree.CloneTree();
            if (!HoyoToonEditorUiAssets.TryApplyDialogStyleSheets(shellRoot, out error))
            {
                return false;
            }

            owner.rootVisualElement.Clear();
            owner.rootVisualElement.style.flexGrow = 1f;
            owner.rootVisualElement.style.flexDirection = FlexDirection.Column;
            owner.rootVisualElement.Add(shellRoot);
            shellRoot.StretchToParentSize();
            shellRoot.style.flexGrow = 1f;

            ScrollView bodyScrollView = shellRoot.Q<ScrollView>("DialogBodyScrollView");
            if (bodyScrollView != null)
            {
                bodyScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                bodyScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
                bodyScrollView.contentContainer.style.paddingRight = 24f;
                bodyScrollView.verticalScroller.style.marginLeft = 16f;
            }

            VisualElement bodyContent = shellRoot.Q<VisualElement>("DialogBodyContent");
            VisualElement actionContent = shellRoot.Q<VisualElement>("DialogActionContent");
            Label titleLabel = shellRoot.Q<Label>("DialogTitleLabel");
            Label subtitleLabel = shellRoot.Q<Label>("DialogSubtitleLabel");
            Image bannerBackground = shellRoot.Q<Image>("DialogBannerBackground");
            Image logoImage = shellRoot.Q<Image>("DialogLogoImage");

            if (bodyContent == null || actionContent == null || titleLabel == null || subtitleLabel == null)
            {
                error = "The HoyoToon dialog shell is missing required UXML nodes.";
                return false;
            }

            HoyoToonEditorUiAssets.ApplyHeaderArtwork(bannerBackground, logoImage);
            shell = new HoyoToonDialogShell(shellRoot, bodyContent, actionContent, titleLabel, subtitleLabel);
            shell.SetHeader(title, subtitle);

            HoyoToonEditorUiAssets.ApplyWidthClass(shellRoot, owner.position.width);
            shellRoot.RegisterCallback<GeometryChangedEvent>(evt =>
                HoyoToonEditorUiAssets.ApplyWidthClass(shellRoot, evt.newRect.width));

            return true;
        }

        internal void SetHeader(string title, string subtitle)
        {
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrWhiteSpace(title) ? "HoyoToon" : title.Trim();
            }

            if (subtitleLabel == null)
            {
                return;
            }

            subtitleLabel.text = subtitle ?? string.Empty;
            subtitleLabel.style.display = string.IsNullOrWhiteSpace(subtitle)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
#endif
