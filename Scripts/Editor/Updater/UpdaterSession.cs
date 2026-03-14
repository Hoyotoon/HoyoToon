#if UNITY_EDITOR
namespace HoyoToon.Editor.Updater
{
    internal sealed class UpdaterSession
    {
        internal UpdaterSession(UpdaterSettings settings)
        {
            Settings = settings ?? UpdaterSettings.Instance;
            Branch = BranchSelector.GetCurrentBranch();
            Api = new GitHubApiClient(Settings.repoOwner, Settings.repoName, Branch, Settings.githubToken);
        }

        internal UpdaterSettings Settings { get; }

        internal string Branch { get; }

        internal GitHubApiClient Api { get; }
    }
}
#endif