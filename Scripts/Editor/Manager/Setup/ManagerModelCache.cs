#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HoyoToon;

using HoyoToon.Editor.ResourceSystem;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    internal static class ManagerModelCache
    {
        internal readonly struct ActiveModelInfo
        {
            public readonly string GameKey;
            public readonly string GameDisplayName;

            public ActiveModelInfo(string gameKey, string gameDisplayName)
            {
                GameKey = gameKey;
                GameDisplayName = string.IsNullOrEmpty(gameDisplayName) ? ResolveGameDisplayName(gameKey) : gameDisplayName;
            }

            public bool HasGame => !string.IsNullOrEmpty(GameKey) || !string.IsNullOrEmpty(GameDisplayName);
        }

        private static readonly Dictionary<int, ActiveModelInfo> s_ActiveModelInfo = new Dictionary<int, ActiveModelInfo>();

        public static void SetGameInfo(HoyoToonManager manager, string gameKey, string displayName = null)
        {
            if (manager == null)
            {
                return;
            }

            s_ActiveModelInfo[manager.GetInstanceID()] = new ActiveModelInfo(gameKey, displayName);
        }

        public static bool TryGetGameInfo(HoyoToonManager manager, out ActiveModelInfo info)
        {
            info = default;
            if (manager == null)
            {
                return false;
            }

            return s_ActiveModelInfo.TryGetValue(manager.GetInstanceID(), out info);
        }

        public static void Clear(HoyoToonManager manager)
        {
            if (manager == null)
            {
                return;
            }

            s_ActiveModelInfo.Remove(manager.GetInstanceID());
        }

        public static string ResolveGameDisplayName(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey))
            {
                return "Unknown Game";
            }

            if (ResourceConfig.Games != null && ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
            {
                if (!string.IsNullOrEmpty(gameConfig?.DisplayName))
                {
                    return gameConfig.DisplayName;
                }
            }

            return gameKey;
        }
    }
}
#endif
