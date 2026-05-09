using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Input;
using HoyoToon.Runtime.ScriptableObjects.Games;
using HoyoToon.Runtime.Utilities;
using UnityEngine;

namespace HoyoToon.Runtime.Character
{
    public enum LookAtDisableCause
    {
        None = 0,
        Manual = 1,
        MissingTarget = 2,
        OutOfConstraint = 3,
        AnimatorState = 4,
        External = 100,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Character/Look At Controller")]
    public class LookAtController : MonoBehaviour
    {
        private const float DirectionEpsilon = 0.000001f;
        private const float WeightEpsilon = 0.0001f;
        private const float BlendShapeFullWeight = 100f;
        private const float YawLimitReachedTolerance = 3f;
        private const float DefaultConstraintReentryMargin = 25f;
        private const float CameraResolveRetryInterval = 1f;
        private const int AnimatorLayerIndex = 0;
        private const int InitialCameraScratchCapacity = 8;

        private static Camera[] s_CameraScratch = new Camera[InitialCameraScratchCapacity];

        public enum TargetMode
        {
            MainCamera,
            ExplicitTransform,
            ExplicitPosition,
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

        [Header("Profile")]
        [SerializeField]
        private LookAtControllerProfileSO profile;

        [SerializeField]
        private bool syncSettingsFromProfile = true;

        [Header("Target")]
        [SerializeField]
        private TargetMode targetMode = TargetMode.MainCamera;

        [SerializeField]
        private Transform lookAtTarget;

        [SerializeField]
        private Camera cameraOverride;

        [SerializeField]
        private bool autoLookAtOnEnable = true;

        [Header("Bones")]
        [SerializeField]
        private Transform boneSearchRoot;

        [SerializeField]
        private Transform constraintRoot;

        [SerializeField]
        private Transform head;

        [SerializeField]
        private Transform leftEye;

        [SerializeField]
        private Transform rightEye;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private bool autoFindAnimator = true;

        [Header("Input")]
        [SerializeField]
        private bool enableInputToggle = true;

        [Header("Original Controller Settings")]
        public string[] LookAtStates = Array.Empty<string>();

        [Min(0f)]
        [Tooltip("Delay before look-at is allowed after the configured state/target becomes valid.")]
        public float lookWaitTime;

        [Min(0f)]
        [Tooltip("Additional idle showcase delay. Kept separate to mirror the original ManikinLookAtController data.")]
        public float idleShowWaitTime;

        public float upWeight;
        public float forwardWeight;
        public float rightWeight;

        [Min(0f)]
        public float OutOfConstraintTimer = 0.2f;

        [Tooltip("When enabled, targets outside the yaw/pitch cone fade the solver out after Out Of Constraint Timer. When disabled, targets are clamped to the cone instead.")]
        public bool DisableWhenOutOfConstraint;

        [Min(0f)]
        [Tooltip("Extra yaw degrees outside the active yaw limit where the controller may reacquire from an out-of-constraint reset. With a 50 degree yaw limit, 25 re-enters around raw yaw 75.")]
        public float ConstraintReentryMargin = DefaultConstraintReentryMargin;

        public LookAtControllerProfileSO.LookAtTargetConstraint Constraint =
            new LookAtControllerProfileSO.LookAtTargetConstraint();

        public LookAtControllerProfileSO.LookAtTargetConstraint AnotherConstraint =
            new LookAtControllerProfileSO.LookAtTargetConstraint();

        [Tooltip("When enabled, Another Constraint is blended in at close range. This mirrors the original controller's second constraint slot and prevents near-camera pitch spikes.")]
        public bool UseAnotherConstraintAtCloseRange;

        [Min(0f)]
        public float CloseConstraintDistance = 1.25f;

        [Min(0f)]
        public float CloseConstraintBlendRange = 0.75f;

        [Min(0f)]
        public float ConstraintSpeed = 1f;

        public AnimationCurve ConstraintCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Min(0f)]
        public float OutConstraintSpeed = 1f;

        public AnimationCurve OutConstraintCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private LookAtControllerProfileSO m_AppliedProfile;
        private Camera m_CachedMainCamera;
        private float m_NextCameraResolveTime;
        private float m_CurrentWeight;
        private float m_ConstraintBlend = 1f;
        private float m_OutConstraintBlend = 1f;
        private float m_OutOfConstraintElapsed;
        private float m_LookAllowedElapsed;
        private bool m_TargetEnabled;
        private bool m_HasExplicitLookAtPosition;
        private Vector3 m_ExplicitLookAtPosition;
        private bool m_HasFilteredLocalDirection;
        private Vector3 m_FilteredLocalDirection = Vector3.forward;
        private Vector2 m_FilteredEyeYawPitch;
        private Vector3 m_ResolvedLocalForwardAxis = Vector3.forward;
        private Vector3 m_ResolvedLocalUpAxis = Vector3.up;
        private bool m_BonesDirty = true;
        private bool m_HasFilteredEyeYawPitch;
        private bool m_EyeBlendShapeBindingsDirty = true;
        private bool m_DrivenBoneBasePoseDirty = true;
        private bool m_AnimatorResolveAttempted;
        private HoyoToonInputManager m_InputManager;
        private int[] m_LookAtStateHashes = Array.Empty<int>();
        private readonly HashSet<LookAtDisableCause> m_DisableCauses = new HashSet<LookAtDisableCause>();
        private readonly List<Transform> m_BodyBones = new List<Transform>(4);
        private readonly List<Vector3> m_BodyBoneForwardAxes = new List<Vector3>(4);
        private readonly List<Vector3> m_BodyBoneUpAxes = new List<Vector3>(4);
        private readonly List<Transform> m_EyeBones = new List<Transform>(2);
        private readonly List<Vector3> m_EyeBoneForwardAxes = new List<Vector3>(2);
        private readonly List<Transform> m_DrivenBones = new List<Transform>(8);
        private readonly List<Quaternion> m_DrivenBoneBaseLocalRotations = new List<Quaternion>(8);
        private readonly List<Transform> m_HierarchyScratch = new List<Transform>(8);
        private readonly List<SkinnedMeshRenderer> m_SkinnedMeshRendererScratch = new List<SkinnedMeshRenderer>(8);
        private readonly List<string> m_BlendShapeNameScratch = new List<string>(8);
        private readonly Dictionary<string, List<BlendShapeBinding>> m_EyeBlendShapeBindingsByName =
            new Dictionary<string, List<BlendShapeBinding>>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_LastAppliedEyeBlendShapeValues =
            new Dictionary<string, float>(StringComparer.Ordinal);

        public LookAtControllerProfileSO Profile
        {
            get => profile;
            set
            {
                if (profile == value)
                    return;

                profile = value;
                m_AppliedProfile = null;
                MarkBonesDirty();
                ApplyProfileSettingsIfNeeded();
            }
        }

        public bool StaticEnabled => isActiveAndEnabled && profile != null;
        public bool IsEnabled => StaticEnabled && !m_DisableCauses.Contains(LookAtDisableCause.Manual);
        public bool IsActive => m_CurrentWeight > WeightEpsilon;
        public bool IsFullWeight => m_CurrentWeight >= 1f - WeightEpsilon;
        public LookAtDisableCause LookAtDisableCause => ResolvePrimaryDisableCause();
        public Transform LookAtTarget
        {
            get => lookAtTarget;
            set
            {
                lookAtTarget = value;
                targetMode = TargetMode.ExplicitTransform;
                m_HasExplicitLookAtPosition = false;
            }
        }

