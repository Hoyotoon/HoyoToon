using System.Text;

namespace HoyoToon.Runtime.Utilities
{
    internal static class RuntimeStringKey
    {
        private static readonly StringBuilder s_KeyBuilder = new StringBuilder(128);

        internal static string NormalizeAlphanumericLower(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            s_KeyBuilder.Clear();
            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                    s_KeyBuilder.Append(char.ToLowerInvariant(character));
            }

            string key = s_KeyBuilder.ToString();
            s_KeyBuilder.Clear();
            return key;
        }

        internal static string NormalizeDisplayNameKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            s_KeyBuilder.Clear();
            bool previousWasSeparator = false;
            for (int i = 0; i < value.Length; ++i)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    s_KeyBuilder.Append(char.ToLowerInvariant(character));
                    previousWasSeparator = false;
                }
                else if (!previousWasSeparator && s_KeyBuilder.Length > 0)
                {
                    s_KeyBuilder.Append(' ');
                    previousWasSeparator = true;
                }
            }

            if (s_KeyBuilder.Length > 0 && s_KeyBuilder[s_KeyBuilder.Length - 1] == ' ')
                s_KeyBuilder.Length--;

            string key = s_KeyBuilder.ToString();
            s_KeyBuilder.Clear();
            return key;
        }
    }
}
