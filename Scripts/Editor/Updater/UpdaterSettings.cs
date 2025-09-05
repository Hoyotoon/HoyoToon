#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace HoyoToon.Updater
{
    /// <summary>
    /// Immutable, code-defined settings for the updater. These values are fixed for production use.
    /// </summary>
    internal sealed class UpdaterSettings
    {
        private UpdaterSettings() { }

        // Singleton instance (code-only, not an asset)
        public static readonly UpdaterSettings Instance = new UpdaterSettings();

        // Repository (configure in code only)
        public string repoOwner => "HoyoToon";
        public string repoName => "HoyoToon";
        public string defaultBranch => "Beta"; // Default to Beta as requested

        // Package paths
        public string packageFolderRelativeToProject => "Packages/com.meliverse.hoyotoon";
        public string toolRelativeRoot => string.Empty; // optional subfolder; empty => package root
        public string packageJsonRelativePath => "package.json";

        // Optional: token for rate limit or private repos; leave empty by default in production
        public string githubToken => string.Empty;

        // Backward-compatible helper for existing callers
        public static UpdaterSettings FindOrCreate() => Instance;
    }
}
#endif
