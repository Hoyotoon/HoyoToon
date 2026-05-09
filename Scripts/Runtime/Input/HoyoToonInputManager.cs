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

        public event Action AutoRotatePressed;
        public event Action PreviousCharacterPressed;
        public event Action NextCharacterPressed;
        public event Action ToggleLookAtPressed;

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

            InvokeIfPressed(m_AutoRotateAction, AutoRotatePressed);
            InvokeIfPressed(m_PreviousCharacterAction, PreviousCharacterPressed);
            InvokeIfPressed(m_NextCharacterAction, NextCharacterPressed);
            InvokeIfPressed(m_ToggleLookAtAction, ToggleLookAtPressed);
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
    }
}
