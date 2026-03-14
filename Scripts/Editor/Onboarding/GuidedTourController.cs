#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoyoToon.Editor.Utilities;
using HoyoToon;

using HoyoToon.Editor.UI.Windows;

using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.Prerequisites;

namespace HoyoToon.Editor.Onboarding
{
    public static partial class GuidedTourController
    {
        private const string TourActiveKey = "HoyoToon.Tour.Active";
        private const string TourStepKey = "HoyoToon.Tour.Step";
        private const string DownloadedKey = "HoyoToon.Tour.Downloaded";
        private const string AutoSetupKey = "HoyoToon.Tour.AutoSetup";
        private const string SelectedModuleKey = "HoyoToon.Tour.SelectedModule";
        private const string SelectedGameKey = "HoyoToon.Tour.SelectedGame";
        private const string SelectedCharacterKey = "HoyoToon.Tour.SelectedCharacter";
        private const string SelectedVariantKey = "HoyoToon.Tour.SelectedVariant";
        private const string SelectedFbxChoiceKey = "HoyoToon.Tour.SelectedFbxChoice";
        private const string SelectedModelKey = "HoyoToon.Tour.SelectedModel";
        private const string PingedFbxKey = "HoyoToon.Tour.PingedFbx";
        private const string ManagerHandoffKey = "HoyoToon.Tour.ManagerHandoff";
        private const string InspectorLockKey = "HoyoToon.Tour.InspectorLock";
        private const string ModelApprovedKey = "HoyoToon.Tour.ModelApproved";
        private const string LightingLightTypeKey = "HoyoToon.Tour.LightingLightType";
        private const string LightingLightAddedKey = "HoyoToon.Tour.LightingLightAdded";
        private const string LightingLightRemovedKey = "HoyoToon.Tour.LightingLightRemoved";
        private const string LightingRotationKey = "HoyoToon.Tour.LightingRotation";
        private const string LightingAutoRotateSeenKey = "HoyoToon.Tour.LightingAutoRotateSeen";
        private const string LightingAutoRotateCycleKey = "HoyoToon.Tour.LightingAutoRotateCycle";
        private const string ScriptablesShadowBoostKey = "HoyoToon.Tour.ScriptablesShadowBoost";
        private const string ScriptablesLevelAdjustKey = "HoyoToon.Tour.ScriptablesLevelAdjust";
        private const string ScriptablesResetKey = "HoyoToon.Tour.ScriptablesReset";
        private const string PostProcessingProfileKey = "HoyoToon.Tour.PostProcessingProfile";
        private const string RendersCameraKey = "HoyoToon.Tour.RendersCamera";
        private const string RendersTransparentKey = "HoyoToon.Tour.RendersTransparent";
        private const string RendersSyncKey = "HoyoToon.Tour.RendersSync";
        private const string RendersWatermarkKey = "HoyoToon.Tour.RendersWatermark";
        private const string FirstTimeConfirmedKey = "HoyoToon.Tour.FirstTimeConfirmed";
        private const string FooterExplainedKey = "HoyoToon.Tour.FooterExplained";
        private const string PrefabCreatedKey = "HoyoToon.Tour.PrefabCreated";

        public const string HoyoToonScenePath = "Packages/com.hoyotoon.hoyotoon/HoyoToon.unity";
        public const string DesiredGameName = GameConstants.HonkaiStarRail;
        public const string DesiredCharacterName = "Acheron";
        public const string DesiredVariantName = "Default";
        public const string DesiredFbxChoiceName = "No Anims";
        public const string DesiredFbxAssetName = "Art_Acheron_01";

        private static HoyoToonManager s_lastManager;
        private static GameObject s_lastModel;
        private static readonly string[] s_windowSteps = { StepIds.FirstTime, StepIds.Prerequisites, StepIds.Resources, StepIds.Scene, StepIds.Manager };
        private static readonly string[] s_tourBoolResetKeys =
        {
            DownloadedKey,
            AutoSetupKey,
            SelectedModelKey,
            PingedFbxKey,
            ManagerHandoffKey,
            ModelApprovedKey,
            LightingLightTypeKey,
            LightingLightAddedKey,
            LightingLightRemovedKey,
            LightingRotationKey,
            LightingAutoRotateSeenKey,
            LightingAutoRotateCycleKey,
            ScriptablesShadowBoostKey,
            ScriptablesLevelAdjustKey,
            ScriptablesResetKey,
            PostProcessingProfileKey,
            RendersCameraKey,
            RendersTransparentKey,
            RendersSyncKey,
            RendersWatermarkKey,
            FirstTimeConfirmedKey,
            FooterExplainedKey,
            PrefabCreatedKey
        };
        private static string s_lastStepId;

