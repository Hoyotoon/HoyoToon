#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoyoToon.Utilities;
using HoyoToon;

namespace HoyoToon.EditorTools.Onboarding
{
    internal static class HoyoToonGuidedTourController
    {
        private const string TourActiveKey = "HoyoToon_Tour_Active";
        private const string TourStepKey = "HoyoToon_Tour_Step";
        private const string SceneApprovedKey = "HoyoToon_Tour_SceneApproved";
        private const string DownloadedKey = "HoyoToon_Tour_Downloaded";
        private const string AutoSetupKey = "HoyoToon_Tour_AutoSetup";
        private const string SelectedModuleKey = "HoyoToon_Tour_SelectedModule";
        private const string SelectedGameKey = "HoyoToon_Tour_SelectedGame";
        private const string SelectedCharacterKey = "HoyoToon_Tour_SelectedCharacter";
        private const string SelectedVariantKey = "HoyoToon_Tour_SelectedVariant";
        private const string SelectedFbxChoiceKey = "HoyoToon_Tour_SelectedFbxChoice";
        private const string SelectedModelKey = "HoyoToon_Tour_SelectedModel";
        private const string PingedFbxKey = "HoyoToon_Tour_PingedFbx";
        private const string ManagerHandoffKey = "HoyoToon_Tour_ManagerHandoff";
        private const string InspectorLockKey = "HoyoToon_Tour_InspectorLock";
        private const string ForceWindowKey = "HoyoToon_Tour_ForceWindow";
        private const string CloseOnManagerSelectKey = "HoyoToon_Tour_CloseOnManagerSelect";
        private const string ModelApprovedKey = "HoyoToon_Tour_ModelApproved";
        private const string LightingExplainedKey = "HoyoToon_Tour_LightingExplained";
        private const string ScriptablesExplainedKey = "HoyoToon_Tour_ScriptablesExplained";
        private const string PostProcessingExplainedKey = "HoyoToon_Tour_PostProcessingExplained";
        private const string RendersExplainedKey = "HoyoToon_Tour_RendersExplained";

        public const string HoyoToonScenePath = "Packages/com.meliverse.hoyotoon/HoyoToon.unity";
        public const string DesiredGameName = "Honkai Star Rail";
        public const string DesiredCharacterName = "Acheron";
        public const string DesiredVariantName = "Default";
        public const string DesiredFbxChoiceName = "No Anims";
        public const string DesiredFbxAssetName = "Art_Acheron_01";

        private static HoyoToonManager s_lastManager;
        private static GameObject s_lastModel;

