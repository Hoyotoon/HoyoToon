#if UNITY_EDITOR
using System;
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;

namespace HoyoToon.Editor.Onboarding
{
    internal static class OnboardingPersistence
    {
        public const string CompletedKey = "HoyoToon.Onboarding.Completed";
        public const string CompletedVersionKey = "HoyoToon.Onboarding.CompletedVersion";
        public const string DebugToolsEnabledKey = "HoyoToon.Onboarding.DebugToolsEnabled";
        private const string ResumeStepKey = "HoyoToon.Onboarding.ResumeStep";
        private const string ResumeDebugRunKey = "HoyoToon.Onboarding.ResumeDebugRun";
        public const string CurrentVersion = "2026.04.complete";

        public static bool IsCompleted => EditorPrefs.GetBool(ScopedKey(CompletedKey), false)
            && string.Equals(CompletedVersion, CurrentVersion, StringComparison.Ordinal);

        public static string CompletedVersion => EditorPrefs.GetString(ScopedKey(CompletedVersionKey), string.Empty);

        public static bool ShouldStartOnboarding => HasResumeStep || !IsCompleted;

        public static bool DebugToolsEnabled => EditorPrefs.GetBool(ScopedKey(DebugToolsEnabledKey), false);

        public static bool HasResumeStep => !string.IsNullOrWhiteSpace(ResumeStepId);

        public static string ResumeStepId => EditorPrefs.GetString(ScopedKey(ResumeStepKey), string.Empty);

        public static bool ResumeDebugRun => EditorPrefs.GetBool(ScopedKey(ResumeDebugRunKey), false);

        public static void SetDebugToolsEnabled(bool enabled)
        {
            string key = ScopedKey(DebugToolsEnabledKey);
            if (enabled)
            {
                EditorPrefs.SetBool(key, true);
            }
            else
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        public static void SaveResumeStep(string stepId, bool debugRun)
        {
            if (string.IsNullOrWhiteSpace(stepId))
            {
                ClearResumeStep();
                return;
            }

            EditorPrefs.SetString(ScopedKey(ResumeStepKey), stepId.Trim());
            EditorPrefs.SetBool(ScopedKey(ResumeDebugRunKey), debugRun);
        }

        public static void ClearResumeStep()
        {
            EditorPrefs.DeleteKey(ScopedKey(ResumeStepKey));
            EditorPrefs.DeleteKey(ScopedKey(ResumeDebugRunKey));
        }

        public static void MarkCompleted()
        {
            EditorPrefs.SetBool(ScopedKey(CompletedKey), true);
            EditorPrefs.SetString(ScopedKey(CompletedVersionKey), CurrentVersion);
            ClearResumeStep();
        }

        public static void Reset()
        {
            HoyoToonEditorPrefs.DeleteProjectKeyAndLegacy(CompletedKey);
            HoyoToonEditorPrefs.DeleteProjectKeyAndLegacy(CompletedVersionKey);
            HoyoToonEditorPrefs.DeleteProjectKeyAndLegacy(DebugToolsEnabledKey);
            HoyoToonEditorPrefs.DeleteProjectKeyAndLegacy(ResumeStepKey);
            HoyoToonEditorPrefs.DeleteProjectKeyAndLegacy(ResumeDebugRunKey);
            ClearResumeStep();
        }

        private static string ScopedKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }
    }
}
#endif
