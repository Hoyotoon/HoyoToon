using System.Collections.Generic;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoyoToon.Simulator.Camera
{
    [AddComponentMenu("HoyoToon/Simulator/Camera Controller")]
    public class CameraController : MonoBehaviour
    {
        private static class ActionNames
        {
            public const string EnableCamera = "EnableCamera";
            public const string Look = "Look";
            public const string Zoom = "Zoom";
            public const string CursorPosition = "CursorPosition";
            public const string AltZoomModifier = "AltZoomModifier";
            public const string AutoRotate = "AutoRotate";
            public const string ToggleProjection = "ToggleProjection";
            public const string ViewFront = "ViewFront";
            public const string ViewBack = "ViewBack";
            public const string ViewLeft = "ViewLeft";
            public const string ViewRight = "ViewRight";
            public const string ViewTop = "ViewTop";
            public const string ViewBottom = "ViewBottom";
            public const string PreviousCharacter = "PreviousCharacter";
            public const string NextCharacter = "NextCharacter";
        }

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Simulator";
        [SerializeField] private bool autoFindInputActions = true;
        [SerializeField] private bool allowZoomWithoutButton = true;
        [SerializeField] private bool showCursorWhenNotControlling = true;

        [Header("Zoom")]
        [SerializeField, Range(0f, 12f)] private float defaultDistance = 6f;
        [SerializeField, Range(0f, 12f)] private float minimumDistance = 1f;
        [SerializeField, Range(0f, 12f)] private float maximumDistance = 12f;
        [SerializeField, Range(0f, 20f)] private float smoothing = 4f;
        [SerializeField, Range(0f, 20f)] private float zoomSensitivity = 1f;
        [SerializeField, Range(0f, 12f)] private float altOverrideMinimumDistance = 0.25f;

        [Header("Auto Rotate")]
        [SerializeField] private float rotationSpeed = 45f;
        [SerializeField] private RotationDirection direction = RotationDirection.Clockwise;
        [SerializeField] private bool pauseDuringManualControl = true;

        [Header("Orthographic Views")]
        [SerializeField, Range(1f, 30f)] private float transitionSpeed = 10f;
        [SerializeField, Range(1f, 50f)] private float viewSnapSpeed = 12f;

        [Header("References")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachineInputAxisController inputAxisController;
        [SerializeField] private DynamicCameraTargetController targetController;
        [SerializeField] private CharacterContainer characterContainer;

        public enum RotationDirection
        {
            Clockwise,
            CounterClockwise
        }

        private readonly List<InputAction> ownedEnabledActions = new();
        private readonly List<InputAction> viewActions = new();
        private InputActionReference generatedLookActionReference;

        private CinemachinePositionComposer positionComposer;
        private CinemachinePanTilt panTilt;
        private UnityEngine.Camera outputCamera;
        private InputAction cameraControlAction;
        private InputAction lookAction;
        private InputAction zoomAction;
        private InputAction cursorPositionAction;
        private InputAction altZoomModifierAction;
        private InputAction autoRotateAction;
        private InputAction toggleProjectionAction;
        private InputAction previousCharacterAction;
        private InputAction nextCharacterAction;
        private bool isCameraControlActive;
        private bool isAutoRotating;
        private bool isOrthographic;
        private float currentTargetDistance;
        private bool isAltInteractionActive;
        private bool hasLockedAltAnchor;
        private Vector3 lockedAltAnchorWorldPosition;
        private bool isLerpingAngles;
        private float targetPan;
        private float targetTilt;
        private float targetOrthoSize;

        public InputAction CameraControlInputAction => cameraControlAction;
        public bool IsCameraControlActive => isCameraControlActive;
        public bool IsAutoRotating => isAutoRotating;
        public bool IsOrthographic => isOrthographic;
        public float CurrentDistance => positionComposer != null ? positionComposer.CameraDistance : currentTargetDistance;
        public float TargetDistance => currentTargetDistance;
        public float MinDistance => minimumDistance;
        public float MaxDistance => maximumDistance;

        private void Awake()
        {
            if (virtualCamera == null)
                virtualCamera = GetComponent<CinemachineCamera>();
            if (virtualCamera == null)
                virtualCamera = FindAnyObjectByType<CinemachineCamera>();

            if (virtualCamera == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] No CinemachineCamera found!", this);
                enabled = false;
                return;
            }

            if (inputAxisController == null)
                inputAxisController = GetComponent<CinemachineInputAxisController>();
            if (targetController == null)
                targetController = GetComponent<DynamicCameraTargetController>();
            if (characterContainer == null)
                characterContainer = FindAnyObjectByType<CharacterContainer>();

            positionComposer = GetComponent<CinemachinePositionComposer>();
            panTilt = GetComponent<CinemachinePanTilt>();

            if (positionComposer == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] Missing CinemachinePositionComposer.", this);
                enabled = false;
                return;
            }

            if (panTilt == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] Missing CinemachinePanTilt.", this);
                enabled = false;
                return;
            }

            if (!ResolveInputActions())
            {
                enabled = false;
                return;
            }

            ConfigureLookAxisController();

            currentTargetDistance = Mathf.Clamp(defaultDistance, minimumDistance, maximumDistance);

            outputCamera = ResolveOutputCamera();
            isOrthographic = virtualCamera.Lens.ModeOverride == LensSettings.OverrideModes.Orthographic
                || (outputCamera != null && outputCamera.orthographic);
            targetOrthoSize = virtualCamera.Lens.OrthographicSize > 0f
                ? virtualCamera.Lens.OrthographicSize
                : EstimateOrthoSize();

            SubscribeActions();
        }

        private void OnEnable()
        {
            EnableAction(cameraControlAction);
            EnableAction(zoomAction);
            EnableAction(altZoomModifierAction);
            EnableAction(cursorPositionAction);
            EnableAction(autoRotateAction);
            EnableAction(toggleProjectionAction);
            EnableAction(previousCharacterAction);
            EnableAction(nextCharacterAction);

            for (int index = 0; index < viewActions.Count; index++)
                EnableAction(viewActions[index]);

            if (inputAxisController == null)
                EnableAction(lookAction);

            isCameraControlActive = IsPressed(cameraControlAction);
            SetCameraMovementEnabled(isCameraControlActive);
            UpdateCursorState();
        }

        private void OnDisable()
        {
            SetCameraMovementEnabled(false);
            DisableOwnedActions();
            ClearAltInteractionState();
            isCameraControlActive = false;
            UpdateCursorState();
        }

        private void OnDestroy()
        {
            UnsubscribeActions();

            if (generatedLookActionReference != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedLookActionReference);
                else
                    DestroyImmediate(generatedLookActionReference);
            }
        }

        private void Update()
        {
            HandleFallbackLookInput();
            HandleZoom();
            HandleAutoRotate();
        }

        private void LateUpdate()
        {
            UpdateViewSnap();
            UpdateOrthographicSize();
        }

        private void SubscribeActions()
        {
            if (cameraControlAction != null)
            {
                cameraControlAction.performed += OnCameraControlPerformed;
                cameraControlAction.canceled += OnCameraControlCanceled;
            }

            if (autoRotateAction != null)
                autoRotateAction.performed += OnAutoRotatePerformed;
            if (toggleProjectionAction != null)
                toggleProjectionAction.performed += OnProjectionTogglePerformed;
            if (previousCharacterAction != null)
                previousCharacterAction.performed += OnPreviousCharacterPerformed;
            if (nextCharacterAction != null)
                nextCharacterAction.performed += OnNextCharacterPerformed;

            for (int index = 0; index < viewActions.Count; index++)
            {
                viewActions[index].performed += OnViewPerformed;
            }
        }

        private void UnsubscribeActions()
        {
            if (cameraControlAction != null)
            {
                cameraControlAction.performed -= OnCameraControlPerformed;
                cameraControlAction.canceled -= OnCameraControlCanceled;
            }

            if (autoRotateAction != null)
                autoRotateAction.performed -= OnAutoRotatePerformed;
            if (toggleProjectionAction != null)
                toggleProjectionAction.performed -= OnProjectionTogglePerformed;
            if (previousCharacterAction != null)
                previousCharacterAction.performed -= OnPreviousCharacterPerformed;
            if (nextCharacterAction != null)
                nextCharacterAction.performed -= OnNextCharacterPerformed;

            if (viewActions == null)
                return;

            for (int index = 0; index < viewActions.Count; index++)
            {
                viewActions[index].performed -= OnViewPerformed;
            }
        }

        private void OnCameraControlPerformed(InputAction.CallbackContext context)
        {
            isCameraControlActive = true;
            SetCameraMovementEnabled(true);
            UpdateCursorState();
        }

        private void OnCameraControlCanceled(InputAction.CallbackContext context)
        {
            isCameraControlActive = false;
            SetCameraMovementEnabled(false);
            UpdateCursorState();
        }

        private void OnAutoRotatePerformed(InputAction.CallbackContext context)
        {
            isAutoRotating = !isAutoRotating;
        }

        private void OnProjectionTogglePerformed(InputAction.CallbackContext context)
        {
            SetOrthographic(!isOrthographic);
        }

        private void OnViewPerformed(InputAction.CallbackContext context)
        {
            if (!isOrthographic)
                SetOrthographic(true);

            GetViewAngles(context.action.name, out targetPan, out targetTilt);
            isLerpingAngles = true;
        }

        private void OnPreviousCharacterPerformed(InputAction.CallbackContext context)
        {
            characterContainer?.SwitchToPreviousActiveCharacter();
        }

        private void OnNextCharacterPerformed(InputAction.CallbackContext context)
        {
            characterContainer?.SwitchToNextActiveCharacter();
        }

        private void HandleFallbackLookInput()
        {
            if (inputAxisController != null || !isCameraControlActive || lookAction == null || panTilt == null)
                return;

            Vector2 lookDelta = lookAction.ReadValue<Vector2>();
            if (lookDelta.sqrMagnitude <= Mathf.Epsilon)
                return;

            panTilt.PanAxis.Value += lookDelta.x;
            panTilt.TiltAxis.Value = Mathf.Clamp(
                panTilt.TiltAxis.Value + lookDelta.y,
                panTilt.TiltAxis.Range.x,
                panTilt.TiltAxis.Range.y);
        }

        private void HandleZoom()
        {
            float zoomValue = ReadZoomValue(zoomAction) * zoomSensitivity;
            bool isAltHeld = IsPressed(altZoomModifierAction);

            if (positionComposer == null || zoomAction == null)
                return;

            if (!allowZoomWithoutButton && !isCameraControlActive)
                return;

            if (targetController != null)
            {
                if (isAltHeld)
                {
                    if (!isAltInteractionActive)
                    {
                        isAltInteractionActive = true;
                        hasLockedAltAnchor = false;
                        lockedAltAnchorWorldPosition = Vector3.zero;
                    }

                    targetController.SetBlendOverrideLock(true);

                    if (!hasLockedAltAnchor)
                        hasLockedAltAnchor = TryResolveAltAnchorWorldPosition(out lockedAltAnchorWorldPosition);

                    UpdateAltZoomOffsetFromLockedAnchor();
                }
                else if (isAltInteractionActive)
                {
                    ClearAltInteractionState();
                }
            }
            else
            {
                isAltInteractionActive = false;
                hasLockedAltAnchor = false;
                lockedAltAnchorWorldPosition = Vector3.zero;
            }

            float activeMinimumDistance = Mathf.Clamp(isAltHeld ? altOverrideMinimumDistance : minimumDistance, 0f, maximumDistance);

            currentTargetDistance = Mathf.Clamp(currentTargetDistance + zoomValue, activeMinimumDistance, maximumDistance);

            if (Mathf.Approximately(positionComposer.CameraDistance, currentTargetDistance))
                return;

            float currentDistance = positionComposer.CameraDistance;
            float lerpedZoomValue = Mathf.Lerp(currentDistance, currentTargetDistance, smoothing * Time.deltaTime);
            positionComposer.CameraDistance = lerpedZoomValue;
        }

        private void HandleAutoRotate()
        {
            if (!isAutoRotating || panTilt == null)
                return;

            if (pauseDuringManualControl && isCameraControlActive)
                return;

            float sign = direction == RotationDirection.Clockwise ? 1f : -1f;
            panTilt.PanAxis.Value += rotationSpeed * sign * Time.deltaTime;
        }

        private void UpdateViewSnap()
        {
            if (!isLerpingAngles || panTilt == null)
                return;

            float speed = viewSnapSpeed * Time.deltaTime;
            float pan = Mathf.LerpAngle(panTilt.PanAxis.Value, targetPan, speed);
            float tilt = Mathf.Lerp(panTilt.TiltAxis.Value, targetTilt, speed);

            panTilt.PanAxis.Value = pan;
            panTilt.TiltAxis.Value = tilt;

            if (Mathf.Abs(Mathf.DeltaAngle(pan, targetPan)) < 0.1f
                && Mathf.Abs(tilt - targetTilt) < 0.1f)
            {
                panTilt.PanAxis.Value = targetPan;
                panTilt.TiltAxis.Value = targetTilt;
                isLerpingAngles = false;
            }
        }

        private void UpdateOrthographicSize()
        {
            if (!isOrthographic || virtualCamera == null)
                return;

            float desiredSize = EstimateOrthoSize();
            if (!Mathf.Approximately(targetOrthoSize, desiredSize))
                targetOrthoSize = desiredSize;

            var lens = virtualCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetOrthoSize, transitionSpeed * Time.deltaTime);
            virtualCamera.Lens = lens;

            if (ResolveOutputCamera() != null)
                outputCamera.orthographicSize = lens.OrthographicSize;
        }

        private void GetViewAngles(string actionName, out float pan, out float tilt)
        {
            if (actionName == ActionNames.ViewFront)
            {
                pan = 0f;
                tilt = 0f;
            }
            else if (actionName == ActionNames.ViewBack)
            {
                pan = 180f;
                tilt = 0f;
            }
            else if (actionName == ActionNames.ViewLeft)
            {
                pan = 90f;
                tilt = 0f;
            }
            else if (actionName == ActionNames.ViewRight)
            {
                pan = -90f;
                tilt = 0f;
            }
            else if (actionName == ActionNames.ViewTop)
            {
                pan = 0f;
                tilt = 90f;
            }
            else if (actionName == ActionNames.ViewBottom)
            {
                pan = 0f;
                tilt = -90f;
            }
            else
            {
                pan = 0f;
                tilt = 0f;
            }
        }

        private bool TryResolveAltAnchorWorldPosition(out Vector3 anchorWorldPosition)
        {
            anchorWorldPosition = Vector3.zero;

            if (targetController == null || cursorPositionAction == null)
                return false;

            if (!targetController.TryGetBaseTargetPosition(out var baseTargetPosition))
                return false;

            var worldCamera = ResolveOutputCamera();
            if (worldCamera == null)
                return false;

            Vector2 cursorScreenPosition = cursorPositionAction.ReadValue<Vector2>();
            Ray ray = worldCamera.ScreenPointToRay(cursorScreenPosition);

            Vector3 toBaseTarget = baseTargetPosition - worldCamera.transform.position;
            float planeDistance = Vector3.Dot(toBaseTarget, worldCamera.transform.forward);
            if (planeDistance <= 0f)
                return false;

            Plane anchorPlane = new Plane(
                worldCamera.transform.forward,
                worldCamera.transform.position + worldCamera.transform.forward * planeDistance);

            if (!anchorPlane.Raycast(ray, out float enterDistance))
                return false;

            anchorWorldPosition = ray.GetPoint(enterDistance);
            return true;
        }

        private void UpdateAltZoomOffsetFromLockedAnchor()
        {
            if (!hasLockedAltAnchor || targetController == null)
                return;

            if (!targetController.TryGetBaseTargetPosition(out var baseTargetPosition))
                return;

            targetController.SetRuntimePivotOffset(lockedAltAnchorWorldPosition - baseTargetPosition);
        }

        private void ClearAltInteractionState()
        {
            isAltInteractionActive = false;
            hasLockedAltAnchor = false;
            lockedAltAnchorWorldPosition = Vector3.zero;

            if (targetController == null)
                return;

            targetController.SetBlendOverrideLock(false);
            targetController.ClearRuntimePivotOffset();
        }

        private UnityEngine.Camera ResolveOutputCamera()
        {
            if (outputCamera != null)
                return outputCamera;

            var brain = FindAnyObjectByType<CinemachineBrain>();
            outputCamera = brain != null ? brain.OutputCamera : UnityEngine.Camera.main;
            if (outputCamera == null && brain != null)
                outputCamera = brain.GetComponent<UnityEngine.Camera>();

            return outputCamera;
        }

        private float EstimateOrthoSize()
        {
            float distance = positionComposer != null
                ? positionComposer.CameraDistance
                : currentTargetDistance;

            float fov = virtualCamera != null ? virtualCamera.Lens.FieldOfView : 60f;
            return Mathf.Max(0.01f, distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
        }

        private void SetCameraMovementEnabled(bool enabledState)
        {
            if (inputAxisController == null)
                return;

            var controllersProperty = inputAxisController.GetType().GetProperty("Controllers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (controllersProperty == null)
                return;

            if (controllersProperty.GetValue(inputAxisController) is not System.Collections.IList controllers)
                return;

            for (int index = 0; index < controllers.Count; index++)
                SetFieldOrPropertyValue(controllers[index], "Enabled", enabledState);
        }

        private void UpdateCursorState()
        {
            if (!Application.isPlaying)
                return;

            if (isCameraControlActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = showCursorWhenNotControlling;
        }

        private bool ResolveInputActions()
        {
            if (inputActions == null && autoFindInputActions)
                inputActions = FindInputActionsAsset();

            if (inputActions == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] No InputActionAsset found. Assign the shared HoyoToon input actions or keep auto-find enabled.", this);
                return false;
            }

            InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
            if (actionMap == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] Action map '{actionMapName}' was not found in '{inputActions.name}'.", this);
                return false;
            }

            cameraControlAction = FindRequiredAction(actionMap, ActionNames.EnableCamera);
            lookAction = FindRequiredAction(actionMap, ActionNames.Look);
            zoomAction = FindRequiredAction(actionMap, ActionNames.Zoom);
            cursorPositionAction = FindRequiredAction(actionMap, ActionNames.CursorPosition);
            altZoomModifierAction = FindRequiredAction(actionMap, ActionNames.AltZoomModifier);
            autoRotateAction = FindRequiredAction(actionMap, ActionNames.AutoRotate);
            toggleProjectionAction = FindRequiredAction(actionMap, ActionNames.ToggleProjection);
            previousCharacterAction = actionMap.FindAction(ActionNames.PreviousCharacter, false);
            nextCharacterAction = actionMap.FindAction(ActionNames.NextCharacter, false);

            viewActions.Clear();
            AddViewAction(actionMap, ActionNames.ViewFront);
            AddViewAction(actionMap, ActionNames.ViewBack);
            AddViewAction(actionMap, ActionNames.ViewLeft);
            AddViewAction(actionMap, ActionNames.ViewRight);
            AddViewAction(actionMap, ActionNames.ViewTop);
            AddViewAction(actionMap, ActionNames.ViewBottom);

            return cameraControlAction != null
                && lookAction != null
                && zoomAction != null
                && cursorPositionAction != null
                && altZoomModifierAction != null
                && autoRotateAction != null
                && toggleProjectionAction != null;
        }

        private InputActionAsset FindInputActionsAsset()
        {
            var components = GetComponents<MonoBehaviour>();
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                MonoBehaviour component = components[componentIndex];
                if (component == null || ReferenceEquals(component, this))
                    continue;

                var componentType = component.GetType();
                var fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    if (fields[fieldIndex].FieldType != typeof(InputActionAsset))
                        continue;

                    var value = fields[fieldIndex].GetValue(component) as InputActionAsset;
                    if (value != null)
                        return value;
                }

                var properties = componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    PropertyInfo property = properties[propertyIndex];
                    if (property.PropertyType != typeof(InputActionAsset) || !property.CanRead || property.GetIndexParameters().Length > 0)
                        continue;

                    var value = property.GetValue(component) as InputActionAsset;
                    if (value != null)
                        return value;
                }
            }

            return null;
        }

        private static InputAction FindRequiredAction(InputActionMap actionMap, string actionName)
        {
            return actionMap.FindAction(actionName, false);
        }

        private void AddViewAction(InputActionMap actionMap, string actionName)
        {
            InputAction action = actionMap.FindAction(actionName, false);
            if (action != null)
                viewActions.Add(action);
        }

        private void ConfigureLookAxisController()
        {
            if (inputAxisController == null || lookAction == null)
                return;

            if (generatedLookActionReference != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedLookActionReference);
                else
                    DestroyImmediate(generatedLookActionReference);
            }

            generatedLookActionReference = InputActionReference.Create(lookAction);

            var controllersProperty = inputAxisController.GetType().GetProperty("Controllers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (controllersProperty == null)
                return;

            if (controllersProperty.GetValue(inputAxisController) is not System.Collections.IList controllers)
                return;

            for (int index = 0; index < controllers.Count; index++)
                AssignLookActionToController(controllers[index]);
        }

        private void AssignLookActionToController(object controller)
        {
            if (controller == null)
                return;

            object input = GetFieldOrPropertyValue(controller, "Input");
            if (input == null)
                return;

            if (!SetFieldOrPropertyValue(input, "InputAction", generatedLookActionReference))
                SetFieldOrPropertyValue(input, "Input", generatedLookActionReference);
        }

        private static object GetFieldOrPropertyValue(object target, string memberName)
        {
            var targetType = target.GetType();
            var field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);

            var property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead)
                return property.GetValue(target);

            return null;
        }

        private static bool SetFieldOrPropertyValue(object target, string memberName, object value)
        {
            var targetType = target.GetType();
            var field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return true;
            }

            var property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value);
                return true;
            }

            return false;
        }

        private void EnableAction(InputAction action)
        {
            if (action == null || action.enabled || ownedEnabledActions.Contains(action))
                return;

            action.Enable();
            ownedEnabledActions.Add(action);
        }

        private void DisableOwnedActions()
        {
            for (int index = 0; index < ownedEnabledActions.Count; index++)
            {
                ownedEnabledActions[index].Disable();
            }

            ownedEnabledActions.Clear();
        }

        private static bool IsPressed(InputAction action)
        {
            return action != null && action.IsPressed();
        }

        private static float ReadZoomValue(InputAction action)
        {
            if (action == null)
                return 0f;

            Vector2 zoomVector = action.ReadValue<Vector2>();
            return -(Mathf.Abs(zoomVector.y) > Mathf.Abs(zoomVector.x) ? zoomVector.y : zoomVector.x);
        }

        public void SetAutoRotating(bool active)
        {
            isAutoRotating = active;
        }

        public void SetSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        public void SetDirection(RotationDirection rotationDirection)
        {
            direction = rotationDirection;
        }

        public void SetZoomLimits(float min, float max)
        {
            minimumDistance = min;
            maximumDistance = max;
            currentTargetDistance = Mathf.Clamp(currentTargetDistance, minimumDistance, maximumDistance);
        }

        public void SetZoomDistance(float distance)
        {
            currentTargetDistance = Mathf.Clamp(distance, minimumDistance, maximumDistance);
        }

        public void SetOrthographic(bool orthographic)
        {
            if (virtualCamera == null)
                return;

            isOrthographic = orthographic;
            isLerpingAngles = false;

            var lens = virtualCamera.Lens;
            lens.ModeOverride = orthographic ? LensSettings.OverrideModes.Orthographic : LensSettings.OverrideModes.Perspective;

            if (orthographic)
            {
                targetOrthoSize = EstimateOrthoSize();
                lens.OrthographicSize = targetOrthoSize;
            }

            virtualCamera.Lens = lens;

            if (ResolveOutputCamera() != null)
            {
                outputCamera.orthographic = orthographic;
                if (orthographic)
                    outputCamera.orthographicSize = lens.OrthographicSize;
            }
        }
    }
}