        private static readonly TourStep[] s_steps =
        {
            new TourStep(
                id: "resources",
                title: "Check Resources",
                instruction: "Download any missing resources so materials and lighting look correct.",
                highlightTarget: null,
                isComplete: () => !HasMissingResources()),
            new TourStep(
                id: "scene",
                title: "Open HoyoToon Scene",
                instruction: "Open the HoyoToon scene (recommended) or approve your current scene.",
                highlightTarget: null,
                isComplete: () => IsSceneReady()),
            new TourStep(
                id: "manager",
                title: "Ensure Manager",
                instruction: "Select the HoyoToon Manager so we can drive all modules.",
                highlightTarget: "tour.manager.create",
                isComplete: () => IsHoyoToonSceneActive() || FindManagerInScene() != null),
            new TourStep(
                id: "modules",
                title: "Open Models Module",
                instruction: "Go to Models to download Acheron and choose the correct files.",
                highlightTarget: "tour.modules.models",
                isComplete: () => string.Equals(SessionState.GetString(SelectedModuleKey, string.Empty), "Models", StringComparison.OrdinalIgnoreCase)),
            new TourStep(
                id: "game",
                title: "Choose Game",
                instruction: "Select Honkai Star Rail so we pull the right resources.",
                highlightTarget: "tour.models.game",
                isComplete: () => IsSelectedGame(DesiredGameName)),
            new TourStep(
                id: "character",
                title: "Choose Character",
                instruction: "Pick Acheron so the download matches this walkthrough.",
                highlightTarget: "tour.models.character",
                isComplete: () => IsSelectedCharacter(DesiredCharacterName)),
            new TourStep(
                id: "variant",
                title: "Choose Variant",
                instruction: "Use the Default variant so the file names match the steps.",
                highlightTarget: "tour.models.variant",
                isComplete: () => IsSelectedVariant(DesiredVariantName)),
            new TourStep(
                id: "fbx",
                title: "Choose FBX",
                instruction: "Select No Anims to keep the setup lighter for this guide.",
                highlightTarget: "tour.models.fbx.noanims",
                isComplete: () => IsSelectedFbxChoice(DesiredFbxChoiceName)),
            new TourStep(
                id: "download",
                title: "Download Your First Model",
                instruction: "Click Download Selected Models and wait for the import to finish.",
                highlightTarget: "tour.models.download",
                isComplete: () => SessionState.GetBool(DownloadedKey, false)),
            new TourStep(
                id: "mainmodule",
                title: "Open Main Module",
                instruction: "Switch to Main so we can add the downloaded FBX.",
                highlightTarget: "tour.modules.main",
                isComplete: () => string.Equals(SessionState.GetString(SelectedModuleKey, string.Empty), "Main", StringComparison.OrdinalIgnoreCase)),
            new TourStep(
                id: "addmodel",
                title: "Select Downloaded FBX",
                instruction: "In Add Model, choose the downloaded FBX named Art_Acheron_01.",
                highlightTarget: "tour.manager.addmodel",
                isComplete: () => SessionState.GetBool(SelectedModelKey, false)),
            new TourStep(
                id: "autosetup",
                title: "Run Auto Setup",
                instruction: "Press Auto Setup to generate materials and add the model to the scene.",
                highlightTarget: "tour.manager.autosetup",
                isComplete: () => SessionState.GetBool(AutoSetupKey, false)),
            new TourStep(
                id: "model",
                title: "Model In Scene",
                instruction: "Confirm the model appears in the scene and looks correct.",
                highlightTarget: null,
                isComplete: () => IsModelInScene() && SessionState.GetBool(ModelApprovedKey, false)),
            new TourStep(
                id: "lighting",
                title: "Lighting Module",
                instruction: "Adjust the key light direction and intensity for a clear silhouette.",
                highlightTarget: "tour.modules.lighting",
                isComplete: () => IsSelectedModule("Lighting") && SessionState.GetBool(LightingExplainedKey, false)),
            new TourStep(
                id: "scriptables",
                title: "Scriptables Module",
                instruction: "Ensure Scriptables Controller exists and review game lighting settings.",
                highlightTarget: "tour.modules.scriptables",
                isComplete: () => IsSelectedModule("Scriptables") && SessionState.GetBool(ScriptablesExplainedKey, false)),
            new TourStep(
                id: "postprocessing",
                title: "Post Processing Module",
                instruction: "Pick a post-processing profile that matches the game style.",
                highlightTarget: "tour.modules.postprocessing",
                isComplete: () => IsSelectedModule("Post Processing") && SessionState.GetBool(PostProcessingExplainedKey, false)),
            new TourStep(
                id: "renders",
                title: "Renders Module",
                instruction: "Capture a quick render to verify the final look.",
                highlightTarget: "tour.modules.renders",
                isComplete: () => IsSelectedModule("Renders") && SessionState.GetBool(RendersExplainedKey, false)),
            new TourStep(
                id: "finish",
                title: "Finish",
                instruction: "Tour complete. You now know the core setup, lighting, and render flow.",
                highlightTarget: null,
                isComplete: () => true)
        };

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
            SessionState.SetBool(TourActiveKey, true);
            SessionState.SetBool(SceneApprovedKey, false);
            SessionState.SetBool(DownloadedKey, false);
            SessionState.SetBool(AutoSetupKey, false);
            SessionState.SetString(SelectedModuleKey, string.Empty);
            SessionState.SetString(SelectedGameKey, string.Empty);
            SessionState.SetString(SelectedCharacterKey, string.Empty);
            SessionState.SetString(SelectedVariantKey, string.Empty);
            SessionState.SetString(SelectedFbxChoiceKey, string.Empty);
            SessionState.SetBool(SelectedModelKey, false);
            SessionState.SetBool(PingedFbxKey, false);
            SessionState.SetBool(ManagerHandoffKey, false);
            SessionState.SetBool(ForceWindowKey, true);
            SessionState.SetBool(CloseOnManagerSelectKey, false);
            SessionState.SetBool(ModelApprovedKey, false);
            SessionState.SetBool(LightingExplainedKey, false);
            SessionState.SetBool(ScriptablesExplainedKey, false);
            SessionState.SetBool(PostProcessingExplainedKey, false);
            SessionState.SetBool(RendersExplainedKey, false);
            CacheInspectorLockState();
            SetInspectorLocked(true);
            CurrentStepIndex = 0;
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
            SessionState.SetBool(ForceWindowKey, CurrentStepIndex <= 2);
            NotifyStepChanged();
        }

        public static void StopTour()
        {
            SessionState.SetBool(TourActiveKey, false);
            SessionState.SetBool(ForceWindowKey, false);
            SessionState.SetBool(CloseOnManagerSelectKey, false);
            RestoreInspectorLockState();
            NotifyStepChanged();
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

            var tracker = ActiveEditorTracker.sharedTracker;
            if (tracker != null)
            {
                tracker.isLocked = locked;
                tracker.ForceRebuild();
            }
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

        public static void ApproveCurrentScene()
        {
            SessionState.SetBool(SceneApprovedKey, true);
            NotifyStepChanged();
        }

        public static bool TryOpenHoyoToonScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(HoyoToonScenePath))
            {
                HoyoToonDialogWindow.ShowError("Scene Missing", "HoyoToon scene asset was not found in the package.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(HoyoToonScenePath, OpenSceneMode.Single);
            SessionState.SetBool(SceneApprovedKey, true);
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
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            return string.Equals(SessionState.GetString(SelectedModuleKey, string.Empty), moduleName, StringComparison.OrdinalIgnoreCase);
        }

        public static void CompleteModelStep()
        {
            SessionState.SetBool(ModelApprovedKey, true);
        }

        public static void CompleteLightingStep()
        {
            SessionState.SetBool(LightingExplainedKey, true);
        }

        public static void CompleteScriptablesStep()
        {
            SessionState.SetBool(ScriptablesExplainedKey, true);
        }

        public static void CompletePostProcessingStep()
        {
            SessionState.SetBool(PostProcessingExplainedKey, true);
        }

        public static void CompleteRendersStep()
        {
            SessionState.SetBool(RendersExplainedKey, true);
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
            SessionState.SetBool(SelectedModelKey, true);
            NotifyStepChanged();
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
            NotifyStepChanged();
        }

        public static bool ShouldLockGameSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex("game");
        }

