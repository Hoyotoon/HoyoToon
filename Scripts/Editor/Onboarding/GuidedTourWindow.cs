#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;
using HoyoToon;

namespace HoyoToon.Editor.Onboarding
{
    internal sealed class GuidedTourWindow : BaseHoyoToonWindow
    {
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _tourMessageStyle;

        public static void ShowWindow()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var window = CreateInstance<GuidedTourWindow>();
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

            var window = Resources.FindObjectsOfTypeAll<GuidedTourWindow>().FirstOrDefault();
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
            GuidedTourController.OnStepChanged += Repaint;

            if (!GuidedTourController.IsActive)
            {
                GuidedTourController.StartTour();
            }
        }

        private void OnDisable()
        {
            GuidedTourController.OnStepChanged -= Repaint;
        }

        protected override float MeasureBodyContentHeight(float contentWidth)
        {
            EnsureLocalStyles();
            float total = 0f;
            total += _sectionHeaderStyle.CalcHeight(new GUIContent("Guided Tour"), contentWidth);
            total += _tourMessageStyle.CalcHeight(new GUIContent(" "), contentWidth) * 6f;
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
            var step = GuidedTourController.CurrentStep;
            GUILayout.Label("Guided Tour", _sectionHeaderStyle);
            GUILayout.Label(step.title, _tourMessageStyle);
            GUILayout.Space(2f);
            GUILayout.Label(step.instruction, _tourMessageStyle);
            GUILayout.Space(6f);

            DrawStepActions(step.id);
        }

        private void DrawStepActions(string stepId)
        {
            switch (stepId)
            {
                case GuidedTourController.StepIds.Resources:
                    GUILayout.Label("Download missing resources so materials and lighting work correctly.", EditorStyles.centeredGreyMiniLabel);
                    if (GUILayout.Button("Open Resource Status", GUILayout.Height(24f)))
                    {
                        EditorApplication.ExecuteMenuItem("HoyoToon/Resources/Check Resource Status");
                    }
                    break;
                case GuidedTourController.StepIds.Prerequisites:
                    var prereqStatus = GuidedTourController.GetPrerequisitesStatus();
                    if (prereqStatus.AllPassed)
                    {
                        GUILayout.Label("All prerequisite checks are passing.", EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        GUILayout.Label(
                            $"{prereqStatus.FailedChecks}/{prereqStatus.TotalChecks} checks need attention ({prereqStatus.ErrorCount} errors, {prereqStatus.WarningCount} warnings).",
                            EditorStyles.centeredGreyMiniLabel);
                    }

                    if (GUILayout.Button("Run Prerequisite Checks & Auto-Fix", GUILayout.Height(24f)))
                    {
                        GuidedTourController.RunPrerequisitesAutoFix();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Open Player Settings", GUILayout.Height(22f)))
                        {
                            SettingsService.OpenProjectSettings("Project/Player");
                        }

                        if (GUILayout.Button("Open Quality Settings", GUILayout.Height(22f)))
                        {
                            SettingsService.OpenProjectSettings("Project/Quality");
                        }
                    }
                    break;
                case GuidedTourController.StepIds.FirstTime:
                    GUILayout.Label("Is this your first time using HoyoToon?", EditorStyles.centeredGreyMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Yes, guide me", GUILayout.Height(24f)))
                        {
                            GuidedTourController.ConfirmFirstTime(true);
                        }
                        if (GUILayout.Button("No, skip tour", GUILayout.Height(24f)))
                        {
                            GuidedTourController.ConfirmFirstTime(false);
                        }
                    }
                    break;
                case GuidedTourController.StepIds.Scene:
                    EditorGUILayout.HelpBox(
                        "The guided tour runs in the packaged HoyoToon scene so the setup, lighting, and manager flow stay predictable.",
                        MessageType.Info);

                    if (GUILayout.Button("Open HoyoToon Scene", GUILayout.Height(28f)))
                    {
                        GuidedTourController.TryOpenHoyoToonScene();
                    }
                    break;
                case GuidedTourController.StepIds.Manager:
                    bool hasManager = GuidedTourController.HasManagerInScene();
                    if (hasManager)
                    {
                        EditorGUILayout.HelpBox(
                            "We found a HoyoToon Manager in this scene. Press Let's Start to switch the Inspector to it and continue the guided tour inside the manager.",
                            MessageType.Info);

                        if (GUILayout.Button("Let's Start", GUILayout.Height(24f)))
                        {
                            GuidedTourController.FocusManagerInScene();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            "No HoyoToon Manager was found in this scene. Create one below, and the tour will open it in the Inspector so we can continue.",
                            MessageType.Warning);

                        if (GUILayout.Button("Create Manager", GUILayout.Height(24f)))
                        {
                            GuidedTourController.CreateAndFocusManagerInScene();
                        }
                    }
                    break;
            }
        }

        private void HandleButton(int index)
        {
            switch (index)
            {
                case 0:
                    GuidedTourController.StopTour();
                    Close();
                    break;
                case 1:
                    GuidedTourController.RestartTour();
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

            if (_tourMessageStyle == null)
            {
                _tourMessageStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11
                };
            }
        }
    }
}
#endif
