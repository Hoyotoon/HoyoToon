using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Scene.Placement
{
    internal static class ManagerLayoutCoordinator
    {
        private const int GridColumns = 5;
        private const float GridSpacingX = 1f;
        private const float GridSpacingZ = -1f;

        private const string SingleRootName = "Single";
        private const string TeamRootName = "Team";
        private const string GridRootName = "Grid";
        private const string SingleCameraName = "HSRSingleFreelook";
        private const string TeamCameraName = "HSRTeamLook";

        private static readonly Dictionary<HoyoToonTeamGame, string> s_TeamRootNames = new Dictionary<HoyoToonTeamGame, string>
        {
            { HoyoToonTeamGame.GenshinImpact, "GI" },
            { HoyoToonTeamGame.HonkaiStarRail, "HSR" },
            { HoyoToonTeamGame.ZenlessZoneZero, "ZZZ" },
            { HoyoToonTeamGame.HonkaiImpact3rd, "HI3" }
        };

        private sealed class SceneReferences
        {
            public Transform SingleRoot;
            public Transform TeamRoot;
            public Transform GridRoot;
            public GameObject SingleCamera;
            public GameObject TeamCamera;
            public GameObject LegacyGridCamera;
            public readonly Dictionary<HoyoToonTeamGame, Transform> TeamGameRoots = new Dictionary<HoyoToonTeamGame, Transform>();
        }

        internal static void ApplyLayout(CharacterPlacementController controller)
        {
            if (controller == null)
                return;

            controller.EnsureRosterConsistency();
            if (!TryResolveSceneReferences(controller.gameObject.scene, out SceneReferences sceneReferences))
                return;

            bool changed = false;
            changed |= ApplyModeContainers(sceneReferences, controller.PlacementMode, controller.TeamGame);
            changed |= ApplyModeCameras(sceneReferences, controller.PlacementMode, controller.GridCamera);

            if (!controller.AutoDiscoverManagedModels)
            {
                if (changed)
                    RuntimeEditorBridge.MarkDirty(controller);

                return;
            }

            switch (controller.PlacementMode)
            {
                case ManagerPlacementMode.Single:
                    changed |= ApplySingleLayout(controller, sceneReferences.SingleRoot);
                    break;
                case ManagerPlacementMode.Team:
                    if (sceneReferences.TeamGameRoots.TryGetValue(controller.TeamGame, out Transform selectedTeamRoot))
                        changed |= ApplyTeamLayout(controller, sceneReferences.TeamRoot, selectedTeamRoot);
                    break;
                case ManagerPlacementMode.Grid:
                    changed |= ApplyGridLayout(controller, sceneReferences.GridRoot);
                    break;
            }

            if (changed)
                RuntimeEditorBridge.MarkDirty(controller);
        }

        private static bool ApplySingleLayout(CharacterPlacementController controller, Transform singleRoot)
        {
            if (controller == null || singleRoot == null)
                return false;

            bool changed = false;
            GameObject focusedModel = controller.ActiveModel;
            IReadOnlyList<GameObject> managedModels = controller.ManagedModels;

            for (int i = 0; i < managedModels.Count; ++i)
            {
                GameObject model = managedModels[i];
                if (model == null)
                    continue;

                changed |= ReparentModel(model.transform, singleRoot);
                changed |= ResetLocalTransform(model.transform, Vector3.zero);
                changed |= SetObjectActive(model, model == focusedModel);
            }

            return changed;
        }

        private static bool ApplyTeamLayout(CharacterPlacementController controller, Transform teamRoot, Transform selectedTeamRoot)
        {
            if (controller == null || teamRoot == null || selectedTeamRoot == null)
                return false;

            bool changed = false;
            int assignedSlotCount = 0;
            int slotCount = Mathf.Min(4, selectedTeamRoot.childCount);
            IReadOnlyList<GameObject> teamActiveModels = controller.TeamActiveModels;
            IReadOnlyList<GameObject> managedModels = controller.ManagedModels;

            for (int i = 0; i < managedModels.Count; ++i)
            {
                GameObject model = managedModels[i];
                if (model == null)
                    continue;

                bool shouldBeActive = ContainsModel(teamActiveModels, model) && assignedSlotCount < slotCount;
                Transform parent = shouldBeActive ? selectedTeamRoot.GetChild(assignedSlotCount) : teamRoot;

                changed |= ReparentModel(model.transform, parent);
                changed |= ResetLocalTransform(model.transform, Vector3.zero);
                changed |= SetObjectActive(model, shouldBeActive);

                if (shouldBeActive)
                    assignedSlotCount++;
            }

            return changed;
        }

        private static bool ApplyGridLayout(CharacterPlacementController controller, Transform gridRoot)
        {
            if (controller == null || gridRoot == null)
                return false;

            bool changed = false;
            int layoutIndex = 0;
            IReadOnlyList<GameObject> managedModels = controller.ManagedModels;

            for (int i = 0; i < managedModels.Count; ++i)
            {
                GameObject model = managedModels[i];
                if (model == null)
                    continue;

                changed |= ReparentModel(model.transform, gridRoot);
                changed |= ResetLocalTransform(model.transform, BuildGridOffset(layoutIndex));
                changed |= SetObjectActive(model, true);
                layoutIndex++;
            }

            return changed;
        }

        private static bool ApplyModeContainers(SceneReferences sceneReferences, ManagerPlacementMode mode, HoyoToonTeamGame teamGame)
        {
            bool changed = false;

            changed |= SetObjectActive(sceneReferences.SingleRoot != null ? sceneReferences.SingleRoot.gameObject : null, mode == ManagerPlacementMode.Single);
            changed |= SetObjectActive(sceneReferences.TeamRoot != null ? sceneReferences.TeamRoot.gameObject : null, mode == ManagerPlacementMode.Team);
            changed |= SetObjectActive(sceneReferences.GridRoot != null ? sceneReferences.GridRoot.gameObject : null, mode == ManagerPlacementMode.Grid);

            foreach (KeyValuePair<HoyoToonTeamGame, Transform> pair in sceneReferences.TeamGameRoots)
            {
                changed |= SetObjectActive(pair.Value != null ? pair.Value.gameObject : null, mode == ManagerPlacementMode.Team && pair.Key == teamGame);
            }

            return changed;
        }

        private static bool ApplyModeCameras(SceneReferences sceneReferences, ManagerPlacementMode placementMode, GridCameraMode gridCameraMode)
        {
            if (sceneReferences == null)
                return false;

            bool changed = false;
            bool useSingleCamera = placementMode == ManagerPlacementMode.Single
                || (placementMode == ManagerPlacementMode.Grid && gridCameraMode == GridCameraMode.Single);
            bool useTeamCamera = placementMode == ManagerPlacementMode.Team
                || (placementMode == ManagerPlacementMode.Grid && gridCameraMode == GridCameraMode.Team);

            changed |= SetObjectActive(sceneReferences.SingleCamera, useSingleCamera);
            changed |= SetObjectActive(sceneReferences.TeamCamera, useTeamCamera);
            changed |= SetObjectActive(sceneReferences.LegacyGridCamera, false);
            return changed;
        }

        private static bool TryResolveSceneReferences(UnityScene scene, out SceneReferences sceneReferences)
        {
            sceneReferences = null;
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return false;

            SceneReferences resolved = new SceneReferences
            {
                SingleRoot = FindTransform(scene, SingleRootName),
                TeamRoot = FindTransform(scene, TeamRootName),
                GridRoot = FindTransform(scene, GridRootName),
                SingleCamera = FindTransform(scene, SingleCameraName)?.gameObject,
                TeamCamera = FindTransform(scene, TeamCameraName)?.gameObject
            };

            ManagerLayoutReferences explicitReferences = FindLayoutReferences(scene);
            if (explicitReferences != null)
            {
                resolved.SingleRoot = explicitReferences.SingleRoot != null ? explicitReferences.SingleRoot : resolved.SingleRoot;
                resolved.TeamRoot = explicitReferences.TeamRoot != null ? explicitReferences.TeamRoot : resolved.TeamRoot;
                resolved.GridRoot = explicitReferences.GridRoot != null ? explicitReferences.GridRoot : resolved.GridRoot;
                resolved.SingleCamera = explicitReferences.SingleCamera != null ? explicitReferences.SingleCamera : resolved.SingleCamera;
                resolved.TeamCamera = explicitReferences.TeamCamera != null ? explicitReferences.TeamCamera : resolved.TeamCamera;
                resolved.LegacyGridCamera = explicitReferences.LegacyGridCamera;
            }

            foreach (KeyValuePair<HoyoToonTeamGame, string> pair in s_TeamRootNames)
            {
                Transform fallbackRoot = FindTransform(scene, pair.Value);
                if (explicitReferences != null && explicitReferences.TryGetTeamRoot(pair.Key, out Transform explicitTeamRoot))
                {
                    resolved.TeamGameRoots[pair.Key] = explicitTeamRoot;
                }
                else
                {
                    resolved.TeamGameRoots[pair.Key] = fallbackRoot;
                }
            }

            if (resolved.SingleRoot == null || resolved.TeamRoot == null || resolved.GridRoot == null)
                return false;

            sceneReferences = resolved;
            return true;
        }

        private static bool ContainsModel(IReadOnlyList<GameObject> models, GameObject candidate)
        {
            if (models == null || candidate == null)
                return false;

            for (int i = 0; i < models.Count; ++i)
            {
                if (models[i] == candidate)
                    return true;
            }

            return false;
        }

        private static Vector3 BuildGridOffset(int index)
        {
            int columns = Mathf.Max(1, GridColumns);
            int row = index / columns;
            int slot = index % columns;
            int columnOffset = slot == 0
                ? 0
                : (slot % 2 == 1 ? -((slot + 1) / 2) : slot / 2);

            return new Vector3(columnOffset * GridSpacingX, 0f, row * GridSpacingZ);
        }

        private static Transform FindTransform(UnityScene scene, string name)
        {
            return TransformSearchUtility.FindInScene(scene, name);
        }

        private static ManagerLayoutReferences FindLayoutReferences(UnityScene scene)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; ++i)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                ManagerLayoutReferences references = root.GetComponentInChildren<ManagerLayoutReferences>(includeInactive: true);
                if (references != null)
                    return references;
            }

            return null;
        }

        private static bool ReparentModel(Transform modelTransform, Transform parent)
        {
            if (modelTransform == null || parent == null || modelTransform.parent == parent)
                return false;

            modelTransform.SetParent(parent, false);
            RuntimeEditorBridge.MarkDirty(modelTransform);
            return true;
        }

        private static bool ResetLocalTransform(Transform target, Vector3 localPosition)
        {
            if (target == null)
                return false;

            bool positionChanged = target.localPosition != localPosition;
            bool rotationChanged = target.localRotation != Quaternion.identity;
            if (!positionChanged && !rotationChanged)
                return false;

            if (positionChanged)
                target.localPosition = localPosition;

            if (rotationChanged)
                target.localRotation = Quaternion.identity;

            RuntimeEditorBridge.MarkDirty(target);
            return true;
        }

        private static bool SetObjectActive(GameObject target, bool isActive)
        {
            if (target == null || target.activeSelf == isActive)
                return false;

            target.SetActive(isActive);
            RuntimeEditorBridge.MarkDirty(target);
            return true;
        }
    }
}