        public Vector3 LookAtIKRotation => head != null ? head.eulerAngles : Vector3.zero;

        public void ConfigureProfile(
            LookAtControllerProfileSO nextProfile,
            Transform nextTarget = null,
            Camera nextCamera = null)
        {
            profile = nextProfile;
            if (nextTarget != null)
            {
                lookAtTarget = nextTarget;
                targetMode = TargetMode.ExplicitTransform;
                m_HasExplicitLookAtPosition = false;
            }

            if (nextCamera != null)
            {
                cameraOverride = nextCamera;
                targetMode = TargetMode.MainCamera;
            }

            m_AppliedProfile = null;
            MarkBonesDirty();
            ApplyProfileSettingsIfNeeded();
        }

        public void EnableLookAt(Transform target)
        {
            if (target != null)
            {
                lookAtTarget = target;
                targetMode = TargetMode.ExplicitTransform;
                m_HasExplicitLookAtPosition = false;
            }

            m_TargetEnabled = true;
            SetDisableWithCause(LookAtDisableCause.Manual, false);
        }

        public void DisableLookAt()
        {
            m_TargetEnabled = false;
            SetDisableWithCause(LookAtDisableCause.Manual, true);
        }

        public void DisableLookAtAndReset()
        {
            DisableLookAt();
            ResetLookAtState();
        }

        public void ToggleLookAt()
        {
            if (m_DisableCauses.Contains(LookAtDisableCause.Manual) || !m_TargetEnabled)
            {
                m_LookAllowedElapsed = 0f;
                EnableLookAt(null);
                return;
            }

            DisableLookAtAndReset();
        }

        public void HeadStopLookAt(bool immediate)
        {
            m_TargetEnabled = false;
            if (immediate)
                ResetLookAtState();
        }

        public void HeadLookAt(Vector3 position, bool force, LookAtControllerProfileSO.LookAtTargetConstraint constraint)
        {
            if (force)
                m_CurrentWeight = 1f;

            if (constraint != null)
                Constraint = constraint;

            m_ExplicitLookAtPosition = position;
            m_HasExplicitLookAtPosition = true;
            targetMode = TargetMode.ExplicitPosition;
            m_TargetEnabled = true;
            SetDisableWithCause(LookAtDisableCause.Manual, false);
        }

        public void HeadLookAt(
            Transform target,
            bool force,
            bool useQuicklySlerpSpeed,
            LookAtControllerProfileSO.LookAtTargetConstraint constraint)
        {
            if (force)
                m_CurrentWeight = 1f;

            if (constraint != null)
                Constraint = constraint;

            if (target != null)
            {
                lookAtTarget = target;
                targetMode = TargetMode.ExplicitTransform;
                m_HasExplicitLookAtPosition = false;
            }

            m_TargetEnabled = true;
            SetDisableWithCause(LookAtDisableCause.Manual, false);
        }

        public void SetDisableWithCause(LookAtDisableCause cause, bool disabled)
        {
            if (cause == LookAtDisableCause.None)
                return;

            if (disabled)
                m_DisableCauses.Add(cause);
            else
                m_DisableCauses.Remove(cause);
        }

        public bool IsNearlyLookAt()
        {
            if (head == null || !TryResolveLookAtPosition(out Vector3 targetPosition))
                return false;

            Vector3 direction = targetPosition - head.position;
            if (direction.sqrMagnitude <= DirectionEpsilon)
                return false;

            Vector3 headForward = head.TransformDirection(m_ResolvedLocalForwardAxis);
            return Vector3.Angle(headForward, direction) <= 2f;
        }

        public Vector3 GetLookAtPosition()
        {
            return TryResolveLookAtPosition(out Vector3 targetPosition) ? targetPosition : Vector3.zero;
        }

        public Vector3 GetHeadForwardPosition()
        {
            return head != null ? head.position + head.TransformDirection(m_ResolvedLocalForwardAxis) : Vector3.zero;
        }

        private void Reset()
        {
            boneSearchRoot = transform;
            m_TargetEnabled = autoLookAtOnEnable;
            MarkBonesDirty();
        }

        private void Awake()
        {
            ApplyProfileSettingsIfNeeded();
            RebuildLookAtStateHashes();
            ResolveAnimator();
            EnsureBones();
            BindInputManager();
        }

        private void OnEnable()
        {
            ApplyProfileSettingsIfNeeded();
            ResolveAnimator();
            EnsureBones();
            BindInputManager();
            m_TargetEnabled = autoLookAtOnEnable;
            m_CurrentWeight = 0f;
            m_LookAllowedElapsed = 0f;
            m_OutOfConstraintElapsed = 0f;
        }

        private void OnDisable()
        {
            UnbindInputManager();
            RestoreDrivenBoneBasePoseIfNeeded();
            m_CurrentWeight = 0f;
            m_HasFilteredLocalDirection = false;
            m_HasFilteredEyeYawPitch = false;
            ResetEyeBlendShapes();
        }

        private void OnValidate()
        {
            m_AppliedProfile = null;
            m_AnimatorResolveAttempted = false;
            MarkBonesDirty();
            ApplyProfileSettingsIfNeeded();
            RebuildLookAtStateHashes();
        }

        private void OnTransformChildrenChanged()
        {
            MarkBonesDirty();
            m_AnimatorResolveAttempted = false;
        }

        private void LateUpdate()
        {
            DoLateUpdate(Time.deltaTime);
        }

