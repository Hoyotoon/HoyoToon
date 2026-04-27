#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Updater
{
    internal sealed class UpdaterKeepRules
    {
        private readonly List<Rule> rules = new List<Rule>();

        private struct Rule
        {
            public Regex Regex;
            public bool IsNegation;
        }

        public static UpdaterKeepRules Load(string rootPath)
        {
            var keepRules = new UpdaterKeepRules();
            string gitIgnorePath = Path.Combine(rootPath, ".gitignore");
            if (!File.Exists(gitIgnorePath))
            {
                return keepRules;
            }

            try
            {
                keepRules.AddRulesFromText(File.ReadAllText(gitIgnorePath));
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, $"Failed to read updater keep rules from .gitignore: {exception.Message}");
                keepRules.rules.Clear();
            }

            return keepRules;
        }

        public static UpdaterKeepRules FromGitIgnoreText(string gitIgnoreText)
        {
            var keepRules = new UpdaterKeepRules();
            try
            {
                keepRules.AddRulesFromText(gitIgnoreText);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, $"Failed to parse updater keep rules from .gitignore: {exception.Message}");
                keepRules.rules.Clear();
            }

            return keepRules;
        }

        public void Merge(UpdaterKeepRules other)
        {
            if (other == null || other.rules.Count == 0)
            {
                return;
            }

            rules.AddRange(other.rules);
        }

        public bool IsKept(string relativePath)
        {
            if (rules.Count <= 0 || string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalized = relativePath.Replace('\\', '/');
            bool kept = false;
            foreach (Rule rule in rules)
            {
                if (rule.Regex == null || !rule.Regex.IsMatch(normalized))
                {
                    continue;
                }

                kept = !rule.IsNegation;
            }

            return kept;
        }

        private void AddRulesFromText(string gitIgnoreText)
        {
            if (string.IsNullOrEmpty(gitIgnoreText))
            {
                return;
            }

            string normalizedText = gitIgnoreText.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalizedText.Split('\n');
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
                    TryAddRule(trimmed.Substring(trimmed.IndexOf(':') + 1).Trim());
                    foundTaggedSyntax = true;
                    continue;
                }

                if (!inTaggedBlock || string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                TryAddRule(trimmed);
            }

            if (!foundTaggedSyntax)
            {
                rules.Clear();
            }
        }

        private void TryAddRule(string pattern)
        {
            Rule rule = BuildRule(pattern);
            if (rule.Regex != null)
            {
                rules.Add(rule);
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
                IsNegation = isNegation,
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
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, $"Failed to compile updater keep rule '{pattern}': {exception.Message}");
                return null;
            }
        }
    }
}
#endif
