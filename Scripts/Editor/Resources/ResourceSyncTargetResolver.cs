#if UNITY_EDITOR
using System.Collections.Generic;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Runtime.ScriptableObjects.Resources;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Resources
{
    internal static class ResourceSyncTargetResolver
    {
        internal static bool TryResolveAllTargets(out List<ResourceSyncTarget> targets, out List<string> messages)
        {
            targets = new List<ResourceSyncTarget>();
            messages = new List<string>();

            foreach (HoyoToonResourcesSO resourceAsset in ResourceRegistry.Resources)
            {
                if (resourceAsset == null)
                {
                    continue;
                }

                if (TryBuildTarget(resourceAsset, out ResourceSyncTarget target, out string message))
                {
                    targets.Add(target);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    messages.Add(message);
                }
            }

            if (targets.Count > 0)
            {
                return true;
            }

            if (messages.Count <= 0)
            {
                messages.Add("No generated HoyoToonResources definitions were found.");
            }

            return false;
        }

        internal static bool TryResolveDetectedTarget(out ResourceSyncTarget target, out string message)
        {
            if (TryResolveFromSelectedResource(out target, out message))
            {
                return true;
            }

            if (TryResolveFromSelectionContext(out target, out message))
            {
                return true;
            }

            target = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Select a HoyoToonResources asset or an asset with nearby game JSON so HoyoToon can detect which game resources to sync.";
            }

            return false;
        }

        private static bool TryResolveFromSelectedResource(out ResourceSyncTarget target, out string message)
        {
            if (Selection.activeObject is HoyoToonResourcesSO selectedResource)
            {
                return TryBuildTarget(selectedResource, out target, out message);
            }

            target = null;
            message = null;
            return false;
        }

        private static bool TryResolveFromSelectionContext(out ResourceSyncTarget target, out string message)
        {
            target = null;
            message = null;

            Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                message = "No asset is selected. Select a HoyoToonResources asset or an asset belonging to a game-specific context first.";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                message = "The current selection does not resolve to a valid asset path.";
                return false;
            }

            if (!GameDetector.TryDetectGameFromAssetContext(assetPath, out Runtime.ScriptableObjects.Games.GameConfigSO gameConfig, out string matchedJsonAssetPath)
                || gameConfig == null)
            {
                message = $"Could not detect a game from '{assetPath}'. Select the HoyoToonResources asset directly or an asset with nearby game JSON markers.";
                return false;
            }

            if (!ResourceRegistry.TryGetResources(gameConfig.Key, out HoyoToonResourcesSO resourceAsset))
            {
                message = $"Detected game '{gameConfig.Key}' from '{matchedJsonAssetPath}', but no generated HoyoToonResources definition exists for that game.";
                return false;
            }

            return TryBuildTarget(resourceAsset, out target, out message);
        }

        private static bool TryBuildTarget(HoyoToonResourcesSO resourceAsset, out ResourceSyncTarget target, out string message)
        {
            target = null;
            message = null;

            if (resourceAsset == null)
            {
                message = "No HoyoToon resource definition was selected.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourceAsset.Key))
            {
                message = $"The selected HoyoToonResources asset '{AssetDatabase.GetAssetPath(resourceAsset)}' is missing its game key.";
                return false;
            }

            if (!ResourceSyncStorage.TryResolveDestinationRoot(resourceAsset, out string destinationAssetPath, out string destinationAbsolutePath, out string pathError))
            {
                message = $"Cannot sync '{resourceAsset.Key}' resources: {pathError}";
                return false;
            }

            target = new ResourceSyncTarget
            {
                GameKey = resourceAsset.Key,
                DisplayName = string.IsNullOrWhiteSpace(resourceAsset.DisplayName) ? resourceAsset.Key : resourceAsset.DisplayName,
                ResourceAsset = resourceAsset,
                DestinationAssetPath = destinationAssetPath,
                DestinationAbsolutePath = destinationAbsolutePath,
            };

            return true;
        }
    }
}
#endif