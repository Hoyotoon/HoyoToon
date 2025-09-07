#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Utilities;
using HoyoToon.Parsing;

namespace HoyoToon.API
{
    /// <summary>
    /// Global API facade for HoyoToon Editor services.
    /// Allows centralizing service registration and access.
    /// </summary>
    public static partial class HoyoToonApi
    {
        private static IConfigService _config;
        private static IJsonParsingService _parser;

        /// <summary>
        /// Configuration service (JSON-backed by default).
        /// </summary>
        public static IConfigService Config
        {
            get
            {
                if (_config == null)
                {
                    _config = new Services.JsonConfigService();
                }
                return _config;
            }
            set
            {
                _config = value;
                HoyoToonLogger.APIInfo("Config service set via HoyoToonApi.");
            }
        }

        /// <summary>
        /// High-performance JSON parser service (Utf8Json-backed by default).
        /// </summary>
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
                HoyoToonLogger.APIInfo("Parser service set via HoyoToonApi.");
            }
        }
        // Menu: Config file actions (Editor only)
        [MenuItem("HoyoToon/API/Open Config JSON")]
        private static void Menu_OpenConfigJson()
        {
            var path = Config.ConfigPath;
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else HoyoToonLogger.APIWarning($"Resource config not found at: {path}");
        }

        [MenuItem("HoyoToon/API/Reload Config")]
        private static void Menu_ReloadConfig()
        {
            Config.Reload();
            HoyoToonLogger.APIInfo("Resource config reloaded from JSON.");
        }

        // Optional thin wrappers for brevity at call sites
        public static IReadOnlyDictionary<string, GameConfig> GetGames() => Config.GetGames();
        public static void SaveGames(IEnumerable<GameConfig> games) => Config.SaveGames(games);
        public static void ReloadConfig() => Config.Reload();

        // New metadata convenience accessors
        public static IReadOnlyDictionary<string, GameMetadata> GetGameMetadata() => Config.GetGameMetadata();
        public static void SaveGameMetadata(IEnumerable<GameMetadata> games) => Config.SaveGameMetadata(games);
    }
}
#endif
