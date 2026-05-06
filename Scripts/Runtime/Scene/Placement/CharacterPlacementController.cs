using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Scene.Placement
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Scene/Character Placement Controller")]
    public sealed class CharacterPlacementController : MonoBehaviour
    {
        private const int MaxTeamMembers = 4;

        private static readonly List<CharacterPlacementController> s_ActivePlacementControllers = new List<CharacterPlacementController>();
        private static readonly List<HSRCharacterController> s_DiscoveredControllerScratch = new List<HSRCharacterController>();
        private static readonly List<Transform> s_LeftHierarchyScratch = new List<Transform>(16);
        private static readonly List<Transform> s_RightHierarchyScratch = new List<Transform>(16);

        [SerializeField]
        [Tooltip("When enabled, managed models are rebuilt from HSRCharacterController instances in this scene and automatically placed into the active scene layout roots.")]
        private bool autoDiscoverManagedModels = true;

        [SerializeField]
        [Tooltip("Models managed by the placement controller. Valid entries must have an HSRCharacterController on the root object.")]
        private List<GameObject> managedModels = new List<GameObject>();

        [SerializeField]
        [Tooltip("Placement mode used for managed characters.")]
        private ManagerPlacementMode placementMode = ManagerPlacementMode.Single;

        [SerializeField]
        [Tooltip("Selected team roster root when Team mode is active.")]
        private HoyoToonTeamGame teamGame = HoyoToonTeamGame.HonkaiStarRail;

        [SerializeField]
        [Tooltip("Camera behavior used while Grid mode is active.")]
        private GridCameraMode gridCamera = GridCameraMode.Single;

        [SerializeField]
        [Tooltip("Models enabled in Team mode. Maximum of four.")]
        private List<GameObject> teamActiveModels = new List<GameObject>();

        [SerializeField]
        [Tooltip("Index of the currently focused model inside the managed list.")]
        private int activeModelIndex = -1;

        [SerializeField]
        [Tooltip("When enabled, camera targets are synchronized from the active model for Single mode and Grid + Single camera mode.")]
        private bool syncCameraTargets = true;

        [SerializeField]
        [Tooltip("Optional root used when resolving camera target transforms in the scene.")]
        private Transform preferredCameraTargetRoot;

        [SerializeField]
        [Tooltip("When enabled, Grid + Single camera mode uses the active model's full world position for camera target sync.")]
        private bool useGridSingleCameraWorldPosition = true;

        private readonly List<GameObject> m_DiscoveredModelsScratch = new List<GameObject>();
        private readonly HashSet<GameObject> m_ModelValidationScratch = new HashSet<GameObject>();
        private bool m_HasAppliedLayout;
        private int m_LastAppliedLayoutHash;
        private bool m_HasSyncedCameraTargets;
        private GameObject m_LastSyncedCameraTargetModel;
        private ManagerPlacementMode m_LastSyncedCameraTargetMode;
        private GridCameraMode m_LastSyncedGridCameraMode;
        private Transform m_LastSyncedCameraTargetRoot;
        private bool m_HasPendingPlacementRequest;
        private bool m_HasPendingForcedPlacementRequest;
        private bool m_ManagedModelDiscoveryDirty = true;
        private bool m_RosterConsistencyDirty = true;
        private int m_InputSwitchVersion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistryOnSubsystemRegistration()
        {
            s_ActivePlacementControllers.Clear();
            s_DiscoveredControllerScratch.Clear();
        }

        public bool AutoDiscoverManagedModels
        {
            get => autoDiscoverManagedModels;
            set
            {
                if (autoDiscoverManagedModels == value)
                    return;

                autoDiscoverManagedModels = value;
                MarkManagedModelDiscoveryDirty();
                RequestPlacement(force: true);
            }
        }

        public IReadOnlyList<GameObject> ManagedModels => managedModels;
        public IReadOnlyList<GameObject> TeamActiveModels => teamActiveModels;

        public ManagerPlacementMode PlacementMode
        {
            get => placementMode;
            set
            {
                if (placementMode == value)
                    return;

                bool enteringTeamMode = value == ManagerPlacementMode.Team;
                GameObject preferredTeamModel = null;
                if (enteringTeamMode)
                {
                    EnsureRosterConsistency();
                    preferredTeamModel = ResolveActiveModel();
                }

                placementMode = value;
                if (enteringTeamMode)
                    SeedDefaultTeamActiveModels(preferredTeamModel);

                RequestPlacement(force: true);
            }
        }

        public HoyoToonTeamGame TeamGame
        {
            get => teamGame;
            set
            {
                if (teamGame == value)
                    return;

                teamGame = value;
                RequestPlacement(force: true);
            }
        }

        public GridCameraMode GridCamera
        {
            get => gridCamera;
            set
            {
                if (gridCamera == value)
                    return;

                gridCamera = value;
                RequestPlacement(force: true);
            }
        }

        public bool SyncCameraTargets
        {
            get => syncCameraTargets;
            set
            {
                if (syncCameraTargets == value)
                    return;

                syncCameraTargets = value;
                RequestPlacement(force: true);
            }
        }

        public Transform PreferredCameraTargetRoot
        {
            get => preferredCameraTargetRoot;
            set
            {
                if (preferredCameraTargetRoot == value)
                    return;

                preferredCameraTargetRoot = value;
                RequestPlacement(force: true);
            }
        }

        public bool UseGridSingleCameraWorldPosition
        {
            get => useGridSingleCameraWorldPosition;
            set
            {
                if (useGridSingleCameraWorldPosition == value)
                    return;

                useGridSingleCameraWorldPosition = value;
                RequestPlacement(force: true);
            }
        }

        public int ActiveModelIndex
        {
            get => activeModelIndex;
            set
            {
                EnsureRosterConsistency();
                int clampedIndex = managedModels.Count == 0
                    ? -1
                    : Mathf.Clamp(value, -1, managedModels.Count - 1);

                if (activeModelIndex == clampedIndex)
                    return;

                activeModelIndex = clampedIndex;
                RequestPlacement(force: true);
            }
        }

        public GameObject ActiveModel
        {
            get => ResolveActiveModel();
        }

        public GameObject FocusedModel => ActiveModel;

        public int FocusedModelIndex
        {
            get => ActiveModelIndex;
            set => ActiveModelIndex = value;
        }

        public HSRCharacterController ActiveCharacterController
        {
            get
            {
                GameObject activeModel = ActiveModel;
                return activeModel != null ? activeModel.GetComponent<HSRCharacterController>() : null;
            }
        }

        public int InputSwitchVersion => m_InputSwitchVersion;

        public static CharacterPlacementController GetPrimaryCachedOrFind()
        {
            PruneNullPlacementControllers();
            for (int i = 0; i < s_ActivePlacementControllers.Count; ++i)
            {
                CharacterPlacementController controller = s_ActivePlacementControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    return controller;
            }

            CharacterPlacementController[] found = FindObjectsByType<CharacterPlacementController>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; ++i)
                Register(found[i]);

            PruneNullPlacementControllers();
            for (int i = 0; i < s_ActivePlacementControllers.Count; ++i)
            {
                CharacterPlacementController controller = s_ActivePlacementControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    return controller;
            }

            return null;
        }

        public static CharacterPlacementController FindForScene(UnityScene scene)
        {
            if (!scene.IsValid())
                return null;

            PruneNullPlacementControllers();
            for (int i = 0; i < s_ActivePlacementControllers.Count; ++i)
            {
                CharacterPlacementController controller = s_ActivePlacementControllers[i];
                if (controller != null
                    && controller.isActiveAndEnabled
                    && controller.gameObject.scene == scene)
                {
                    return controller;
                }
            }

            CharacterPlacementController[] found = FindObjectsByType<CharacterPlacementController>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; ++i)
                Register(found[i]);

            for (int i = 0; i < s_ActivePlacementControllers.Count; ++i)
            {
                CharacterPlacementController controller = s_ActivePlacementControllers[i];
                if (controller != null
                    && controller.isActiveAndEnabled
                    && controller.gameObject.scene == scene)
                {
                    return controller;
                }
            }

            return null;
        }

        public static GameObject FindActiveModel(UnityScene scene)
        {
            HSRCharacterController activeController = FindActiveCharacterController(scene);
            return activeController != null ? activeController.gameObject : null;
        }

        public static HSRCharacterController FindActiveCharacterController(UnityScene scene)
        {
            if (!scene.IsValid())
                return null;

            CharacterPlacementController placementController = FindForScene(scene);
            if (placementController != null)
            {
                placementController.EnsureRosterConsistency();
                HSRCharacterController activeController = placementController.ActiveCharacterController;
                if (activeController != null)
                    return activeController;
            }

            s_DiscoveredControllerScratch.Clear();
            DiscoverSceneCharacterControllers(scene, s_DiscoveredControllerScratch, includeInactive: false);
            return s_DiscoveredControllerScratch.Count > 0 ? s_DiscoveredControllerScratch[0] : null;
        }

        private void OnEnable()
        {
            Register(this);
            HSRCharacterController.ActiveControllerRegistryChanged -= HandleCharacterRegistryChanged;
            HSRCharacterController.ActiveControllerRegistryChanged += HandleCharacterRegistryChanged;
            CameraTargetUtility.ClearCachedBaselines();
            MarkManagedModelDiscoveryDirty();
            RequestPlacement(force: true);
        }

        private void OnDisable()
        {
            HSRCharacterController.ActiveControllerRegistryChanged -= HandleCharacterRegistryChanged;
            ClearPendingPlacementRequest();
            Unregister(this);
            ResetCameraTargetSyncState();
            m_HasAppliedLayout = false;
            m_LastAppliedLayoutHash = 0;
        }

        private void OnDestroy()
        {
            HSRCharacterController.ActiveControllerRegistryChanged -= HandleCharacterRegistryChanged;
            ClearPendingPlacementRequest();
            Unregister(this);
        }

        private void OnValidate()
        {
            MarkManagedModelDiscoveryDirty();
            if (Application.isPlaying)
                QueuePlacementRequest(force: true);
        }

        private void OnTransformChildrenChanged()
        {
            MarkManagedModelDiscoveryDirty();
            if (Application.isPlaying)
                QueuePlacementRequest(force: true);
        }

        private void Update()
        {
            if (Application.isPlaying && TryConsumePendingPlacementRequest(out bool force))
            {
                ApplyPlacement(force);
            }

        }

        public void ApplyLayout()
        {
            ApplyNow();
        }

        public void ApplyNow()
        {
            ClearPendingPlacementRequest();
            ApplyPlacement(force: true);
        }

        public void RefreshManagedModels()
        {
            MarkManagedModelDiscoveryDirty();
            RequestPlacement(force: true);
        }

        public void PruneManagedModels()
        {
            MarkManagedModelDiscoveryDirty();
            RequestPlacement(force: true);
        }

        public void RegisterModel(GameObject model)
        {
            if (!IsSceneModelValid(model))
                return;

            if (!managedModels.Contains(model))
            {
                managedModels.Add(model);
                MarkRosterConsistencyDirty();
            }

            if (activeModelIndex < 0)
                activeModelIndex = managedModels.IndexOf(model);

            RequestPlacement(force: true);
        }

        public void RemoveModel(GameObject model)
        {
            if (model == null)
                return;

            int removedIndex = managedModels.IndexOf(model);
            if (removedIndex < 0)
                return;

            managedModels.RemoveAt(removedIndex);
            teamActiveModels.Remove(model);
            MarkRosterConsistencyDirty();

            if (managedModels.Count == 0)
            {
                activeModelIndex = -1;
            }
            else if (activeModelIndex >= 0)
            {
                if (removedIndex < activeModelIndex)
                {
                    activeModelIndex = Mathf.Clamp(activeModelIndex - 1, 0, managedModels.Count - 1);
                }
                else if (removedIndex == activeModelIndex)
                {
                    activeModelIndex = Mathf.Clamp(removedIndex, 0, managedModels.Count - 1);
                }
            }

            RequestPlacement(force: true);
        }

        public bool SetModelTeamActive(GameObject model, bool isActive)
        {
            if (!IsSceneModelValid(model))
                return false;

            bool rosterChanged = false;
            if (!managedModels.Contains(model))
            {
                managedModels.Add(model);
                rosterChanged = true;
            }

            if (isActive)
            {
                if (teamActiveModels.Contains(model))
                {
                    if (rosterChanged)
                    {
                        MarkRosterConsistencyDirty();
                        RequestPlacement(force: true);
                    }

                    return true;
                }

                if (teamActiveModels.Count >= MaxTeamMembers)
                {
                    if (rosterChanged)
                    {
                        MarkRosterConsistencyDirty();
                        RequestPlacement(force: true);
                    }

                    return false;
                }

                teamActiveModels.Add(model);
                MarkRosterConsistencyDirty();
                RequestPlacement(force: true);
                return true;
            }

            bool removed = teamActiveModels.Remove(model);
            if (removed || rosterChanged)
                MarkRosterConsistencyDirty();

            RequestPlacement(force: true);
            return removed;
        }

        public void SetFocusedModel(GameObject model)
        {
            if (model == null)
            {
                activeModelIndex = -1;
                RequestPlacement(force: true);
                return;
            }

            int index = managedModels.IndexOf(model);
            if (index < 0 && IsSceneModelValid(model))
            {
                managedModels.Add(model);
                MarkRosterConsistencyDirty();
                index = managedModels.Count - 1;
            }

            if (index < 0)
                return;

            activeModelIndex = index;
            RequestPlacement(force: true);
        }

        public bool IsModelActiveInCurrentMode(GameObject model)
        {
            EnsureRosterConsistency();
            if (!IsSceneModelValid(model) || !managedModels.Contains(model))
                return false;

            switch (placementMode)
            {
                case ManagerPlacementMode.Single:
                    return model == ResolveActiveModel();
                case ManagerPlacementMode.Team:
                    return teamActiveModels.Contains(model);
                case ManagerPlacementMode.Grid:
                    return true;
                default:
                    return false;
            }
        }

        public void EnsureRosterConsistency()
        {
            bool discoveryRan = false;
            if (autoDiscoverManagedModels && m_ManagedModelDiscoveryDirty)
            {
                RefreshManagedModelsFromScene();
                m_ManagedModelDiscoveryDirty = false;
                discoveryRan = true;
            }

            if (!discoveryRan && !m_RosterConsistencyDirty)
                return;

            RemoveInvalidModels(managedModels);
            RemoveInvalidModels(teamActiveModels);
            RemoveTeamModelsNotInManagedRoster();

            if (teamActiveModels.Count > MaxTeamMembers)
                teamActiveModels.RemoveRange(MaxTeamMembers, teamActiveModels.Count - MaxTeamMembers);

            if (managedModels.Count == 0)
            {
                activeModelIndex = -1;
                teamActiveModels.Clear();
                m_RosterConsistencyDirty = false;
                return;
            }

            activeModelIndex = Mathf.Clamp(activeModelIndex, -1, managedModels.Count - 1);

            if (placementMode == ManagerPlacementMode.Team)
            {
                if (teamActiveModels.Count == 0)
                {
                    activeModelIndex = -1;
                }
                else
                {
                    GameObject activeModel = ResolveActiveModel();
                    if (activeModel == null || !teamActiveModels.Contains(activeModel))
                        activeModelIndex = managedModels.IndexOf(teamActiveModels[0]);
                }
            }

            m_RosterConsistencyDirty = false;
        }

        private void SeedDefaultTeamActiveModels(GameObject preferredModel)
        {
            if (teamActiveModels.Count > 0 || managedModels.Count == 0)
                return;

            if (preferredModel != null && managedModels.Contains(preferredModel))
                teamActiveModels.Add(preferredModel);

            for (int i = 0; i < managedModels.Count && teamActiveModels.Count < MaxTeamMembers; ++i)
            {
                GameObject model = managedModels[i];
                if (model == null || teamActiveModels.Contains(model))
                    continue;

                teamActiveModels.Add(model);
            }

            if (activeModelIndex < 0 && teamActiveModels.Count > 0)
                activeModelIndex = managedModels.IndexOf(teamActiveModels[0]);
        }

        internal bool TryGetCameraTargetSyncSettings(out Transform preferredRoot, out bool useFullWorldPosition)
        {
            preferredRoot = preferredCameraTargetRoot;
            useFullWorldPosition = placementMode == ManagerPlacementMode.Grid
                && gridCamera == GridCameraMode.Single
                && useGridSingleCameraWorldPosition;
            return syncCameraTargets;
        }

        private void ApplyPlacement(bool force)
        {
            if (!isActiveAndEnabled)
                return;

            EnsureRosterConsistency();

            int layoutHash = BuildPlacementStateHash();
            bool layoutChanged = force || !m_HasAppliedLayout || layoutHash != m_LastAppliedLayoutHash;
            if (layoutChanged)
            {
                ManagerLayoutCoordinator.ApplyLayout(this);
                m_HasAppliedLayout = true;
                m_LastAppliedLayoutHash = BuildPlacementStateHash();
            }

            EnsureCameraTargetsSynced(force || layoutChanged);
        }

        private void RequestPlacement(bool force)
        {
            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying)
            {
                ApplyPlacement(force);
                return;
            }

            m_HasPendingPlacementRequest = true;
            m_HasPendingForcedPlacementRequest |= force;
        }

        public void SwitchToNextModel()
        {
            int nextIndex = GetWrappedNextActiveModelIndex();
            if (nextIndex < 0)
                return;

            m_InputSwitchVersion++;
            ActiveModelIndex = nextIndex;
        }

        public void SwitchToPreviousModel()
        {
            int previousIndex = GetWrappedPreviousActiveModelIndex();
            if (previousIndex < 0)
                return;

            m_InputSwitchVersion++;
            ActiveModelIndex = previousIndex;
        }

        private int GetWrappedNextActiveModelIndex()
        {
            EnsureRosterConsistency();
            if (managedModels.Count == 0)
                return -1;

            int currentIndex = activeModelIndex;
            if (currentIndex < 0 || currentIndex >= managedModels.Count)
                currentIndex = 0;

            return (currentIndex + 1) % managedModels.Count;
        }

        private int GetWrappedPreviousActiveModelIndex()
        {
            EnsureRosterConsistency();
            if (managedModels.Count == 0)
                return -1;

            int currentIndex = activeModelIndex;
            if (currentIndex < 0 || currentIndex >= managedModels.Count)
                currentIndex = 0;

            return (currentIndex - 1 + managedModels.Count) % managedModels.Count;
        }

        private void QueuePlacementRequest(bool force)
        {
            if (!isActiveAndEnabled)
                return;

            m_HasPendingPlacementRequest = true;
            m_HasPendingForcedPlacementRequest |= force;
        }

        private bool TryConsumePendingPlacementRequest(out bool force)
        {
            force = m_HasPendingForcedPlacementRequest;
            bool hasPendingRequest = m_HasPendingPlacementRequest;
            if (!hasPendingRequest)
                return false;

            m_HasPendingPlacementRequest = false;
            m_HasPendingForcedPlacementRequest = false;
            return true;
        }

        private void ClearPendingPlacementRequest()
        {
            m_HasPendingPlacementRequest = false;
            m_HasPendingForcedPlacementRequest = false;
        }

        private void HandleCharacterRegistryChanged()
        {
            MarkManagedModelDiscoveryDirty();
            RequestPlacement(force: true);
        }

        private void MarkManagedModelDiscoveryDirty()
        {
            m_ManagedModelDiscoveryDirty = true;
            MarkRosterConsistencyDirty();
        }

        private void MarkRosterConsistencyDirty()
        {
            m_RosterConsistencyDirty = true;
        }

        private void EnsureCameraTargetsSynced(bool force)
        {
            if (force)
                CameraTargetUtility.ClearCachedBaselines();

            if (!TryGetCameraTargetSyncSettings(out Transform preferredRoot, out bool useFullWorldPosition))
            {
                ResetCameraTargetSyncState();
                return;
            }

            if (placementMode == ManagerPlacementMode.Team
                || (placementMode == ManagerPlacementMode.Grid && gridCamera == GridCameraMode.Team))
            {
                ResetCameraTargetSyncState();
                return;
            }

            GameObject activeModel = ResolveActiveModel();
            if (activeModel == null)
            {
                ResetCameraTargetSyncState();
                return;
            }

            if (!force
                && m_HasSyncedCameraTargets
                && activeModel == m_LastSyncedCameraTargetModel
                && placementMode == m_LastSyncedCameraTargetMode
                && gridCamera == m_LastSyncedGridCameraMode
                && preferredRoot == m_LastSyncedCameraTargetRoot)
            {
                return;
            }

            CameraTargetUtility.SyncDefaultTargets(
                gameObject.scene,
                activeModel,
                preferredRoot,
                useFullWorldPosition,
                CameraTargetUtility.DefaultTargetYOffset);

            m_LastSyncedCameraTargetModel = activeModel;
            m_LastSyncedCameraTargetMode = placementMode;
            m_LastSyncedGridCameraMode = gridCamera;
            m_LastSyncedCameraTargetRoot = preferredRoot;
            m_HasSyncedCameraTargets = true;
        }

        private void ResetCameraTargetSyncState()
        {
            CameraTargetUtility.ClearCachedBaselines();
            m_LastSyncedCameraTargetModel = null;
            m_LastSyncedCameraTargetMode = default;
            m_LastSyncedGridCameraMode = default;
            m_LastSyncedCameraTargetRoot = null;
            m_HasSyncedCameraTargets = false;
        }

        private GameObject ResolveActiveModel()
        {
            if (activeModelIndex < 0 || activeModelIndex >= managedModels.Count)
                return null;

            return managedModels[activeModelIndex];
        }

        private void RefreshManagedModelsFromScene()
        {
            GameObject previousActiveModel = ResolveActiveModel();
            m_DiscoveredModelsScratch.Clear();
            DiscoverSceneManagedModels(gameObject.scene, m_DiscoveredModelsScratch);

            if (AreModelListsEqual(managedModels, m_DiscoveredModelsScratch))
                return;

            managedModels.Clear();
            managedModels.AddRange(m_DiscoveredModelsScratch);
            MarkRosterConsistencyDirty();

            if (previousActiveModel != null)
                activeModelIndex = managedModels.IndexOf(previousActiveModel);

            if (managedModels.Count == 0)
            {
                activeModelIndex = -1;
            }
            else
            {
                activeModelIndex = Mathf.Clamp(activeModelIndex, -1, managedModels.Count - 1);
            }
        }

        private int BuildPlacementStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + gameObject.scene.handle;
                hash = (hash * 31) + autoDiscoverManagedModels.GetHashCode();
                hash = (hash * 31) + syncCameraTargets.GetHashCode();
                hash = (hash * 31) + useGridSingleCameraWorldPosition.GetHashCode();
                hash = (hash * 31) + (preferredCameraTargetRoot != null ? preferredCameraTargetRoot.GetInstanceID() : 0);
                hash = (hash * 31) + (int)placementMode;
                hash = (hash * 31) + (int)teamGame;
                hash = (hash * 31) + (int)gridCamera;
                hash = (hash * 31) + activeModelIndex;

                hash = (hash * 31) + managedModels.Count;
                for (int i = 0; i < managedModels.Count; ++i)
                {
                    GameObject model = managedModels[i];
                    hash = (hash * 31) + (model != null ? model.GetInstanceID() : 0);
                    if (model == null)
                        continue;

                    Transform modelTransform = model.transform;
                    hash = (hash * 31) + model.activeSelf.GetHashCode();
                    hash = (hash * 31) + (modelTransform.parent != null ? modelTransform.parent.GetInstanceID() : 0);
                    hash = (hash * 31) + modelTransform.localPosition.GetHashCode();
                    hash = (hash * 31) + modelTransform.localRotation.GetHashCode();
                }

                hash = (hash * 31) + teamActiveModels.Count;
                for (int i = 0; i < teamActiveModels.Count; ++i)
                {
                    hash = (hash * 31) + (teamActiveModels[i] != null ? teamActiveModels[i].GetInstanceID() : 0);
                }

                return hash;
            }
        }

        private static void Register(CharacterPlacementController controller)
        {
            if (controller == null)
                return;

            PruneNullPlacementControllers();
            if (!s_ActivePlacementControllers.Contains(controller))
                s_ActivePlacementControllers.Add(controller);
        }

        private static void Unregister(CharacterPlacementController controller)
        {
            if (controller == null)
                return;

            s_ActivePlacementControllers.Remove(controller);
        }

        private static void PruneNullPlacementControllers()
        {
            for (int i = s_ActivePlacementControllers.Count - 1; i >= 0; --i)
            {
                if (s_ActivePlacementControllers[i] == null)
                    s_ActivePlacementControllers.RemoveAt(i);
            }
        }

        private static bool AreModelListsEqual(List<GameObject> left, List<GameObject> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; ++i)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static void DiscoverSceneManagedModels(UnityScene scene, List<GameObject> results)
        {
            results.Clear();
            if (!scene.IsValid())
                return;

            DiscoverSceneCharacterControllers(scene, s_DiscoveredControllerScratch, includeInactive: true);
            for (int i = 0; i < s_DiscoveredControllerScratch.Count; ++i)
            {
                HSRCharacterController controller = s_DiscoveredControllerScratch[i];
                if (controller != null && controller.gameObject != null)
                    results.Add(controller.gameObject);
            }
        }

        private static void DiscoverSceneCharacterControllers(UnityScene scene, List<HSRCharacterController> results, bool includeInactive)
        {
            results.Clear();
            if (!scene.IsValid())
                return;

            HSRCharacterController[] foundControllers = FindObjectsByType<HSRCharacterController>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < foundControllers.Length; ++i)
            {
                HSRCharacterController controller = foundControllers[i];
                if (controller == null || controller.gameObject.scene != scene)
                    continue;

                results.Add(controller);
            }

            results.Sort(CompareCharacterControllerHierarchyOrder);
        }

        private static int CompareCharacterControllerHierarchyOrder(HSRCharacterController left, HSRCharacterController right)
        {
            Transform leftTransform = left != null ? left.transform : null;
            Transform rightTransform = right != null ? right.transform : null;
            return CompareTransformHierarchyOrder(leftTransform, rightTransform);
        }

        private static int CompareTransformHierarchyOrder(Transform left, Transform right)
        {
            return TransformSearchUtility.CompareHierarchyOrder(
                left,
                right,
                s_LeftHierarchyScratch,
                s_RightHierarchyScratch);
        }

        private void RemoveInvalidModels(List<GameObject> models)
        {
            if (models == null)
                return;

            m_ModelValidationScratch.Clear();
            for (int i = models.Count - 1; i >= 0; --i)
            {
                GameObject model = models[i];
                if (!IsSceneModelValid(model) || !m_ModelValidationScratch.Add(model))
                    models.RemoveAt(i);
            }

            m_ModelValidationScratch.Clear();
        }

        private void RemoveTeamModelsNotInManagedRoster()
        {
            m_ModelValidationScratch.Clear();
            for (int i = 0; i < managedModels.Count; ++i)
            {
                GameObject model = managedModels[i];
                if (model != null)
                    m_ModelValidationScratch.Add(model);
            }

            for (int i = teamActiveModels.Count - 1; i >= 0; --i)
            {
                if (!m_ModelValidationScratch.Contains(teamActiveModels[i]))
                    teamActiveModels.RemoveAt(i);
            }

            m_ModelValidationScratch.Clear();
        }

        private bool IsSceneModelValid(GameObject model)
        {
            return model != null
                && model != gameObject
                && model.scene.IsValid()
                && model.scene == gameObject.scene
                && model.GetComponent<HSRCharacterController>() != null;
        }
    }
}
