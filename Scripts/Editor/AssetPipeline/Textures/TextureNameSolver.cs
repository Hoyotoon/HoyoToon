#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.AssetPipeline.Textures
{
    public static class TextureNameSolver
    {
        private static readonly Dictionary<string, Regex> s_regexCache = new Dictionary<string, Regex>(StringComparer.Ordinal);
        private static readonly TextInfo s_textInfo = CultureInfo.InvariantCulture.TextInfo;

        public static string ExtractCharacterNameUsingMeta(GameMetadata meta, string pathOrName)
        {
            try
            {
                if (meta == null) return null;
                string name = ExtractUsingGlobalRegex(meta, pathOrName);
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"ExtractCharacterNameUsingMeta failed: {ex.Message}");
                return null;
            }
        }

        private static string ExtractUsingGlobalRegex(GameMetadata meta, string pathOrName)
        {
            if (meta?.ProblemList == null || string.IsNullOrWhiteSpace(meta.ProblemList.Regex)) return null;
            try
            {
                string input = pathOrName;
                if (!string.IsNullOrEmpty(input) && input.IndexOfAny(new[] { '/', '\\' }) >= 0)
                    input = Path.GetFileName(input);
                if (!s_regexCache.TryGetValue(meta.ProblemList.Regex, out var re))
                {
                    re = new Regex(meta.ProblemList.Regex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    s_regexCache[meta.ProblemList.Regex] = re;
                }
                var m = re.Match(input ?? string.Empty);
                if (m.Success)
                {
                    string name = null;
                    if (m.Groups["name"] != null && m.Groups["name"].Success) name = m.Groups["name"].Value;
                    else if (m.Groups.Count > 1) name = m.Groups[1].Value; // first captured group
                    else name = m.Value; // entire match if no capture groups
                    if (!string.IsNullOrWhiteSpace(name)) return s_textInfo.ToTitleCase(name.Trim().ToLowerInvariant());
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning($"TextureNameSolver.Regex({meta.ProblemList.Regex})", $"Regex error for ProblemList pattern: {ex.Message}");
            }
            return null;
        }
    }
}
#endif