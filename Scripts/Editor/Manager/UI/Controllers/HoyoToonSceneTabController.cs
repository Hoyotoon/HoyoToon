using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Scene Tab Controller
    /// Handles scene setup, lighting, and model instantiation
    /// Updated to use the new modular UI system
    /// </summary>
    public class HoyoToonSceneTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Scene";
        public override bool RequiresModel => false; // Scene tab can work without model

        private GameObject sceneInstance;
        private Light[] sceneLights;
        private Camera sceneCamera;

        // Cache for optimized scene instance detection
        private GameObject lastCachedModel;
        private string lastSceneName;

        // Public method to set scene instance from external sources (like main Add to Scene button)
        public void SetSceneInstance(GameObject instance)
        {
            sceneInstance = instance;
            RefreshTabContent(); // Refresh the display to show the new instance
        }

        // Public method to clear scene instance when model is removed externally
        public void ClearSceneInstance()
        {
            sceneInstance = null;
            RefreshTabContent(); // Refresh the display to reflect removal
        }

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Scene tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            // Note: "Add to Scene" and "Remove from Scene" functionality is now provided by the dynamic main button at the bottom
            // Removed "Remove from Scene" quick action since the main button now handles both add and remove operations

            // Generate Tangents action (only if model is in scene and tangents can be generated)
            if (sceneInstance != null && CanGenerateTangentsForSceneInstance())
            {
                actions.Add(new QuickAction("Generate Tangents", () =>
                {
                    try
                    {
                        GenerateTangentsForSceneInstance();
                    }
                    catch (System.Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to generate tangents: {e.Message}", "OK");
                    }
                }));
            }

            // Reset Tangents action (only if model is in scene and tangents have been generated)
            if (sceneInstance != null && HasTangentsGeneratedForSceneInstance())
            {
                actions.Add(new QuickAction("Reset Tangents", () =>
                {
                    try
                    {
                        if (EditorUtility.DisplayDialog("Confirm Reset",
                            "This will reset the model's tangents to their original state. Continue?",
                            "Reset", "Cancel"))
                        {
                            ResetTangentsForSceneInstance();
                        }
                    }
                    catch (System.Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to reset tangents: {e.Message}", "OK");
                    }
                }));
            }

            // Setup Lighting action
            actions.Add(new QuickAction("Setup Lighting", () =>
            {
                try
                {
                    SetupOptimalLighting();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Lighting setup failed: {e.Message}", "OK");
                }
            }));

            // Add AvatarLight action (if model in scene)
            if (sceneInstance != null)
            {
                actions.Add(new QuickAction("Add AvatarLight", () =>
                {
                    try
                    {
                        HoyoToonSceneManager.AddAvatarLight(sceneInstance);
                    }
                    catch (System.Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to add AvatarLight: {e.Message}", "OK");
                    }
                }));
            }

            return actions;
        }

        #endregion

        protected override void CreateTabContent()
        {
            // Auto-detect model in scene on every refresh
            RefreshSceneModelDetection();

            // Scene Overview Section
            contentView.Add(CreateHoyoToonSectionHeader("Scene Setup"));
            CreateSceneOverviewSection();

            if (IsModelAvailable())
            {
                // Model in Scene & Tangent Generation Section (combined)
                contentView.Add(CreateHoyoToonSectionHeader("Model in Scene & Tangents"));
                CreateCombinedModelAndTangentSection();
            }

            // Lighting Section
            contentView.Add(CreateHoyoToonSectionHeader("Lighting Setup"));
            CreateLightingSection();

            // Camera Section
            contentView.Add(CreateHoyoToonSectionHeader("Camera Setup"));
            CreateCameraSection();
        }

        private void RefreshSceneModelDetection()
        {
            // Only refresh if model or scene changed to avoid expensive GameObject.Find calls
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string currentSceneName = currentScene.name;

            if (selectedModel == lastCachedModel && currentSceneName == lastSceneName && sceneInstance != null)
            {
                // Use cached result if model and scene haven't changed
                return;
            }

            // Update cache tracking
            lastCachedModel = selectedModel;
            lastSceneName = currentSceneName;

            // Auto-detect if the selected model is in the scene
            if (IsModelAvailable())
            {
                sceneInstance = GameObject.Find(selectedModel.name);
            }
            else
            {
                sceneInstance = null;
            }
        }

        private void CreateSceneOverviewSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Current scene info
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            container.Add(CreateHoyoToonInfoRow("Current Scene:", currentScene.name));
            container.Add(CreateHoyoToonInfoRow("Scene Path:", currentScene.path));
            container.Add(CreateHoyoToonInfoRow("Is Dirty:", currentScene.isDirty ? "Yes" : "No",
                currentScene.isDirty ? Color.yellow : Color.green));

            // Scene objects count
            var rootObjects = currentScene.GetRootGameObjects();
            container.Add(CreateHoyoToonInfoRow("Root Objects:", rootObjects.Length.ToString()));

            // Lighting info
            var renderSettings = RenderSettings.fog;
            container.Add(CreateHoyoToonInfoRow("Fog Enabled:", RenderSettings.fog ? "Yes" : "No"));
            container.Add(CreateHoyoToonInfoRow("Ambient Mode:", RenderSettings.ambientMode.ToString()));

            contentView.Add(container);
        }

        private void CreateCombinedModelAndTangentSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Auto-detect model instance each time this section is created
            // First check if our cached reference is still valid
            if (sceneInstance != null && sceneInstance.name != selectedModel.name)
            {
                sceneInstance = null; // Clear if name doesn't match (renamed or different model)
            }

            // If no cached instance or it was cleared, try to find it
            if (sceneInstance == null)
            {
                sceneInstance = GameObject.Find(selectedModel.name);
            }

            // Double-check that our reference is still valid (object wasn't destroyed)
            bool modelInScene = sceneInstance != null;
            if (modelInScene)
            {
                try
                {
                    // This will throw if the object was destroyed but reference wasn't cleared
                    var name = sceneInstance.name;
                }
                catch
                {
                    sceneInstance = null;
                    modelInScene = false;
                }
            }

            // Model in Scene Status
            container.Add(CreateHoyoToonInfoRow("Model in Scene:", modelInScene ? "Yes" : "No",
                modelInScene ? Color.green : Color.yellow));

            if (modelInScene && sceneInstance != null)
            {
                container.Add(CreateHoyoToonInfoRow("Instance Name:", sceneInstance.name));
                container.Add(CreateHoyoToonInfoRow("Position:", sceneInstance.transform.position.ToString("F2")));
                container.Add(CreateHoyoToonInfoRow("Rotation:", sceneInstance.transform.eulerAngles.ToString("F1")));
                container.Add(CreateHoyoToonInfoRow("Scale:", sceneInstance.transform.localScale.ToString("F2")));

                // Model status
                var animator = sceneInstance.GetComponent<Animator>();
                if (animator != null)
                {
                    container.Add(CreateHoyoToonInfoRow("Has Animator:", "Yes", Color.green));
                    container.Add(CreateHoyoToonInfoRow("Avatar Valid:", animator.avatar != null ? "Yes" : "No",
                        animator.avatar != null ? Color.green : Color.red));
                }

                // Tangent Status Section
                if (sceneInstance != null)
                {
                    bool canGenerateTangents = CanGenerateTangentsForSceneInstance();
                    bool hasTangentsGenerated = HasTangentsGeneratedForSceneInstance();

                    container.Add(CreateHoyoToonInfoRow("Can Generate Tangents:", canGenerateTangents ? "Yes" : "No",
                        canGenerateTangents ? Color.green : Color.yellow));

                    if (canGenerateTangents)
                    {
                        container.Add(CreateHoyoToonInfoRow("Tangents Generated:", hasTangentsGenerated ? "Yes" : "No",
                            hasTangentsGenerated ? Color.green : Color.yellow));

                        // Action buttons for tangent operations
                        var buttonsContainer = new VisualElement();
                        buttonsContainer.style.flexDirection = FlexDirection.Row;
                        buttonsContainer.style.marginTop = 10;
                        buttonsContainer.style.justifyContent = Justify.FlexStart;

                        if (!hasTangentsGenerated)
                        {
                            var generateBtn = CreateHoyoToonStyledButton("Generate Tangents", () =>
                            {
                                try
                                {
                                    GenerateTangentsForSceneInstance();
                                    RefreshTabContent(); // Refresh to update status
                                    EditorUtility.DisplayDialog("Success", "Tangents generated successfully!", "OK");
                                }
                                catch (System.Exception e)
                                {
                                    EditorUtility.DisplayDialog("Error", $"Failed to generate tangents: {e.Message}", "OK");
                                }
                            });
                            generateBtn.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1.0f);
                            buttonsContainer.Add(generateBtn);

                            container.Add(CreateHoyoToonWarningBox("Tangents have not been generated for this model. Click 'Generate Tangents' to create them."));
                        }
                        else
                        {
                            var regenerateBtn = CreateHoyoToonStyledButton("Regenerate Tangents", () =>
                            {
                                if (EditorUtility.DisplayDialog("Confirm Regeneration",
                                    "This will regenerate tangents for the model. Continue?", "Yes", "Cancel"))
                                {
                                    try
                                    {
                                        GenerateTangentsForSceneInstance();
                                        RefreshTabContent(); // Refresh to update status
                                        EditorUtility.DisplayDialog("Success", "Tangents regenerated successfully!", "OK");
                                    }
                                    catch (System.Exception e)
                                    {
                                        EditorUtility.DisplayDialog("Error", $"Failed to regenerate tangents: {e.Message}", "OK");
                                    }
                                }
                            });
                            regenerateBtn.style.backgroundColor = new Color(0.6f, 0.4f, 0.2f, 1.0f);
                            regenerateBtn.style.marginRight = 10;
                            buttonsContainer.Add(regenerateBtn);

                            var resetBtn = CreateHoyoToonStyledButton("Reset Tangents", () =>
                            {
                                if (EditorUtility.DisplayDialog("Confirm Reset",
                                    "This will reset the model's tangents to their original state. Continue?",
                                    "Reset", "Cancel"))
                                {
                                    try
                                    {
                                        ResetTangentsForSceneInstance();
                                        RefreshTabContent(); // Refresh to update status
                                        EditorUtility.DisplayDialog("Success", "Tangents reset successfully!", "OK");
                                    }
                                    catch (System.Exception e)
                                    {
                                        EditorUtility.DisplayDialog("Error", $"Failed to reset tangents: {e.Message}", "OK");
                                    }
                                }
                            });
                            resetBtn.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f, 1.0f);
                            buttonsContainer.Add(resetBtn);

                            container.Add(CreateHoyoToonSuccessBox("Tangents have been generated for this model."));
                        }

                        container.Add(buttonsContainer);
                    }
                    else
                    {
                        // Determine why tangents can't be generated
                        string reason = GetTangentGenerationRestrictionReasonForSceneInstance();
                        container.Add(CreateHoyoToonInfoBox($"Tangent generation is not available: {reason}"));
                    }
                }

                container.Add(CreateHoyoToonSuccessBox("Model is successfully instantiated in the scene"));

                // Combined details section showing both mesh info and tangent details
                var detailsContainer = new VisualElement();
                detailsContainer.style.marginTop = 15;
                detailsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
                detailsContainer.style.borderTopLeftRadius = 5;
                detailsContainer.style.borderTopRightRadius = 5;
                detailsContainer.style.borderBottomLeftRadius = 5;
                detailsContainer.style.borderBottomRightRadius = 5;
                detailsContainer.style.paddingTop = 10;
                detailsContainer.style.paddingBottom = 10;
                detailsContainer.style.paddingLeft = 10;
                detailsContainer.style.paddingRight = 10;

                var detailsLabel = new Label("Model & Tangent Details:");
                detailsLabel.style.fontSize = 11;
                detailsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                detailsLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
                detailsLabel.style.marginBottom = 5;
                detailsContainer.Add(detailsLabel);

                // Show body type information
                var bodyType = HoyoToonParseManager.currentBodyType;
                detailsContainer.Add(CreateHoyoToonInfoRow("Current Body Type:", bodyType.ToString()));

                // Show comprehensive mesh information
                var meshFilters = sceneInstance.GetComponentsInChildren<MeshFilter>();
                var skinnedMeshRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();
                var meshRenderers = sceneInstance.GetComponentsInChildren<MeshRenderer>();
                var totalMeshes = meshFilters.Length + skinnedMeshRenderers.Length;

                detailsContainer.Add(CreateHoyoToonInfoRow("Total Meshes:", totalMeshes.ToString()));
                detailsContainer.Add(CreateHoyoToonInfoRow("Mesh Filters:", meshFilters.Length.ToString()));
                detailsContainer.Add(CreateHoyoToonInfoRow("Skinned Mesh Renderers:", skinnedMeshRenderers.Length.ToString()));
                detailsContainer.Add(CreateHoyoToonInfoRow("Mesh Renderers:", meshRenderers.Length.ToString()));

                container.Add(detailsContainer);
            }
            else
            {
                container.Add(CreateHoyoToonWarningBox("Model is not instantiated in the scene"));
            }

            contentView.Add(container);
        }

        private void CreateLightingSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Find lights in scene
            sceneLights = Object.FindObjectsOfType<Light>();
            container.Add(CreateHoyoToonInfoRow("Lights in Scene:", sceneLights.Length.ToString()));

            // Lighting settings
            container.Add(CreateHoyoToonInfoRow("Ambient Source:", RenderSettings.ambientMode.ToString()));
            container.Add(CreateHoyoToonInfoRow("Ambient Color:", ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight)));
            container.Add(CreateHoyoToonInfoRow("Fog Enabled:", RenderSettings.fog ? "Yes" : "No"));

            if (sceneLights.Length > 0)
            {
                container.Add(CreateHoyoToonSubsectionHeader("Scene Lights:"));

                foreach (var light in sceneLights)
                {
                    var lightInfo = new VisualElement();
                    lightInfo.style.marginLeft = 15;
                    lightInfo.style.marginBottom = 5;
                    lightInfo.style.paddingTop = 5;
                    lightInfo.style.paddingBottom = 5;
                    lightInfo.style.paddingLeft = 10;
                    lightInfo.style.paddingRight = 10;
                    lightInfo.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.5f);
                    lightInfo.style.borderTopLeftRadius = 4;
                    lightInfo.style.borderTopRightRadius = 4;
                    lightInfo.style.borderBottomLeftRadius = 4;
                    lightInfo.style.borderBottomRightRadius = 4;

                    var lightHeader = new Label($"{light.name} ({light.type})");
                    lightHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    lightHeader.style.color = new Color(0.8f, 0.9f, 1f);

                    var lightDetails = new VisualElement();
                    lightDetails.style.marginLeft = 10;
                    lightDetails.style.marginTop = 3;

                    lightDetails.Add(CreateHoyoToonInfoRow("Intensity:", light.intensity.ToString("F2")));
                    lightDetails.Add(CreateHoyoToonInfoRow("Color:", ColorUtility.ToHtmlStringRGB(light.color)));
                    lightDetails.Add(CreateHoyoToonInfoRow("Enabled:", light.enabled ? "Yes" : "No",
                        light.enabled ? Color.green : Color.red));

                    if (light.type == LightType.Directional)
                    {
                        lightDetails.Add(CreateHoyoToonInfoRow("Rotation:", light.transform.eulerAngles.ToString("F1")));
                    }

                    lightInfo.Add(lightHeader);
                    lightInfo.Add(lightDetails);
                    container.Add(lightInfo);
                }
            }
            else
            {
                container.Add(CreateHoyoToonWarningBox("No lights found in scene - model may appear dark"));
            }

            contentView.Add(container);
        }

        private void CreateCameraSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Find main camera
            sceneCamera = Camera.main;
            if (sceneCamera == null)
                sceneCamera = Object.FindObjectOfType<Camera>();

            if (sceneCamera != null)
            {
                container.Add(CreateHoyoToonInfoRow("Main Camera:", sceneCamera.name));
                container.Add(CreateHoyoToonInfoRow("Position:", sceneCamera.transform.position.ToString("F2")));
                container.Add(CreateHoyoToonInfoRow("Field of View:", sceneCamera.fieldOfView.ToString("F1") + "°"));
                container.Add(CreateHoyoToonInfoRow("Render Mode:", sceneCamera.renderingPath.ToString()));
                container.Add(CreateHoyoToonInfoRow("Clear Flags:", sceneCamera.clearFlags.ToString()));

                if (sceneCamera.clearFlags == CameraClearFlags.SolidColor)
                {
                    container.Add(CreateHoyoToonInfoRow("Background Color:",
                        ColorUtility.ToHtmlStringRGB(sceneCamera.backgroundColor)));
                }

                container.Add(CreateHoyoToonSuccessBox("Camera is properly configured"));
            }
            else
            {
                container.Add(CreateHoyoToonErrorBox("No camera found in scene"));
            }

            contentView.Add(container);
        }

        #region Scene Operations

        private bool CanGenerateTangentsForSceneInstance()
        {
            if (sceneInstance == null) return false;

            // Load HoyoToon data to check body type rules
            HoyoToonDataManager.GetHoyoToonData();

            // Use the scene instance to determine body type
            HoyoToonParseManager.DetermineBodyType(sceneInstance);

            // Check if current body type supports tangent generation
            var bodyType = HoyoToonParseManager.currentBodyType;

            // HI3P2 uses color data movement instead of tangent generation
            if (bodyType == HoyoToonParseManager.BodyType.HI3P2)
                return false;

            // Check if all meshes are marked for skipping
            string shaderKey = GetShaderKeyFromBodyType(bodyType);
            var skipMeshes = HoyoToonDataManager.Data.GetSkipMeshesForShader(shaderKey);

            // If wildcard "*" is present, all meshes are skipped
            if (System.Array.IndexOf(skipMeshes, "*") >= 0)
                return false;

            // Check if the scene instance has any processable meshes
            var meshFilters = sceneInstance.GetComponentsInChildren<MeshFilter>();
            var skinnedMeshRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            bool hasProcessableMeshes = false;

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null && !HoyoToonDataManager.Data.ShouldSkipMesh(shaderKey, meshFilter.name))
                {
                    hasProcessableMeshes = true;
                    break;
                }
            }

            if (!hasProcessableMeshes)
            {
                foreach (var renderer in skinnedMeshRenderers)
                {
                    if (renderer.sharedMesh != null && !HoyoToonDataManager.Data.ShouldSkipMesh(shaderKey, renderer.name))
                    {
                        hasProcessableMeshes = true;
                        break;
                    }
                }
            }

            return hasProcessableMeshes;
        }

        private string GetShaderKeyFromBodyType(HoyoToonParseManager.BodyType bodyType)
        {
            switch (bodyType)
            {
                // Honkai Star Rail variants
                case HoyoToonParseManager.BodyType.HSRMaid:
                case HoyoToonParseManager.BodyType.HSRKid:
                case HoyoToonParseManager.BodyType.HSRLad:
                case HoyoToonParseManager.BodyType.HSRMale:
                case HoyoToonParseManager.BodyType.HSRLady:
                case HoyoToonParseManager.BodyType.HSRGirl:
                case HoyoToonParseManager.BodyType.HSRBoy:
                case HoyoToonParseManager.BodyType.HSRMiss:
                    return "HSRShader";

                // Genshin Impact variants
                case HoyoToonParseManager.BodyType.GIBoy:
                case HoyoToonParseManager.BodyType.GIGirl:
                case HoyoToonParseManager.BodyType.GILady:
                case HoyoToonParseManager.BodyType.GIMale:
                case HoyoToonParseManager.BodyType.GILoli:
                    return "GIShader";

                // Honkai Impact variants
                case HoyoToonParseManager.BodyType.HI3P1:
                    return "HI3Shader";
                case HoyoToonParseManager.BodyType.HI3P2:
                    return "HI3P2Shader";

                // Wuthering Waves
                case HoyoToonParseManager.BodyType.WuWa:
                    return "WuWaShader";

                // Zenless Zone Zero
                case HoyoToonParseManager.BodyType.ZZZ:
                    return "ZZZShader";

                default:
                    return "WuWaShader"; // Default fallback
            }
        }

        private bool HasTangentsGeneratedForSceneInstance()
        {
            if (sceneInstance == null) return false;

            // Check if any mesh in the scene instance is using a generated mesh (from Meshes folder)
            var meshFilters = sceneInstance.GetComponentsInChildren<MeshFilter>();
            var skinnedMeshRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            // Check SkinnedMeshRenderers first (most common for character models)
            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer.sharedMesh != null)
                {
                    string assetPath = HoyoToonAssetService.GetAssetPath(renderer.sharedMesh);
                    // Check if the mesh is from a generated "Meshes" folder (indicates HoyoToon tangent generation)
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("/Meshes/") && assetPath.EndsWith(".asset"))
                    {
                        return true;
                    }
                }
            }

            // Check MeshFilters as fallback
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    string assetPath = HoyoToonAssetService.GetAssetPath(meshFilter.sharedMesh);
                    // Check if the mesh is from a generated "Meshes" folder (indicates HoyoToon tangent generation)
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.Contains("/Meshes/") && assetPath.EndsWith(".asset"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string GetTangentGenerationRestrictionReasonForSceneInstance()
        {
            if (sceneInstance == null) return "No model instance in scene";

            HoyoToonDataManager.GetHoyoToonData();

            // Use the scene instance to determine body type
            HoyoToonParseManager.DetermineBodyType(sceneInstance);

            var bodyType = HoyoToonParseManager.currentBodyType;
            string shaderKey = GetShaderKeyFromBodyType(bodyType);

            switch (bodyType)
            {
                case HoyoToonParseManager.BodyType.HI3P2:
                    return "HI3P2 models use color data movement instead of tangent generation";

                case HoyoToonParseManager.BodyType.ZZZ:
                    // Check if ZZZ models have mesh skipping rules
                    var skipMeshes = HoyoToonDataManager.Data.GetSkipMeshesForShader("ZZZShader");
                    if (System.Array.IndexOf(skipMeshes, "*") >= 0)
                        return "ZZZ models do not require tangent generation";
                    break;
            }

            // Check if all meshes are marked for skipping
            var allSkipMeshes = HoyoToonDataManager.Data.GetSkipMeshesForShader(shaderKey);
            if (System.Array.IndexOf(allSkipMeshes, "*") >= 0)
                return $"{bodyType} models do not require tangent generation";

            // Check if specific meshes prevent tangent generation
            var meshFilters = sceneInstance.GetComponentsInChildren<MeshFilter>();
            var skinnedMeshRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

            bool allMeshesSkipped = true;
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null && !HoyoToonDataManager.Data.ShouldSkipMesh(shaderKey, meshFilter.name))
                {
                    allMeshesSkipped = false;
                    break;
                }
            }

            if (allMeshesSkipped)
            {
                foreach (var renderer in skinnedMeshRenderers)
                {
                    if (renderer.sharedMesh != null && !HoyoToonDataManager.Data.ShouldSkipMesh(shaderKey, renderer.name))
                    {
                        allMeshesSkipped = false;
                        break;
                    }
                }
            }

            if (allMeshesSkipped)
                return "All meshes in this model are marked to skip tangent generation";

            return "Tangent generation is not supported for this model type";
        }

        private void GenerateTangentsForSceneInstance()
        {
            if (sceneInstance == null)
            {
                EditorUtility.DisplayDialog("Error", "No model instance in scene", "OK");
                return;
            }

            if (!CanGenerateTangentsForSceneInstance())
            {
                string reason = GetTangentGenerationRestrictionReasonForSceneInstance();
                EditorUtility.DisplayDialog("Cannot Generate Tangents", reason, "OK");
                return;
            }

            // Check if tangents were already generated
            bool hadTangentsBefore = HasTangentsGeneratedForSceneInstance();

            // Use the existing HoyoToonMeshManager tangent generation with scene instance
            try
            {
                HoyoToonMeshManager.GenTangents(sceneInstance);

                // Check if tangents were actually generated
                bool hasTangentsAfter = HasTangentsGeneratedForSceneInstance();

                if (hasTangentsAfter && !hadTangentsBefore)
                {
                    EditorUtility.DisplayDialog("Success",
                        "Tangents generated successfully! The scene model meshes have been processed.", "OK");
                }
                else if (hasTangentsAfter && hadTangentsBefore)
                {
                    EditorUtility.DisplayDialog("Success",
                        "Tangents were regenerated successfully! The scene model meshes have been updated.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Info",
                        "Tangent generation completed. Some meshes may have been skipped based on model rules.", "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to generate tangents: {ex.Message}", "OK");
            }

            // Refresh the display
            RefreshTabContent();
        }

        private void ResetTangentsForSceneInstance()
        {
            if (sceneInstance == null)
            {
                EditorUtility.DisplayDialog("Error", "No model instance in scene", "OK");
                return;
            }

            if (!HasTangentsGeneratedForSceneInstance())
            {
                EditorUtility.DisplayDialog("Info", "No tangents to reset. The model appears to be using original tangents.", "OK");
                return;
            }

            // Check if we can actually reset tangents
            bool hadTangentsBefore = HasTangentsGeneratedForSceneInstance();

            // Use the existing HoyoToonMeshManager tangent reset with scene instance
            try
            {
                HoyoToonMeshManager.ResetTangents(sceneInstance);

                // Check if tangents were actually reset
                bool hasTangentsAfter = HasTangentsGeneratedForSceneInstance();

                if (!hasTangentsAfter && hadTangentsBefore)
                {
                    EditorUtility.DisplayDialog("Success",
                        "Tangents reset successfully! The scene model meshes have been restored to their original state.", "OK");
                }
                else if (!hasTangentsAfter && !hadTangentsBefore)
                {
                    EditorUtility.DisplayDialog("Info",
                        "The model was already using original tangents.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Warning",
                        "Reset operation completed, but some meshes may not have been affected. Check the console for details.", "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to reset tangents: {ex.Message}", "OK");
            }

            // Refresh the display
            RefreshTabContent();
        }

        private void InstantiateModelInScene()
        {
            try
            {
                // Use the centralized HoyoToonSceneManager method instead of custom implementation
                // Ensure the correct model is selected before adding to scene
                var previousSelection = Selection.activeObject;
                Selection.activeObject = selectedModel;
                try
                {
                    GameObject sceneObject = HoyoToonSceneManager.AddSelectedObjectToScene();

                    if (sceneObject != null)
                    {
                        sceneInstance = sceneObject;

                        // Focus scene view on the model
                        if (SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }

                        EditorUtility.DisplayDialog("Success",
                            $"Model {selectedModel.name} added to scene successfully!", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to add model to scene", "OK");
                    }
                }
                finally
                {
                    Selection.activeObject = previousSelection;
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to instantiate model: {e.Message}", "OK");
            }
        }

        private void RemoveModelFromScene()
        {
            try
            {
                if (sceneInstance != null)
                {
                    Object.DestroyImmediate(sceneInstance);
                    sceneInstance = null;
                    EditorUtility.DisplayDialog("Success", "Model removed from scene", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to remove model: {e.Message}", "OK");
            }
        }

        private void SetupOptimalLighting()
        {
            try
            {
                // Create or find directional light
                var directionalLight = Object.FindObjectOfType<Light>();
                if (directionalLight == null || directionalLight.type != LightType.Directional)
                {
                    var lightGO = new GameObject("Directional Light");
                    directionalLight = lightGO.AddComponent<Light>();
                    directionalLight.type = LightType.Directional;
                }

                // Configure directional light for character lighting
                directionalLight.intensity = 1.2f;
                directionalLight.color = Color.white;
                directionalLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                directionalLight.shadows = LightShadows.Soft;

                // Setup ambient lighting
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1f, 1f);
                RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f, 1f);
                RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

                // Disable fog for cleaner preview
                RenderSettings.fog = false;

                EditorUtility.DisplayDialog("Success", "Optimal lighting setup applied", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to setup lighting: {e.Message}", "OK");
            }
        }

        #endregion

        protected override void OnCleanup()
        {
            sceneInstance = null;
            sceneLights = null;
            sceneCamera = null;
        }
    }
}