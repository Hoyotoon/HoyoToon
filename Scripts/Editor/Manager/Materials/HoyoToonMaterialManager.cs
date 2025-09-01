#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HoyoToon
{
    public class HoyoToonMaterialManager : Editor
    {
        #region Constants
        public static readonly string HSRShader = HoyoToonDataManager.HSRShader;
        public static readonly string GIShader = HoyoToonDataManager.GIShader;
        public static readonly string HI3Shader = HoyoToonDataManager.HI3Shader;
        public static readonly string HI3P2Shader = HoyoToonDataManager.HI3P2Shader;
        public static readonly string WuWaShader = HoyoToonDataManager.WuWaShader;
        public static readonly string ZZZShader = HoyoToonDataManager.ZZZShader;

        #endregion

        #region Texture Cache for Batch Processing
        
        /// <summary>
        /// Cache for textures found during batch processing to avoid repeated asset database searches
        /// </summary>
        private static Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();
        
        /// <summary>
        /// Clear the texture cache (called at start of batch processing)
        /// </summary>
        private static void ClearTextureCache()
        {
            textureCache.Clear();
        }
        
        #endregion

        #region Performance Optimized Material Generation

        /// <summary>
        /// Generate materials from selected JSON files in the project.
        /// </summary>
        [MenuItem("Assets/HoyoToon/Materials/Generate Materials", priority = 20)]
        public static void GenerateMaterialsFromJson()
        {
            HoyoToonParseManager.DetermineBodyType();
            HoyoToonDataManager.GetHoyoToonData();
            
            // Clear texture cache for this batch
            ClearTextureCache();
            
            List<string> loadedTexturePaths = new List<string>();
            List<Material> materialsToSetDirty = new List<Material>();
            
            UnityEngine.Object[] selectedObjects = Selection.objects;
            List<string> jsonFilesToProcess = new List<string>();

            // Collect all JSON files to process
            foreach (var selectedObject in selectedObjects)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedObject);

                if (Path.GetExtension(selectedPath) == ".json")
                {
                    jsonFilesToProcess.Add(selectedPath);
                }
                else
                {
                    string directoryName = Path.GetDirectoryName(selectedPath);
                    string materialsFolderPath = new[] { "Materials", "Material", "Mat" }
                        .Select(folder => Path.Combine(directoryName, folder))
                        .FirstOrDefault(path => Directory.Exists(path) && Directory.GetFileSystemEntries(path).Any());

                    if (materialsFolderPath != null)
                    {
                        string[] jsonFiles = Directory.GetFiles(materialsFolderPath, "*.json");
                        jsonFilesToProcess.AddRange(jsonFiles);
                    }
                    else
                    {
                        string validFolderNames = string.Join(", ", new[] { "Materials", "Material", "Mat" });
                        EditorUtility.DisplayDialog("Error", $"Materials folder path does not exist. Ensure your materials are in a folder named {validFolderNames}.", "OK");
                        HoyoToonLogs.ErrorDebug("Materials folder path does not exist. Ensure your materials are in a folder named 'Materials'.");
                    }
                }
            }

            if (jsonFilesToProcess.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No JSON files found to process.", "OK");
                return;
            }

            // Batch process all JSON files with progress reporting
            AssetDatabase.StartAssetEditing();
            try
            {
                ProcessJsonFilesBatch(jsonFilesToProcess, loadedTexturePaths, materialsToSetDirty);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Set all materials dirty in batch
            foreach (var material in materialsToSetDirty)
            {
                EditorUtility.SetDirty(material);
            }

            // Note: Texture import settings are now applied individually with cache checking
            // to avoid redundant Global settings application over shader-specific settings
            HoyoToonLogs.LogDebug($"Processed {loadedTexturePaths.Count} texture files with individual import settings");

            // Single save and refresh operation at the end
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.ClearProgressBar();
            HoyoToonLogs.LogDebug($"Successfully processed {jsonFilesToProcess.Count} materials.");
        }

        private static void ProcessJsonFilesBatch(List<string> jsonFiles, List<string> loadedTexturePaths, List<Material> materialsToSetDirty)
        {
            // Collect materials being processed for cross-references
            Dictionary<string, Material> materialsInBatch = new Dictionary<string, Material>();
            List<(Material material, string jsonFileName, string jsonFile, JObject jsonObject)> scriptedSettingsToApply = new List<(Material, string, string, JObject)>();

            for (int i = 0; i < jsonFiles.Count; i++)
            {
                string jsonFile = jsonFiles[i];
                float progress = (float)i / jsonFiles.Count;
                
                EditorUtility.DisplayProgressBar("Generating Materials", $"Processing {Path.GetFileNameWithoutExtension(jsonFile)} ({i + 1}/{jsonFiles.Count})", progress);
                
                try
                {
                    var result = ProcessSingleJsonFile(jsonFile, loadedTexturePaths, materialsToSetDirty);
                    if (result.material != null)
                    {
                        // Store material for cross-referencing
                        materialsInBatch[result.materialName] = result.material;
                        
                        // Store scripted settings for later application
                        scriptedSettingsToApply.Add((result.material, result.jsonFileName, jsonFile, result.jsonObject));
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to process {jsonFile}: {ex.Message}");
                    continue;
                }
            }

            // Apply scripted settings after all materials are created
            EditorUtility.DisplayProgressBar("Generating Materials", "Applying shader-specific settings...", 1.0f);
            
            foreach (var settingsData in scriptedSettingsToApply)
            {
                try
                {
                    ApplyScriptedSettingsToMaterial(settingsData.material, settingsData.jsonFileName, 
                        settingsData.jsonFile, settingsData.jsonObject, materialsInBatch);
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to apply scripted settings for {settingsData.material.name}: {ex.Message}");
                }
            }
        }

        private static (Material material, string materialName, string jsonFileName, JObject jsonObject) ProcessSingleJsonFile(string jsonFile, List<string> loadedTexturePaths, List<Material> materialsToSetDirty)
        {
            TextAsset jsonTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonFile);
            string jsonContent = jsonTextAsset.text;
            
            MaterialJsonStructure materialData = JsonConvert.DeserializeObject<MaterialJsonStructure>(jsonContent);
            string jsonFileName = Path.GetFileNameWithoutExtension(jsonFile);

            Shader shaderToApply = DetermineShader(materialData);

            if (shaderToApply != null)
            {
                HoyoToonLogs.LogDebug($"Final shader to apply: {shaderToApply.name}");
                string materialPath = Path.GetDirectoryName(jsonFile) + "/" + jsonFileName + ".mat";
                Material materialToUpdate = GetOrCreateMaterial(materialPath, shaderToApply, jsonFileName);

                if (materialData.IsUnityFormat)
                {
                    ProcessUnityMaterialProperties(materialData, materialToUpdate, loadedTexturePaths, shaderToApply);
                }
                else if (materialData.IsUnrealFormat)
                {
                    ProcessUnrealMaterialProperties(materialData, materialToUpdate, loadedTexturePaths);
                }

                ApplyCustomSettingsToMaterial(materialToUpdate, jsonFileName);
                materialsToSetDirty.Add(materialToUpdate);
                
                return (materialToUpdate, jsonFileName, jsonFileName, JObject.Parse(jsonContent));
            }
            else
            {
                HoyoToonLogs.ErrorDebug("No compatible shader found for " + jsonFileName);
                return (null, null, null, null);
            }
        }

        #endregion

        #region Material Processing Methods

        private static Material GetOrCreateMaterial(string materialPath, Shader shader, string materialName)
        {
            Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMaterial != null)
            {
                existingMaterial.shader = shader;
                return existingMaterial;
            }

            Material newMaterial = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(newMaterial, materialPath);
            return newMaterial;
        }

        private static void ProcessUnityMaterialProperties(MaterialJsonStructure materialData, Material material, 
            List<string> loadedTexturePaths, Shader shader)
        {
            var properties = materialData.m_SavedProperties;

            // Process floats
            if (properties.m_Floats != null)
            {
                foreach (var kvp in properties.m_Floats)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetFloat(kvp.Key, kvp.Value);
                    }
                }
            }

            // Process ints
            if (properties.m_Ints != null)
            {
                foreach (var kvp in properties.m_Ints)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetInt(kvp.Key, kvp.Value);
                    }
                }
            }

            // Process colors
            if (properties.m_Colors != null)
            {
                foreach (var kvp in properties.m_Colors)
                {
                    if (material.HasProperty(kvp.Key))
                    {
                        material.SetColor(kvp.Key, kvp.Value.ToColor());
                    }
                }
            }

            // Process textures
            if (properties.m_TexEnvs != null)
            {
                HoyoToonLogs.LogDebug($"Processing {properties.m_TexEnvs.Count} texture properties for material '{material.name}'");
                HoyoToonLogs.LogDebug($"Available texture properties: [{string.Join(", ", properties.m_TexEnvs.Keys)}]");
                
                foreach (var kvp in properties.m_TexEnvs)
                {
                    HoyoToonLogs.LogDebug($"Processing texture property '{kvp.Key}' - HasProperty: {material.HasProperty(kvp.Key)}, TextureName: '{kvp.Value?.m_Texture?.Name ?? "NULL"}'");
                    
                    if (material.HasProperty(kvp.Key))
                    {
                        ProcessTextureProperty(material, kvp.Key, kvp.Value, loadedTexturePaths, shader);
                    }
                    else
                    {
                        HoyoToonLogs.LogDebug($"Material '{material.name}' does not have texture property '{kvp.Key}', skipping");
                    }
                }
            }
            else
            {
                HoyoToonLogs.WarningDebug($"No m_TexEnvs found in material JSON for '{material.name}'");
            }

            // Apply shader-specific texture assignments from HoyoToonDataManager
            ApplyShaderSpecificTextureAssignments(material, shader, loadedTexturePaths);
        }

        /// <summary>
        /// Apply shader-specific texture assignments from HoyoToonDataManager configuration
        /// This applies to ALL materials using the shader, regardless of what's in the material JSON
        /// </summary>
        private static void ApplyShaderSpecificTextureAssignments(Material material, Shader shader, List<string> loadedTexturePaths)
        {
            if (shader == null) return;

            string shaderKey = HoyoToonDataManager.GetShaderKey(material);
            if (string.IsNullOrEmpty(shaderKey) || shaderKey == "Global") return;

            var assignmentData = HoyoToonDataManager.Data.GetTextureAssignmentForShader(shaderKey);
            if (assignmentData == null)
            {
                HoyoToonLogs.LogDebug($"No shader-specific texture assignments found for shader key: {shaderKey}");
                return;
            }

            HoyoToonLogs.LogDebug($"Applying shader-specific texture assignments for '{shaderKey}' to material '{material.name}'");
            HoyoToonLogs.LogDebug($"Available shader assignments: [{string.Join(", ", assignmentData.Properties?.Keys.ToArray() ?? new string[0])}]");

            string currentBodyTypeString = HoyoToonParseManager.currentBodyType.ToString();
            int bodyTypeIndex = assignmentData.GetBodyTypeIndex(currentBodyTypeString);
            
            // For shaders without body types (like WuWa), body type index will be -1, which is fine
            bool hasBodyTypes = assignmentData.BodyTypes != null && assignmentData.BodyTypes.Length > 0;
            
            if (hasBodyTypes && bodyTypeIndex == -1)
            {
                HoyoToonLogs.WarningDebug($"Body type '{currentBodyTypeString}' not found in shader '{shaderKey}' configuration. Available body types: [{string.Join(", ", assignmentData.BodyTypes ?? new string[0])}]");
                return;
            }

            if (hasBodyTypes)
            {
                HoyoToonLogs.LogDebug($"Using body type '{currentBodyTypeString}' (index {bodyTypeIndex}) for shader assignments");
            }
            else
            {
                HoyoToonLogs.LogDebug($"Shader '{shaderKey}' has no body types defined, using direct property assignments");
            }

            // Apply each shader-specific texture assignment
            if (assignmentData.Properties != null)
            {
                foreach (var propertyAssignment in assignmentData.Properties)
                {
                    string propertyName = propertyAssignment.Key;
                    
                    // Check if the material's shader has this property
                    if (!material.HasProperty(propertyName))
                    {
                        HoyoToonLogs.LogDebug($"Shader does not have property '{propertyName}', skipping assignment");
                        continue;
                    }

                    string textureName = assignmentData.GetTextureForProperty(propertyName, bodyTypeIndex);
                    if (string.IsNullOrEmpty(textureName))
                    {
                        HoyoToonLogs.LogDebug($"No texture name found for property '{propertyName}', skipping assignment");
                        continue;
                    }

                    HoyoToonLogs.LogDebug($"Assigning shader-specific texture '{textureName}' to property '{propertyName}'");

                    // Load and assign the texture
                    Texture texture = FindOrLoadTexture(textureName);
                    
                    if (texture == null)
                    {
                        // Additional debugging for failed texture searches
                        LogAvailableTextures(textureName);
                    }
                    
                    if (texture != null)
                    {
                        material.SetTexture(propertyName, texture);
                        string texturePath = AssetDatabase.GetAssetPath(texture);
                        if (!string.IsNullOrEmpty(texturePath))
                        {
                            loadedTexturePaths.Add(texturePath);
                            
                            // Apply shader-specific texture import settings immediately
                            HoyoToonTextureManager.SetTextureImportSettings(new[] { texturePath }, shaderKey);
                        }
                        HoyoToonLogs.LogDebug($"Successfully assigned shader-specific texture '{texture.name}' to property '{propertyName}' for material '{material.name}'");
                    }
                    else
                    {
                        HoyoToonLogs.ErrorDebug($"Failed to load shader-specific texture '{textureName}' for property '{propertyName}' on material '{material.name}'");
                    }
                }
            }
        }

        private static void ProcessUnrealMaterialProperties(MaterialJsonStructure materialData, Material material, List<string> loadedTexturePaths)
        {
            var parameters = materialData.Parameters;
            string shaderKey = HoyoToonDataManager.GetShaderKey(material);

            // Process colors
            if (parameters.Colors != null)
            {
                foreach (var kvp in parameters.Colors)
                {
                    string unityPropertyName = HoyoToonDataManager.Data.ResolvePropertyName(shaderKey, kvp.Key);
                    if (material.HasProperty(unityPropertyName))
                    {
                        material.SetColor(unityPropertyName, kvp.Value.ToColor());
                        HoyoToonLogs.LogDebug($"Set color property: {kvp.Key} -> {unityPropertyName}");
                    }
                    else
                    {
                        HoyoToonLogs.LogDebug($"Material does not have color property: {unityPropertyName} (from {kvp.Key}), skipping");
                    }
                }
            }

            // Process scalars
            if (parameters.Scalars != null)
            {
                foreach (var kvp in parameters.Scalars)
                {
                    string unityPropertyName = HoyoToonDataManager.Data.ResolvePropertyName(shaderKey, kvp.Key);
                    if (material.HasProperty(unityPropertyName))
                    {
                        material.SetFloat(unityPropertyName, kvp.Value);
                        HoyoToonLogs.LogDebug($"Set float property: {kvp.Key} -> {unityPropertyName} = {kvp.Value}");
                    }
                    else
                    {
                        HoyoToonLogs.LogDebug($"Material does not have float property: {unityPropertyName} (from {kvp.Key}), skipping");
                    }
                }
            }

            // Process switches
            if (parameters.Switches != null)
            {
                foreach (var kvp in parameters.Switches)
                {
                    string unityPropertyName = HoyoToonDataManager.Data.ResolvePropertyName(shaderKey, kvp.Key);
                    if (material.HasProperty(unityPropertyName))
                    {
                        material.SetInt(unityPropertyName, kvp.Value ? 1 : 0);
                        HoyoToonLogs.LogDebug($"Set switch property: {kvp.Key} -> {unityPropertyName} = {(kvp.Value ? 1 : 0)}");
                    }
                    else
                    {
                        HoyoToonLogs.LogDebug($"Material does not have switch property: {unityPropertyName} (from {kvp.Key}), skipping");
                    }
                }
            }

            // Process render queue
            if (parameters.RenderQueue != 0)
            {
                material.renderQueue = parameters.RenderQueue;
            }

            // Process textures
            if (materialData.Textures != null)
            {
                foreach (var kvp in materialData.Textures)
                {
                    string unityPropertyName = HoyoToonDataManager.Data.ResolvePropertyName(shaderKey, kvp.Key);
                    if (material.HasProperty(unityPropertyName))
                    {
                        string texturePath = kvp.Value;
                        string textureName = texturePath.Substring(texturePath.LastIndexOf('.') + 1);
                        Texture texture = FindOrLoadTexture(textureName);
                        
                        if (texture != null)
                        {
                            material.SetTexture(unityPropertyName, texture);
                            string assetPath = AssetDatabase.GetAssetPath(texture);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                loadedTexturePaths.Add(assetPath);
                                
                                // Apply shader-specific texture import settings immediately
                                HoyoToonTextureManager.SetTextureImportSettings(new[] { assetPath }, shaderKey);
                            }

                            material.SetTextureScale(unityPropertyName, Vector2.one);
                            material.SetTextureOffset(unityPropertyName, Vector2.zero);
                            HoyoToonLogs.LogDebug($"Set texture property: {kvp.Key} -> {unityPropertyName} = {textureName}");
                        }
                        else
                        {
                            HoyoToonLogs.WarningDebug($"Could not find texture: {textureName} for property {unityPropertyName} (from {kvp.Key})");
                        }
                    }
                    else
                    {
                        HoyoToonLogs.LogDebug($"Material does not have texture property: {unityPropertyName} (from {kvp.Key}), skipping");
                    }
                }
            }

            // Apply shader-specific texture assignments from HoyoToonDataManager
            ApplyShaderSpecificTextureAssignments(material, material.shader, loadedTexturePaths);
        }

        private static void ProcessTextureProperty(Material material, string propertyName, MaterialJsonStructure.TexturePropertyInfo textureInfo,
            List<string> loadedTexturePaths, Shader shader)
        {
            string textureName = textureInfo.m_Texture.Name;

            if (string.IsNullOrEmpty(textureName))
            {
                HoyoToonTextureManager.HardsetTexture(material, propertyName, shader);
                return;
            }

            Texture texture = FindOrLoadTexture(textureName);
            if (texture != null)
            {
                material.SetTexture(propertyName, texture);
                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (!string.IsNullOrEmpty(texturePath))
                {
                    loadedTexturePaths.Add(texturePath);
                    
                    // Apply shader-specific texture import settings immediately
                    HoyoToonTextureManager.SetTextureImportSettings(new[] { texturePath }, HoyoToonDataManager.GetShaderKey(material));
                }

                material.SetTextureScale(propertyName, textureInfo.m_Scale.ToVector2());
                material.SetTextureOffset(propertyName, textureInfo.m_Offset.ToVector2());
            }
        }

        /// <summary>
        /// Find and load texture by name with caching support
        /// </summary>
        private static Texture FindOrLoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                HoyoToonLogs.LogDebug("Texture name is null or empty");
                return null;
            }

            // Check cache first
            if (textureCache.TryGetValue(textureName, out Texture cachedTexture))
            {
                HoyoToonLogs.LogDebug($"Found texture '{textureName}' in cache");
                return cachedTexture;
            }

            // Try different search strategies
            Texture texture = null;
            
            // Strategy 1: Search by exact name with type filter
            string[] textureGUIDs = AssetDatabase.FindAssets($"{textureName} t:texture");
            if (textureGUIDs.Length > 0)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(textureGUIDs[0]);
                texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                if (texture != null)
                {
                    HoyoToonLogs.LogDebug($"Found texture '{textureName}' using exact name search");
                    textureCache[textureName] = texture; // Cache the found texture
                    return texture;
                }
            }

            // Strategy 2: Search by name only (no type filter)
            textureGUIDs = AssetDatabase.FindAssets(textureName);
            foreach (string guid in textureGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                
                // Check if this is a texture file and name matches
                if (fileName.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (texture != null)
                    {
                        HoyoToonLogs.LogDebug($"Found texture '{textureName}' using filename search at: {path}");
                        textureCache[textureName] = texture; // Cache the found texture
                        return texture;
                    }
                }
            }

            // Strategy 3: Broader search for partial matches
            textureGUIDs = AssetDatabase.FindAssets("t:texture");
            foreach (string guid in textureGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                
                if (fileName.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (texture != null)
                    {
                        HoyoToonLogs.LogDebug($"Found texture '{textureName}' using broad search at: {path}");
                        textureCache[textureName] = texture; // Cache the found texture
                        return texture;
                    }
                }
            }

            HoyoToonLogs.LogDebug($"Texture not found after all search strategies: '{textureName}'");
            return null;
        }

        /// <summary>
        /// Debug method to log available textures for troubleshooting
        /// </summary>
        private static void LogAvailableTextures(string searchTextureName)
        {
            HoyoToonLogs.LogDebug($"=== DEBUG: Available textures for search term '{searchTextureName}' ===");
            
            // Log all textures that contain the search term
            string[] allTextureGUIDs = AssetDatabase.FindAssets("t:texture");
            var matchingTextures = new List<string>();
            
            foreach (string guid in allTextureGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                
                if (fileName.ToLower().Contains(searchTextureName.ToLower()))
                {
                    matchingTextures.Add($"{fileName} at {path}");
                }
            }
            
            if (matchingTextures.Count > 0)
            {
                HoyoToonLogs.LogDebug($"Found {matchingTextures.Count} matching textures:");
                foreach (string match in matchingTextures.Take(10)) // Limit to first 10 matches
                {
                    HoyoToonLogs.LogDebug($"  - {match}");
                }
                if (matchingTextures.Count > 10)
                {
                    HoyoToonLogs.LogDebug($"  ... and {matchingTextures.Count - 10} more matches");
                }
            }
            else
            {
                HoyoToonLogs.LogDebug($"No textures found containing '{searchTextureName}'");
            }
            
            HoyoToonLogs.LogDebug("=== END DEBUG ===");
        }

        #endregion

        #region Public Helper Methods for UI

        /// <summary>
        /// Public wrapper for shader detection from material data (for UI usage)
        /// </summary>
        /// <param name="materialData">Material JSON structure</param>
        /// <returns>Detected shader path or null if not found</returns>
        public static string DetectShaderFromMaterialData(MaterialJsonStructure materialData)
        {
            Shader shader = DetermineShader(materialData);
            return shader?.name;
        }

        /// <summary>
        /// Validate JSON structure and provide error message
        /// </summary>
        /// <param name="jsonPath">Path to JSON file</param>
        /// <param name="errorMessage">Output error message if invalid</param>
        /// <returns>True if valid, false if invalid</returns>
        public static bool ValidateJsonStructure(string jsonPath, out string errorMessage)
        {
            errorMessage = null;
            
            try
            {
                var jsonText = File.ReadAllText(jsonPath);
                var materialData = JsonConvert.DeserializeObject<MaterialJsonStructure>(jsonText);

                // Check for outdated JSON structure
                bool isOutdated = IsOutdatedJsonStructure(materialData, jsonText);
                if (isOutdated)
                {
                    errorMessage = "This JSON file uses an outdated structure. Please download the latest assets from assets.hoyotoon.com for up-to-date JSON files.";
                    return false;
                }

                // Check if we can detect a valid shader
                Shader detectedShader = DetermineShader(materialData);
                if (detectedShader == null)
                {
                    errorMessage = "Could not detect a compatible shader from this JSON file.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to parse JSON: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Check if JSON file is a valid material JSON
        /// </summary>
        /// <param name="jsonPath">Path to JSON file</param>
        /// <returns>True if valid material JSON</returns>
        public static bool IsValidMaterialJson(string jsonPath)
        {
            try
            {
                var jsonText = File.ReadAllText(jsonPath);

                // Enhanced check for material JSON files
                // Check for Unity material properties
                if (jsonText.Contains("m_Shader") || jsonText.Contains("m_SavedProperties"))
                    return true;

                // Check for Hoyo2VRC material format
                if (jsonText.Contains("Parameters") && jsonText.Contains("Textures"))
                    return true;

                // Check for material-related keywords
                var materialKeywords = new[] { "material", "_MainTex", "_Color", "_BaseMap", "_Diffuse", "shader" };
                var keywordCount = materialKeywords.Count(keyword => jsonText.ToLower().Contains(keyword.ToLower()));

                // If it contains multiple material-related keywords, likely a material JSON
                return keywordCount >= 2;
            }
            catch (Exception e)
            {
                HoyoToonLogs.WarningDebug($"Could not read JSON file {jsonPath}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if JSON file uses outdated structure (extracted from UI for reuse)
        /// </summary>
        private static bool IsOutdatedJsonStructure(MaterialJsonStructure materialData, string jsonText)
        {
            // Modern JSON structure MUST have Name fields - this is how we distinguish 
            // current supported format from old outdated formats that should be rejected
            
            try
            {
                // Critical check 1: m_Shader must have a Name field (indicates modern format)
                bool hasShaderName = materialData.m_Shader?.Name != null && !string.IsNullOrEmpty(materialData.m_Shader.Name);
                if (!hasShaderName)
                {
                    HoyoToonLogs.WarningDebug("JSON has outdated format - m_Shader missing Name field. This format is no longer supported.");
                    return true;
                }

                // Critical check 2: Root level must have a Name field (indicates modern format)
                bool hasRootName = false;
                try
                {
                    var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonText);
                    hasRootName = jsonObject.ContainsKey("Name") && jsonObject["Name"] != null;
                }
                catch
                {
                    // If we can't parse as dictionary, fall back to string search
                    hasRootName = jsonText.Contains("\"Name\":");
                }
                
                if (!hasRootName)
                {
                    HoyoToonLogs.WarningDebug("JSON has outdated format - missing root Name field. This format is no longer supported.");
                    return true;
                }

                // Critical check 3: Texture entries should have Name fields (modern format indicator)
                // Only reject if texture is NOT null but missing Name field - empty names are valid for unused slots
                if (materialData.m_SavedProperties?.m_TexEnvs != null)
                {
                    foreach (var texEnv in materialData.m_SavedProperties.m_TexEnvs)
                    {
                        if (texEnv.Value?.m_Texture != null && 
                            !texEnv.Value.m_Texture.IsNull && 
                            texEnv.Value.m_Texture.Name == null)
                        {
                            HoyoToonLogs.WarningDebug($"JSON has outdated format - non-null texture reference '{texEnv.Key}' missing Name field. This format is no longer supported.");
                            return true;
                        }
                    }
                }

                // If we get here, all required Name fields are present - this is the modern supported format
                HoyoToonLogs.LogDebug("JSON format validation passed - all required Name fields present");
                return false;
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Error validating JSON format: {ex.Message}");
                return true; // Consider it outdated if we can't validate it
            }
        }

        #endregion

        #region Shader Determination and Utility Methods

        /// <summary>
        /// Find a Face material in the current batch of materials being processed
        /// </summary>
        private static Material FindFaceMaterialInBatch(Dictionary<string, Material> materialsInBatch)
        {
            foreach (var kvp in materialsInBatch)
            {
                if (kvp.Key.Contains("Face"))
                {
                    return kvp.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Find a Face material in the same directory using asset database search (fallback)
        /// </summary>
        private static Material FindFaceMaterialInDirectory(string jsonFile)
        {
            try
            {
                string[] faceMaterialGUIDs = AssetDatabase.FindAssets("Face t:material", new[] { Path.GetDirectoryName(jsonFile) });
                if (faceMaterialGUIDs.Length > 0)
                {
                    string faceMaterialPath = AssetDatabase.GUIDToAssetPath(faceMaterialGUIDs[0]);
                    Material faceMaterial = AssetDatabase.LoadAssetAtPath<Material>(faceMaterialPath);
                    return faceMaterial;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.WarningDebug($"Failed to find Face material in directory: {ex.Message}");
            }
            return null;
        }

        public static void ApplyCustomSettingsToMaterial(Material material, string jsonFileName)
        {
            var shaderName = material.shader.name;
            HoyoToonLogs.LogDebug($"Shader name: {shaderName}");

            if (HoyoToonDataManager.Data.MaterialSettings.TryGetValue(shaderName, out var shaderSettings))
            {
                HoyoToonLogs.LogDebug($"Found settings for shader: {shaderName}");

                var matchedSettings = shaderSettings.FirstOrDefault(setting => jsonFileName.Contains(setting.Key)).Value
                                      ?? shaderSettings.GetValueOrDefault("Default");

                if (matchedSettings != null)
                {
                    HoyoToonLogs.LogDebug($"Matched settings found for JSON file: {jsonFileName}");

                    foreach (var property in matchedSettings)
                    {
                        try
                        {
                            var propertyValue = property.Value.ToString();

                            // Check if the property value references another property
                            if (material.HasProperty(propertyValue))
                            {
                                var referencedValue = material.GetFloat(propertyValue);
                                material.SetFloat(property.Key, referencedValue);
                                HoyoToonLogs.LogDebug($"Successfully set property: {property.Key} to {referencedValue} (referenced from {propertyValue})");
                            }
                            else
                            {
                                // Attempt to parse the property value as int or float
                                if (int.TryParse(propertyValue, out var intValue))
                                {
                                    material.SetInt(property.Key, intValue);
                                    HoyoToonLogs.LogDebug($"Successfully set int property: {property.Key} to {intValue}");
                                }
                                else if (float.TryParse(propertyValue, out var floatValue))
                                {
                                    material.SetFloat(property.Key, floatValue);
                                    HoyoToonLogs.LogDebug($"Successfully set float property: {property.Key} to {floatValue}");
                                }
                                else
                                {
                                    HoyoToonLogs.WarningDebug($"Failed to parse property: {property.Key} as int or float");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            HoyoToonLogs.ErrorDebug($"Failed to set property: {property.Key} with value: {property.Value}. Error: {ex.Message}");
                        }
                    }

                    if (matchedSettings.TryGetValue("renderQueue", out var renderQueue))
                    {
                        try
                        {
                            material.renderQueue = Convert.ToInt32(renderQueue);
                            HoyoToonLogs.LogDebug($"Successfully set renderQueue to {renderQueue}");
                        }
                        catch (Exception ex)
                        {
                            HoyoToonLogs.ErrorDebug($"Failed to set renderQueue to {renderQueue}. Error: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                HoyoToonLogs.ErrorDebug($"No settings found for shader: {shaderName}");
            }
        }

        private static void ApplyScriptedSettingsToMaterial(Material material, string jsonFileName, string jsonFile, JObject jsonObject, Dictionary<string, Material> materialsInBatch)
        {
            if (material.shader.name == HSRShader)
            {
                if (jsonFileName.Contains("Bang"))
                {
                    // Try to find Face material in current batch first
                    Material faceMaterial = FindFaceMaterialInBatch(materialsInBatch);
                    
                    // If not found in batch, fall back to asset database search
                    if (faceMaterial == null)
                    {
                        faceMaterial = FindFaceMaterialInDirectory(jsonFile);
                    }

                    if (faceMaterial != null && faceMaterial.HasProperty("_ShadowColor"))
                    {
                        Color shadowColor = faceMaterial.GetColor("_ShadowColor");
                        material.SetColor("_ShadowColor", shadowColor);
                        HoyoToonLogs.LogDebug($"Applied face shadow color to Bang material: {material.name}");
                    }
                }
            }
            else if (material.shader.name == GIShader)
            {
                if(jsonFileName.Contains("Bang"))
                {
                    // Try to find Face material in current batch first
                    Material faceMaterial = FindFaceMaterialInBatch(materialsInBatch);
                    
                    // If not found in batch, fall back to asset database search
                    if (faceMaterial == null)
                    {
                        faceMaterial = FindFaceMaterialInDirectory(jsonFile);
                    }

                    if (faceMaterial != null)
                    {
                        if (faceMaterial.HasProperty("_FirstShadowMultColor"))
                        {
                            Color shadowColor = faceMaterial.GetColor("_FirstShadowMultColor");
                            material.SetColor("_HairShadowColor", shadowColor);
                        }
                        if (faceMaterial.HasProperty("_CoolShadowMultColor"))
                        {
                            Color shadowColor = faceMaterial.GetColor("_CoolShadowMultColor");
                            material.SetColor("_CoolHairShadowColor", shadowColor);
                        }
                        HoyoToonLogs.LogDebug($"Applied face shadow colors to Bang material: {material.name}");
                    }
                }
                if (ContainsKey(jsonObject["m_SavedProperties"]?["m_Floats"], "_DummyFixedForNormal"))
                {
                    material.SetInt("_gameVersion", 1);
                }
                else
                {
                    material.SetInt("_gameVersion", 0);
                }
            }
            else if (material.shader.name == WuWaShader)
            {
                // For WuWa shader, we need access to all materials in the directory
                // Combine materials in batch with existing materials in directory
                Dictionary<string, Material> allMaterials = GetAllMaterialsInDirectory(jsonFile, materialsInBatch);
                
                foreach (var kvp in allMaterials)
                {
                    string materialName = kvp.Key;
                    Material originalMaterial = kvp.Value;

                    if (materialName.EndsWith("_OL"))
                    {
                        string baseMaterialName = materialName.Substring(0, materialName.Length - 3);
                        if (allMaterials.TryGetValue(baseMaterialName, out Material baseMaterial))
                        {
                            if (originalMaterial.HasProperty("_MainTex"))
                            {
                                Texture mainTex = originalMaterial.GetTexture("_MainTex");
                                baseMaterial.SetTexture("_OutlineTexture", mainTex);
                            }

                            if (originalMaterial.HasProperty("_OutlineWidth"))
                            {
                                float outlineWidth = originalMaterial.GetFloat("_OutlineWidth");
                                baseMaterial.SetFloat("_OutlineWidth", outlineWidth);
                            }

                            if (originalMaterial.HasProperty("_UseVertexGreen_OutlineWidth"))
                            {
                                float useVertexGreenOutlineWidth = originalMaterial.GetFloat("_UseVertexGreen_OutlineWidth");
                                baseMaterial.SetFloat("_UseVertexGreen_OutlineWidth", useVertexGreenOutlineWidth);
                            }

                            if (originalMaterial.HasProperty("_UseVertexColorB_InnerOutline"))
                            {
                                float useVertexColorBInnerOutline = originalMaterial.GetFloat("_UseVertexColorB_InnerOutline");
                                baseMaterial.SetFloat("_UseVertexColorB_InnerOutline", useVertexColorBInnerOutline);
                            }

                            if (originalMaterial.HasProperty("_OutlineColor"))
                            {
                                Color outlineColor = originalMaterial.GetColor("_OutlineColor");
                                baseMaterial.SetColor("_OutlineColor", outlineColor);
                            }

                            if (originalMaterial.HasProperty("_UseMainTex"))
                            {
                                int useMainTex = originalMaterial.GetInt("_UseMainTex");
                                baseMaterial.SetInt("_UseMainTex", useMainTex);
                            }
                        }
                    }
                    else if (materialName.EndsWith("_HET") || materialName.EndsWith("_HETA"))
                    {
                        int lengthToTrim = materialName.EndsWith("_HET") ? 4 : 5;
                        string baseMaterialName = materialName.Substring(0, materialName.Length - lengthToTrim);
                        if (allMaterials.TryGetValue(baseMaterialName, out Material baseMaterial))
                        {
                            if (originalMaterial.HasProperty("_Mask"))
                            {
                                Texture maskTex = originalMaterial.GetTexture("_Mask");
                                baseMaterial.SetTexture("_Mask", maskTex);
                            }
                        }
                    }
                    else if (materialName.EndsWith("Bangs"))
                    {
                        string faceMaterialName = materialName.Replace("Bangs", "Face");
                        if (allMaterials.TryGetValue(faceMaterialName, out Material faceMaterial))
                        {
                            if (faceMaterial.HasProperty("_SkinSubsurfaceColor"))
                            {
                                Color shadowColor = faceMaterial.GetColor("_SkinSubsurfaceColor");
                                originalMaterial.SetColor("_HairShadowColor", shadowColor);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all materials in the directory, combining batch materials with existing materials
        /// </summary>
        private static Dictionary<string, Material> GetAllMaterialsInDirectory(string jsonFile, Dictionary<string, Material> materialsInBatch)
        {
            Dictionary<string, Material> allMaterials = new Dictionary<string, Material>(materialsInBatch);
            
            try
            {
                // Load existing materials from directory that aren't in the current batch
                string[] materialGUIDs = AssetDatabase.FindAssets("t:material", new[] { Path.GetDirectoryName(jsonFile) });
                
                foreach (string guid in materialGUIDs)
                {
                    string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                    string materialName = Path.GetFileNameWithoutExtension(materialPath);
                    
                    // Only add if not already in batch (batch materials take priority)
                    if (!allMaterials.ContainsKey(materialName))
                    {
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (mat != null)
                        {
                            allMaterials[materialName] = mat;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.WarningDebug($"Failed to load existing materials from directory: {ex.Message}");
            }
            
            return allMaterials;
        }

        [MenuItem("Assets/HoyoToon/Materials/Generate Jsons", priority = 21)]
        public static void GenerateJsonsFromMaterials()
        {
            Material[] selectedMaterials = Selection.GetFiltered<Material>(SelectionMode.Assets);

            if (selectedMaterials.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No materials selected to generate JSONs from.", "OK");
                return;
            }

            for (int i = 0; i < selectedMaterials.Length; i++)
            {
                Material material = selectedMaterials[i];
                float progress = (float)i / selectedMaterials.Length;
                
                EditorUtility.DisplayProgressBar("Generating JSONs", $"Processing {material.name} ({i + 1}/{selectedMaterials.Length})", progress);
                
                string outputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(material));
                outputPath = Path.Combine(outputPath, material.name + ".json");
                GenerateJsonFromMaterial(material, outputPath);
            }
            
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            
            HoyoToonLogs.LogDebug($"Successfully generated {selectedMaterials.Length} JSON files.");
        }

        /// <summary>
        /// Safely clear only generated material files from the Materials folder without affecting the FBX or embedded materials
        /// </summary>
        /// <param name="fbxModel">The FBX model to clear generated materials from</param>
        public static void ClearGeneratedMaterials(GameObject fbxModel)
        {
            if (fbxModel == null)
            {
                EditorUtility.DisplayDialog("Error", "No model provided to clear materials from.", "OK");
                return;
            }

            string modelPath = AssetDatabase.GetAssetPath(fbxModel);
            if (string.IsNullOrEmpty(modelPath) || !modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Error", "The provided object is not a valid FBX model.", "OK");
                return;
            }

            string modelDirectory = Path.GetDirectoryName(modelPath);
            string materialsDirectory = Path.Combine(modelDirectory, "Materials");

            // Check if Materials folder exists
            if (!Directory.Exists(materialsDirectory))
            {
                EditorUtility.DisplayDialog("Info", "No Materials folder found. Nothing to clear.", "OK");
                return;
            }

            try
            {
                // Find all .mat files in the Materials folder
                string[] materialFiles = Directory.GetFiles(materialsDirectory, "*.mat", SearchOption.TopDirectoryOnly);
                
                if (materialFiles.Length == 0)
                {
                    EditorUtility.DisplayDialog("Info", "No material files found in Materials folder.", "OK");
                    return;
                }

                AssetDatabase.StartAssetEditing();

                List<string> deletedMaterials = new List<string>();

                foreach (string materialPath in materialFiles)
                {
                    // Convert to relative path for AssetDatabase
                    string relativePath = materialPath.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                    
                    HoyoToonLogs.LogDebug($"Deleting generated material: {relativePath}");
                    
                    if (AssetDatabase.DeleteAsset(relativePath))
                    {
                        deletedMaterials.Add(Path.GetFileNameWithoutExtension(relativePath));
                    }
                    else
                    {
                        HoyoToonLogs.WarningDebug($"Failed to delete material: {relativePath}");
                    }
                }

                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                HoyoToonLogs.LogDebug($"Successfully deleted {deletedMaterials.Count} generated material files from Materials folder");
                
                // Note: We don't clear material assignments from renderers because:
                // 1. If they were using the deleted materials, Unity will show them as missing (pink)
                // 2. If they were using embedded materials, those remain untouched
                // 3. The user can re-generate materials to fix missing references

                EditorUtility.DisplayDialog("Success", 
                    $"Successfully deleted {deletedMaterials.Count} generated material files from the Materials folder.\n\n" +
                    "Note: Material assignments on renderers were not changed. You may need to re-generate materials to fix any missing references.", 
                    "OK");
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error clearing generated materials: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to clear generated materials: {e.Message}", "OK");
            }
        }

        /// <summary>
        /// Clear all materials from selected FBX models and optionally delete the material assets
        /// </summary>
        [MenuItem("Assets/HoyoToon/Materials/Clear All Materials", priority = 22)]
        public static void ClearAllMaterials()
        {
            UnityEngine.Object[] selectedObjects = Selection.objects;
            List<GameObject> fbxModels = new List<GameObject>();

            // Collect all FBX models from selection
            foreach (var selectedObject in selectedObjects)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedObject);
                
                if (selectedObject is GameObject gameObject && selectedPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    fbxModels.Add(gameObject);
                }
            }

            if (fbxModels.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No FBX models selected. Please select one or more FBX models to clear materials from.", "OK");
                return;
            }

            // For menu-driven access, give user a choice
            ClearMaterialsFromModels(fbxModels.ToArray(), true);
        }

        /// <summary>
        /// Clear all materials from a specific FBX model and optionally delete the material assets
        /// </summary>
        /// <param name="fbxModel">The FBX model to clear materials from</param>
        public static void ClearAllMaterials(GameObject fbxModel)
        {
            if (fbxModel == null)
            {
                EditorUtility.DisplayDialog("Error", "No model provided to clear materials from.", "OK");
                return;
            }

            string modelPath = AssetDatabase.GetAssetPath(fbxModel);
            if (string.IsNullOrEmpty(modelPath) || !modelPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Error", "The provided object is not a valid FBX model.", "OK");
                return;
            }

            // For programmatic access, always do both operations without asking
            ClearMaterialsFromModels(new GameObject[] { fbxModel }, false);
        }

        /// <summary>
        /// Internal method to clear materials from an array of FBX models
        /// </summary>
        /// <param name="fbxModels">Array of FBX models to clear materials from</param>
        /// <param name="showDialog">Whether to show user choice dialog</param>
        private static void ClearMaterialsFromModels(GameObject[] fbxModels, bool showDialog)
        {
            List<Material> materialsToDelete = new List<Material>();

            bool deleteMaterialAssets;
            
            if (showDialog)
            {
                // For menu-driven access, give user a choice
                deleteMaterialAssets = EditorUtility.DisplayDialog("Clear Materials", 
                    $"Clear materials from {fbxModels.Length} FBX model(s)?\n\n" +
                    "This will:\n" +
                    "• Remove material assignments from renderers\n" +
                    "• Optionally delete material asset files\n\n" +
                    "Choose your action:", 
                    "Clear & Delete Assets", "Clear Assignments Only");
            }
            else
            {
                // For programmatic access (like Materials tab), always do both
                deleteMaterialAssets = true;
            }

            int processedCount = 0;
            int deletedAssetsCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var fbxModel in fbxModels)
                {
                    string modelPath = AssetDatabase.GetAssetPath(fbxModel);
                    string modelDirectory = Path.GetDirectoryName(modelPath);

                    HoyoToonLogs.LogDebug($"Clearing materials from model: {fbxModel.name}");

                    // Find all renderers in the model
                    var renderers = fbxModel.GetComponentsInChildren<Renderer>(true);
                    
                    foreach (var renderer in renderers)
                    {
                        if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)
                        {
                            // Collect materials for deletion if they're in the same directory
                            if (deleteMaterialAssets)
                            {
                                foreach (var material in renderer.sharedMaterials)
                                {
                                    if (material != null)
                                    {
                                        string materialPath = AssetDatabase.GetAssetPath(material);
                                        string materialDirectory = Path.GetDirectoryName(materialPath);
                                        
                                        // Only delete materials that are in the same directory as the model
                                        if (materialDirectory.Equals(modelDirectory, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (!materialsToDelete.Contains(material))
                                            {
                                                materialsToDelete.Add(material);
                                            }
                                        }
                                    }
                                }
                            }

                            // Clear material assignments
                            Material[] emptyMaterials = new Material[renderer.sharedMaterials.Length];
                            for (int i = 0; i < emptyMaterials.Length; i++)
                            {
                                emptyMaterials[i] = null;
                            }
                            renderer.sharedMaterials = emptyMaterials;
                        }
                    }

                    processedCount++;
                }

                // Delete material assets if requested
                if (deleteMaterialAssets && materialsToDelete.Count > 0)
                {
                    foreach (var material in materialsToDelete)
                    {
                        string materialPath = AssetDatabase.GetAssetPath(material);
                        if (!string.IsNullOrEmpty(materialPath))
                        {
                            HoyoToonLogs.LogDebug($"Deleting material asset: {materialPath}");
                            AssetDatabase.DeleteAsset(materialPath);
                            deletedAssetsCount++;
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Show completion message
            string message = $"Successfully cleared materials from {processedCount} FBX model(s).";
            if (deleteMaterialAssets)
            {
                message += $" Deleted {deletedAssetsCount} material asset(s).";
            }

            HoyoToonLogs.LogDebug(message);
            
            // Only show dialog for menu-driven access
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Success", message, "OK");
            }
        }

        private static void GenerateJsonFromMaterial(Material material, string outputPath)
        {
            JObject jsonObject = new JObject();
            JObject m_SavedProperties = new JObject();
            JObject m_TexEnvs = new JObject();
            JObject m_Floats = new JObject();
            JObject m_Colors = new JObject();

            jsonObject["m_Shader"] = new JObject
        {
            { "m_FileID", material.shader.GetInstanceID() },
            { "Name", material.shader.name },
            { "IsNull", false }
        };

            Shader shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);

                if (propertyName.StartsWith("m_start") || propertyName.StartsWith("m_end"))
                {
                    continue;
                }

                switch (propertyType)
                {
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        Texture texture = material.GetTexture(propertyName);
                        if (texture != null)
                        {
                            JObject textureObject = new JObject
                        {
                            { "m_Texture", new JObject { { "m_FileID", 0 }, { "m_PathID", 0 }, { "Name", texture.name }, { "IsNull", false } } },
                            { "m_Scale", new JObject { { "X", material.GetTextureScale(propertyName).x }, { "Y", material.GetTextureScale(propertyName).y } } },
                            { "m_Offset", new JObject { { "X", material.GetTextureOffset(propertyName).x }, { "Y", material.GetTextureOffset(propertyName).y } } }
                        };
                            m_TexEnvs[propertyName] = textureObject;
                        }
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        float floatValue = material.GetFloat(propertyName);
                        m_Floats[propertyName] = floatValue;
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        Color colorValue = material.GetColor(propertyName);
                        JObject colorObject = new JObject
                    {
                        { "r", colorValue.r },
                        { "g", colorValue.g },
                        { "b", colorValue.b },
                        { "a", colorValue.a }
                    };
                        m_Colors[propertyName] = colorObject;
                        break;
                }
            }

            m_SavedProperties["m_TexEnvs"] = m_TexEnvs;
            m_SavedProperties["m_Floats"] = m_Floats;
            m_SavedProperties["m_Colors"] = m_Colors;
            jsonObject["m_SavedProperties"] = m_SavedProperties;

            string jsonContent = jsonObject.ToString(Formatting.Indented);

            File.WriteAllText(outputPath, jsonContent);
        }

        private static Shader DetermineShader(MaterialJsonStructure materialData)
        {
            // If shader is directly specified in the Unity format and it's a HoyoToon shader
            if (materialData.m_Shader?.Name != null && !string.IsNullOrEmpty(materialData.m_Shader.Name))
            {
                string shaderName = materialData.m_Shader.Name;
                if (HoyoToonDataManager.IsHoyoToonShader(shaderName))
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader != null)
                    {
                        HoyoToonLogs.LogDebug($"Found HoyoToon shader '{shaderName}' directly specified in JSON");
                        return shader;
                    }
                }
            }

            var shaderKeywords = HoyoToonDataManager.Data.ShaderKeywords;
            var shaderPaths = HoyoToonDataManager.Data.Shaders;

            if (shaderKeywords == null || shaderPaths == null)
            {
                HoyoToonLogs.WarningDebug("Shader data not available from API");
                return null;
            }

            // Fast keyword detection - check each shader type until we find a match
            foreach (var shaderType in shaderKeywords)
            {
                string shaderKey = shaderType.Key;
                var keywords = shaderType.Value;
                int matchCount = 0;

                // Count how many keywords from this shader type are present
                foreach (var keyword in keywords)
                {
                    bool hasKeyword = false;
                    
                    if (materialData.IsUnityFormat)
                    {
                        var properties = materialData.m_SavedProperties;
                        hasKeyword = (properties.m_TexEnvs?.ContainsKey(keyword) ?? false) ||
                                   (properties.m_Floats?.ContainsKey(keyword) ?? false) ||
                                   (properties.m_Ints?.ContainsKey(keyword) ?? false) ||
                                   (properties.m_Colors?.ContainsKey(keyword) ?? false);
                    }
                    else if (materialData.IsUnrealFormat)
                    {
                        // Special check for WuWa shader which uses ShadingModel
                        if (keyword == "ShadingModel" && materialData.Parameters?.ShadingModel != null)
                        {
                            hasKeyword = true;
                        }
                        else
                        {
                            hasKeyword = (materialData.Textures?.ContainsKey(keyword) ?? false) ||
                                       (materialData.Parameters?.Scalars?.ContainsKey(keyword) ?? false) ||
                                       (materialData.Parameters?.Switches?.ContainsKey(keyword) ?? false) ||
                                       (materialData.Parameters?.Properties?.ContainsKey(keyword) ?? false);
                        }
                    }

                    if (hasKeyword)
                    {
                        matchCount++;
                        HoyoToonLogs.LogDebug($"Found shader keyword '{keyword}' for '{shaderKey}'");
                        
                        // For efficiency: if we find ANY unique keyword, we can be confident about the shader
                        // This is much faster than counting all keywords
                        break;
                    }
                }

                // If we found keyword matches for this shader type
                if (matchCount > 0)
                {
                    // Special handling for Hi3 shaders - check for Part 2 specific keywords
                    if (shaderKey == "HI3Shader")
                    {
                        // Check if we have HI3P2 specific keywords too
                        var hi3p2Keywords = shaderKeywords.ContainsKey("HI3P2Shader") ? shaderKeywords["HI3P2Shader"] : null;
                        if (hi3p2Keywords != null)
                        {
                            foreach (var p2Keyword in hi3p2Keywords)
                            {
                                bool hasP2Keyword = false;
                                if (materialData.IsUnityFormat)
                                {
                                    var properties = materialData.m_SavedProperties;
                                    hasP2Keyword = (properties.m_TexEnvs?.ContainsKey(p2Keyword) ?? false) ||
                                                 (properties.m_Floats?.ContainsKey(p2Keyword) ?? false) ||
                                                 (properties.m_Ints?.ContainsKey(p2Keyword) ?? false) ||
                                                 (properties.m_Colors?.ContainsKey(p2Keyword) ?? false);
                                }
                                else if (materialData.IsUnrealFormat)
                                {
                                    hasP2Keyword = (materialData.Textures?.ContainsKey(p2Keyword) ?? false) ||
                                                 (materialData.Parameters?.Scalars?.ContainsKey(p2Keyword) ?? false) ||
                                                 (materialData.Parameters?.Switches?.ContainsKey(p2Keyword) ?? false) ||
                                                 (materialData.Parameters?.Properties?.ContainsKey(p2Keyword) ?? false);
                                }

                                if (hasP2Keyword)
                                {
                                    HoyoToonLogs.LogDebug($"Detected HI3P2 shader via P2-specific keyword '{p2Keyword}'");
                                    shaderKey = "HI3P2Shader";
                                    break;
                                }
                            }
                        }
                    }
                    
                    HoyoToonLogs.LogDebug($"Shader detection result: '{shaderKey}' with {matchCount} keyword matches");
                    
                    if (shaderPaths.ContainsKey(shaderKey) && shaderPaths[shaderKey].Length > 0)
                    {
                        string shaderPath = shaderPaths[shaderKey][0];
                        Shader foundShader = Shader.Find(shaderPath);
                        
                        if (foundShader != null)
                        {
                            HoyoToonLogs.LogDebug($"Successfully found shader: {shaderPath}");
                            return foundShader;
                        }
                        else
                        {
                            HoyoToonLogs.WarningDebug($"Shader path '{shaderPath}' exists in data but shader not found in project");
                        }
                    }
                    else
                    {
                        HoyoToonLogs.WarningDebug($"No shader path defined for shader key: {shaderKey}");
                    }
                }
            }

            HoyoToonLogs.WarningDebug("No matching shader keywords found in material JSON");
            return null;
        }

        private static void ProcessUnrealTexture(Material material, string propertyName, MaterialJsonStructure.TextureInfo textureInfo,
            List<string> loadedTexturePaths)
        {
            if (string.IsNullOrEmpty(textureInfo.Name)) return;

            string textureName = textureInfo.Name.Substring(textureInfo.Name.LastIndexOf('.') + 1);
            Texture texture = FindOrLoadTexture(textureName);
            
            if (texture != null)
            {
                material.SetTexture(propertyName, texture);
                string texturePath = AssetDatabase.GetAssetPath(texture);
                loadedTexturePaths.Add(texturePath);

                // Set default texture scale and offset for Unreal textures
                material.SetTextureScale(propertyName, Vector2.one);
                material.SetTextureOffset(propertyName, Vector2.zero);
            }
        }

        private static bool ContainsKey(JToken token, string key)
        {
            if (token is JArray array)
            {
                return array.Any(j => j["Key"].Value<string>() == key);
            }
            else if (token is JObject obj)
            {
                return obj.ContainsKey(key);
            }
            return false;
        }

        #endregion
    }
}
#endif