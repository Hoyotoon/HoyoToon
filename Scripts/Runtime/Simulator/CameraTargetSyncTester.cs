using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Simulator.Camera
{
    [DisallowMultipleComponent]
    public sealed class CameraTargetSyncTester : MonoBehaviour
    {
        [SerializeField] private GameObject activeModel;
        [SerializeField] private Transform preferredRoot;
        [SerializeField] private bool useFullWorldPosition;
        [SerializeField] private float yOffset = CameraTargetUtility.DefaultTargetYOffset;

        public GameObject ActiveModel => activeModel;
        public Transform PreferredRoot => preferredRoot;
        public bool UseFullWorldPosition => useFullWorldPosition;
        public float YOffset => yOffset;

        public bool SyncNow()
        {
            bool changed = CameraTargetUtility.SyncDefaultTargets(
                ResolveScene(),
                activeModel,
                preferredRoot,
                useFullWorldPosition,
                yOffset);

            Debug.Log(
                changed
                    ? $"Camera target sync applied for '{name}'."
                    : $"Camera target sync made no changes for '{name}'.",
                this);

            return changed;
        }

        public void ClearBaselines()
        {
            CameraTargetUtility.ClearCachedBaselines();
        }

        public string BuildStatusReport()
        {
            UnityScene scene = ResolveScene();
            Transform centerTarget = CameraTargetUtility.ResolveCenterTarget(scene, preferredRoot);
            Transform headTarget = CameraTargetUtility.ResolveHeadTarget(scene, preferredRoot);
            Transform rootBone = CameraTargetUtility.ResolveRootBone(activeModel);
            Transform headBone = CameraTargetUtility.ResolveHeadBone(activeModel);
            bool hasRequiredTargets = CameraTargetUtility.HasDefaultTargets(scene, preferredRoot);

            return
                $"Scene: {FormatScene(scene)}\n" +
                $"Active Model: {FormatObject(activeModel)}\n" +
                $"Preferred Root: {FormatObject(preferredRoot)}\n" +
                $"Has Required Target Pair: {hasRequiredTargets}\n" +
                $"Center Target: {FormatObject(centerTarget)}\n" +
                $"Head Target: {FormatObject(headTarget)}\n" +
                $"Root Bone: {FormatObject(rootBone)}\n" +
                $"Head Bone: {FormatObject(headBone)}\n" +
                $"Use Full World Position: {useFullWorldPosition}\n" +
                $"Y Offset: {yOffset:0.###}";
        }

        [ContextMenu("Test Sync Camera Targets")]
        private void SyncFromContextMenu()
        {
            SyncNow();
        }

        [ContextMenu("Clear Cached Camera Target Baselines")]
        private void ClearBaselinesFromContextMenu()
        {
            ClearBaselines();
            Debug.Log("Cleared cached camera target baselines.", this);
        }

        private UnityScene ResolveScene()
        {
            if (preferredRoot != null)
            {
                return preferredRoot.gameObject.scene;
            }

            if (activeModel != null)
            {
                return activeModel.scene;
            }

            return gameObject.scene;
        }

        private static string FormatObject(Object target)
        {
            return target != null ? target.name : "<missing>";
        }

        private static string FormatScene(UnityScene scene)
        {
            return scene.IsValid() ? scene.name : "<invalid>";
        }
    }
}
