#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class SceneModule : DynamicControllerModule
    {
        private static readonly string[] SectionOrder =
        {
            "Main Light",
            "Environment",
            "Character Lighting",
            "Rim Settings",
            "Height Lerp",
            "Level Lighting",
            "Fog Settings",
            "Effects",
            "Local Lights",
            "General"
        };

        public override string DisplayName => "Scene";

        protected override string ControllerTypeSuffix => "SceneController";

        protected override string ControllerInterfaceSuffix => "ISceneController";

        protected override string PreferredRuntimeNamespace => "Runtime.Scene";

        protected override string EmptyPropertiesMessage => "No editable scene properties were found.";

        protected override IReadOnlyList<string> PreferredSectionOrder => SectionOrder;

        internal override string NavbarTourTarget => "tour.modules.scene";

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Assign a HoyoToon Manager to edit scene settings.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var resolvedController = ResolveController(targetManager);
                if (resolvedController == null)
                {
                    EditorGUILayout.HelpBox("No compatible scene controller found.", MessageType.Info);
                    if (GUILayout.Button("Create Scene Controller"))
                    {
                        var controllerType = SceneControllerTypeResolver.ResolveHSRSceneControllerType();
                        if (controllerType != null)
                        {
                            var go = new GameObject("HSR Scene Controller");
                            Undo.RegisterCreatedObjectUndo(go, "Create Scene Controller");
                            go.AddComponent(controllerType);
                        }
                    }
                    return;
                }

                EnsureSerializedObject(resolvedController);
                if (ControllerSerializedObject == null)
                {
                    EditorGUILayout.HelpBox("Scene Controller settings are unavailable.", MessageType.Warning);
                    return;
                }

                ControllerSerializedObject.Update();
                DrawDynamicPropertyGroups();
                if (ControllerSerializedObject.ApplyModifiedProperties())
                {
                    TrySyncController(resolvedController);
                }
            }
        }

        private MonoBehaviour ResolveController(HoyoToonManager manager)
        {
            if (manager != null)
            {
                var hierarchyController = FindBestControllerInHierarchy(manager.transform);
                if (hierarchyController != null)
                {
                    return hierarchyController;
                }
            }

            var allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            MonoBehaviour best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < allBehaviours.Length; i++)
            {
                var candidate = allBehaviours[i];
                if (!IsCompatibleController(candidate))
                {
                    continue;
                }

                int score = ScoreController(candidate.GetType());
                if (best == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        protected override string ResolveGroupName(SerializedProperty property)
        {
            string name = property.name ?? string.Empty;
            string displayName = property.displayName ?? string.Empty;

            if (ContainsAny(name, displayName, "LocalMainLight", "DisableCharacterLocal"))
            {
                return "Local Lights";
            }

            if (ContainsAny(name, displayName, "MainLight", "Shadow", "Indoor"))
            {
                return "Main Light";
            }

            if (ContainsAny(name, displayName, "Rotation", "GlobalRot", "Global"))
            {
                return "Environment";
            }

            if (ContainsAny(name, displayName, "CharacterToon", "CharacterDisable", "AddColor", "SPColor", "SPIntensity", "OutLine", "Outline"))
            {
                return "Character Lighting";
            }

            if (ContainsAny(name, displayName, "Rim", "CharacterShadowFactor"))
            {
                return "Rim Settings";
            }

            if (ContainsAny(name, displayName, "HeightLerp"))
            {
                return "Height Lerp";
            }

            if (ContainsAny(name, displayName, "Level", "IndoorChar"))
            {
                return "Level Lighting";
            }

            if (ContainsAny(name, displayName, "Fog"))
            {
                return "Fog Settings";
            }

            if (ContainsAny(name, displayName, "Effect", "Eff"))
            {
                return "Effects";
            }

            return "General";
        }

        protected override bool IsDefaultSectionExpanded(string sectionName)
        {
            return string.Equals(sectionName, "Main Light", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, "General", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
