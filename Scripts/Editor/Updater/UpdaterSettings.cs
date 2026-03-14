#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace HoyoToon.Editor.Updater
{
    internal sealed class UpdaterSettings
    {
        private UpdaterSettings() { }

        public static readonly UpdaterSettings Instance = new UpdaterSettings();

        public string repoOwner => "HoyoToon";
        public string repoName => "HoyoToon";
        public string defaultBranch => "Beta";

        public string packageFolderRelativeToProject => "Packages/com.hoyotoon.hoyotoon";
        public string toolRelativeRoot => string.Empty;
        public string packageJsonRelativePath => "package.json";

        public string githubToken => string.Empty;
        public bool respectGitIgnoreForDeletions => true;
    }
}
#endif