        public static event Action OnStepChanged;

        public static bool IsActive => SessionState.GetBool(TourActiveKey, false);

        public static int CurrentStepIndex
        {
            get => Mathf.Clamp(SessionState.GetInt(TourStepKey, 0), 0, s_steps.Length - 1);
            private set => SessionState.SetInt(TourStepKey, Mathf.Clamp(value, 0, s_steps.Length - 1));
        }

        public static TourStep CurrentStep => s_steps[CurrentStepIndex];
        public static int StepCount => s_steps.Length;

        public static void StartTour()
        {
            s_lastManager = null;
            s_lastModel = null;
            s_lastStepId = null;
            SessionState.SetBool(TourActiveKey, true);
            foreach (string key in s_tourBoolResetKeys)
            {
                SessionState.SetBool(key, false);
            }
            SessionState.SetString(SelectedModuleKey, string.Empty);
            SessionState.SetString(SelectedGameKey, string.Empty);
            SessionState.SetString(SelectedCharacterKey, string.Empty);
            SessionState.SetString(SelectedVariantKey, string.Empty);
            SessionState.SetString(SelectedFbxChoiceKey, string.Empty);
            CacheInspectorLockState();
            SetInspectorLocked(true);
            CurrentStepIndex = 0;
            LogEarlyStepDiagnostic($"event=tour-start source={GetStartupSourceOrManual()} step={CurrentStep.id} index={CurrentStepIndex}");
            NotifyStepChanged();
        }

        public static void StartTourAtStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
            {
                StartTour();
                return;
            }

            StartTour();
            int index = GetStepIndex(stepId);
            CurrentStepIndex = Mathf.Clamp(index, 0, s_steps.Length - 1);
            LogEarlyStepDiagnostic($"event=tour-start-at-step source={GetStartupSourceOrManual()} step={CurrentStep.id} index={CurrentStepIndex}");
            NotifyStepChanged();
        }

        public static void StopTour()
        {
            LogEarlyStepDiagnostic($"event=tour-stop step={CurrentStep.id} index={CurrentStepIndex}");
            SessionState.SetBool(TourActiveKey, false);
            OnboardingStartupPolicy.ClearStartupSourceForSession();
            RestoreInspectorLockState();
            MarkTourSeen();
            NotifyStepChanged();
            s_lastStepId = null;
        }

        public static void RestartTour()
        {
            StartTour();
        }

