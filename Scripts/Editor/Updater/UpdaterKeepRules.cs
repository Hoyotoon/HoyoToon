#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal sealed class UpdaterKeepRules
    {
        private readonly List<Rule> _rules = new List<Rule>();

        private struct Rule
        {
            public Regex Regex;
            public bool IsNegation;
        }

        public bool HasRules => _rules.Count > 0;

        public static UpdaterKeepRules Load(string rootPath)
        {
            var rules = new UpdaterKeepRules();
            string gitIgnorePath = Path.Combine(rootPath, ".gitignore");
            if (!File.Exists(gitIgnorePath))
            {
                return rules;
            }

            try
            {
                string[] lines = File.ReadAllLines(gitIgnorePath);
                bool inTaggedBlock = false;
                bool foundTaggedSyntax = false;

                foreach (string rawLine in lines)
                {
                    string trimmed = rawLine.Trim();
                    string upper = trimmed.ToUpperInvariant();

                    if (upper == "# HOYOTOON-UPDATER-KEEP START")
                    {
                        inTaggedBlock = true;
                        foundTaggedSyntax = true;
                        continue;
                    }

                    if (upper == "# HOYOTOON-UPDATER-KEEP END")
                    {
                        inTaggedBlock = false;
                        continue;
                    }

                    if (trimmed.StartsWith("# updater-keep:", StringComparison.OrdinalIgnoreCase))
                    {
                        string pattern = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                        rules.TryAddRule(pattern);
                        foundTaggedSyntax = true;
                        continue;
                    }

                    if (!inTaggedBlock)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    rules.TryAddRule(trimmed);
                }

                if (!foundTaggedSyntax)
                {
                    rules._rules.Clear();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, $"Failed to read updater keep rules from .gitignore: {ex.Message}");
                rules._rules.Clear();
            }

            return rules;
        }

        public bool IsKept(string relativePath, bool isDirectory)
        {
            if (!HasRules || string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalized = relativePath.Replace('\\', '/');
            bool kept = false;
            foreach (Rule rule in _rules)
            {
                if (rule.Regex == null)
                {
                    continue;
                }

                if (!rule.Regex.IsMatch(normalized))
                {
                    continue;
                }

                kept = !rule.IsNegation;
            }

            return kept;
        }

        private void TryAddRule(string pattern)
        {
            Rule rule = BuildRule(pattern);
            if (rule.Regex != null)
            {
                _rules.Add(rule);
            }
        }

        private static Rule BuildRule(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return default;
            }

            bool isNegation = false;
            string working = pattern.Trim();
            if (working.StartsWith("!", StringComparison.Ordinal))
            {
                isNegation = true;
                working = working.Substring(1);
            }

            working = working.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(working))
            {
                return default;
            }

            return new Rule
            {
                Regex = CompilePattern(working),
                IsNegation = isNegation
            };
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

                if (pattern.Contains("/", StringComparison.Ordinal))
                {
                    regexPattern = "^" + regexPattern + "($|/.*)";
                }
                else
                {
                    regexPattern = "(^|.*/)" + regexPattern + "($|/.*)";
                }

                return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, $"Failed to compile updater keep rule '{pattern}': {ex.Message}");
                return null;
            }
        }
    }
}
#endif