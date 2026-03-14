#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    internal static class SetupSceneHelper
    {
        private const int GridColumns = 5;
        private const float GridSpacingX = 1f;
        private const float GridSpacingZ = -1f;

        public static bool ShouldInstantiateModel(ModelSetupUtility.SetupContext context)
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
                DialogWindow.ShowError("Instantiation Failed", "Unity could not instantiate the selected asset. See console for details.");
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
            int slot = index % columns;
            int columnOffset = slot == 0
                ? 0
                : (slot % 2 == 1 ? -((slot + 1) / 2) : (slot / 2));

            var origin = manager.transform.position;
            var offset = new Vector3(columnOffset * GridSpacingX, 0f, row * GridSpacingZ);
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

            var managedModels = manager.ManagedModels;
            int managedModelCount = managedModels != null ? managedModels.Count : 0;
            if (managedModelCount > 0)
            {
                for (int i = 0; i < managedModelCount; i++)
                {
                    var model = managedModels[i];
                    if (!model || model == exclude)
                    {
                        continue;
                    }

                    roots.Add(model.GetInstanceID());
                }
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                if (!RendererUtils.RendererUsesHoyoToon(renderer))
                {
                    continue;
                }

                var root = RendererUtils.ResolveRendererRoot(renderer, manager);
                if (root == null || root == exclude)
                {
                    continue;
                }

                roots.Add(root.GetInstanceID());
            }

            return roots.Count;
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
