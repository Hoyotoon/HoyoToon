#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using NUnit.Framework;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class CharacterModule : DynamicControllerModule
    {
        private static readonly string[] SectionOrder = { "General", "Lighting", "Shadows", "Local Lighting", "Special Effects", "Rendering"};

        public override string DisplayName => "Character";

        protected override string ControllerTypeSuffix => "CharacterController";

        protected override string ControllerInterfaceSuffix => "ICharacterController";

        protected override string PreferredRuntimeNamespace => "Runtime.Character";

        protected override string EmptyPropertiesMessage => "No editable character properties were found.";

        protected override IReadOnlyList<string> PreferredSectionOrder => SectionOrder;

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Assign a HoyoToon Manager to edit character settings.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var resolvedController = ResolveController(targetManager);
                if (resolvedController == null)
                {
                    EditorGUILayout.HelpBox("No compatible character controller was found for this manager.", MessageType.Info);
                    return;
                }

                EnsureSerializedObject(resolvedController);
                if (ControllerSerializedObject == null)
                {
                    EditorGUILayout.HelpBox("Character Controller settings are unavailable.", MessageType.Warning);
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
            if (manager != null && manager.ActiveModel != null)
            {
                var activeModelController = FindBestControllerInHierarchy(manager.ActiveModel.transform);
                if (activeModelController != null)
                {
                    return activeModelController;
                }
            }

            if (manager != null)
            {
                return FindBestControllerInHierarchy(manager.transform);
            }

            return null;
        }

        protected override string ResolveGroupName(SerializedProperty property)
        {
            string name = property.name ?? string.Empty;
            string displayName = property.displayName ?? string.Empty;
            if (ContainsAny(name, displayName, "EffectMaterials"))
            {
                return "Special Effects";
            }
            if (ContainsAny(name, displayName, "Stencil", "Renderer", "Render"))
            {
                return "Rendering";
            }
            if(ContainsAny(name, displayName, "Shadow"))
            {
                return "Shadows";
            }

            if (ContainsAny(name, displayName, "Override", "NewLocal", "Disable", "EnableCustom"))
            {
                return "Local Lighting";
            }

            if (ContainsAny(name, displayName, "CharacterLight", "_NewLocalLightStrength","SceneLight", "Head", "Light", "Color"))
            {
                return "Lighting";
            }

            return "General";
        }

        protected override bool IsDefaultSectionExpanded(string sectionName)
        {
            return string.Equals(sectionName, "Lighting", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionName, "General", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
