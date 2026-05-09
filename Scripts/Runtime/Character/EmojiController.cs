using System;
using System.Collections.Generic;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEngine;

namespace HoyoToon.Runtime.Character
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Character/Emoji Controller")]
    public class EmojiController : MonoBehaviour
    {
        private const float MinimumBlinkDuration = 0.01f;
        private const float BlendShapeFullWeight = 100f;
        private const float WeightEpsilon = 0.0001f;

        private enum BlinkPhase
        {
            Waiting,
            Blinking,
            DoubleBlinkGap
        }

        private readonly struct BlendShapeBinding
        {
            public BlendShapeBinding(SkinnedMeshRenderer renderer, int index)
            {
                Renderer = renderer;
                Index = index;
            }

            public readonly SkinnedMeshRenderer Renderer;
            public readonly int Index;
        }

        [Serializable]
        public sealed class AnimLayerBlink
        {
            [Tooltip("When enabled, this controller can generate automatic blink pulses.")]
            public bool EnableAutoBlink = true;

            [Min(0f)]
            [Tooltip("Minimum idle time before an automatic blink starts.")]
            public float MinBlinkGap = 3f;

            [Min(0f)]
            [Tooltip("Maximum idle time before an automatic blink starts.")]
            public float MaxBlinkGap = 5f;

            [Range(0.01f, 1f)]
            [Tooltip("Total time for one blink pulse from open, to closed, back to open.")]
            public float BlinkingDuration = 0.16f;

            [Range(0f, 1f)]
            [Tooltip("Chance that an automatic blink becomes a quick double blink.")]
            public float DoubleBlinkProbability = 0.05f;

            private float m_BlinkWeight;

            public float BlinkWeight
            {
                get => m_BlinkWeight;
                internal set => m_BlinkWeight = Mathf.Clamp01(value);
            }

            internal void ApplyTiming(EmojiControllerProfileSO.BlinkTiming timing)
            {
                if (timing == null)
                    return;

                EnableAutoBlink = timing.EnableAutoBlink;
                MinBlinkGap = timing.MinBlinkGap;
                MaxBlinkGap = timing.MaxBlinkGap;
                BlinkingDuration = timing.BlinkingDuration;
                DoubleBlinkProbability = timing.DoubleBlinkProbability;
            }

            internal void Validate()
            {
                MinBlinkGap = Mathf.Max(0f, MinBlinkGap);
                MaxBlinkGap = Mathf.Max(MinBlinkGap, MaxBlinkGap);
                BlinkingDuration = Mathf.Clamp(BlinkingDuration, MinimumBlinkDuration, 1f);
                DoubleBlinkProbability = Mathf.Clamp01(DoubleBlinkProbability);
                BlinkWeight = m_BlinkWeight;
            }
        }

        [SerializeField]
        [Tooltip("Game-specific face profile. This defines face mesh matching, blink poses, and default pose params.")]
        private EmojiControllerProfileSO profile;

        [SerializeField]
        [Tooltip("Blink timing copied from the assigned profile by default, then used by runtime Tick.")]
        public AnimLayerBlink BlinkConfig = new AnimLayerBlink();

        [Header("Blend Shape Driver")]
        [SerializeField]
        [Tooltip("Specific face mesh renderer to drive. Auto setup should assign this when it can.")]
        private SkinnedMeshRenderer faceRenderer;

        [SerializeField]
        [Tooltip("When enabled, the controller resolves the face renderer from the profile's mesh names.")]
        private bool autoFindFaceRenderer = true;

        [SerializeField]
        [Tooltip("Optional root used for face renderer discovery. If unset, this component's transform is used.")]
        private Transform faceSearchRoot;

        [SerializeField]
        [Tooltip("When enabled, this controller consumes face pose values and writes them to matching blendshapes.")]
        private bool applyBlendShapes = true;

        [SerializeField]
        [Tooltip("When enabled, profile blendshape values are reapplied every LateUpdate. Leave disabled unless another animation system overwrites the same blendshapes.")]
        private bool reapplyBlendShapesEveryFrame;

        [SerializeField]
        [Tooltip("When enabled, BlinkConfig timing is refreshed from the profile when the profile changes.")]
        private bool syncBlinkTimingFromProfile = true;

        [Header("Animator Binding")]
        [SerializeField]
        [Tooltip("Optional animator whose blink layer weight should also be driven by BlinkConfig.BlinkWeight.")]
        private Animator animator;

        [SerializeField]
        [Tooltip("When enabled, the first Animator found in this hierarchy is assigned automatically.")]
        private bool autoFindAnimator;

        [SerializeField]
        [Tooltip("When enabled, BlinkConfig.BlinkWeight is written to the configured Animator layer every LateUpdate.")]
        private bool applyBlinkToAnimatorLayer;

        [SerializeField]
        [Tooltip("Animator layer name to drive when Blink Layer Index is negative.")]
        private string blinkLayerName = "Blink";

        [SerializeField]
        [Tooltip("Animator layer index to drive. Set to -1 to resolve by Blink Layer Name.")]
        private int blinkLayerIndex = -1;

        private BlinkPhase m_BlinkPhase;
        private float m_BlinkPhaseTimer;
        private float m_BlinkNormalizedTime;
        private float m_RawBlinkWeight;
        private float m_AutoBlinkFade = 1f;
        private int m_QueuedExtraBlinks;
        private bool m_BlinkCycleInitialized;
        private Animator m_CachedLayerAnimator;
        private string m_CachedLayerName;
        private int m_CachedLayerIndex = -1;
        private bool m_AnimatorResolveAttempted;
        private bool m_FaceRendererResolveAttempted;
        private bool m_BlendShapeBindingsDirty = true;
        private bool m_BlendShapeOutputDirty = true;
        private bool m_AnimatorOutputDirty = true;
        private float m_LastAppliedAnimatorLayerWeight = float.NaN;
        private EmojiControllerProfileSO m_AppliedTimingProfile;
        private readonly HashSet<string> m_DisableBlinkReasons = new HashSet<string>();
        private readonly Dictionary<string, List<BlendShapeBinding>> m_BlendShapeBindingsByName =
            new Dictionary<string, List<BlendShapeBinding>>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_BlendShapeValueMap =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_LastAppliedBlendShapeValues =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly List<SkinnedMeshRenderer> m_SkinnedMeshRendererScratch = new List<SkinnedMeshRenderer>(8);

        public EmojiControllerProfileSO Profile
        {
            get => profile;
            set
            {
                if (profile == value)
                    return;

                profile = value;
                m_AppliedTimingProfile = null;
                MarkFaceBindingsDirty();
                MarkOutputDirty();
                ApplyProfileTimingIfNeeded();
            }
        }

        public SkinnedMeshRenderer FaceRenderer
        {
            get => faceRenderer;
            set
            {
                if (faceRenderer == value)
                    return;

                faceRenderer = value;
                m_FaceRendererResolveAttempted = faceRenderer != null;
                MarkBlendShapeBindingsDirty();
            }
        }

        public Animator Animator
        {
            get => animator;
            set
            {
                if (animator == value)
                    return;

                animator = value;
                m_AnimatorResolveAttempted = animator != null;
                InvalidateAnimatorLayerCache();
            }
        }

        public bool Enabled => isActiveAndEnabled && profile != null && (applyBlendShapes || applyBlinkToAnimatorLayer);
        public bool IsAutoBlinkEnabled => IsAutoBlinkAllowed();
        public bool AutoBlinkEnabled => IsAutoBlinkEnabled;
        public float Weight => profile != null ? profile.AutoBlinkWeight : 0f;
        public float BlinkWeight => GetBlinkWeight();
        public bool HasBlinkBlendShapes
        {
            get
            {
                EnsureBlendShapeBindings();
                return profile != null
                    && faceRenderer != null
                    && profile.HasConfiguredBlinkPose(faceRenderer.sharedMesh);
            }
        }

        public void ConfigureProfile(EmojiControllerProfileSO nextProfile, SkinnedMeshRenderer nextFaceRenderer = null)
        {
            profile = nextProfile;
            faceRenderer = nextFaceRenderer;
            m_AppliedTimingProfile = null;
            m_FaceRendererResolveAttempted = faceRenderer != null;
            MarkFaceBindingsDirty();
            MarkOutputDirty();
            ApplyProfileTimingIfNeeded();
            RefreshBlendShapeBindings();
        }

        private void Reset()
        {
            BlinkConfig = new AnimLayerBlink();
            faceSearchRoot = transform;
            m_AutoBlinkFade = 1f;
            MarkFaceBindingsDirty();
            MarkOutputDirty();
            ResolveAnimator();
            BeginIdle();
        }

        private void Awake()
        {
            ValidateSettings();
            EnsureBlendShapeBindings();
            ResolveAnimator();
        }

        private void OnEnable()
        {
            ValidateSettings();
            EnsureBlendShapeBindings();
            ResolveAnimator();
            BeginIdle();
        }

        private void OnDisable()
        {
            m_BlinkCycleInitialized = false;
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            SetBlinkWeight(0f);
            ApplyOutputsNow();
        }

        private void OnValidate()
        {
            m_AppliedTimingProfile = null;
            ValidateSettings();
            MarkFaceBindingsDirty();
            MarkOutputDirty();
            InvalidateAnimatorLayerCache();
            m_AnimatorResolveAttempted = false;
        }

        private void OnTransformChildrenChanged()
        {
            MarkFaceBindingsDirty();
            MarkOutputDirty();
            m_AnimatorResolveAttempted = false;
        }

        private void Update()
        {
            if (!HasRuntimeOutputTarget())
                return;

            Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!HasRuntimeOutputTarget())
                return;

            ApplyOutputsNow();
        }

        public void BeginIdle()
        {
            ValidateSettings();
            m_QueuedExtraBlinks = 0;
            m_BlinkCycleInitialized = true;
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            SetBlinkWeight(0f);
            ScheduleNextBlink();
        }

        public void EnableAutoBlink(bool enabled, bool restartBlink)
        {
            ValidateSettings();
            BlinkConfig.EnableAutoBlink = enabled;

            if (enabled)
            {
                if (restartBlink || !m_BlinkCycleInitialized)
                    BeginIdle();

                return;
            }

            m_BlinkCycleInitialized = false;
            m_QueuedExtraBlinks = 0;
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            SetBlinkWeight(0f);
        }

        public void SetAutoBlinkEnabled(bool enabled)
        {
            EnableAutoBlink(enabled, true);
        }

        public void PushDisableBlinkReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;

            if (m_DisableBlinkReasons.Add(reason))
            {
                SetBlinkWeight(0f);
                MarkOutputDirty();
            }
        }

        public void PopDisableBlinkReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;

            if (m_DisableBlinkReasons.Remove(reason) && IsAutoBlinkAllowed())
                BeginIdle();
        }

        public void OnLightweightActivate()
        {
            BeginIdle();
        }

        public void OnLightweightDeactivate()
        {
            m_BlinkCycleInitialized = false;
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            SetBlinkWeight(0f);
            ApplyOutputsNow();
        }

        public void TriggerBlink(bool includeSecondBlink = false)
        {
            ValidateSettings();
            m_BlinkCycleInitialized = true;
            m_QueuedExtraBlinks = includeSecondBlink ? 1 : 0;
            StartBlink();
        }

        public void Tick(float deltaTime)
        {
            if (BlinkConfig == null)
                BlinkConfig = new AnimLayerBlink();

            ApplyProfileTimingIfNeeded();
            BlinkConfig.Validate();

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            bool autoBlinkAllowed = IsAutoBlinkAllowed();
            UpdateAutoBlinkFade(autoBlinkAllowed, safeDeltaTime);

            if (!autoBlinkAllowed)
            {
                m_BlinkCycleInitialized = false;
                m_RawBlinkWeight = 0f;
                m_BlinkNormalizedTime = 0f;
                SetBlinkWeight(0f);
                return;
            }

            if (!m_BlinkCycleInitialized)
                BeginIdle();

            switch (m_BlinkPhase)
            {
                case BlinkPhase.Waiting:
                    TickWaiting(safeDeltaTime);
                    break;

                case BlinkPhase.Blinking:
                    TickBlinking(safeDeltaTime);
                    break;

                case BlinkPhase.DoubleBlinkGap:
                    TickDoubleBlinkGap(safeDeltaTime);
                    break;
            }

            SetBlinkWeight(m_RawBlinkWeight * m_AutoBlinkFade);
        }

        public float GetBlinkWeight()
        {
            return BlinkConfig != null ? BlinkConfig.BlinkWeight : 0f;
        }

        public void RefreshBlendShapeBindings()
        {
            MarkBlendShapeBindingsDirty();
            EnsureBlendShapeBindings();
            ApplyOutputsNow();
        }

        public void ConsumeBlendShapeValueMap(Dictionary<string, float> blendShapeValueMap)
        {
            if (blendShapeValueMap == null || profile == null)
                return;

            IReadOnlyList<EmojiControllerProfileSO.FacePoseParam> poseParams = profile.AutoBlinkPoseParams;
            if (profile.ApplyDefaultFacePose && poseParams != null)
            {
                for (int i = 0; i < poseParams.Count; ++i)
                {
                    EmojiControllerProfileSO.FacePoseParam poseParam = poseParams[i];
                    if (poseParam == null || !poseParam.Enabled || string.IsNullOrWhiteSpace(poseParam.BlendShapeName))
                        continue;

                    SetBlendShapeValue(blendShapeValueMap, poseParam.BlendShapeName, poseParam.Value);
                }
            }

            IReadOnlyList<EmojiControllerProfileSO.BlinkClosePoseConfig> blinkPoses = profile.BlinkClosePoseConfigs;
            if (blinkPoses == null)
                return;

            float blinkWeight = GetBlinkWeight();
            float blinkOutputScale = profile.AutoBlinkWeight / BlendShapeFullWeight;
            for (int i = 0; i < blinkPoses.Count; ++i)
            {
                EmojiControllerProfileSO.BlinkClosePoseConfig blinkPose = blinkPoses[i];
                if (blinkPose == null || string.IsNullOrWhiteSpace(blinkPose.BlendShapeName))
                    continue;

                float poseWeight = blinkWeight > 0f && blinkOutputScale > 0f
                    ? blinkPose.Evaluate(m_BlinkNormalizedTime, blinkWeight) * blinkOutputScale
                    : 0f;
                SetBlendShapeValue(blendShapeValueMap, blinkPose.BlendShapeName, poseWeight);
            }
        }

        public void ApplyDefaultFaceBlendShapes()
        {
            if (profile == null)
                return;

            EnsureBlendShapeBindings();
            m_BlendShapeValueMap.Clear();

            IReadOnlyList<EmojiControllerProfileSO.FacePoseParam> poseParams = profile.AutoBlinkPoseParams;
            if (poseParams != null)
            {
                for (int i = 0; i < poseParams.Count; ++i)
                {
                    EmojiControllerProfileSO.FacePoseParam poseParam = poseParams[i];
                    if (poseParam == null || !poseParam.Enabled || string.IsNullOrWhiteSpace(poseParam.BlendShapeName))
                        continue;

                    SetBlendShapeValue(m_BlendShapeValueMap, poseParam.BlendShapeName, poseParam.Value);
                }
            }

            m_LastAppliedBlendShapeValues.Clear();
            ApplyBlendShapeValueMap(m_BlendShapeValueMap);
        }

        public bool CanOverrideBlendShape(string blendShapeName)
        {
            return profile != null && profile.CanOverrideBlendShape(blendShapeName);
        }

        private void TickWaiting(float deltaTime)
        {
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            m_BlinkPhaseTimer -= deltaTime;
            if (m_BlinkPhaseTimer > 0f)
                return;

            m_QueuedExtraBlinks = UnityEngine.Random.value < BlinkConfig.DoubleBlinkProbability ? 1 : 0;
            StartBlink();
        }

        private void TickBlinking(float deltaTime)
        {
            m_BlinkPhaseTimer += deltaTime;

            float duration = Mathf.Max(MinimumBlinkDuration, BlinkConfig.BlinkingDuration);
            m_BlinkNormalizedTime = Mathf.Clamp01(m_BlinkPhaseTimer / duration);
            m_RawBlinkWeight = EvaluateBlinkPulseWeight(m_BlinkNormalizedTime);

            if (m_BlinkNormalizedTime < 1f)
                return;

            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            if (m_QueuedExtraBlinks > 0)
            {
                m_QueuedExtraBlinks--;
                m_BlinkPhase = BlinkPhase.DoubleBlinkGap;
                m_BlinkPhaseTimer = ResolveDoubleBlinkGap();
                return;
            }

            ScheduleNextBlink();
        }

        private void TickDoubleBlinkGap(float deltaTime)
        {
            m_RawBlinkWeight = 0f;
            m_BlinkNormalizedTime = 0f;
            m_BlinkPhaseTimer -= deltaTime;
            if (m_BlinkPhaseTimer > 0f)
                return;

            StartBlink();
        }

        private void StartBlink()
        {
            m_BlinkPhase = BlinkPhase.Blinking;
            m_BlinkPhaseTimer = 0f;
            m_BlinkNormalizedTime = 0f;
            m_RawBlinkWeight = 0f;
            SetBlinkWeight(0f);
        }

        private void ScheduleNextBlink()
        {
            m_BlinkPhase = BlinkPhase.Waiting;
            m_BlinkPhaseTimer = GetRandomBlinkGap();
            m_BlinkNormalizedTime = 0f;
            m_RawBlinkWeight = 0f;
        }

        private float GetRandomBlinkGap()
        {
            float minGap = Mathf.Max(0f, BlinkConfig.MinBlinkGap);
            float maxGap = Mathf.Max(minGap, BlinkConfig.MaxBlinkGap);
            if (Mathf.Approximately(minGap, maxGap))
                return minGap;

            return UnityEngine.Random.Range(minGap, maxGap);
        }

        private float ResolveDoubleBlinkGap()
        {
            return profile != null && profile.Timing != null
                ? profile.Timing.DoubleBlinkGap
                : 0.08f;
        }

        private void SetBlinkWeight(float weight)
        {
            if (BlinkConfig == null)
                return;

            float previousWeight = BlinkConfig.BlinkWeight;
            BlinkConfig.BlinkWeight = weight;
            if (Mathf.Abs(previousWeight - BlinkConfig.BlinkWeight) > WeightEpsilon)
                MarkOutputDirty();
        }

        private void UpdateAutoBlinkFade(bool autoBlinkAllowed, float deltaTime)
        {
            float target = autoBlinkAllowed ? 1f : 0f;
            EmojiControllerProfileSO.BlinkTiming timing = profile != null ? profile.Timing : null;
            float fadeTime = autoBlinkAllowed
                ? timing != null ? timing.FadeInAutoBlinkTime : 0f
                : timing != null ? timing.FadeOutAutoBlinkTime : 0f;

            if (fadeTime <= 0f || deltaTime <= 0f)
            {
                m_AutoBlinkFade = target;
                return;
            }

            m_AutoBlinkFade = Mathf.MoveTowards(m_AutoBlinkFade, target, deltaTime / fadeTime);
        }

        private void ApplyOutputsNow()
        {
            ApplyBlendShapeOutputs();
            ApplyBlinkWeightToAnimator();
        }

        private bool HasRuntimeOutputTarget()
        {
            if (applyBlinkToAnimatorLayer)
                return true;

            if (!applyBlendShapes || profile == null)
                return false;

            EnsureBlendShapeBindings();
            return m_BlendShapeBindingsByName.Count > 0;
        }

        private void ApplyBlendShapeOutputs()
        {
            if (!applyBlendShapes || profile == null)
                return;

            if (!m_BlendShapeOutputDirty && !reapplyBlendShapesEveryFrame)
                return;

            EnsureBlendShapeBindings();
            if (m_BlendShapeBindingsByName.Count == 0)
            {
                m_BlendShapeOutputDirty = false;
                return;
            }

            m_BlendShapeValueMap.Clear();
            ConsumeBlendShapeValueMap(m_BlendShapeValueMap);
            ApplyBlendShapeValueMap(m_BlendShapeValueMap);
            m_BlendShapeOutputDirty = false;
        }

        private void ApplyBlendShapeValueMap(Dictionary<string, float> blendShapeValueMap)
        {
            if (blendShapeValueMap == null || blendShapeValueMap.Count == 0)
                return;

            bool hasStaleBinding = false;
            foreach (KeyValuePair<string, float> blendShapeValue in blendShapeValueMap)
            {
                if (!m_BlendShapeBindingsByName.TryGetValue(blendShapeValue.Key, out List<BlendShapeBinding> bindings))
                    continue;

                float weight = Mathf.Clamp(blendShapeValue.Value, 0f, BlendShapeFullWeight);
                if (!reapplyBlendShapesEveryFrame
                    && m_LastAppliedBlendShapeValues.TryGetValue(blendShapeValue.Key, out float previousWeight)
                    && Mathf.Abs(previousWeight - weight) <= WeightEpsilon)
                {
                    continue;
                }

                for (int i = 0; i < bindings.Count; ++i)
                {
                    if (!TryApplyBlendShapeWeight(bindings[i], weight))
                        hasStaleBinding = true;
                }

                m_LastAppliedBlendShapeValues[blendShapeValue.Key] = weight;
            }

            if (hasStaleBinding)
                MarkBlendShapeBindingsDirty();
        }

        private void EnsureBlendShapeBindings()
        {
            if (!applyBlendShapes || profile == null || !m_BlendShapeBindingsDirty)
                return;

            m_BlendShapeBindingsDirty = false;
            m_BlendShapeBindingsByName.Clear();
            m_LastAppliedBlendShapeValues.Clear();
            ResolveFaceRenderer();

            Mesh mesh = faceRenderer != null ? faceRenderer.sharedMesh : null;
            if (mesh == null || mesh.blendShapeCount <= 0)
                return;

            for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; ++blendShapeIndex)
            {
                string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                if (!profile.CanOverrideBlendShape(blendShapeName))
                    continue;

                AddBlendShapeBinding(blendShapeName, new BlendShapeBinding(faceRenderer, blendShapeIndex));
            }
        }

        private void ResolveFaceRenderer()
        {
            if (!autoFindFaceRenderer || faceRenderer != null || m_FaceRendererResolveAttempted || profile == null)
                return;

            m_FaceRendererResolveAttempted = true;

            Transform searchRoot = faceSearchRoot != null ? faceSearchRoot : transform;
            if (searchRoot == null)
                return;

            m_SkinnedMeshRendererScratch.Clear();
            searchRoot.GetComponentsInChildren(true, m_SkinnedMeshRendererScratch);

            SkinnedMeshRenderer firstRendererWithConfiguredShape = null;
            SkinnedMeshRenderer firstProfileMatchedRenderer = null;
            for (int i = 0; i < m_SkinnedMeshRendererScratch.Count; ++i)
            {
                SkinnedMeshRenderer candidate = m_SkinnedMeshRendererScratch[i];
                Mesh mesh = candidate != null ? candidate.sharedMesh : null;
                if (mesh == null || mesh.blendShapeCount <= 0)
                    continue;

                bool hasConfiguredBlendShape = profile.HasConfiguredBlendShape(mesh);
                bool matchesProfile = profile.MatchesFaceRenderer(candidate);
                if (matchesProfile && hasConfiguredBlendShape)
                {
                    faceRenderer = candidate;
                    break;
                }

                if (firstRendererWithConfiguredShape == null && hasConfiguredBlendShape)
                    firstRendererWithConfiguredShape = candidate;

                if (firstProfileMatchedRenderer == null && matchesProfile)
                    firstProfileMatchedRenderer = candidate;
            }

            if (faceRenderer == null)
                faceRenderer = firstRendererWithConfiguredShape != null ? firstRendererWithConfiguredShape : firstProfileMatchedRenderer;

            m_SkinnedMeshRendererScratch.Clear();
        }

        private void AddBlendShapeBinding(string blendShapeName, BlendShapeBinding binding)
        {
            if (!m_BlendShapeBindingsByName.TryGetValue(blendShapeName, out List<BlendShapeBinding> bindings))
            {
                bindings = new List<BlendShapeBinding>(1);
                m_BlendShapeBindingsByName.Add(blendShapeName, bindings);
            }

            bindings.Add(binding);
        }

        private void MarkFaceBindingsDirty()
        {
            m_FaceRendererResolveAttempted = faceRenderer != null;
            MarkBlendShapeBindingsDirty();
        }

        private void MarkBlendShapeBindingsDirty()
        {
            m_BlendShapeBindingsDirty = true;
            m_LastAppliedBlendShapeValues.Clear();
            m_BlendShapeOutputDirty = true;
        }

        private void MarkOutputDirty()
        {
            m_BlendShapeOutputDirty = true;
            m_AnimatorOutputDirty = true;
        }

        private static bool TryApplyBlendShapeWeight(BlendShapeBinding binding, float weight)
        {
            SkinnedMeshRenderer renderer = binding.Renderer;
            Mesh mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount)
                return false;

            renderer.SetBlendShapeWeight(binding.Index, weight);
            return true;
        }

        private void ApplyBlinkWeightToAnimator()
        {
            if (!applyBlinkToAnimatorLayer)
                return;

            float targetWeight = GetBlinkWeight();
            if (!m_AnimatorOutputDirty
                && !float.IsNaN(m_LastAppliedAnimatorLayerWeight)
                && Mathf.Abs(m_LastAppliedAnimatorLayerWeight - targetWeight) <= WeightEpsilon)
            {
                return;
            }

            ResolveAnimator();
            if (animator == null)
            {
                m_AnimatorOutputDirty = false;
                return;
            }

            int layerIndex = ResolveAnimatorLayerIndex();
            if (layerIndex < 0 || layerIndex >= animator.layerCount)
            {
                m_AnimatorOutputDirty = false;
                return;
            }

            animator.SetLayerWeight(layerIndex, targetWeight);
            m_LastAppliedAnimatorLayerWeight = targetWeight;
            m_AnimatorOutputDirty = false;
        }

        private int ResolveAnimatorLayerIndex()
        {
            if (animator == null)
                return -1;

            if (blinkLayerIndex >= 0 && blinkLayerIndex < animator.layerCount)
                return blinkLayerIndex;

            string layerName = string.IsNullOrWhiteSpace(blinkLayerName) ? string.Empty : blinkLayerName.Trim();
            if (string.IsNullOrEmpty(layerName))
                return -1;

            if (m_CachedLayerAnimator == animator && string.Equals(m_CachedLayerName, layerName, StringComparison.Ordinal))
                return m_CachedLayerIndex;

            m_CachedLayerAnimator = animator;
            m_CachedLayerName = layerName;
            m_CachedLayerIndex = animator.GetLayerIndex(layerName);
            return m_CachedLayerIndex;
        }

        private void ResolveAnimator()
        {
            if (!autoFindAnimator || animator != null || m_AnimatorResolveAttempted)
                return;

            m_AnimatorResolveAttempted = true;
            animator = GetComponentInChildren<Animator>(true);
            InvalidateAnimatorLayerCache();
        }

        private void InvalidateAnimatorLayerCache()
        {
            m_CachedLayerAnimator = null;
            m_CachedLayerName = null;
            m_CachedLayerIndex = -1;
        }

        private void ValidateSettings()
        {
            if (BlinkConfig == null)
                BlinkConfig = new AnimLayerBlink();

            ApplyProfileTimingIfNeeded();
            BlinkConfig.Validate();
            blinkLayerIndex = Mathf.Max(-1, blinkLayerIndex);
        }

        private void ApplyProfileTimingIfNeeded()
        {
            if (!syncBlinkTimingFromProfile || profile == null || m_AppliedTimingProfile == profile)
                return;

            BlinkConfig.ApplyTiming(profile.Timing);
            m_AppliedTimingProfile = profile;
            MarkOutputDirty();
        }

        private bool IsAutoBlinkAllowed()
        {
            return BlinkConfig != null
                && BlinkConfig.EnableAutoBlink
                && m_DisableBlinkReasons.Count == 0;
        }

        private static void SetBlendShapeValue(Dictionary<string, float> blendShapeValueMap, string blendShapeName, float value)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
                return;

            blendShapeValueMap[blendShapeName] = Mathf.Clamp(value, 0f, BlendShapeFullWeight);
        }

        private static float EvaluateBlinkPulseWeight(float normalizedTime)
        {
            float triangle = 1f - Mathf.Abs(Mathf.Clamp01(normalizedTime) * 2f - 1f);
            return Mathf.SmoothStep(0f, 1f, triangle);
        }
    }
}
