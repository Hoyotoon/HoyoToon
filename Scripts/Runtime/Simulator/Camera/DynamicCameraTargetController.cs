using Unity.Cinemachine;
using UnityEngine;

namespace HoyoToon.Simulator.Camera
{
    public class DynamicCameraTargetController : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private Transform characterCenter;
        [SerializeField] private Transform characterLookAtHead;

        [Header("Distance Settings")]
        [SerializeField] private float blendStartDistance = 4f;
        [SerializeField] private float blendEndDistance = 2f;
        [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Transition Settings")]
        [SerializeField] private float transitionSpeed = 2f;
        [SerializeField] private TransitionType transitionType = TransitionType.SmoothStep;
        [SerializeField] private AnimationCurve customTransitionCurve;
        [SerializeField] private bool useInterpolatedTargets = true;

        public enum TransitionType
        {
            Linear,
            SmoothStep,
            SmootherStep,
            EaseInOut,
            CustomCurve
        }

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private CinemachineCamera cameraComponent;
        private CinemachinePositionComposer positionComposer;
        private Transform interpolatedTarget;
        private bool isCloseUp;
        private float currentDistance;
        private float currentBlendValue;
        private bool isBlendOverrideLocked;
        private float lockedBlendValue;
        private bool hasRuntimePivotOffset;
        private Vector3 runtimePivotOffset;
        private bool isTransitioning;
        private float transitionProgress;
        private Vector3 transitionStartPosition;

        public bool IsInCloseUpMode => isCloseUp;
        public bool IsBlendOverrideLocked => isBlendOverrideLocked;
        public float CurrentCameraDistance => currentDistance;
        public float CurrentBlendValue => currentBlendValue;
        public Transform CurrentFollowTarget => GetActiveCameraTarget();
        public Transform CurrentLookAtTarget => GetActiveCameraTarget();
        public bool IsTransitioning => isTransitioning;
        public float TransitionProgress => transitionProgress;

        private void Awake()
        {
            EnsureCustomTransitionCurve();

            cameraComponent = GetComponent<CinemachineCamera>();
            positionComposer = GetComponent<CinemachinePositionComposer>();

            if (cameraComponent == null || positionComposer == null)
            {
                Debug.LogError($"[{nameof(DynamicCameraTargetController)}] Missing CinemachineCamera or CinemachinePositionComposer.", this);
                enabled = false;
                return;
            }

            RefreshCameraTargets(true);
        }

        private void OnDestroy()
        {
            if (interpolatedTarget != null)
            {
                Destroy(interpolatedTarget.gameObject);
            }
        }

        private void Update()
        {
            if (characterCenter == null)
            {
                return;
            }

            currentDistance = positionComposer.CameraDistance;
            UpdateProgressiveBlending();

            if (useInterpolatedTargets)
            {
                UpdateInterpolatedTarget();
            }

            if (showDebugInfo && Time.frameCount % 30 == 0)
            {
                DisplayDebugInfo();
            }
        }

        public void StartTransition()
        {
            if (!useInterpolatedTargets)
            {
                return;
            }

            EnsureInterpolatedTarget();
            isTransitioning = true;
            transitionProgress = 0f;
            transitionStartPosition = interpolatedTarget.position;
        }

        public void SetCharacterReferences(Transform center, Transform head)
        {
            characterCenter = center;
            characterLookAtHead = head;
            isCloseUp = false;
            RefreshCameraTargets(true);
        }

        public void SetBlendDistances(float startDistance, float endDistance)
        {
            blendStartDistance = startDistance;
            blendEndDistance = endDistance;
        }

        public void SetTransitionSpeed(float speed)
        {
            transitionSpeed = speed;
        }

        public void SetTransitionType(TransitionType type)
        {
            transitionType = type;
        }

        public void SetCustomTransitionCurve(AnimationCurve curve)
        {
            customTransitionCurve = curve;
            EnsureCustomTransitionCurve();
        }

        public void ToggleInterpolatedTargets(bool enable)
        {
            useInterpolatedTargets = enable;
            RefreshCameraTargets(true);
        }

        public void ForceUpdate()
        {
            if (characterCenter == null || positionComposer == null)
            {
                return;
            }

            currentDistance = positionComposer.CameraDistance;
            UpdateProgressiveBlending();

            if (useInterpolatedTargets)
            {
                UpdateInterpolatedTarget();
            }
        }

        public void SetBlendOverrideLock(bool isLocked)
        {
            if (isBlendOverrideLocked == isLocked)
            {
                return;
            }

            isBlendOverrideLocked = isLocked;
            if (isLocked)
            {
                lockedBlendValue = currentBlendValue;
            }
        }

        public bool TryGetBaseTargetPosition(out Vector3 baseTargetPosition)
        {
            if (characterCenter == null)
            {
                baseTargetPosition = Vector3.zero;
                return false;
            }

            baseTargetPosition = GetBlendedTargetPosition();
            return true;
        }

        public void SetRuntimePivotOffset(Vector3 offset)
        {
            runtimePivotOffset = offset;
            hasRuntimePivotOffset = true;
        }

        public void ClearRuntimePivotOffset()
        {
            runtimePivotOffset = Vector3.zero;
            hasRuntimePivotOffset = false;
        }

        private void EnsureCustomTransitionCurve()
        {
            if (customTransitionCurve != null && customTransitionCurve.keys.Length > 0)
            {
                return;
            }

            customTransitionCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(1f, 1f, 2f, 0f));
        }

