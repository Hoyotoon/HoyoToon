#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HoyoToon.Updater
{
    /// <summary>
    /// Minimal .gitignore parser focused on preventing deletion of locally ignored paths.
    /// Supported features (subset of gitignore spec):
    ///  - Blank lines & comments (#) ignored.
    ///  - Leading '!' negation to re-include a previously ignored pattern.
    ///  - Trailing slash indicates directory-only match.
    ///  - '*' and '?' wildcards.
    ///  - '**' matches across directory separators.
    ///  - Patterns without slashes match against file / directory name anywhere in path.
    /// Limitations:
    ///  - Does not implement advanced gitignore edge cases (e.g., escaped spaces, character ranges).
    ///  - Assumes UTF-8 .gitignore.
    ///  - Designed for editor-time filtering only; safe fallbacks if parsing fails.
    ///
    /// Updater Tagging (optional – narrows scope):
    ///  You can restrict which patterns the updater considers by adding tagged sections or inline directives.
    ///  If at least one tagged rule is found ONLY tagged rules are used; otherwise the entire file is parsed.
    ///
    ///  Block syntax:
    ///      # HOYOTOON-UPDATER-KEEP START
    ///      Dev/
    ///      Debug/
    ///      !Debug/KeepThis.txt
    ///      # comments allowed inside block
    ///      # HOYOTOON-UPDATER-KEEP END
    ///
    ///  Inline syntax (anywhere in file):
    ///      # updater-keep: Experiments/
    ///      # updater-keep: *.local
    ///
    ///  Use whichever style you prefer; both may coexist. Negations (!) and directory-only (trailing /) apply the same.
    /// </summary>
    internal sealed class GitIgnoreFilter
    {
        private readonly List<Rule> _rules = new List<Rule>();
        private readonly string _root; // absolute root for relative path normalization
        private DateTime _loadedAtUtc;

        private static readonly Dictionary<string, GitIgnoreFilter> _cache = new Dictionary<string, GitIgnoreFilter>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        private struct Rule
        {
            public Regex regex;          // compiled regex for match
            public bool isNegation;      // true if rule starts with '!'
            public bool directoryOnly;   // true if pattern ended with '/'
            public string original;      // original pattern (for debug)
        }

        private GitIgnoreFilter(string root)
        {
            _root = root.Replace("\\", "/");
        }

        public static GitIgnoreFilter Load(string rootFullPath)
        {
            try
            {
                if (_cache.TryGetValue(rootFullPath, out var cached))
                {
                    if ((DateTime.UtcNow - cached._loadedAtUtc) < CacheLifetime)
                        return cached;
                }

                var filter = new GitIgnoreFilter(rootFullPath) { _loadedAtUtc = DateTime.UtcNow };
                var gitIgnorePath = Path.Combine(rootFullPath, ".gitignore");
                if (!File.Exists(gitIgnorePath)) { _cache[rootFullPath] = filter; return filter; }

                var lines = File.ReadAllLines(gitIgnorePath);

                // Tagging Strategy:
                // 1. Block markers: lines exactly (case-insensitive) '# HOYOTOON-UPDATER-KEEP START' and '# HOYOTOON-UPDATER-KEEP END'.
                //    Only patterns inside the block are considered. Multiple blocks allowed.
                // 2. Inline marker: lines starting with '# updater-keep:' followed by a pattern. (Useful when user does not want block.)
                // If at least one block or inline pattern is found, ONLY those tagged patterns are used.
                // Otherwise fallback to interpreting the whole file (previous behavior).

                var taggedRules = new List<Rule>();
                bool inBlock = false;
                bool anyTagged = false;
                foreach (var raw in lines)
                {
                    var trimmed = raw.Trim();
                    var upper = trimmed.ToUpperInvariant();
                    if (upper == "# HOYOTOON-UPDATER-KEEP START") { inBlock = true; anyTagged = true; continue; }
                    if (upper == "# HOYOTOON-UPDATER-KEEP END") { inBlock = false; continue; }

                    if (trimmed.StartsWith("# updater-keep:", StringComparison.OrdinalIgnoreCase))
                    {
                        var pattern = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrEmpty(pattern))
                        {
                            var rule = BuildRule(pattern, raw);
                            if (rule.regex != null) taggedRules.Add(rule);
                            anyTagged = true;
                        }
                        continue;
                    }

                    if (!inBlock) continue; // ignore non-tagged content outside block while scanning for tagged
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (trimmed.StartsWith("#")) continue; // allow comments inside block
                    var blockRule = BuildRule(trimmed, raw);
                    if (blockRule.regex != null) taggedRules.Add(blockRule);
                }

                if (anyTagged)
                {
                    filter._rules.AddRange(taggedRules);
                }
                else
                {
                    // Fallback to legacy behavior: use every non-comment pattern in file
                    foreach (var rawAll in lines)
                    {
                        var line = rawAll.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        if (line.StartsWith("#")) continue;
                        var rule = BuildRule(line, rawAll);
                        if (rule.regex != null) filter._rules.Add(rule);
                    }
                }
                _cache[rootFullPath] = filter;
                return filter;
            }
            catch
            {
                var empty = new GitIgnoreFilter(rootFullPath) { _loadedAtUtc = DateTime.UtcNow };
                _cache[rootFullPath] = empty;
                return empty; // fail safe: treat as no ignore
            }
        }

        private static Rule BuildRule(string pattern, string originalRaw)
        {
            bool neg = false;
            if (pattern.StartsWith("!")) { neg = true; pattern = pattern.Substring(1); }
            bool dirOnly = pattern.EndsWith("/");
            if (dirOnly) pattern = pattern.TrimEnd('/');
            if (string.IsNullOrEmpty(pattern)) return new Rule();
            var regex = CompilePattern(pattern);
            return new Rule { regex = regex, isNegation = neg, directoryOnly = dirOnly, original = originalRaw };
        }

        /// <summary>
        /// Returns true if the relative path (POSIX style) is considered ignored by .gitignore rules.
        /// </summary>
        public bool IsIgnored(string relativePath, bool isDirectory)
        {
            if (string.IsNullOrEmpty(relativePath)) return false;
            var rel = relativePath.Replace("\\", "/");

            bool ignored = false;
            foreach (var rule in _rules)
            {
                if (rule.directoryOnly && !isDirectory) continue;
                if (rule.regex.IsMatch(rel))
                {
                    ignored = !rule.isNegation; // negation flips to NOT ignored
                }
            }
            return ignored;
        }

        private static Regex CompilePattern(string pattern)
        {
            try
            {
                // Escape regex special chars then re-introduce globs
                // '**/' or '/**' should match zero or more directories
                // We'll translate:
                //   '.' -> escaped
                //   '**' -> .* (including slash)
                //   '*' -> [^/]*
                //   '?' -> [^/]
                string regexPattern = Regex.Escape(pattern);
                // Replace escaped glob tokens with regex equivalents
                regexPattern = regexPattern.Replace(@"\*\*", "__DOUBLESTAR__"); // temp placeholder
                regexPattern = regexPattern.Replace(@"\*", "[^/]*");
                regexPattern = regexPattern.Replace("__DOUBLESTAR__", ".*");
                regexPattern = regexPattern.Replace(@"\?", "[^/]");

                // If pattern contains a slash, match from start; else allow match on any path segment
                bool containsSlash = pattern.Contains("/");
                if (containsSlash)
                    regexPattern = "^" + regexPattern + "($|/.*)"; // allow deeper descendants
                else
                    regexPattern = "(^|.*/)" + regexPattern + "$"; // segment match

                return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
