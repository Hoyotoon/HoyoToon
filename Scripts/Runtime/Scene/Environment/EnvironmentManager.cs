using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Core;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.Runtime.Scene.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("HoyoToon/Scene/Environment Manager")]
    public sealed class EnvironmentManager : MonoBehaviour
    {
        private static readonly List<EnvironmentManager> s_ActiveEnvironmentManagers = new List<EnvironmentManager>();

        [Serializable]
        public sealed class EnvironmentEntry
        {
            [SerializeField] private string label;
            [SerializeField] private GameObject prefab;
            [SerializeField] private GameObject instance;
            [SerializeField] private Transform parentOverride;

            public string Label => label;
            public GameObject Prefab => prefab;
            public GameObject Instance => instance;
            public Transform ParentOverride => parentOverride;

            internal static EnvironmentEntry CreateFromInstance(GameObject instance)
            {
                return new EnvironmentEntry
                {
                    instance = instance
                };
            }

            internal GameObject ResolveExistingObject()
            {
                return instance != null ? instance : prefab;
            }

            internal GameObject EnsureInstance(Transform fallbackParent, bool instantiatePrefabEntries)
            {
                if (instance != null)
                    return instance;

                if (!instantiatePrefabEntries || prefab == null)
                    return null;

                Transform parent = parentOverride != null ? parentOverride : fallbackParent;
                GameObject createdObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    createdObject = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                    if (createdObject != null)
                    {
                        Undo.RegisterCreatedObjectUndo(createdObject, "HoyoToon Instantiate Environment");
                    }
                }
                else
#endif
                {
                    createdObject = parent != null
                        ? Object.Instantiate(prefab, parent)
                        : Object.Instantiate(prefab);
                }

                if (createdObject == null)
                    return null;

                createdObject.name = prefab.name;
                instance = createdObject;
                return instance;
            }
        }

        [Serializable]
        public sealed class GameEnvironmentSet
        {
            [SerializeField] private string label;
            [SerializeField] private Transform root;
            [SerializeField] private int activeEnvironmentIndex;
            [SerializeField] private List<EnvironmentEntry> environments = new List<EnvironmentEntry>();

            public string Label => label;
            public Transform Root => root;
            public int ActiveEnvironmentIndex
            {
                get => activeEnvironmentIndex;
                internal set => activeEnvironmentIndex = value;
            }

            public IReadOnlyList<EnvironmentEntry> Environments => environments;

            internal List<EnvironmentEntry> MutableEnvironments
            {
                get
                {
                    if (environments == null)
                        environments = new List<EnvironmentEntry>();

                    return environments;
                }
            }

            internal static GameEnvironmentSet Create(string label, Transform root, List<EnvironmentEntry> environments)
            {
                return new GameEnvironmentSet
                {
                    label = label,
                    root = root,
                    activeEnvironmentIndex = 0,
                    environments = environments ?? new List<EnvironmentEntry>()
                };
            }
        }

        [SerializeField]
        [Tooltip("Optional root used for child-based environment discovery. If this is unset, the manager transform is used.")]
        private Transform discoveryRoot;

        [SerializeField]
        [Tooltip("When enabled and no game entries are configured, direct children under the discovery root are added as game entries.")]
        private bool autoPopulateGamesFromDiscoveryRoot = true;

        [SerializeField]
        [Tooltip("When enabled, the selected game and environment are applied as soon as this component enables.")]
        private bool autoApplyOnEnable = true;

        [SerializeField]
        [Tooltip("When enabled, prefab entries are instantiated the first time they are selected.")]
        private bool instantiatePrefabEntries = true;

        [SerializeField]
        [Tooltip("When a game has no explicit environment entries, its direct child objects are used as selectable environment instances.")]
        private bool useRootChildrenWhenNoEntries = true;

        [SerializeField]
        [Tooltip("Index of the active game entry.")]
        private int activeGameIndex;

        [SerializeField]
        [Tooltip("Game environment sets managed by this scene.")]
        private List<GameEnvironmentSet> games = new List<GameEnvironmentSet>();

        private bool m_HasScheduledEditModeApply;

        public IReadOnlyList<GameEnvironmentSet> Games => games;
        public int GameCount => games != null ? games.Count : 0;
        public int ActiveGameIndex => ClampGameIndex(activeGameIndex);
        public GameEnvironmentSet ActiveGameSet => GetGameSet(ActiveGameIndex);

        public int ActiveEnvironmentIndex
        {
            get
            {
                GameEnvironmentSet activeGameSet = ActiveGameSet;
                return activeGameSet != null
                    ? ClampEnvironmentIndex(activeGameSet, activeGameSet.ActiveEnvironmentIndex)
                    : -1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistryOnSubsystemRegistration()
        {
            s_ActiveEnvironmentManagers.Clear();
        }

        public static EnvironmentManager GetPrimaryCachedOrFind()
        {
            PruneNullEnvironmentManagers();
            for (int i = 0; i < s_ActiveEnvironmentManagers.Count; ++i)
            {
                EnvironmentManager manager = s_ActiveEnvironmentManagers[i];
                if (manager != null && manager.isActiveAndEnabled)
                    return manager;
            }

            EnvironmentManager[] found = FindObjectsByType<EnvironmentManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; ++i)
                Register(found[i]);

            PruneNullEnvironmentManagers();
            for (int i = 0; i < s_ActiveEnvironmentManagers.Count; ++i)
            {
                EnvironmentManager manager = s_ActiveEnvironmentManagers[i];
                if (manager != null && manager.isActiveAndEnabled)
                    return manager;
            }

            return null;
        }

        public static EnvironmentManager FindForScene(UnityScene scene)
        {
            if (!scene.IsValid())
                return null;

            PruneNullEnvironmentManagers();
            for (int i = 0; i < s_ActiveEnvironmentManagers.Count; ++i)
            {
                EnvironmentManager manager = s_ActiveEnvironmentManagers[i];
                if (manager != null
                    && manager.isActiveAndEnabled
                    && manager.gameObject.scene == scene)
                {
                    return manager;
                }
            }

            EnvironmentManager[] found = FindObjectsByType<EnvironmentManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; ++i)
                Register(found[i]);

            for (int i = 0; i < s_ActiveEnvironmentManagers.Count; ++i)
            {
                EnvironmentManager manager = s_ActiveEnvironmentManagers[i];
                if (manager != null
                    && manager.isActiveAndEnabled
                    && manager.gameObject.scene == scene)
                {
                    return manager;
                }
            }

            return null;
        }

        public void SetActiveGameIndex(int index)
        {
            int clampedIndex = ClampGameIndex(index);
            if (activeGameIndex == clampedIndex)
                return;

            activeGameIndex = clampedIndex;
            ApplyEnvironment();
        }

        public void SetActiveEnvironmentIndex(int index)
        {
            GameEnvironmentSet activeGameSet = ActiveGameSet;
            if (activeGameSet == null)
                return;

            int clampedIndex = ClampEnvironmentIndex(activeGameSet, index);
            if (activeGameSet.ActiveEnvironmentIndex == clampedIndex)
                return;

            activeGameSet.ActiveEnvironmentIndex = clampedIndex;
            ApplyEnvironment();
        }

        public int GetActiveEnvironmentIndex(int gameIndex)
        {
            GameEnvironmentSet gameSet = GetGameSet(gameIndex);
            return gameSet != null ? ClampEnvironmentIndex(gameSet, gameSet.ActiveEnvironmentIndex) : -1;
        }

        public int GetEnvironmentCount(int gameIndex)
        {
            return GetEnvironmentCount(GetGameSet(gameIndex));
        }

        public string GetGameLabel(int gameIndex)
        {
            GameEnvironmentSet gameSet = GetGameSet(gameIndex);
            if (gameSet == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(gameSet.Label))
                return gameSet.Label.Trim();

            return gameSet.Root != null ? gameSet.Root.name : "Game " + (gameIndex + 1);
        }

        public string GetEnvironmentLabel(int gameIndex, int environmentIndex)
        {
            GameEnvironmentSet gameSet = GetGameSet(gameIndex);
            if (gameSet == null || environmentIndex < 0)
                return string.Empty;

            IReadOnlyList<EnvironmentEntry> entries = gameSet.Environments;
            if (entries != null && entries.Count > 0)
            {
                if (environmentIndex >= entries.Count)
                    return string.Empty;

                return GetEnvironmentEntryLabel(entries[environmentIndex], environmentIndex);
            }

            if (!useRootChildrenWhenNoEntries || gameSet.Root == null || environmentIndex >= gameSet.Root.childCount)
                return string.Empty;

            Transform child = gameSet.Root.GetChild(environmentIndex);
            return child != null ? child.name : "Environment " + (environmentIndex + 1);
        }

        public void ApplyEnvironment()
        {
            EnsureSelectionIndices();

            int gameCount = GameCount;
            for (int gameIndex = 0; gameIndex < gameCount; ++gameIndex)
            {
                GameEnvironmentSet gameSet = games[gameIndex];
                if (gameSet == null)
                    continue;

                bool isActiveGame = gameIndex == activeGameIndex;
                if (gameSet.Root != null)
                    SetObjectActive(gameSet.Root.gameObject, isActiveGame);

                ApplyGameEnvironmentSet(gameSet, isActiveGame);
            }

            RuntimeEditorBridge.MarkDirty(this);
        }

        public void CollectManagedObjects(List<Object> results)
        {
            if (results == null)
                return;

            AddUnique(results, this);
            AddUnique(results, gameObject);

            for (int gameIndex = 0; gameIndex < GameCount; ++gameIndex)
            {
                GameEnvironmentSet gameSet = games[gameIndex];
                if (gameSet == null)
                    continue;

                AddUnique(results, gameSet.Root != null ? gameSet.Root.gameObject : null);
                AddUnique(results, gameSet.Root);

                IReadOnlyList<EnvironmentEntry> entries = gameSet.Environments;
                for (int environmentIndex = 0; environmentIndex < (entries != null ? entries.Count : 0); ++environmentIndex)
                {
                    EnvironmentEntry entry = entries[environmentIndex];
                    if (entry == null)
                        continue;

                    AddUnique(results, entry.Instance);
                    AddUnique(results, entry.Instance != null ? entry.Instance.transform : null);
                    AddUnique(results, entry.ParentOverride);
                }

                if (!useRootChildrenWhenNoEntries || gameSet.Root == null || (entries != null && entries.Count > 0))
                    continue;

                for (int childIndex = 0; childIndex < gameSet.Root.childCount; ++childIndex)
                {
                    Transform child = gameSet.Root.GetChild(childIndex);
                    AddUnique(results, child);
                    AddUnique(results, child != null ? child.gameObject : null);
                }
            }
        }

        private void OnEnable()
        {
            Register(this);
            EnsureGameEntriesFromDiscoveryRoot();
            if (autoApplyOnEnable)
                QueueApplyEnvironment();
        }

        private void OnDisable()
        {
            CancelScheduledEditModeApply();
            Unregister(this);
        }

        private void OnDestroy()
        {
            CancelScheduledEditModeApply();
            Unregister(this);
        }

        private void OnValidate()
        {
            EnsureGameEntriesFromDiscoveryRoot();
            EnsureSelectionIndices();
            if (isActiveAndEnabled)
                QueueApplyEnvironment();
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled && useRootChildrenWhenNoEntries)
                QueueApplyEnvironment();
        }

        private void ApplyGameEnvironmentSet(GameEnvironmentSet gameSet, bool isActiveGame)
        {
            if (gameSet == null)
                return;

            IReadOnlyList<EnvironmentEntry> entries = gameSet.Environments;
            int configuredEntryCount = entries != null ? entries.Count : 0;
            int selectedEnvironmentIndex = ClampEnvironmentIndex(gameSet, gameSet.ActiveEnvironmentIndex);

            if (configuredEntryCount > 0)
            {
                Transform fallbackParent = gameSet.Root != null
                    ? gameSet.Root
                    : discoveryRoot != null
                        ? discoveryRoot
                        : transform;

                for (int environmentIndex = 0; environmentIndex < configuredEntryCount; ++environmentIndex)
                {
                    EnvironmentEntry entry = entries[environmentIndex];
                    if (entry == null)
                        continue;

                    bool shouldBeActive = isActiveGame && environmentIndex == selectedEnvironmentIndex;
                    GameObject environmentObject = shouldBeActive
                        ? entry.EnsureInstance(fallbackParent, instantiatePrefabEntries)
                        : entry.Instance;

                    SetObjectActive(environmentObject, shouldBeActive);
                }

                return;
            }

            if (!useRootChildrenWhenNoEntries || gameSet.Root == null)
                return;

            for (int childIndex = 0; childIndex < gameSet.Root.childCount; ++childIndex)
            {
                Transform child = gameSet.Root.GetChild(childIndex);
                SetObjectActive(child != null ? child.gameObject : null, isActiveGame && childIndex == selectedEnvironmentIndex);
            }
        }

        private void QueueApplyEnvironment()
        {
            if (!isActiveAndEnabled)
                return;

            if (Application.isPlaying)
            {
                ApplyEnvironment();
                return;
            }

            if (m_HasScheduledEditModeApply)
                return;

            m_HasScheduledEditModeApply = true;
            RuntimeEditorBridge.ScheduleDelayedEditModeAction(ApplyScheduledEditModeEnvironment);
            RuntimeEditorBridge.RequestPlayerLoopUpdate();
        }

        private void ApplyScheduledEditModeEnvironment()
        {
            m_HasScheduledEditModeApply = false;
            if (this == null || Application.isPlaying || !isActiveAndEnabled)
                return;

            ApplyEnvironment();
        }

        private void CancelScheduledEditModeApply()
        {
            if (!m_HasScheduledEditModeApply)
                return;

            RuntimeEditorBridge.CancelDelayedEditModeAction(ApplyScheduledEditModeEnvironment);
            m_HasScheduledEditModeApply = false;
        }

        private void EnsureSelectionIndices()
        {
            EnsureGameEntriesFromDiscoveryRoot();
            ClampSelectionIndices();
        }

        private void EnsureGameEntriesFromDiscoveryRoot()
        {
            if (!autoPopulateGamesFromDiscoveryRoot)
                return;

            if (games == null)
                games = new List<GameEnvironmentSet>();

            Transform root = discoveryRoot != null ? discoveryRoot : transform;
            if (root == null)
                return;

            GameEnvironmentSet previousActiveGameSet = GetGameSet(activeGameIndex);
            Transform previousActiveGameRoot = previousActiveGameSet != null ? previousActiveGameSet.Root : null;

            Dictionary<Transform, int> previousEnvironmentIndices = new Dictionary<Transform, int>();
            for (int gameIndex = 0; gameIndex < games.Count; ++gameIndex)
            {
                GameEnvironmentSet gameSet = games[gameIndex];
                if (gameSet != null && gameSet.Root != null && !previousEnvironmentIndices.ContainsKey(gameSet.Root))
                    previousEnvironmentIndices.Add(gameSet.Root, gameSet.ActiveEnvironmentIndex);
            }

            List<GameEnvironmentSet> syncedGames = new List<GameEnvironmentSet>();
            for (int childIndex = 0; childIndex < root.childCount; ++childIndex)
            {
                Transform child = root.GetChild(childIndex);
                if (child == null)
                    continue;

                GameEnvironmentSet gameSet = GameEnvironmentSet.Create(child.name, child, BuildEnvironmentEntriesFromChildren(child));
                if (previousEnvironmentIndices.TryGetValue(child, out int previousEnvironmentIndex))
                    gameSet.ActiveEnvironmentIndex = previousEnvironmentIndex;

                syncedGames.Add(gameSet);
            }

            if (AreGameSetsEqual(games, syncedGames))
                return;

            games.Clear();
            games.AddRange(syncedGames);

            activeGameIndex = 0;
            if (previousActiveGameRoot != null)
            {
                for (int gameIndex = 0; gameIndex < games.Count; ++gameIndex)
                {
                    if (games[gameIndex] != null && games[gameIndex].Root == previousActiveGameRoot)
                    {
                        activeGameIndex = gameIndex;
                        break;
                    }
                }
            }

            ClampSelectionIndices();
            RuntimeEditorBridge.MarkDirty(this);
        }

        private void ClampSelectionIndices()
        {
            activeGameIndex = ClampGameIndex(activeGameIndex);
            for (int gameIndex = 0; gameIndex < GameCount; ++gameIndex)
            {
                GameEnvironmentSet gameSet = games[gameIndex];
                if (gameSet != null)
                    gameSet.ActiveEnvironmentIndex = ClampEnvironmentIndex(gameSet, gameSet.ActiveEnvironmentIndex);
            }
        }

        private static bool AreGameSetsEqual(IReadOnlyList<GameEnvironmentSet> current, IReadOnlyList<GameEnvironmentSet> next)
        {
            int currentCount = current != null ? current.Count : 0;
            int nextCount = next != null ? next.Count : 0;
            if (currentCount != nextCount)
                return false;

            for (int gameIndex = 0; gameIndex < currentCount; ++gameIndex)
            {
                GameEnvironmentSet currentSet = current[gameIndex];
                GameEnvironmentSet nextSet = next[gameIndex];
                if (currentSet == null || nextSet == null)
                {
                    if (currentSet != nextSet)
                        return false;

                    continue;
                }

                if (currentSet.Root != nextSet.Root
                    || currentSet.ActiveEnvironmentIndex != nextSet.ActiveEnvironmentIndex
                    || !string.Equals(currentSet.Label, nextSet.Label, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!AreEnvironmentEntriesEqual(currentSet.Environments, nextSet.Environments))
                    return false;
            }

            return true;
        }

        private static bool AreEnvironmentEntriesEqual(IReadOnlyList<EnvironmentEntry> current, IReadOnlyList<EnvironmentEntry> next)
        {
            int currentCount = current != null ? current.Count : 0;
            int nextCount = next != null ? next.Count : 0;
            if (currentCount != nextCount)
                return false;

            for (int environmentIndex = 0; environmentIndex < currentCount; ++environmentIndex)
            {
                EnvironmentEntry currentEntry = current[environmentIndex];
                EnvironmentEntry nextEntry = next[environmentIndex];
                if (currentEntry == null || nextEntry == null)
                    return currentEntry == nextEntry;

                if (currentEntry.Instance != nextEntry.Instance
                    || currentEntry.Prefab != nextEntry.Prefab
                    || currentEntry.ParentOverride != nextEntry.ParentOverride
                    || !string.Equals(currentEntry.Label, nextEntry.Label, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private int ClampGameIndex(int index)
        {
            int gameCount = GameCount;
            if (gameCount <= 0)
                return -1;

            return Mathf.Clamp(index, 0, gameCount - 1);
        }

        private int ClampEnvironmentIndex(GameEnvironmentSet gameSet, int index)
        {
            int environmentCount = GetEnvironmentCount(gameSet);
            if (environmentCount <= 0)
                return -1;

            return Mathf.Clamp(index, -1, environmentCount - 1);
        }

        private int GetEnvironmentCount(GameEnvironmentSet gameSet)
        {
            if (gameSet == null)
                return 0;

            IReadOnlyList<EnvironmentEntry> entries = gameSet.Environments;
            if (entries != null && entries.Count > 0)
                return entries.Count;

            return useRootChildrenWhenNoEntries && gameSet.Root != null
                ? gameSet.Root.childCount
                : 0;
        }

        private GameEnvironmentSet GetGameSet(int gameIndex)
        {
            return gameIndex >= 0 && gameIndex < GameCount ? games[gameIndex] : null;
        }

        private static string GetEnvironmentEntryLabel(EnvironmentEntry entry, int index)
        {
            if (entry == null)
                return "Environment " + (index + 1);

            if (!string.IsNullOrWhiteSpace(entry.Label))
                return entry.Label.Trim();

            GameObject existingObject = entry.ResolveExistingObject();
            return existingObject != null ? existingObject.name : "Environment " + (index + 1);
        }

        private static List<EnvironmentEntry> BuildEnvironmentEntriesFromChildren(Transform gameRoot)
        {
            List<EnvironmentEntry> entries = new List<EnvironmentEntry>();
            if (gameRoot == null)
                return entries;

            for (int childIndex = 0; childIndex < gameRoot.childCount; ++childIndex)
            {
                Transform child = gameRoot.GetChild(childIndex);
                if (child != null)
                    entries.Add(EnvironmentEntry.CreateFromInstance(child.gameObject));
            }

            return entries;
        }

        private static void SetObjectActive(GameObject target, bool isActive)
        {
            if (target == null || target.activeSelf == isActive)
                return;

            target.SetActive(isActive);
            RuntimeEditorBridge.MarkDirty(target);
        }

        private static void Register(EnvironmentManager manager)
        {
            if (manager == null)
                return;

            PruneNullEnvironmentManagers();
            if (!s_ActiveEnvironmentManagers.Contains(manager))
                s_ActiveEnvironmentManagers.Add(manager);
        }

        private static void Unregister(EnvironmentManager manager)
        {
            if (manager == null)
                return;

            s_ActiveEnvironmentManagers.Remove(manager);
        }

        private static void PruneNullEnvironmentManagers()
        {
            for (int i = s_ActiveEnvironmentManagers.Count - 1; i >= 0; --i)
            {
                if (s_ActiveEnvironmentManagers[i] == null)
                    s_ActiveEnvironmentManagers.RemoveAt(i);
            }
        }

        private static void AddUnique(List<Object> objects, Object value)
        {
            if (value != null && !objects.Contains(value))
                objects.Add(value);
        }
    }
}
