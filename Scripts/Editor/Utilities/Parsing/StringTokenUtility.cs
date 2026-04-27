#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

namespace HoyoToon.Editor.Utilities.Parsing
{
    public static class StringTokenUtility
    {
        public static string NormalizeAlphanumeric(string value, bool lowercase = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                builder.Append(lowercase ? char.ToLowerInvariant(character) : character);
            }

            return builder.ToString();
        }

        public static string NormalizeAlphanumericLower(string value)
        {
            return NormalizeAlphanumeric(value, lowercase: true);
        }

        public static List<string> ExtractAlphanumericTokens(string value, bool lowercase = true)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return tokens;
            }

            var builder = new StringBuilder();
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(lowercase ? char.ToLowerInvariant(character) : character);
                    continue;
                }

                FlushToken(builder, tokens);
            }

            FlushToken(builder, tokens);
            return tokens;
        }

        public static string BuildSlug(string prefix, string value)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return prefix;
            }

            var builder = new StringBuilder(prefix.Length + value.Length + 1);
            builder.Append(prefix);

            if (builder.Length > 0 && builder[builder.Length - 1] != '-')
            {
                builder.Append('-');
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == '-')
            {
                builder.Length--;
            }

            return builder.Length > 0 ? builder.ToString() : prefix;
        }

        private static void FlushToken(StringBuilder builder, List<string> tokens)
        {
            if (builder.Length <= 0)
            {
                return;
            }

            tokens.Add(builder.ToString());
            builder.Clear();
        }
    }
}
#endif
