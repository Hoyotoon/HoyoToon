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
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _messageLabelStyle;

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
            EnsureLocalStyles();
            float total = 0f;
            total += _sectionHeaderStyle.CalcHeight(new GUIContent("Guided Tour"), contentWidth);
            total += _messageLabelStyle.CalcHeight(new GUIContent(" "), contentWidth) * 6f;
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

            if (_messageLabelStyle == null)
            {
                _messageLabelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11
                };
            }
        }
    }
}
#endif
