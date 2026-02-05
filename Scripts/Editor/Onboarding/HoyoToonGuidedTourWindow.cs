#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon;

namespace HoyoToon.EditorTools.Onboarding
{
    internal sealed class HoyoToonGuidedTourWindow : BaseHoyoToonWindow
    {
        private const string MenuPath = "HoyoToon/Getting Started/Guided Tour";
        private const string ResetMenuPath = "HoyoToon/Getting Started/Guided Tour (Reset)";
        private const string ResetStepMenuPrefix = "HoyoToon/Getting Started/Guided Tour/Reset To/";
        private GUIStyle _sectionHeaderStyle;

        [MenuItem(MenuPath, priority = 1)]
        public static void OpenFromMenu()
        {
            ShowWindow();
        }

        [MenuItem(ResetMenuPath, priority = 2)]
        public static void ResetFromMenu()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            HoyoToonGuidedTourController.StartTour();
            ShowWindow();
        }

        [MenuItem(ResetStepMenuPrefix + "1 - First Time", priority = 3)]
        public static void ResetToFirstTime() => ResetToStep("firsttime");

        [MenuItem(ResetStepMenuPrefix + "2 - Resources", priority = 4)]
        public static void ResetToResources() => ResetToStep("resources");

        [MenuItem(ResetStepMenuPrefix + "3 - Scene", priority = 5)]
        public static void ResetToScene() => ResetToStep("scene");

        [MenuItem(ResetStepMenuPrefix + "4 - Manager", priority = 6)]
        public static void ResetToManager() => ResetToStep("manager");

        [MenuItem(ResetStepMenuPrefix + "5 - Models Module", priority = 7)]
        public static void ResetToModelsModule() => ResetToStep("modules");

        [MenuItem(ResetStepMenuPrefix + "6 - Game", priority = 8)]
        public static void ResetToGame() => ResetToStep("game");

        [MenuItem(ResetStepMenuPrefix + "7 - Character", priority = 9)]
        public static void ResetToCharacter() => ResetToStep("character");

        [MenuItem(ResetStepMenuPrefix + "8 - Variant", priority = 10)]
        public static void ResetToVariant() => ResetToStep("variant");

        [MenuItem(ResetStepMenuPrefix + "9 - FBX", priority = 11)]
        public static void ResetToFbx() => ResetToStep("fbx");

        [MenuItem(ResetStepMenuPrefix + "10 - Download", priority = 12)]
        public static void ResetToDownload() => ResetToStep("download");

        [MenuItem(ResetStepMenuPrefix + "11 - Main Module", priority = 13)]
        public static void ResetToMainModule() => ResetToStep("mainmodule");

        [MenuItem(ResetStepMenuPrefix + "12 - Add Model", priority = 14)]
        public static void ResetToAddModel() => ResetToStep("addmodel");

        [MenuItem(ResetStepMenuPrefix + "13 - Auto Setup", priority = 15)]
        public static void ResetToAutoSetup() => ResetToStep("autosetup");

        [MenuItem(ResetStepMenuPrefix + "14 - Model In Scene", priority = 16)]
        public static void ResetToModel() => ResetToStep("model");

        [MenuItem(ResetStepMenuPrefix + "15 - Lighting Select", priority = 17)]
        public static void ResetToLightingSelect() => ResetToStep("lighting_select");

        [MenuItem(ResetStepMenuPrefix + "16 - Lighting Light Type", priority = 18)]
        public static void ResetToLightingType() => ResetToStep("lighting_lighttype");

        [MenuItem(ResetStepMenuPrefix + "17 - Lighting Add Light", priority = 19)]
        public static void ResetToLightingAdd() => ResetToStep("lighting_addlight");

        [MenuItem(ResetStepMenuPrefix + "18 - Lighting Remove", priority = 20)]
        public static void ResetToLightingRemove() => ResetToStep("lighting_remove");

        [MenuItem(ResetStepMenuPrefix + "19 - Lighting Rotation", priority = 21)]
        public static void ResetToLightingRotation() => ResetToStep("lighting_rotation");

        [MenuItem(ResetStepMenuPrefix + "20 - Lighting Auto Rotate", priority = 22)]
        public static void ResetToLightingAutoRotate() => ResetToStep("lighting_autorotate");

        [MenuItem(ResetStepMenuPrefix + "21 - Scriptables Select", priority = 23)]
        public static void ResetToScriptablesSelect() => ResetToStep("scriptables_select");

        [MenuItem(ResetStepMenuPrefix + "22 - Shadow Boost", priority = 24)]
        public static void ResetToShadowBoost() => ResetToStep("scriptables_shadowboost");

