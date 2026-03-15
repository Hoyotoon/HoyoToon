#if UNITY_EDITOR
using UnityEditor;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal static class BranchSelector
    {
        private const string PrefKey = PrefsKeys.UpdaterCurrentBranch;
        private const string PrefCleanOnSwitchKey = PrefsKeys.UpdaterCleanOnSwitch;

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

        public static bool IsCleanPending()
        {
            return EditorPrefs.GetBool(PrefCleanOnSwitchKey, false);
        }

        public static bool ConsumeCleanFlag()
        {
            bool need = EditorPrefs.GetBool(PrefCleanOnSwitchKey, false);
            if (need) EditorPrefs.DeleteKey(PrefCleanOnSwitchKey);
            return need;
        }
    }
}
#endif