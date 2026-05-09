#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Rendering.Character;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.Character;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Scene.HSR;
using HoyoToon.Runtime.Scene.Placement;
using HoyoToon.Runtime.ScriptableObjects.Games;
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
        private const string EmojiControllerProfileAssetSuffix = "/Config/EmojiControllerProfile.asset";
        private const string LookAtControllerProfileAssetSuffix = "/Config/LookAtControllerProfile.asset";

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

        public static void AddEmojiController(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.InstantiatedModels.Count <= 0)
            {
                return;
            }

            EmojiControllerProfileSO profile = ResolveEmojiControllerProfile(context, result);
            if (profile == null)
            {
                return;
            }

            for (int i = 0; i < context.InstantiatedModels.Count; i++)
            {
                GameObject instantiatedModel = context.InstantiatedModels[i];
                if (instantiatedModel == null)
                {
                    continue;
                }

                EmojiController emojiController = EnsureComponent<EmojiController>(instantiatedModel);
                if (emojiController == null)
                {
                    result.RecordWarning($"Auto setup failed to add an EmojiController to '{instantiatedModel.name}'.");
                    continue;
                }

                SkinnedMeshRenderer faceRenderer = FindEmojiFaceRenderer(instantiatedModel, profile);
                Undo.RecordObject(emojiController, "HoyoToon Auto Setup Configure Emoji Controller");
                emojiController.ConfigureProfile(profile, faceRenderer);
                EditorUtility.SetDirty(emojiController);

                if (faceRenderer == null)
                {
                    result.RecordWarning(
                        $"Auto setup added an EmojiController to '{instantiatedModel.name}', but no matching face renderer was found.");
                }
            }
        }

        public static void AddLookAtController(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.InstantiatedModels.Count <= 0)
            {
                return;
            }

            LookAtControllerProfileSO profile = ResolveLookAtControllerProfile(context, result);
            if (profile == null)
            {
                return;
            }

            for (int i = 0; i < context.InstantiatedModels.Count; i++)
            {
                GameObject instantiatedModel = context.InstantiatedModels[i];
                if (instantiatedModel == null)
                {
                    continue;
                }

                LookAtController lookAtController = EnsureComponent<LookAtController>(instantiatedModel);
                if (lookAtController == null)
                {
                    result.RecordWarning($"Auto setup failed to add a LookAtController to '{instantiatedModel.name}'.");
                    continue;
                }

                Undo.RecordObject(lookAtController, "HoyoToon Auto Setup Configure Look At Controller");
                lookAtController.ConfigureProfile(profile);
                EditorUtility.SetDirty(lookAtController);
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

        private static SkinnedMeshRenderer FindEmojiFaceRenderer(GameObject model, EmojiControllerProfileSO profile)
        {
            if (model == null || profile == null)
            {
                return null;
            }

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer firstRendererWithConfiguredShape = null;
            SkinnedMeshRenderer firstProfileMatchedRenderer = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer candidate = renderers[i];
                Mesh mesh = candidate != null ? candidate.sharedMesh : null;
                if (mesh == null || mesh.blendShapeCount <= 0)
                {
                    continue;
                }

                bool matchesProfile = profile.MatchesFaceRenderer(candidate);
                bool hasConfiguredBlendShape = profile.HasConfiguredBlendShape(mesh);
                if (matchesProfile && hasConfiguredBlendShape)
                {
                    return candidate;
                }

                if (firstRendererWithConfiguredShape == null && hasConfiguredBlendShape)
                {
                    firstRendererWithConfiguredShape = candidate;
                }

                if (firstProfileMatchedRenderer == null && matchesProfile)
                {
                    firstProfileMatchedRenderer = candidate;
                }
            }

            return firstRendererWithConfiguredShape != null ? firstRendererWithConfiguredShape : firstProfileMatchedRenderer;
        }

        private static EmojiControllerProfileSO ResolveEmojiControllerProfile(
            AutoSetupContext context,
            AutoSetupResult result)
        {
            string gameKey = ResolveEmojiProfileGameKey(context, result);
            if (string.IsNullOrWhiteSpace(gameKey))
            {
                result?.RecordWarning("Auto setup skipped the EmojiController because no game was detected.");
                return null;
            }

            EmojiControllerProfileSO profile = GeneratedAssetQueryUtility
                .LoadGeneratedAssets<EmojiControllerProfileSO>("t:EmojiControllerProfileSO", EmojiControllerProfileAssetSuffix)
                .Where(candidate => candidate != null
                    && string.Equals(candidate.GameKey, gameKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => AssetDatabase.GetAssetPath(candidate), StringComparer.OrdinalIgnoreCase)
                .LastOrDefault();

            if (profile == null)
            {
                result?.RecordWarning(
                    $"Auto setup skipped the EmojiController because no emoji controller profile was found for '{gameKey}'.");
            }

            return profile;
        }

        private static LookAtControllerProfileSO ResolveLookAtControllerProfile(
            AutoSetupContext context,
            AutoSetupResult result)
        {
            string gameKey = ResolveProfileGameKey(context, result);
            if (string.IsNullOrWhiteSpace(gameKey))
            {
                result?.RecordWarning("Auto setup skipped the LookAtController because no game was detected.");
                return null;
            }

            LookAtControllerProfileSO profile = GeneratedAssetQueryUtility
                .LoadGeneratedAssets<LookAtControllerProfileSO>("t:LookAtControllerProfileSO", LookAtControllerProfileAssetSuffix)
                .Where(candidate => candidate != null
                    && string.Equals(candidate.GameKey, gameKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => AssetDatabase.GetAssetPath(candidate), StringComparer.OrdinalIgnoreCase)
                .LastOrDefault();

            if (profile == null)
            {
                result?.RecordWarning(
                    $"Auto setup skipped the LookAtController because no look-at controller profile was found for '{gameKey}'.");
            }

            return profile;
        }

        private static string ResolveEmojiProfileGameKey(AutoSetupContext context, AutoSetupResult result)
        {
            return ResolveProfileGameKey(context, result);
        }

        private static string ResolveProfileGameKey(AutoSetupContext context, AutoSetupResult result)
        {
            if (!string.IsNullOrWhiteSpace(context?.DetectedGameKey))
            {
                return context.DetectedGameKey;
            }

            return !string.IsNullOrWhiteSpace(result?.ProfileKey) ? result.ProfileKey : null;
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