        public static bool ShouldLockCharacterSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex("character");
        }

        public static bool ShouldLockVariantSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex("variant");
        }

        public static bool ShouldLockFbxSelection()
        {
            return IsActive && CurrentStepIndex >= GetStepIndex("fbx");
        }

        public static bool IsSelectedGame(string gameName)
        {
            return string.Equals(SessionState.GetString(SelectedGameKey, string.Empty), gameName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSelectedCharacter(string characterName)
        {
            return string.Equals(SessionState.GetString(SelectedCharacterKey, string.Empty), characterName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSelectedVariant(string variantName)
        {
            return string.Equals(SessionState.GetString(SelectedVariantKey, string.Empty), variantName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSelectedFbxChoice(string choice)
        {
            return string.Equals(SessionState.GetString(SelectedFbxChoiceKey, string.Empty), choice, StringComparison.OrdinalIgnoreCase);
        }

        public static void NotifyManagerSeen(HoyoToonManager manager)
        {
            if (manager != null)
            {
                s_lastManager = manager;
            }

            if (IsActive && CurrentStepIndex >= GetStepIndex("manager")
                && SessionState.GetBool(CloseOnManagerSelectKey, false)
                && !SessionState.GetBool(ForceWindowKey, false))
            {
                HoyoToonGuidedTourWindow.EnsureWindowVisible(false);
                SessionState.SetBool(CloseOnManagerSelectKey, false);
            }
        }

        public static void RequestCloseOnManagerSelect()
        {
            SessionState.SetBool(CloseOnManagerSelectKey, true);
        }

        public static void FocusManagerInScene()
        {
            var manager = s_lastManager != null ? s_lastManager : FindManagerInScene();
            if (manager == null)
            {
                return;
            }

            Selection.activeObject = manager.gameObject;
            EditorGUIUtility.PingObject(manager.gameObject);
            FrameSelection();
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

        private static bool IsSceneReady()
        {
            if (SessionState.GetBool(SceneApprovedKey, false))
            {
                return true;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return false;
            }

            if (string.Equals(scene.path, HoyoToonScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
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
            return UnityEngine.Object.FindObjectOfType<HoyoToonManager>();
        }

        private static void NotifyStepChanged()
        {
            try
            {
                OnStepChanged?.Invoke();
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Tour", ex.ToString(), LogType.Exception);
            }

            bool shouldShowWindow = CurrentStepIndex <= 2 || SessionState.GetBool(ForceWindowKey, false);
            HoyoToonGuidedTourWindow.EnsureWindowVisible(shouldShowWindow);
            if (CurrentStepIndex > 2)
            {
                SessionState.SetBool(ForceWindowKey, false);
            }
            if (string.Equals(CurrentStep.id, "finish", StringComparison.OrdinalIgnoreCase))
            {
                RestoreInspectorLockState();
            }

            AutoAdvanceIfComplete();
        }

        public static bool ShouldShowManagerHandoffInline()
        {
            return IsActive && CurrentStepIndex == 4 && !SessionState.GetBool(ManagerHandoffKey, false);
        }

        public static void MarkManagerHandoffShown()
        {
            SessionState.SetBool(ManagerHandoffKey, true);
        }

        public static bool ShouldForceWindowVisible()
        {
            return IsActive && SessionState.GetBool(ForceWindowKey, false);
        }
        private static bool HasMissingResources()
        {
            var status = HoyoToonResourceManager.GetResourceStatus();
            return status != null && status.Any(kvp => !kvp.Value.HasResources);
        }


        private static bool s_autoAdvancing;

        private static void AutoAdvanceIfComplete()
        {
            if (!IsActive || s_autoAdvancing)
            {
                return;
            }

            try
            {
                s_autoAdvancing = true;
                while (CurrentStepIndex < s_steps.Length - 1 && CanAdvance())
                {
                    Advance();
                }
            }
            finally
            {
                s_autoAdvancing = false;
            }
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

        internal readonly struct TourStep
        {
            public readonly string id;
            public readonly string title;
            public readonly string instruction;
            public readonly string highlightTarget;
            public readonly Func<bool> isComplete;

            public TourStep(string id, string title, string instruction, string highlightTarget, Func<bool> isComplete)
            {
                this.id = id;
                this.title = title;
                this.instruction = instruction;
                this.highlightTarget = highlightTarget;
                this.isComplete = isComplete;
            }
        }
    }
}
#endif
