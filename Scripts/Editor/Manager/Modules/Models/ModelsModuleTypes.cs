#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Editor.API;
using HoyoToon.Editor.ResourceSystem;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class GameFolderCache
    {
        public GameFolderCache(string gameFolder)
        {
            GameFolderName = gameFolder;
            VariantsByCharacter = new Dictionary<string, List<VariantOption>>(StringComparer.OrdinalIgnoreCase);
        }

        public string GameFolderName { get; }
        public string CharactersFolderName { get; set; }
        public List<string> Characters { get; set; } = new List<string>();
        public Dictionary<string, List<VariantOption>> VariantsByCharacter { get; }
        public bool IsLoadingCharacters { get; set; }
        public bool IsLoadingVariants { get; set; }
    }

    internal sealed class VariantOption
    {
        public VariantOption(string name, string relativePath)
        {
            Name = name;
            RelativePath = relativePath;
        }

        public string Name { get; }
        public string RelativePath { get; }
    }

    internal sealed class GameOption
    {
        public GameOption(GameConfig config, string folderName)
        {
            Config = config;
            FolderName = folderName;
        }

        public GameConfig Config { get; }
        public string FolderName { get; }
        public string Key => Config?.Key;
        public string DisplayName => string.IsNullOrEmpty(Config?.DisplayName) ? Config?.Key : Config.DisplayName;
    }

    internal enum FbxVariantType
    {
        Unknown,
        WithAnims,
        NoAnims
    }

    internal enum HsrFbxChoice
    {
        WithAnims,
        NoAnims,
        Both
    }
}
#endif
