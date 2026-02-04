#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonSetupSceneHelper
    {
        private const int GridColumns = 4;
        private const float GridSpacingX = 1f;
        private const float GridSpacingZ = 1f;

        public static bool ShouldInstantiateModel(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (context.ExistingInstance != null)
            {
                return false;
            }

            return context.Options == null || context.Options.InstantiateInScene;
        }

        public static GameObject InstantiateAssetInScene(HoyoToonManager manager, GameObject asset, string undoLabel)
        {
            if (manager == null || asset == null)
            {
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(asset, manager.gameObject.scene) as GameObject;
            if (instance == null)
            {
                HoyoToonDialogWindow.ShowError("Instantiation Failed", "Unity could not instantiate the selected asset. See console for details.");
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, undoLabel);
            PlaceInstanceOnGrid(manager, instance);
            Selection.activeObject = instance;
            EditorGUIUtility.PingObject(instance);
            return instance;
        }

        private static void PlaceInstanceOnGrid(HoyoToonManager manager, GameObject instance)
        {
            if (manager == null || instance == null)
            {
                return;
            }

            int index = Mathf.Max(0, CountExistingModels(manager, instance));
            int columns = Mathf.Max(1, GridColumns);
            int row = index / columns;
            int column = index % columns;

            var origin = manager.transform.position;
            var offset = new Vector3(column * GridSpacingX, 0f, row * GridSpacingZ);
            Undo.RecordObject(instance.transform, "Place Model On Grid");
            instance.transform.position = origin + offset;
        }

        private static int CountExistingModels(HoyoToonManager manager, GameObject exclude)
        {
            if (manager == null)
            {
                return 0;
            }

            var scene = manager.gameObject.scene;
            var roots = new System.Collections.Generic.HashSet<int>();

            var managed = manager.ManagedModels;
            if (managed != null)
            {
                foreach (var model in managed)
                {
                    if (!model || model == exclude)
                    {
                        continue;
                    }

                    roots.Add(model.GetInstanceID());
                }
            }

            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                if (!RendererUsesHoyoToon(renderer))
                {
                    continue;
                }

                var root = ResolveRendererRoot(renderer, manager);
                if (root == null || root == exclude)
                {
                    continue;
                }

                roots.Add(root.GetInstanceID());
            }

            return roots.Count;
        }

        private static GameObject ResolveRendererRoot(Renderer renderer, HoyoToonManager manager)
        {
            if (renderer == null)
            {
                return null;
            }

            var managerTransform = manager != null ? manager.transform : null;
            var current = renderer.transform;

            while (current != null && current.parent != null && current.parent != managerTransform)
            {
                current = current.parent;
            }

            if (current == null || current == managerTransform)
            {
                return null;
            }

            return current.gameObject;
        }

        private static bool RendererUsesHoyoToon(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var materials = renderer.sharedMaterials;
            foreach (var material in materials)
            {
                if (!material || material.shader == null)
                {
                    continue;
                }

                if (material.shader.name.IndexOf("HoyoToon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static GameObject FindExistingSceneInstance(HoyoToonManager manager, GameObject asset, string assetPath)
        {
            if (manager == null || asset == null)
            {
                return null;
            }

            var managed = manager.ManagedModels;
            if (managed != null)
            {
                foreach (var model in managed)
                {
                    if (!model) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(model) == asset)
                    {
                        return model;
                    }

                    var modelPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model);
                    if (!string.IsNullOrEmpty(modelPath) && string.Equals(modelPath, assetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return model;
                    }
                }
            }

            return null;
        }
    }
}
#endif
