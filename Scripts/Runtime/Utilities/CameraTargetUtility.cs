using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Utilities
{
    public static class CameraTargetUtility
    {
        private readonly struct TargetBaseline
        {
            internal TargetBaseline(bool hasParent, Vector3 localPosition, Vector3 worldPosition)
            {
                HasParent = hasParent;
                LocalPosition = localPosition;
                WorldPosition = worldPosition;
            }

            internal bool HasParent { get; }
            internal Vector3 LocalPosition { get; }
            internal Vector3 WorldPosition { get; }
        }

        private readonly struct CachedTargetBaseline
        {
            internal CachedTargetBaseline(Transform target, in TargetBaseline baseline)
            {
                Target = target;
                Baseline = baseline;
            }

            internal Transform Target { get; }
            internal TargetBaseline Baseline { get; }
        }

        private static readonly string[] s_DefaultLookAtTargetNames = { "CharacterLookAtCentre", "CameraRoot" };
        private static readonly string[] s_CloseLookAtTargetNames = { "CharacterLookAtHead", "CameraRootHead" };
        private static readonly string[] s_CenterTargetNames = { "CameraRoot" };
        private static readonly string[] s_HeadTargetNames = { "CameraRootHead" };
        private static readonly string[] s_RootBoneNames = { "Root_M" };
        private static readonly string[] s_HeadBoneNames = { "Head_M", "Head" };
        private const int TargetBaselinePruneInterval = 64;
        private static readonly Dictionary<int, CachedTargetBaseline> s_TargetBaselines = new Dictionary<int, CachedTargetBaseline>();
        private static readonly List<int> s_StaleTargetBaselineIds = new List<int>(16);
        private static int s_TargetBaselineLookupsUntilPrune = TargetBaselinePruneInterval;

        public const float DefaultTargetYOffset = 0.05f;

        public static bool SyncDefaultTargets(
            GameObject activeModel,
            Transform preferredRoot = null,
            bool useFullWorldPosition = false,
            float yOffset = DefaultTargetYOffset)
        {
            UnityScene scene = ResolveScene(activeModel, preferredRoot);
            return SyncDefaultTargets(scene, activeModel, preferredRoot, useFullWorldPosition, yOffset);
        }

        public static bool SyncDefaultTargets(
            UnityScene scene,
            GameObject activeModel,
            Transform preferredRoot = null,
            bool useFullWorldPosition = false,
            float yOffset = DefaultTargetYOffset)
        {
            Transform centerTarget = ResolveCenterTarget(scene, preferredRoot);
            Transform headTarget = ResolveHeadTarget(scene, preferredRoot);
            Transform rootBone = ResolveRootBone(activeModel);
            Transform headBone = ResolveHeadBone(activeModel);

            bool changed = false;
            changed |= ApplyTargetTransform(centerTarget, rootBone, useFullWorldPosition, yOffset);
            changed |= ApplyTargetTransform(headTarget, headBone, useFullWorldPosition, yOffset);
            return changed;
        }

        public static bool HasDefaultTargets(UnityScene scene, Transform preferredRoot = null)
        {
            return ResolveCenterTarget(scene, preferredRoot) != null
                && ResolveHeadTarget(scene, preferredRoot) != null;
        }

        private static bool ApplyTargetTransform(
            Transform target,
            Transform source,
            bool useFullWorldPosition = false,
            float yOffset = DefaultTargetYOffset)
        {
            if (target == null)
            {
                return false;
            }

            TargetBaseline baseline = GetOrCreateBaseline(target);
            bool changed = RemoveParentConstraint(target);
            if (source == null)
            {
                return changed;
            }

            Vector3 desiredPosition = ResolveDesiredPosition(
                target,
                source,
                baseline,
                useFullWorldPosition,
                yOffset);

            if (Approximately(target.position, desiredPosition))
            {
                return changed;
            }

            target.position = desiredPosition;
            RuntimeEditorBridge.MarkDirty(target);
            return true;
        }

        public static Transform ResolveDefaultLookAtTarget(UnityScene scene, Transform preferredRoot = null)
        {
            return ResolveSceneTarget(scene, preferredRoot, s_DefaultLookAtTargetNames);
        }

        public static Transform ResolveCloseLookAtTarget(UnityScene scene, Transform preferredRoot = null)
        {
            return ResolveSceneTarget(scene, preferredRoot, s_CloseLookAtTargetNames);
        }

        public static Transform ResolveCenterTarget(UnityScene scene, Transform preferredRoot = null)
        {
            return ResolveSceneTarget(scene, preferredRoot, s_CenterTargetNames);
        }

        public static Transform ResolveHeadTarget(UnityScene scene, Transform preferredRoot = null)
        {
            return ResolveSceneTarget(scene, preferredRoot, s_HeadTargetNames);
        }

        public static Transform ResolveRootBone(GameObject activeModel)
        {
            return ResolveBone(activeModel, s_RootBoneNames);
        }

        public static Transform ResolveHeadBone(GameObject activeModel)
        {
            return ResolveBone(activeModel, s_HeadBoneNames);
        }

        public static Transform ResolveBone(GameObject activeModel, IReadOnlyList<string> candidateNames)
        {
            if (activeModel == null)
            {
                return null;
            }

            return TransformSearchUtility.FindChildRecursive(activeModel.transform, candidateNames);
        }

        public static Transform ResolveSceneTarget(UnityScene scene, IReadOnlyList<string> candidateNames)
        {
            return ResolveSceneTarget(scene, null, candidateNames);
        }

        public static Transform ResolveSceneTarget(UnityScene scene, Transform preferredRoot, IReadOnlyList<string> candidateNames)
        {
            Transform target = TransformSearchUtility.FindChildRecursive(preferredRoot, candidateNames);
            if (target != null)
            {
                return target;
            }

            return TransformSearchUtility.FindInScene(scene, candidateNames);
        }

        public static Transform FindTransformRecursive(Transform root, IReadOnlyList<string> candidateNames)
        {
            return TransformSearchUtility.FindChildRecursive(root, candidateNames);
        }

        public static void ClearCachedBaseline(Transform target)
        {
            if (target == null)
            {
                return;
            }

            s_TargetBaselines.Remove(target.GetInstanceID());
        }

        public static void ClearCachedBaselines()
        {
            s_TargetBaselines.Clear();
            s_StaleTargetBaselineIds.Clear();
            s_TargetBaselineLookupsUntilPrune = TargetBaselinePruneInterval;
        }

        public static Vector3 ResolveDesiredPosition(
            Transform target,
            Transform source,
            bool useFullWorldPosition = false,
            float yOffset = DefaultTargetYOffset)
        {
            return ResolveDesiredPosition(target, source, GetOrCreateBaseline(target), useFullWorldPosition, yOffset);
        }

        private static TargetBaseline CaptureBaseline(Transform target)
        {
            if (target == null)
            {
                return default;
            }

            return new TargetBaseline(target.parent != null, target.localPosition, target.position);
        }

        private static Vector3 ResolveDesiredPosition(
            Transform target,
            Transform source,
            TargetBaseline baseline,
            bool useFullWorldPosition,
            float yOffset)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            if (source == null)
            {
                return target.position;
            }

            Vector3 desiredPosition;
            if (useFullWorldPosition)
            {
                desiredPosition = source.position;
            }
            else
            {
                desiredPosition = baseline.HasParent && target.parent != null
                    ? target.parent.position
                    : baseline.WorldPosition;
            }

            desiredPosition.y = source.position.y + yOffset;
            return desiredPosition;
        }

        public static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x)
                && Mathf.Approximately(left.y, right.y)
                && Mathf.Approximately(left.z, right.z);
        }

        private static UnityScene ResolveScene(GameObject activeModel, Transform preferredRoot)
        {
            if (preferredRoot != null)
            {
                return preferredRoot.gameObject.scene;
            }

            return activeModel != null ? activeModel.scene : default;
        }

        private static TargetBaseline GetOrCreateBaseline(Transform target)
        {
            if (target == null)
            {
                return default;
            }

            PruneInvalidBaselines(force: false);

            int targetId = target.GetInstanceID();
            if (!s_TargetBaselines.TryGetValue(targetId, out CachedTargetBaseline cachedBaseline)
                || cachedBaseline.Target != target)
            {
                TargetBaseline baseline = CaptureBaseline(target);
                cachedBaseline = new CachedTargetBaseline(target, baseline);
                s_TargetBaselines[targetId] = cachedBaseline;
            }

            return cachedBaseline.Baseline;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedBaselines()
        {
            ClearCachedBaselines();
        }

        private static void PruneInvalidBaselines(bool force)
        {
            if (!force)
            {
                --s_TargetBaselineLookupsUntilPrune;
                if (s_TargetBaselineLookupsUntilPrune > 0)
                    return;
            }

            s_TargetBaselineLookupsUntilPrune = TargetBaselinePruneInterval;
            s_StaleTargetBaselineIds.Clear();

            foreach (var baseline in s_TargetBaselines)
            {
                if (baseline.Value.Target == null)
                    s_StaleTargetBaselineIds.Add(baseline.Key);
            }

            for (int i = 0; i < s_StaleTargetBaselineIds.Count; ++i)
                s_TargetBaselines.Remove(s_StaleTargetBaselineIds[i]);

            s_StaleTargetBaselineIds.Clear();
        }

        private static bool RemoveParentConstraint(Transform target)
        {
            ParentConstraint constraint = target.GetComponent<ParentConstraint>();
            if (constraint == null)
            {
                return false;
            }

            constraint.constraintActive = false;
            RuntimeEditorBridge.DestroyObject(constraint);
            RuntimeEditorBridge.MarkDirty(target);
            return true;
        }
    }
}
