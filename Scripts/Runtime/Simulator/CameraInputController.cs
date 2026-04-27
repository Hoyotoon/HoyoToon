using Cinemachine;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace HoyoToon.Runtime.Simulator.Camera
{
    [RequireComponent(typeof(CinemachineFreeLook))]
    public class CameraInputController : MonoBehaviour
    {
        private const string HoyoToonInputAssetPath = "Input/HoyoToon";
        private const string HoyoToonSimulatorActionMap = "Simulator";
        private const string LookActionName = "Look";
        private const string ZoomActionName = "Zoom";
        private const string AutoRotateActionName = "AutoRotate";

        [System.Serializable]
        private struct ZoomOrbitSettings
        {
            public float height;
            public float radius;
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float lookSensitivity = 0.5f;
        [SerializeField] private float zoomSensitivity = 0.11f;
        [SerializeField] private bool autoRotateEnabled;
        [SerializeField] private float autoRotateSpeed = 30f;
        [SerializeField] private int zoomOrbitIndex = 1;
        [SerializeField] private float zoomSmoothTime = 0.25f;
        [SerializeField] private ZoomOrbitSettings defaultZoom = new ZoomOrbitSettings { height = 0f, radius = 2.7f };
        [SerializeField] private ZoomOrbitSettings farZoom = new ZoomOrbitSettings { height = 0f, radius = 4.05f };
        [SerializeField] private ZoomOrbitSettings closeZoom = new ZoomOrbitSettings { height = 0.4f, radius = 1.35f };
        [SerializeField] private ZoomOrbitSettings reallyCloseZoom = new ZoomOrbitSettings { height = 0.46f, radius = 0.5f };
        [SerializeField] private float defaultFollowOffsetY = 0f;
        [SerializeField] private float closeFollowOffsetY = 0.4f;
        [SerializeField] private float reallyCloseFollowOffsetY = 0f;
        [SerializeField] private Transform middleRigDefaultLookAtTarget;
        [SerializeField] private Transform middleRigCloseLookAtTarget;
        [SerializeField] private float middleRigCloseLookAtRadius = 2f;
        [FormerlySerializedAs("middleRigLookAtBlendRange")]
        [FormerlySerializedAs("middleRigLookAtSwitchHysteresis")]
        [SerializeField] private float middleRigLookAtBlendSmoothTime = 0.5f;

        private CinemachineFreeLook freeLook;
        private InputAction lookAction;
        private InputAction zoomAction;
        private InputAction autoRotateAction;
        private float targetZoomRadius;
        private float zoomRadiusVelocity;
        private Transform cachedMiddleRigDefaultLookAtTarget;
        private Transform cachedMiddleRigCloseLookAtTarget;
        private bool hasCachedMiddleRigDefaultLookAtTarget;
        private bool hasCachedMiddleRigCloseLookAtTarget;
        private Transform middleRigLookAtBlendTarget;
        private float middleRigLookAtBlendWeight;
        private float middleRigLookAtBlendWeightVelocity;

        public bool HasValidCameraState => HasValidZoomOrbitIndex();

        public bool AutoRotateEnabled => autoRotateEnabled;

        public float CurrentOrbitX => freeLook != null ? freeLook.m_XAxis.Value : 0f;

        public float CurrentZoomRadius => HasValidZoomOrbitIndex() ? freeLook.m_Orbits[zoomOrbitIndex].m_Radius : 0f;

        private void Awake()
        {
            freeLook = GetComponent<CinemachineFreeLook>();
            BindInputActions();
            SyncZoomState();
        }

        private void OnEnable()
        {
            SetInputActionsEnabled(true);
            SyncZoomState();
        }

        private void OnDisable()
        {
            SetInputActionsEnabled(false);
            RestoreMiddleRigLookAtTarget();
        }

        private void OnDestroy()
        {
            if (middleRigLookAtBlendTarget == null)
            {
                return;
            }

            RuntimeEditorBridge.DestroyObject(middleRigLookAtBlendTarget.gameObject);
            middleRigLookAtBlendTarget = null;
        }

        private void BindInputActions()
        {
            lookAction = null;
            zoomAction = null;
            autoRotateAction = null;

            InputActionAsset resolvedInputActions = inputActions;
            if (resolvedInputActions == null)
            {
                resolvedInputActions = Resources.Load<InputActionAsset>(HoyoToonInputAssetPath);
            }

            if (resolvedInputActions == null)
            {
                return;
            }

            inputActions = resolvedInputActions;
            InputActionMap map = resolvedInputActions.FindActionMap(HoyoToonSimulatorActionMap);
            if (map == null)
            {
                return;
            }

            lookAction = map.FindAction(LookActionName);
            zoomAction = map.FindAction(ZoomActionName);
            autoRotateAction = map.FindAction(AutoRotateActionName);
        }

        private void SetInputActionsEnabled(bool enabled)
        {
            if (enabled)
            {
                lookAction?.Enable();
                zoomAction?.Enable();
                autoRotateAction?.Enable();
                return;
            }

            lookAction?.Disable();
            zoomAction?.Disable();
            autoRotateAction?.Disable();
        }

        private bool HasValidZoomOrbitIndex()
        {
            return freeLook != null
                && zoomOrbitIndex >= 0
                && zoomOrbitIndex < freeLook.m_Orbits.Length;
        }

        private bool TryGetZoomRig(out CinemachineVirtualCamera zoomRig)
        {
            zoomRig = null;
            if (!HasValidZoomOrbitIndex())
            {
                return false;
            }

            zoomRig = freeLook.GetRig(zoomOrbitIndex);
            return zoomRig != null;
        }

        private void SyncZoomState()
        {
            if (!HasValidZoomOrbitIndex())
            {
                return;
            }

            float minRadius = GetMinimumZoomRadius();
            float maxRadius = GetMaximumZoomRadius();
            targetZoomRadius = Mathf.Clamp(freeLook.m_Orbits[zoomOrbitIndex].m_Radius, minRadius, maxRadius);
            zoomRadiusVelocity = 0f;
            middleRigLookAtBlendWeight = targetZoomRadius <= middleRigCloseLookAtRadius ? 1f : 0f;
            middleRigLookAtBlendWeightVelocity = 0f;
            UpdateMiddleRigLookAt(targetZoomRadius, forceRefresh: true);
        }

        private float GetMinimumZoomRadius()
        {
            return Mathf.Min(reallyCloseZoom.radius, Mathf.Min(closeZoom.radius, farZoom.radius));
        }

        private float GetMaximumZoomRadius()
        {
            return Mathf.Max(reallyCloseZoom.radius, Mathf.Max(closeZoom.radius, farZoom.radius));
        }

        private bool CanConsumeScrollInput()
        {
            if (!Application.isFocused)
            {
                return false;
            }

            if (!Application.isEditor)
            {
                return true;
            }

            return RuntimeEditorBridge.IsGameViewFocused();
        }

        private Transform ResolveDefaultSceneLookAtTarget()
        {
            return freeLook == null ? null : CameraTargetUtility.ResolveDefaultLookAtTarget(freeLook.gameObject.scene);
        }

        private Transform ResolveCloseSceneLookAtTarget()
        {
            return freeLook == null ? null : CameraTargetUtility.ResolveCloseLookAtTarget(freeLook.gameObject.scene);
        }

        private float GetOrbitHeightForRadius(float radius)
        {
            if (radius < closeZoom.radius)
            {
                float reallyCloseT = Mathf.InverseLerp(closeZoom.radius, reallyCloseZoom.radius, radius);
                float closeHeight = closeZoom.height + closeFollowOffsetY;
                float reallyCloseHeight = reallyCloseZoom.height + reallyCloseFollowOffsetY;
                return Mathf.Lerp(closeHeight, reallyCloseHeight, reallyCloseT);
            }

            if (radius < defaultZoom.radius)
            {
                float zoomInT = Mathf.InverseLerp(defaultZoom.radius, closeZoom.radius, radius);
                float verticalOffsetY = Mathf.Lerp(defaultFollowOffsetY, closeFollowOffsetY, zoomInT);
                return Mathf.Lerp(defaultZoom.height, closeZoom.height, zoomInT) + verticalOffsetY;
            }

            float zoomOutT = Mathf.InverseLerp(defaultZoom.radius, farZoom.radius, radius);
            return Mathf.Lerp(defaultZoom.height, farZoom.height, zoomOutT);
        }

        private Transform ResolveMiddleRigDefaultLookAtTarget(CinemachineVirtualCamera zoomRig, bool forceRefresh)
        {
            if (middleRigDefaultLookAtTarget != null)
            {
                cachedMiddleRigDefaultLookAtTarget = middleRigDefaultLookAtTarget;
                hasCachedMiddleRigDefaultLookAtTarget = true;
                return cachedMiddleRigDefaultLookAtTarget;
            }

            if (!hasCachedMiddleRigDefaultLookAtTarget || forceRefresh)
            {
                Transform sceneDefaultLookAtTarget = ResolveDefaultSceneLookAtTarget();
                if (sceneDefaultLookAtTarget != null)
                {
                    cachedMiddleRigDefaultLookAtTarget = sceneDefaultLookAtTarget;
                    hasCachedMiddleRigDefaultLookAtTarget = true;
                    return cachedMiddleRigDefaultLookAtTarget;
                }

                Transform currentLookAt = zoomRig != null && zoomRig.m_LookAt != null
                    ? zoomRig.m_LookAt
                    : (freeLook != null ? freeLook.LookAt : null);
                if (currentLookAt != null && currentLookAt != middleRigLookAtBlendTarget)
                {
                    cachedMiddleRigDefaultLookAtTarget = currentLookAt;
                    hasCachedMiddleRigDefaultLookAtTarget = true;
                }
                else
                {
                    cachedMiddleRigDefaultLookAtTarget = null;
                    hasCachedMiddleRigDefaultLookAtTarget = false;
                }
            }

            if (hasCachedMiddleRigDefaultLookAtTarget && cachedMiddleRigDefaultLookAtTarget != null)
            {
                return cachedMiddleRigDefaultLookAtTarget;
            }

            return freeLook != null ? freeLook.LookAt : null;
        }

        private Transform ResolveMiddleRigCloseLookAtTarget(bool forceRefresh)
        {
            if (middleRigCloseLookAtTarget != null)
            {
                cachedMiddleRigCloseLookAtTarget = middleRigCloseLookAtTarget;
                hasCachedMiddleRigCloseLookAtTarget = true;
                return middleRigCloseLookAtTarget;
            }

            if (!hasCachedMiddleRigCloseLookAtTarget || forceRefresh)
            {
                cachedMiddleRigCloseLookAtTarget = ResolveCloseSceneLookAtTarget();
                hasCachedMiddleRigCloseLookAtTarget = cachedMiddleRigCloseLookAtTarget != null;
            }

            return cachedMiddleRigCloseLookAtTarget;
        }

        private void EnsureMiddleRigLookAtBlendTarget()
        {
            if (middleRigLookAtBlendTarget != null)
            {
                return;
            }

            GameObject blendTarget = new GameObject($"{name}_MiddleRigLookAtBlendTarget");
            blendTarget.hideFlags = HideFlags.HideInHierarchy;
            middleRigLookAtBlendTarget = blendTarget.transform;
        }

        private void RestoreMiddleRigLookAtTarget()
        {
            if (!TryGetZoomRig(out CinemachineVirtualCamera zoomRig))
            {
                return;
            }

            Transform defaultLookAtTarget = ResolveMiddleRigDefaultLookAtTarget(zoomRig, forceRefresh: false);
            if (defaultLookAtTarget != null)
            {
                freeLook.LookAt = defaultLookAtTarget;
            }

            zoomRig.LookAt = defaultLookAtTarget;
            middleRigLookAtBlendWeight = 0f;
            middleRigLookAtBlendWeightVelocity = 0f;
        }

        private void UpdateMiddleRigLookAt(float currentRadius, bool forceRefresh = false)
        {
            if (!TryGetZoomRig(out CinemachineVirtualCamera zoomRig))
            {
                return;
            }

            Transform defaultLookAtTarget = ResolveMiddleRigDefaultLookAtTarget(zoomRig, forceRefresh);
            Transform closeLookAtTarget = ResolveMiddleRigCloseLookAtTarget(forceRefresh);

            if (defaultLookAtTarget == null || closeLookAtTarget == null || defaultLookAtTarget == closeLookAtTarget)
            {
                if (defaultLookAtTarget != null)
                {
                    freeLook.LookAt = defaultLookAtTarget;
                }

                zoomRig.LookAt = defaultLookAtTarget;
                middleRigLookAtBlendWeight = 0f;
                middleRigLookAtBlendWeightVelocity = 0f;
                return;
            }

            EnsureMiddleRigLookAtBlendTarget();

            float targetBlendWeight = currentRadius <= middleRigCloseLookAtRadius ? 1f : 0f;
            float lookAtBlend = middleRigLookAtBlendSmoothTime > 0f
                ? Mathf.SmoothDamp(middleRigLookAtBlendWeight, targetBlendWeight, ref middleRigLookAtBlendWeightVelocity, middleRigLookAtBlendSmoothTime)
                : targetBlendWeight;

            if (Mathf.Abs(lookAtBlend - targetBlendWeight) < 0.001f)
            {
                lookAtBlend = targetBlendWeight;
            }

            middleRigLookAtBlendWeight = lookAtBlend;

            middleRigLookAtBlendTarget.SetPositionAndRotation(
                Vector3.Lerp(defaultLookAtTarget.position, closeLookAtTarget.position, lookAtBlend),
                Quaternion.Slerp(defaultLookAtTarget.rotation, closeLookAtTarget.rotation, lookAtBlend));

            Transform activeLookAtTarget = lookAtBlend <= 0f
                ? defaultLookAtTarget
                : lookAtBlend >= 1f
                    ? closeLookAtTarget
                    : middleRigLookAtBlendTarget;

            freeLook.LookAt = activeLookAtTarget;
            zoomRig.LookAt = activeLookAtTarget;
        }

        private void LateUpdate()
        {
            if (!HasValidZoomOrbitIndex())
            {
                return;
            }

            if (autoRotateAction != null && autoRotateAction.WasPressedThisFrame())
            {
                autoRotateEnabled = !autoRotateEnabled;
            }

            Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            freeLook.m_XAxis.m_InputAxisValue = lookInput.x * lookSensitivity;

            if (autoRotateEnabled)
            {
                freeLook.m_XAxis.Value += autoRotateSpeed * Time.deltaTime;
            }

            float scroll = CanConsumeScrollInput() && zoomAction != null ? zoomAction.ReadValue<float>() : 0f;
            CinemachineFreeLook.Orbit orbit = freeLook.m_Orbits[zoomOrbitIndex];
            float minRadius = GetMinimumZoomRadius();
            float maxRadius = GetMaximumZoomRadius();
            if (scroll != 0f)
            {
                targetZoomRadius = Mathf.Clamp(targetZoomRadius + (scroll * zoomSensitivity), minRadius, maxRadius);
            }

            float appliedRadius = zoomSmoothTime > 0f
                ? Mathf.SmoothDamp(orbit.m_Radius, targetZoomRadius, ref zoomRadiusVelocity, zoomSmoothTime)
                : targetZoomRadius;

            if (Mathf.Abs(appliedRadius - targetZoomRadius) < 0.001f)
            {
                appliedRadius = targetZoomRadius;
            }

            orbit.m_Radius = appliedRadius;
            orbit.m_Height = GetOrbitHeightForRadius(appliedRadius);
            freeLook.m_Orbits[zoomOrbitIndex] = orbit;
            UpdateMiddleRigLookAt(appliedRadius);
        }
    }
}
