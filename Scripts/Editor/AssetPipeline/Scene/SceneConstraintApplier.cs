#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.AssetPipeline.Scene
{
    internal static class SceneConstraintApplier
    {
        public static int ApplyConstraints(Transform armatureRoot, GameMetadata meta, bool replaceExisting = false, bool logWarnings = true)
        {
            if (armatureRoot == null || meta == null || meta.BoneConstraints == null || meta.BoneConstraints.Count == 0)
            {
                return 0;
            }

            var boneMap = BuildBoneMap(armatureRoot);
            int applied = 0;

            foreach (var rule in meta.BoneConstraints)
            {
                if (rule == null) continue;
                if (string.IsNullOrWhiteSpace(rule.TargetBone) || string.IsNullOrWhiteSpace(rule.SourceBone))
                {
                    if (logWarnings)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Constraint rule missing TargetBone or SourceBone.");
                    continue;
                }

                if (!boneMap.TryGetValue(rule.TargetBone, out var targetBone) || targetBone == null)
                {
                    if (logWarnings)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Constraint target bone not found: '{rule.TargetBone}'.");
                    continue;
                }

                if (!boneMap.TryGetValue(rule.SourceBone, out var sourceBone) || sourceBone == null)
                {
                    if (logWarnings)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Constraint source bone not found: '{rule.SourceBone}'.");
                    continue;
                }

                var type = NormalizeConstraintType(rule.ConstraintType);
                switch (type)
                {
                    case ConstraintKind.Parent:
                        if (ApplyParentConstraint(targetBone, sourceBone, rule, replaceExisting)) applied++;
                        break;
                    case ConstraintKind.Rotation:
                        if (ApplyRotationConstraint(targetBone, sourceBone, rule, replaceExisting)) applied++;
                        break;
                    case ConstraintKind.Position:
                        if (ApplyPositionConstraint(targetBone, sourceBone, rule, replaceExisting)) applied++;
                        break;
                    default:
                        if (logWarnings)
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Unknown constraint type '{rule.ConstraintType}' for bone '{rule.TargetBone}'.");
                        break;
                }
            }

            if (applied > 0)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Applied {applied} constraint(s) to '{armatureRoot.name}'.");
            }

            return applied;
        }

        private static Dictionary<string, Transform> BuildBoneMap(Transform root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            if (root == null) return map;

            var all = root.GetComponentsInChildren<Transform>(true);
            if (all == null) return map;

            foreach (var t in all)
            {
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                if (!map.ContainsKey(t.name))
                {
                    map[t.name] = t;
                }
            }
            return map;
        }

        private static ConstraintKind NormalizeConstraintType(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ConstraintKind.Unknown;
            var key = value.Trim().ToLowerInvariant();
            if (key.Contains("parent")) return ConstraintKind.Parent;
            if (key.Contains("rotation") || key.Contains("rot")) return ConstraintKind.Rotation;
            if (key.Contains("position") || key.Contains("pos")) return ConstraintKind.Position;
            return ConstraintKind.Unknown;
        }

        private static bool ApplyTypedConstraint<T>(
            Transform target, Transform source, BoneConstraintRule rule, bool replaceExisting,
            Action<T, Transform, BoneConstraintRule> configure,
            bool includeTranslation, bool includeRotation) where T : Component, IConstraint
        {
            if (target == null || source == null) return false;
            var constraint = EnsureConstraint<T>(target, replaceExisting);
            if (constraint == null) return false;
            ConfigureConstraintCommon(constraint, rule);
            configure(constraint, source, rule);
            ApplyOffsets(rule, source, target, constraint, sourceIndex: 0, includeTranslation: includeTranslation, includeRotation: includeRotation);
            FinalizeConstraint(constraint, rule);
            return true;
        }

        private static bool ApplyParentConstraint(Transform target, Transform source, BoneConstraintRule rule, bool replaceExisting)
            => ApplyTypedConstraint<ParentConstraint>(target, source, rule, replaceExisting, (c, src, r) =>
            {
                c.translationAxis = GetAxis(r.PositionAxes);
                c.rotationAxis = GetAxis(r.RotationAxes);
                c.SetSources(new List<ConstraintSource> { new ConstraintSource { sourceTransform = src, weight = Clamp01(r.SourceWeight) } });
            }, includeTranslation: true, includeRotation: true);

        private static bool ApplyRotationConstraint(Transform target, Transform source, BoneConstraintRule rule, bool replaceExisting)
            => ApplyTypedConstraint<RotationConstraint>(target, source, rule, replaceExisting, (c, src, r) =>
            {
                c.rotationAxis = GetAxis(r.RotationAxes);
                c.SetSources(new List<ConstraintSource> { new ConstraintSource { sourceTransform = src, weight = Clamp01(r.SourceWeight) } });
            }, includeTranslation: false, includeRotation: true);

        private static bool ApplyPositionConstraint(Transform target, Transform source, BoneConstraintRule rule, bool replaceExisting)
            => ApplyTypedConstraint<PositionConstraint>(target, source, rule, replaceExisting, (c, src, r) =>
            {
                c.translationAxis = GetAxis(r.PositionAxes);
                c.SetSources(new List<ConstraintSource> { new ConstraintSource { sourceTransform = src, weight = Clamp01(r.SourceWeight) } });
            }, includeTranslation: true, includeRotation: false);

        private static void ConfigureConstraintCommon(IConstraint constraint, BoneConstraintRule rule)
        {
            if (constraint == null) return;
            constraint.constraintActive = false;
            constraint.locked = false;
            constraint.weight = Clamp01(rule?.Weight);
        }

        private static void FinalizeConstraint(IConstraint constraint, BoneConstraintRule rule)
        {
            if (constraint == null) return;
            if (rule?.Locked.HasValue == true)
            {
                constraint.locked = rule.Locked.Value;
            }
            if (rule?.Active.HasValue == true)
            {
                constraint.constraintActive = rule.Active.Value;
            }
            else
            {
                constraint.constraintActive = true;
            }
        }

        private static void ApplyOffsets(BoneConstraintRule rule, Transform source, Transform target, Component constraint, int sourceIndex, bool includeTranslation, bool includeRotation)
        {
            if (rule?.MaintainOffset == false)
            {
                if (includeTranslation)
                {
                    switch (constraint)
                    {
                        case ParentConstraint parent:
                            parent.SetTranslationOffset(sourceIndex, Vector3.zero);
                            break;
                        case PositionConstraint position:
                            position.translationOffset = Vector3.zero;
                            break;
                    }
                }
                if (includeRotation)
                {
                    switch (constraint)
                    {
                        case ParentConstraint parent:
                            parent.SetRotationOffset(sourceIndex, Vector3.zero);
                            break;
                        case RotationConstraint rotation:
                            rotation.rotationOffset = Vector3.zero;
                            break;
                    }
                }
                return;
            }

            if (source == null || target == null) return;

            if (includeTranslation)
            {
                var offsetPos = source.InverseTransformPoint(target.position);
                switch (constraint)
                {
                    case ParentConstraint parent:
                        parent.SetTranslationOffset(sourceIndex, offsetPos);
                        break;
                    case PositionConstraint position:
                        position.translationOffset = offsetPos;
                        break;
                }
            }

            if (includeRotation)
            {
                var offsetRot = Quaternion.Inverse(source.rotation) * target.rotation;
                var offsetEuler = offsetRot.eulerAngles;
                switch (constraint)
                {
                    case ParentConstraint parent:
                        parent.SetRotationOffset(sourceIndex, offsetEuler);
                        break;
                    case RotationConstraint rotation:
                        rotation.rotationOffset = offsetEuler;
                        break;
                }
            }
        }

        private static T EnsureConstraint<T>(Transform target, bool replaceExisting) where T : Component
        {
            if (target == null) return null;

            var existing = target.GetComponents<T>();
            if (existing != null && existing.Length > 0)
            {
                if (replaceExisting)
                {
                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(existing[i]);
                        }
                    }
                }
                else
                {
                    return existing[0];
                }
            }

            return target.gameObject.AddComponent<T>();
        }

        private static Axis GetAxis(ConstraintAxisConfig config)
        {
            if (config == null || (!config.X.HasValue && !config.Y.HasValue && !config.Z.HasValue))
            {
                return Axis.X | Axis.Y | Axis.Z;
            }

            Axis axis = Axis.None;
            if (config.X == true) axis |= Axis.X;
            if (config.Y == true) axis |= Axis.Y;
            if (config.Z == true) axis |= Axis.Z;
            return axis;
        }

        private static float Clamp01(float? value)
        {
            if (!value.HasValue) return 1f;
            return Mathf.Clamp01(value.Value);
        }

        private enum ConstraintKind
        {
            Unknown,
            Parent,
            Rotation,
            Position
        }
    }
}
#endif
