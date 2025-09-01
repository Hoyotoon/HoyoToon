#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace HoyoToon
{
    public static class HoyoToonDataManager
    {
        static string packageName = "com.meliverse.hoyotoon";
        static string packagePath = Path.Combine(HoyoToonParseManager.GetPackagePath(packageName), "Scripts/Editor/Manager");
        private static readonly string cacheFilePath = Path.Combine(packagePath, "HoyoToonManager.json");
        private static readonly string url = "https://api.hoyotoon.com/HoyoToonManager.json";
        private static HoyoToonData hoyoToonData;
        public static string HSRShader => GetShaderPath("HSRShader");
        public static string GIShader => GetShaderPath("GIShader");
        public static string HI3Shader => GetShaderPath("HI3Shader");
        public static string HI3P2Shader => GetShaderPath("HI3P2Shader");
        public static string WuWaShader => GetShaderPath("WuWaShader");
        public static string ZZZShader => GetShaderPath("ZZZShader");

        static HoyoToonDataManager()
        {
            Initialize();
        }

        private static void Initialize()
        {
            hoyoToonData = GetHoyoToonData();
        }

        public static HoyoToonData Data
        {
            get
            {
                if (hoyoToonData == null)
                {
                    Initialize();
                }
                return hoyoToonData;
            }
        }

        public static HoyoToonData GetHoyoToonData()
        {
            // Return cached data if already loaded
            if (hoyoToonData != null)
            {
                return hoyoToonData;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string json = client.GetStringAsync(url).Result;
                    CacheJson(json);
                    HoyoToonLogs.LogDebug("Successfully retrieved HoyoToon data from the server.");
                    hoyoToonData = JsonConvert.DeserializeObject<HoyoToonData>(json);
                    return hoyoToonData;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to get HoyoToon data from the server. Using cached data. Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                hoyoToonData = ReadFromCache();
                return hoyoToonData;
            }
        }

        /// <summary>
        /// Force refresh HoyoToon data from server (bypasses cache)
        /// </summary>
        public static HoyoToonData RefreshHoyoToonData()
        {
            hoyoToonData = null; // Clear cache
            return GetHoyoToonData(); // Will fetch from server
        }

        private static void CacheJson(string json)
        {
            string directoryPath = Path.GetDirectoryName(cacheFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(cacheFilePath, json);
        }

        private static HoyoToonData ReadFromCache()
        {
            if (File.Exists(cacheFilePath))
            {
                string json = File.ReadAllText(cacheFilePath);
                return JsonConvert.DeserializeObject<HoyoToonData>(json);
            }

            return new HoyoToonData();
        }

        private static string GetShaderPath(string shaderKey)
        {
            HoyoToonLogs.LogDebug($"Retrieving shader path for key: {shaderKey}");
            if (Data.Shaders.TryGetValue(shaderKey, out var paths))
            {
                HoyoToonLogs.LogDebug($"Shader path found: {paths[0]}");
                return paths[0];
            }
            HoyoToonLogs.LogDebug($"No shader path found for key: {shaderKey}");
            return null;
        }

        #region Public Helper Methods for UI

        /// <summary>
        /// Convert shader path to friendly display name
        /// Example: "HoyoToon/Zenless Zone Zero/Character" becomes "Zenless Zone Zero - Character"
        /// </summary>
        /// <param name="shaderPath">Full shader path</param>
        /// <returns>Friendly display name</returns>
        public static string GetFriendlyShaderName(string shaderPath)
        {
            if (string.IsNullOrEmpty(shaderPath))
                return "Unknown Shader";

            // Remove the "HoyoToon/" prefix if present
            var displayName = shaderPath;
            if (displayName.StartsWith("HoyoToon/"))
            {
                displayName = displayName.Substring("HoyoToon/".Length);
            }

            // Remove "/Character" suffix if present
            if (displayName.EndsWith("/Character"))
            {
                displayName = displayName.Substring(0, displayName.Length - "/Character".Length);
            }

            // Replace forward slashes with hyphens for better readability
            displayName = displayName.Replace("/", " - ");

            return displayName;
        }

        /// <summary>
        /// Get available material types for a shader from MaterialSettings data
        /// </summary>
        /// <param name="shaderPath">Full shader path</param>
        /// <returns>List of available material types</returns>
        public static List<string> GetMaterialTypesForShader(string shaderPath)
        {
            var materialTypes = new List<string>();

            if (string.IsNullOrEmpty(shaderPath))
                return materialTypes;

            try
            {
                // Get shader key from shader path
                string shaderKey = GetShaderKeyFromPath(shaderPath);
                if (string.IsNullOrEmpty(shaderKey))
                {
                    HoyoToonLogs.WarningDebug($"Could not determine shader key for path: {shaderPath}");
                    return GetDefaultMaterialTypes();
                }

                // Get material settings for this shader
                if (Data.MaterialSettings != null && Data.MaterialSettings.TryGetValue(shaderPath, out var shaderSettings))
                {
                    // Extract material types from the settings keys
                    foreach (var settingKey in shaderSettings.Keys)
                    {
                        if (settingKey != "Default" && !materialTypes.Contains(settingKey))
                        {
                            materialTypes.Add(settingKey);
                        }
                    }
                }

                // If no types found in settings, return defaults
                if (materialTypes.Count == 0)
                {
                    materialTypes = GetDefaultMaterialTypes();
                }
            }
            catch (Exception e)
            {
                HoyoToonLogs.WarningDebug($"Failed to get material types for shader {shaderPath}: {e.Message}");
                materialTypes = GetDefaultMaterialTypes();
            }

            return materialTypes;
        }

        /// <summary>
        /// Get all available shader paths as override options
        /// </summary>
        /// <returns>List of all available shader paths</returns>
        public static List<string> GetAvailableShaderOverrides()
        {
            var overrides = new List<string>();

            if (Data?.Shaders == null) 
                return overrides;

            // Add ALL available shaders as overrides
            foreach (var shaderGroup in Data.Shaders)
            {
                overrides.AddRange(shaderGroup.Value);
            }

            return overrides.Distinct().ToList();
        }

        /// <summary>
        /// Get shader key from shader path
        /// </summary>
        /// <param name="shaderPath">Full shader path</param>
        /// <returns>Shader key (e.g., "HSRShader", "GIShader") or null if not found</returns>
        public static string GetShaderKeyFromPath(string shaderPath)
        {
            if (Data?.Shaders == null) 
                return null;

            foreach (var kvp in Data.Shaders)
            {
                if (kvp.Value != null && kvp.Value.Contains(shaderPath))
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Get default material types when shader-specific types are not available
        /// </summary>
        /// <returns>Default list of material types</returns>
        private static List<string> GetDefaultMaterialTypes()
        {
            return new List<string> { "Base", "Face", "Hair", "Body", "Clothing", "Weapon", "Eyes" };
        }

        /// <summary>
        /// Check if a shader name is a HoyoToon shader
        /// </summary>
        /// <param name="shaderName">Shader name to check</param>
        /// <returns>True if it's a HoyoToon shader</returns>
        public static bool IsHoyoToonShader(string shaderName)
        {
            var data = Data;
            if (data?.Shaders == null) return false;

            foreach (var shaderGroup in data.Shaders.Values)
            {
                if (shaderGroup.Contains(shaderName))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get shader key from shader name or instance
        /// </summary>
        /// <param name="shader">Shader instance</param>
        /// <returns>Shader key (e.g., "HSRShader", "GIShader") or "Global" if not found</returns>
        public static string GetShaderKey(Shader shader)
        {
            return GetShaderKey(shader?.name);
        }

        /// <summary>
        /// Get shader key from shader name
        /// </summary>
        /// <param name="shaderName">Shader name</param>
        /// <returns>Shader key (e.g., "HSRShader", "GIShader") or "Global" if not found</returns>
        public static string GetShaderKey(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return "Global";

            // Use hardcoded approach for performance (these mappings don't change)
            if (shaderName == HSRShader) return "HSRShader";
            if (shaderName == GIShader) return "GIShader";
            if (shaderName == HI3Shader) return "HI3Shader";
            if (shaderName == HI3P2Shader) return "HI3P2Shader";
            if (shaderName == WuWaShader) return "WuWaShader";
            if (shaderName == ZZZShader) return "ZZZShader";

            // Fallback to data-driven approach for unknown shaders
            var data = Data;
            if (data?.Shaders != null)
            {
                foreach (var kvp in data.Shaders)
                {
                    if (kvp.Value != null && kvp.Value.Contains(shaderName))
                    {
                        return kvp.Key;
                    }
                }
            }

            return "Global";
        }

        /// <summary>
        /// Get shader key from material
        /// </summary>
        /// <param name="material">Material instance</param>
        /// <returns>Shader key (e.g., "HSRShader", "GIShader") or "Global" if not found</returns>
        public static string GetShaderKey(Material material)
        {
            return GetShaderKey(material?.shader);
        }

        #endregion

        #region Texture Analysis and Optimization API

        /// <summary>
        /// Analyze texture and determine if it should be optimized according to HoyoToon standards
        /// </summary>
        /// <param name="texturePath">Path to the texture asset</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>Texture analysis result with optimization recommendations</returns>
        public static HoyoToonTextureAnalysis AnalyzeTexture(string texturePath, string shaderKey = null)
        {
            var analysis = new HoyoToonTextureAnalysis
            {
                TexturePath = texturePath,
                TextureName = Path.GetFileNameWithoutExtension(texturePath),
                ShaderKey = shaderKey
            };

            try
            {
                var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var importer = UnityEditor.AssetImporter.GetAtPath(texturePath) as UnityEditor.TextureImporter;

                if (texture == null || importer == null)
                {
                    analysis.IsValid = false;
                    analysis.Issues.Add("Texture or importer not found");
                    return analysis;
                }

                analysis.IsValid = true;
                analysis.CurrentSettings = GetCurrentTextureSettings(importer);
                analysis.RecommendedSettings = GetRecommendedTextureSettings(analysis.TextureName, shaderKey);

                // Analyze current vs recommended settings
                AnalyzeSettingsComparison(analysis);
            }
            catch (Exception e)
            {
                analysis.IsValid = false;
                analysis.Issues.Add($"Analysis failed: {e.Message}");
                HoyoToonLogs.ErrorDebug($"Texture analysis failed for {texturePath}: {e.Message}");
            }

            return analysis;
        }

        /// <summary>
        /// Get recommended texture settings based on HoyoToon configuration
        /// </summary>
        /// <param name="textureName">Name of the texture</param>
        /// <param name="shaderKey">Optional shader key for shader-specific settings</param>
        /// <returns>Recommended texture import settings</returns>
        public static HoyoToonData.TextureImportSettingsData.TextureImportSettings GetRecommendedTextureSettings(string textureName, string shaderKey = null)
        {
            return Data.GetTextureImportSettingsForShader(shaderKey ?? "Global", textureName);
        }

        /// <summary>
        /// Check if a texture should be optimized according to HoyoToon standards
        /// </summary>
        /// <param name="texturePath">Path to the texture asset</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>True if texture needs optimization</returns>
        public static bool ShouldOptimizeTexture(string texturePath, string shaderKey = null)
        {
            var analysis = AnalyzeTexture(texturePath, shaderKey);
            return analysis.IsValid && analysis.NeedsOptimization;
        }

        /// <summary>
        /// Get optimization recommendations for a texture
        /// </summary>
        /// <param name="texturePath">Path to the texture asset</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>List of optimization recommendations</returns>
        public static List<string> GetTextureOptimizationRecommendations(string texturePath, string shaderKey = null)
        {
            var analysis = AnalyzeTexture(texturePath, shaderKey);
            return analysis.IsValid ? analysis.Recommendations : new List<string> { "Texture analysis failed" };
        }

        /// <summary>
        /// Check if texture can be safely resized without quality loss
        /// </summary>
        /// <param name="texturePath">Path to the texture asset</param>
        /// <param name="targetSize">Target maximum size</param>
        /// <returns>True if texture can be safely resized</returns>
        public static bool CanSafelyResizeTexture(string texturePath, int targetSize = 2048)
        {
            try
            {
                var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) return false;

                // Don't resize if texture is already smaller or equal to target
                if (texture.width <= targetSize && texture.height <= targetSize) return false;

                // Check if texture has specific size requirements based on its name/purpose
                string textureName = Path.GetFileNameWithoutExtension(texturePath).ToLower();
                
                // UI textures and detail textures can usually be resized
                bool isResizableType = textureName.Contains("ui") || 
                                     textureName.Contains("detail") || 
                                     textureName.Contains("secondary") ||
                                     textureName.Contains("noise") ||
                                     textureName.EndsWith("_m") || // Mask textures
                                     textureName.EndsWith("_r"); // Roughness textures

                // Main diffuse/albedo textures should be more carefully evaluated
                bool isDiffuseTexture = textureName.Contains("diffuse") || 
                                      textureName.Contains("albedo") || 
                                      textureName.Contains("color") ||
                                      textureName.Contains("base");

                if (isDiffuseTexture && texture.width > 4096)
                {
                    // Large diffuse textures can usually be reduced to 4096 or 2048
                    return true;
                }

                return isResizableType;
            }
            catch
            {
                return false;
            }
        }

        private static HoyoToonTextureSettings GetCurrentTextureSettings(UnityEditor.TextureImporter importer)
        {
            return new HoyoToonTextureSettings
            {
                TextureType = importer.textureType.ToString(),
                TextureCompression = importer.textureCompression.ToString(),
                MipmapEnabled = importer.mipmapEnabled,
                StreamingMipmaps = importer.streamingMipmaps,
                WrapMode = importer.wrapMode.ToString(),
                SRGBTexture = importer.sRGBTexture,
                NPOTScale = importer.npotScale.ToString(),
                MaxTextureSize = importer.maxTextureSize,
                FilterMode = importer.filterMode.ToString()
            };
        }

        private static void AnalyzeSettingsComparison(HoyoToonTextureAnalysis analysis)
        {
            if (analysis.RecommendedSettings == null)
            {
                analysis.Issues.Add("No recommended settings found - using default optimization rules");
                AnalyzeWithDefaults(analysis);
                return;
            }

            var current = analysis.CurrentSettings;
            var recommended = analysis.RecommendedSettings;

            // Check each setting
            if (!string.IsNullOrEmpty(recommended.TextureType) && 
                current.TextureType != recommended.TextureType)
            {
                analysis.Recommendations.Add($"Change texture type from {current.TextureType} to {recommended.TextureType}");
                analysis.NeedsOptimization = true;
            }

            if (!string.IsNullOrEmpty(recommended.TextureCompression) && 
                current.TextureCompression != recommended.TextureCompression)
            {
                analysis.Recommendations.Add($"Change compression from {current.TextureCompression} to {recommended.TextureCompression}");
                analysis.NeedsOptimization = true;
            }

            if (recommended.MipmapEnabled.HasValue && 
                current.MipmapEnabled != recommended.MipmapEnabled.Value)
            {
                analysis.Recommendations.Add($"Change mipmaps to {(recommended.MipmapEnabled.Value ? "enabled" : "disabled")}");
                analysis.NeedsOptimization = true;
            }

            if (recommended.SRGBTexture.HasValue && 
                current.SRGBTexture != recommended.SRGBTexture.Value)
            {
                analysis.Recommendations.Add($"Change sRGB setting to {recommended.SRGBTexture.Value}");
                analysis.NeedsOptimization = true;
            }

            if (recommended.MaxTextureSize.HasValue && 
                current.MaxTextureSize > recommended.MaxTextureSize.Value)
            {
                analysis.Recommendations.Add($"Reduce max texture size from {current.MaxTextureSize} to {recommended.MaxTextureSize.Value}");
                analysis.NeedsOptimization = true;
                analysis.CanReduceSize = true;
            }

            if (!string.IsNullOrEmpty(recommended.FilterMode) && 
                current.FilterMode != recommended.FilterMode)
            {
                analysis.Recommendations.Add($"Change filter mode from {current.FilterMode} to {recommended.FilterMode}");
                analysis.NeedsOptimization = true;
            }

            if (!string.IsNullOrEmpty(recommended.WrapMode) && 
                current.WrapMode != recommended.WrapMode)
            {
                analysis.Recommendations.Add($"Change wrap mode from {current.WrapMode} to {recommended.WrapMode}");
                analysis.NeedsOptimization = true;
            }
        }

        private static void AnalyzeWithDefaults(HoyoToonTextureAnalysis analysis)
        {
            var current = analysis.CurrentSettings;

            // Default optimization rules when no specific settings found
            if (current.TextureCompression == "Uncompressed")
            {
                analysis.Recommendations.Add("Enable texture compression for better performance");
                analysis.NeedsOptimization = true;
            }

            if (current.MaxTextureSize > 2048 && CanSafelyResizeTexture(analysis.TexturePath, 2048))
            {
                analysis.Recommendations.Add($"Consider reducing texture size from {current.MaxTextureSize} to 2048 for better performance");
                analysis.CanReduceSize = true;
                analysis.NeedsOptimization = true;
            }

            if (!current.MipmapEnabled)
            {
                analysis.Recommendations.Add("Enable mipmaps for better performance at distance");
                analysis.NeedsOptimization = true;
            }
        }

        #endregion
    }

    public class HoyoToonData
    {
        public Dictionary<string, TextureAssignmentData> TextureAssignments { get; set; }
        public Dictionary<string, TextureImportSettingsData> TextureImportSettings { get; set; }
        public Dictionary<string, string[]> Shaders { get; set; }
        public Dictionary<string, string[]> ShaderKeywords { get; set; }
        public Dictionary<string, string[]> SkipMeshes { get; set; }
        public Dictionary<string, Dictionary<string, Dictionary<string, object>>> MaterialSettings { get; set; }
        public Dictionary<string, Dictionary<string, string>> PropertyNameMappings { get; set; }

        public class TextureAssignmentData
        {
            public string[] BodyTypes { get; set; }
            public Dictionary<string, object> Properties { get; set; }

            /// <summary>
            /// Get texture name for a specific property and body type index
            /// </summary>
            /// <param name="propertyName">Shader property name</param>
            /// <param name="bodyTypeIndex">Index in the BodyTypes array</param>
            /// <returns>Texture name or null if not found</returns>
            public string GetTextureForProperty(string propertyName, int bodyTypeIndex)
            {
                if (Properties == null || !Properties.TryGetValue(propertyName, out var value))
                    return null;

                // Handle array of textures (one per body type)
                if (value is Newtonsoft.Json.Linq.JArray arrayValue)
                {
                    var textureArray = arrayValue.ToObject<string[]>();
                    if (bodyTypeIndex >= 0 && bodyTypeIndex < textureArray.Length)
                        return textureArray[bodyTypeIndex];
                }
                // Handle single texture (shared across all body types)
                else if (value is string stringValue)
                {
                    return stringValue;
                }

                return null;
            }

            /// <summary>
            /// Get body type index for a given body type string
            /// </summary>
            /// <param name="bodyType">Body type string</param>
            /// <returns>Index or -1 if not found</returns>
            public int GetBodyTypeIndex(string bodyType)
            {
                if (BodyTypes == null) return -1;
                
                for (int i = 0; i < BodyTypes.Length; i++)
                {
                    if (BodyTypes[i].Equals(bodyType, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return -1;
            }
        }

        public class TextureImportSettingsData
        {
            public Dictionary<string, TextureImportSettings> SpecificTextures { get; set; }
            public Dictionary<string, TextureImportSettings> PatternSettings { get; set; }
            public Dictionary<string, TextureImportSettings> EndsWithPatternSettings { get; set; }
            public TextureImportSettings DefaultSettings { get; set; }

            public class TextureImportSettings
            {
                public string TextureType { get; set; }
                public string TextureCompression { get; set; }
                public bool? MipmapEnabled { get; set; }
                public bool? StreamingMipmaps { get; set; }
                public string WrapMode { get; set; }
                public bool? SRGBTexture { get; set; }
                public string NPOTScale { get; set; }
                public int? MaxTextureSize { get; set; }
                public string FilterMode { get; set; }
            }

            /// <summary>
            /// Get texture import settings for a specific texture name
            /// </summary>
            /// <param name="textureName">Name of the texture</param>
            /// <returns>TextureImportSettings if found, null otherwise</returns>
            public TextureImportSettings GetSettingsForTexture(string textureName)
            {
                // First check for specific texture name match
                if (SpecificTextures != null && 
                    SpecificTextures.TryGetValue(textureName, out var specificSettings))
                {
                    return specificSettings;
                }

                // Then check for pattern matches (contains)
                if (PatternSettings != null)
                {
                    foreach (var pattern in PatternSettings)
                    {
                        if (textureName.IndexOf(pattern.Key, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            return pattern.Value;
                        }
                    }
                }

                // Then check for ends with pattern matches
                if (EndsWithPatternSettings != null)
                {
                    foreach (var pattern in EndsWithPatternSettings)
                    {
                        if (textureName.EndsWith(pattern.Key, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return pattern.Value;
                        }
                    }
                }

                // Return default settings if available
                return DefaultSettings;
            }
        }
        
        /// <summary>
        /// Get skip meshes for a specific shader type
        /// </summary>
        /// <param name="shaderKey">Shader key (e.g., "HSRShader", "GIShader", etc.)</param>
        /// <returns>Array of mesh names to skip, or empty array if shader not found</returns>
        public string[] GetSkipMeshesForShader(string shaderKey)
        {
            if (SkipMeshes != null && SkipMeshes.TryGetValue(shaderKey, out var meshes))
            {
                return meshes ?? new string[0];
            }
            return new string[0];
        }
        
        /// <summary>
        /// Check if a specific mesh should be skipped for a given shader type
        /// </summary>
        /// <param name="shaderKey">Shader key (e.g., "HSRShader", "GIShader", etc.)</param>
        /// <param name="meshName">Name of the mesh to check</param>
        /// <returns>True if the mesh should be skipped, false otherwise</returns>
        public bool ShouldSkipMesh(string shaderKey, string meshName)
        {
            var skipMeshes = GetSkipMeshesForShader(shaderKey);
            
            // Check if all meshes should be skipped (wildcard "*")
            if (skipMeshes.Contains("*"))
            {
                return true;
            }
            
            // Check if specific mesh name should be skipped
            return skipMeshes.Contains(meshName);
        }

        /// <summary>
        /// Get texture assignment data for a specific shader
        /// </summary>
        /// <param name="shaderKey">Shader key (e.g., "HSRShader", "GIShader", etc.)</param>
        /// <returns>TextureAssignmentData if found, null otherwise</returns>
        public TextureAssignmentData GetTextureAssignmentForShader(string shaderKey)
        {
            if (TextureAssignments != null && TextureAssignments.TryGetValue(shaderKey, out var assignment))
            {
                return assignment;
            }
            return null;
        }

        /// <summary>
        /// Get texture import settings for a specific shader and texture name
        /// </summary>
        /// <param name="shaderKey">Shader key (e.g., "HSRShader", "GIShader", etc.)</param>
        /// <param name="textureName">Name of the texture</param>
        /// <returns>TextureImportSettings if found, null otherwise</returns>
        public TextureImportSettingsData.TextureImportSettings GetTextureImportSettingsForShader(string shaderKey, string textureName)
        {
            // First try shader-specific settings
            if (TextureImportSettings != null && TextureImportSettings.TryGetValue(shaderKey, out var shaderSettings))
            {
                var settings = shaderSettings.GetSettingsForTexture(textureName);
                if (settings != null)
                    return settings;
            }

            // Fallback to global settings
            if (TextureImportSettings != null && TextureImportSettings.TryGetValue("Global", out var globalSettings))
            {
                return globalSettings.GetSettingsForTexture(textureName);
            }

            return null;
        }

        /// <summary>
        /// Get texture import settings for a texture name (legacy method for backward compatibility)
        /// </summary>
        /// <param name="textureName">Name of the texture</param>
        /// <returns>TextureImportSettings if found, null otherwise</returns>
        public TextureImportSettingsData.TextureImportSettings GetTextureSettings(string textureName)
        {
            return GetTextureImportSettingsForShader("Global", textureName);
        }

        /// <summary>
        /// Resolve property name using the mapping system
        /// </summary>
        /// <param name="shaderKey">Shader key (e.g., "HSRShader", "GIShader", etc.)</param>
        /// <param name="originalPropertyName">Original property name from the material JSON</param>
        /// <returns>Mapped Unity property name, or fallback name if no mapping found</returns>
        public string ResolvePropertyName(string shaderKey, string originalPropertyName)
        {
            // First try shader-specific mappings
            if (PropertyNameMappings != null && PropertyNameMappings.TryGetValue(shaderKey, out var shaderMappings))
            {
                if (shaderMappings.TryGetValue(originalPropertyName, out var mappedName))
                {
                    return mappedName;
                }
            }

            // Then try global mappings
            if (PropertyNameMappings != null && PropertyNameMappings.TryGetValue("Global", out var globalMappings))
            {
                if (globalMappings.TryGetValue(originalPropertyName, out var mappedName))
                {
                    return mappedName;
                }
            }

            // Fallback: apply default naming convention (add underscore prefix and clean up)
            return CreateFallbackPropertyName(originalPropertyName);
        }

        /// <summary>
        /// Creates a fallback Unity property name from the original property name
        /// </summary>
        /// <param name="originalPropertyName">Original property name</param>
        /// <returns>Cleaned up Unity property name</returns>
        private string CreateFallbackPropertyName(string originalPropertyName)
        {
            if (string.IsNullOrEmpty(originalPropertyName))
                return originalPropertyName;

            // Remove common suffixes and special characters
            string cleanedName = originalPropertyName
                .Replace(" (S)", "") // Remove scalar suffix
                .Replace(" (V)", "") // Remove vector suffix
                .Replace(" (0-1)", "") // Remove range indicators
                .Replace(" ", "") // Remove spaces
                .Replace("(", "") // Remove parentheses
                .Replace(")", "")
                .Replace("-", ""); // Remove hyphens

            // Add underscore prefix if not already present
            if (!cleanedName.StartsWith("_"))
            {
                cleanedName = "_" + cleanedName;
            }

            return cleanedName;
        }

        /// <summary>
        /// Get all property name mappings for a specific shader
        /// </summary>
        /// <param name="shaderKey">Shader key</param>
        /// <returns>Dictionary of property mappings, or empty dictionary if none found</returns>
        public Dictionary<string, string> GetPropertyMappingsForShader(string shaderKey)
        {
            var result = new Dictionary<string, string>();

            // Add global mappings first
            if (PropertyNameMappings != null && PropertyNameMappings.TryGetValue("Global", out var globalMappings))
            {
                foreach (var kvp in globalMappings)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            // Add shader-specific mappings (will override global if there are conflicts)
            if (PropertyNameMappings != null && PropertyNameMappings.TryGetValue(shaderKey, out var shaderMappings))
            {
                foreach (var kvp in shaderMappings)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
    }

    #region Texture Analysis Support Classes

    /// <summary>
    /// Comprehensive texture analysis result with optimization recommendations
    /// </summary>
    public class HoyoToonTextureAnalysis
    {
        public string TexturePath { get; set; }
        public string TextureName { get; set; }
        public string ShaderKey { get; set; }
        public bool IsValid { get; set; }
        public bool NeedsOptimization { get; set; }
        public bool CanReduceSize { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public HoyoToonTextureSettings CurrentSettings { get; set; }
        public HoyoToonData.TextureImportSettingsData.TextureImportSettings RecommendedSettings { get; set; }

        /// <summary>
        /// Get priority level for optimization (Higher = More Important)
        /// </summary>
        public int OptimizationPriority
        {
            get
            {
                if (!NeedsOptimization) return 0;

                int priority = 0;
                foreach (var rec in Recommendations)
                {
                    if (rec.Contains("compression")) priority += 3; // High priority
                    if (rec.Contains("size") || rec.Contains("reduce")) priority += 2; // Medium priority
                    if (rec.Contains("mipmap")) priority += 1; // Low priority
                    if (rec.Contains("sRGB")) priority += 1; // Low priority
                }
                return priority;
            }
        }

        /// <summary>
        /// Get estimated memory savings from optimization
        /// </summary>
        public string EstimatedSavings
        {
            get
            {
                if (!CanReduceSize) return "No size reduction possible";
                
                try
                {
                    var texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                    if (texture == null) return "Cannot calculate";

                    // Estimate memory reduction if size is reduced
                    long currentMemory = texture.width * texture.height * 4; // Rough estimate
                    if (RecommendedSettings?.MaxTextureSize.HasValue == true)
                    {
                        int newSize = RecommendedSettings.MaxTextureSize.Value;
                        long newMemory = newSize * newSize * 4;
                        long saved = currentMemory - newMemory;
                        if (saved > 0)
                        {
                            return $"~{saved / (1024 * 1024)}MB";
                        }
                    }
                    return "Minimal";
                }
                catch
                {
                    return "Cannot calculate";
                }
            }
        }
    }

    /// <summary>
    /// Current texture settings representation
    /// </summary>
    public class HoyoToonTextureSettings
    {
        public string TextureType { get; set; }
        public string TextureCompression { get; set; }
        public bool MipmapEnabled { get; set; }
        public bool StreamingMipmaps { get; set; }
        public string WrapMode { get; set; }
        public bool SRGBTexture { get; set; }
        public string NPOTScale { get; set; }
        public int MaxTextureSize { get; set; }
        public string FilterMode { get; set; }
    }

    #endregion
}
#endif