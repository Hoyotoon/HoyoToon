#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Dialogs
{
    public enum HoyoToonDialogType
    {
        Info,
        Success,
        Warning,
        Error,
        Question
    }

    public sealed class HoyoToonDialogButton
    {
        public HoyoToonDialogButton(string text, int result, string styleKind = "secondary", Action callback = null)
        {
            Text = string.IsNullOrWhiteSpace(text) ? "OK" : text.Trim();
            Result = result;
            StyleKind = string.IsNullOrWhiteSpace(styleKind) ? "secondary" : styleKind.Trim();
            Callback = callback;
        }

        public string Text { get; }
        public int Result { get; }
        public string StyleKind { get; }
        public Action Callback { get; }

        public static HoyoToonDialogButton Primary(string text, int result = 0, Action callback = null)
        {
            return new HoyoToonDialogButton(text, result, "primary", callback);
        }

        public static HoyoToonDialogButton Secondary(string text, int result = 1, Action callback = null)
        {
            return new HoyoToonDialogButton(text, result, "secondary", callback);
        }

        public static HoyoToonDialogButton Ghost(string text, int result = 2, Action callback = null)
        {
            return new HoyoToonDialogButton(text, result, "ghost", callback);
        }
    }

    public sealed class HoyoToonDialogOptions
    {
        public string Title { get; set; } = "HoyoToon";
        public string Subtitle { get; set; } = "Editor message";
        public string Message { get; set; } = string.Empty;
        public HoyoToonDialogType DialogType { get; set; } = HoyoToonDialogType.Info;
        public IReadOnlyList<HoyoToonDialogButton> Buttons { get; set; } = Array.Empty<HoyoToonDialogButton>();
        public Action<VisualElement> BuildBody { get; set; }
        public Action<int> OnClosed { get; set; }
        public int CloseResult { get; set; } = -1;
        public Vector2 Size { get; set; } = HoyoToonDialogWindow.DefaultSize;
        public bool AutoSizeToContent { get; set; } = true;
        public bool Utility { get; set; } = true;
    }

    public static class HoyoToonDialog
    {
        public static EditorWindow Show(HoyoToonDialogOptions options)
        {
            HoyoToonDialogOptions resolvedOptions = NormalizeOptions(options);
            if (Application.isBatchMode)
            {
                CompleteBatch(resolvedOptions, GetBatchResult(resolvedOptions));
                return null;
            }

            return HoyoToonDialogWindow.ShowWindow(resolvedOptions);
        }

        public static EditorWindow ShowMessage(
            string title,
            string message,
            string ok = "OK",
            HoyoToonDialogType dialogType = HoyoToonDialogType.Info)
        {
            return Show(BuildMessageOptions(title, message, ok, dialogType));
        }

        public static EditorWindow ShowConfirm(
            string title,
            string message,
            Action<bool> onClosed,
            string ok = "OK",
            string cancel = "Cancel",
            HoyoToonDialogType dialogType = HoyoToonDialogType.Question)
        {
            HoyoToonDialogOptions options = BuildConfirmOptions(title, message, ok, cancel, dialogType);
            options.OnClosed = result => onClosed?.Invoke(result == 0);
            return Show(options);
        }

        public static bool DisplayDialog(string title, string message, string ok)
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            return HoyoToonDialogWindow.ShowModalAndReturn(BuildMessageOptions(title, message, ok, HoyoToonDialogType.Info)) == 0;
        }

        public static bool DisplayDialog(string title, string message, string ok, string cancel)
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            return HoyoToonDialogWindow.ShowModalAndReturn(
                BuildConfirmOptions(title, message, ok, cancel, HoyoToonDialogType.Question)) == 0;
        }

        public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt)
        {
            if (Application.isBatchMode)
            {
                return 1;
            }

            HoyoToonDialogOptions options = new HoyoToonDialogOptions
            {
                Title = title,
                Subtitle = "Choose an action",
                Message = message,
                DialogType = HoyoToonDialogType.Question,
                CloseResult = 1,
                Buttons = new[]
                {
                    HoyoToonDialogButton.Primary(ok, 0),
                    HoyoToonDialogButton.Secondary(cancel, 1),
                    HoyoToonDialogButton.Ghost(alt, 2)
                }
            };

            return HoyoToonDialogWindow.ShowModalAndReturn(options);
        }

        internal static HoyoToonDialogOptions NormalizeOptions(HoyoToonDialogOptions options)
        {
            HoyoToonDialogOptions resolvedOptions = options ?? new HoyoToonDialogOptions();
            resolvedOptions.Title = string.IsNullOrWhiteSpace(resolvedOptions.Title)
                ? "HoyoToon"
                : resolvedOptions.Title.Trim();
            resolvedOptions.Subtitle = resolvedOptions.Subtitle ?? string.Empty;
            resolvedOptions.Message = resolvedOptions.Message ?? string.Empty;

            List<HoyoToonDialogButton> buttons = (resolvedOptions.Buttons ?? Array.Empty<HoyoToonDialogButton>())
                .Where(button => button != null)
                .ToList();

            if (buttons.Count == 0)
            {
                buttons.Add(HoyoToonDialogButton.Primary("OK", 0));
            }

            resolvedOptions.Buttons = buttons;
            if (resolvedOptions.Size.x <= 0f || resolvedOptions.Size.y <= 0f)
            {
                resolvedOptions.Size = HoyoToonDialogWindow.DefaultSize;
            }

            return resolvedOptions;
        }

        private static HoyoToonDialogOptions BuildMessageOptions(
            string title,
            string message,
            string ok,
            HoyoToonDialogType dialogType)
        {
            return new HoyoToonDialogOptions
            {
                Title = title,
                Subtitle = GetDefaultSubtitle(dialogType),
                Message = message,
                DialogType = dialogType,
                CloseResult = 0,
                Buttons = new[] { HoyoToonDialogButton.Primary(ok, 0) }
            };
        }

        private static HoyoToonDialogOptions BuildConfirmOptions(
            string title,
            string message,
            string ok,
            string cancel,
            HoyoToonDialogType dialogType)
        {
            return new HoyoToonDialogOptions
            {
                Title = title,
                Subtitle = GetDefaultSubtitle(dialogType),
                Message = message,
                DialogType = dialogType,
                CloseResult = 1,
                Buttons = new[]
                {
                    HoyoToonDialogButton.Primary(ok, 0),
                    HoyoToonDialogButton.Secondary(cancel, 1)
                }
            };
        }

        private static string GetDefaultSubtitle(HoyoToonDialogType dialogType)
        {
            switch (dialogType)
            {
                case HoyoToonDialogType.Success:
                    return "Complete";
                case HoyoToonDialogType.Warning:
                    return "Review required";
                case HoyoToonDialogType.Error:
                    return "Action failed";
                case HoyoToonDialogType.Question:
                    return "Choose an action";
                default:
                    return "Editor message";
            }
        }

        private static int GetBatchResult(HoyoToonDialogOptions options)
        {
            if (options == null)
            {
                return -1;
            }

            return options.CloseResult >= 0
                ? options.CloseResult
                : options.Buttons?.FirstOrDefault()?.Result ?? -1;
        }

        private static void CompleteBatch(HoyoToonDialogOptions options, int result)
        {
            HoyoToonDialogButton button = options?.Buttons?.FirstOrDefault(candidate => candidate != null && candidate.Result == result);
            try
            {
                button?.Callback?.Invoke();
                options?.OnClosed?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    internal static class HoyoToonDialogMarkdown
    {
        private const string AccentColor = "#B99DFF";

        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,4})\s+(.+)$", RegexOptions.Multiline);
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]\n]+)\]\(([^\)\n]+)\)");
        private static readonly Regex BoldRegex = new Regex(@"(\*\*|__)(.+?)\1", RegexOptions.Singleline);
        private static readonly Regex ItalicRegex = new Regex(@"(?<!\*)\*([^*\n]+)\*(?!\*)");
        private static readonly Regex CodeRegex = new Regex(@"`([^`\n]+)`");
        private static readonly Regex BulletRegex = new Regex(@"^(\s*)[-*]\s+", RegexOptions.Multiline);
        private static readonly Regex NumberedBulletRegex = new Regex(@"^(\s*)\d+\.\s+", RegexOptions.Multiline);

        internal static string AccentRichTextColor => AccentColor;

        internal static string ToRichText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            string richText = ToInlineRichText(NormalizeLineEndings(markdown).Trim());
            richText = HeadingRegex.Replace(richText, match =>
            {
                int level = match.Groups[1].Value.Length;
                int size = level == 1 ? 18 : level == 2 ? 16 : 14;
                return $"<b><size={size}>{match.Groups[2].Value.Trim()}</size></b>";
            });

            richText = LinkRegex.Replace(richText, match =>
                $"<color={AccentColor}>{match.Groups[1].Value}</color>");
            richText = BulletRegex.Replace(richText, "$1\u2022 ");
            richText = NumberedBulletRegex.Replace(richText, "$1\u2022 ");

            return richText;
        }

        internal static string ToInlineRichText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            string richText = EscapeRichText(NormalizeLineEndings(markdown));
            richText = BoldRegex.Replace(richText, "<b>$2</b>");
            richText = ItalicRegex.Replace(richText, "<i>$1</i>");
            richText = CodeRegex.Replace(richText, $"<color={AccentColor}>$1</color>");
            return richText;
        }

        internal static string ToPlainText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            string plainText = NormalizeLineEndings(markdown).Trim();
            plainText = HeadingRegex.Replace(plainText, "$2");
            plainText = LinkRegex.Replace(plainText, "$1");
            plainText = BoldRegex.Replace(plainText, "$2");
            plainText = ItalicRegex.Replace(plainText, "$1");
            plainText = CodeRegex.Replace(plainText, "$1");
            plainText = BulletRegex.Replace(plainText, "$1");
            plainText = NumberedBulletRegex.Replace(plainText, "$1");
            plainText = plainText.Replace("__", string.Empty);
            plainText = plainText.Replace("**", string.Empty);
            plainText = plainText.Replace("`", string.Empty);
            return plainText;
        }

        internal static string NormalizeLineEndings(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        internal static bool TryGetHeading(string line, out int level, out string text)
        {
            Match match = HeadingRegex.Match(line ?? string.Empty);
            if (!match.Success)
            {
                level = 0;
                text = string.Empty;
                return false;
            }

            level = Mathf.Clamp(match.Groups[1].Value.Length, 1, 4);
            text = match.Groups[2].Value.Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        internal static bool TryGetBullet(string line, out string text)
        {
            string candidate = line ?? string.Empty;
            string stripped = BulletRegex.Replace(candidate, string.Empty, 1);
            stripped = NumberedBulletRegex.Replace(stripped, string.Empty, 1);
            if (string.Equals(candidate, stripped, StringComparison.Ordinal))
            {
                text = string.Empty;
                return false;
            }

            text = stripped.Trim();
            return !string.IsNullOrWhiteSpace(text);
        }

        internal static IReadOnlyList<HoyoToonDialogMarkdownSegment> ParseInline(string line)
        {
            var segments = new List<HoyoToonDialogMarkdownSegment>();
            string normalizedLine = NormalizeLineEndings(line);
            int cursor = 0;

            foreach (Match match in LinkRegex.Matches(normalizedLine))
            {
                if (match.Index > cursor)
                {
                    segments.Add(HoyoToonDialogMarkdownSegment.Plain(normalizedLine.Substring(cursor, match.Index - cursor)));
                }

                string label = match.Groups[1].Value.Trim();
                string url = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(url))
                {
                    segments.Add(HoyoToonDialogMarkdownSegment.Link(label, url));
                }

                cursor = match.Index + match.Length;
            }

            if (cursor < normalizedLine.Length)
            {
                segments.Add(HoyoToonDialogMarkdownSegment.Plain(normalizedLine.Substring(cursor)));
            }

            if (segments.Count == 0 && !string.IsNullOrEmpty(normalizedLine))
            {
                segments.Add(HoyoToonDialogMarkdownSegment.Plain(normalizedLine));
            }

            return segments;
        }

        private static string EscapeRichText(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                switch (character)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }

    internal sealed class HoyoToonDialogMarkdownSegment
    {
        private HoyoToonDialogMarkdownSegment(string text, string url)
        {
            Text = text ?? string.Empty;
            Url = url ?? string.Empty;
        }

        internal string Text { get; }
        internal string Url { get; }
        internal bool IsLink => !string.IsNullOrWhiteSpace(Url);

        internal static HoyoToonDialogMarkdownSegment Plain(string text)
        {
            return new HoyoToonDialogMarkdownSegment(text, string.Empty);
        }

        internal static HoyoToonDialogMarkdownSegment Link(string text, string url)
        {
            return new HoyoToonDialogMarkdownSegment(text, url);
        }
    }
}
#endif
