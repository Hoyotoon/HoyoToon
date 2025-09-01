#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon
{
    public class HoyoToonSceneManager
    {
        [MenuItem("GameObject/HoyoToon/Scene/Add AvatarLight", priority = 0)]
        public static void AddAvatarLight()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Error", "No Model selected. Please select a Model to add AvatarLight.", "OK");
                HoyoToonLogs.WarningDebug("No Model selected. Please select a Model to add AvatarLight.");
                return;
            }

            AddAvatarLight(selectedObject);
        }

        /// <summary>
        /// Adds AvatarLight to a specific target GameObject
        /// </summary>
        /// <param name="targetObject">The GameObject to add AvatarLight to</param>
        public static void AddAvatarLight(GameObject targetObject)
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Error", "No target object provided for AvatarLight.", "OK");
                HoyoToonLogs.WarningDebug("No target object provided for AvatarLight.");
                return;
            }

#if VRC_SDK_VRCSDK3
            var avatarLightPrefab = Resources.Load<GameObject>("Prefabs/AvatarLight VRC");
            if (avatarLightPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "AvatarLight VRC prefab not found in Resources/Prefabs.", "OK");
                HoyoToonLogs.WarningDebug("AvatarLight VRC prefab not found in Resources/Prefabs.");
                return;
            }
#else
            var avatarLightPrefab = Resources.Load<GameObject>("Prefabs/AvatarLight Default");
            if (avatarLightPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "AvatarLight Default prefab not found in Resources/Prefabs.", "OK");
                HoyoToonLogs.WarningDebug("AvatarLight Default prefab not found in Resources/Prefabs.");
                return;
            }
#endif

            var avatarLight = UnityEngine.Object.Instantiate(avatarLightPrefab, targetObject.transform);
            avatarLight.name = "AvatarLight";
            avatarLight.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            avatarLight.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(avatarLight, "Add AvatarLight");
            Selection.activeGameObject = avatarLight;
            EditorGUIUtility.PingObject(avatarLight);
        }

        [MenuItem("GameObject/HoyoToon/Scene/Add Post Processing", priority = 1)]
        private static void AddPostProcessing()
        {
            Camera mainCamera = Camera.main;
            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>();

            if (mainCamera != null)
            {
                AttachPostProcessing(mainCamera);
            }
            else if (cameras.Length > 0)
            {
                EditorUtility.DisplayDialog("Main Camera Not Found", "No camera with the 'MainCamera' tag was found. Post-processing will be added to the first camera: " + cameras[0].name, "OK");
                AttachPostProcessing(cameras[0]);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "No cameras found in the scene. Please make sure you have at least one camera in your scene.", "OK");
                HoyoToonLogs.ErrorDebug("No cameras found in the scene. Please make sure you have at least one camera in your scene.");
            }
        }

        private static void AttachPostProcessing(Camera camera)
        {
            HoyoToonPostProcessing postProcessing = camera.gameObject.GetComponent<HoyoToonPostProcessing>();
            if (postProcessing == null)
            {
                postProcessing = camera.gameObject.AddComponent<HoyoToonPostProcessing>();
                Undo.RegisterCreatedObjectUndo(postProcessing, "Add PostProcessing");
                Selection.activeGameObject = postProcessing.gameObject;
                EditorGUIUtility.PingObject(postProcessing);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"HoyoToon Post Processing is already attached to the selected camera: {camera.name}", "OK");
                HoyoToonLogs.ErrorDebug("HoyoToon Post Processing is already attached to the selected camera.");
            }
        }

        public static GameObject AddSelectedObjectToScene()
        {
            GameObject selectedObject = Selection.activeObject as GameObject;
            if (selectedObject != null)
            {
                GameObject instance = GameObject.Instantiate(selectedObject);
                instance.name = selectedObject.name;
                Undo.RegisterCreatedObjectUndo(instance, "Add Selected Object to Scene");
                return instance;
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "No valid model selected. Please select a model to add to the scene.", "OK");
                HoyoToonLogs.ErrorDebug("No valid model selected. Please select a model to add to the scene.");
                return null;
            }
        }
    }
}
#endif