        private static void CacheInspectorLockState()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var tracker = ActiveEditorTracker.sharedTracker;
            if (tracker != null)
            {
                SessionState.SetBool(InspectorLockKey, tracker.isLocked);
            }
        }

        private static void SetInspectorLocked(bool locked)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                var tracker = ActiveEditorTracker.sharedTracker;
                if (tracker != null)
                {
                    tracker.isLocked = locked;
                    tracker.ForceRebuild();
                }
            };
        }

        private static void RestoreInspectorLockState()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            bool wasLocked = SessionState.GetBool(InspectorLockKey, false);
            SetInspectorLocked(wasLocked);
        }

        public static bool CanAdvance()
        {
            return CurrentStep.isComplete?.Invoke() ?? true;
        }

        public static void Advance()
        {
            if (!CanAdvance())
            {
                return;
            }

            if (CurrentStepIndex < s_steps.Length - 1)
            {
                CurrentStepIndex++;
                NotifyStepChanged();
            }
        }

        public static void JumpToStep(string stepId)
        {
            if (!IsActive || string.IsNullOrEmpty(stepId))
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < s_steps.Length; i++)
            {
                if (string.Equals(s_steps[i].id, stepId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || index == CurrentStepIndex)
            {
                return;
            }

            CurrentStepIndex = index;
            NotifyStepChanged();
        }

        public static void GoBack()
        {
            if (CurrentStepIndex > 0)
            {
                CurrentStepIndex--;
                NotifyStepChanged();
            }
        }

        public static bool IsTargetActive(string targetId)
        {
            if (!IsActive || string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            return string.Equals(CurrentStep.highlightTarget, targetId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryOpenHoyoToonScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(HoyoToonScenePath))
            {
                DialogWindow.ShowError("Scene Missing", "HoyoToon scene asset was not found in the package.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(HoyoToonScenePath, OpenSceneMode.Single);
            NotifyStepChanged();
            return true;
        }

        public static void NotifyModuleSelected(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return;
            }

            SessionState.SetString(SelectedModuleKey, moduleName);
            NotifyStepChanged();
        }

        public static bool IsSelectedModule(string moduleName)
        {
            return IsSessionMatch(SelectedModuleKey, moduleName);
        }

        public static void CompleteModelStep()
        {
            SessionState.SetBool(ModelApprovedKey, true);
            NotifyStepChanged();
        }


        public static void NotifyLightingLightTypePicked()
        {
            SetBoolAndNotify(LightingLightTypeKey);
        }

        public static void NotifyLightingLightAdded()
        {
            SetBoolAndNotify(LightingLightAddedKey);
        }

        public static void NotifyLightingLightRemoved()
        {
            SetBoolAndNotify(LightingLightRemovedKey);
        }

        public static void NotifyLightingRotationAdjusted()
        {
            SetBoolAndNotify(LightingRotationKey);
        }

        public static void NotifyLightingAutoRotateToggled(bool enabled)
        {
            if (enabled)
            {
                SetBoolAndNotify(LightingAutoRotateSeenKey);
                return;
            }

            if (SessionState.GetBool(LightingAutoRotateSeenKey, false))
            {
                SetBoolAndNotify(LightingAutoRotateCycleKey);
            }
        }

        public static void NotifyScriptablesShadowBoostEnabled()
        {
            SetBoolAndNotify(ScriptablesShadowBoostKey);
        }

        public static void NotifyScriptablesLevelAdjustEnabled()
        {
            SetBoolAndNotify(ScriptablesLevelAdjustKey);
        }

        public static void NotifyScriptablesReset()
        {
            SetBoolAndNotify(ScriptablesResetKey);
        }

        public static void NotifyPostProcessingProfilePicked()
        {
            SetBoolAndNotify(PostProcessingProfileKey);
        }

        public static void NotifyRendersCameraSelected()
        {
            SetBoolAndNotify(RendersCameraKey);
        }

        public static void NotifyRendersTransparentEnabled()
        {
            SetBoolAndNotify(RendersTransparentKey);
        }

        public static void NotifyRendersSyncEnabled()
        {
            SetBoolAndNotify(RendersSyncKey);
        }

        public static void NotifyRendersWatermarkEnabled()
        {
            SetBoolAndNotify(RendersWatermarkKey);
        }

        public static void CompleteFooterStep()
        {
            SessionState.SetBool(FooterExplainedKey, true);
            NotifyStepChanged();
        }

        public static void NotifyPrefabCreated()
        {
            SetBoolAndNotify(PrefabCreatedKey);
        }

        public static void ConfirmFirstTime(bool isFirstTime)
        {
            if (!isFirstTime)
            {
                PrefsKeys.SetTourShown(true);
                StopTour();
                return;
            }

            SessionState.SetBool(FirstTimeConfirmedKey, true);

            if (string.Equals(CurrentStep.id, StepIds.FirstTime, StringComparison.OrdinalIgnoreCase))
            {
                CurrentStepIndex = GetStepIndex(StepIds.Prerequisites);
                NotifyStepChanged();
                return;
            }

            Advance();
        }

        public static bool ArePrerequisitesSatisfied()
        {
            return PrerequisitesRunner.EvaluateAggregateStatus(attemptAutoFix: false).AllPassed;
        }

        public static PrerequisitesAggregateStatus GetPrerequisitesStatus()
        {
            return PrerequisitesRunner.EvaluateAggregateStatus(attemptAutoFix: false);
        }

        public static bool RunPrerequisitesAutoFix()
        {
            var status = PrerequisitesRunner.EvaluateAggregateStatus(attemptAutoFix: true);
            NotifyStepChanged();
            return status.AllPassed;
        }

        public static bool HasSeenTour()
        {
            return PrefsKeys.GetTourShown();
        }

        public static void MarkTourSeen()
        {
            PrefsKeys.SetTourShown(true);
        }

        public static bool TryAcquireStartupEntryForSession(string source)
        {
            return OnboardingStartupPolicy.TryAcquireStartupEntryForSession(source);
        }

        public static void NotifyGameSelected(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
            {
                return;
            }

            SessionState.SetString(SelectedGameKey, gameName);
            NotifyStepChanged();
        }

        public static void NotifyCharacterSelected(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                return;
            }

            SessionState.SetString(SelectedCharacterKey, characterName);
            NotifyStepChanged();
        }

        public static void NotifyVariantSelected(string variantName)
        {
            if (string.IsNullOrEmpty(variantName))
            {
                return;
            }

            SessionState.SetString(SelectedVariantKey, variantName);
            NotifyStepChanged();
        }

        public static void NotifyFbxChoiceSelected(string choice)
        {
            if (string.IsNullOrEmpty(choice))
            {
                return;
            }

            SessionState.SetString(SelectedFbxChoiceKey, choice);
            NotifyStepChanged();
        }

        public static void NotifyPendingModelSelected()
        {
            SetBoolAndNotify(SelectedModelKey);
        }

        public static void NotifyModelDownloaded()
        {
            SessionState.SetBool(DownloadedKey, true);
            SessionState.SetBool(SelectedModelKey, false);
            NotifyStepChanged();
        }

        public static void NotifyAutoSetupCompleted(GameObject instance)
        {
            if (instance != null)
            {
                s_lastModel = instance;
            }

            SessionState.SetBool(AutoSetupKey, true);
            SessionState.SetBool(ModelApprovedKey, true);
            NotifyStepChanged();
        }

        public static bool ShouldLockGameSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex(StepIds.Game);
        }

        public static bool ShouldLockCharacterSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex(StepIds.Character);
        }

        public static bool ShouldLockVariantSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex(StepIds.Variant);
        }

        public static bool ShouldLockFbxSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex(StepIds.Fbx);
        }

        public static bool IsSelectedGame(string gameName)
        {
            return IsSessionMatch(SelectedGameKey, gameName);
        }

        public static bool IsSelectedCharacter(string characterName)
        {
            return IsSessionMatch(SelectedCharacterKey, characterName);
        }

        public static bool IsSelectedVariant(string variantName)
        {
            return IsSessionMatch(SelectedVariantKey, variantName);
        }

        public static bool IsSelectedFbxChoice(string choice)
        {
            return IsSessionMatch(SelectedFbxChoiceKey, choice);
        }

        public static void NotifyManagerSeen(HoyoToonManager manager)
        {
            if (manager == null)
            {
                return;
            }

            bool managerChanged = !ReferenceEquals(s_lastManager, manager);
            if (managerChanged)
            {
                s_lastManager = manager;
                NotifyStepChanged();
            }
        }

        public static void FocusManagerInScene()
        {
            var manager = s_lastManager != null ? s_lastManager : FindManagerInScene();
            if (manager == null)
            {
                return;
            }

            s_lastManager = manager;
            RevealObjectInInspector(manager.gameObject);
            FrameSelection();
            CompleteManagerHandoff();
        }

        public static bool HasManagerInScene()
        {
            return FindManagerInScene() != null;
        }

        public static void CreateAndFocusManagerInScene()
        {
            var go = new GameObject("HoyoToon Manager");
            var manager = go.AddComponent<HoyoToonManager>();
            s_lastManager = manager;
            RevealObjectInInspector(go);
            NotifyManagerSeen(manager);
            FrameSelection();
            CompleteManagerHandoff();
        }

        public static void FocusModelInScene()
        {
            if (s_lastModel == null)
            {
                return;
            }

            Selection.activeObject = s_lastModel;
            EditorGUIUtility.PingObject(s_lastModel);
            FrameSelection();
        }

        private static void FrameSelection()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected();
            }
        }

        private static void RevealObjectInInspector(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            bool wasLocked = false;
            var tracker = ActiveEditorTracker.sharedTracker;
            if (tracker != null)
            {
                wasLocked = tracker.isLocked;
                if (wasLocked)
                {
                    tracker.isLocked = false;
                    tracker.ForceRebuild();
                }
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            if (!wasLocked)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (!IsActive)
                {
                    return;
                }

                var delayedTracker = ActiveEditorTracker.sharedTracker;
                if (delayedTracker == null)
                {
                    return;
                }

                delayedTracker.isLocked = true;
                delayedTracker.ForceRebuild();
            };
        }

        private static bool IsSceneReady()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            return string.Equals(scene.path, HoyoToonScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static void SetBoolAndNotify(string key)
        {
            SessionState.SetBool(key, true);
            NotifyStepChanged();
        }

        private static bool IsSessionMatch(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return string.Equals(SessionState.GetString(key, string.Empty), value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModelInScene()
        {
            if (s_lastModel != null)
            {
                return true;
            }

            var manager = FindManagerInScene();
            if (manager == null)
            {
                return false;
            }

            return manager.ActiveModel != null;
        }

        private static bool IsHoyoToonSceneActive()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            return string.Equals(scene.path, HoyoToonScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static HoyoToonManager FindManagerInScene()
        {
            // Use cached reference when still alive to avoid costly FindObjectOfType in hot paths
            if (s_lastManager != null)
                return s_lastManager;
            s_lastManager = UnityEngine.Object.FindFirstObjectByType<HoyoToonManager>(FindObjectsInactive.Exclude);
            return s_lastManager;
        }

        private static void NotifyStepChanged()
        {
            string previousStepId = s_lastStepId;
            int previousStepIndex = GetStepIndexOrMinusOne(previousStepId);
            try
            {
                OnStepChanged?.Invoke();
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Tour", ex.ToString(), LogType.Exception);
            }

            if (!IsActive)
            {
                GuidedTourWindow.EnsureWindowVisible(false);
                return;
            }

            GuidedTourWindow.EnsureWindowVisible(IsWindowStep(CurrentStep.id));
            if (IsWindowStep(previousStepId) && !IsWindowStep(CurrentStep.id))
            {
                FocusManagerInScene();
            }

            if (IsEarlyWindowStep(previousStepId) || IsEarlyWindowStep(CurrentStep.id))
            {
                bool canAdvance = CurrentStep.isComplete?.Invoke() ?? true;
                LogEarlyStepDiagnostic(
                    $"event=step-transition fromStep={SafeStepId(previousStepId)} fromIndex={previousStepIndex} toStep={CurrentStep.id} toIndex={CurrentStepIndex} canAdvance={canAdvance}");
            }

            s_lastStepId = CurrentStep.id;
            AutoAdvanceIfComplete();
        }

        private static bool IsWindowStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
            {
                return false;
            }

            foreach (var id in s_windowSteps)
            {
                if (string.Equals(stepId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldShowManagerHandoffInline()
        {
            return IsActive && string.Equals(CurrentStep.id, StepIds.Game, StringComparison.OrdinalIgnoreCase)
                && !SessionState.GetBool(ManagerHandoffKey, false);
        }

        public static bool HasManagerHandoffSignal()
        {
            return SessionState.GetBool(ManagerHandoffKey, false);
        }

        public static void MarkManagerHandoffShown()
        {
            CompleteManagerHandoff();
        }

        private static void CompleteManagerHandoff()
        {
            if (SessionState.GetBool(ManagerHandoffKey, false))
            {
                return;
            }

            SessionState.SetBool(ManagerHandoffKey, true);
            NotifyStepChanged();
        }

        private static bool HasMissingResources()
        {
            var status = ResourceManager.GetResourceStatus();
            return status != null && status.Any(kvp => !kvp.Value.HasResources);
        }


        private static bool s_autoAdvancing;

        private static void AutoAdvanceIfComplete()
        {
            if (!IsActive || s_autoAdvancing)
            {
                return;
            }

            int startIndex = CurrentStepIndex;
            string startStep = CurrentStep.id;
            bool shouldLog = IsEarlyWindowStep(startStep);
            try
            {
                s_autoAdvancing = true;
                bool initialCanAdvance = CanAdvance();
                if (shouldLog)
                {
                    LogEarlyStepDiagnostic($"event=auto-advance-check step={startStep} index={startIndex} canAdvance={initialCanAdvance}");
                }

                while (CurrentStepIndex < s_steps.Length - 1 && CanAdvance())
                {
                    Advance();
                }

                int endIndex = CurrentStepIndex;
                string endStep = CurrentStep.id;
                if (shouldLog || IsEarlyWindowStep(endStep))
                {
                    int advancedBy = endIndex - startIndex;
                    bool blocked = endIndex < s_steps.Length - 1 && !CanAdvance();
                    LogEarlyStepDiagnostic(
                        $"event=auto-advance-result startStep={startStep} startIndex={startIndex} endStep={endStep} endIndex={endIndex} advancedBy={advancedBy} blocked={blocked}");
                }
            }
            finally
            {
                s_autoAdvancing = false;
            }
        }

        private static void LogEarlyStepDiagnostic(string message)
        {
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Tour, LogLevel.Info, $"OnboardingDiag {message}");
        }

        private static bool IsEarlyWindowStep(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
            {
                return false;
            }

            return string.Equals(stepId, StepIds.FirstTime, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, StepIds.Prerequisites, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, StepIds.Resources, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, StepIds.Scene, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, StepIds.Manager, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetStepIndexOrMinusOne(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (int i = 0; i < s_steps.Length; i++)
            {
                if (string.Equals(s_steps[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string SafeStepId(string stepId)
        {
            return string.IsNullOrEmpty(stepId) ? "None" : stepId;
        }

        private static string GetStartupSourceOrManual()
        {
            return OnboardingStartupPolicy.GetStartupSourceOrManual();
        }


        private static int GetStepIndex(string id)
        {
            for (int i = 0; i < s_steps.Length; i++)
            {
                if (string.Equals(s_steps[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
#endif
