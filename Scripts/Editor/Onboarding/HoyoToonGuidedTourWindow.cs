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

        [MenuItem(ResetStepMenuPrefix + "1 - Resources", priority = 3)]
        public static void ResetToResources() => ResetToStep("resources");

        [MenuItem(ResetStepMenuPrefix + "2 - Scene", priority = 4)]
        public static void ResetToScene() => ResetToStep("scene");

        [MenuItem(ResetStepMenuPrefix + "3 - Manager", priority = 5)]
        public static void ResetToManager() => ResetToStep("manager");

        [MenuItem(ResetStepMenuPrefix + "4 - Models Module", priority = 6)]
        public static void ResetToModelsModule() => ResetToStep("modules");

        [MenuItem(ResetStepMenuPrefix + "5 - Game", priority = 7)]
        public static void ResetToGame() => ResetToStep("game");

        [MenuItem(ResetStepMenuPrefix + "6 - Character", priority = 8)]
        public static void ResetToCharacter() => ResetToStep("character");

        [MenuItem(ResetStepMenuPrefix + "7 - Variant", priority = 9)]
        public static void ResetToVariant() => ResetToStep("variant");

        [MenuItem(ResetStepMenuPrefix + "8 - FBX", priority = 10)]
        public static void ResetToFbx() => ResetToStep("fbx");

        [MenuItem(ResetStepMenuPrefix + "9 - Download", priority = 11)]
        public static void ResetToDownload() => ResetToStep("download");

        [MenuItem(ResetStepMenuPrefix + "10 - Main Module", priority = 12)]
        public static void ResetToMainModule() => ResetToStep("mainmodule");

        [MenuItem(ResetStepMenuPrefix + "11 - Add Model", priority = 13)]
        public static void ResetToAddModel() => ResetToStep("addmodel");

        [MenuItem(ResetStepMenuPrefix + "12 - Auto Setup", priority = 14)]
        public static void ResetToAutoSetup() => ResetToStep("autosetup");

        [MenuItem(ResetStepMenuPrefix + "13 - Model In Scene", priority = 15)]
        public static void ResetToModel() => ResetToStep("model");

        [MenuItem(ResetStepMenuPrefix + "14 - Lighting", priority = 16)]
        public static void ResetToLighting() => ResetToStep("lighting");

        [MenuItem(ResetStepMenuPrefix + "15 - Scriptables", priority = 17)]
        public static void ResetToScriptables() => ResetToStep("scriptables");

        [MenuItem(ResetStepMenuPrefix + "16 - Post Processing", priority = 18)]
        public static void ResetToPostProcessing() => ResetToStep("postprocessing");

        [MenuItem(ResetStepMenuPrefix + "17 - Renders", priority = 19)]
        public static void ResetToRenders() => ResetToStep("renders");

        [MenuItem(ResetStepMenuPrefix + "18 - Finish", priority = 20)]
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
            total += _sectionHeaderStyle.CalcHeight(new GUIContent("Current Step"), contentWidth);
            total += EditorGUIUtility.singleLineHeight * 6f;
            total += 90f;
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
            GUILayout.Label("Current Step", _sectionHeaderStyle);
            GUILayout.Space(2f);
            GUILayout.Label($"Step {HoyoToonGuidedTourController.CurrentStepIndex + 1} of {HoyoToonGuidedTourController.StepCount}", EditorStyles.miniLabel);
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

            GUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(HoyoToonGuidedTourController.CurrentStepIndex == 0))
                {
                    if (GUILayout.Button("Back", GUILayout.Width(90f)))
                    {
                        HoyoToonGuidedTourController.GoBack();
                    }
                }

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!HoyoToonGuidedTourController.CanAdvance()))
                {
                    if (GUILayout.Button("Next", GUILayout.Width(90f)))
                    {
                        HoyoToonGuidedTourController.Advance();
                    }
                }
            }
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
                case "lighting":
                    GUILayout.Label("Go to Lighting and set the key light for a clean silhouette.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "scriptables":
                    GUILayout.Label("Open Scriptables to review game lighting settings.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "postprocessing":
                    GUILayout.Label("Pick a post-processing profile that matches the game style.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "renders":
                    GUILayout.Label("Take a quick render to verify the final look.", EditorStyles.centeredGreyMiniLabel);
                    break;
                case "finish":
                    GUILayout.Label("Tour complete. You can keep exploring the modules.", EditorStyles.centeredGreyMiniLabel);
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
