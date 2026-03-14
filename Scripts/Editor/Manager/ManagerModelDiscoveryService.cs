#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal static class ManagerModelDiscoveryService
    {
        internal static void AutoDiscoverSceneModels(
            HoyoToonManager manager,
            SerializedProperty managedModelsProperty,
            SerializedProperty activeModelIndexProperty,
            Action<GameObject, bool> addManagedModelInstance,
            Action<int> removeManagedModelAt)
        {
            if (managedModelsProperty == null || manager == null)
            {
                return;
            }

            PruneInvalidManagedModels(manager, managedModelsProperty, removeManagedModelAt);

            var managerScene = manager.gameObject.scene;
            if (!managerScene.IsValid())
            {
                return;
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != managerScene)
                {
                    continue;
                }

                if (!RendererUtils.RendererUsesHoyoToon(renderer))
                {
                    continue;
                }

                var root = RendererUtils.ResolveRendererRoot(renderer, manager);
                if (!IsSceneModelValid(root, manager))
                {
                    continue;
                }

                addManagedModelInstance?.Invoke(root, false);
            }

            if (activeModelIndexProperty != null && activeModelIndexProperty.intValue < 0 && managedModelsProperty.arraySize > 0)
            {
                activeModelIndexProperty.intValue = 0;
            }
        }

        internal static void PruneInvalidManagedModels(HoyoToonManager manager, SerializedProperty managedModelsProperty, Action<int> removeManagedModelAt)
        {
            if (manager == null || managedModelsProperty == null || removeManagedModelAt == null)
            {
                return;
            }

            for (int i = managedModelsProperty.arraySize - 1; i >= 0; i--)
            {
                var element = managedModelsProperty.GetArrayElementAtIndex(i);
                var go = element?.objectReferenceValue as GameObject;
                if (!IsSceneModelValid(go, manager))
                {
                    removeManagedModelAt(i);
                }
            }
        }

        internal static List<GameObject> GetSupportedAssetsFromSelection()
        {
            return GetSupportedAssetsFromObjects(Selection.objects);
        }

        internal static List<GameObject> GetSupportedAssetsFromObjects(UnityEngine.Object[] objects)
        {
            var results = new List<GameObject>();
            if (objects == null || objects.Length == 0)
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in objects)
            {
                var asset = obj as GameObject;
                if (!TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        internal static List<GameObject> GetSupportedAssetsFromFolder(DefaultAsset folderAsset, bool includeSubfolders)
        {
            var results = new List<GameObject>();
            if (folderAsset == null)
            {
                return results;
            }

            var folderPath = AssetDatabase.GetAssetPath(folderAsset);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return results;
            }

            if (!includeSubfolders)
            {
                return GetSupportedAssetsFromDirectory(folderPath, SearchOption.TopDirectoryOnly);
            }

            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            return FilterFolderAssets(guids);
        }

        internal static bool IsPendingModelReady(GameObject pendingAsset)
        {
            if (pendingAsset == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(pendingAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetSupportedAssetPath(GameObject asset, out string assetPath)
        {
            assetPath = null;
            if (asset == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private static List<GameObject> FilterFolderAssets(string[] guids)
        {
            var results = new List<GameObject>();
            if (guids == null || guids.Length == 0)
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static List<GameObject> GetSupportedAssetsFromDirectory(string unityFolderPath, SearchOption searchOption)
        {
            var results = new List<GameObject>();
            if (string.IsNullOrEmpty(unityFolderPath))
            {
                return results;
            }

            var absoluteFolder = ToAbsolutePath(unityFolderPath);
            if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = Directory.GetFiles(absoluteFolder, "*.fbx", searchOption);
            foreach (var file in files)
            {
                var assetPath = ToUnityAssetPath(file);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static string ToAbsolutePath(string unityPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                return null;
            }

            var relative = unityPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot.FullName, relative));
        }

        private static string ToUnityAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }

            var normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
            var assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + normalized.Substring(assetsRoot.Length);
        }

        private static bool IsSceneModelValid(GameObject go, HoyoToonManager manager)
        {
            if (!go || manager == null)
            {
                return false;
            }

            if (go == manager.gameObject)
            {
                return false;
            }

            if (!go.scene.IsValid() || go.scene != manager.gameObject.scene)
            {
                return false;
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (RendererUtils.RendererUsesHoyoToon(renderer))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
