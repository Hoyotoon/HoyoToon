using HoyoToon.Runtime.Scene.Placement;
using HoyoToon.Runtime.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HoyoToon.Runtime.Simulator.Placement
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Simulator/Character Placement Input Controller")]
    public sealed class CharacterPlacementInputController : MonoBehaviour
    {
        [SerializeField]
        private CharacterPlacementController placementController;

        private HoyoToonInputManager m_InputManager;
        private static bool s_SceneLoadedHandlerRegistered;

        public CharacterPlacementController PlacementController
        {
            get => ResolvePlacementController();
            set => placementController = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            s_SceneLoadedHandlerRegistered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapPrimaryPlacementInput()
        {
            RegisterSceneLoadedHandler();
            EnsurePrimaryPlacementInputController();
        }

        private void Awake()
        {
            ResolvePlacementController();
            BindInputManager();
        }

        private void OnEnable()
        {
            ResolvePlacementController();
            BindInputManager();
        }

        private void OnDisable()
        {
            UnbindInputManager();
        }

        private void OnValidate()
        {
            if (placementController == null)
                placementController = GetComponent<CharacterPlacementController>();
        }

        private CharacterPlacementController ResolvePlacementController()
        {
            if (placementController == null)
                placementController = GetComponent<CharacterPlacementController>();

            if (placementController == null)
                placementController = GetComponentInParent<CharacterPlacementController>();

            if (placementController == null)
                placementController = CharacterPlacementController.GetPrimaryCachedOrFind();

            return placementController;
        }

        private static void RegisterSceneLoadedHandler()
        {
            if (s_SceneLoadedHandlerRegistered)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            s_SceneLoadedHandlerRegistered = true;
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            EnsurePrimaryPlacementInputController();
        }

        private static void EnsurePrimaryPlacementInputController()
        {
            CharacterPlacementInputController[] existingControllers =
                FindObjectsByType<CharacterPlacementInputController>(FindObjectsSortMode.None);
            if (existingControllers.Length > 0)
                return;

            CharacterPlacementController placementController = CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController == null)
                return;

            CharacterPlacementInputController inputController =
                placementController.GetComponent<CharacterPlacementInputController>();
            if (inputController == null)
                inputController = placementController.gameObject.AddComponent<CharacterPlacementInputController>();

            inputController.PlacementController = placementController;
        }

        private void BindInputManager()
        {
            UnbindInputManager();
            m_InputManager = HoyoToonInputManager.Instance;
            m_InputManager.PreviousCharacterPressed += HandlePreviousCharacterPressed;
            m_InputManager.NextCharacterPressed += HandleNextCharacterPressed;
        }

        private void UnbindInputManager()
        {
            if (m_InputManager == null)
                return;

            m_InputManager.PreviousCharacterPressed -= HandlePreviousCharacterPressed;
            m_InputManager.NextCharacterPressed -= HandleNextCharacterPressed;
            m_InputManager = null;
        }

        private void HandlePreviousCharacterPressed()
        {
            if (!isActiveAndEnabled || ResolvePlacementController() == null)
                return;

            placementController.SwitchToPreviousModel();
        }

        private void HandleNextCharacterPressed()
        {
            if (!isActiveAndEnabled || ResolvePlacementController() == null)
                return;

            placementController.SwitchToNextModel();
        }
    }
}
