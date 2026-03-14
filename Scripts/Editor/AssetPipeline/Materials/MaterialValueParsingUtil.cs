#if UNITY_EDITOR
using System;
using System.Globalization;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    internal static class MaterialValueParsingUtil
    {
        public static bool TryParseTruthy(object value, out bool parsed)
        {
            parsed = false;
            if (value == null) return false;

            switch (value)
            {
                case bool b:
                    parsed = b;
                    return true;
                case int i:
                    parsed = i != 0;
                    return true;
                case long l:
                    parsed = l != 0L;
                    return true;
                case float f:
                    parsed = Math.Abs(f) > float.Epsilon;
                    return true;
                case double d:
                    parsed = Math.Abs(d) > double.Epsilon;
                    return true;
                case decimal m:
                    parsed = m != decimal.Zero;
                    return true;
                case string s:
                {
                    var trimmed = s.Trim();
                    if (trimmed.Length == 0) return false;
                    if (bool.TryParse(trimmed, out var parsedBool))
                    {
                        parsed = parsedBool;
                        return true;
                    }

                    if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNum))
                    {
                        parsed = parsedNum != 0d;
                        return true;
                    }

                    return false;
                }
                default:
                    return false;
            }
        }

        public static bool ParseKeywordMapTruthyOrDefault(object value)
        {
            if (value == null) return true;

            if (value is string s && s.Trim().Length == 0)
                return true;

            return TryParseTruthy(value, out var parsed) ? parsed : true;
        }
    }
}
#endif