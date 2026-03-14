#if UNITY_EDITOR
using System.Collections.Generic;
using HoyoToon.Editor.ResourceSystem;

namespace HoyoToon.Editor.API
{
    public interface IConfigService
    {
        IReadOnlyDictionary<string, GameConfig> GetGames();

        void SaveGames(IEnumerable<GameConfig> games);

        void Reload();

        string ConfigPath { get; }

        IReadOnlyDictionary<string, GameMetadata> GetGameMetadata();

        void SaveGameMetadata(IEnumerable<GameMetadata> games);

        IReadOnlyDictionary<string, ConverterProfile> GetConverterProfiles();

        void SaveConverterProfiles(IEnumerable<ConverterProfile> profiles);
    }
}
#endif
