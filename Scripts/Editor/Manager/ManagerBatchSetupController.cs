#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal static class ManagerBatchSetupController
    {
        internal static void AddQueuedAssets(List<GameObject> queue, List<GameObject> assets)
        {
            if (queue == null || assets == null || assets.Count == 0)
            {
                return;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in queue)
            {
                if (ManagerModelDiscoveryService.TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    existingPaths.Add(assetPath);
                }
            }

            foreach (var asset in assets)
            {
                if (ManagerModelDiscoveryService.TryGetSupportedAssetPath(asset, out var assetPath) && existingPaths.Add(assetPath))
                {
                    queue.Add(asset);
                }
            }
        }

        internal static void RunSetupForAssets(HoyoToonManager manager, List<GameObject> assets, Action<GameObject, bool> addManagedModelInstance, Action onModelsDirty)
        {
            if (assets == null || assets.Count == 0)
            {
                DialogWindow.ShowWarning("Batch Auto Setup", "No valid FBX assets were provided.");
                return;
            }

            if (assets.Count == 1)
            {
                if (TryProcessAsset(manager, assets[0], out var instance))
                {
                    addManagedModelInstance?.Invoke(instance, true);
                    onModelsDirty?.Invoke();
                }

                return;
            }

            RunBatchSetup(manager, assets, addManagedModelInstance, onModelsDirty);
        }

        internal static void RunBatchSetup(HoyoToonManager manager, List<GameObject> assets, Action<GameObject, bool> addManagedModelInstance, Action onModelsDirty)
        {
            if (manager == null)
            {
                DialogWindow.ShowError("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                return;
            }

            if (assets == null || assets.Count == 0)
            {
                DialogWindow.ShowWarning("Batch Auto Setup", "No valid FBX assets were provided.");
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var createdInstances = new List<GameObject>();

            foreach (var asset in assets)
            {
                bool success = TryProcessAsset(manager, asset, false, out var instance);

                if (success)
                {
                    successCount++;
                    if (instance != null)
                    {
                        createdInstances.Add(instance);
                    }
                }
                else
                {
                    failCount++;
                }
            }

            for (int i = 0; i < createdInstances.Count; i++)
            {
                bool setActive = i == createdInstances.Count - 1;
                addManagedModelInstance?.Invoke(createdInstances[i], setActive);
            }

            onModelsDirty?.Invoke();

            if (failCount > 0)
            {
                DialogWindow.ShowWarning("Batch Auto Setup Complete", $"Completed with {successCount} success(es) and {failCount} failure(s). Check the console for details.");
            }
            else
            {
                DialogWindow.ShowInfo("Batch Auto Setup Complete", $"Successfully set up {successCount} model(s).");
            }
        }

        private static bool TryProcessAsset(HoyoToonManager manager, GameObject modelAsset, out GameObject instance)
        {
            instance = null;
            return TryProcessAsset(manager, modelAsset, true, out instance);
        }

        private static bool TryProcessAsset(HoyoToonManager manager, GameObject modelAsset, bool showDialogs, out GameObject instance)
        {
            instance = null;
            if (!ModelSetupUtility.TryPrepareFbxRequest(manager, modelAsset, null, ModelSetupUtility.SetupRequestSource.BatchSetup, out var preparedRequest, showDialogs))
            {
                return false;
            }

            return ModelSetupUtility.TryRunPreparedRequest(preparedRequest, out instance, out _);
        }
    }
}
#endif
