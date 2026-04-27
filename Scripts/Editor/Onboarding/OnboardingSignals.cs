#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace HoyoToon.Editor.Onboarding
{
    internal enum OnboardingOperationKind
    {
        Download,
        Setup,
        Render,
        TurnaroundRender
    }

    internal static class OnboardingSignals
    {
        private static readonly Dictionary<string, int> ActionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> StepStartActionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> LastValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> ValueChangeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> StepStartValueChangeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> StepRestoreBaselineValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> StepRestoreCurrentValues = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> StepRestoreChangedAway = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<OnboardingOperationKind, bool> SimulatedSuccess = new Dictionary<OnboardingOperationKind, bool>();
        private static readonly Dictionary<OnboardingOperationKind, bool> SimulatedFailure = new Dictionary<OnboardingOperationKind, bool>();
        private static readonly Dictionary<OnboardingOperationKind, string> LastOperationErrors = new Dictionary<OnboardingOperationKind, string>();
        private static readonly Dictionary<OnboardingOperationKind, double> LastOperationSuccessTime = new Dictionary<OnboardingOperationKind, double>();

        public static string CurrentStepId { get; private set; } = string.Empty;

        public static void MarkStepStart(string stepId)
        {
            CurrentStepId = stepId ?? string.Empty;
            StepStartActionCounts.Clear();
            foreach (KeyValuePair<string, int> pair in ActionCounts)
            {
                StepStartActionCounts[pair.Key] = pair.Value;
            }

            StepStartValueChangeCounts.Clear();
            foreach (KeyValuePair<string, int> pair in ValueChangeCounts)
            {
                StepStartValueChangeCounts[pair.Key] = pair.Value;
            }

            StepRestoreBaselineValues.Clear();
            StepRestoreCurrentValues.Clear();
            StepRestoreChangedAway.Clear();
        }

        public static void RecordAction(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string key = targetId.Trim();
            ActionCounts[key] = GetActionCount(key) + 1;
        }

        public static int GetActionCount(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 0;
            }

            int count;
            return ActionCounts.TryGetValue(targetId.Trim(), out count) ? count : 0;
        }

        public static int GetActionCountSinceStepStart(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 0;
            }

            string key = targetId.Trim();
            int startCount;
            StepStartActionCounts.TryGetValue(key, out startCount);
            return Math.Max(0, GetActionCount(key) - startCount);
        }

        public static void RecordValueChanged(string targetId, object value)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string key = targetId.Trim();
            string serializedValue = SerializeValue(value);
            string previousValue;
            if (LastValues.TryGetValue(key, out previousValue)
                && string.Equals(previousValue, serializedValue, StringComparison.Ordinal))
            {
                return;
            }

            LastValues[key] = serializedValue;
            ValueChangeCounts[key] = GetValueChangeCount(key) + 1;
            RecordAction(key);
        }

        public static void RecordRestorableValueChanged(string targetId, object previousValue, object newValue)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string key = targetId.Trim();
            string serializedPreviousValue = SerializeValue(previousValue);
            string serializedNewValue = SerializeValue(newValue);
            if (!StepRestoreBaselineValues.ContainsKey(key))
            {
                StepRestoreBaselineValues[key] = serializedPreviousValue;
            }

            StepRestoreCurrentValues[key] = serializedNewValue;
            string baselineValue;
            if (StepRestoreBaselineValues.TryGetValue(key, out baselineValue)
                && !string.Equals(serializedNewValue, baselineValue, StringComparison.Ordinal))
            {
                StepRestoreChangedAway.Add(key);
            }

            RecordValueChanged(key, newValue);
        }

        public static int GetValueChangeCount(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return 0;
            }

            int count;
            return ValueChangeCounts.TryGetValue(targetId.Trim(), out count) ? count : 0;
        }

        public static int GetValueChangeCountSinceStepStart(params string[] targetIds)
        {
            if (targetIds == null || targetIds.Length <= 0)
            {
                return 0;
            }

            int count = 0;
            foreach (string rawId in targetIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                string id = rawId.Trim();
                int startCount;
                StepStartValueChangeCounts.TryGetValue(id, out startCount);
                count += Math.Max(0, GetValueChangeCount(id) - startCount);
            }

            return count;
        }

        public static bool WasValueChangedAndRestoredSinceStepStart(params string[] targetIds)
        {
            if (targetIds == null || targetIds.Length <= 0)
            {
                return false;
            }

            foreach (string rawId in targetIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                string id = rawId.Trim();
                string baselineValue;
                string currentValue;
                if (!StepRestoreChangedAway.Contains(id)
                    || !StepRestoreBaselineValues.TryGetValue(id, out baselineValue)
                    || !StepRestoreCurrentValues.TryGetValue(id, out currentValue))
                {
                    continue;
                }

                if (string.Equals(baselineValue, currentValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static void RecordOperationSuccess(OnboardingOperationKind kind)
        {
            SimulatedFailure[kind] = false;
            SimulatedSuccess[kind] = true;
            LastOperationErrors.Remove(kind);
            LastOperationSuccessTime[kind] = UnityEditor.EditorApplication.timeSinceStartup;
        }

        public static void RecordOperationFailure(OnboardingOperationKind kind, string errorMessage)
        {
            SimulatedSuccess[kind] = false;
            SimulatedFailure[kind] = true;
            LastOperationErrors[kind] = string.IsNullOrWhiteSpace(errorMessage)
                ? "The simulated onboarding operation failed."
                : errorMessage.Trim();
        }

        public static void ClearOperationSimulation(OnboardingOperationKind kind)
        {
            SimulatedSuccess.Remove(kind);
            SimulatedFailure.Remove(kind);
            LastOperationErrors.Remove(kind);
        }

        public static bool HasOperationSucceeded(OnboardingOperationKind kind)
        {
            bool simulated;
            return SimulatedSuccess.TryGetValue(kind, out simulated) && simulated;
        }

        public static bool HasOperationFailed(OnboardingOperationKind kind)
        {
            bool simulated;
            return SimulatedFailure.TryGetValue(kind, out simulated) && simulated;
        }

        public static string GetOperationError(OnboardingOperationKind kind)
        {
            string error;
            return LastOperationErrors.TryGetValue(kind, out error) ? error : string.Empty;
        }

        public static void ResetAll()
        {
            ActionCounts.Clear();
            StepStartActionCounts.Clear();
            LastValues.Clear();
            ValueChangeCounts.Clear();
            StepStartValueChangeCounts.Clear();
            StepRestoreBaselineValues.Clear();
            StepRestoreCurrentValues.Clear();
            StepRestoreChangedAway.Clear();
            SimulatedSuccess.Clear();
            SimulatedFailure.Clear();
            LastOperationErrors.Clear();
            LastOperationSuccessTime.Clear();
        }

        private static string SerializeValue(object value)
        {
            return value != null ? value.ToString() : string.Empty;
        }
    }
}
#endif