        public void DoLateUpdate(float deltaTime)
        {
            ApplyProfileSettingsIfNeeded();

            LookAtControllerProfileSO.LookAtIKSettings lookAt = ResolveLookAtSettings();
            if (lookAt.StopLookAtIkUpdate)
                return;

            EnsureBones();
            if (head == null)
                return;

            RestoreDrivenBoneBasePoseIfNeeded();

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            bool hasTarget = TryResolveLookAtPosition(out Vector3 targetPosition);
            bool stateAllowed = IsAnimatorStateAllowed();

            Vector3 constrainedLocalDirection = m_FilteredLocalDirection;
            bool inConstraint = false;
            float yawOutsideAmount = 0f;
            bool canTrackConstraintTarget = !DisableWhenOutOfConstraint;
            if (hasTarget)
            {
                constrainedLocalDirection = ResolveConstrainedLocalDirection(
                    targetPosition,
                    out inConstraint,
                    out yawOutsideAmount);
                bool alreadyDisabledByConstraint = m_DisableCauses.Contains(LookAtDisableCause.OutOfConstraint);
                float constraintReentryMargin = Mathf.Max(0f, ConstraintReentryMargin);
                bool yawWithinReentryWindow = yawOutsideAmount <= constraintReentryMargin;
                bool canTrackReentryWindow = !inConstraint && yawWithinReentryWindow;
                if (alreadyDisabledByConstraint && (inConstraint || canTrackReentryWindow))
                    SeedFilteredDirection(constrainedLocalDirection);

                bool holdUntilYawLimit = DisableWhenOutOfConstraint
                    && !alreadyDisabledByConstraint
                    && !inConstraint
                    && !yawWithinReentryWindow
                    && !HasFilteredDirectionReachedYawLimit(constrainedLocalDirection);
                canTrackConstraintTarget = !DisableWhenOutOfConstraint
                    || inConstraint
                    || canTrackReentryWindow
                    || holdUntilYawLimit;
                UpdateConstraintState(canTrackConstraintTarget, safeDeltaTime);
            }
            else
            {
                UpdateConstraintState(false, safeDeltaTime);
            }

            SetDisableWithCause(LookAtDisableCause.MissingTarget, !hasTarget);
            SetDisableWithCause(LookAtDisableCause.AnimatorState, !stateAllowed);

            bool outOfConstraintTimedOut = OutOfConstraintTimer <= 0f || m_OutOfConstraintElapsed > OutOfConstraintTimer;
            bool disableOutOfConstraint = DisableWhenOutOfConstraint
                && outOfConstraintTimedOut
                && hasTarget
                && !canTrackConstraintTarget;
            SetDisableWithCause(LookAtDisableCause.OutOfConstraint, disableOutOfConstraint);

            bool canLookAt = m_TargetEnabled && hasTarget && stateAllowed && m_DisableCauses.Count == 0;
            if (canLookAt)
                m_LookAllowedElapsed += safeDeltaTime;
            else
                m_LookAllowedElapsed = 0f;

            float requiredWaitTime = Mathf.Max(0f, lookWaitTime);
            if (canLookAt && m_LookAllowedElapsed < requiredWaitTime)
                canLookAt = false;

            float targetWeight = canLookAt ? 1f : 0f;
            float fadeTime = targetWeight > m_CurrentWeight ? lookAt.FadeInTime : lookAt.FadeOutTime;
            m_CurrentWeight = MoveWeight(m_CurrentWeight, targetWeight, fadeTime, safeDeltaTime);
            if (m_CurrentWeight <= WeightEpsilon)
            {
                m_HasFilteredLocalDirection = false;
                ApplyEyeFollow(targetPosition, hasTarget, 0f, safeDeltaTime);
                return;
            }

            constrainedLocalDirection = FilterLocalDirection(constrainedLocalDirection, lookAt, canLookAt, safeDeltaTime);
            float constraintWeight = EvaluateCurve(ConstraintCurve, m_ConstraintBlend)
                * EvaluateCurve(OutConstraintCurve, m_OutConstraintBlend);
            float solverWeight = m_CurrentWeight * constraintWeight;
            ApplyLookAt(constrainedLocalDirection, solverWeight);
            ApplyEyeFollow(targetPosition, hasTarget, solverWeight, safeDeltaTime);
        }

        private void BindInputManager()
        {
            UnbindInputManager();
            if (!enableInputToggle)
                return;

            m_InputManager = HoyoToonInputManager.Instance;
            m_InputManager.ToggleLookAtPressed += HandleToggleLookAtPressed;
        }

        private void UnbindInputManager()
        {
            if (m_InputManager == null)
                return;

            m_InputManager.ToggleLookAtPressed -= HandleToggleLookAtPressed;
            m_InputManager = null;
        }

        private void HandleToggleLookAtPressed()
        {
            if (isActiveAndEnabled && enableInputToggle)
                ToggleLookAt();
        }

        private void ResetLookAtState()
        {
            m_CurrentWeight = 0f;
            m_LookAllowedElapsed = 0f;
            m_OutOfConstraintElapsed = 0f;
            m_ConstraintBlend = 1f;
            m_OutConstraintBlend = 1f;
            m_HasFilteredLocalDirection = false;
            m_HasFilteredEyeYawPitch = false;
            RestoreDrivenBoneBasePoseIfNeeded();
            ResetEyeBlendShapes();
        }

        private void ApplyProfileSettingsIfNeeded()
        {
            if (!syncSettingsFromProfile || profile == null || m_AppliedProfile == profile)
                return;

            CopyProfileSettings(profile);
            m_AppliedProfile = profile;
            RebuildLookAtStateHashes();
        }

        private void CopyProfileSettings(LookAtControllerProfileSO source)
        {
            LookAtStates = CopyStrings(source.LookAtStates);
            lookWaitTime = source.LookWaitTime;
            idleShowWaitTime = source.IdleShowWaitTime;
            upWeight = source.UpWeight;
            forwardWeight = source.ForwardWeight;
            rightWeight = source.RightWeight;
            OutOfConstraintTimer = source.OutOfConstraintTimer;
            DisableWhenOutOfConstraint = source.DisableWhenOutOfConstraint;
            ConstraintReentryMargin = source.ConstraintReentryMargin;
            Constraint = source.Constraint;
            AnotherConstraint = source.AnotherConstraint;
            UseAnotherConstraintAtCloseRange = source.UseAnotherConstraintAtCloseRange;
            CloseConstraintDistance = source.CloseConstraintDistance;
            CloseConstraintBlendRange = source.CloseConstraintBlendRange;
            ConstraintSpeed = source.ConstraintSpeed;
            ConstraintCurve = source.ConstraintCurve;
            OutConstraintSpeed = source.OutConstraintSpeed;
            OutConstraintCurve = source.OutConstraintCurve;
            MarkBonesDirty();
        }

        private void ResolveAnimator()
        {
            if (!autoFindAnimator || animator != null || m_AnimatorResolveAttempted)
                return;

            m_AnimatorResolveAttempted = true;
            animator = GetComponentInChildren<Animator>(true);
        }

        private void EnsureBones()
        {
            if (!m_BonesDirty)
                return;

            m_BonesDirty = false;
            m_BodyBones.Clear();
            m_BodyBoneForwardAxes.Clear();
            m_BodyBoneUpAxes.Clear();
            m_EyeBones.Clear();
            m_EyeBoneForwardAxes.Clear();

            LookAtControllerProfileSO.LookAtSolverSettings solver = ResolveSolverSettings();
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow = ResolveEyeFollowSettings();
            Transform searchRoot = boneSearchRoot != null ? boneSearchRoot : transform;
            if (constraintRoot == null)
                constraintRoot = transform;

            if (head == null)
                head = TransformSearchUtility.FindChildRecursive(searchRoot, solver.HeadBoneNames);

            if (head == null)
                return;

            CollectBodyBones(head, solver.SpineNum);
            Transform referenceRoot = ResolveReferenceRoot();
            m_ResolvedLocalForwardAxis = solver.AutoDetectLocalForwardAxis
                ? ResolveLocalForwardAxis(head, referenceRoot, solver.LocalForwardAxis)
                : solver.LocalForwardAxis;
            m_ResolvedLocalUpAxis = solver.AutoDetectLocalForwardAxis
                ? ResolveLocalUpAxis(head, referenceRoot, solver.LocalUpAxis)
                : solver.LocalUpAxis;

            for (int i = 0; i < m_BodyBones.Count; ++i)
            {
                Transform bodyBone = m_BodyBones[i];
                Vector3 axis = solver.AutoDetectLocalForwardAxis
                    ? ResolveLocalForwardAxis(bodyBone, referenceRoot, solver.LocalForwardAxis)
                    : solver.LocalForwardAxis;
                m_BodyBoneForwardAxes.Add(axis);

                Vector3 upAxis = solver.AutoDetectLocalForwardAxis
                    ? ResolveLocalUpAxis(bodyBone, referenceRoot, solver.LocalUpAxis)
                    : solver.LocalUpAxis;
                m_BodyBoneUpAxes.Add(upAxis);
            }

            if (eyeFollow.Enabled && eyeFollow.RotateEyeBones)
                ResolveEyeBones(searchRoot, referenceRoot, eyeFollow);

            RebuildDrivenBoneBasePoseCache();
        }

