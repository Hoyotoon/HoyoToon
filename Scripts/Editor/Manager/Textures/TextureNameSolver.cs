#if UNITY_EDITOR
using System;
using System.IO;
using HoyoToon.API;

namespace HoyoToon.Textures
{
    /// <summary>
    /// Helpers to extract a character name from a texture filename using the per-game
    /// ProblemList Regex, with a heuristic fallback for resilience.
    /// </summary>
    public static class TextureNameSolver
    {

        /// <summary>
        /// Extract character name using the game's ProblemList regex when available; falls back to heuristics.
        /// Returns the extracted name in Title Case, or null if none found.
        /// </summary>
        public static string ExtractCharacterNameUsingMeta(GameMetadata meta, string pathOrName)
        {
            try
            {
                if (meta == null) return null;
                string name = ExtractUsingGlobalRegex(meta, pathOrName);
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch { return null; }
        }

        private static string ExtractUsingGlobalRegex(GameMetadata meta, string pathOrName)
        {
            if (meta?.ProblemList == null || string.IsNullOrWhiteSpace(meta.ProblemList.Regex)) return null;
            try
            {
                string input = pathOrName;
                if (!string.IsNullOrEmpty(input) && input.IndexOfAny(new[] { '/', '\\' }) >= 0)
                    input = Path.GetFileName(input);
                var re = new System.Text.RegularExpressions.Regex(meta.ProblemList.Regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var m = re.Match(input ?? string.Empty);
                if (m.Success)
                {
                    string name = null;
                    if (m.Groups["name"] != null && m.Groups["name"].Success) name = m.Groups["name"].Value;
                    else if (m.Groups.Count > 1) name = m.Groups[1].Value; // first captured group
                    else name = m.Value; // entire match if no capture groups
                    if (!string.IsNullOrWhiteSpace(name)) return TitleCase(name.Trim());
                }
            }
            catch (Exception)
            {
                // Ignore regex errors; fall back to heuristic or return null.
            }
            return null;
        }

        private static string TitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var parts = s.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length == 0) continue;
                if (p.Length == 1) parts[i] = char.ToUpperInvariant(p[0]).ToString();
                else parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
            }
            return string.Join(" ", parts);
        }
    }
}
#endif