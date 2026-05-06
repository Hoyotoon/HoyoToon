using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Scene.Placement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoyoToon.Runtime.Simulator.Placement
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Simulator/Character Placement Input Controller")]
    public sealed class CharacterPlacementInputController : MonoBehaviour
    {
        private const string PreviousCharacterActionName = "PreviousCharacter";
        private const string NextCharacterActionName = "NextCharacter";

        [SerializeField]
        private CharacterPlacementController placementController;

        [SerializeField]
        private InputActionAsset inputActions;

        private InputAction m_PreviousModelAction;
        private InputAction m_NextModelAction;

        public CharacterPlacementController PlacementController
        {
            get => ResolvePlacementController();
            set => placementController = value;
        }

        private void Awake()
        {
            ResolvePlacementController();
            BindInputActions();
        }

        private void OnEnable()
        {
            ResolvePlacementController();
            BindInputActions();
            SetInputActionsEnabled(true);
        }

        private void OnDisable()
        {
            SetInputActionsEnabled(false);
        }

        private void OnValidate()
        {
            if (placementController == null)
                placementController = GetComponent<CharacterPlacementController>();
        }

        private void Update()
        {
            if (!CanConsumeModelInput())
                return;

            if (m_PreviousModelAction != null && m_PreviousModelAction.WasPressedThisFrame())
            {
                placementController.SwitchToPreviousModel();
            }
            else if (m_NextModelAction != null && m_NextModelAction.WasPressedThisFrame())
            {
                placementController.SwitchToNextModel();
            }
        }

        private CharacterPlacementController ResolvePlacementController()
        {
            if (placementController == null)
                placementController = GetComponent<CharacterPlacementController>();

            if (placementController == null)
                placementController = GetComponentInParent<CharacterPlacementController>();

            return placementController;
        }

        private void BindInputActions()
        {
            SetInputActionsEnabled(false);

            m_PreviousModelAction = null;
            m_NextModelAction = null;

            InputActionAsset resolvedInputActions = SimulatorInputActions.Resolve(inputActions);
            if (resolvedInputActions == null)
                return;

            inputActions = resolvedInputActions;
            InputActionMap map = resolvedInputActions.FindActionMap(SimulatorInputActions.ActionMapName);
            if (map == null)
                return;

            m_PreviousModelAction = map.FindAction(PreviousCharacterActionName);
            m_NextModelAction = map.FindAction(NextCharacterActionName);
        }

        private void SetInputActionsEnabled(bool enabled)
        {
            if (enabled)
            {
                m_PreviousModelAction?.Enable();
                m_NextModelAction?.Enable();
                return;
            }

            m_PreviousModelAction?.Disable();
            m_NextModelAction?.Disable();
        }

        private bool CanConsumeModelInput()
        {
            if (!isActiveAndEnabled || ResolvePlacementController() == null)
                return false;

            if (!Application.isFocused)
                return false;

            if (!Application.isEditor)
                return true;

            return RuntimeEditorBridge.IsGameViewFocused();
        }
    }
}