        private void UpdateProgressiveBlending()
        {
            float targetBlendValue = isBlendOverrideLocked
                ? lockedBlendValue
                : blendCurve.Evaluate(Mathf.InverseLerp(blendStartDistance, blendEndDistance, currentDistance));

            currentBlendValue = Mathf.Lerp(currentBlendValue, targetBlendValue, transitionSpeed * Time.deltaTime);
            isCloseUp = currentBlendValue > 0.5f;
        }

        private void UpdateInterpolatedTarget()
        {
            EnsureInterpolatedTarget();

            Vector3 targetPosition = GetTargetPositionWithOffset();
            if (isTransitioning)
            {
                transitionProgress += transitionSpeed * Time.deltaTime;
                interpolatedTarget.position = Vector3.Lerp(
                    transitionStartPosition,
                    targetPosition,
                    GetSmoothTransitionValue(transitionProgress));

                if (transitionProgress >= 1f)
                {
                    isTransitioning = false;
                    transitionProgress = 0f;
                }

                return;
            }

            interpolatedTarget.position = targetPosition;
        }

        private void RefreshCameraTargets(bool snapInterpolatedTarget)
        {
            if (cameraComponent == null || characterCenter == null)
            {
                return;
            }

            Transform activeTarget = characterCenter;
            if (useInterpolatedTargets)
            {
                EnsureInterpolatedTarget();
                if (snapInterpolatedTarget)
                {
                    interpolatedTarget.position = GetTargetPositionWithOffset();
                }

                activeTarget = interpolatedTarget;
            }

            cameraComponent.Follow = activeTarget;
            cameraComponent.LookAt = activeTarget;
        }

        private void EnsureInterpolatedTarget()
        {
            if (interpolatedTarget != null)
            {
                return;
            }

            var targetObject = new GameObject("InterpolatedCameraTarget");
            targetObject.transform.SetParent(transform);
            interpolatedTarget = targetObject.transform;
        }

        private Vector3 GetTargetPositionWithOffset()
        {
            Vector3 targetPosition = GetBlendedTargetPosition();
            if (hasRuntimePivotOffset)
            {
                targetPosition += runtimePivotOffset;
            }

            return targetPosition;
        }

        private Vector3 GetBlendedTargetPosition()
        {
            Transform headTarget = characterLookAtHead != null ? characterLookAtHead : characterCenter;
            return Vector3.Lerp(characterCenter.position, headTarget.position, currentBlendValue);
        }

        private float GetSmoothTransitionValue(float t)
        {
            t = Mathf.Clamp01(t);

            switch (transitionType)
            {
                case TransitionType.Linear:
                    return t;
                case TransitionType.SmoothStep:
                    return t * t * (3f - 2f * t);
                case TransitionType.SmootherStep:
                    return t * t * t * (t * (t * 6f - 15f) + 10f);
                case TransitionType.EaseInOut:
                    return 0.5f * (1f - Mathf.Cos(t * Mathf.PI));
                case TransitionType.CustomCurve:
                    return customTransitionCurve.Evaluate(t);
                default:
                    return t;
            }
        }

        private Transform GetActiveCameraTarget()
        {
            return useInterpolatedTargets ? interpolatedTarget : characterCenter;
        }

        private void DisplayDebugInfo()
        {
            float targetBlendValue = blendCurve.Evaluate(Mathf.InverseLerp(blendStartDistance, blendEndDistance, currentDistance));
            string mode = isCloseUp ? "Close-Up (Head)" : "Far View (Center)";
            string transition = isTransitioning ? $"Transitioning: {transitionProgress:F2}" : "Progressive";

            Debug.Log(
                $"Camera Targets: {mode} | Distance: {currentDistance:F2} | Blend Range: {blendEndDistance:F1}-{blendStartDistance:F1} | Blend Value: {currentBlendValue:F2} | Target Blend: {targetBlendValue:F2} | {transition}");
        }
    }
}
