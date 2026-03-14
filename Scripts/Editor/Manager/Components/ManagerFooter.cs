#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector.Components
{
    internal sealed class ManagerFooter
    {
        internal sealed class ActionContext
        {
            internal readonly Func<GameObject> ActiveModelProvider;
            internal readonly Func<GameObject, string> PrefabFolderResolver;
            internal readonly Action<GameObject> CreatePrefabAction;
            internal readonly Action<GameObject> RegenerateMaterialsAction;

            internal ActionContext(
                Func<GameObject> activeModelProvider = null,
                Func<GameObject, string> prefabFolderResolver = null,
                Action<GameObject> createPrefabAction = null,
                Action<GameObject> regenerateMaterialsAction = null)
            {
                ActiveModelProvider = activeModelProvider;
                PrefabFolderResolver = prefabFolderResolver;
                CreatePrefabAction = createPrefabAction;
                RegenerateMaterialsAction = regenerateMaterialsAction;
            }
        }

        private static readonly GUIContent s_RegenerateMaterialsContent = new GUIContent("Regenerate Materials");
        private static readonly GUIContent s_CreatePrefabContent = new GUIContent("Create Prefab");
        private const string NoModelSelectedTooltip = "No model selected";
        private const string RegenerateTooltip = "Rebuild materials using detected JSON sources.";
        private const string MissingPrefabFolderTooltip = "Prefab source folder could not be located.";

        private readonly string _title;
        private readonly string _message;
        private readonly Action _renderBody;
        private readonly ActionContext _actions;

        public ManagerFooter(
            string title = "Global Controls",
            string message = null,
            Action renderBody = null,
            ActionContext actions = null)
        {
            _title = string.IsNullOrEmpty(title) ? "Global Controls" : title;
            _message = message;
            _renderBody = renderBody;
            _actions = actions;
        }

        public void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
                if (_renderBody != null)
                {
                    _renderBody.Invoke();
                }
                else if (!string.IsNullOrEmpty(_message))
                {
                    EditorGUILayout.HelpBox(_message, MessageType.Info);
                }
                else if (_actions == null || _actions.CreatePrefabAction == null)
                {
                    EditorGUILayout.HelpBox("Future global controls will appear here.", MessageType.Info);
                }

                DrawFooterCalloutIfNeeded();
                DrawActionRow();
            }
        }

        private void DrawActionRow()
        {
            if (_actions == null || _actions.ActiveModelProvider == null)
            {
                return;
            }

            var activeModel = _actions.ActiveModelProvider.Invoke();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPrefabButton(activeModel);
                DrawRegenerateButton(activeModel);

            }
        }

        private void DrawRegenerateButton(GameObject activeModel)
        {
            if (_actions == null || _actions.RegenerateMaterialsAction == null)
            {
                return;
            }

            bool isValid = activeModel != null;
            using (new EditorGUI.DisabledScope(!isValid))
            {
                s_RegenerateMaterialsContent.tooltip = isValid ? RegenerateTooltip : NoModelSelectedTooltip;

                if (GUILayout.Button(s_RegenerateMaterialsContent, GUILayout.Width(160f)))
                {
                    _actions.RegenerateMaterialsAction.Invoke(activeModel);
                }
            }
        }

        private void DrawPrefabButton(GameObject activeModel)
        {
            if (_actions == null || _actions.CreatePrefabAction == null)
            {
                return;
            }

            string folder = _actions.PrefabFolderResolver?.Invoke(activeModel);
            bool hasFolder = !string.IsNullOrEmpty(folder);
            bool isValid = activeModel != null && hasFolder;

            using (new EditorGUI.DisabledScope(!isValid))
            {
                s_CreatePrefabContent.tooltip = isValid ? $"Create a prefab next to:\n{folder}" :
                    activeModel == null ? NoModelSelectedTooltip : MissingPrefabFolderTooltip;

                DrawPrefabCalloutIfNeeded();
                if (GUILayout.Button(s_CreatePrefabContent, GUILayout.Width(140f)))
                {
                    _actions.CreatePrefabAction.Invoke(activeModel);
                }
                var buttonRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.footer", buttonRect, "Create Prefab", onClick: () =>
                {
                    if (GuidedTourController.CurrentStep.id != StepIds.Footer)
                    {
                        return;
                    }

                    GuidedTourController.CompleteFooterStep();
                    GuidedTourController.Advance();
                });
                TourOverlay.DrawHighlightIfActive("tour.footer.prefab", buttonRect, "Create Prefab");
            }
        }

        private void DrawFooterCalloutIfNeeded()
        {
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (step.id == StepIds.Footer)
            {
                TourCallout.Draw(
                    $"Guided Tour: {step.title}",
                    "Click Create Prefab to continue.\n\nFooter actions apply to the active model.",
                    null,
                    null);
                return;
            }

            if (step.id == StepIds.Finish)
            {
                var rect = TourCallout.DrawWithRect(
                    $"Guided Tour: {step.title}",
                    step.instruction,
                    null,
                    null);
                TourOverlay.DrawHighlightIfActive("tour.finish.callout", rect, "Finish", onClick: GuidedTourController.StopTour);
            }
        }

        private void DrawPrefabCalloutIfNeeded()
        {
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (step.id == StepIds.Prefab)
            {
                TourCallout.Draw(
                    $"Guided Tour: {step.title}",
                    "Click Create Prefab to save the Acheron model next to its assets.\n\nPrefabs let you reuse this setup later.",
                    null,
                    null);
            }
        }
    }
}
#endif
