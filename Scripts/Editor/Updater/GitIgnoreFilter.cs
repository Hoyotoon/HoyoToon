#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal sealed class GitIgnoreFilter
    {
        private readonly List<Rule> _rules = new List<Rule>();
        private readonly string _root; // absolute root for relative path normalization
        private DateTime _loadedAtUtc;

        private static readonly ConcurrentDictionary<string, GitIgnoreFilter> _cache = new ConcurrentDictionary<string, GitIgnoreFilter>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        private struct Rule
        {
            public Regex regex;
            public bool isNegation;
            public bool directoryOnly;
            public string original;
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

                    if (!inBlock) continue;
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (trimmed.StartsWith("#")) continue;
                    var blockRule = BuildRule(trimmed, raw);
                    if (blockRule.regex != null) taggedRules.Add(blockRule);
                }

                if (anyTagged)
                {
                    filter._rules.AddRange(taggedRules);
                }
                else
                {
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
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, $"Failed to load .gitignore at '{rootFullPath}': {ex.Message}");
                var empty = new GitIgnoreFilter(rootFullPath) { _loadedAtUtc = DateTime.UtcNow };
                _cache[rootFullPath] = empty;
                return empty;
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
            if (regex == null) return new Rule();
            return new Rule { regex = regex, isNegation = neg, directoryOnly = dirOnly, original = originalRaw };
        }

        public bool IsIgnored(string relativePath, bool isDirectory)
        {
            if (string.IsNullOrEmpty(relativePath)) return false;
            var rel = relativePath.Replace("\\", "/");

            bool ignored = false;
            foreach (var rule in _rules)
            {
                if (rule.regex == null) continue;
                if (rule.directoryOnly && !isDirectory) continue;
                if (rule.regex.IsMatch(rel))
                {
                    ignored = !rule.isNegation;
                }
            }
            return ignored;
        }

        private static Regex CompilePattern(string pattern)
        {
            try
            {
                string regexPattern = Regex.Escape(pattern);
                regexPattern = regexPattern.Replace(@"\*\*", "__DOUBLESTAR__");
                regexPattern = regexPattern.Replace(@"\*", "[^/]*");
                regexPattern = regexPattern.Replace("__DOUBLESTAR__", ".*");
                regexPattern = regexPattern.Replace(@"\?", "[^/]");
                bool containsSlash = pattern.Contains("/");
                if (containsSlash)
                    regexPattern = "^" + regexPattern + "($|/.*)";
                else
                    regexPattern = "(^|.*/)" + regexPattern + "($|/.*)";

                return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, $"Failed to compile gitignore pattern '{pattern}': {ex.Message}");
                return null;
            }
        }
    }
}
#endif
