#if UNITY_EDITOR
using System.Text.RegularExpressions;

namespace HoyoToon.Editor.Utilities
{
    internal static class MarkdownToRichText
    {
        private static readonly Regex[] HeadingPatterns = new Regex[6];
        private static readonly Regex BoldPattern            = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicStarPattern      = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex ItalicUnderscorePattern = new Regex(@"_(.+?)_", RegexOptions.Compiled);
        private static readonly Regex InlineCodePattern      = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex UnorderedListPattern   = new Regex(@"(?m)^[*-] (.+)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListPattern     = new Regex(@"(?m)^[0-9]+\. (.+)$", RegexOptions.Compiled);
        private static readonly Regex LinkPattern            = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

        static MarkdownToRichText()
        {
            for (int h = 0; h < 6; h++)
            {
                string hashes = new string('#', h + 1);
                HeadingPatterns[h] = new Regex("^(" + hashes + @")\s+(.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
            }
        }

        public static bool LooksLikeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return text.Contains("\n# ") || text.StartsWith("# ") || text.Contains("**") || text.Contains("_") ||
                   OrderedListPattern.IsMatch(text) || UnorderedListPattern.IsMatch(text) || text.Contains("`") || text.Contains("](");
        }

        public static string Convert(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;
            string s = md.Replace("<", "&lt;").Replace(">", "&gt;");

            for (int h = 5; h >= 0; h--)
            {
                int level = h + 1;
                s = HeadingPatterns[h].Replace(s, m =>
                {
                    string title = m.Groups[2].Value.Trim();
                    int size = 22 - (level - 1) * 2;
                    return $"<size={size}><b>{title}</b></size>";
                });
            }

            s = BoldPattern.Replace(s, "<b>$1</b>");
            s = ItalicStarPattern.Replace(s, "<i>$1</i>");
            s = ItalicUnderscorePattern.Replace(s, "<i>$1</i>");
            s = InlineCodePattern.Replace(s, "<color=#CCCCCC>$1</color>");
            s = UnorderedListPattern.Replace(s, "• $1");
            s = OrderedListPattern.Replace(s, "• $1");
            s = LinkPattern.Replace(s, "<color=#4EA1FF>$1</color>");

            return s;
        }
    }
}
#endif
