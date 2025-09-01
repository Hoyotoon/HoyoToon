using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Model Analysis Component
    /// Provides comprehensive analysis of FBX models for HoyoToon setup
    /// </summary>
    public static class HoyoToonModelAnalysisComponent
    {
        #region Events

        /// <summary>
        /// Event triggered when model analysis is complete
        /// </summary>
        public static event System.Action<HoyoToonModelAnalysisData> OnAnalysisComplete;

        #endregion

        #region Public Methods

        /// <summary>
        /// Analyze a GameObject (FBX model) for HoyoToon compatibility
        /// </summary>
        /// <param name="model">The GameObject to analyze</param>
        /// <returns>Complete analysis data</returns>
        public static HoyoToonModelAnalysisData AnalyzeModel(GameObject model)
        {
            var data = new HoyoToonModelAnalysisData();

            if (model == null)
            {
                data.AddIssue("No model selected");
                OnAnalysisComplete?.Invoke(data);
                return data;
            }

            try
            {
                HoyoToonLogs.LogDebug($"Starting analysis of model: {model.name}");

                // Basic model validation
                AnalyzeBasicModelInfo(model, data);

                // Rig and animation analysis
                AnalyzeRigAndAnimations(model, data);

                // Material analysis
                AnalyzeMaterials(model, data);

                // Texture analysis
                AnalyzeTextures(model, data);

                // Shader analysis
                AnalyzeShaders(model, data);

                // HoyoToon specific detection
                AnalyzeHoyoToonSpecific(model, data);

                // Import settings analysis
                AnalyzeImportSettings(model, data);

                // Performance analysis
                AnalyzePerformance(model, data);

                HoyoToonLogs.LogDebug($"Analysis complete. Preparation progress: {data.preparationProgress}%");
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error during model analysis: {e.Message}");
                data.AddIssue($"Analysis failed: {e.Message}");
            }

            OnAnalysisComplete?.Invoke(data);
            return data;
        }

        #endregion

        #region Analysis Methods

        private static void AnalyzeBasicModelInfo(GameObject model, HoyoToonModelAnalysisData data)
        {
            string assetPath = HoyoToonModularBaseTabController.HoyoToonAssetService.GetAssetPath(model);

            if (string.IsNullOrEmpty(assetPath))
            {
                data.AddIssue("Model is not an asset (scene object)");
                return;
            }

            if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                data.AddIssue("Selected object is not an FBX file");
                return;
            }

            data.hasValidModel = true;
            data.modelName = model.name;
            data.modelPath = assetPath;

            // File information
            var fileInfo = new FileInfo(assetPath);
            if (fileInfo.Exists)
            {
                data.fileSize = fileInfo.Length;
                data.creationDate = fileInfo.CreationTime;
            }

            // Check for components
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (renderers.Length == 0)
            {
                data.AddWarning("No renderers found in model");
            }

            if (skinnedRenderers.Length == 0)
            {
                data.AddWarning("No skinned mesh renderers found");
            }

            // Calculate mesh geometry statistics
            CalculateMeshGeometry(model, data);
        }

        private static void AnalyzeRigAndAnimations(GameObject model, HoyoToonModelAnalysisData data)
        {
            var animator = model.GetComponent<Animator>();

            if (animator != null && animator.avatar != null)
            {
                data.isHumanoidRig = animator.avatar.isHuman;
                data.rigType = animator.avatar.isHuman ? "Humanoid" : "Generic";

                if (animator.avatar.isHuman)
                {
                    // Count humanoid bones
                    data.humanoidBoneCount = CountHumanoidBones(animator.avatar);
                }
            }
            else
            {
                data.rigType = "None";
                // Don't add warning for missing animator - not required for HoyoToon
            }

            // Count all bones
            var bones = model.GetComponentsInChildren<Transform>(true);
            data.boneCount = bones.Length;

            // Find root bone
            var skinnedRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.rootBone != null)
            {
                data.rootBoneName = skinnedRenderer.rootBone.name;
            }

            // Check for animations
            var clips = AnimationUtility.GetAnimationClips(model);
            data.animationCount = clips != null ? clips.Length : 0;

            // Don't warn about humanoid rig - not required for HoyoToon functionality
        }

        private static void AnalyzeMaterials(GameObject model, HoyoToonModelAnalysisData data)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials != null)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }
            }

            data.hasMaterials = materials.Count > 0;
            data.materialCount = materials.Count;

            foreach (var material in materials)
            {
                var materialInfo = AnalyzeMaterial(material);
                data.materials.Add(materialInfo);
                data.materialNames.Add(material.name);
            }

            if (!data.hasMaterials)
            {
                data.AddIssue("No materials found on model");
            }
        }

        private static HoyoToonMaterialInfo AnalyzeMaterial(Material material)
        {
            var info = new HoyoToonMaterialInfo();
            info.name = material.name;
            info.materialPath = HoyoToonModularBaseTabController.HoyoToonAssetService.GetAssetPath(material);
            info.currentShader = material.shader != null ? material.shader.name : "None";
            info.isValid = material.shader != null;

            if (!info.isValid)
            {
                info.invalidReason = "Missing shader";
            }

            // Determine material type based on name
            info.materialType = DetermineMaterialType(material.name);

            // Suggest appropriate HoyoToon shader
            info.suggestedShader = SuggestHoyoToonShader(info.materialType, material.name);

            // Check for missing textures
            var shader = material.shader;
            if (shader != null)
            {
                int propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propertyName = ShaderUtil.GetPropertyName(shader, i);
                        var texture = material.GetTexture(propertyName);

                        if (texture == null)
                        {
                            info.missingTextures.Add(propertyName);
                        }
                        else
                        {
                            info.textureCount++;
                            // Estimate memory usage (rough calculation)
                            if (texture is Texture2D tex2D)
                            {
                                info.memoryUsage += EstimateTextureMemory(tex2D);
                            }
                        }
                    }
                }
            }

            return info;
        }

        private static void AnalyzeTextures(GameObject model, HoyoToonModelAnalysisData data)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var textures = new HashSet<Texture>();

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials != null)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null)
                        {
                            var shader = material.shader;
                            int propertyCount = ShaderUtil.GetPropertyCount(shader);

                            for (int i = 0; i < propertyCount; i++)
                            {
                                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                                {
                                    string propertyName = ShaderUtil.GetPropertyName(shader, i);
                                    var texture = material.GetTexture(propertyName);

                                    if (texture != null)
                                    {
                                        textures.Add(texture);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            data.hasTextures = textures.Count > 0;

            foreach (var texture in textures)
            {
                var textureInfo = AnalyzeTexture(texture);
                data.textures.Add(textureInfo);
                data.textureNames.Add(texture.name);
                data.totalTextureMemory += textureInfo.memorySize;
            }

            if (!data.hasTextures)
            {
                data.AddWarning("No textures found on model materials");
            }
        }

        private static HoyoToonTextureInfo AnalyzeTexture(Texture texture)
        {
            var info = new HoyoToonTextureInfo();
            info.name = texture.name;
            info.path = HoyoToonModularBaseTabController.HoyoToonAssetService.GetAssetPath(texture);

            if (texture is Texture2D tex2D)
            {
                info.width = tex2D.width;
                info.height = tex2D.height;
                info.preview = tex2D;
                info.memorySize = EstimateTextureMemory(tex2D);

                // Get import settings
                var importer = AssetImporter.GetAtPath(info.path) as TextureImporter;
                if (importer != null)
                {
                    info.importerType = importer.textureType;
                    info.maxSize = importer.maxTextureSize;
                    info.isReadable = importer.isReadable;
                    info.hasMipmaps = importer.mipmapEnabled;

                    var platformSettings = importer.GetDefaultPlatformTextureSettings();
                    info.format = platformSettings.format;
                    info.isCompressed = platformSettings.format != TextureImporterFormat.RGBA32 &&
                                       platformSettings.format != TextureImporterFormat.RGB24;
                }

                // Determine texture type based on name
                info.textureType = DetermineTextureType(texture.name);
            }

            return info;
        }

        private static void AnalyzeShaders(GameObject model, HoyoToonModelAnalysisData data)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var shaders = new HashSet<string>();
            bool hasHoyoToonShaders = false;

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials != null)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null)
                        {
                            string shaderName = material.shader.name;
                            shaders.Add(shaderName);

                            if (IsHoyoToonShader(shaderName))
                            {
                                hasHoyoToonShaders = true;
                            }
                        }
                    }
                }
            }

            data.hasCorrectShaders = hasHoyoToonShaders;
            data.shaderNames = shaders.ToList();

            if (!hasHoyoToonShaders)
            {
                data.AddWarning("No HoyoToon shaders detected. Materials may need shader updates.");
            }
        }

        private static void AnalyzeHoyoToonSpecific(GameObject model, HoyoToonModelAnalysisData data)
        {
            try
            {
                // Use HoyoToonParseManager for game detection with the specific model
                HoyoToonParseManager.DetermineBodyType(model);
                data.potentialGameType = HoyoToonParseManager.currentBodyType;
                data.bodyType = HoyoToonParseManager.currentBodyType.ToString();

                // Check for HI3 new face system
                data.isHI3NewFace = CheckForHI3NewFace(model);

                // Check for Genshin specific features
                data.isPrenatlaNPrenodkrai = CheckForGenshinFeatures(model);

                // Check if already converted by Hoyo2VRC
                data.isHoyo2VRCConverted = CheckForHoyo2VRCConversion(model);

                if (!data.isHoyo2VRCConverted)
                {
                    data.AddIssue("PREREQUISITE: Model must be processed by Hoyo2VRC first (Bip bone naming not detected)");
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error in HoyoToon specific analysis: {e.Message}");
                data.AddWarning("Could not perform complete HoyoToon-specific analysis");
            }
        }

        private static void AnalyzeImportSettings(GameObject model, HoyoToonModelAnalysisData data)
        {
            string assetPath = AssetDatabase.GetAssetPath(model);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (importer != null)
            {
                data.hasImportSettingsSet = true;
                data.isReadWriteEnabled = importer.isReadable;
                data.meshCompression = importer.meshCompression.ToString();

                // Check if model needs tangent generation - now game-aware
                bool gameRequiresTangents = DoesGameRequireTangents(data.potentialGameType);
                bool meshHasTangents = CheckMeshHasTangents(model);

                data.gameRequiresTangents = gameRequiresTangents;
                data.meshHasTangents = meshHasTangents;
                data.needsTangentGeneration = gameRequiresTangents && !meshHasTangents;

                // Check if reimport is possible
                data.canReimport = !string.IsNullOrEmpty(assetPath);

                // Validate import settings for HoyoToon
                if (!importer.importAnimation)
                {
                    data.AddWarning("Animation import is disabled");
                }

                // Don't warn about avatar setup - humanoid rig is not required for HoyoToon

                if (importer.importTangents == ModelImporterTangents.None && gameRequiresTangents && !meshHasTangents)
                {
                    data.AddWarning("Tangents need to be generated for proper lighting in this game");
                }
            }
            else
            {
                data.AddIssue("Could not access model import settings");
            }
        }

        private static void AnalyzePerformance(GameObject model, HoyoToonModelAnalysisData data)
        {
            // Count total triangles
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            int totalTriangles = 0;

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }

            foreach (var skinnedRenderer in skinnedRenderers)
            {
                if (skinnedRenderer.sharedMesh != null)
                {
                    totalTriangles += skinnedRenderer.sharedMesh.triangles.Length / 3;
                }
            }

            // Performance warnings
            if (totalTriangles > 100000)
            {
                data.AddWarning($"High triangle count: {totalTriangles:N0} triangles");
            }

            if (data.materialCount > 20)
            {
                data.AddWarning($"High material count: {data.materialCount} materials");
            }

            if (data.totalTextureMemory > 100 * 1024 * 1024) // 100MB
            {
                data.AddWarning($"High texture memory usage: {data.textureMemoryFormatted}");
            }
        }

        #endregion

        #region Helper Methods

        private static int CountHumanoidBones(Avatar avatar)
        {
            if (avatar == null || !avatar.isHuman) return 0;

            // Since Avatar.GetBoneTransform doesn't exist, we need to count through the animator
            // This is an approximation based on available humanoid bone mapping
            int count = 0;

            // Count the essential humanoid bones that are typically mapped
            var humanoidBones = System.Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>()
                .Where(bone => bone != HumanBodyBones.LastBone);

            // Since we can't directly access bone transforms from Avatar,
            // we'll return a reasonable estimate for humanoid rigs
            return avatar.isHuman ? 55 : 0; // Standard humanoid rig has ~55 bones
        }

        private static long EstimateTextureMemory(Texture2D texture)
        {
            if (texture == null) return 0;

            // Basic calculation - actual memory usage depends on compression and format
            int pixelCount = texture.width * texture.height;
            int bytesPerPixel = 4; // Assume RGBA32 for estimation

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer != null)
            {
                var platformSettings = importer.GetDefaultPlatformTextureSettings();

                // Adjust bytes per pixel based on format
                switch (platformSettings.format)
                {
                    case TextureImporterFormat.RGB24:
                        bytesPerPixel = 3;
                        break;
                    case TextureImporterFormat.RGBA16:
                        bytesPerPixel = 2;
                        break;
                    case TextureImporterFormat.DXT1:
                        return pixelCount / 2; // DXT1 is 4 bits per pixel
                    case TextureImporterFormat.DXT5:
                        return pixelCount; // DXT5 is 8 bits per pixel
                }
            }

            return pixelCount * bytesPerPixel;
        }

        private static string DetermineMaterialType(string materialName)
        {
            string name = materialName.ToLower();

            if (name.Contains("face") || name.Contains("head"))
                return "Face";
            if (name.Contains("hair"))
                return "Hair";
            if (name.Contains("body") || name.Contains("skin"))
                return "Body";
            if (name.Contains("eye"))
                return "Eye";
            if (name.Contains("cloth") || name.Contains("dress") || name.Contains("shirt"))
                return "Clothing";
            if (name.Contains("weapon") || name.Contains("sword") || name.Contains("gun"))
                return "Weapon";
            if (name.Contains("accessory") || name.Contains("decoration"))
                return "Accessory";

            return "Base";
        }

        private static string DetermineTextureType(string textureName)
        {
            string name = textureName.ToLower();

            if (name.Contains("diffuse") || name.Contains("albedo") || name.Contains("color"))
                return "Diffuse";
            if (name.Contains("normal") || name.Contains("bump"))
                return "Normal";
            if (name.Contains("metallic") || name.Contains("metal"))
                return "Metallic";
            if (name.Contains("roughness") || name.Contains("smooth"))
                return "Roughness";
            if (name.Contains("ao") || name.Contains("occlusion"))
                return "AO";
            if (name.Contains("emission") || name.Contains("emissive"))
                return "Emission";
            if (name.Contains("height") || name.Contains("displacement"))
                return "Height";
            if (name.Contains("mask"))
                return "Mask";

            return "Unknown";
        }

        private static string SuggestHoyoToonShader(string materialType, string materialName)
        {
            // Get shader suggestions from HoyoToonDataManager
            var data = HoyoToonDataManager.Data;
            if (data?.Shaders == null) return "Standard";

            string name = materialName.ToLower();

            // Face materials
            if (materialType == "Face" || name.Contains("face"))
            {
                if (data.Shaders.ContainsKey("GIShader"))
                    return data.Shaders["GIShader"][0];
            }

            // Hair materials
            if (materialType == "Hair" || name.Contains("hair"))
            {
                if (data.Shaders.ContainsKey("GIShader"))
                    return data.Shaders["GIShader"][0];
            }

            // Default to main shader
            if (data.Shaders.ContainsKey("GIShader"))
                return data.Shaders["GIShader"][0];

            return "Standard";
        }

        private static bool IsHoyoToonShader(string shaderName)
        {
            var data = HoyoToonDataManager.Data;
            if (data?.Shaders == null) return false;

            foreach (var shaderGroup in data.Shaders.Values)
            {
                if (shaderGroup.Contains(shaderName))
                    return true;
            }

            return shaderName.Contains("HoyoToon") ||
                   shaderName.Contains("Genshin") ||
                   shaderName.Contains("Honkai") ||
                   shaderName.Contains("miHoYo");
        }

        private static bool CheckForHI3NewFace(GameObject model)
        {
            // Check for HI3 new face system indicators
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer.name.ToLower().Contains("face") &&
                    renderer.sharedMaterials != null)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.name.ToLower().Contains("newface"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool CheckForGenshinFeatures(GameObject model)
        {
            // Check for Genshin-specific naming patterns
            string modelName = model.name.ToLower();
            return modelName.Contains("prenatal") || modelName.Contains("prenodkrai");
        }

        private static bool CheckForHoyo2VRCConversion(GameObject model)
        {
            // First check: If ANY bone contains "bip" = NOT converted (raw datamined models have bip naming)
            var transforms = model.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                string boneName = transform.name.ToLowerInvariant();
                // Raw datamined models have "bip001" style naming - if found, it's NOT converted
                if (boneName.Equals("Bip001") ||
                    boneName.StartsWith("Bip001 Pelvis"))
                {
                    HoyoToonLogs.LogDebug($"Model {model.name} has raw bip naming: {boneName}");
                    return false; // NOT converted - still has raw bip naming
                }
            }

            // Second check: Must have VRC viseme blendshapes (Hoyo2VRC adds these)
            var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool hasVRCVisemes = false;

            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                {
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        string blendShapeName = renderer.sharedMesh.GetBlendShapeName(i);

                        // Check for VRC viseme naming patterns and specific visemes (case sensitive)
                        if (blendShapeName.StartsWith("vrc.") ||
                            blendShapeName == "A" ||
                            blendShapeName == "O" ||
                            blendShapeName == "CH")
                        {
                            hasVRCVisemes = true;
                            break;
                        }
                    }
                    if (hasVRCVisemes) break;
                }
            }

            // Only converted if it has VRC visemes AND no bip naming
            return hasVRCVisemes;
        }

        /// <summary>
        /// Determine if the detected game requires tangents for proper lighting
        /// </summary>
        /// <param name="gameType">Detected game/body type</param>
        /// <returns>True if the game requires tangents</returns>
        private static bool DoesGameRequireTangents(HoyoToonParseManager.BodyType? gameType)
        {
            if (!gameType.HasValue)
                return true; // Default to requiring tangents if game is unknown

            var bodyTypeString = gameType.Value.ToString();

            // Games that require tangents for proper lighting
            if (bodyTypeString.StartsWith("GI"))      // Genshin Impact
                return true;
            if (bodyTypeString.StartsWith("HSR"))     // Honkai Star Rail  
                return true;
            if (bodyTypeString.StartsWith("HI3"))     // Honkai Impact 3rd
                return true;

            // Games that may not require tangents or have different lighting models
            if (bodyTypeString.StartsWith("ZZZ"))     // Zenless Zone Zero
                return false; // ZZZ uses different lighting approach
            if (bodyTypeString.StartsWith("WuWa"))    // Wuthering Waves
                return false; // WuWa may not require tangents

            // Default to requiring tangents for unknown games
            return true;
        }

        /// <summary>
        /// Check if the mesh already has tangent data
        /// </summary>
        /// <param name="model">The model to check</param>
        /// <returns>True if any mesh in the model has tangents</returns>
        private static bool CheckMeshHasTangents(GameObject model)
        {
            // Check all SkinnedMeshRenderers first (most common for character models)
            var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.sharedMesh != null && renderer.sharedMesh.tangents != null && renderer.sharedMesh.tangents.Length > 0)
                {
                    return true;
                }
            }

            // Check MeshFilters as fallback
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null && meshFilter.sharedMesh.tangents != null && meshFilter.sharedMesh.tangents.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CalculateMeshGeometry(GameObject model, HoyoToonModelAnalysisData data)
        {
            var skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshRenderers = model.GetComponentsInChildren<MeshRenderer>(true);

            data.vertexCount = 0;
            data.triangleCount = 0;
            data.blendshapeCount = 0;

            // Calculate from SkinnedMeshRenderers
            foreach (var skinnedRenderer in skinnedRenderers)
            {
                if (skinnedRenderer.sharedMesh != null)
                {
                    var mesh = skinnedRenderer.sharedMesh;
                    data.vertexCount += mesh.vertexCount;
                    data.triangleCount += mesh.triangles.Length / 3;
                    data.blendshapeCount += mesh.blendShapeCount;
                }
            }

            // Calculate from MeshRenderers (in case there are static meshes)
            foreach (var meshRenderer in meshRenderers)
            {
                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var mesh = meshFilter.sharedMesh;
                    data.vertexCount += mesh.vertexCount;
                    data.triangleCount += mesh.triangles.Length / 3;
                    data.blendshapeCount += mesh.blendShapeCount;
                }
            }

            HoyoToonLogs.LogDebug($"Mesh geometry calculated - Vertices: {data.vertexCount}, Triangles: {data.triangleCount}, BlendShapes: {data.blendshapeCount}");
        }

        #endregion
    }
}