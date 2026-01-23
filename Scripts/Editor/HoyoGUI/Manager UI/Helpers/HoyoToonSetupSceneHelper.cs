#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonSetupSceneHelper
    {
        public static bool ShouldInstantiateModel(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null)
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
            Selection.activeObject = instance;
            EditorGUIUtility.PingObject(instance);
            return instance;
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
