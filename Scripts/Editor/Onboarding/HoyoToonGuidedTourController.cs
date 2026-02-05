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
        private const string LightingLightTypeKey = "HoyoToon_Tour_LightingLightType";
        private const string LightingLightAddedKey = "HoyoToon_Tour_LightingLightAdded";
        private const string LightingLightRemovedKey = "HoyoToon_Tour_LightingLightRemoved";
        private const string LightingRotationKey = "HoyoToon_Tour_LightingRotation";
        private const string LightingAutoRotateSeenKey = "HoyoToon_Tour_LightingAutoRotateSeen";
        private const string LightingAutoRotateCycleKey = "HoyoToon_Tour_LightingAutoRotateCycle";
        private const string ScriptablesShadowBoostKey = "HoyoToon_Tour_ScriptablesShadowBoost";
        private const string ScriptablesLevelAdjustKey = "HoyoToon_Tour_ScriptablesLevelAdjust";
        private const string ScriptablesResetKey = "HoyoToon_Tour_ScriptablesReset";
        private const string PostProcessingProfileKey = "HoyoToon_Tour_PostProcessingProfile";
        private const string RendersCameraKey = "HoyoToon_Tour_RendersCamera";
        private const string RendersTransparentKey = "HoyoToon_Tour_RendersTransparent";
        private const string RendersSyncKey = "HoyoToon_Tour_RendersSync";
        private const string RendersWatermarkKey = "HoyoToon_Tour_RendersWatermark";
        private const string FirstTimeConfirmedKey = "HoyoToon_Tour_FirstTimeConfirmed";
        private const string FooterExplainedKey = "HoyoToon_Tour_FooterExplained";
        private const string PrefabCreatedKey = "HoyoToon_Tour_PrefabCreated";
        private const string TourShownKey = "HoyoToon_Tour_Shown";

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
                id: "firsttime",
                title: "First Time With HoyoToon?",
                instruction: "Let us know if this is your first time so we can tailor the walkthrough.",
                highlightTarget: null,
                isComplete: () => SessionState.GetBool(FirstTimeConfirmedKey, false)),
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
                instruction: "Click the Main tab to continue.",
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
                id: "lighting_select",
                title: "Open Lighting Module",
                instruction: "Click the Lighting tab to continue.\n\nLighting controls let you shape the key light and shadows on the model.",
                highlightTarget: "tour.modules.lighting",
                isComplete: () => IsSelectedModule("Lighting")),
            new TourStep(
                id: "lighting_lighttype",
                title: "Choose Light Type",
                instruction: "Click the Light Type dropdown and choose a light.\n\nDifferent light types help preview how the model reads.",
                highlightTarget: "tour.lighting.create.dropdown",
                isComplete: () => SessionState.GetBool(LightingLightTypeKey, false)),
            new TourStep(
                id: "lighting_addlight",
                title: "Add Light",
                instruction: "Click Add Light to create the light in the scene.\n\nThis is a preview light and can be removed after.",
                highlightTarget: "tour.lighting.create.add",
                isComplete: () => SessionState.GetBool(LightingLightAddedKey, false)),
            new TourStep(
                id: "lighting_remove",
                title: "Remove Tutorial Light",
                instruction: "Click the highlighted area to remove the tutorial light.\n\nThe base scene lighting stays intact.",
                highlightTarget: "tour.lighting.remove",
                isComplete: () => SessionState.GetBool(LightingLightRemovedKey, false)),
            new TourStep(
                id: "lighting_rotation",
                title: "Rotate Light",
                instruction: "Drag Rotation to aim the key light.\n\nThis changes shadow direction and highlights.",
                highlightTarget: "tour.lighting.rotation",
                isComplete: () => SessionState.GetBool(LightingRotationKey, false)),
            new TourStep(
                id: "lighting_autorotate",
                title: "Auto Rotate",
                instruction: "Toggle Auto Rotate on, then toggle it off.\n\nAuto Rotate is a quick preview for moving light.",
                highlightTarget: "tour.lighting.autorotate",
                isComplete: () => SessionState.GetBool(LightingAutoRotateCycleKey, false)),
            new TourStep(
                id: "scriptables_select",
                title: "Open Scriptables",
                instruction: "Click the Scriptables tab to continue.\n\nScriptables control per-game shader lighting flags.",
                highlightTarget: "tour.modules.scriptables",
                isComplete: () => IsSelectedModule("Scriptables")),
            new TourStep(
                id: "scriptables_shadowboost",
                title: "Enable Shadow Boost",
                instruction: "Enable Shadow Boost and check the scene.\n\nShadow Boost strengthens contact shadows.",
                highlightTarget: "tour.scriptables.shadowboost",
                isComplete: () => SessionState.GetBool(ScriptablesShadowBoostKey, false)),
            new TourStep(
                id: "scriptables_leveladjust",
                title: "Enable Level Adjust",
                instruction: "Enable Level Adjust and check the scene.\n\nLevel Adjust lifts overall brightness for cutscene looks.",
                highlightTarget: "tour.scriptables.leveladjust",
                isComplete: () => SessionState.GetBool(ScriptablesLevelAdjustKey, false)),
            new TourStep(
                id: "scriptables_reset",
                title: "Reset Lighting Flags",
                instruction: "Click the highlighted toggle to continue.\n\nShadow Boost and Level Adjust are turned off for you so the rest of the tour uses neutral lighting.",
                highlightTarget: "tour.scriptables.reset",
                isComplete: () => SessionState.GetBool(ScriptablesResetKey, false)),
            new TourStep(
                id: "postprocessing_select",
                title: "Open Post Processing",
                instruction: "Click the Post Processing tab to continue.\n\nProfiles match in-game color grading and bloom.",
                highlightTarget: "tour.modules.postprocessing",
                isComplete: () => IsSelectedModule("Post Processing")),
            new TourStep(
                id: "postprocessing_profile",
                title: "Select Profile",
                instruction: "Click the Profile dropdown and select Genshin Impact.\n\nThis profile matches the tutorial look.",
                highlightTarget: "tour.postprocessing.profile",
                isComplete: () => SessionState.GetBool(PostProcessingProfileKey, false)),
            new TourStep(
                id: "renders_select",
                title: "Open Renders",
                instruction: "Click the Renders tab to continue.\n\nRenders captures high-res stills from a chosen camera.",
                highlightTarget: "tour.modules.renders",
                isComplete: () => IsSelectedModule("Renders")),
            new TourStep(
                id: "renders_camera",
                title: "Select Camera",
                instruction: "Click Use Main to pick the main camera.\n\nThis matches what you see in the scene.",
                highlightTarget: "tour.renders.camera",
                isComplete: () => SessionState.GetBool(RendersCameraKey, false)),
            new TourStep(
                id: "renders_transparent",
                title: "Transparent Background",
                instruction: "Toggle Transparent Background on for alpha. Off makes opaque renders.\n\nUse alpha for cutouts and compositing.",
                highlightTarget: "tour.renders.transparent",
                isComplete: () => SessionState.GetBool(RendersTransparentKey, false)),
            new TourStep(
                id: "renders_sync",
                title: "Sync With Scene Camera",
                instruction: "Toggle Sync with Scene Camera on to follow the Scene view.\n\nThis mirrors Scene view framing.",
                highlightTarget: "tour.renders.sync",
                isComplete: () => SessionState.GetBool(RendersSyncKey, false)),
            new TourStep(
                id: "renders_watermark",
                title: "Watermark",
                instruction: "Toggle Watermark on to add the logo.\n\nUse this to match in-game branding.",
                highlightTarget: "tour.renders.watermark",
                isComplete: () => SessionState.GetBool(RendersWatermarkKey, false)),
            new TourStep(
                id: "footer",
                title: "Footer Actions",
                instruction: "Click Create Prefab to continue.\n\nFooter actions apply to the active model.",
                highlightTarget: "tour.footer",
                isComplete: () => SessionState.GetBool(FooterExplainedKey, false)),
            new TourStep(
                id: "prefab",
                title: "Create Prefab",
                instruction: "Click Create Prefab to save the model.\n\nPrefabs let you reuse this setup later.",
                highlightTarget: "tour.footer.prefab",
                isComplete: () => SessionState.GetBool(PrefabCreatedKey, false)),
            new TourStep(
                id: "finish",
                title: "Finish",
                instruction: "Click the highlighted callout to finish the tour.\n\nYou are all set and your prefab is saved.",
                highlightTarget: "tour.finish.callout",
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
            SessionState.SetBool(LightingLightTypeKey, false);
            SessionState.SetBool(LightingLightAddedKey, false);
            SessionState.SetBool(LightingLightRemovedKey, false);
            SessionState.SetBool(LightingRotationKey, false);
            SessionState.SetBool(LightingAutoRotateSeenKey, false);
            SessionState.SetBool(LightingAutoRotateCycleKey, false);
            SessionState.SetBool(ScriptablesShadowBoostKey, false);
            SessionState.SetBool(ScriptablesLevelAdjustKey, false);
            SessionState.SetBool(ScriptablesResetKey, false);
            SessionState.SetBool(PostProcessingProfileKey, false);
            SessionState.SetBool(RendersCameraKey, false);
            SessionState.SetBool(RendersTransparentKey, false);
            SessionState.SetBool(RendersSyncKey, false);
            SessionState.SetBool(RendersWatermarkKey, false);
            SessionState.SetBool(FirstTimeConfirmedKey, false);
            SessionState.SetBool(FooterExplainedKey, false);
            SessionState.SetBool(PrefabCreatedKey, false);
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
            try
            {
                OnStepChanged?.Invoke();
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Tour", ex.ToString(), LogType.Exception);
            }
            HoyoToonGuidedTourWindow.EnsureWindowVisible(false);
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
            NotifyStepChanged();
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

        public static void NotifyLightingLightTypePicked()
        {
            SessionState.SetBool(LightingLightTypeKey, true);
            NotifyStepChanged();
        }

        public static void NotifyLightingLightAdded()
        {
            SessionState.SetBool(LightingLightAddedKey, true);
            NotifyStepChanged();
        }

        public static void NotifyLightingLightRemoved()
        {
            SessionState.SetBool(LightingLightRemovedKey, true);
            NotifyStepChanged();
        }

        public static void NotifyLightingRotationAdjusted()
        {
            SessionState.SetBool(LightingRotationKey, true);
            NotifyStepChanged();
        }

        public static void NotifyLightingAutoRotateToggled(bool enabled)
        {
            if (enabled)
            {
                SessionState.SetBool(LightingAutoRotateSeenKey, true);
                NotifyStepChanged();
                return;
            }

            if (SessionState.GetBool(LightingAutoRotateSeenKey, false))
            {
                SessionState.SetBool(LightingAutoRotateCycleKey, true);
                NotifyStepChanged();
            }
        }

        public static void NotifyScriptablesShadowBoostEnabled()
        {
            SessionState.SetBool(ScriptablesShadowBoostKey, true);
            NotifyStepChanged();
        }

        public static void NotifyScriptablesLevelAdjustEnabled()
        {
            SessionState.SetBool(ScriptablesLevelAdjustKey, true);
            NotifyStepChanged();
        }

        public static void NotifyScriptablesReset()
        {
            SessionState.SetBool(ScriptablesResetKey, true);
            NotifyStepChanged();
        }

        public static void NotifyPostProcessingProfilePicked()
        {
            SessionState.SetBool(PostProcessingProfileKey, true);
            NotifyStepChanged();
        }

        public static void NotifyRendersCameraSelected()
        {
            SessionState.SetBool(RendersCameraKey, true);
            NotifyStepChanged();
        }

        public static void NotifyRendersTransparentEnabled()
        {
            SessionState.SetBool(RendersTransparentKey, true);
            NotifyStepChanged();
        }

        public static void NotifyRendersSyncEnabled()
        {
            SessionState.SetBool(RendersSyncKey, true);
            NotifyStepChanged();
        }

        public static void NotifyRendersWatermarkEnabled()
        {
            SessionState.SetBool(RendersWatermarkKey, true);
            NotifyStepChanged();
        }

        public static void CompleteFooterStep()
        {
            SessionState.SetBool(FooterExplainedKey, true);
            NotifyStepChanged();
        }

        public static void NotifyPrefabCreated()
        {
            SessionState.SetBool(PrefabCreatedKey, true);
            NotifyStepChanged();
        }

        public static void ConfirmFirstTime(bool isFirstTime)
        {
            if (!isFirstTime)
            {
                EditorPrefs.SetBool(TourShownKey, true);
                StopTour();
                return;
            }

            SessionState.SetBool(FirstTimeConfirmedKey, true);
            Advance();
        }

        public static bool HasSeenTour()
        {
            return EditorPrefs.GetBool(TourShownKey, false);
        }

        public static void MarkTourSeen()
        {
            EditorPrefs.SetBool(TourShownKey, true);
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
            SessionState.SetBool(ModelApprovedKey, true);
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

            if (!IsActive)
            {
                HoyoToonGuidedTourWindow.EnsureWindowVisible(false);
                return;
            }

            bool shouldShowWindow = CurrentStepIndex <= 2 || SessionState.GetBool(ForceWindowKey, false);
            HoyoToonGuidedTourWindow.EnsureWindowVisible(shouldShowWindow);
            if (CurrentStepIndex > 2)
            {
                SessionState.SetBool(ForceWindowKey, false);
            }
            if (string.Equals(CurrentStep.id, "finish", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AutoAdvanceIfComplete();
        }

        public static bool ShouldShowManagerHandoffInline()
        {
            return IsActive && string.Equals(CurrentStep.id, "modules", StringComparison.OrdinalIgnoreCase)
                && !SessionState.GetBool(ManagerHandoffKey, false);
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
