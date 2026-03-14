#if UNITY_EDITOR
using System;
using UnityEditor;

namespace HoyoToon.Editor.Onboarding
{
    public static partial class GuidedTourController
    {
        public static class StepIds
        {
            public const string FirstTime             = "firsttime";
            public const string Prerequisites         = "prerequisites";
            public const string Resources             = "resources";
            public const string Scene                 = "scene";
            public const string Manager               = "manager";
            public const string Modules               = "modules";
            public const string Game                  = "game";
            public const string Character             = "character";
            public const string Variant               = "variant";
            public const string Fbx                   = "fbx";
            public const string Download              = "download";
            public const string MainModule            = "mainmodule";
            public const string AddModel              = "addmodel";
            public const string AutoSetup             = "autosetup";
            public const string Model                 = "model";
            public const string LightingSelect        = "lighting_select";
            public const string LightingLightType     = "lighting_lighttype";
            public const string LightingAddLight      = "lighting_addlight";
            public const string LightingRemove        = "lighting_remove";
            public const string LightingRotation      = "lighting_rotation";
            public const string LightingAutoRotate    = "lighting_autorotate";
            public const string ScriptablesSelect     = "scriptables_select";
            public const string ScriptablesShadowBoost = "scriptables_shadowboost";
            public const string ScriptablesLevelAdjust = "scriptables_leveladjust";
            public const string ScriptablesReset      = "scriptables_reset";
            public const string PostProcessingSelect  = "postprocessing_select";
            public const string PostProcessingProfile = "postprocessing_profile";
            public const string RendersSelect         = "renders_select";
            public const string RendersCamera         = "renders_camera";
            public const string RendersTransparent    = "renders_transparent";
            public const string RendersSync           = "renders_sync";
            public const string RendersWatermark      = "renders_watermark";
            public const string Footer                = "footer";
            public const string Prefab                = "prefab";
            public const string Finish                = "finish";
        }

        private static class StepInstructionText
        {
            public static readonly string FirstTime = "Let us know if this is your first time so we can tailor the walkthrough.";
            public static readonly string Prerequisites = "Run prerequisite checks and apply fixes before downloading resources.";
            public static readonly string Resources = "Download any missing resources so materials and lighting look correct.";
            public static readonly string Scene = "Open the HoyoToon scene to continue. This tutorial is controlled and should stay in the packaged HoyoToon scene.";
            public static readonly string Manager = "We are about to switch the Inspector to the HoyoToon Manager so the rest of the tour can continue there.";
            public static readonly string Modules = "Go to Models to download Acheron and choose the correct files.";
            public static readonly string Game = "Select Honkai Star Rail so we pull the right resources.";
            public static readonly string Character = "Pick Acheron so the download matches this walkthrough.";
            public static readonly string Variant = "Use the Default variant so the file names match the steps.";
            public static readonly string Fbx = "Select No Anims to keep the setup lighter for this guide.";
            public static readonly string Download = "Click Download Selected Models and wait for the import to finish.";
            public static readonly string MainModule = "Click the Main tab to continue.";
            public static readonly string AddModel = "In Add Model, choose the downloaded FBX named Art_Acheron_01.";
            public static readonly string AutoSetup = "Press Auto Setup to generate materials and add the model to the scene.";
            public static readonly string Model = "Confirm the model appears in the scene and looks correct.";
            public static readonly string LightingSelect = "Click the Lighting tab to continue.\n\nLighting controls let you shape the key light and shadows on the model.";
            public static readonly string LightingLightType = "Click the Light Type dropdown and choose a light.\n\nDifferent light types help preview how the model reads.";
            public static readonly string LightingAddLight = "Click Add Light to create the light in the scene.\n\nThis is a preview light and can be removed after.";
            public static readonly string LightingRemove = "Click the highlighted area to remove the tutorial light.\n\nThe base scene lighting stays intact.\n\n Scroll down for the next steps.";
            public static readonly string LightingRotation = "Drag Rotation to aim the key light.\n\nThis changes shadow direction and highlights.";
            public static readonly string LightingAutoRotate = "Toggle Auto Rotate on, then toggle it off.\n\nAuto Rotate is a quick preview for moving light.";
            public static readonly string ScriptablesSelect = "Click the Scriptables tab to continue.\n\nScriptables control per-game shader lighting flags.";
            public static readonly string ScriptablesShadowBoost = "Enable Shadow Boost and check the scene.\n\nShadow Boost strengthens contact shadows.";
            public static readonly string ScriptablesLevelAdjust = "Enable Level Adjust and check the scene.\n\nLevel Adjust lifts overall brightness for cutscene looks.";
            public static readonly string ScriptablesReset = "Click the highlighted toggle to continue.\n\nShadow Boost and Level Adjust are turned off for you so the rest of the tour uses neutral lighting.";
            public static readonly string PostProcessingSelect = "Click the Post Processing tab to continue.\n\nProfiles match in-game color grading and bloom.";
            public static readonly string PostProcessingProfile = "The profile is set to Genshin Impact. Click the dropdown and select Honkai Star Rail.\n\nThis profile matches the tutorial look.";
            public static readonly string RendersSelect = "Click the Renders tab to continue.\n\nRenders captures high-res stills from a chosen camera.";
            public static readonly string RendersCamera = "Click Use Main to pick the main camera.\n\nThis matches what you see in the scene.";
            public static readonly string RendersTransparent = "Toggle Transparent Background on for alpha. Off makes opaque renders.\n\nUse alpha for cutouts and compositing.";
            public static readonly string RendersSync = "Toggle Sync with Scene Camera on to follow the Scene view.\n\nThis mirrors Scene view framing.";
            public static readonly string RendersWatermark = "Toggle Watermark on to add the logo.\n\nUse this to match in-game branding.\n\n Scroll down for the next steps.";
            public static readonly string Footer = "Click Create Prefab to continue.\n\nFooter actions apply to the active model.";
            public static readonly string Prefab = "Click Create Prefab to save the model.\n\nPrefabs let you reuse this setup later.";
            public static readonly string Finish = "Congrats on finishing the guided tour! Click the highlighted callout to wrap up.\n\nYour prefab is saved - have fun with HoyoToon.";
        }

