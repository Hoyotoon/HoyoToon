#if UNITY_EDITOR
using System.Collections.Generic;
using HoyoToon;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.API
{
    public static class Api
    {
        private static IConfigService _config;
        private static IJsonParsingService _parser;

        public static IConfigService Config
        {
            get
            {
                if (_config == null)
                {
                    _config = new JsonConfigService();
                }
                return _config;
            }
            set
            {
                _config = value;
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, "Config service set via Api.");
            }
        }

        public static IJsonParsingService Parser
        {
            get
            {
                if (_parser == null)
                {
                    _parser = new Utf8JsonParsingService();
                }
                return _parser;
            }
            set
            {
                _parser = value;
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, "Parser service set via Api.");
            }
        }

        // Optional thin wrappers for brevity at call sites
        public static IReadOnlyDictionary<string, GameConfig> GetGames() => Config.GetGames();
        public static void ReloadConfig() => Config.Reload();

        // New metadata convenience accessors
        public static IReadOnlyDictionary<string, GameMetadata> GetGameMetadata() => Config.GetGameMetadata();

        // Converter profiles (Hoyo2Unity / Hoyo2VRC)
        public static IReadOnlyDictionary<string, ConverterProfile> GetConverterProfiles() => Config.GetConverterProfiles();
        public static ConverterProfile GetConverterProfile(string key) =>
            string.IsNullOrWhiteSpace(key)
                ? null
                : (Config.GetConverterProfiles() is { } map && map.TryGetValue(key, out var profile) ? profile : null);
    }
}
#endif
