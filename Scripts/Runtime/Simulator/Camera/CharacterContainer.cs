using UnityEngine;
using UnityEngine.Animations;

namespace HoyoToon.Simulator.Camera
{
    public class CharacterContainer : MonoBehaviour
    {
        [Header("Camera Target Objects")]
        [SerializeField] private GameObject characterLookAtHead;
        [SerializeField] private GameObject characterLookAtCentre;

        [Header("Manager Integration")]
        [SerializeField] private HoyoToonManager manager;
        [SerializeField] private bool autoFindManager = true;

        [Header("Auto-Update Settings")]
        [SerializeField] private bool autoDetectActiveCharacterChanges = true;
        [SerializeField] private float checkInterval = 0.1f;

        [Header("Camera Integration")]
        [SerializeField] private DynamicCameraTargetController cameraTargetController;
        [SerializeField] private bool autoFindCameraController = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private ParentConstraint headConstraint;
        private ParentConstraint centreConstraint;
        private GameObject currentActiveCharacter;
        private float lastCheckTime;
        private Transform currentHeadTarget;
        private Transform currentCentreTarget;
        private Transform lastCameraHeadReference;
        private Transform lastCameraCentreReference;

        public GameObject CurrentActiveCharacter => currentActiveCharacter;
        public HoyoToonManager Manager => manager;

        private void Awake()
        {
            if (autoFindManager && manager == null)
            {
                manager = FindAnyObjectByType<HoyoToonManager>();
            }

            if (manager == null)
            {
                Debug.LogError($"[{nameof(CharacterContainer)}] No HoyoToonManager found!", this);
                enabled = false;
                return;
            }

            if (autoFindCameraController && cameraTargetController == null)
            {
                cameraTargetController = FindAnyObjectByType<DynamicCameraTargetController>();
            }

            EnsureConstraints();
        }

        private void Start()
        {
            RefreshActiveCharacter(true);
        }

        private void Update()
        {
            if (!autoDetectActiveCharacterChanges || manager == null) return;
            if (Time.time - lastCheckTime < checkInterval) return;

            lastCheckTime = Time.time;
            RefreshActiveCharacter(false);
        }

        private void EnsureConstraints()
        {
            EnsureConstraint(characterLookAtHead, ref headConstraint);
            EnsureConstraint(characterLookAtCentre, ref centreConstraint);
        }

        private static void EnsureConstraint(GameObject targetObject, ref ParentConstraint constraint)
        {
            if (targetObject == null)
            {
                return;
            }

            if (!targetObject.TryGetComponent(out constraint))
                constraint = targetObject.AddComponent<ParentConstraint>();
        }

        private void RefreshActiveCharacter(bool forceApply)
        {
            GameObject nextCharacter = ResolveActiveCharacter();
            if (nextCharacter == null)
            {
                return;
            }

            if (!forceApply && nextCharacter == currentActiveCharacter)
            {
                return;
            }

            SwitchToModel(nextCharacter);
        }

        private GameObject ResolveActiveCharacter()
        {
            if (IsUsableModel(manager.ActiveModel))
            {
                return manager.ActiveModel;
            }

            if (IsUsableModel(currentActiveCharacter))
            {
                return currentActiveCharacter;
            }

            return FindFirstActiveModel();
        }

        private GameObject FindFirstActiveModel()
        {
            var models = manager.ManagedModels;
            for (int index = 0; index < models.Count; index++)
            {
                if (IsUsableModel(models[index]))
                {
                    return models[index];
                }
            }

            return null;
        }

        private static bool IsUsableModel(GameObject model)
        {
            return model != null && model.activeInHierarchy;
        }

        private void SwitchToModel(GameObject model)
        {
            var previous = currentActiveCharacter;
            currentActiveCharacter = model;

            UpdateCameraTargets(forceCameraRetarget: true);

            if (showDebugInfo)
            {
                string prevName = previous != null ? previous.name : "None";
                Debug.Log($"[{nameof(CharacterContainer)}] Switched from '{prevName}' to '{currentActiveCharacter.name}'");
            }
        }

