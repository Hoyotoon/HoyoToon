using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI.Manager
{
    public static class SelectionResolver
    {
        public static ModuleContext Resolve(EditorWindow window)
        {
            Object[] selectedObjects = Selection.objects == null
                ? Array.Empty<Object>()
                : Selection.objects.Where(selectedObject => selectedObject != null).ToArray();

            Object activeObject = Selection.activeObject;
            GameObject selectedGameObject = null;
            Component targetComponent = null;

            if (activeObject is Component selectedComponent)
            {
                selectedGameObject = selectedComponent.gameObject;
                targetComponent = selectedComponent;
            }
            else if (activeObject is GameObject selectedGo)
            {
                selectedGameObject = selectedGo;
                targetComponent = ResolvePrimaryComponent(selectedGo);
            }

            GameObject targetRoot = ResolveTargetRoot(selectedGameObject);

            return new ModuleContext
            {
                Window = window,
                ActiveObject = activeObject,
                SelectedObjects = selectedObjects,
                SelectedGameObject = selectedGameObject,
                TargetRoot = targetRoot,
                TargetComponent = targetComponent,
                SerializedObject = targetComponent != null ? new SerializedObject(targetComponent) : null
            };
        }

        private static GameObject ResolveTargetRoot(GameObject selectedGameObject)
        {
            if (selectedGameObject == null)
            {
                return null;
            }

            GameObject prefabInstanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selectedGameObject);
            return prefabInstanceRoot != null ? prefabInstanceRoot : selectedGameObject;
        }

        private static Component ResolvePrimaryComponent(GameObject selectedGameObject)
        {
            if (selectedGameObject == null)
            {
                return null;
            }

            foreach (Component component in selectedGameObject.GetComponents<Component>())
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                return component;
            }

            return null;
        }
    }
}