        [MenuItem(ResetStepMenuPrefix + "23 - Level Adjust", priority = 25)]
        public static void ResetToLevelAdjust() => ResetToStep("scriptables_leveladjust");

        [MenuItem(ResetStepMenuPrefix + "24 - Reset Scriptables", priority = 26)]
        public static void ResetToScriptablesReset() => ResetToStep("scriptables_reset");

        [MenuItem(ResetStepMenuPrefix + "25 - Post Processing Select", priority = 27)]
        public static void ResetToPostProcessingSelect() => ResetToStep("postprocessing_select");

        [MenuItem(ResetStepMenuPrefix + "26 - Post Processing Profile", priority = 28)]
        public static void ResetToPostProcessingProfile() => ResetToStep("postprocessing_profile");

        [MenuItem(ResetStepMenuPrefix + "27 - Renders Select", priority = 29)]
        public static void ResetToRendersSelect() => ResetToStep("renders_select");

        [MenuItem(ResetStepMenuPrefix + "28 - Renders Camera", priority = 30)]
        public static void ResetToRendersCamera() => ResetToStep("renders_camera");

        [MenuItem(ResetStepMenuPrefix + "29 - Renders Transparent", priority = 31)]
        public static void ResetToRendersTransparent() => ResetToStep("renders_transparent");

        [MenuItem(ResetStepMenuPrefix + "30 - Renders Sync", priority = 32)]
        public static void ResetToRendersSync() => ResetToStep("renders_sync");

        [MenuItem(ResetStepMenuPrefix + "31 - Renders Watermark", priority = 33)]
        public static void ResetToRendersWatermark() => ResetToStep("renders_watermark");

        [MenuItem(ResetStepMenuPrefix + "32 - Footer Actions", priority = 34)]
        public static void ResetToFooter() => ResetToStep("footer");

        [MenuItem(ResetStepMenuPrefix + "33 - Prefab", priority = 35)]
        public static void ResetToPrefab() => ResetToStep("prefab");

        [MenuItem(ResetStepMenuPrefix + "34 - Finish", priority = 36)]
        public static void ResetToFinish() => ResetToStep("finish");

