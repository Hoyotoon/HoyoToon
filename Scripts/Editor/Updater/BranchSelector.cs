#if UNITY_EDITOR
using UnityEditor;

namespace HoyoToon.Updater
{
    internal static class BranchSelector
    {
        private const string PrefKey = "HoyoToon.Updater.CurrentBranch";
        private const string PrefCleanOnSwitchKey = "HoyoToon.Updater.CleanOnSwitch";

        public static string GetCurrentBranch()
        {
            var def = UpdaterSettings.Instance.defaultBranch;
            var value = EditorPrefs.GetString(PrefKey, def);
            return string.IsNullOrWhiteSpace(value) ? def : value;
        }

        public static void SetBranch(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return;
            EditorPrefs.SetString(PrefKey, branch);
            EditorPrefs.SetBool(PrefCleanOnSwitchKey, true);
        }

        public static bool ConsumeCleanFlag()
        {
            bool need = EditorPrefs.GetBool(PrefCleanOnSwitchKey, false);
            if (need) EditorPrefs.DeleteKey(PrefCleanOnSwitchKey);
            return need;
        }

        public static bool IsCleanPending()
        {
            return EditorPrefs.GetBool(PrefCleanOnSwitchKey, false);
        }
    }
}
#endif