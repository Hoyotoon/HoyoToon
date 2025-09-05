#if UNITY_EDITOR
using System.Collections.Generic;

namespace HoyoToon.API
{
    /// <summary>
    /// Service interface for HoyoToon configuration data (games, shares, etc.).
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Get games keyed by GameConfig.Key.
        /// </summary>
        IReadOnlyDictionary<string, GameConfig> GetGames();

        /// <summary>
        /// Save the complete games list (overwrites existing).
        /// </summary>
        void SaveGames(IEnumerable<GameConfig> games);

        /// <summary>
        /// Reload underlying storage and clear caches.
        /// </summary>
        void Reload();

        /// <summary>
        /// Absolute path to backing config (if applicable) for tooling.
        /// </summary>
        string ConfigPath { get; }
    }
}
#endif