        private static void ResetToStep(string stepId)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            HoyoToonGuidedTourController.StartTourAtStep(stepId);
            if (HoyoToonGuidedTourController.CurrentStepIndex <= 2)
            {
                ShowWindow();
            }
        }

        public static void ShowWindow()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var window = CreateInstance<HoyoToonGuidedTourWindow>();
            window.titleContent = new GUIContent("HoyoToon");
            window.PreSizeBeforeShow(UiLayout.WINDOW_DEFAULT_WIDTH);
            window.ShowUtility();
            window.Focus();
        }

        internal static void EnsureWindowVisible(bool shouldShow)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var window = Resources.FindObjectsOfTypeAll<HoyoToonGuidedTourWindow>().FirstOrDefault();
            if (shouldShow)
            {
                if (window == null)
                {
                    ShowWindow();
                }
                else
                {
                    window.Focus();
                }

                return;
            }

            if (window != null)
            {
                window.Close();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _title = "Guided Tour";
            _type = MessageType.Info;
            _topBar = TopBarConfig.Default();
            SetButtons(new[] { "Exit Tour", "Restart", "Close" }, 0, 2, HandleButton);
            HoyoToonGuidedTourController.OnStepChanged += Repaint;

            if (!HoyoToonGuidedTourController.IsActive)
            {
                HoyoToonGuidedTourController.StartTour();
            }
        }

        private void OnDisable()
        {
            HoyoToonGuidedTourController.OnStepChanged -= Repaint;
        }

        protected override float MeasureBodyContentHeight(float contentWidth)
        {
            EnsureStyles();
            EnsureLocalStyles();

            float total = 0f;
            total += _sectionHeaderStyle.CalcHeight(new GUIContent("Guided Tour"), contentWidth);
            total += EditorGUIUtility.singleLineHeight * 5f;
            total += 60f;
            return total;
        }

        protected override void DrawBodyContent(float contentWidth)
        {
            EnsureLocalStyles();
            DrawCurrentStep();
        }

        private void DrawCurrentStep()
        {
            var step = HoyoToonGuidedTourController.CurrentStep;
            GUILayout.Label("Guided Tour", _sectionHeaderStyle);
            GUILayout.Label(step.title, _messageLabelStyle);
            GUILayout.Space(2f);
            GUILayout.Label(step.instruction, _messageLabelStyle);
            GUILayout.Space(6f);

            if (string.Equals(step.id, "manager", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox("Once you select the manager, this window will close and guidance continues inline.", MessageType.Info);
                GUILayout.Space(4f);
            }

            DrawStepActions(step.id);
        }


        private void DrawStepActions(string stepId)
        {
            switch (stepId)
            {
                case "resources":
                    GUILayout.Label("Download missing resources so materials and lighting work correctly.", EditorStyles.centeredGreyMiniLabel);
                    if (GUILayout.Button("Open Resource Status", GUILayout.Height(24f)))
                    {
                        EditorApplication.ExecuteMenuItem("HoyoToon/Resources/Check Resource Status");
                    }
                    break;
                case "firsttime":
                    GUILayout.Label("Is this your first time using HoyoToon?", EditorStyles.centeredGreyMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Yes, guide me", GUILayout.Height(24f)))
                        {
                            HoyoToonGuidedTourController.ConfirmFirstTime(true);
                        }
                        if (GUILayout.Button("No, skip tour", GUILayout.Height(24f)))
                        {
                            HoyoToonGuidedTourController.ConfirmFirstTime(false);
                        }
                    }
                    break;
                case "scene":
                    if (GUILayout.Button("Open HoyoToon Scene", GUILayout.Height(28f)))
                    {
                        HoyoToonGuidedTourController.TryOpenHoyoToonScene();
                    }
                    if (GUILayout.Button("Approve Current Scene", GUILayout.Height(22f)))
                    {
                        HoyoToonGuidedTourController.ApproveCurrentScene();
                    }
                    break;
                case "manager":
                    if (GUILayout.Button("Select Manager", GUILayout.Height(24f)))
                    {
                        HoyoToonGuidedTourController.RequestCloseOnManagerSelect();
                        HoyoToonGuidedTourController.FocusManagerInScene();
                    }
                    if (GUILayout.Button("Create Manager", GUILayout.Height(24f)))
                    {
                        var go = new GameObject("HoyoToon Manager");
                        var manager = go.AddComponent<HoyoToonManager>();
                        Selection.activeObject = go;
                        EditorGUIUtility.PingObject(go);
                        HoyoToonGuidedTourController.NotifyManagerSeen(manager);
                    }
                    break;
                case "modules":
                    if (GUILayout.Button("Select Manager", GUILayout.Height(24f)))
                    {
                        HoyoToonGuidedTourController.FocusManagerInScene();
                    }
                    GUILayout.Label("Use the Modules bar in the Manager Inspector and click Models.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "game":
                    GUILayout.Label($"Select game: {HoyoToonGuidedTourController.DesiredGameName}", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "character":
                    GUILayout.Label($"Select character: {HoyoToonGuidedTourController.DesiredCharacterName}", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "variant":
                    GUILayout.Label($"Select variant: {HoyoToonGuidedTourController.DesiredVariantName}", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "fbx":
                    GUILayout.Label($"Select FBX: {HoyoToonGuidedTourController.DesiredFbxChoiceName}", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "download":
                    GUILayout.Label("Use the Models module lists and press Download Selected Models.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "mainmodule":
                    GUILayout.Label("Click Main in the Modules bar.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "addmodel":
                    GUILayout.Label($"Select {HoyoToonGuidedTourController.DesiredFbxAssetName} in the Add Model field.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "autosetup":
                    GUILayout.Label("Press Auto Setup to import, generate materials, and add the model.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "model":
                    if (GUILayout.Button("Focus Model", GUILayout.Height(24f)))
                    {
                        HoyoToonGuidedTourController.FocusModelInScene();
                    }
                    break;
                case "lighting_select":
                    GUILayout.Label("Click Lighting in the Modules bar to continue.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "scriptables_select":
                    GUILayout.Label("Click Scriptables in the Modules bar to continue.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "postprocessing_select":
                    GUILayout.Label("Click Post Processing in the Modules bar to continue.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "renders_select":
                    GUILayout.Label("Click Renders in the Modules bar to continue.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "footer":
                    GUILayout.Label("Review the footer actions below the modules.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "prefab":
                    GUILayout.Label("Use Create Prefab in the footer to save the Acheron model.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "finish":
                    GUILayout.Label("Congrats! Your prefab is saved and you are ready to keep exploring.", EditorStyles.centeredGreyMiniLabel);
                    break;
            }
        }

        private void HandleButton(int index)
        {
            switch (index)
            {
                case 0:
                    HoyoToonGuidedTourController.StopTour();
                    Close();
                    break;
                case 1:
                    HoyoToonGuidedTourController.RestartTour();
                    break;
                default:
                    Close();
                    break;
            }
        }

        private void EnsureLocalStyles()
        {
            if (_sectionHeaderStyle == null)
            {
                _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }
        }
    }
}
#endif
