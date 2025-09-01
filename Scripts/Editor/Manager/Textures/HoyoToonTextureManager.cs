#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace HoyoToon
{
    public class HoyoToonTextureManager
    {

        #region Data-Driven Texture Management

        /// <summary>
        /// Find and assign texture to material property based on JSON configuration
        /// </summary>
        /// <param name="newMaterial">Target material</param>
        /// <param name="propertyName">Shader property name</param>
        /// <param name="shader">Shader instance</param>
        public static void HardsetTexture(Material newMaterial, string propertyName, Shader shader)
        {
            string shaderKey = HoyoToonDataManager.GetShaderKey(shader);
            if (string.IsNullOrEmpty(shaderKey))
            {
                HoyoToonLogs.WarningDebug($"No shader key found for shader: {shader.name}");
                return;
            }

            var assignmentData = HoyoToonDataManager.Data.GetTextureAssignmentForShader(shaderKey);
            if (assignmentData == null)
            {
                HoyoToonLogs.WarningDebug($"No texture assignment data found for shader key: {shaderKey}");
                return;
            }

            string currentBodyTypeString = HoyoToonParseManager.currentBodyType.ToString();
            int bodyTypeIndex = assignmentData.GetBodyTypeIndex(currentBodyTypeString);
            
            if (bodyTypeIndex == -1)
            {
                HoyoToonLogs.WarningDebug($"Body type '{currentBodyTypeString}' not found in shader '{shaderKey}' configuration");
                return;
            }

            string textureName = assignmentData.GetTextureForProperty(propertyName, bodyTypeIndex);
            if (string.IsNullOrEmpty(textureName))
            {
                HoyoToonLogs.WarningDebug($"No texture mapping found for property '{propertyName}' in shader '{shaderKey}' for body type '{currentBodyTypeString}'");
                return;
            }

            // Find and load the texture
            Texture texture = LoadTextureByName(textureName, out List<string> texturePaths);
            
            if (texture != null)
            {
                newMaterial.SetTexture(propertyName, texture);
                SetTextureImportSettings(texturePaths, shaderKey);
                HoyoToonLogs.LogDebug($"Successfully assigned texture '{textureName}' to property '{propertyName}' for material '{newMaterial.name}'");
            }
            else
            {
                HoyoToonLogs.WarningDebug($"Texture not found with name: {textureName}");
            }
        }

        /// <summary>
        /// Load texture by name from Resources or AssetDatabase
        /// </summary>
        /// <param name="textureName">Name of the texture to load (can include file extension)</param>
        /// <param name="texturePaths">Output list of texture paths for import settings</param>
        /// <returns>Loaded texture or null if not found</returns>
        private static Texture LoadTextureByName(string textureName, out List<string> texturePaths)
        {
            texturePaths = new List<string>();
            
            // Check if texture name includes a file extension
            bool hasExtension = Path.HasExtension(textureName);
            string nameWithoutExtension = hasExtension ? Path.GetFileNameWithoutExtension(textureName) : textureName;
            
            // Try loading from Resources first (without extension)
            Texture texture = Resources.Load<Texture>(nameWithoutExtension);
            if (texture != null)
            {
                return texture;
            }
            
            // Search in AssetDatabase
            string[] guids = AssetDatabase.FindAssets(nameWithoutExtension);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);
                string assetNameWithoutExt = Path.GetFileNameWithoutExtension(path);
                
                // If texture name has extension, match exact filename
                if (hasExtension)
                {
                    if (fileName.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                    {
                        texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        if (texture != null)
                        {
                            texturePaths.Add(path);
                            HoyoToonLogs.LogDebug($"Found exact texture match: {fileName} at {path}");
                            return texture;
                        }
                    }
                }
                // If no extension specified, match by name without extension
                else
                {
                    if (assetNameWithoutExt.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                    {
                        texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        if (texture != null)
                        {
                            texturePaths.Add(path);
                            HoyoToonLogs.LogDebug($"Found texture match: {fileName} at {path}");
                            return texture;
                        }
                    }
                }
            }

            HoyoToonLogs.WarningDebug($"Texture not found: {textureName}");
            return null;
        }

        /// <summary>
        /// Apply texture import settings based on JSON configuration
        /// </summary>
        /// <param name="paths">Texture file paths</param>
        /// <param name="shaderKey">Shader key for shader-specific settings</param>
        public static void SetTextureImportSettings(IEnumerable<string> paths, string shaderKey = null)
        {
            var pathsToReimport = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null) continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    // Apply JSON-based settings
                    if (ApplyJsonBasedSettings(importer, texture.name, shaderKey))
                    {
                        pathsToReimport.Add(path);
                        HoyoToonLogs.LogDebug($"Applied JSON-based texture settings for: {texture.name} (shader: {shaderKey ?? "Global"})");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Reimport textures that need updating
            if (pathsToReimport.Count > 0)
            {
                foreach (var path in pathsToReimport)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        #endregion

        #region Import Settings Application

        /// <summary>
        /// Apply JSON-based texture import settings
        /// Order: Global Default -> Global Patterns -> Shader Default -> Shader Patterns
        /// </summary>
        /// <param name="importer">Texture importer</param>
        /// <param name="textureName">Texture name</param>
        /// <param name="shaderKey">Shader key for shader-specific settings</param>
        /// <returns>True if settings were applied, false if no settings found</returns>
        private static bool ApplyJsonBasedSettings(TextureImporter importer, string textureName, string shaderKey)
        {
            bool settingsChanged = false;
            
            // Get Global settings (always applied as base)
            var globalSettings = HoyoToonDataManager.Data.TextureImportSettings?.GetValueOrDefault("Global");
            
            // Get shader-specific settings (will override global where defined)
            var shaderSettings = string.IsNullOrEmpty(shaderKey) || shaderKey == "Global" 
                ? null 
                : HoyoToonDataManager.Data.TextureImportSettings?.GetValueOrDefault(shaderKey);

            // === STEP 1: Apply Global Settings (Base Layer) ===
            if (globalSettings != null)
            {
                // Apply Global Default Settings
                if (globalSettings.DefaultSettings != null)
                {
                    if (ApplyTextureSettings(importer, globalSettings.DefaultSettings))
                    {
                        settingsChanged = true;
                        HoyoToonLogs.LogDebug($"Applied global default settings for texture: {textureName}");
                    }
                }

                // Apply Global Pattern Settings
                if (globalSettings.PatternSettings != null)
                {
                    foreach (var pattern in globalSettings.PatternSettings)
                    {
                        if (textureName.ToLower().Contains(pattern.Key.ToLower()))
                        {
                            if (ApplyTextureSettings(importer, pattern.Value))
                            {
                                settingsChanged = true;
                                HoyoToonLogs.LogDebug($"Applied global pattern settings '{pattern.Key}' for texture: {textureName}");
                            }
                        }
                    }
                }

                // Apply Global EndsWithPattern Settings
                if (globalSettings.EndsWithPatternSettings != null)
                {
                    foreach (var pattern in globalSettings.EndsWithPatternSettings)
                    {
                        if (textureName.ToLower().EndsWith(pattern.Key.ToLower()))
                        {
                            if (ApplyTextureSettings(importer, pattern.Value))
                            {
                                settingsChanged = true;
                                HoyoToonLogs.LogDebug($"Applied global ends-with pattern settings '{pattern.Key}' for texture: {textureName}");
                            }
                        }
                    }
                }

                // Apply Global Specific Texture Settings
                if (globalSettings.SpecificTextures != null && globalSettings.SpecificTextures.ContainsKey(textureName))
                {
                    if (ApplyTextureSettings(importer, globalSettings.SpecificTextures[textureName]))
                    {
                        settingsChanged = true;
                        HoyoToonLogs.LogDebug($"Applied global specific settings for texture: {textureName}");
                    }
                }
            }

            // === STEP 2: Apply Shader-Specific Settings (Override Layer) ===
            if (shaderSettings != null)
            {
                HoyoToonLogs.LogDebug($"Applying shader-specific settings '{shaderKey}' on top of global settings for texture: {textureName}");

                // Apply Shader Default Settings
                if (shaderSettings.DefaultSettings != null)
                {
                    if (ApplyTextureSettings(importer, shaderSettings.DefaultSettings))
                    {
                        settingsChanged = true;
                        HoyoToonLogs.LogDebug($"Applied shader default settings '{shaderKey}' for texture: {textureName}");
                    }
                }

                // Apply Shader Pattern Settings
                if (shaderSettings.PatternSettings != null)
                {
                    foreach (var pattern in shaderSettings.PatternSettings)
                    {
                        if (textureName.ToLower().Contains(pattern.Key.ToLower()))
                        {
                            if (ApplyTextureSettings(importer, pattern.Value))
                            {
                                settingsChanged = true;
                                HoyoToonLogs.LogDebug($"Applied shader pattern settings '{pattern.Key}' (shader: {shaderKey}) for texture: {textureName}");
                            }
                        }
                    }
                }

                // Apply Shader EndsWithPattern Settings
                if (shaderSettings.EndsWithPatternSettings != null)
                {
                    foreach (var pattern in shaderSettings.EndsWithPatternSettings)
                    {
                        if (textureName.ToLower().EndsWith(pattern.Key.ToLower()))
                        {
                            if (ApplyTextureSettings(importer, pattern.Value))
                            {
                                settingsChanged = true;
                                HoyoToonLogs.LogDebug($"Applied shader ends-with pattern settings '{pattern.Key}' (shader: {shaderKey}) for texture: {textureName}");
                            }
                        }
                    }
                }

                // Apply Shader Specific Texture Settings
                if (shaderSettings.SpecificTextures != null && shaderSettings.SpecificTextures.ContainsKey(textureName))
                {
                    if (ApplyTextureSettings(importer, shaderSettings.SpecificTextures[textureName]))
                    {
                        settingsChanged = true;
                        HoyoToonLogs.LogDebug($"Applied shader specific settings (shader: {shaderKey}) for texture: {textureName}");
                    }
                }
            }

            return settingsChanged;
        }

        /// <summary>
        /// Apply a set of texture settings to the importer
        /// </summary>
        /// <param name="importer">Texture importer</param>
        /// <param name="settings">Settings to apply</param>
        /// <returns>True if any settings were changed</returns>
        private static bool ApplyTextureSettings(TextureImporter importer, dynamic settings)
        {
            if (settings == null) return false;

            bool settingsChanged = false;

            // Check and apply texture type
            if (!string.IsNullOrEmpty(settings.TextureType))
            {
                var newType = ParseTextureType(settings.TextureType);
                if (importer.textureType != newType)
                {
                    importer.textureType = newType;
                    settingsChanged = true;
                }
            }

            // Check and apply compression
            if (!string.IsNullOrEmpty(settings.TextureCompression))
            {
                var newCompression = ParseTextureCompression(settings.TextureCompression);
                if (importer.textureCompression != newCompression)
                {
                    importer.textureCompression = newCompression;
                    settingsChanged = true;
                }
            }

            // Check and apply mipmap settings
            if (settings.MipmapEnabled != null && importer.mipmapEnabled != settings.MipmapEnabled)
            {
                importer.mipmapEnabled = settings.MipmapEnabled;
                settingsChanged = true;
            }

            if (settings.StreamingMipmaps != null && importer.streamingMipmaps != settings.StreamingMipmaps)
            {
                importer.streamingMipmaps = settings.StreamingMipmaps;
                settingsChanged = true;
            }

            // Check and apply wrap mode
            if (!string.IsNullOrEmpty(settings.WrapMode))
            {
                var newWrapMode = ParseWrapMode(settings.WrapMode);
                if (importer.wrapMode != newWrapMode)
                {
                    importer.wrapMode = newWrapMode;
                    settingsChanged = true;
                }
            }

            // Check and apply sRGB setting
            if (settings.SRGBTexture != null && importer.sRGBTexture != settings.SRGBTexture)
            {
                importer.sRGBTexture = settings.SRGBTexture;
                settingsChanged = true;
            }

            // Check and apply NPOT scale
            if (!string.IsNullOrEmpty(settings.NPOTScale))
            {
                var newNPOTScale = ParseNPOTScale(settings.NPOTScale);
                if (importer.npotScale != newNPOTScale)
                {
                    importer.npotScale = newNPOTScale;
                    settingsChanged = true;
                }
            }

            // Check and apply max texture size
            if (settings.MaxTextureSize != null && importer.maxTextureSize != settings.MaxTextureSize)
            {
                importer.maxTextureSize = settings.MaxTextureSize;
                settingsChanged = true;
            }

            // Check and apply filter mode
            if (!string.IsNullOrEmpty(settings.FilterMode))
            {
                var newFilterMode = ParseFilterMode(settings.FilterMode);
                if (importer.filterMode != newFilterMode)
                {
                    importer.filterMode = newFilterMode;
                    settingsChanged = true;
                }
            }

            return settingsChanged;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Parse texture type string to Unity enum
        /// </summary>
        private static TextureImporterType ParseTextureType(string textureType)
        {
            if (string.IsNullOrEmpty(textureType)) return TextureImporterType.Default;
            
            return textureType.ToLower() switch
            {
                "default" => TextureImporterType.Default,
                "normalmap" => TextureImporterType.NormalMap,
                "sprite" => TextureImporterType.Sprite,
                "cursor" => TextureImporterType.Cursor,
                "cookie" => TextureImporterType.Cookie,
                "lightmap" => TextureImporterType.Lightmap,
                "singlechannel" => TextureImporterType.SingleChannel,
                _ => TextureImporterType.Default
            };
        }

        /// <summary>
        /// Parse texture compression string to Unity enum
        /// </summary>
        private static TextureImporterCompression ParseTextureCompression(string compression)
        {
            if (string.IsNullOrEmpty(compression)) return TextureImporterCompression.Uncompressed;
            
            return compression.ToLower() switch
            {
                "uncompressed" => TextureImporterCompression.Uncompressed,
                "compressed" => TextureImporterCompression.Compressed,
                "compressedhq" => TextureImporterCompression.CompressedHQ,
                "compressedlq" => TextureImporterCompression.CompressedLQ,
                _ => TextureImporterCompression.Uncompressed
            };
        }

        /// <summary>
        /// Parse wrap mode string to Unity enum
        /// </summary>
        private static TextureWrapMode ParseWrapMode(string wrapMode)
        {
            if (string.IsNullOrEmpty(wrapMode)) return TextureWrapMode.Repeat;
            
            return wrapMode.ToLower() switch
            {
                "repeat" => TextureWrapMode.Repeat,
                "clamp" => TextureWrapMode.Clamp,
                "mirror" => TextureWrapMode.Mirror,
                "mirroronce" => TextureWrapMode.MirrorOnce,
                _ => TextureWrapMode.Repeat
            };
        }

        /// <summary>
        /// Parse NPOT scale string to Unity enum
        /// </summary>
        private static TextureImporterNPOTScale ParseNPOTScale(string npotScale)
        {
            if (string.IsNullOrEmpty(npotScale)) return TextureImporterNPOTScale.ToNearest;
            
            return npotScale.ToLower() switch
            {
                "none" => TextureImporterNPOTScale.None,
                "tonearest" => TextureImporterNPOTScale.ToNearest,
                "tolarger" => TextureImporterNPOTScale.ToLarger,
                "tosmaller" => TextureImporterNPOTScale.ToSmaller,
                _ => TextureImporterNPOTScale.ToNearest
            };
        }

        /// <summary>
        /// Parse filter mode string to Unity enum
        /// </summary>
        private static FilterMode ParseFilterMode(string filterMode)
        {
            if (string.IsNullOrEmpty(filterMode)) return FilterMode.Bilinear;
            
            return filterMode.ToLower() switch
            {
                "point" => FilterMode.Point,
                "bilinear" => FilterMode.Bilinear,
                "trilinear" => FilterMode.Trilinear,
                _ => FilterMode.Bilinear
            };
        }

        #endregion

        #region Legacy API Support

        /// <summary>
        /// Legacy method for setting texture import settings without shader context
        /// </summary>
        /// <param name="paths">Texture file paths</param>
        public static void SetTextureImportSettings(IEnumerable<string> paths)
        {
            SetTextureImportSettings(paths, null);
        }

        #endregion

        #region Texture Analysis and Validation API

        /// <summary>
        /// Analyze multiple textures and get comprehensive optimization recommendations
        /// </summary>
        /// <param name="texturePaths">Paths to texture assets</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>List of texture analysis results</returns>
        public static List<HoyoToonTextureAnalysis> AnalyzeTextures(IEnumerable<string> texturePaths, string shaderKey = null)
        {
            var results = new List<HoyoToonTextureAnalysis>();

            foreach (var path in texturePaths)
            {
                try
                {
                    var analysis = HoyoToonDataManager.AnalyzeTexture(path, shaderKey);
                    results.Add(analysis);
                }
                catch (Exception e)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to analyze texture {path}: {e.Message}");
                    results.Add(new HoyoToonTextureAnalysis
                    {
                        TexturePath = path,
                        TextureName = Path.GetFileNameWithoutExtension(path),
                        IsValid = false,
                        Issues = new List<string> { $"Analysis failed: {e.Message}" }
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Get textures that need optimization with priority ordering
        /// </summary>
        /// <param name="texturePaths">Paths to texture assets</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>Textures needing optimization, ordered by priority</returns>
        public static List<HoyoToonTextureAnalysis> GetOptimizationCandidates(IEnumerable<string> texturePaths, string shaderKey = null)
        {
            var analyses = AnalyzeTextures(texturePaths, shaderKey);
            return analyses
                .Where(a => a.IsValid && a.NeedsOptimization)
                .OrderByDescending(a => a.OptimizationPriority)
                .ToList();
        }

        /// <summary>
        /// Apply HoyoToon-optimized settings to a texture based on analysis
        /// </summary>
        /// <param name="texturePath">Path to texture asset</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>True if optimization was applied successfully</returns>
        public static bool OptimizeTextureWithAnalysis(string texturePath, string shaderKey = null)
        {
            try
            {
                var analysis = HoyoToonDataManager.AnalyzeTexture(texturePath, shaderKey);
                if (!analysis.IsValid || !analysis.NeedsOptimization)
                {
                    HoyoToonLogs.LogDebug($"Texture {analysis.TextureName} does not need optimization");
                    return false;
                }

                // Apply recommended settings
                SetTextureImportSettings(new[] { texturePath }, shaderKey);
                
                HoyoToonLogs.LogDebug($"Successfully optimized texture: {analysis.TextureName}");
                return true;
            }
            catch (Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to optimize texture {texturePath}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Batch optimize multiple textures with comprehensive analysis
        /// </summary>
        /// <param name="texturePaths">Paths to texture assets</param>
        /// <param name="shaderKey">Optional shader key for shader-specific requirements</param>
        /// <returns>Optimization results summary</returns>
        public static HoyoToonTextureOptimizationResult BatchOptimizeTextures(IEnumerable<string> texturePaths, string shaderKey = null)
        {
            var result = new HoyoToonTextureOptimizationResult();
            var candidates = GetOptimizationCandidates(texturePaths, shaderKey);

            result.TotalTextures = texturePaths.Count();
            result.CandidatesForOptimization = candidates.Count;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var candidate in candidates)
                {
                    try
                    {
                        if (OptimizeTextureWithAnalysis(candidate.TexturePath, shaderKey))
                        {
                            result.OptimizedTextures.Add(candidate.TextureName);
                            result.EstimatedMemorySaved += ExtractMemorySavings(candidate.EstimatedSavings);
                        }
                        else
                        {
                            result.FailedTextures.Add($"{candidate.TextureName}: Optimization not applied");
                        }
                    }
                    catch (Exception e)
                    {
                        result.FailedTextures.Add($"{candidate.TextureName}: {e.Message}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return result;
        }

        /// <summary>
        /// Validate if a texture meets HoyoToon standards for a specific shader
        /// </summary>
        /// <param name="texturePath">Path to texture asset</param>
        /// <param name="shaderKey">Shader key for validation</param>
        /// <returns>Validation result with pass/fail and issues</returns>
        public static HoyoToonTextureValidationResult ValidateTextureForShader(string texturePath, string shaderKey)
        {
            var result = new HoyoToonTextureValidationResult
            {
                TexturePath = texturePath,
                TextureName = Path.GetFileNameWithoutExtension(texturePath),
                ShaderKey = shaderKey
            };

            try
            {
                var analysis = HoyoToonDataManager.AnalyzeTexture(texturePath, shaderKey);
                result.IsValid = analysis.IsValid;
                result.MeetsStandards = analysis.IsValid && !analysis.NeedsOptimization;
                result.Issues = analysis.Issues.Concat(analysis.Recommendations).ToList();
                result.Analysis = analysis;
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.MeetsStandards = false;
                result.Issues.Add($"Validation failed: {e.Message}");
            }

            return result;
        }

        private static long ExtractMemorySavings(string estimatedSavings)
        {
            try
            {
                if (estimatedSavings.Contains("MB"))
                {
                    var mbString = estimatedSavings.Replace("~", "").Replace("MB", "").Trim();
                    if (long.TryParse(mbString, out long mb))
                    {
                        return mb * 1024 * 1024;
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Result Classes

        /// <summary>
        /// Result of batch texture optimization operation
        /// </summary>
        public class HoyoToonTextureOptimizationResult
        {
            public int TotalTextures { get; set; }
            public int CandidatesForOptimization { get; set; }
            public List<string> OptimizedTextures { get; set; } = new List<string>();
            public List<string> FailedTextures { get; set; } = new List<string>();
            public long EstimatedMemorySaved { get; set; }

            public int SuccessCount => OptimizedTextures.Count;
            public int FailureCount => FailedTextures.Count;
            public string MemorySavedFormatted => EstimatedMemorySaved > 1024 * 1024 
                ? $"{EstimatedMemorySaved / (1024 * 1024)}MB" 
                : $"{EstimatedMemorySaved / 1024}KB";

            public string Summary => $"Optimized {SuccessCount}/{CandidatesForOptimization} textures. " +
                                   $"Estimated memory saved: {MemorySavedFormatted}. " +
                                   $"{FailureCount} failed.";
        }

        /// <summary>
        /// Result of texture validation for shader compliance
        /// </summary>
        public class HoyoToonTextureValidationResult
        {
            public string TexturePath { get; set; }
            public string TextureName { get; set; }
            public string ShaderKey { get; set; }
            public bool IsValid { get; set; }
            public bool MeetsStandards { get; set; }
            public List<string> Issues { get; set; } = new List<string>();
            public HoyoToonTextureAnalysis Analysis { get; set; }

            public string StatusMessage => MeetsStandards 
                ? "Meets HoyoToon standards" 
                : $"Needs optimization: {string.Join(", ", Issues.Take(3))}";
        }

        #endregion
    }
}
#endif