        private void CollectBodyBones(Transform headBone, int spineNum)
        {
            m_HierarchyScratch.Clear();
            Transform current = headBone != null ? headBone.parent : null;
            while (current != null && current != transform && m_HierarchyScratch.Count < spineNum)
            {
                m_HierarchyScratch.Add(current);
                current = current.parent;
            }

            for (int i = m_HierarchyScratch.Count - 1; i >= 0; --i)
                m_BodyBones.Add(m_HierarchyScratch[i]);

            m_HierarchyScratch.Clear();
        }

        private void ResolveEyeBones(
            Transform searchRoot,
            Transform referenceRoot,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            if (leftEye == null)
                leftEye = TransformSearchUtility.FindChildRecursive(searchRoot, eyeFollow.LeftEyeBoneNames);

            if (rightEye == null)
                rightEye = TransformSearchUtility.FindChildRecursive(searchRoot, eyeFollow.RightEyeBoneNames);

            TryAddEyeBone(leftEye, referenceRoot, eyeFollow);
            TryAddEyeBone(rightEye, referenceRoot, eyeFollow);
        }

        private void TryAddEyeBone(
            Transform eyeBone,
            Transform referenceRoot,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            if (eyeBone == null || m_EyeBones.Contains(eyeBone))
                return;

            Vector3 axis = eyeFollow.AutoDetectLocalForwardAxis
                ? ResolveLocalForwardAxis(eyeBone, referenceRoot, eyeFollow.LocalForwardAxis)
                : eyeFollow.LocalForwardAxis;

            m_EyeBones.Add(eyeBone);
            m_EyeBoneForwardAxes.Add(axis);
        }

        private void RebuildDrivenBoneBasePoseCache()
        {
            m_DrivenBones.Clear();
            m_DrivenBoneBaseLocalRotations.Clear();

            for (int i = 0; i < m_BodyBones.Count; ++i)
                AddDrivenBoneBasePose(m_BodyBones[i]);

            AddDrivenBoneBasePose(head);

            for (int i = 0; i < m_EyeBones.Count; ++i)
                AddDrivenBoneBasePose(m_EyeBones[i]);

            m_DrivenBoneBasePoseDirty = false;
        }

        private void AddDrivenBoneBasePose(Transform bone)
        {
            if (bone == null || m_DrivenBones.Contains(bone))
                return;

            m_DrivenBones.Add(bone);
            m_DrivenBoneBaseLocalRotations.Add(bone.localRotation);
        }

        private void RestoreDrivenBoneBasePoseIfNeeded()
        {
            bool shouldUseCachedBasePose = !IsAnimatorDrivingPose();
            if (!shouldUseCachedBasePose)
                return;

            if (m_DrivenBoneBasePoseDirty)
                RebuildDrivenBoneBasePoseCache();

            for (int i = 0; i < m_DrivenBones.Count; ++i)
            {
                Transform bone = m_DrivenBones[i];
                if (bone == null || i >= m_DrivenBoneBaseLocalRotations.Count)
                    continue;

                bone.localRotation = m_DrivenBoneBaseLocalRotations[i];
            }
        }

        private bool IsAnimatorDrivingPose()
        {
            ResolveAnimator();
            return animator != null
                && animator.enabled
                && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null;
        }

        private bool TryResolveLookAtPosition(out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;

            Transform target = null;
            switch (targetMode)
            {
                case TargetMode.ExplicitPosition:
                    if (!m_HasExplicitLookAtPosition)
                        return false;

                    targetPosition = m_ExplicitLookAtPosition;
                    return true;

                case TargetMode.ExplicitTransform:
                    target = lookAtTarget;
                    break;

                case TargetMode.MainCamera:
                    Camera targetCamera = ResolveCamera();
                    target = targetCamera != null ? targetCamera.transform : null;
                    break;
            }

            if (target == null)
                return false;

            targetPosition = target.position
                + target.up * upWeight
                + target.forward * forwardWeight
                + target.right * rightWeight;
            return true;
        }

        private Camera ResolveCamera()
        {
            if (cameraOverride != null)
                return cameraOverride;

            if (m_CachedMainCamera != null)
                return m_CachedMainCamera;

            float now = Time.unscaledTime;
            if (now < m_NextCameraResolveTime)
                return null;

            m_NextCameraResolveTime = now + CameraResolveRetryInterval;
            m_CachedMainCamera = Camera.main;
            if (m_CachedMainCamera == null)
                m_CachedMainCamera = ResolveSceneCamera();

            return m_CachedMainCamera;
        }

