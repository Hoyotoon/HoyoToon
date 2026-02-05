#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Components
{
    internal sealed class HoyoToonManagerFooter
    {
        private readonly string _title;
        private readonly string _message;
        private readonly Action _renderBody;
        private readonly Func<GameObject> _activeModelProvider;
        private readonly Func<GameObject, string> _prefabFolderResolver;
        private readonly Action<GameObject> _createPrefabAction;
        private readonly Action<GameObject> _regenerateMaterialsAction;

        public HoyoToonManagerFooter(
            string title = "Global Controls",
            string message = null,
            Action renderBody = null,
            Func<GameObject> activeModelProvider = null,
            Func<GameObject, string> prefabFolderResolver = null,
            Action<GameObject> createPrefabAction = null,
            Action<GameObject> regenerateMaterialsAction = null)
        {
            _title = string.IsNullOrEmpty(title) ? "Global Controls" : title;
            _message = message;
            _renderBody = renderBody;
            _activeModelProvider = activeModelProvider;
            _prefabFolderResolver = prefabFolderResolver;
            _createPrefabAction = createPrefabAction;
            _regenerateMaterialsAction = regenerateMaterialsAction;
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
                else if (_createPrefabAction == null)
                {
                    EditorGUILayout.HelpBox("Future global controls will appear here.", MessageType.Info);
                }

                DrawFooterCalloutIfNeeded();
                DrawActionRow();
            }
        }

        private void DrawActionRow()
        {
            if (_activeModelProvider == null)
            {
                return;
            }

            var activeModel = _activeModelProvider.Invoke();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPrefabButton(activeModel);
                DrawRegenerateButton(activeModel);

            }
        }

        private void DrawRegenerateButton(GameObject activeModel)
        {
            if (_regenerateMaterialsAction == null)
            {
                return;
            }

            bool isValid = activeModel != null;
            using (new EditorGUI.DisabledScope(!isValid))
            {
                var content = new GUIContent(
                    "Regenerate Materials",
                    isValid ? "Rebuild materials using detected JSON sources." : "No model selected");

                if (GUILayout.Button(content, GUILayout.Width(160f)))
                {
                    _regenerateMaterialsAction.Invoke(activeModel);
                }
            }
        }

        private void DrawPrefabButton(GameObject activeModel)
        {
            if (_createPrefabAction == null)
            {
                return;
            }

            string folder = _prefabFolderResolver?.Invoke(activeModel);
            bool hasFolder = !string.IsNullOrEmpty(folder);
            bool isValid = activeModel != null && hasFolder;

            using (new EditorGUI.DisabledScope(!isValid))
            {
                var content = new GUIContent(
                    "Create Prefab",
                    isValid ? $"Create a prefab next to:\n{folder}" :
                    activeModel == null ? "No model selected" : "Prefab source folder could not be located.");

                DrawPrefabCalloutIfNeeded();
                if (GUILayout.Button(content, GUILayout.Width(140f)))
                {
                    _createPrefabAction.Invoke(activeModel);
                }
                var buttonRect = GUILayoutUtility.GetLastRect();
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.footer", buttonRect, "Create Prefab", onClick: () =>
                {
                    if (HoyoToonGuidedTourController.CurrentStep.id != "footer")
                    {
                        return;
                    }

                    HoyoToonGuidedTourController.CompleteFooterStep();
                    HoyoToonGuidedTourController.Advance();
                });
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.footer.prefab", buttonRect, "Create Prefab");
                DrawPrefabCalloutIfNeeded();
            }
        }

        private void DrawFooterCalloutIfNeeded()
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (step.id == "footer")
            {
                HoyoToonTourCallout.Draw(
                    $"Guided Tour: {step.title}",
                    "Click Create Prefab to continue.\n\nFooter actions apply to the active model.",
                    null,
                    null);
                return;
            }

            if (step.id == "finish")
            {
                var rect = HoyoToonTourCallout.DrawWithRect(
                    $"Guided Tour: {step.title}",
                    step.instruction,
                    null,
                    null);
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.finish.callout", rect, "Finish", onClick: HoyoToonGuidedTourController.StopTour);
            }
        }

        private void DrawPrefabCalloutIfNeeded()
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (step.id == "prefab")
            {
                HoyoToonTourCallout.Draw(
                    $"Guided Tour: {step.title}",
                    "Click Create Prefab to save the Acheron model next to its assets.\n\nPrefabs let you reuse this setup later.",
                    null,
                    null);
            }
        }
    }
}
#endif // UNITY_EDITOR
