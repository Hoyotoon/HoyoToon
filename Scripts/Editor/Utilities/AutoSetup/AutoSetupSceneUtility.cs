#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Rendering.Character;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Scene.HSR;
using HoyoToon.Runtime.Scene.Placement;
using HoyoToon.Runtime.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Editor.Utilities.AutoSetup
{
    internal static class AutoSetupSceneUtility
    {
        private const string HsrSceneControllerObjectName = "HSR Scene Controller";

        public static GameObject InstantiateModel(string modelAssetPath)
        {
            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                return null;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            return InstantiateModel(modelAsset);
        }

        public static GameObject InstantiateModel(GameObject modelAsset)
        {
            if (modelAsset == null)
            {
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = modelAsset.name;
            Undo.RegisterCreatedObjectUndo(instance, "HoyoToon Auto Setup Instantiate Model");
            return instance;
        }

        public static Component[] EnsureComponents(GameObject target, params Type[] componentTypes)
        {
            if (target == null || componentTypes == null || componentTypes.Length == 0)
            {
                return Array.Empty<Component>();
            }

            var resolvedComponents = new List<Component>(componentTypes.Length);
            for (int i = 0; i < componentTypes.Length; i++)
            {
                Type componentType = componentTypes[i];
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                {
                    continue;
                }

                Component component = EnsureComponent(target, componentType);
                if (component != null)
                {
                    resolvedComponents.Add(component);
                }
            }

            return resolvedComponents.ToArray();
        }

        public static T EnsureComponent<T>(GameObject target)
            where T : Component
        {
            return target != null ? target.GetComponent<T>() ?? Undo.AddComponent<T>(target) : null;
        }

        public static Component EnsureComponent(GameObject target, Type componentType)
        {
            if (target == null || componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                return null;
            }

            return target.GetComponent(componentType) ?? Undo.AddComponent(target, componentType);
        }

        public static T EnsureSceneComponent<T>(string objectName)
            where T : Component
        {
            return EnsureSceneComponent<T>(EditorSceneManager.GetActiveScene(), objectName);
        }

        public static T EnsureSceneComponent<T>(UnityScene scene, string objectName)
            where T : Component
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
            {
                return null;
            }

            T existingComponent = FindSceneComponent<T>(scene);
            if (existingComponent != null)
            {
                return existingComponent;
            }

            GameObject target = FindRootObjectInScene(scene, objectName);
            if (target == null)
            {
                target = new GameObject(objectName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, scene);
                Undo.RegisterCreatedObjectUndo(target, $"HoyoToon Auto Setup Add {typeof(T).Name}");
            }

            return EnsureComponent<T>(target);
        }

        public static void InstantiateModels(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null)
            {
                return;
            }

            IReadOnlyList<string> targetModelAssetPaths = CollectTargetModelAssetPaths(context);
            for (int i = 0; i < targetModelAssetPaths.Count; i++)
            {
                string modelAssetPath = targetModelAssetPaths[i];
                GameObject instance = FindExistingSetupModel(modelAssetPath) ?? InstantiateModel(modelAssetPath);
                if (instance == null)
                {
                    result.RecordWarning($"Auto setup failed to instantiate '{modelAssetPath}'.");
                    continue;
                }

                context.RegisterInstantiatedModel(instance);
            }
        }

        public static void AddHsrComponents(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.InstantiatedModels.Count <= 0)
            {
                return;
            }

            var preparedSceneHandles = new HashSet<int>();
            for (int i = 0; i < context.InstantiatedModels.Count; i++)
            {
                GameObject instantiatedModel = context.InstantiatedModels[i];
                if (instantiatedModel == null)
                {
                    continue;
                }

                UnityScene modelScene = instantiatedModel.scene;
                if (modelScene.IsValid() && modelScene.isLoaded && preparedSceneHandles.Add(modelScene.handle))
                {
                    EnsureSceneComponent<HSRSceneController>(modelScene, HsrSceneControllerObjectName);
                }

                Component[] components = EnsureComponents(
                    instantiatedModel,
                    typeof(HSRCharacterController),
                    typeof(HoyoToonPlanarReflectionParticipant));
                if (components.Length <= 0 || components[0] is not HSRCharacterController characterController)
                {
                    result.RecordWarning($"Auto setup failed to add an HSRCharacterController to '{instantiatedModel.name}'.");
                    continue;
                }

                if (instantiatedModel.GetComponent<HoyoToonPlanarReflectionParticipant>() is HoyoToonPlanarReflectionParticipant reflectionParticipant)
                    reflectionParticipant.RefreshRenderers();

                HSRCharacterEditorService.TryAssignDefaultComputeShader(characterController);
                characterController.SyncToRenderer();
            }
        }

        public static void UpdatePlacement(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || context.InstantiatedModels.Count <= 0)
            {
                return;
            }

            UpdatePlacementForModels(context.InstantiatedModels);
        }

        public static void UpdatePlacementForModels(IEnumerable<GameObject> models)
        {
            if (models == null)
            {
                return;
            }

            foreach (GameObject model in models)
            {
                UpdatePlacementForModel(model);
            }
        }

        public static void UpdatePlacementForModel(GameObject model)
        {
            if (model == null || !model.scene.IsValid())
            {
                return;
            }

            CharacterPlacementController placementController = CharacterPlacementController.FindForScene(model.scene);
            if (placementController == null)
            {
                return;
            }

            Undo.RecordObject(placementController, "HoyoToon Auto Setup Update Placement");

            if (placementController.PlacementMode == ManagerPlacementMode.Single)
            {
                placementController.SetFocusedModel(model);
            }
            else
            {
                placementController.RegisterModel(model);
            }

            placementController.ApplyNow();
            EditorUtility.SetDirty(placementController);
            EditorSceneManager.MarkSceneDirty(placementController.gameObject.scene);
        }

        private static IReadOnlyList<string> CollectTargetModelAssetPaths(AutoSetupContext context)
        {
            IEnumerable<string> sourcePaths = context.ConvertedModelAssetPaths.Count > 0
                ? context.ConvertedModelAssetPaths
                : context.ModelAssetPaths;

            return sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(EditorPathUtility.NormalizeAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static GameObject FindExistingSetupModel(string modelAssetPath)
        {
            string normalizedAssetPath = NormalizeAssetPath(modelAssetPath);
            if (string.IsNullOrWhiteSpace(normalizedAssetPath))
            {
                return null;
            }

            UnityScene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return null;
            }

            CharacterPlacementController placementController = CharacterPlacementController.FindForScene(activeScene);
            if (placementController != null)
            {
                placementController.EnsureRosterConsistency();
                IReadOnlyList<GameObject> managedModels = placementController.ManagedModels;
                for (int i = 0; i < managedModels.Count; i++)
                {
                    GameObject model = managedModels[i];
                    if (IsSetupModelForAssetPath(model, normalizedAssetPath, activeScene))
                    {
                        return model;
                    }
                }
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                HSRCharacterController[] characterControllers = root.GetComponentsInChildren<HSRCharacterController>(true);
                for (int i = 0; i < characterControllers.Length; i++)
                {
                    HSRCharacterController characterController = characterControllers[i];
                    GameObject model = characterController != null ? characterController.gameObject : null;
                    if (IsSetupModelForAssetPath(model, normalizedAssetPath, activeScene))
                    {
                        return model;
                    }
                }
            }

            return null;
        }

        private static T FindSceneComponent<T>(UnityScene scene)
            where T : Component
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindRootObjectInScene(UnityScene scene, string objectName)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene) || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, objectName, StringComparison.Ordinal))
                {
                    return root;
                }
            }

            return null;
        }

        private static bool IsSetupModelForAssetPath(GameObject model, string normalizedAssetPath, UnityScene scene)
        {
            if (model == null
                || string.IsNullOrWhiteSpace(normalizedAssetPath)
                || !model.scene.IsValid()
                || model.scene != scene
                || model.GetComponent<HSRCharacterController>() == null)
            {
                return false;
            }

            string prefabAssetPath = NormalizeAssetPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model));
            if (string.Equals(prefabAssetPath, normalizedAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(model);
            string sourceAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            return string.Equals(sourceAssetPath, normalizedAssetPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath) ? null : EditorPathUtility.NormalizeAssetPath(assetPath);
        }
    }
}
#endif
