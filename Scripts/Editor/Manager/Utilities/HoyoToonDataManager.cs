#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        public static string Hi3Shader => GetShaderPath("Hi3Shader");
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
}
#endif