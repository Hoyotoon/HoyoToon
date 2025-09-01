#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;


namespace HoyoToon
{
    public static class HoyoToonParseManager
    {
        public enum BodyType
        {
            GIBoy,
            GIGirl,
            GILady,
            GIMale,
            GILoli,
            HSRMaid,
            HSRKid,
            HSRLad,
            HSRMale,
            HSRLady,
            HSRGirl,
            HSRBoy,
            HSRMiss,
            HI3P1,
            HI3P2,
            WuWa,
            ZZZ
        }
        public static BodyType currentBodyType;
        
        // Additional properties to track detected game and shader information
        public static string currentGameName = "WuWa";
        public static string currentShaderKey = "WuWaShader";

        /// <summary>
        /// Get comprehensive game detection information
        /// </summary>
        /// <returns>Tuple containing (GameName, ShaderKey, BodyType)</returns>
        public static (string GameName, string ShaderKey, BodyType BodyType) GetCurrentGameInfo()
        {
            return (currentGameName, currentShaderKey, currentBodyType);
        }

        /// <summary>
        /// Get the shader path for the currently detected game
        /// </summary>
        /// <returns>Shader path from HoyoToonDataManager</returns>
        public static string GetCurrentShaderPath()
        {
            try
            {
                switch (currentShaderKey)
                {
                    case "HSRShader":
                        return HoyoToonDataManager.HSRShader;
                    case "GIShader":
                        return HoyoToonDataManager.GIShader;
                    case "HI3Shader":
                        return HoyoToonDataManager.HI3Shader;
                    case "HI3P2Shader":
                        return HoyoToonDataManager.HI3P2Shader;
                    case "ZZZShader":
                        return HoyoToonDataManager.ZZZShader;
                    case "WuWaShader":
                    default:
                        return HoyoToonDataManager.WuWaShader;
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to get shader path for {currentShaderKey}: {e.Message}");
                return HoyoToonDataManager.WuWaShader; // Safe fallback
            }
        }

        /// <summary>
        /// Validate that the detected game information is consistent
        /// </summary>
        /// <returns>True if detection is valid and consistent</returns>
        public static bool ValidateDetection()
        {
            try
            {
                // Check if we have valid game information
                if (string.IsNullOrEmpty(currentGameName) || string.IsNullOrEmpty(currentShaderKey))
                {
                    HoyoToonLogs.WarningDebug("Invalid game detection: Missing game name or shader key");
                    return false;
                }

                // Check if shader path is available
                string shaderPath = GetCurrentShaderPath();
                if (string.IsNullOrEmpty(shaderPath))
                {
                    HoyoToonLogs.WarningDebug($"Invalid shader path for game {currentGameName}");
                    return false;
                }

                // Validate game-bodytype consistency
                bool isConsistent = ValidateGameBodyTypeConsistency();
                if (!isConsistent)
                {
                    HoyoToonLogs.WarningDebug($"Inconsistent game-bodytype combination: {currentGameName} - {currentBodyType}");
                    return false;
                }

                HoyoToonLogs.LogDebug($"Detection validation passed: {currentGameName}/{currentShaderKey}/{currentBodyType}");
                return true;
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error during detection validation: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the current body type is consistent with the detected game
        /// </summary>
        private static bool ValidateGameBodyTypeConsistency()
        {
            string bodyTypeString = currentBodyType.ToString();
            
            switch (currentGameName.ToUpperInvariant())
            {
                case "HSR":
                    return bodyTypeString.StartsWith("HSR");
                case "GI":
                    return bodyTypeString.StartsWith("GI");
                case "HI3P1":
                    return bodyTypeString == "HI3P1";
                case "HI3P2":
                    return bodyTypeString == "HI3P2";
                case "ZZZ":
                    return bodyTypeString == "ZZZ";
                case "WUWA":
                    return bodyTypeString == "WuWa";
                default:
                    return true; // Unknown games are considered valid
            }
        }

        #region Parsing

        public static string[] GetAssetSelectionPaths()
        {
            return Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        public static string GetPackagePath(string packageName)
        {
            ListRequest request = Client.List(true);
            while (!request.IsCompleted) { }

            if (request.Status == StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == packageName)
                    {
                        return package.resolvedPath;
                    }
                }
            }
            else if (request.Status >= StatusCode.Failure)
            {
                HoyoToonLogs.ErrorDebug(request.Error.message);
            }

            return null;
        }

        public static void DetermineBodyType()
        {
            GameObject selectedGameObject = Selection.activeGameObject;
            string selectedAssetPath = "";

            if (selectedGameObject != null)
            {
                Mesh mesh = FindMeshInGameObject(selectedGameObject);
                if (mesh != null)
                {
                    selectedAssetPath = AssetDatabase.GetAssetPath(mesh);
                }
                else
                {
                    HoyoToonLogs.WarningDebug("No mesh found in the selected GameObject or its children.");
                    return;
                }
            }
            else
            {
                selectedAssetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            }

            if (string.IsNullOrEmpty(selectedAssetPath))
            {
                HoyoToonLogs.WarningDebug("No valid asset selected.");
                return;
            }

            DetermineBodyTypeFromAssetPath(selectedAssetPath);
        }

        /// <summary>
        /// Determine body type for a specific GameObject model
        /// </summary>
        /// <param name="model">The GameObject model to analyze</param>
        public static void DetermineBodyType(GameObject model)
        {
            HoyoToonLogs.LogDebug($"=== Starting DetermineBodyType for GameObject ===");
            
            if (model == null)
            {
                HoyoToonLogs.WarningDebug("No model provided for body type determination.");
                return;
            }

            HoyoToonLogs.LogDebug($"Model name: {model.name}");
            HoyoToonLogs.LogDebug($"Model instance ID: {model.GetInstanceID()}");

            string selectedAssetPath = "";

            // Try to get asset path from the model itself first
            selectedAssetPath = AssetDatabase.GetAssetPath(model);
            HoyoToonLogs.LogDebug($"Direct asset path from model: '{selectedAssetPath}'");

            // If that fails, try to find a mesh in the model and get its path
            if (string.IsNullOrEmpty(selectedAssetPath))
            {
                HoyoToonLogs.LogDebug("Direct asset path empty, searching for mesh in GameObject...");
                Mesh mesh = FindMeshInGameObject(model);
                if (mesh != null)
                {
                    selectedAssetPath = AssetDatabase.GetAssetPath(mesh);
                    HoyoToonLogs.LogDebug($"Found mesh '{mesh.name}', asset path: '{selectedAssetPath}'");
                }
                else
                {
                    HoyoToonLogs.WarningDebug("No mesh found in GameObject hierarchy");
                }
            }

            if (string.IsNullOrEmpty(selectedAssetPath))
            {
                HoyoToonLogs.WarningDebug($"Could not determine asset path for model: {model.name}");
                return;
            }

            HoyoToonLogs.LogDebug($"Final asset path for analysis: '{selectedAssetPath}'");
            HoyoToonLogs.LogDebug($"=== Calling DetermineBodyTypeFromAssetPath ===");
            DetermineBodyTypeFromAssetPath(selectedAssetPath);
        }

        /// <summary>
        /// Common logic for determining body type from an asset path
        /// </summary>
        /// <param name="selectedAssetPath">Path to the asset</param>
        private static void DetermineBodyTypeFromAssetPath(string selectedAssetPath)
        {
            HoyoToonLogs.LogDebug($"Starting body type detection from asset path: {selectedAssetPath}");
            
            string directoryPath = Path.GetDirectoryName(selectedAssetPath);
            HoyoToonLogs.LogDebug($"Asset directory path: {directoryPath}");
            
            if (Path.GetExtension(selectedAssetPath) == ".json")
            {
                directoryPath = Directory.GetParent(directoryPath).FullName;
                HoyoToonLogs.LogDebug($"JSON detected, moved to parent directory: {directoryPath}");
            }

            string materialsPath = FindMaterialsFolder(directoryPath);
            HoyoToonLogs.LogDebug($"Materials folder search result: {materialsPath ?? "NOT FOUND"}");

            if (Directory.Exists(materialsPath))
            {
                HoyoToonLogs.LogDebug($"Found materials folder, analyzing JSON files...");
                DetermineBodyTypeFromJson(materialsPath);
            }
            else
            {
                HoyoToonLogs.WarningDebug($"Materials folder not found in directory: {directoryPath}");
                string validFolderNames = string.Join(", ", new[] { "Materials", "Material", "Mat" });
                EditorUtility.DisplayDialog("Error", $"Materials folder path does not exist. Ensure your materials are in a folder named {validFolderNames}.", "OK");
                HoyoToonLogs.ErrorDebug("You need to have a Materials folder matching the valid names (e.g., 'Materials', 'Material', 'Mat') and have all the materials inside of them.");
                currentBodyType = BodyType.WuWa;
            }

            HoyoToonLogs.LogDebug($"Final detected body type: {currentBodyType} (Game: {currentGameName}, Shader: {currentShaderKey})");
        }

        private static Mesh FindMeshInGameObject(GameObject obj)
        {
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return meshFilter.sharedMesh;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            // If not found in the current GameObject, search in children
            foreach (Transform child in obj.transform)
            {
                Mesh childMesh = FindMeshInGameObject(child.gameObject);
                if (childMesh != null)
                {
                    return childMesh;
                }
            }

            return null;
        }


        private static string FindMaterialsFolder(string startPath)
        {
            HoyoToonLogs.LogDebug($"Starting Materials folder search from: {startPath}");
            
            string[] validFolderNames = { "Materials", "Material", "Mat" };

            // Search in the current directory and up to 3 levels up
            for (int i = 0; i < 4; i++)
            {
                HoyoToonLogs.LogDebug($"Searching level {i} in directory: {startPath}");
                
                foreach (string folderName in validFolderNames)
                {
                    string path = Path.Combine(startPath, folderName);
                    HoyoToonLogs.LogDebug($"Checking path: {path}");
                    
                    if (Directory.Exists(path))
                    {
                        HoyoToonLogs.LogDebug($"Found Materials folder: {path}");
                        return path;
                    }
                }
                
                string parentPath = Directory.GetParent(startPath)?.FullName;
                if (parentPath == null)
                {
                    HoyoToonLogs.LogDebug("Reached root directory, stopping search");
                    break;
                }
                
                startPath = parentPath;
                HoyoToonLogs.LogDebug($"Moving up to parent directory: {startPath}");
            }

            HoyoToonLogs.WarningDebug("Materials folder not found after searching up to 4 levels");
            return null;
        }

        public static void DetermineBodyTypeFromJson(string jsonPath)
        {
            HoyoToonLogs.LogDebug($"Enhanced game/shader detection starting. Searching for JSON files in: {jsonPath}");
            
            // First detect the game (which determines the shader)
            var gameDetectionResult = DetectGameFromJson(jsonPath);
            
            if (gameDetectionResult != null)
            {
                var (detectedGame, detectedShader) = gameDetectionResult.Value;
                HoyoToonLogs.LogDebug($"Game detected: {detectedGame}, Shader: {detectedShader}");
                
                // Update the current game and shader information
                currentGameName = detectedGame;
                currentShaderKey = detectedShader;
                
                // Now detect body type based on the detected game
                DetectBodyTypeForGame(jsonPath, detectedGame);
            }
            else
            {
                HoyoToonLogs.LogDebug("No specific game detected, defaulting to WuWa");
                currentGameName = "WuWa";
                currentShaderKey = "WuWaShader";
                currentBodyType = BodyType.WuWa;
            }

            HoyoToonLogs.LogDebug($"Final determination - Game: {currentGameName}, Shader: {currentShaderKey}, BodyType: {currentBodyType}");
        }

        /// <summary>
        /// Detect the game type from JSON material files using the same approach as HoyoToonMaterialManager
        /// Returns tuple of (GameName, ShaderKey) or null if not detected
        /// </summary>
        private static (string GameName, string ShaderKey)? DetectGameFromJson(string jsonPath)
        {
            string[] jsonFiles = Directory.GetFiles(jsonPath, "*.json").Where(f => f.Contains("Face")).ToArray();
            HoyoToonLogs.LogDebug($"Found {jsonFiles.Length} Face JSON files for shader keyword detection");

            // Get shader keywords from API data
            var shaderKeywords = HoyoToonDataManager.Data.ShaderKeywords;
            if (shaderKeywords == null)
            {
                HoyoToonLogs.WarningDebug("No shader keywords data available from API");
                return null;
            }

            int totalKeywords = 0;
            foreach (var shaderGroup in shaderKeywords)
            {
                if (shaderGroup.Value != null)
                {
                    totalKeywords += shaderGroup.Value.Count();
                }
            }
            HoyoToonLogs.LogDebug($"Built keyword mapping with {totalKeywords} total keywords from API");

            foreach (string jsonFile in jsonFiles)
            {
                try
                {
                    HoyoToonLogs.LogDebug($"Analyzing file for shader keyword detection: {Path.GetFileName(jsonFile)}");
                    string jsonContent = File.ReadAllText(jsonFile);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    // Fast keyword detection - check each shader type until we find a match
                    foreach (var shaderType in shaderKeywords)
                    {
                        string shaderKey = shaderType.Key;
                        var keywords = shaderType.Value;

                        // Check if ANY keyword from this shader type exists
                        bool hasMatchingKeyword = false;
                        string foundKeyword = null;

                        foreach (var keyword in keywords)
                        {
                            bool hasKeyword = false;

                            // Check for shader keywords in Unity format
                            var properties = jsonObject["m_SavedProperties"];
                            if (properties != null && properties.Type == JTokenType.Object)
                            {
                                var texEnvs = properties["m_TexEnvs"];
                                var floats = properties["m_Floats"];
                                var colors = properties["m_Colors"];

                                // Check each property type with proper null and type validation
                                bool hasInTexEnvs = texEnvs != null && texEnvs.Type == JTokenType.Object && texEnvs[keyword] != null;
                                bool hasInFloats = floats != null && floats.Type == JTokenType.Object && floats[keyword] != null;
                                bool hasInColors = colors != null && colors.Type == JTokenType.Object && colors[keyword] != null;

                                hasKeyword = hasInTexEnvs || hasInFloats || hasInColors;
                            }

                            // Check for Unreal format
                            if (!hasKeyword)
                            {
                                // Special check for WuWa shader which uses ShadingModel
                                if (keyword == "ShadingModel")
                                {
                                    var parameters = jsonObject["Parameters"];
                                    if (parameters?.Type == JTokenType.Object && parameters["ShadingModel"] != null)
                                    {
                                        hasKeyword = true;
                                    }
                                }
                                else
                                {
                                    var textures = jsonObject["Textures"];
                                    var parameters = jsonObject["Parameters"];
                                    
                                    bool hasInTextures = textures?.Type == JTokenType.Object && textures[keyword] != null;
                                    bool hasInScalars = false;
                                    bool hasInSwitches = false;
                                    bool hasInProperties = false;

                                    if (parameters?.Type == JTokenType.Object)
                                    {
                                        var scalars = parameters["Scalars"];
                                        var switches = parameters["Switches"];
                                        var props = parameters["Properties"];

                                        hasInScalars = scalars?.Type == JTokenType.Object && scalars[keyword] != null;
                                        hasInSwitches = switches?.Type == JTokenType.Object && switches[keyword] != null;
                                        hasInProperties = props?.Type == JTokenType.Object && props[keyword] != null;
                                    }

                                    hasKeyword = hasInTextures || hasInScalars || hasInSwitches || hasInProperties;
                                }
                            }

                            if (hasKeyword)
                            {
                                hasMatchingKeyword = true;
                                foundKeyword = keyword;
                                HoyoToonLogs.LogDebug($"Found shader keyword '{keyword}' for shader '{shaderKey}'");
                                break; // Found a match, no need to check more keywords for this shader
                            }
                        }

                        if (hasMatchingKeyword)
                        {
                            // Special handling for Hi3 shaders
                            if (shaderKey == "HI3Shader" && shaderKeywords.ContainsKey("HI3P2Shader"))
                            {
                                // Check if this is actually HI3P2 based on P2-specific keywords
                                bool isPart2Shader = false;
                                foreach (var p2Keyword in shaderKeywords["HI3P2Shader"])
                                {
                                    var properties = jsonObject["m_SavedProperties"];
                                    if (properties?.Type == JTokenType.Object)
                                    {
                                        var texEnvs = properties["m_TexEnvs"];
                                        var floats = properties["m_Floats"];
                                        var colors = properties["m_Colors"];

                                        // Check each property type with proper null validation
                                        bool hasInTexEnvs = texEnvs != null && texEnvs.Type == JTokenType.Object && texEnvs[p2Keyword] != null;
                                        bool hasInFloats = floats != null && floats.Type == JTokenType.Object && floats[p2Keyword] != null;
                                        bool hasInColors = colors != null && colors.Type == JTokenType.Object && colors[p2Keyword] != null;

                                        if (hasInTexEnvs || hasInFloats || hasInColors)
                                        {
                                            isPart2Shader = true;
                                            HoyoToonLogs.LogDebug($"Detected HI3P2 shader via P2-specific keyword '{p2Keyword}'");
                                            break;
                                        }
                                    }
                                }

                                if (isPart2Shader)
                                {
                                    string gameName = ConvertShaderKeyToGameName("HI3P2Shader");
                                    HoyoToonLogs.LogDebug($"Shader detection result: Game='{gameName}', ShaderKey='HI3P2Shader'");
                                    return (gameName, "HI3P2Shader");
                                }
                                else
                                {
                                    string gameName = ConvertShaderKeyToGameName("HI3Shader");
                                    HoyoToonLogs.LogDebug($"Shader detection result: Game='{gameName}', ShaderKey='HI3Shader'");
                                    return (gameName, "HI3Shader");
                                }
                            }

                            // Convert shader key to game name
                            string gameNameResult = ConvertShaderKeyToGameName(shaderKey);
                            HoyoToonLogs.LogDebug($"Shader detection result: Game='{gameNameResult}', ShaderKey='{shaderKey}'");
                            return (gameNameResult, shaderKey);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    HoyoToonLogs.WarningDebug($"Failed to parse JSON file {jsonFile}: {e.Message}");
                    continue;
                }
            }

            HoyoToonLogs.LogDebug("No shader keywords matched - no specific game detected");
            return null;
        }

        /// <summary>
        /// Convert shader key to game name for consistency
        /// </summary>
        private static string ConvertShaderKeyToGameName(string shaderKey)
        {
            switch (shaderKey)
            {
                case "HSRShader": return "HSR";
                case "GIShader": return "GI";
                case "HI3Shader": return "HI3P1";
                case "HI3P2Shader": return "HI3P2";
                case "WuWaShader": return "WuWa";
                case "ZZZShader": return "ZZZ";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Detect body type for a specific game
        /// </summary>
        private static void DetectBodyTypeForGame(string jsonPath, string detectedGame)
        {
            HoyoToonLogs.LogDebug($"Detecting body type for game: {detectedGame}");
            
            string[] jsonFiles = Directory.GetFiles(jsonPath, "*.json").Where(f => f.Contains("Face")).ToArray();
            bool bodyTypeSet = false;

            foreach (string jsonFile in jsonFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonFile);
                    JObject jsonObject = JObject.Parse(jsonContent);

                    switch (detectedGame.ToUpperInvariant())
                    {
                        case "HSR":
                            if (TryGetTextureNameFromJson(jsonObject, "_FaceExpression", out string expressionMapName))
                            {
                                HoyoToonLogs.LogDebug($"HSR body type detection from: {expressionMapName}");
                                SetHSRBodyType(expressionMapName, ref bodyTypeSet);
                            }
                            break;

                        case "GI":
                            if (TryGetTextureNameFromJson(jsonObject, "_FaceMapTex", out string faceMapName))
                            {
                                HoyoToonLogs.LogDebug($"GI body type detection from: {faceMapName}");
                                SetGIBodyType(faceMapName, ref bodyTypeSet);
                            }
                            break;

                        case "HI3P1":
                            currentBodyType = BodyType.HI3P1;
                            bodyTypeSet = true;
                            break;

                        case "HI3P2":
                            currentBodyType = BodyType.HI3P2;
                            bodyTypeSet = true;
                            break;

                        case "ZZZ":
                            currentBodyType = BodyType.ZZZ;
                            bodyTypeSet = true;
                            break;

                        default:
                            HoyoToonLogs.LogDebug($"Unknown game for body type detection: {detectedGame}");
                            break;
                    }

                    if (bodyTypeSet) break;
                }
                catch (System.Exception e)
                {
                    HoyoToonLogs.WarningDebug($"Failed to parse JSON file for body type detection {jsonFile}: {e.Message}");
                    continue;
                }
            }

            if (!bodyTypeSet)
            {
                // Set default body type based on detected game
                switch (detectedGame.ToUpperInvariant())
                {
                    case "HSR":
                        currentBodyType = BodyType.HSRLady; // Default HSR body type
                        break;
                    case "GI":
                        currentBodyType = BodyType.GIGirl; // Default GI body type
                        break;
                    case "HI3P1":
                        currentBodyType = BodyType.HI3P1;
                        break;
                    case "HI3P2":
                        currentBodyType = BodyType.HI3P2;
                        break;
                    case "ZZZ":
                        currentBodyType = BodyType.ZZZ;
                        break;
                    default:
                        currentBodyType = BodyType.WuWa;
                        break;
                }
                HoyoToonLogs.LogDebug($"Set default body type for {detectedGame}: {currentBodyType}");
            }
        }

        private static bool TryGetTextureNameFromJson(JObject jsonObject, string key, out string textureName)
        {
            textureName = null;
            var texEnvs = jsonObject["m_SavedProperties"]?["m_TexEnvs"];
            if (texEnvs == null)
            {
                HoyoToonLogs.LogDebug("m_SavedProperties or m_TexEnvs not found in JSON");
                return false;
            }

            foreach (var prop in texEnvs.Children<JProperty>())
            {
                if (prop.Name == key)
                {
                    textureName = prop.Value["m_Texture"]?["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(textureName))
                    {
                        return true;
                    }
                }
            }

            HoyoToonLogs.LogDebug($"Texture name not found for key '{key}'");
            return false;
        }

        private static void SetHSRBodyType(string expressionMapName, ref bool bodyTypeSet)
        {
            if (expressionMapName.Contains("Maid")) { currentBodyType = BodyType.HSRMaid; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Lady")) { currentBodyType = BodyType.HSRLady; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Girl")) { currentBodyType = BodyType.HSRGirl; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Kid")) { currentBodyType = BodyType.HSRKid; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Lad")) { currentBodyType = BodyType.HSRLad; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Male")) { currentBodyType = BodyType.HSRMale; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Boy")) { currentBodyType = BodyType.HSRBoy; bodyTypeSet = true; }
            else if (expressionMapName.Contains("Miss")) { currentBodyType = BodyType.HSRMiss; bodyTypeSet = true; }
        }

        private static void SetGIBodyType(string faceMapName, ref bool bodyTypeSet)
        {
            if (faceMapName.Contains("Boy")) { currentBodyType = BodyType.GIBoy; bodyTypeSet = true; }
            else if (faceMapName.Contains("Girl")) { currentBodyType = BodyType.GIGirl; bodyTypeSet = true; }
            else if (faceMapName.Contains("Lady")) { currentBodyType = BodyType.GILady; bodyTypeSet = true; }
            else if (faceMapName.Contains("Male")) { currentBodyType = BodyType.GIMale; bodyTypeSet = true; }
            else if (faceMapName.Contains("Loli")) { currentBodyType = BodyType.GILoli; bodyTypeSet = true; }
            else
            {
                currentBodyType = BodyType.HI3P1;
                bodyTypeSet = true;
                HoyoToonLogs.LogDebug($"Matched texture: {faceMapName} with BodyType.Hi3P1");
            }
        }

        /// <summary>
        /// Test the enhanced detection system with the current material
        /// </summary>
        [MenuItem("Assets/HoyoToon/Debug/Test Enhanced Detection")]
        public static void TestEnhancedDetection()
        {
            GameObject selectedGameObject = Selection.activeGameObject;
            string selectedAssetPath = "";

            if (selectedGameObject != null)
            {
                Mesh mesh = FindMeshInGameObject(selectedGameObject);
                if (mesh != null)
                {
                    selectedAssetPath = AssetDatabase.GetAssetPath(mesh);
                }
                else
                {
                    HoyoToonLogs.WarningDebug("No mesh found in the selected GameObject or its children.");
                    return;
                }
            }
            else
            {
                selectedAssetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            }

            if (string.IsNullOrEmpty(selectedAssetPath))
            {
                HoyoToonLogs.WarningDebug("No valid asset selected.");
                return;
            }

            string directoryPath = Path.GetDirectoryName(selectedAssetPath);
            if (Path.GetExtension(selectedAssetPath) == ".json")
            {
                directoryPath = Directory.GetParent(directoryPath).FullName;
            }

            string materialsPath = FindMaterialsFolder(directoryPath);
            
            if (Directory.Exists(materialsPath))
            {
                HoyoToonLogs.LogDebug("=== TESTING ENHANCED DETECTION SYSTEM ===");
                
                // Test the enhanced detection
                var gameDetectionResult = DetectGameFromJson(materialsPath);
                
                if (gameDetectionResult != null)
                {
                    var (detectedGame, detectedShader) = gameDetectionResult.Value;
                    HoyoToonLogs.LogDebug($"✅ DETECTION SUCCESS: Game='{detectedGame}', Shader='{detectedShader}'");
                    
                    // Test validation
                    currentGameName = detectedGame;
                    currentShaderKey = detectedShader;
                    DetectBodyTypeForGame(materialsPath, detectedGame);
                    
                    bool isValid = ValidateDetection();
                    HoyoToonLogs.LogDebug($"✅ VALIDATION: {(isValid ? "PASSED" : "FAILED")}");
                    
                    HoyoToonLogs.LogDebug($"✅ FINAL STATE: Game='{currentGameName}', Shader='{currentShaderKey}', BodyType='{currentBodyType}'");
                }
                else
                {
                    HoyoToonLogs.LogDebug("❌ DETECTION FAILED: No game detected using enhanced system");
                }
                
                HoyoToonLogs.LogDebug("=== END TESTING ===");
            }
            else
            {
                HoyoToonLogs.ErrorDebug("Materials folder not found for testing");
            }
        }

        #endregion
    }
}
#endif