        private static readonly TourStep[] s_steps =
        {
            new TourStep(
                id: StepIds.FirstTime,
                title: "First Time With HoyoToon?",
                instruction: StepInstructionText.FirstTime,
                highlightTarget: null,
                isComplete: () => SessionState.GetBool(FirstTimeConfirmedKey, false)),
            new TourStep(
                id: StepIds.Prerequisites,
                title: "Check Prerequisites",
                instruction: StepInstructionText.Prerequisites,
                highlightTarget: null,
                isComplete: () => ArePrerequisitesSatisfied()),
            new TourStep(
                id: StepIds.Resources,
                title: "Check Resources",
                instruction: StepInstructionText.Resources,
                highlightTarget: null,
                isComplete: () => !HasMissingResources()),
            new TourStep(
                id: StepIds.Scene,
                title: "Open HoyoToon Scene",
                instruction: StepInstructionText.Scene,
                highlightTarget: null,
                isComplete: () => IsSceneReady()),
            new TourStep(
                id: StepIds.Manager,
                title: "Ensure Manager",
                instruction: StepInstructionText.Manager,
                highlightTarget: "tour.manager.create",
                isComplete: () => HasManagerHandoffSignal()),
            new TourStep(
                id: StepIds.Modules,
                title: "Open Models Module",
                instruction: StepInstructionText.Modules,
                highlightTarget: "tour.modules.models",
                isComplete: () => string.Equals(SessionState.GetString(SelectedModuleKey, string.Empty), "Models", StringComparison.OrdinalIgnoreCase)),
            new TourStep(
                id: StepIds.Game,
                title: "Choose Game",
                instruction: StepInstructionText.Game,
                highlightTarget: "tour.models.game",
                isComplete: () => IsSelectedGame(DesiredGameName)),
            new TourStep(
                id: StepIds.Character,
                title: "Choose Character",
                instruction: StepInstructionText.Character,
                highlightTarget: "tour.models.character",
                isComplete: () => IsSelectedCharacter(DesiredCharacterName)),
            new TourStep(
                id: StepIds.Variant,
                title: "Choose Variant",
                instruction: StepInstructionText.Variant,
                highlightTarget: "tour.models.variant",
                isComplete: () => IsSelectedVariant(DesiredVariantName)),
            new TourStep(
                id: StepIds.Fbx,
                title: "Choose FBX",
                instruction: StepInstructionText.Fbx,
                highlightTarget: "tour.models.fbx.noanims",
                isComplete: () => IsSelectedFbxChoice(DesiredFbxChoiceName)),
            new TourStep(
                id: StepIds.Download,
                title: "Download Your First Model",
                instruction: StepInstructionText.Download,
                highlightTarget: "tour.models.download",
                isComplete: () => SessionState.GetBool(DownloadedKey, false)),
            new TourStep(
                id: StepIds.MainModule,
                title: "Open Main Module",
                instruction: StepInstructionText.MainModule,
                highlightTarget: "tour.modules.main",
                isComplete: () => string.Equals(SessionState.GetString(SelectedModuleKey, string.Empty), "Main", StringComparison.OrdinalIgnoreCase)),
            new TourStep(
                id: StepIds.AddModel,
                title: "Select Downloaded FBX",
                instruction: StepInstructionText.AddModel,
                highlightTarget: "tour.manager.addmodel",
                isComplete: () => SessionState.GetBool(SelectedModelKey, false)),
            new TourStep(
                id: StepIds.AutoSetup,
                title: "Run Auto Setup",
                instruction: StepInstructionText.AutoSetup,
                highlightTarget: "tour.manager.autosetup",
                isComplete: () => SessionState.GetBool(AutoSetupKey, false)),
            new TourStep(
                id: StepIds.Model,
                title: "Model In Scene",
                instruction: StepInstructionText.Model,
                highlightTarget: null,
                isComplete: () => IsModelInScene() && SessionState.GetBool(ModelApprovedKey, false)),
            new TourStep(
                id: StepIds.LightingSelect,
                title: "Open Lighting Module",
                instruction: StepInstructionText.LightingSelect,
                highlightTarget: "tour.modules.lighting",
                isComplete: () => IsSelectedModule("Lighting")),
            new TourStep(
                id: StepIds.LightingLightType,
                title: "Choose Light Type",
                instruction: StepInstructionText.LightingLightType,
                highlightTarget: "tour.lighting.create.dropdown",
                isComplete: () => SessionState.GetBool(LightingLightTypeKey, false)),
            new TourStep(
                id: StepIds.LightingAddLight,
                title: "Add Light",
                instruction: StepInstructionText.LightingAddLight,
                highlightTarget: "tour.lighting.create.add",
                isComplete: () => SessionState.GetBool(LightingLightAddedKey, false)),
            new TourStep(
                id: StepIds.LightingRemove,
                title: "Remove Tutorial Light",
                instruction: StepInstructionText.LightingRemove,
                highlightTarget: "tour.lighting.remove",
                isComplete: () => SessionState.GetBool(LightingLightRemovedKey, false)),
            new TourStep(
                id: StepIds.LightingRotation,
                title: "Rotate Light",
                instruction: StepInstructionText.LightingRotation,
                highlightTarget: "tour.lighting.rotation",
                isComplete: () => SessionState.GetBool(LightingRotationKey, false)),
            new TourStep(
                id: StepIds.LightingAutoRotate,
                title: "Auto Rotate",
                instruction: StepInstructionText.LightingAutoRotate,
                highlightTarget: "tour.lighting.autorotate",
                isComplete: () => SessionState.GetBool(LightingAutoRotateCycleKey, false)),
            new TourStep(
                id: StepIds.ScriptablesSelect,
                title: "Open Scriptables",
                instruction: StepInstructionText.ScriptablesSelect,
                highlightTarget: "tour.modules.scriptables",
                isComplete: () => IsSelectedModule("Scriptables")),
            new TourStep(
                id: StepIds.ScriptablesShadowBoost,
                title: "Enable Shadow Boost",
                instruction: StepInstructionText.ScriptablesShadowBoost,
                highlightTarget: "tour.scriptables.shadowboost",
                isComplete: () => SessionState.GetBool(ScriptablesShadowBoostKey, false)),
            new TourStep(
                id: StepIds.ScriptablesLevelAdjust,
                title: "Enable Level Adjust",
                instruction: StepInstructionText.ScriptablesLevelAdjust,
                highlightTarget: "tour.scriptables.leveladjust",
                isComplete: () => SessionState.GetBool(ScriptablesLevelAdjustKey, false)),
            new TourStep(
                id: StepIds.ScriptablesReset,
                title: "Reset Lighting Flags",
                instruction: StepInstructionText.ScriptablesReset,
                highlightTarget: "tour.scriptables.reset",
                isComplete: () => SessionState.GetBool(ScriptablesResetKey, false)),
            new TourStep(
                id: StepIds.RendersSelect,
                title: "Open Renders",
                instruction: StepInstructionText.RendersSelect,
                highlightTarget: "tour.modules.renders",
                isComplete: () => IsSelectedModule("Renders")),
            new TourStep(
                id: StepIds.RendersCamera,
                title: "Select Camera",
                instruction: StepInstructionText.RendersCamera,
                highlightTarget: "tour.renders.camera",
                isComplete: () => SessionState.GetBool(RendersCameraKey, false)),
            new TourStep(
                id: StepIds.RendersTransparent,
                title: "Transparent Background",
                instruction: StepInstructionText.RendersTransparent,
                highlightTarget: "tour.renders.transparent",
                isComplete: () => SessionState.GetBool(RendersTransparentKey, false)),
            new TourStep(
                id: StepIds.RendersSync,
                title: "Sync With Scene Camera",
                instruction: StepInstructionText.RendersSync,
                highlightTarget: "tour.renders.sync",
                isComplete: () => SessionState.GetBool(RendersSyncKey, false)),
            new TourStep(
                id: StepIds.RendersWatermark,
                title: "Watermark",
                instruction: StepInstructionText.RendersWatermark,
                highlightTarget: "tour.renders.watermark",
                isComplete: () => SessionState.GetBool(RendersWatermarkKey, false)),
            new TourStep(
                id: StepIds.Footer,
                title: "Footer Actions",
                instruction: StepInstructionText.Footer,
                highlightTarget: "tour.footer",
                isComplete: () => SessionState.GetBool(FooterExplainedKey, false)),
            new TourStep(
                id: StepIds.Prefab,
                title: "Create Prefab",
                instruction: StepInstructionText.Prefab,
                highlightTarget: "tour.footer.prefab",
                isComplete: () => SessionState.GetBool(PrefabCreatedKey, false)),
            new TourStep(
                id: StepIds.Finish,
                title: "Finish",
                instruction: StepInstructionText.Finish,
                highlightTarget: "tour.finish.callout",
                isComplete: () => true)
        };

        public readonly struct TourStep
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
