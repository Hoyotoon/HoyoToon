#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;

namespace HoyoToon.Editor.ResourceSystem
{
    public static class ResourceConfig
    {
        private static Lazy<Dictionary<string, GameConfig>> _games = CreateGamesLazy();

        private static Lazy<Dictionary<string, GameConfig>> CreateGamesLazy()
        {
            return new Lazy<Dictionary<string, GameConfig>>(
                () => new Dictionary<string, GameConfig>(HoyoToon.Editor.API.Api.GetGames()),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public static Dictionary<string, GameConfig> Games
        {
            get { return _games.Value; }
        }

        public static void Reload()
        {
            HoyoToon.Editor.API.Api.ReloadConfig();
            _games = CreateGamesLazy();
        }
    }

    public class GameConfig
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string WebdavUrl { get; set; }
        public string LocalPath { get; set; }
    }
}
#endif
