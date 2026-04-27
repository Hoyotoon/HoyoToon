#if UNITY_EDITOR
using System;

namespace HoyoToon.Editor.Utilities.Parsing
{
    public static class EnumParsingUtility
    {
        public static bool TryParseFlexibleEnum<TEnum>(string value, out TEnum result)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default;
                return false;
            }

            if (Enum.TryParse(value, true, out result))
            {
                return true;
            }

            string normalized = NormalizeToken(value);
            foreach (TEnum candidate in Enum.GetValues(typeof(TEnum)))
            {
                if (string.Equals(NormalizeToken(candidate.ToString()), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    result = candidate;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static string NormalizeToken(string value)
        {
            return StringTokenUtility.NormalizeAlphanumeric(value);
        }
    }
}
#endif