        private Camera ResolveSceneCamera()
        {
            int cameraCount = Camera.allCamerasCount;
            if (cameraCount <= 0)
                return null;

            if (s_CameraScratch == null || s_CameraScratch.Length < cameraCount)
                s_CameraScratch = new Camera[cameraCount];

            int count = Camera.GetAllCameras(s_CameraScratch);
            Camera fallback = null;
            for (int i = 0; i < count; ++i)
            {
                Camera candidate = s_CameraScratch[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (candidate.gameObject.scene == gameObject.scene)
                {
                    if (string.Equals(candidate.name, "Camera", StringComparison.OrdinalIgnoreCase))
                        return candidate;

                    fallback ??= candidate;
                }
            }

            if (fallback != null)
                return fallback;

            for (int i = 0; i < count; ++i)
            {
                Camera candidate = s_CameraScratch[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                    return candidate;
            }

            return null;
        }

        private Vector3 ResolveConstrainedLocalDirection(
            Vector3 targetPosition,
            out bool inConstraint,
            out float yawOutsideAmount)
        {
            yawOutsideAmount = 0f;
            Transform root = ResolveReferenceRoot();
            Vector3 worldDirection = targetPosition - head.position;
            float targetDistance = worldDirection.magnitude;
            if (targetDistance <= DirectionEpsilon)
            {
                inConstraint = false;
                return m_HasFilteredLocalDirection ? m_FilteredLocalDirection : Vector3.forward;
            }

            Vector3 localDirection = root.InverseTransformDirection(worldDirection / targetDistance);
            if (localDirection.sqrMagnitude <= DirectionEpsilon)
            {
                inConstraint = false;
                return m_HasFilteredLocalDirection ? m_FilteredLocalDirection : Vector3.forward;
            }

            localDirection.Normalize();
            Vector2 yawPitch = DirectionToYawPitch(localDirection);
            yawPitch = ClampYawPitchToActiveConstraint(yawPitch, targetDistance, out inConstraint, out yawOutsideAmount);

            return YawPitchToDirection(yawPitch.x, yawPitch.y);
        }

        private void UpdateConstraintState(bool inConstraint, float deltaTime)
        {
            if (inConstraint)
            {
                m_OutOfConstraintElapsed = 0f;
                m_ConstraintBlend = MoveBlend(m_ConstraintBlend, 1f, ConstraintSpeed, deltaTime);
                m_OutConstraintBlend = MoveBlend(m_OutConstraintBlend, 1f, OutConstraintSpeed, deltaTime);
                return;
            }

            m_OutOfConstraintElapsed += deltaTime;
            m_ConstraintBlend = MoveBlend(m_ConstraintBlend, 0f, ConstraintSpeed, deltaTime);
            m_OutConstraintBlend = MoveBlend(m_OutConstraintBlend, 0f, OutConstraintSpeed, deltaTime);
        }

        private bool HasFilteredDirectionReachedYawLimit(Vector3 constrainedLocalDirection)
        {
            if (!m_HasFilteredLocalDirection)
                return false;

            Vector2 filteredYawPitch = DirectionToYawPitch(m_FilteredLocalDirection);
            Vector2 constrainedYawPitch = DirectionToYawPitch(constrainedLocalDirection);
            return Mathf.Abs(Mathf.DeltaAngle(filteredYawPitch.x, constrainedYawPitch.x)) <= YawLimitReachedTolerance;
        }

        private bool SeedFilteredDirection(Vector3 constrainedLocalDirection)
        {
            if (constrainedLocalDirection.sqrMagnitude <= DirectionEpsilon)
                return false;

            m_FilteredLocalDirection = constrainedLocalDirection.normalized;
            m_HasFilteredLocalDirection = true;
            return true;
        }

        private Vector2 ClampYawPitchToActiveConstraint(
            Vector2 yawPitch,
            float targetDistance,
            out bool inConstraint,
            out float yawOutsideAmount)
        {
            yawOutsideAmount = 0f;
            LookAtControllerProfileSO.LookAtTargetConstraint normalConstraint = Constraint ?? ResolveProfileConstraint();
            if (normalConstraint == null)
            {
                inConstraint = true;
                return yawPitch;
            }

            float pitchUp = normalConstraint.PitchUp;
            float pitchDown = normalConstraint.PitchDown;
            float yawLeft = normalConstraint.YawLeft;
            float yawRight = normalConstraint.YawRight;

            float closeConstraintBlend = ResolveCloseConstraintBlend(targetDistance);
            LookAtControllerProfileSO.LookAtTargetConstraint closeConstraint = AnotherConstraint;
            if (closeConstraintBlend > 0f && closeConstraint != null)
            {
                pitchUp = Mathf.Lerp(pitchUp, closeConstraint.PitchUp, closeConstraintBlend);
                pitchDown = Mathf.Lerp(pitchDown, closeConstraint.PitchDown, closeConstraintBlend);
                yawLeft = Mathf.Lerp(yawLeft, closeConstraint.YawLeft, closeConstraintBlend);
                yawRight = Mathf.Lerp(yawRight, closeConstraint.YawRight, closeConstraintBlend);
            }

            if (yawPitch.x < -yawLeft)
                yawOutsideAmount = -yawLeft - yawPitch.x;
            else if (yawPitch.x > yawRight)
                yawOutsideAmount = yawPitch.x - yawRight;

            inConstraint = yawOutsideAmount <= DirectionEpsilon;
            return new Vector2(
                Mathf.Clamp(yawPitch.x, -yawLeft, yawRight),
                Mathf.Clamp(yawPitch.y, -pitchDown, pitchUp));
        }

        private float ResolveCloseConstraintBlend(float targetDistance)
        {
            if (!UseAnotherConstraintAtCloseRange || AnotherConstraint == null)
                return 0f;

            float closeDistance = Mathf.Max(0f, CloseConstraintDistance);
            float blendRange = Mathf.Max(0f, CloseConstraintBlendRange);
            if (targetDistance <= closeDistance)
                return 1f;

            if (blendRange <= DirectionEpsilon)
                return 0f;

            return 1f - Mathf.Clamp01((targetDistance - closeDistance) / blendRange);
        }

        private Vector3 FilterLocalDirection(
            Vector3 localDirection,
            LookAtControllerProfileSO.LookAtIKSettings lookAt,
            bool canLookAt,
            float deltaTime)
        {
            if (localDirection.sqrMagnitude <= DirectionEpsilon)
                localDirection = Vector3.forward;

            localDirection.Normalize();
            if (!m_HasFilteredLocalDirection)
            {
                m_FilteredLocalDirection = localDirection;
                m_HasFilteredLocalDirection = true;
                return m_FilteredLocalDirection;
            }

            Vector3 axisFiltered = localDirection;
            axisFiltered.x = Mathf.Lerp(localDirection.x, m_FilteredLocalDirection.x, lookAt.LeftRightFilterIntensity);
            axisFiltered.y = Mathf.Lerp(localDirection.y, m_FilteredLocalDirection.y, lookAt.UpDownFilterIntensity);
            if (axisFiltered.sqrMagnitude <= DirectionEpsilon)
                axisFiltered = localDirection;

            axisFiltered.Normalize();
            float speed = canLookAt ? lookAt.Speed : lookAt.StopSpeed;
            float t = ExponentialT(speed, deltaTime);
            m_FilteredLocalDirection = Vector3.Slerp(m_FilteredLocalDirection, axisFiltered, t).normalized;
            return m_FilteredLocalDirection;
        }

        private void ApplyLookAt(Vector3 constrainedLocalDirection, float weight)
        {
            if (head == null || weight <= WeightEpsilon)
                return;

            LookAtControllerProfileSO.LookAtSolverSettings solver = ResolveSolverSettings();
            Transform root = ResolveReferenceRoot();
            Vector2 yawPitch = DirectionToYawPitch(constrainedLocalDirection);

            if (m_BodyBones.Count > 0 && solver.BodyWeight > 0f)
            {
                float bodyPitchFactor = yawPitch.y >= 0f ? solver.BodyPitchUpFactor : solver.BodyPitchDownFactor;
                Vector3 bodyDirection = BuildWorldDirection(
                    root,
                    yawPitch.x,
                    yawPitch.y * bodyPitchFactor,
                    solver.BodyDefaultFwdWeight);
                float bodyWeightPerBone = weight * solver.BodyWeight / m_BodyBones.Count;
                for (int i = 0; i < m_BodyBones.Count; ++i)
                {
                    Transform bodyBone = m_BodyBones[i];
                    Vector3 bodyAxis = i < m_BodyBoneForwardAxes.Count ? m_BodyBoneForwardAxes[i] : m_ResolvedLocalForwardAxis;
                    Vector3 bodyUpAxis = i < m_BodyBoneUpAxes.Count ? m_BodyBoneUpAxes[i] : m_ResolvedLocalUpAxis;
                    ApplyBoneLookAt(
                        bodyBone,
                        bodyDirection,
                        bodyWeightPerBone,
                        solver.BodyRotMax,
                        bodyAxis,
                        bodyUpAxis,
                        root.up,
                        EvaluateCurve(solver.UprightConstraintCurve, bodyWeightPerBone));
                }
            }

            Vector3 headDirection = BuildWorldDirection(
                root,
                yawPitch.x,
                yawPitch.y * solver.HeadPitchFactor,
                solver.HeadDefaultFwdWeight);
            float headWeight = weight * solver.HeadWeight;
            ApplyBoneLookAt(
                head,
                headDirection,
                headWeight,
                solver.HeadRotMax,
                m_ResolvedLocalForwardAxis,
                m_ResolvedLocalUpAxis,
                root.up,
                EvaluateCurve(solver.UprightConstraintCurve, headWeight));
        }

        private void ApplyEyeFollow(Vector3 targetPosition, bool hasTarget, float weight, float deltaTime)
        {
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow = ResolveEyeFollowSettings();
            if (!eyeFollow.Enabled)
            {
                ResetEyeBlendShapes();
                return;
            }

            if (!eyeFollow.DriveBlendShapes || !eyeFollow.HasBlendShapes)
                ResetEyeBlendShapes();

            if (head == null || !hasTarget || weight <= WeightEpsilon || eyeFollow.Weight <= WeightEpsilon)
            {
                m_HasFilteredEyeYawPitch = false;
                ApplyEyeBlendShapes(Vector2.zero, 0f, eyeFollow);
                return;
            }

            EnsureBones();

            float eyeWeight = Mathf.Clamp01(weight * eyeFollow.Weight);
            Vector2 targetYawPitch = ResolveHeadLocalYawPitch(targetPosition, eyeFollow);
            targetYawPitch = eyeFollow.Clamp(targetYawPitch.x, targetYawPitch.y);

            if (!m_HasFilteredEyeYawPitch)
            {
                m_FilteredEyeYawPitch = targetYawPitch;
                m_HasFilteredEyeYawPitch = true;
            }
            else
            {
                float t = ExponentialT(eyeFollow.Speed, deltaTime);
                m_FilteredEyeYawPitch = Vector2.Lerp(m_FilteredEyeYawPitch, targetYawPitch, t);
            }

            if (eyeFollow.RotateEyeBones && m_EyeBones.Count > 0)
                ApplyEyeBoneLookAt(m_FilteredEyeYawPitch, eyeWeight, eyeFollow);

            ApplyEyeBlendShapes(m_FilteredEyeYawPitch, eyeWeight, eyeFollow);
        }

        private Vector2 ResolveHeadLocalYawPitch(
            Vector3 targetPosition,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            Vector3 worldDirection = targetPosition - head.position;
            if (worldDirection.sqrMagnitude <= DirectionEpsilon)
                return Vector2.zero;

            Vector3 headLocalDirection = head.InverseTransformDirection(worldDirection.normalized);
            Vector3 axisDirection = LocalDirectionToAxisCoordinates(
                headLocalDirection,
                m_ResolvedLocalForwardAxis,
                eyeFollow.LocalUpAxis);
            return DirectionToYawPitch(axisDirection);
        }

        private void ApplyEyeBoneLookAt(
            Vector2 yawPitch,
            float eyeWeight,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            Vector3 axisDirection = YawPitchToDirection(yawPitch.x, yawPitch.y);
            Vector3 headLocalDirection = AxisCoordinatesToLocalDirection(
                axisDirection,
                m_ResolvedLocalForwardAxis,
                eyeFollow.LocalUpAxis);
            Vector3 worldDirection = head.TransformDirection(headLocalDirection);
            for (int i = 0; i < m_EyeBones.Count; ++i)
            {
                Transform eyeBone = m_EyeBones[i];
                Vector3 eyeAxis = i < m_EyeBoneForwardAxes.Count ? m_EyeBoneForwardAxes[i] : eyeFollow.LocalForwardAxis;
                ApplyBoneLookAt(eyeBone, worldDirection, eyeWeight, 180f, eyeAxis, eyeFollow.LocalUpAxis, head.up, 0f);
            }
        }

        private void ApplyEyeBlendShapes(
            Vector2 yawPitch,
            float eyeWeight,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            if (!eyeFollow.DriveBlendShapes || !eyeFollow.HasBlendShapes)
                return;

            EnsureEyeBlendShapeBindings(eyeFollow);
            if (m_EyeBlendShapeBindingsByName.Count == 0)
                return;

            IReadOnlyList<LookAtControllerProfileSO.EyeBlendShapeConfig> configs = eyeFollow.BlendShapes;
            for (int i = 0; i < configs.Count; ++i)
            {
                LookAtControllerProfileSO.EyeBlendShapeConfig config = configs[i];
                if (config == null || !config.IsValid)
                    continue;

                float axisWeight = ResolveEyeBlendShapeAxisWeight(yawPitch, eyeFollow, config);
                float value = config.Evaluate(axisWeight) * Mathf.Clamp01(eyeWeight);
                ApplyEyeBlendShapeWeight(config.BlendShapeName, value);
            }
        }

        private void ResetEyeBlendShapes()
        {
            if (m_EyeBlendShapeBindingsByName.Count == 0 || m_LastAppliedEyeBlendShapeValues.Count == 0)
                return;

            m_BlendShapeNameScratch.Clear();
            foreach (string blendShapeName in m_EyeBlendShapeBindingsByName.Keys)
                m_BlendShapeNameScratch.Add(blendShapeName);

            for (int i = 0; i < m_BlendShapeNameScratch.Count; ++i)
                ApplyEyeBlendShapeWeight(m_BlendShapeNameScratch[i], 0f);

            m_BlendShapeNameScratch.Clear();
        }

        private void EnsureEyeBlendShapeBindings(LookAtControllerProfileSO.EyeFollowSettings eyeFollow)
        {
            if (!m_EyeBlendShapeBindingsDirty)
                return;

            m_EyeBlendShapeBindingsDirty = false;
            m_EyeBlendShapeBindingsByName.Clear();
            m_LastAppliedEyeBlendShapeValues.Clear();

            if (!eyeFollow.DriveBlendShapes || !eyeFollow.HasBlendShapes)
                return;

            Transform searchRoot = boneSearchRoot != null ? boneSearchRoot : transform;
            if (searchRoot == null)
                return;

            m_SkinnedMeshRendererScratch.Clear();
            searchRoot.GetComponentsInChildren(true, m_SkinnedMeshRendererScratch);
            for (int rendererIndex = 0; rendererIndex < m_SkinnedMeshRendererScratch.Count; ++rendererIndex)
            {
                SkinnedMeshRenderer renderer = m_SkinnedMeshRendererScratch[rendererIndex];
                Mesh mesh = renderer != null ? renderer.sharedMesh : null;
                if (mesh == null || mesh.blendShapeCount <= 0)
                    continue;

                for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; ++blendShapeIndex)
                {
                    string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                    if (!eyeFollow.CanDriveBlendShape(blendShapeName))
                        continue;

                    AddEyeBlendShapeBinding(blendShapeName, new BlendShapeBinding(renderer, blendShapeIndex));
                }
            }

            m_SkinnedMeshRendererScratch.Clear();
        }

        private void AddEyeBlendShapeBinding(string blendShapeName, BlendShapeBinding binding)
        {
            if (!m_EyeBlendShapeBindingsByName.TryGetValue(blendShapeName, out List<BlendShapeBinding> bindings))
            {
                bindings = new List<BlendShapeBinding>(1);
                m_EyeBlendShapeBindingsByName.Add(blendShapeName, bindings);
            }

            bindings.Add(binding);
        }

        private void ApplyEyeBlendShapeWeight(string blendShapeName, float value)
        {
            if (!m_EyeBlendShapeBindingsByName.TryGetValue(blendShapeName, out List<BlendShapeBinding> bindings))
                return;

            float weight = Mathf.Clamp(value, 0f, BlendShapeFullWeight);
            if (m_LastAppliedEyeBlendShapeValues.TryGetValue(blendShapeName, out float previousWeight)
                && Mathf.Abs(previousWeight - weight) <= WeightEpsilon)
            {
                return;
            }

            bool hasStaleBinding = false;
            for (int i = 0; i < bindings.Count; ++i)
            {
                if (!TryApplyBlendShapeWeight(bindings[i], weight))
                    hasStaleBinding = true;
            }

            m_LastAppliedEyeBlendShapeValues[blendShapeName] = weight;
            if (hasStaleBinding)
                m_EyeBlendShapeBindingsDirty = true;
        }

        private Vector3 BuildWorldDirection(Transform root, float yaw, float pitch, float targetWeight)
        {
            Vector3 targetDirection = root.TransformDirection(YawPitchToDirection(yaw, pitch));
            Vector3 defaultForward = root.forward;
            return BlendDirections(defaultForward, targetDirection, targetWeight);
        }

        private void ApplyBoneLookAt(
            Transform bone,
            Vector3 targetDirection,
            float weight,
            float maxAngle,
            Vector3 localForwardAxis,
            Vector3 localUpAxis,
            Vector3 uprightWorldUp,
            float uprightWeight)
        {
            if (bone == null || weight <= WeightEpsilon || targetDirection.sqrMagnitude <= DirectionEpsilon)
                return;

            Vector3 currentForward = bone.TransformDirection(localForwardAxis);
            if (currentForward.sqrMagnitude <= DirectionEpsilon)
                return;

            currentForward.Normalize();
            targetDirection.Normalize();
            if (maxAngle < 180f)
            {
                targetDirection = Vector3.RotateTowards(
                    currentForward,
                    targetDirection,
                    maxAngle * Mathf.Deg2Rad,
                    0f);
            }

            Quaternion delta = Quaternion.FromToRotation(currentForward, targetDirection);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
            ApplyBoneUprightConstraint(bone, localForwardAxis, localUpAxis, uprightWorldUp, uprightWeight);
        }

        private void ApplyBoneUprightConstraint(
            Transform bone,
            Vector3 localForwardAxis,
            Vector3 localUpAxis,
            Vector3 uprightWorldUp,
            float weight)
        {
            if (bone == null || weight <= WeightEpsilon || uprightWorldUp.sqrMagnitude <= DirectionEpsilon)
                return;

            Vector3 currentForward = bone.TransformDirection(localForwardAxis);
            Vector3 currentUp = bone.TransformDirection(localUpAxis);
            if (currentForward.sqrMagnitude <= DirectionEpsilon || currentUp.sqrMagnitude <= DirectionEpsilon)
                return;

            currentForward.Normalize();
            Vector3 projectedCurrentUp = Vector3.ProjectOnPlane(currentUp, currentForward);
            Vector3 projectedTargetUp = Vector3.ProjectOnPlane(uprightWorldUp, currentForward);
            if (projectedCurrentUp.sqrMagnitude <= DirectionEpsilon || projectedTargetUp.sqrMagnitude <= DirectionEpsilon)
                return;

            Quaternion rollDelta = Quaternion.FromToRotation(projectedCurrentUp.normalized, projectedTargetUp.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, rollDelta, Mathf.Clamp01(weight)) * bone.rotation;
        }

        private bool IsAnimatorStateAllowed()
        {
            if (m_LookAtStateHashes == null || m_LookAtStateHashes.Length == 0)
                return true;

            ResolveAnimator();
            if (animator == null || !animator.isActiveAndEnabled || animator.layerCount <= AnimatorLayerIndex)
                return true;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(AnimatorLayerIndex);
            if (MatchesAnimatorState(current))
                return true;

            return animator.IsInTransition(AnimatorLayerIndex)
                && MatchesAnimatorState(animator.GetNextAnimatorStateInfo(AnimatorLayerIndex));
        }

        private bool MatchesAnimatorState(AnimatorStateInfo stateInfo)
        {
            for (int i = 0; i < m_LookAtStateHashes.Length; ++i)
            {
                int hash = m_LookAtStateHashes[i];
                if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                    return true;
            }

            return false;
        }

        private void RebuildLookAtStateHashes()
        {
            if (LookAtStates == null || LookAtStates.Length == 0)
            {
                m_LookAtStateHashes = Array.Empty<int>();
                return;
            }

            var hashes = new List<int>(LookAtStates.Length);
            for (int i = 0; i < LookAtStates.Length; ++i)
            {
                string stateName = LookAtStates[i];
                if (string.IsNullOrWhiteSpace(stateName))
                    continue;

                hashes.Add(Animator.StringToHash(stateName.Trim()));
            }

            m_LookAtStateHashes = hashes.ToArray();
        }

        private LookAtDisableCause ResolvePrimaryDisableCause()
        {
            foreach (LookAtDisableCause cause in m_DisableCauses)
                return cause;

            return LookAtDisableCause.None;
        }

        private LookAtControllerProfileSO.LookAtIKSettings ResolveLookAtSettings()
        {
            return profile != null && profile.LookAt != null ? profile.LookAt : s_DefaultLookAtSettings;
        }

        private LookAtControllerProfileSO.LookAtSolverSettings ResolveSolverSettings()
        {
            return profile != null && profile.Solver != null ? profile.Solver : s_DefaultSolverSettings;
        }

        private LookAtControllerProfileSO.LookAtTargetConstraint ResolveProfileConstraint()
        {
            return profile != null && profile.Constraint != null ? profile.Constraint : s_DefaultConstraint;
        }

        private LookAtControllerProfileSO.EyeFollowSettings ResolveEyeFollowSettings()
        {
            return profile != null && profile.EyeFollow != null ? profile.EyeFollow : s_DefaultEyeFollowSettings;
        }

        private void MarkBonesDirty()
        {
            RestoreDrivenBoneBasePoseIfNeeded();
            ResetEyeBlendShapes();
            m_BonesDirty = true;
            m_HasFilteredLocalDirection = false;
            m_HasFilteredEyeYawPitch = false;
            m_EyeBlendShapeBindingsDirty = true;
            m_DrivenBoneBasePoseDirty = true;
        }

        private static string[] CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            for (int i = 0; i < values.Count; ++i)
                copy[i] = values[i];

            return copy;
        }