        private void UpdateCameraTargets(bool forceCameraRetarget = false)
        {
            if (currentActiveCharacter == null) return;

            ResolveCameraTargets(out Transform headTarget, out Transform centreTarget);

            bool headChanged = ApplyConstraint(headConstraint, headTarget, ref currentHeadTarget, forceCameraRetarget);
            bool centreChanged = ApplyConstraint(centreConstraint, centreTarget, ref currentCentreTarget, forceCameraRetarget);

            Transform headReference = characterLookAtHead != null ? characterLookAtHead.transform : null;
            Transform centreReference = characterLookAtCentre != null ? characterLookAtCentre.transform : null;
            bool cameraReferencesChanged = headReference != lastCameraHeadReference || centreReference != lastCameraCentreReference;

            if (cameraTargetController != null && characterLookAtCentre != null && characterLookAtHead != null && (forceCameraRetarget || headChanged || centreChanged || cameraReferencesChanged))
            {
                lastCameraHeadReference = headReference;
                lastCameraCentreReference = centreReference;
                cameraTargetController.SetCharacterReferences(
                    characterLookAtCentre.transform,
                    characterLookAtHead.transform);
                cameraTargetController.StartTransition();
            }
        }

        private void ResolveCameraTargets(out Transform headTarget, out Transform centreTarget)
        {
            if (currentActiveCharacter.TryGetComponent<CharacterCameraSetup>(out var cameraSetup) && cameraSetup.HasValidReferences)
            {
                headTarget = cameraSetup.CharacterHead;
                centreTarget = cameraSetup.CharacterCenter;
                return;
            }

            if (showDebugInfo)
                Debug.Log($"[{nameof(CharacterContainer)}] No CharacterCameraSetup on '{currentActiveCharacter.name}', using root as fallback");

            headTarget = currentActiveCharacter.transform;
            centreTarget = currentActiveCharacter.transform;
        }

        private static bool ApplyConstraint(ParentConstraint constraint, Transform target, ref Transform currentTarget, bool force)
        {
            if (constraint == null || target == null) return false;

            if (!force && currentTarget == target)
                return false;

            while (constraint.sourceCount > 0)
                constraint.RemoveSource(0);

            constraint.AddSource(new ConstraintSource
            {
                sourceTransform = target,
                weight = 1f
            });

            constraint.constraintActive = true;
            constraint.enabled = true;
            currentTarget = target;
            return true;
        }

        // Public API for editor tooling
        public void SetManager(HoyoToonManager mgr)
        {
            manager = mgr;
        }

        public void SetCameraTargetObjects(GameObject headObject, GameObject centreObject)
        {
            characterLookAtHead = headObject;
            characterLookAtCentre = centreObject;
            EnsureConstraints();
            lastCameraHeadReference = null;
            lastCameraCentreReference = null;
            if (currentActiveCharacter != null)
                UpdateCameraTargets(forceCameraRetarget: true);
        }

        public void ForceRefresh()
        {
            if (manager != null)
                manager.PruneManagedModels();
            RefreshActiveCharacter(true);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug Character Info")]
        private void DebugCharacterInfo()
        {
            if (manager == null)
            {
                Debug.Log($"[{nameof(CharacterContainer)}] No manager assigned.");
                return;
            }

            var models = manager.ManagedModels;
            Debug.Log($"=== Character Container Debug ===");
            Debug.Log($"Manager: '{manager.name}' | Managed Models: {models.Count}");
            Debug.Log($"Active Character: {(currentActiveCharacter != null ? currentActiveCharacter.name : "None")}");

            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] == null) continue;
                string active = models[i] == currentActiveCharacter ? " <- CURRENT" : "";
                string hierarchy = models[i].activeInHierarchy ? "ACTIVE" : "inactive";
                Debug.Log($"  [{i}] '{models[i].name}' - {hierarchy}{active}");
            }
        }
#endif
    }
}
