#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.Onboarding
{
    internal enum OnboardingStepType
    {
        Dialog,
        Confirm,
        Click,
        FieldSelection,
        Toggle,
        MultiAction,
        Action,
        Validation
    }

    internal enum OnboardingAsyncState
    {
        Idle,
        Running,
        Succeeded,
        Failed
    }

    internal sealed class OnboardingAsyncStatus
    {
        public OnboardingAsyncState State { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public float Progress { get; set; } = -1f;

        public bool IsRunning => State == OnboardingAsyncState.Running;
        public bool HasFailed => State == OnboardingAsyncState.Failed;
        public bool HasSucceeded => State == OnboardingAsyncState.Succeeded;

        public static OnboardingAsyncStatus Idle(string message = null)
        {
            return new OnboardingAsyncStatus
            {
                State = OnboardingAsyncState.Idle,
                Message = message ?? string.Empty
            };
        }

        public static OnboardingAsyncStatus Running(string message, float progress = -1f)
        {
            return new OnboardingAsyncStatus
            {
                State = OnboardingAsyncState.Running,
                Message = message ?? string.Empty,
                Progress = progress
            };
        }

        public static OnboardingAsyncStatus Succeeded(string message = null)
        {
            return new OnboardingAsyncStatus
            {
                State = OnboardingAsyncState.Succeeded,
                Message = message ?? string.Empty,
                Progress = 1f
            };
        }

        public static OnboardingAsyncStatus Failed(string errorMessage)
        {
            return new OnboardingAsyncStatus
            {
                State = OnboardingAsyncState.Failed,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? "The onboarding action failed."
                    : errorMessage.Trim()
            };
        }
    }

    internal sealed class OnboardingStep
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string InstructionText { get; set; } = string.Empty;
        public string InlineHintText { get; set; } = string.Empty;
        public string CorrectionText { get; set; } = string.Empty;
        public string RequiredValue { get; set; } = string.Empty;
        public OnboardingStepType StepType { get; set; }
        public List<string> HighlightTargets { get; } = new List<string>();
        public List<string> AllowedTargets { get; } = new List<string>();
        public bool BlockAllOtherUI { get; set; } = true;
        public bool AutoFocusTargets { get; set; } = true;
        public bool ShowContinueButton { get; set; }
        public bool RequireCompletionBeforeContinue { get; set; } = true;
        public bool AutoAdvanceWhenComplete { get; set; }
        public bool AdvanceAfterContinueAction { get; set; } = true;
        public Action OnEnter { get; set; }
        public Action OnExit { get; set; }
        public Action ContinueAction { get; set; }
        public Func<bool> IsComplete { get; set; }
        public Func<bool> HasFailed { get; set; }
        public Action Retry { get; set; }
        public Func<OnboardingAsyncStatus> GetAsyncStatus { get; set; }

        public bool IsDialogLike => StepType == OnboardingStepType.Dialog || StepType == OnboardingStepType.Confirm;

        public bool EvaluateCompletion()
        {
            return IsComplete == null || IsComplete();
        }

        public bool EvaluateFailure()
        {
            return HasFailed != null && HasFailed();
        }
    }
}
#endif