        private static float MoveWeight(float current, float target, float time, float deltaTime)
        {
            if (time <= 0f)
                return target;

            return Mathf.MoveTowards(current, target, deltaTime / time);
        }

        private static float MoveBlend(float current, float target, float speed, float deltaTime)
        {
            if (speed <= 0f)
                return target;

            return Mathf.MoveTowards(current, target, speed * deltaTime);
        }

        private static float ExponentialT(float speed, float deltaTime)
        {
            if (speed <= 0f || deltaTime <= 0f)
                return 1f;

            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        private static float EvaluateCurve(AnimationCurve curve, float time)
        {
            return curve != null && curve.length > 0 ? Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(time))) : Mathf.Clamp01(time);
        }

        private static float ResolveEyeBlendShapeAxisWeight(
            Vector2 yawPitch,
            LookAtControllerProfileSO.EyeFollowSettings eyeFollow,
            LookAtControllerProfileSO.EyeBlendShapeConfig config)
        {
            float value;
            float limit;
            if (config.Axis == LookAtControllerProfileSO.EyeBlendShapeAxis.Yaw)
            {
                value = yawPitch.x;
                limit = config.Direction == LookAtControllerProfileSO.EyeBlendShapeDirection.Positive
                    ? eyeFollow.YawRight
                    : eyeFollow.YawLeft;
            }
            else
            {
                value = yawPitch.y;
                limit = config.Direction == LookAtControllerProfileSO.EyeBlendShapeDirection.Positive
                    ? eyeFollow.PitchUp
                    : eyeFollow.PitchDown;
            }

            if (limit <= DirectionEpsilon)
                return 0f;

            float signedValue = config.Direction == LookAtControllerProfileSO.EyeBlendShapeDirection.Positive
                ? value
                : -value;
            return Mathf.Clamp01(signedValue / limit);
        }

