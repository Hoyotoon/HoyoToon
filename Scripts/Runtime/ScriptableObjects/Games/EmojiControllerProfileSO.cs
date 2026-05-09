using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "EmojiControllerProfile", menuName = "HoyoToon/Emoji Controller Profile")]
    public class EmojiControllerProfileSO : GameScopedScriptableObject
    {
        private const float MinimumBlinkDuration = 0.01f;
        private const float BlendShapeFullWeight = 100f;

        [Serializable]
        public sealed class BlinkTiming
        {
            [SerializeField] private bool enableAutoBlink = true;
            [SerializeField] [Min(0f)] private float minBlinkGap = 3f;
            [SerializeField] [Min(0f)] private float maxBlinkGap = 5f;
            [SerializeField] [Range(0.01f, 1f)] private float blinkingDuration = 0.16f;
            [SerializeField] [Range(0f, 1f)] private float doubleBlinkProbability = 0.05f;
            [SerializeField] [Min(0f)] private float doubleBlinkGap = 0.08f;
            [SerializeField] [Min(0f)] private float fadeInAutoBlinkTime;
            [SerializeField] [Min(0f)] private float fadeOutAutoBlinkTime;

            public bool EnableAutoBlink => enableAutoBlink;
            public float MinBlinkGap => minBlinkGap;
            public float MaxBlinkGap => Mathf.Max(minBlinkGap, maxBlinkGap);
            public float BlinkingDuration => Mathf.Max(MinimumBlinkDuration, blinkingDuration);
            public float DoubleBlinkProbability => Mathf.Clamp01(doubleBlinkProbability);
            public float DoubleBlinkGap => Mathf.Max(0f, doubleBlinkGap);
            public float FadeInAutoBlinkTime => Mathf.Max(0f, fadeInAutoBlinkTime);
            public float FadeOutAutoBlinkTime => Mathf.Max(0f, fadeOutAutoBlinkTime);
        }

        [Serializable]
        public sealed class BlinkClosePoseConfig
        {
            [SerializeField] private string blendShapeName;
            [SerializeField] [Range(0f, 100f)] private float blinkCloseValue = BlendShapeFullWeight;
            [SerializeField] private AnimationCurve curve = CreateDefaultBlinkCurve();

            public string BlendShapeName => blendShapeName;
            public float BlinkCloseValue => Mathf.Clamp(blinkCloseValue, 0f, BlendShapeFullWeight);
            public AnimationCurve Curve => curve;

            public float Evaluate(float normalizedBlinkTime, float fallbackBlinkWeight)
            {
                float curveWeight = curve != null && curve.length > 0
                    ? curve.Evaluate(Mathf.Clamp01(normalizedBlinkTime))
                    : fallbackBlinkWeight;

                return Mathf.Clamp01(curveWeight) * BlinkCloseValue;
            }
        }

        [Serializable]
        public sealed class FacePoseParam
        {
            [SerializeField] private string blendShapeName;
            [SerializeField] [Range(0f, 100f)] private float value = BlendShapeFullWeight;
            [SerializeField] private bool enabled = true;

            public string BlendShapeName => blendShapeName;
            public float Value => Mathf.Clamp(value, 0f, BlendShapeFullWeight);
            public bool Enabled => enabled;
        }

        [SerializeField] private string displayName;
        [SerializeField]
        [FormerlySerializedAs("faceMeshNameHints")]
        [Tooltip("Mesh names that can host this face profile. Names are matched case-insensitively; use * as a wildcard when a game needs variants.")]
        private List<string> faceMeshNames = new List<string>();
        [SerializeField] private BlinkTiming blinkTiming = new BlinkTiming();
        [SerializeField] [Range(0f, 100f)] private float autoBlinkWeight = BlendShapeFullWeight;
        [SerializeField] private bool applyDefaultFacePose = true;
        [SerializeField] private List<BlinkClosePoseConfig> blinkClosePoseConfigs = new List<BlinkClosePoseConfig>();
        [SerializeField] private List<FacePoseParam> autoBlinkPoseParams = new List<FacePoseParam>();

        public string DisplayName => displayName;
        public IReadOnlyList<string> FaceMeshNames => faceMeshNames;
        public BlinkTiming Timing => blinkTiming;
        public float AutoBlinkWeight => Mathf.Clamp(autoBlinkWeight, 0f, BlendShapeFullWeight);
        public bool ApplyDefaultFacePose => applyDefaultFacePose;
        public IReadOnlyList<BlinkClosePoseConfig> BlinkClosePoseConfigs => blinkClosePoseConfigs;
        public IReadOnlyList<FacePoseParam> AutoBlinkPoseParams => autoBlinkPoseParams;

        public bool MatchesFaceRenderer(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null)
                return false;

            return MatchesFaceMesh(mesh);
        }

        public bool MatchesFaceMesh(Mesh mesh)
        {
            return mesh != null && MatchesAnyMeshName(mesh.name, faceMeshNames);
        }

        public bool CanOverrideBlendShape(string blendShapeName)
        {
            return IsConfiguredBlinkClosePose(blendShapeName)
                || IsConfiguredAutoBlinkPoseParam(blendShapeName);
        }

        public bool HasConfiguredBlinkPose(Mesh mesh)
        {
            return HasConfiguredBlendShape(mesh, IsConfiguredBlinkClosePose);
        }

        public bool HasConfiguredBlendShape(Mesh mesh)
        {
            return HasConfiguredBlendShape(mesh, CanOverrideBlendShape);
        }

        private bool IsConfiguredBlinkClosePose(string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName) || blinkClosePoseConfigs == null)
                return false;

            for (int i = 0; i < blinkClosePoseConfigs.Count; ++i)
            {
                BlinkClosePoseConfig config = blinkClosePoseConfigs[i];
                if (config != null && MatchesName(blendShapeName, config.BlendShapeName))
                    return true;
            }

            return false;
        }

        private bool IsConfiguredAutoBlinkPoseParam(string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName) || autoBlinkPoseParams == null)
                return false;

            for (int i = 0; i < autoBlinkPoseParams.Count; ++i)
            {
                FacePoseParam param = autoBlinkPoseParams[i];
                if (param != null && param.Enabled && MatchesName(blendShapeName, param.BlendShapeName))
                    return true;
            }

            return false;
        }

        private static bool HasConfiguredBlendShape(Mesh mesh, Func<string, bool> predicate)
        {
            if (mesh == null || mesh.blendShapeCount <= 0 || predicate == null)
                return false;

            for (int i = 0; i < mesh.blendShapeCount; ++i)
            {
                if (predicate(mesh.GetBlendShapeName(i)))
                    return true;
            }

            return false;
        }

        private static bool MatchesAnyMeshName(string value, IReadOnlyList<string> names)
        {
            if (string.IsNullOrWhiteSpace(value) || names == null)
                return false;

            for (int i = 0; i < names.Count; ++i)
            {
                string expectedName = names[i];
                if (MatchesMeshName(value, expectedName))
                    return true;
            }

            return false;
        }

        private static bool MatchesMeshName(string actualName, string expectedName)
        {
            if (string.IsNullOrWhiteSpace(actualName) || string.IsNullOrWhiteSpace(expectedName))
                return false;

            string normalizedActualName = NormalizeUnityObjectName(actualName);
            string normalizedExpectedName = NormalizeUnityObjectName(expectedName);
            if (string.Equals(normalizedActualName, normalizedExpectedName, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalizedExpectedName.IndexOf('*') >= 0
                && WildcardMatch(normalizedActualName, normalizedExpectedName);
        }

        private static bool MatchesName(string actualName, string expectedName)
        {
            return !string.IsNullOrWhiteSpace(actualName)
                && !string.IsNullOrWhiteSpace(expectedName)
                && string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }

        private static string NormalizeUnityObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            const string cloneSuffix = "(Clone)";
            if (normalized.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).TrimEnd();

            return normalized;
        }

        private static bool WildcardMatch(string actualName, string pattern)
        {
            int actualIndex = 0;
            int patternIndex = 0;
            int starIndex = -1;
            int matchIndex = 0;

            while (actualIndex < actualName.Length)
            {
                if (patternIndex < pattern.Length
                    && (pattern[patternIndex] == '?'
                        || char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(actualName[actualIndex])))
                {
                    actualIndex++;
                    patternIndex++;
                    continue;
                }

                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    matchIndex = actualIndex;
                    continue;
                }

                if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    actualIndex = ++matchIndex;
                    continue;
                }

                return false;
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                patternIndex++;

            return patternIndex == pattern.Length;
        }

        private static AnimationCurve CreateDefaultBlinkCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f));
        }
    }
}
