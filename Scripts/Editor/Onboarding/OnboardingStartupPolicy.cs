#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Onboarding
{
    internal readonly struct OnboardingStartupPolicySnapshot
    {
        public readonly bool IsFirstTimeSetup;
        public readonly bool HasSeenTour;
        public readonly bool IsBatchMode;
        public readonly bool StartupEntryHandled;
        public readonly string StartupEntrySource;

        public bool CanOnboardingOwnStartup => IsFirstTimeSetup && !HasSeenTour && !IsBatchMode;
        public bool ShouldDeferPrerequisiteDialogs => CanOnboardingOwnStartup;

        public OnboardingStartupPolicySnapshot(
            bool isFirstTimeSetup,
            bool hasSeenTour,
            bool isBatchMode,
            bool startupEntryHandled,
            string startupEntrySource)
        {
            IsFirstTimeSetup = isFirstTimeSetup;
            HasSeenTour = hasSeenTour;
            IsBatchMode = isBatchMode;
            StartupEntryHandled = startupEntryHandled;
            StartupEntrySource = startupEntrySource ?? string.Empty;
        }

        public string GetStartupSourceOrDefault(string defaultValue)
        {
            return string.IsNullOrEmpty(StartupEntrySource) ? defaultValue : StartupEntrySource;
        }
    }

    internal static class OnboardingStartupPolicy
    {
        private const string StartupEntryHandledKey = "HoyoToon.Tour.StartupEntryHandled";
        private const string StartupEntrySourceKey = "HoyoToon.Tour.StartupEntrySource";

        public static OnboardingStartupPolicySnapshot Evaluate()
        {
            return new OnboardingStartupPolicySnapshot(
                isFirstTimeSetup: !PrefsKeys.GetResourceFirstTimeSetupCompleted(),
                hasSeenTour: PrefsKeys.GetTourShown(),
                isBatchMode: Application.isBatchMode,
                startupEntryHandled: SessionState.GetBool(StartupEntryHandledKey, false),
                startupEntrySource: SessionState.GetString(StartupEntrySourceKey, string.Empty));
        }

        public static bool TryAcquireStartupEntryForSession(string source)
        {
            string normalizedSource = string.IsNullOrEmpty(source) ? "Unknown" : source;
            var snapshot = Evaluate();
            if (snapshot.StartupEntryHandled)
            {
                HoyoToonLogger.Log(
                    HoyoToonLogger.Categories.Tour,
                    LogLevel.Info,
                    $"OnboardingDiag event=startup-entry-denied source={normalizedSource} existingSource={snapshot.GetStartupSourceOrDefault("Unknown")}");
                return false;
            }

            SessionState.SetBool(StartupEntryHandledKey, true);
            SessionState.SetString(StartupEntrySourceKey, normalizedSource);
            HoyoToonLogger.Log(
                HoyoToonLogger.Categories.Tour,
                LogLevel.Info,
                $"OnboardingDiag event=startup-entry-acquired source={normalizedSource}");
            return true;
        }

        public static void ClearStartupSourceForSession()
        {
            SessionState.SetString(StartupEntrySourceKey, string.Empty);
        }

        public static string GetStartupSourceOrManual()
        {
            return Evaluate().GetStartupSourceOrDefault("Manual");
        }
    }
}
#endif