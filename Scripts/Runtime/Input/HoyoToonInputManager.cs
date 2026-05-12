using System;
using HoyoToon.Runtime.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoyoToon.Runtime.Input
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Input Manager")]
    public sealed class HoyoToonInputManager : MonoBehaviour
    {
        private const string LookActionName = "Look";
        private const string ZoomActionName = "Zoom";
        private const string AutoRotateActionName = "AutoRotate";
        private const string PreviousCharacterActionName = "PreviousCharacter";
        private const string NextCharacterActionName = "NextCharacter";
        private const string ToggleLookAtActionName = "ToggleLookAt";
        private const string ToggleUiActionName = "ToggleUI";

        private static HoyoToonInputManager s_Instance;

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private string actionMapName = HoyoToonInputActions.DefaultActionMapName;

        [SerializeField]
        private bool persistAcrossSceneLoads = true;

        private InputActionMap m_ActionMap;
        private InputAction m_LookAction;
        private InputAction m_ZoomAction;
        private InputAction m_AutoRotateAction;
        private InputAction m_PreviousCharacterAction;
        private InputAction m_NextCharacterAction;
        private InputAction m_ToggleLookAtAction;
        private InputAction m_ToggleUiAction;
        private bool m_RuntimeUiVisible = true;
        private static readonly System.Collections.Generic.HashSet<int> s_CameraInputBlockers =
            new System.Collections.Generic.HashSet<int>();

        public event Action AutoRotatePressed;
        public event Action PreviousCharacterPressed;
        public event Action NextCharacterPressed;
        public event Action ToggleLookAtPressed;
        public event Action ToggleUiPressed;

        public static HoyoToonInputManager Instance
        {
            get
            {
                if (s_Instance != null)
                    return s_Instance;

                HoyoToonInputManager[] managers = FindObjectsByType<HoyoToonInputManager>(FindObjectsSortMode.None);
                if (managers.Length > 0)
                {
                    s_Instance = managers[0];
                    return s_Instance;
                }

                GameObject inputManagerObject = new GameObject("HoyoToon Input Manager");
                s_Instance = inputManagerObject.AddComponent<HoyoToonInputManager>();
                return s_Instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Instance = null;
            s_CameraInputBlockers.Clear();
        }

        public static bool IsCameraInputBlocked => s_CameraInputBlockers.Count > 0;

        public static void SetCameraInputBlocked(UnityEngine.Object owner, bool blocked)
        {
            if (owner == null)
                return;

            int ownerId = owner.GetInstanceID();
            if (blocked)
            {
                s_CameraInputBlockers.Add(ownerId);
            }
            else
            {
                s_CameraInputBlockers.Remove(ownerId);
            }
        }

        public Vector2 ReadLookDelta()
        {
            return CanConsumeCameraInput() && m_LookAction != null
                ? m_LookAction.ReadValue<Vector2>()
                : Vector2.zero;
        }

        public float ReadZoomDelta()
        {
            return CanConsumeCameraInput() && m_ZoomAction != null
                ? m_ZoomAction.ReadValue<float>()
                : 0f;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            if (Application.isPlaying && persistAcrossSceneLoads && transform.parent == null)
                DontDestroyOnLoad(gameObject);

            BindInputActions();
        }

        private void OnEnable()
        {
            BindInputActions();
            SetInputActionsEnabled(true);
        }

        private void OnDisable()
        {
            SetInputActionsEnabled(false);
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        private void Update()
        {
            if (!RuntimeEditorBridge.CanConsumePlayModeKeyboardShortcut())
                return;

            if (CanConsumeCameraInput())
                InvokeIfPressed(m_AutoRotateAction, AutoRotatePressed);

            InvokeIfPressed(m_PreviousCharacterAction, PreviousCharacterPressed);
            InvokeIfPressed(m_NextCharacterAction, NextCharacterPressed);
            InvokeIfPressed(m_ToggleLookAtAction, ToggleLookAtPressed);
            if (m_ToggleUiAction != null && m_ToggleUiAction.WasPressedThisFrame())
            {
                ToggleRuntimeUiVisibility();
                ToggleUiPressed?.Invoke();
            }
        }

        private void BindInputActions()
        {
            InputActionAsset resolvedInputActions = HoyoToonInputActions.Resolve(inputActions);
            if (resolvedInputActions == null)
                return;

            inputActions = resolvedInputActions;
            string resolvedActionMapName = string.IsNullOrWhiteSpace(actionMapName)
                ? HoyoToonInputActions.DefaultActionMapName
                : actionMapName;
            m_ActionMap = inputActions.FindActionMap(resolvedActionMapName);
            if (m_ActionMap == null && !string.Equals(resolvedActionMapName, HoyoToonInputActions.LegacyActionMapName, StringComparison.Ordinal))
                m_ActionMap = inputActions.FindActionMap(HoyoToonInputActions.LegacyActionMapName);

            if (m_ActionMap == null)
                return;

            m_LookAction = m_ActionMap.FindAction(LookActionName);
            m_ZoomAction = m_ActionMap.FindAction(ZoomActionName);
            m_AutoRotateAction = m_ActionMap.FindAction(AutoRotateActionName);
            m_PreviousCharacterAction = m_ActionMap.FindAction(PreviousCharacterActionName);
            m_NextCharacterAction = m_ActionMap.FindAction(NextCharacterActionName);
            m_ToggleLookAtAction = m_ActionMap.FindAction(ToggleLookAtActionName);
            m_ToggleUiAction = m_ActionMap.FindAction(ToggleUiActionName);
        }

        private void SetInputActionsEnabled(bool enabled)
        {
            if (enabled)
                m_ActionMap?.Enable();
            else
                m_ActionMap?.Disable();
        }

        private static bool CanConsumeCameraInput()
        {
            if (IsCameraInputBlocked)
                return false;

            if (!Application.isFocused)
                return false;

            if (!Application.isEditor)
                return true;

            return RuntimeEditorBridge.IsGameViewFocused();
        }

        private static void InvokeIfPressed(InputAction action, Action callback)
        {
            if (action != null && action.WasPressedThisFrame())
                callback?.Invoke();
        }

        private void ToggleRuntimeUiVisibility()
        {
            SetRuntimeUiVisible(!m_RuntimeUiVisible);
        }

        private void SetRuntimeUiVisible(bool visible)
        {
            m_RuntimeUiVisible = visible;

            GameObject uiRoot = GameObject.Find("HoyoToon/UI");
            if (uiRoot == null)
                uiRoot = GameObject.Find("UI");

            if (uiRoot == null)
                return;

            Transform uiTransform = uiRoot.transform;
            for (int index = 0; index < uiTransform.childCount; index++)
            {
                Transform child = uiTransform.GetChild(index);
                if (child != null)
                    child.gameObject.SetActive(visible);
            }
        }
    }
}
