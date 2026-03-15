#if UNITY_EDITOR
using UnityEditor;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Debugging
{
    /// <summary>Debug menu items for testing and resetting the guided tour.</summary>
    internal static class OnboardingDebugMenu
    {
        private const string MenuRoot = "HoyoToon/Debug/Onboarding/";
        private const string MenuResetRoot = "HoyoToon/Debug/Onboarding/Reset To/";

        [MenuItem(MenuRoot + "Start Tour", false, 600)]
        private static void StartTour()
        {
            GuidedTourController.StartTour();
        }

        [MenuItem(MenuRoot + "Reset Steps", false, 601)]
        private static void ResetSteps()
        {
            GuidedTourController.StartTourAtStep(StepIds.FirstTime);
        }

        [MenuItem(MenuRoot + "Jump To Finish", false, 602)]
        private static void JumpToFinish()
        {
            GuidedTourController.StartTourAtStep(StepIds.Finish);
        }

        [MenuItem(MenuRoot + "Stop Tour", false, 603)]
        private static void StopTour()
        {
            GuidedTourController.StopTour();
        }

        [MenuItem(MenuResetRoot + "1 - First Time", false, 700)]
        private static void ResetToFirstTime() => GuidedTourController.StartTourAtStep(StepIds.FirstTime);

        [MenuItem(MenuResetRoot + "2 - Resources", false, 701)]
        private static void ResetToResources() => GuidedTourController.StartTourAtStep(StepIds.Resources);

        [MenuItem(MenuResetRoot + "3 - Scene", false, 702)]
        private static void ResetToScene() => GuidedTourController.StartTourAtStep(StepIds.Scene);

        [MenuItem(MenuResetRoot + "4 - Manager", false, 703)]
        private static void ResetToManager() => GuidedTourController.StartTourAtStep(StepIds.Manager);

        [MenuItem(MenuResetRoot + "5 - Models Module", false, 704)]
        private static void ResetToModelsModule() => GuidedTourController.StartTourAtStep(StepIds.Modules);

        [MenuItem(MenuResetRoot + "6 - Game", false, 705)]
        private static void ResetToGame() => GuidedTourController.StartTourAtStep(StepIds.Game);

        [MenuItem(MenuResetRoot + "7 - Character", false, 706)]
        private static void ResetToCharacter() => GuidedTourController.StartTourAtStep(StepIds.Character);

        [MenuItem(MenuResetRoot + "8 - Variant", false, 707)]
        private static void ResetToVariant() => GuidedTourController.StartTourAtStep(StepIds.Variant);

        [MenuItem(MenuResetRoot + "9 - FBX", false, 708)]
        private static void ResetToFbx() => GuidedTourController.StartTourAtStep(StepIds.Fbx);

        [MenuItem(MenuResetRoot + "10 - Download", false, 709)]
        private static void ResetToDownload() => GuidedTourController.StartTourAtStep(StepIds.Download);

        [MenuItem(MenuResetRoot + "11 - Main Module", false, 710)]
        private static void ResetToMainModule() => GuidedTourController.StartTourAtStep(StepIds.MainModule);

        [MenuItem(MenuResetRoot + "12 - Add Model", false, 711)]
        private static void ResetToAddModel() => GuidedTourController.StartTourAtStep(StepIds.AddModel);

        [MenuItem(MenuResetRoot + "13 - Auto Setup", false, 712)]
        private static void ResetToAutoSetup() => GuidedTourController.StartTourAtStep(StepIds.AutoSetup);

        [MenuItem(MenuResetRoot + "14 - Model In Scene", false, 713)]
        private static void ResetToModel() => GuidedTourController.StartTourAtStep(StepIds.Model);

        [MenuItem(MenuResetRoot + "15 - Lighting Select", false, 714)]
        private static void ResetToLightingSelect() => GuidedTourController.StartTourAtStep(StepIds.LightingSelect);

        [MenuItem(MenuResetRoot + "16 - Lighting Light Type", false, 715)]
        private static void ResetToLightingType() => GuidedTourController.StartTourAtStep(StepIds.LightingLightType);

        [MenuItem(MenuResetRoot + "17 - Lighting Add Light", false, 716)]
        private static void ResetToLightingAdd() => GuidedTourController.StartTourAtStep(StepIds.LightingAddLight);

        [MenuItem(MenuResetRoot + "18 - Lighting Remove", false, 717)]
        private static void ResetToLightingRemove() => GuidedTourController.StartTourAtStep(StepIds.LightingRemove);

        [MenuItem(MenuResetRoot + "19 - Lighting Rotation", false, 718)]
        private static void ResetToLightingRotation() => GuidedTourController.StartTourAtStep(StepIds.LightingRotation);

        [MenuItem(MenuResetRoot + "20 - Lighting Auto Rotate", false, 719)]
        private static void ResetToLightingAutoRotate() => GuidedTourController.StartTourAtStep(StepIds.LightingAutoRotate);

        [MenuItem(MenuResetRoot + "21 - Scene Select", false, 720)]
        private static void ResetToScriptablesSelect() => GuidedTourController.StartTourAtStep(StepIds.ScriptablesSelect);

        [MenuItem(MenuResetRoot + "22 - Level Adjust", false, 721)]
        private static void ResetToLevelAdjust() => GuidedTourController.StartTourAtStep(StepIds.ScriptablesLevelAdjust);

        [MenuItem(MenuResetRoot + "23 - Reset Scene Flags", false, 722)]
        private static void ResetToScriptablesReset() => GuidedTourController.StartTourAtStep(StepIds.ScriptablesReset);

        [MenuItem(MenuResetRoot + "24 - Post Processing Select", false, 723)]
        private static void ResetToPostProcessingSelect() => GuidedTourController.StartTourAtStep(StepIds.PostProcessingSelect);

        [MenuItem(MenuResetRoot + "25 - Post Processing Profile", false, 724)]
        private static void ResetToPostProcessingProfile() => GuidedTourController.StartTourAtStep(StepIds.PostProcessingProfile);

        [MenuItem(MenuResetRoot + "26 - Renders Select", false, 725)]
        private static void ResetToRendersSelect() => GuidedTourController.StartTourAtStep(StepIds.RendersSelect);

        [MenuItem(MenuResetRoot + "27 - Renders Camera", false, 726)]
        private static void ResetToRendersCamera() => GuidedTourController.StartTourAtStep(StepIds.RendersCamera);

        [MenuItem(MenuResetRoot + "28 - Renders Transparent", false, 727)]
        private static void ResetToRendersTransparent() => GuidedTourController.StartTourAtStep(StepIds.RendersTransparent);

        [MenuItem(MenuResetRoot + "29 - Renders Sync", false, 728)]
        private static void ResetToRendersSync() => GuidedTourController.StartTourAtStep(StepIds.RendersSync);

        [MenuItem(MenuResetRoot + "30 - Renders Watermark", false, 729)]
        private static void ResetToRendersWatermark() => GuidedTourController.StartTourAtStep(StepIds.RendersWatermark);

        [MenuItem(MenuResetRoot + "31 - Footer Actions", false, 730)]
        private static void ResetToFooter() => GuidedTourController.StartTourAtStep(StepIds.Footer);

        [MenuItem(MenuResetRoot + "32 - Prefab", false, 731)]
        private static void ResetToPrefab() => GuidedTourController.StartTourAtStep(StepIds.Prefab);

        [MenuItem(MenuResetRoot + "33 - Finish", false, 732)]
        private static void ResetToFinish() => GuidedTourController.StartTourAtStep(StepIds.Finish);
    }
}
#endif