        private static Vector2 DirectionToYawPitch(Vector3 localDirection)
        {
            if (localDirection.sqrMagnitude <= DirectionEpsilon)
                return Vector2.zero;

            localDirection.Normalize();
            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            float horizontal = Mathf.Sqrt(localDirection.x * localDirection.x + localDirection.z * localDirection.z);
            float pitch = Mathf.Atan2(localDirection.y, horizontal) * Mathf.Rad2Deg;
            return new Vector2(yaw, pitch);
        }

        private static Vector3 YawPitchToDirection(float yawDegrees, float pitchDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitch);
            return new Vector3(
                Mathf.Sin(yaw) * cosPitch,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * cosPitch).normalized;
        }

        private static Vector3 LocalDirectionToAxisCoordinates(
            Vector3 localDirection,
            Vector3 localForwardAxis,
            Vector3 localUpAxis)
        {
            if (localDirection.sqrMagnitude <= DirectionEpsilon)
                return Vector3.forward;

            BuildLocalBasis(localForwardAxis, localUpAxis, out Vector3 right, out Vector3 up, out Vector3 forward);
            Vector3 normalizedDirection = localDirection.normalized;
            return new Vector3(
                Vector3.Dot(normalizedDirection, right),
                Vector3.Dot(normalizedDirection, up),
                Vector3.Dot(normalizedDirection, forward)).normalized;
        }

