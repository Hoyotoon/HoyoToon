#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon
{
    public class HoyoToonManager : Editor
    {
        #region Quick Access Buttons

        [MenuItem("Assets/HoyoToon/Full Setup", priority = 0)]
        private static void FullSetup()
        {
            // Check if resources are available before proceeding
            if (!HoyoToonResourceManager.ValidateResourcesAvailable())
            {
                return;
            }

            HoyoToonDataManager.GetHoyoToonData();
            HoyoToonMaterialManager.GenerateMaterialsFromJson();
            
            // Setup FBX with integrated humanoid configuration
            SetupFBX();
            
            GameObject selectedObject = HoyoToonSceneManager.AddSelectedObjectToScene();
            if (selectedObject != null)
            {
                HoyoToonMeshManager.GenTangents(selectedObject);
                EditorUtility.DisplayDialog("Full Setup Complete", 
                    "Model has been fully configured and added to the scene with all optimizations applied!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Setup Complete", 
                    "Model configuration completed. You can now manually add the model to the scene.", "OK");
            }
        }

        [MenuItem("Assets/HoyoToon/Mesh/Setup FBX", priority = 10)]
        public static void SetupFBX()
        {
            HoyoToonMeshManager.SetFBXImportSettings(HoyoToonParseManager.GetAssetSelectionPaths());
        }

        // Humanoid configuration is now integrated into FBX setup

        [MenuItem("GameObject/HoyoToon/Mesh/Generate Tangents", priority = 21)]
        private static void GenerateTangents()
        {
            HoyoToonDataManager.GetHoyoToonData();
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Error", "No Model selected. Please select a Model to generate tangents.", "OK");
                HoyoToonLogs.WarningDebug("No Model selected. Please select a Model to generate tangents.");
                return;
            }

            HoyoToonMeshManager.GenTangents(selectedObject);
        }
        
        [MenuItem("GameObject/HoyoToon/Mesh/Reset Tangents", priority = 22)]
        private static void ResetTangents()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Error", "No GameObject selected. Please select a GameObject to reset tangents.", "OK");
                HoyoToonLogs.WarningDebug("No GameObject selected. Please select a GameObject to reset tangents.");
                return;
            }

            HoyoToonMeshManager.ResetTangents(selectedObject);
        }

        [MenuItem("Assets/HoyoToon/Tools/Copy GUID", priority = 50)]
        private static void CopyGUIDToClipboard()
        {
            HoyoToonToolsManager.CopyGUIDToClipboard();
        }

        [MenuItem("HoyoToon/Socials/Twitter", priority = 21)]
        static void MenuHoyoToonTwitter()
        {
            Application.OpenURL("https://www.twitter.com/Meliodas7DL");
        }

        [MenuItem("HoyoToon/Socials/Discord", priority = 20)]
        static void MenuHoyoToonDiscord()
        {
            Application.OpenURL("https://discord.gg/hoyotoon");
        }

        // [MenuItem("HoyoToon/Socials/Documentation", priority = 10)]
        // private static void OpenDocumentation()
        // {
        //     Application.OpenURL("https://docs.hoyotoon.com");
        // }

        [MenuItem("HoyoToon/Socials/Asset Repo", priority = 11)]
        private static void OpenAssetRepo()
        {
            Application.OpenURL("https://github.com/HoyoToon/HoyoToon-Assets");
        }

        #endregion
    }
}
#endif