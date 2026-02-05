#if UNITY_EDITOR
using UnityEditor;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.Debugging
{
    internal static class OnboardingDebugMenu
    {
        private const string MenuRoot = "HoyoToon/Debug/Onboarding/";
        private const string MenuResetRoot = "HoyoToon/Debug/Onboarding/Reset To/";

        [MenuItem(MenuRoot + "Start Tour", false, 600)]
        private static void StartTour()
        {
            HoyoToonGuidedTourController.StartTour();
        }

        [MenuItem(MenuRoot + "Reset Steps", false, 601)]
        private static void ResetSteps()
        {
            HoyoToonGuidedTourController.StartTourAtStep("firsttime");
        }

        [MenuItem(MenuRoot + "Jump To Finish", false, 602)]
        private static void JumpToFinish()
        {
            HoyoToonGuidedTourController.StartTourAtStep("finish");
        }

        [MenuItem(MenuRoot + "Stop Tour", false, 603)]
        private static void StopTour()
        {
            HoyoToonGuidedTourController.StopTour();
        }

        [MenuItem(MenuResetRoot + "1 - First Time", false, 700)]
        private static void ResetToFirstTime() => HoyoToonGuidedTourController.StartTourAtStep("firsttime");

        [MenuItem(MenuResetRoot + "2 - Resources", false, 701)]
        private static void ResetToResources() => HoyoToonGuidedTourController.StartTourAtStep("resources");

        [MenuItem(MenuResetRoot + "3 - Scene", false, 702)]
        private static void ResetToScene() => HoyoToonGuidedTourController.StartTourAtStep("scene");

        [MenuItem(MenuResetRoot + "4 - Manager", false, 703)]
        private static void ResetToManager() => HoyoToonGuidedTourController.StartTourAtStep("manager");

        [MenuItem(MenuResetRoot + "5 - Models Module", false, 704)]
        private static void ResetToModelsModule() => HoyoToonGuidedTourController.StartTourAtStep("modules");

        [MenuItem(MenuResetRoot + "6 - Game", false, 705)]
        private static void ResetToGame() => HoyoToonGuidedTourController.StartTourAtStep("game");

        [MenuItem(MenuResetRoot + "7 - Character", false, 706)]
        private static void ResetToCharacter() => HoyoToonGuidedTourController.StartTourAtStep("character");

        [MenuItem(MenuResetRoot + "8 - Variant", false, 707)]
        private static void ResetToVariant() => HoyoToonGuidedTourController.StartTourAtStep("variant");

        [MenuItem(MenuResetRoot + "9 - FBX", false, 708)]
        private static void ResetToFbx() => HoyoToonGuidedTourController.StartTourAtStep("fbx");

        [MenuItem(MenuResetRoot + "10 - Download", false, 709)]
        private static void ResetToDownload() => HoyoToonGuidedTourController.StartTourAtStep("download");

        [MenuItem(MenuResetRoot + "11 - Main Module", false, 710)]
        private static void ResetToMainModule() => HoyoToonGuidedTourController.StartTourAtStep("mainmodule");

        [MenuItem(MenuResetRoot + "12 - Add Model", false, 711)]
        private static void ResetToAddModel() => HoyoToonGuidedTourController.StartTourAtStep("addmodel");

        [MenuItem(MenuResetRoot + "13 - Auto Setup", false, 712)]
        private static void ResetToAutoSetup() => HoyoToonGuidedTourController.StartTourAtStep("autosetup");

        [MenuItem(MenuResetRoot + "14 - Model In Scene", false, 713)]
        private static void ResetToModel() => HoyoToonGuidedTourController.StartTourAtStep("model");

        [MenuItem(MenuResetRoot + "15 - Lighting Select", false, 714)]
        private static void ResetToLightingSelect() => HoyoToonGuidedTourController.StartTourAtStep("lighting_select");

        [MenuItem(MenuResetRoot + "16 - Lighting Light Type", false, 715)]
        private static void ResetToLightingType() => HoyoToonGuidedTourController.StartTourAtStep("lighting_lighttype");

        [MenuItem(MenuResetRoot + "17 - Lighting Add Light", false, 716)]
        private static void ResetToLightingAdd() => HoyoToonGuidedTourController.StartTourAtStep("lighting_addlight");

        [MenuItem(MenuResetRoot + "18 - Lighting Remove", false, 717)]
        private static void ResetToLightingRemove() => HoyoToonGuidedTourController.StartTourAtStep("lighting_remove");

        [MenuItem(MenuResetRoot + "19 - Lighting Rotation", false, 718)]
        private static void ResetToLightingRotation() => HoyoToonGuidedTourController.StartTourAtStep("lighting_rotation");

        [MenuItem(MenuResetRoot + "20 - Lighting Auto Rotate", false, 719)]
        private static void ResetToLightingAutoRotate() => HoyoToonGuidedTourController.StartTourAtStep("lighting_autorotate");

        [MenuItem(MenuResetRoot + "21 - Scriptables Select", false, 720)]
        private static void ResetToScriptablesSelect() => HoyoToonGuidedTourController.StartTourAtStep("scriptables_select");

        [MenuItem(MenuResetRoot + "22 - Shadow Boost", false, 721)]
        private static void ResetToShadowBoost() => HoyoToonGuidedTourController.StartTourAtStep("scriptables_shadowboost");

        [MenuItem(MenuResetRoot + "23 - Level Adjust", false, 722)]
        private static void ResetToLevelAdjust() => HoyoToonGuidedTourController.StartTourAtStep("scriptables_leveladjust");

        [MenuItem(MenuResetRoot + "24 - Reset Scriptables", false, 723)]
        private static void ResetToScriptablesReset() => HoyoToonGuidedTourController.StartTourAtStep("scriptables_reset");

        [MenuItem(MenuResetRoot + "25 - Post Processing Select", false, 724)]
        private static void ResetToPostProcessingSelect() => HoyoToonGuidedTourController.StartTourAtStep("postprocessing_select");

        [MenuItem(MenuResetRoot + "26 - Post Processing Profile", false, 725)]
        private static void ResetToPostProcessingProfile() => HoyoToonGuidedTourController.StartTourAtStep("postprocessing_profile");

        [MenuItem(MenuResetRoot + "27 - Renders Select", false, 726)]
        private static void ResetToRendersSelect() => HoyoToonGuidedTourController.StartTourAtStep("renders_select");

        [MenuItem(MenuResetRoot + "28 - Renders Camera", false, 727)]
        private static void ResetToRendersCamera() => HoyoToonGuidedTourController.StartTourAtStep("renders_camera");

        [MenuItem(MenuResetRoot + "29 - Renders Transparent", false, 728)]
        private static void ResetToRendersTransparent() => HoyoToonGuidedTourController.StartTourAtStep("renders_transparent");

        [MenuItem(MenuResetRoot + "30 - Renders Sync", false, 729)]
        private static void ResetToRendersSync() => HoyoToonGuidedTourController.StartTourAtStep("renders_sync");

        [MenuItem(MenuResetRoot + "31 - Renders Watermark", false, 730)]
        private static void ResetToRendersWatermark() => HoyoToonGuidedTourController.StartTourAtStep("renders_watermark");

        [MenuItem(MenuResetRoot + "32 - Footer Actions", false, 731)]
        private static void ResetToFooter() => HoyoToonGuidedTourController.StartTourAtStep("footer");

        [MenuItem(MenuResetRoot + "33 - Prefab", false, 732)]
        private static void ResetToPrefab() => HoyoToonGuidedTourController.StartTourAtStep("prefab");

        [MenuItem(MenuResetRoot + "34 - Finish", false, 733)]
        private static void ResetToFinish() => HoyoToonGuidedTourController.StartTourAtStep("finish");
    }
}
#endif