        private static Vector3 AxisCoordinatesToLocalDirection(
            Vector3 axisDirection,
            Vector3 localForwardAxis,
            Vector3 localUpAxis)
        {
            if (axisDirection.sqrMagnitude <= DirectionEpsilon)
                axisDirection = Vector3.forward;

            BuildLocalBasis(localForwardAxis, localUpAxis, out Vector3 right, out Vector3 up, out Vector3 forward);
            Vector3 direction = right * axisDirection.x + up * axisDirection.y + forward * axisDirection.z;
            return direction.sqrMagnitude > DirectionEpsilon ? direction.normalized : forward;
        }

        private static void BuildLocalBasis(
            Vector3 localForwardAxis,
            Vector3 localUpAxis,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward)
        {
            forward = localForwardAxis.sqrMagnitude > DirectionEpsilon
                ? localForwardAxis.normalized
                : Vector3.forward;
            up = localUpAxis.sqrMagnitude > DirectionEpsilon
                ? localUpAxis.normalized
                : Vector3.up;
            up -= forward * Vector3.Dot(up, forward);
            if (up.sqrMagnitude <= DirectionEpsilon)
            {
                up = Vector3.up - forward * Vector3.Dot(Vector3.up, forward);
                if (up.sqrMagnitude <= DirectionEpsilon)
                    up = Vector3.right - forward * Vector3.Dot(Vector3.right, forward);
            }

            up.Normalize();
            right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude <= DirectionEpsilon)
                right = Vector3.right;
            else
                right.Normalize();
        }

        private static Vector3 BlendDirections(Vector3 from, Vector3 to, float weight)
        {
            if (from.sqrMagnitude <= DirectionEpsilon)
                return to.sqrMagnitude > DirectionEpsilon ? to.normalized : Vector3.forward;

            if (to.sqrMagnitude <= DirectionEpsilon)
                return from.normalized;

            return Vector3.Slerp(from.normalized, to.normalized, Mathf.Clamp01(weight)).normalized;
        }

        private Transform ResolveReferenceRoot()
        {
            return boneSearchRoot != null ? boneSearchRoot : transform;
        }

        private static Vector3 ResolveLocalForwardAxis(Transform bone, Transform referenceRoot, Vector3 fallbackAxis)
        {
            Vector3 referenceForward = referenceRoot != null ? referenceRoot.forward : bone != null ? bone.root.forward : Vector3.forward;
            return ResolveLocalAxis(bone, referenceForward, fallbackAxis);
        }

        private static Vector3 ResolveLocalUpAxis(Transform bone, Transform referenceRoot, Vector3 fallbackAxis)
        {
            Vector3 referenceUp = referenceRoot != null ? referenceRoot.up : bone != null ? bone.root.up : Vector3.up;
            return ResolveLocalAxis(bone, referenceUp, fallbackAxis);
        }

        private static Vector3 ResolveLocalAxis(Transform bone, Vector3 referenceDirection, Vector3 fallbackAxis)
        {
            if (bone == null)
                return fallbackAxis;

            if (referenceDirection.sqrMagnitude <= DirectionEpsilon)
                return fallbackAxis;

            referenceDirection.Normalize();
            Vector3 bestAxis = fallbackAxis;
            float bestDot = Vector3.Dot(bone.TransformDirection(fallbackAxis).normalized, referenceDirection);
            TestLocalAxis(bone, referenceDirection, Vector3.right, ref bestAxis, ref bestDot);
            TestLocalAxis(bone, referenceDirection, -Vector3.right, ref bestAxis, ref bestDot);
            TestLocalAxis(bone, referenceDirection, Vector3.up, ref bestAxis, ref bestDot);
            TestLocalAxis(bone, referenceDirection, -Vector3.up, ref bestAxis, ref bestDot);
            TestLocalAxis(bone, referenceDirection, Vector3.forward, ref bestAxis, ref bestDot);
            TestLocalAxis(bone, referenceDirection, -Vector3.forward, ref bestAxis, ref bestDot);
            return bestAxis;
        }

        private static void TestLocalAxis(
            Transform bone,
            Vector3 referenceDirection,
            Vector3 candidateAxis,
            ref Vector3 bestAxis,
            ref float bestDot)
        {
            Vector3 worldAxis = bone.TransformDirection(candidateAxis);
            if (worldAxis.sqrMagnitude <= DirectionEpsilon)
                return;

            float dot = Vector3.Dot(worldAxis.normalized, referenceDirection);
            if (dot <= bestDot)
                return;

            bestDot = dot;
            bestAxis = candidateAxis;
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

        private static readonly LookAtControllerProfileSO.LookAtIKSettings s_DefaultLookAtSettings =
            new LookAtControllerProfileSO.LookAtIKSettings();
        private static readonly LookAtControllerProfileSO.LookAtSolverSettings s_DefaultSolverSettings =
            new LookAtControllerProfileSO.LookAtSolverSettings();
        private static readonly LookAtControllerProfileSO.LookAtTargetConstraint s_DefaultConstraint =
            new LookAtControllerProfileSO.LookAtTargetConstraint();
        private static readonly LookAtControllerProfileSO.EyeFollowSettings s_DefaultEyeFollowSettings =
            new LookAtControllerProfileSO.EyeFollowSettings();
    }
}
