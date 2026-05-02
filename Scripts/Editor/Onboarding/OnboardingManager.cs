#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.UI.Manager;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Onboarding
{
    [InitializeOnLoad]
    internal static class OnboardingManager
    {
        private static readonly OnboardingDialogController DialogController = new OnboardingDialogController();
        private static IReadOnlyList<OnboardingStep> steps = Array.Empty<OnboardingStep>();
        private static int currentStepIndex = -1;
        private static OnboardingOverlay overlay;
        private static OnboardingInputBlocker inputBlocker;
        private static HoyoToonManagerWindow managerWindow;
        private static VisualElement managerRoot;
        private static bool isRunning;
        private static bool isDebugRun;
        private static bool forcedFailure;
        private static string forcedFailureMessage = string.Empty;
        private static string debugHeldStepId = string.Empty;
        private static double debugHoldUntilTime;
        private static bool targetRefreshQueued;
        private static bool applyManagerUiContextOnNextEntry;
        private static double lastFocusTime;
        private static double lastDialogRefreshTime;
        private static IReadOnlyList<string> missingTargets = Array.Empty<string>();

        static OnboardingManager()
        {
            OnboardingTargetRegistry.TargetsChanged += HandleTargetsChanged;
            EditorApplication.delayCall += StartFirstTimeIfNeeded;
        }

        public static event Action StateChanged;

        public static bool IsRunning => isRunning;
        public static bool IsDebugRun => isDebugRun;
        public static int CurrentStepIndex => currentStepIndex;
        public static IReadOnlyList<OnboardingStep> Steps => steps;
        public static OnboardingStep CurrentStep => currentStepIndex >= 0 && currentStepIndex < steps.Count ? steps[currentStepIndex] : null;
        public static IReadOnlyList<string> MissingTargets => missingTargets;
        public static bool InputBlockingEnabled { get; set; } = true;
        public static bool OverlayDimmingEnabled { get; set; } = true;
        public static bool DrawAllTargetRects { get; set; }
        public static bool DebugToolsEnabled
        {
            get
            {
                return Unsupported.IsDeveloperMode()
                    || OnboardingPersistence.DebugToolsEnabled;
            }
        }

        public static void SetDebugToolsEnabled(bool enabled)
        {
            OnboardingPersistence.SetDebugToolsEnabled(enabled);
        }

        public static void StartFirstTimeIfNeeded()
        {
            if (Application.isBatchMode || isRunning)
            {
                return;
            }

            bool hasResumeStep = OnboardingPersistence.HasResumeStep;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += StartFirstTimeIfNeeded;
                return;
            }

            if (!OnboardingPersistence.ShouldStartOnboarding)
            {
                OnboardingValidation.PromptForMissingLocalUserProfileAfterCompletedOnboarding();
                return;
            }

            StartOnboarding(hasResumeStep && OnboardingPersistence.ResumeDebugRun, hasResumeStep ? OnboardingPersistence.ResumeStepId : null);
        }

        public static void StartOnboarding(bool debugRun, string startStepId = null)
        {
            steps = OnboardingStepDefinitions.BuildDefaultFlow();
            isRunning = true;
            isDebugRun = debugRun;
            forcedFailure = false;
            forcedFailureMessage = string.Empty;
            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            applyManagerUiContextOnNextEntry = false;
            OnboardingSignals.ResetAll();
            SubscribeUpdate();

            currentStepIndex = ResolveStartIndex(startStepId);
            if (debugRun && !string.IsNullOrWhiteSpace(startStepId))
            {
                HoldCurrentStepForDebug();
            }

            EnterCurrentStep();
            StateChanged?.Invoke();
        }

        public static void StopOnboarding(bool markComplete)
        {
            OnboardingStep step = CurrentStep;
            step?.OnExit?.Invoke();

            isRunning = false;
            isDebugRun = false;
            currentStepIndex = -1;
            forcedFailure = false;
            forcedFailureMessage = string.Empty;
            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            applyManagerUiContextOnNextEntry = false;
            missingTargets = Array.Empty<string>();
            overlay?.SetStep(null);
            inputBlocker?.SetStep(null);
            DialogController.Close();
            UnsubscribeUpdate();

            if (markComplete)
            {
                OnboardingPersistence.MarkCompleted();
            }
            else
            {
                OnboardingPersistence.ClearResumeStep();
            }

            StateChanged?.Invoke();
        }

        public static void AttachToManagerWindow(HoyoToonManagerWindow window, VisualElement root)
        {
            managerWindow = window;
            managerRoot = root;

            if (root == null)
            {
                return;
            }

            overlay = root.Q<OnboardingOverlay>("HoyoToonOnboardingOverlay");
            if (overlay == null)
            {
                overlay = new OnboardingOverlay();
                root.Add(overlay);
            }

            inputBlocker = new OnboardingInputBlocker(root);
            ApplyVisualState();
        }

        public static void NotifyManagerClosed(HoyoToonManagerWindow window)
        {
            if (!ReferenceEquals(window, managerWindow))
            {
                return;
            }

            managerWindow = null;
            managerRoot = null;
            overlay = null;
            inputBlocker = null;
        }

        public static void RefreshDialog()
        {
            if (!isRunning || CurrentStep == null)
            {
                return;
            }

            ShowDialogForCurrentStep();
        }

        public static void HandleDialogContinue()
        {
            if (!isRunning || CurrentStep == null)
            {
                return;
            }

            OnboardingStep step = CurrentStep;
            if (!CanContinue(step))
            {
                NotifyBlockedInput();
                return;
            }

            if (step.ContinueAction != null)
            {
                try
                {
                    step.ContinueAction.Invoke();
                }
                catch (Exception exception)
                {
                    forcedFailure = true;
                    forcedFailureMessage = exception.Message;
                    Debug.LogException(exception);
                }

                ShowDialogForCurrentStep();

                if (!step.AdvanceAfterContinueAction)
                {
                    return;
                }
            }

            Advance();
        }

        public static void RetryCurrentStep()
        {
            if (!isRunning || CurrentStep == null)
            {
                return;
            }

            forcedFailure = false;
            forcedFailureMessage = string.Empty;
            CurrentStep.Retry?.Invoke();
            applyManagerUiContextOnNextEntry = true;
            EnterCurrentStep(reenter: true);
        }

        public static void SkipCurrentStepForDebug()
        {
            if (!CanUseDebugTools())
            {
                return;
            }

            Advance(applyManagerUiContext: true);
        }

        public static void PreviousStepForDebug()
        {
            if (!CanUseDebugTools() || steps.Count <= 0)
            {
                return;
            }

            OnboardingStep step = CurrentStep;
            step?.OnExit?.Invoke();
            currentStepIndex = Mathf.Clamp(currentStepIndex - 1, 0, steps.Count - 1);
            HoldCurrentStepForDebug();
            applyManagerUiContextOnNextEntry = true;
            EnterCurrentStep();
        }

        public static void NextStepForDebug()
        {
            if (!CanUseDebugTools())
            {
                return;
            }

            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            Advance(applyManagerUiContext: true);
        }

        public static void JumpToStepForDebug(string stepId)
        {
            if (!CanUseDebugTools() || string.IsNullOrWhiteSpace(stepId))
            {
                return;
            }

            int index = ResolveStartIndex(stepId);
            if (index < 0 || index >= steps.Count)
            {
                return;
            }

            OnboardingStep step = CurrentStep;
            step?.OnExit?.Invoke();
            currentStepIndex = index;
            HoldCurrentStepForDebug();
            applyManagerUiContextOnNextEntry = true;
            EnterCurrentStep();
        }

        public static void ReRunCurrentStepForDebug()
        {
            if (!CanUseDebugTools())
            {
                return;
            }

            HoldCurrentStepForDebug();
            applyManagerUiContextOnNextEntry = true;
            EnterCurrentStep(reenter: true);
        }

        public static void MarkCurrentStepCompleteForDebug()
        {
            if (!CanUseDebugTools())
            {
                return;
            }

            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            Advance(applyManagerUiContext: true);
        }

        public static void ForceCurrentStepFailureForDebug()
        {
            if (!CanUseDebugTools())
            {
                return;
            }

            forcedFailure = true;
            forcedFailureMessage = "Debug forced this step into a failure state.";
            ShowDialogForCurrentStep();
        }

        public static void NotifyBlockedInput()
        {
            overlay?.ShowReminder("Follow the highlighted step first.");
        }

        public static void FlashTarget(string targetId)
        {
            overlay?.Flash(targetId);
        }

        public static bool CanUseDebugTools()
        {
            return DebugToolsEnabled;
        }

        private static int ResolveStartIndex(string stepId)
        {
            if (steps == null || steps.Count <= 0)
            {
                return -1;
            }

            if (string.IsNullOrWhiteSpace(stepId))
            {
                return 0;
            }

            for (int index = 0; index < steps.Count; index++)
            {
                if (string.Equals(steps[index].Id, stepId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return 0;
        }

        private static void EnterCurrentStep(bool reenter = false)
        {
            OnboardingStep step = CurrentStep;
            if (step == null)
            {
                StopOnboarding(markComplete: true);
                return;
            }

            forcedFailure = false;
            forcedFailureMessage = string.Empty;
            OnboardingSignals.MarkStepStart(step.Id);
            OnboardingPersistence.SaveResumeStep(step.Id, isDebugRun);
            bool shouldApplyManagerUiContext = applyManagerUiContextOnNextEntry;
            applyManagerUiContextOnNextEntry = false;

            if (reenter)
            {
                step.OnExit?.Invoke();
            }

            try
            {
                if (shouldApplyManagerUiContext)
                {
                    ApplyManagerUiContextForStep(step);
                }

                step.OnEnter?.Invoke();
            }
            catch (Exception exception)
            {
                forcedFailure = true;
                forcedFailureMessage = exception.Message;
                Debug.LogException(exception);
            }

            lastFocusTime = 0d;
            ApplyVisualState();
            RefreshMissingTargets();
            FocusCurrentTargets(force: true);
            RefreshMissingTargets();
            ShowDialogForCurrentStep();
            StateChanged?.Invoke();
        }

        private static void Advance(bool applyManagerUiContext = false)
        {
            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            OnboardingStep step = CurrentStep;
            step?.OnExit?.Invoke();

            currentStepIndex++;
            if (currentStepIndex >= steps.Count)
            {
                StopOnboarding(markComplete: true);
                return;
            }

            applyManagerUiContextOnNextEntry = applyManagerUiContext;
            EnterCurrentStep();
        }

        private static void ApplyManagerUiContextForStep(OnboardingStep step)
        {
            string moduleId = ResolveManagerModuleForStep(step);
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return;
            }

            HoyoToonManagerWindow window;
            if (!HoyoToonManagerWindow.TryGetOpenWindow(out window) || window == null)
            {
                window = HoyoToonManagerWindow.ShowWindowForOnboarding();
            }

            window.SelectModuleForOnboarding(moduleId);
            QueueManagerUiContextRefresh(step.Id, moduleId);
        }

        private static void QueueManagerUiContextRefresh(string stepId, string moduleId)
        {
            EditorApplication.delayCall += () =>
            {
                OnboardingStep currentStep = CurrentStep;
                if (!isRunning
                    || currentStep == null
                    || !string.Equals(currentStep.Id, stepId, StringComparison.Ordinal))
                {
                    return;
                }

                HoyoToonManagerWindow window;
                if (HoyoToonManagerWindow.TryGetOpenWindow(out window) && window != null)
                {
                    window.SelectModuleForOnboarding(moduleId);
                }

                ApplyVisualState();
                RefreshMissingTargets();
                FocusCurrentTargets(force: true);
                RefreshMissingTargets();
                ShowDialogForCurrentStep();
            };
        }

        private static string ResolveManagerModuleForStep(OnboardingStep step)
        {
            if (step == null)
            {
                return string.Empty;
            }

            switch (step.Id)
            {
                case "Assets.OpenModule":
                    return "setup";
                case "Setup.OpenModule":
                    return "assets";
                case "Character.OpenTab":
                    return "setup";
                case "Scene.OpenTab":
                    return "character";
                case "Render.OpenTab":
                    return "scene";
            }

            foreach (string targetId in step.HighlightTargets.Concat(step.AllowedTargets))
            {
                string moduleId = ResolveManagerModuleForTarget(targetId);
                if (!string.IsNullOrWhiteSpace(moduleId))
                {
                    return moduleId;
                }
            }

            return ResolveManagerModuleForTarget(step.Id);
        }

        private static string ResolveManagerModuleForTarget(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return string.Empty;
            }

            if (targetId.StartsWith("Assets.", StringComparison.Ordinal)
                || string.Equals(targetId, "Manager.Nav.AssetsTab", StringComparison.Ordinal))
            {
                return "assets";
            }

            if (targetId.StartsWith("Setup.", StringComparison.Ordinal)
                || string.Equals(targetId, "Manager.Nav.SetupTab", StringComparison.Ordinal))
            {
                return "setup";
            }

            if (targetId.StartsWith("Character.", StringComparison.Ordinal)
                || string.Equals(targetId, "Manager.Nav.CharacterTab", StringComparison.Ordinal))
            {
                return "character";
            }

            if (targetId.StartsWith("Scene.", StringComparison.Ordinal)
                || string.Equals(targetId, "Manager.Nav.SceneTab", StringComparison.Ordinal))
            {
                return "scene";
            }

            if (targetId.StartsWith("Render.", StringComparison.Ordinal)
                || string.Equals(targetId, "Manager.Nav.RenderTab", StringComparison.Ordinal))
            {
                return "renders";
            }

            return string.Empty;
        }

        private static void Update()
        {
            if (!isRunning)
            {
                return;
            }

            ApplyVisualState();
            RefreshMissingTargets();
            FocusCurrentTargets(force: false);
            RefreshMissingTargets();

            OnboardingStep step = CurrentStep;
            if (step == null)
            {
                StopOnboarding(markComplete: true);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - lastDialogRefreshTime > 0.25d)
            {
                ShowDialogForCurrentStep();
            }

            DialogController.EnsureVisible();

            if (IsStepFailed(step))
            {
                return;
            }

            if ((!step.ShowContinueButton || step.AutoAdvanceWhenComplete)
                && step.EvaluateCompletion()
                && !IsCurrentStepHeldForDebug(step))
            {
                Advance();
            }
        }

        private static void ApplyVisualState()
        {
            if (overlay != null)
            {
                overlay.DimmingEnabled = OverlayDimmingEnabled;
                overlay.DrawAllTargets = DrawAllTargetRects;
                overlay.SetStep(isRunning ? CurrentStep : null);
            }

            if (inputBlocker != null)
            {
                inputBlocker.BlockingEnabled = InputBlockingEnabled;
                inputBlocker.SetStep(isRunning ? CurrentStep : null);
            }
        }

        private static void FocusCurrentTargets(bool force)
        {
            OnboardingStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            if (!force && step.HighlightTargets.Any(OnboardingTargetRegistry.IsTargetVisible))
            {
                return;
            }

            if (!force && IsFocusInsideAllowedTargets(step))
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!force && now - lastFocusTime < 1.0d)
            {
                return;
            }

            lastFocusTime = now;
            OnboardingFocusController.FocusStepTargets(step);
        }

        private static bool IsFocusInsideAllowedTargets(OnboardingStep step)
        {
            if (step == null || managerRoot == null || managerRoot.panel == null)
            {
                return false;
            }

            FocusController focusController = managerRoot.panel.focusController;
            VisualElement focusedElement = focusController != null ? focusController.focusedElement as VisualElement : null;
            return OnboardingTargetRegistry.IsFocusedElementInsideTargets(focusedElement, step.AllowedTargets);
        }

        private static void RefreshMissingTargets()
        {
            OnboardingStep step = CurrentStep;
            if (step == null)
            {
                missingTargets = Array.Empty<string>();
                return;
            }

            var missing = new List<string>();
            string[] managerAllowedTargets = step.AllowedTargets
                .Where(IsManagerTargetId)
                .ToArray();
            string[] managerHighlightTargets = step.HighlightTargets
                .Where(IsManagerTargetId)
                .ToArray();

            missing.AddRange(OnboardingTargetRegistry.GetMissingTargets(managerAllowedTargets));
            if (managerHighlightTargets.Length > 0
                && !managerHighlightTargets.Any(OnboardingTargetRegistry.IsTargetAvailable))
            {
                missing.AddRange(OnboardingTargetRegistry.GetMissingTargets(managerHighlightTargets));
            }

            missingTargets = missing
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsManagerTargetId(string targetId)
        {
            return !string.IsNullOrWhiteSpace(targetId)
                && !targetId.StartsWith("Dialog.", StringComparison.Ordinal);
        }

        private static void HandleTargetsChanged()
        {
            if (!isRunning || targetRefreshQueued)
            {
                return;
            }

            targetRefreshQueued = true;
            EditorApplication.delayCall += RefreshAfterTargetsChanged;
        }

        private static void RefreshAfterTargetsChanged()
        {
            targetRefreshQueued = false;
            if (!isRunning)
            {
                return;
            }

            RefreshMissingTargets();
            ApplyVisualState();
            ShowDialogForCurrentStep();
        }

        private static bool CanContinue(OnboardingStep step)
        {
            if (step == null)
            {
                return false;
            }

            if (!step.RequireCompletionBeforeContinue)
            {
                return true;
            }

            return !IsStepFailed(step) && step.EvaluateCompletion();
        }

        private static void HoldCurrentStepForDebug()
        {
            OnboardingStep step = CurrentStep;
            debugHeldStepId = step != null ? step.Id : string.Empty;
            debugHoldUntilTime = EditorApplication.timeSinceStartup + 0.75d;
        }

        private static bool IsCurrentStepHeldForDebug(OnboardingStep step)
        {
            if (step == null
                || string.IsNullOrWhiteSpace(debugHeldStepId)
                || !string.Equals(debugHeldStepId, step.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (EditorApplication.timeSinceStartup < debugHoldUntilTime)
            {
                return true;
            }

            debugHeldStepId = string.Empty;
            debugHoldUntilTime = 0d;
            return false;
        }

        private static bool IsStepFailed(OnboardingStep step)
        {
            if (forcedFailure)
            {
                return true;
            }

            if (step == null)
            {
                return false;
            }

            if (step.EvaluateFailure())
            {
                return true;
            }

            OnboardingAsyncStatus status = step.GetAsyncStatus != null ? step.GetAsyncStatus() : null;
            return status != null && status.HasFailed;
        }

        private static void ShowDialogForCurrentStep()
        {
            OnboardingStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            RefreshMissingTargets();
            OnboardingAsyncStatus status = step.GetAsyncStatus != null ? step.GetAsyncStatus() : OnboardingAsyncStatus.Idle();
            string error = BuildErrorText(step, status);
            bool canContinue = CanContinue(step);
            Rect anchor = managerWindow != null ? managerWindow.position : Rect.zero;
            DialogController.ShowStep(step, currentStepIndex, steps.Count, status, error, canContinue, anchor);
            lastDialogRefreshTime = EditorApplication.timeSinceStartup;
        }

        private static string BuildErrorText(OnboardingStep step, OnboardingAsyncStatus status)
        {
            if (forcedFailure)
            {
                return forcedFailureMessage;
            }

            if (status != null && status.HasFailed)
            {
                return status.ErrorMessage;
            }

            if (missingTargets != null && missingTargets.Count > 0)
            {
                if (isDebugRun || CanUseDebugTools())
                {
                    return "Missing onboarding target(s) for step '" + step.Id + "': " + string.Join(", ", missingTargets);
                }

                bool anyHighlightedTargetAvailable = step.HighlightTargets.Any(OnboardingTargetRegistry.IsTargetAvailable);
                if (!anyHighlightedTargetAvailable)
                {
                    return "The tutorial could not find the expected HoyoToon UI. Please reopen the HoyoToon Manager and try again.";
                }
            }

            return string.Empty;
        }

        private static void SubscribeUpdate()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        private static void UnsubscribeUpdate()
        {
            EditorApplication.update -= Update;
        }
    }
}
#endif
