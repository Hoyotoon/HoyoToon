using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace HoyoToon
{
    /// <summary>
    /// Comprehensive model analysis data for HoyoToon framework
    /// Contains all information needed for model validation and setup
    /// </summary>
    [System.Serializable]
    public class HoyoToonModelAnalysisData
    {
        #region Basic Validation

        public bool hasValidModel;
        public bool isHumanoidRig;
        public bool hasMaterials;
        public bool hasTextures;
        public bool hasCorrectShaders;

        #endregion

        #region Model Information

        public string modelName;
        public string modelPath;
        public int boneCount;
        public int humanoidBoneCount;
        public string rootBoneName;
        public string rigType;
        public int animationCount;
        public int materialCount;
        public long fileSize;
        public DateTime creationDate;

        // Mesh geometry information
        public int vertexCount;
        public int triangleCount;
        public int blendshapeCount;

        #endregion

        #region HoyoToon Specific Detection

        public HoyoToonParseManager.BodyType? potentialGameType;
        public string bodyType;
        public string detectedCharacter;
        public bool isHI3NewFace;
        public bool isPrenatlaNPrenodkrai; // For Genshin specific detection
        public bool isHoyo2VRCConverted;

        #endregion

        #region Import Settings

        public bool hasImportSettingsSet;
        public bool needsTangentGeneration;
        public bool gameRequiresTangents;
        public bool meshHasTangents;
        public bool canReimport;
        public bool isReadWriteEnabled;
        public string meshCompression = "Off";

        #endregion

        #region Shader and Materials

        public int shaderOverrideCount;
        public List<HoyoToonMaterialInfo> materials = new List<HoyoToonMaterialInfo>();
        public List<string> materialNames = new List<string>();
        public List<string> shaderNames = new List<string>();

        #endregion

        #region Texture Information

        public List<HoyoToonTextureInfo> textures = new List<HoyoToonTextureInfo>();
        public List<string> textureNames = new List<string>();
        public long totalTextureMemory; // In bytes

        #endregion

        #region Issues and Warnings

        public List<string> issues = new List<string>();
        public List<string> warnings = new List<string>();

        #endregion

        #region Computed Properties

        /// <summary>
        /// Whether the model meets all prerequisites for HoyoToon conversion
        /// </summary>
        public bool meetsPrerequisites => hasValidModel && isHoyo2VRCConverted;

        /// <summary>
        /// Whether the model is ready for HoyoToon conversion (no critical blockers)
        /// </summary>
        public bool readyForSetup => hasValidModel && !hasCriticalIssues;

        /// <summary>
        /// Whether there are any critical issues that prevent conversion
        /// </summary>
        public bool hasCriticalIssues => issues.Any(i => i.Contains("CRITICAL") || i.Contains("BLOCKING"));

        /// <summary>
        /// List of missing prerequisites with descriptions
        /// </summary>
        public List<string> missingPrerequisites
        {
            get
            {
                var missing = new List<string>();
                if (!hasValidModel)
                    missing.Add("Valid FBX model required");
                if (!isHoyo2VRCConverted)
                    missing.Add("Model must be processed by Hoyo2VRC first (Bip bone naming not detected)");
                return missing;
            }
        }

        /// <summary>
        /// Count of preparation steps needed
        /// </summary>
        public int preparationStepsNeeded
        {
            get
            {
                int steps = 0;
                if (!hasValidModel) steps++;
                if (!isHumanoidRig) steps++;
                if (!hasMaterials) steps++;
                if (!hasTextures) steps++;
                if (!hasCorrectShaders) steps++;
                return steps;
            }
        }

        /// <summary>
        /// Preparation progress percentage (0-100)
        /// </summary>
        public int preparationProgress
        {
            get
            {
                // If not converted by Hoyo2VRC, progress should be 0 as it's the fundamental first step
                if (!isHoyo2VRCConverted)
                    return 0;

                int totalSteps = 5; // Model, Rig, Materials, Textures, Shaders
                int completedSteps = totalSteps - preparationStepsNeeded;
                return Mathf.Clamp((completedSteps * 100) / totalSteps, 0, 100);
            }
        }

        /// <summary>
        /// Get formatted file size string
        /// </summary>
        public string fileSizeFormatted
        {
            get
            {
                if (fileSize < 1024) return $"{fileSize} B";
                if (fileSize < 1024 * 1024) return $"{fileSize / 1024f:F1} KB";
                if (fileSize < 1024 * 1024 * 1024) return $"{fileSize / (1024f * 1024f):F1} MB";
                return $"{fileSize / (1024f * 1024f * 1024f):F2} GB";
            }
        }

        /// <summary>
        /// Get formatted texture memory string
        /// </summary>
        public string textureMemoryFormatted
        {
            get
            {
                if (totalTextureMemory < 1024 * 1024) return $"{totalTextureMemory / 1024f:F1} KB";
                if (totalTextureMemory < 1024 * 1024 * 1024) return $"{totalTextureMemory / (1024f * 1024f):F1} MB";
                return $"{totalTextureMemory / (1024f * 1024f * 1024f):F2} GB";
            }
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Add an issue to the analysis
        /// </summary>
        public void AddIssue(string issue)
        {
            if (!issues.Contains(issue))
                issues.Add(issue);
        }

        /// <summary>
        /// Add a warning to the analysis
        /// </summary>
        public void AddWarning(string warning)
        {
            if (!warnings.Contains(warning))
                warnings.Add(warning);
        }

        /// <summary>
        /// Clear all issues and warnings
        /// </summary>
        public void ClearIssuesAndWarnings()
        {
            issues.Clear();
            warnings.Clear();
        }

        /// <summary>
        /// Get summary text for the analysis focused on preparation workflow
        /// </summary>
        public string GetSummaryText()
        {
            if (!hasValidModel)
                return "No valid Hoyo2VRC model selected";

            if (!isHoyo2VRCConverted)
                return "Model must be converted with Hoyo2VRC first (0% complete)";

            if (readyForSetup)
                return $"Model ready for HoyoToon conversion ({preparationProgress}% prepared)";

            if (hasCriticalIssues)
                return $"Critical issues must be resolved before conversion";

            int stepsNeeded = preparationStepsNeeded;
            return stepsNeeded == 1 ?
                "1 preparation step remaining" :
                $"{stepsNeeded} preparation steps remaining";
        }

        #endregion
    }

    /// <summary>
    /// Material information for HoyoToon analysis
    /// </summary>
    [System.Serializable]
    public class HoyoToonMaterialInfo
    {
        public string name;
        public string materialPath;
        public bool isValid;
        public string invalidReason;
        public string currentShader;
        public string suggestedShader;
        public string shaderOverride;
        public string materialType; // Base, Face, Hair, etc.
        public int textureCount;
        public long memoryUsage; // In bytes
        public List<string> missingTextures = new List<string>();
        public List<string> availableShaderOverrides = new List<string>();
        public Dictionary<string, object> shaderProperties = new Dictionary<string, object>();

        /// <summary>
        /// Whether this material needs shader update
        /// </summary>
        public bool needsShaderUpdate => currentShader != suggestedShader && !string.IsNullOrEmpty(suggestedShader);

        /// <summary>
        /// Get formatted memory usage
        /// </summary>
        public string memoryUsageFormatted
        {
            get
            {
                if (memoryUsage < 1024 * 1024) return $"{memoryUsage / 1024f:F1} KB";
                return $"{memoryUsage / (1024f * 1024f):F1} MB";
            }
        }
    }

    /// <summary>
    /// Texture information for HoyoToon analysis
    /// </summary>
    [System.Serializable]
    public class HoyoToonTextureInfo
    {
        public string name;
        public string path;
        public TextureImporterType importerType;
        public TextureImporterFormat format;
        public int maxSize;
        public int width;
        public int height;
        public bool isReadable;
        public bool hasMipmaps;
        public bool isCompressed;
        public Texture2D preview;
        public long memorySize; // In bytes
        public string textureType; // Diffuse, Normal, AO, etc.

        /// <summary>
        /// Get formatted memory size
        /// </summary>
        public string memorySizeFormatted
        {
            get
            {
                if (memorySize < 1024 * 1024) return $"{memorySize / 1024f:F1} KB";
                return $"{memorySize / (1024f * 1024f):F1} MB";
            }
        }

        /// <summary>
        /// Get texture resolution string
        /// </summary>
        public string resolutionString => $"{width}x{height}";

        /// <summary>
        /// Whether this texture has optimal settings for HoyoToon
        /// </summary>
        public bool hasOptimalSettings
        {
            get
            {
                // Basic checks for optimal texture settings
                return isCompressed &&
                       !isReadable &&
                       hasMipmaps &&
                       maxSize <= 2048;
            }
        }
    }

    /// <summary>
    /// UI settings for HoyoToon Manager
    /// </summary>
    [System.Serializable]
    public class HoyoToonUISettings
    {
        public bool autoSaveSettings = true;
        public bool useBuiltInToneMapping = true;
        public bool useSelfShadows = false;
        public float outlineWidth = 1.0f;
        public float rimWidth = 1.0f;
        public string preferredGameType = "Auto";
        public string preferredBodyType = "Auto";
        public bool showMemoryUsage = true;
        public bool autoDetectCharacter = true;
        public string customCharacterOverride = "";
        public bool expertModeOverride = false;
        public bool showDetailedAnalysis = false;
        public bool enablePreview = true;
        public bool autoRefreshOnSelect = true;
        public int previewQuality = 1; // 0=Low, 1=Medium, 2=High

        private const string PREFS_PREFIX = "HoyoToonUI_";

        /// <summary>
        /// Load settings from EditorPrefs
        /// </summary>
        public void LoadFromEditorPrefs()
        {
            autoSaveSettings = EditorPrefs.GetBool(PREFS_PREFIX + "autoSaveSettings", autoSaveSettings);
            useBuiltInToneMapping = EditorPrefs.GetBool(PREFS_PREFIX + "useBuiltInToneMapping", useBuiltInToneMapping);
            useSelfShadows = EditorPrefs.GetBool(PREFS_PREFIX + "useSelfShadows", useSelfShadows);
            outlineWidth = EditorPrefs.GetFloat(PREFS_PREFIX + "outlineWidth", outlineWidth);
            rimWidth = EditorPrefs.GetFloat(PREFS_PREFIX + "rimWidth", rimWidth);
            preferredGameType = EditorPrefs.GetString(PREFS_PREFIX + "preferredGameType", preferredGameType);
            preferredBodyType = EditorPrefs.GetString(PREFS_PREFIX + "preferredBodyType", preferredBodyType);
            showMemoryUsage = EditorPrefs.GetBool(PREFS_PREFIX + "showMemoryUsage", showMemoryUsage);
            autoDetectCharacter = EditorPrefs.GetBool(PREFS_PREFIX + "autoDetectCharacter", autoDetectCharacter);
            customCharacterOverride = EditorPrefs.GetString(PREFS_PREFIX + "customCharacterOverride", customCharacterOverride);
            expertModeOverride = EditorPrefs.GetBool(PREFS_PREFIX + "expertModeOverride", expertModeOverride);
            showDetailedAnalysis = EditorPrefs.GetBool(PREFS_PREFIX + "showDetailedAnalysis", showDetailedAnalysis);
            enablePreview = EditorPrefs.GetBool(PREFS_PREFIX + "enablePreview", enablePreview);
            autoRefreshOnSelect = EditorPrefs.GetBool(PREFS_PREFIX + "autoRefreshOnSelect", autoRefreshOnSelect);
            previewQuality = EditorPrefs.GetInt(PREFS_PREFIX + "previewQuality", previewQuality);
        }

        /// <summary>
        /// Save settings to EditorPrefs
        /// </summary>
        public void SaveToEditorPrefs()
        {
            if (!autoSaveSettings) return;

            EditorPrefs.SetBool(PREFS_PREFIX + "autoSaveSettings", autoSaveSettings);
            EditorPrefs.SetBool(PREFS_PREFIX + "useBuiltInToneMapping", useBuiltInToneMapping);
            EditorPrefs.SetBool(PREFS_PREFIX + "useSelfShadows", useSelfShadows);
            EditorPrefs.SetFloat(PREFS_PREFIX + "outlineWidth", outlineWidth);
            EditorPrefs.SetFloat(PREFS_PREFIX + "rimWidth", rimWidth);
            EditorPrefs.SetString(PREFS_PREFIX + "preferredGameType", preferredGameType);
            EditorPrefs.SetString(PREFS_PREFIX + "preferredBodyType", preferredBodyType);
            EditorPrefs.SetBool(PREFS_PREFIX + "showMemoryUsage", showMemoryUsage);
            EditorPrefs.SetBool(PREFS_PREFIX + "autoDetectCharacter", autoDetectCharacter);
            EditorPrefs.SetString(PREFS_PREFIX + "customCharacterOverride", customCharacterOverride);
            EditorPrefs.SetBool(PREFS_PREFIX + "expertModeOverride", expertModeOverride);
            EditorPrefs.SetBool(PREFS_PREFIX + "showDetailedAnalysis", showDetailedAnalysis);
            EditorPrefs.SetBool(PREFS_PREFIX + "enablePreview", enablePreview);
            EditorPrefs.SetBool(PREFS_PREFIX + "autoRefreshOnSelect", autoRefreshOnSelect);
            EditorPrefs.SetInt(PREFS_PREFIX + "previewQuality", previewQuality);
        }
    }

    /// <summary>
    /// Banner data for dynamic UI banner system
    /// </summary>
    [System.Serializable]
    public class HoyoToonBannerData
    {
        public string backgroundPath = "UI/background";
        public string logoPath = "UI/hoyotoon";
        public string characterLeftPath = "";
        public string characterRightPath = "";
        public string detectedGame = "Auto";
        public string detectedCharacter = "Wise"; // Default to Wise

        public Texture2D backgroundTexture;
        public Texture2D logoTexture;
        public Texture2D characterLeftTexture;
        public Texture2D characterRightTexture;

        /// <summary>
        /// Update banner data based on game/character detection
        /// </summary>
        public void UpdateForGameAndCharacter(string game, string character)
        {
            detectedGame = game ?? "Auto";
            detectedCharacter = character ?? "Wise";

            // Update paths based on detection - use actual file structure
            string gamePrefix = GetGameFilePrefix(detectedGame);
            if (!string.IsNullOrEmpty(gamePrefix))
            {
                characterLeftPath = $"UI/{gamePrefix}l";
                characterRightPath = $"UI/{gamePrefix}r";
                logoPath = $"UI/{gamePrefix}logo";
            }
            else
            {
                // Default to HoyoToon branding
                characterLeftPath = "";
                characterRightPath = "";
                logoPath = "UI/hoyotoon";
            }
        }

        private string GetGameFilePrefix(string detectedGame)
        {
            if (string.IsNullOrEmpty(detectedGame) || detectedGame == "Auto")
                return "";

            switch (detectedGame.ToLower())
            {
                // Genshin Impact
                case "genshin":
                case "gi":
                case "genshinimpact":
                    return "gi";

                // Honkai Impact 3rd Part 1
                case "honkai":
                case "hi3":
                case "honkaiimpact":
                case "hi3p1":
                case "honkaiimpactpart1":
                    return "hi3p1";

                // Honkai Impact 3rd Part 2
                case "hi3p2":
                case "honkaiimpactpart2":
                case "honkaiimpactp2":
                    return "hi3p2";

                // Honkai Star Rail
                case "starrail":
                case "hsr":
                case "honkaistarrail":
                    return "hsr";

                // Wuthering Waves
                case "wuthering":
                case "wuwa":
                case "wutheringwaves":
                    return "wuwa";

                // Zenless Zone Zero
                case "zenless":
                case "zzz":
                case "zenlesszonezero":
                    return "zzz";

                default:
                    return "";
            }
        }

        /// <summary>
        /// Load all textures from Resources
        /// </summary>
        public void LoadTextures()
        {
            backgroundTexture = Resources.Load<Texture2D>(backgroundPath);
            logoTexture = Resources.Load<Texture2D>(logoPath);

            if (!string.IsNullOrEmpty(characterLeftPath))
                characterLeftTexture = Resources.Load<Texture2D>(characterLeftPath);

            if (!string.IsNullOrEmpty(characterRightPath))
                characterRightTexture = Resources.Load<Texture2D>(characterRightPath);
        }
    }
}