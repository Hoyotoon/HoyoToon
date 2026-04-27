#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Dialogs
{
    internal static class HoyoToonDialogMarkdownView
    {
        internal static VisualElement Create(string message)
        {
            VisualElement markdownRoot = new VisualElement();
            markdownRoot.AddToClassList("ht-dialog-markdown");

            string[] lines = HoyoToonDialogMarkdown.NormalizeLineEndings(message).Trim().Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    VisualElement spacer = new VisualElement();
                    spacer.AddToClassList("ht-dialog-markdown-spacer");
                    markdownRoot.Add(spacer);
                    continue;
                }

                if (HoyoToonDialogMarkdown.TryGetHeading(line, out int level, out string headingText))
                {
                    Label headingLabel = CreateRichLabel(headingText, "ht-dialog-markdown-heading");
                    headingLabel.AddToClassList("ht-dialog-markdown-heading--" + Mathf.Min(level, 3));
                    markdownRoot.Add(headingLabel);
                    continue;
                }

                if (HoyoToonDialogMarkdown.TryGetBullet(line, out string bulletText))
                {
                    VisualElement bulletRow = new VisualElement();
                    bulletRow.AddToClassList("ht-dialog-markdown-bullet-row");

                    Label bulletLabel = new Label("\u2022");
                    bulletLabel.AddToClassList("ht-dialog-markdown-bullet");
                    bulletRow.Add(bulletLabel);

                    VisualElement bulletContent = CreateInlineMarkdownLine(bulletText);
                    bulletContent.AddToClassList("ht-dialog-markdown-bullet-content");
                    bulletRow.Add(bulletContent);
                    markdownRoot.Add(bulletRow);
                    continue;
                }

                markdownRoot.Add(CreateInlineMarkdownLine(line));
            }

            return markdownRoot;
        }

        private static VisualElement CreateInlineMarkdownLine(string line)
        {
            IReadOnlyList<HoyoToonDialogMarkdownSegment> segments = HoyoToonDialogMarkdown.ParseInline(line);
            if (!segments.Any(segment => segment.IsLink))
            {
                return CreateRichLabel(line, "ht-dialog-message");
            }

            VisualElement inlineRoot = new VisualElement();
            inlineRoot.AddToClassList("ht-dialog-markdown-inline");

            foreach (HoyoToonDialogMarkdownSegment segment in segments)
            {
                if (segment.IsLink)
                {
                    Label linkLabel = new Label(HoyoToonDialogMarkdown.ToPlainText(segment.Text));
                    linkLabel.tooltip = segment.Url;
                    linkLabel.focusable = true;
                    linkLabel.pickingMode = PickingMode.Position;
                    linkLabel.AddToClassList("ht-dialog-link");
                    linkLabel.RegisterCallback<ClickEvent>(_ => Application.OpenURL(segment.Url));
                    linkLabel.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space)
                        {
                            return;
                        }

                        Application.OpenURL(segment.Url);
                        evt.StopPropagation();
                    });
                    inlineRoot.Add(linkLabel);
                    continue;
                }

                if (string.IsNullOrEmpty(segment.Text))
                {
                    continue;
                }

                Label segmentLabel = CreateRichLabel(segment.Text, "ht-dialog-markdown-inline-text");
                inlineRoot.Add(segmentLabel);
            }

            return inlineRoot;
        }

        private static Label CreateRichLabel(string text, string className)
        {
            Label label = new Label(HoyoToonDialogMarkdown.ToInlineRichText(text))
            {
                enableRichText = true
            };
            label.AddToClassList(className);
            return label;
        }
    }
}
#endif
