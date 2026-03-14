#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;
using HoyoToon.Editor.AssetPipeline.Textures;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    public static class MaterialGeneration
    {

        public sealed class Result
        {
            public bool Ok { get; set; }
            public string GameKey { get; set; }
            public string ShaderPath { get; set; }
            public Shader Shader { get; set; }
            public string SourceJson { get; set; }
            public Material Material { get; set; }
            public string MaterialAssetPath { get; set; }
            public string Error { get; set; }
        }

        private sealed class PreparedGeneration
        {
            public string GameKey;
            public string ShaderPath;
            public Shader Shader;
            public string SourceJson;
            public string JsonPayload;
            public MaterialJsonStructure MaterialData;
            public GameMetadata Metadata;
            public string Error;
        }

        

        public static Result GenerateMaterialAuto(UnityEngine.Object assetOrNull = null, string pathOrJson = null, string outputDir = null, string materialName = null)
        {
            var prepared = PrepareGeneration(assetOrNull, pathOrJson, null, null, null);
            return GenerateFromPrepared(prepared, outputDir, materialName, null, false);
        }

        private static PreparedGeneration PrepareGeneration(
            UnityEngine.Object assetOrNull,
            string pathOrJson,
            string knownGame,
            string knownShaderPath,
            string knownSourceJson)
        {
            var prepared = new PreparedGeneration
            {
                GameKey = knownGame,
                ShaderPath = knownShaderPath,
                SourceJson = knownSourceJson
            };

            if (string.IsNullOrWhiteSpace(prepared.SourceJson) || string.IsNullOrWhiteSpace(prepared.GameKey) || string.IsNullOrWhiteSpace(prepared.ShaderPath))
            {
                var detected = MaterialDetection.DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson);
                if (string.IsNullOrWhiteSpace(prepared.GameKey)) prepared.GameKey = detected.gameKey;
                if (string.IsNullOrWhiteSpace(prepared.ShaderPath)) prepared.ShaderPath = detected.shaderPath;
                if (string.IsNullOrWhiteSpace(prepared.SourceJson)) prepared.SourceJson = detected.sourceJson;
            }

            if (!TryResolveJsonPayload(pathOrJson, prepared.SourceJson, out prepared.JsonPayload, out var jsonError))
            {
                prepared.Error = jsonError;
                return prepared;
            }

            if (!Api.Parser.TryParse<MaterialJsonStructure>(prepared.JsonPayload, out var parsed, out var parseError) || parsed == null)
            {
                prepared.Error = $"MaterialGeneration: Parse failed: {parseError}";
                return prepared;
            }

            if (string.IsNullOrWhiteSpace(prepared.GameKey) || string.IsNullOrWhiteSpace(prepared.ShaderPath))
            {
                if (!MaterialDetection.TryDetectFromParsed(parsed, out var detectedGame, out var detectedShader, out _, out var reason))
                {
                    prepared.Error = $"MaterialGeneration: Unsupported material JSON ({reason}).";
                    return prepared;
                }

                if (string.IsNullOrWhiteSpace(prepared.GameKey)) prepared.GameKey = detectedGame;
                if (string.IsNullOrWhiteSpace(prepared.ShaderPath)) prepared.ShaderPath = detectedShader;
            }

            prepared.Shader = Shader.Find(prepared.ShaderPath);
            if (prepared.Shader == null)
            {
                prepared.Error = $"MaterialGeneration: Shader '{prepared.ShaderPath}' not found in project.";
                return prepared;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(prepared.GameKey, out var meta) || meta == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Warning, $"MaterialGeneration: No metadata configured for game '{prepared.GameKey}'. Using direct property names only.");
                meta = new GameMetadata();
            }

            prepared.Metadata = meta;
            prepared.MaterialData = MaterialConversion.ApplyPropertyConversions(parsed, meta);
            return prepared;
        }

        private static bool TryResolveJsonPayload(string pathOrJson, string sourceJson, out string jsonPayload, out string error)
        {
            jsonPayload = null;
            error = null;

            if (!string.IsNullOrWhiteSpace(pathOrJson) && EditorUtil.LooksLikeJson(pathOrJson))
            {
                jsonPayload = pathOrJson;
                return true;
            }

            if (!string.IsNullOrEmpty(sourceJson) && !string.Equals(sourceJson, "<raw-json>", StringComparison.Ordinal) && File.Exists(sourceJson))
            {
                try
                {
                    jsonPayload = File.ReadAllText(sourceJson);
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"MaterialGeneration: Failed reading JSON file '{sourceJson}': {ex.Message}";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(pathOrJson) && File.Exists(pathOrJson))
            {
                try
                {
                    jsonPayload = File.ReadAllText(pathOrJson);
                    return true;
                }
                catch (Exception ex)
                {
                    error = $"MaterialGeneration: Failed reading JSON file '{pathOrJson}': {ex.Message}";
                    return false;
                }
            }

            error = "MaterialGeneration: No JSON payload to parse.";
            return false;
        }

        private static Result GenerateFromPrepared(
            PreparedGeneration prepared,
            string outputDir,
            string materialName,
            TextureAssigner.TextureLookupCache textureCache,
            bool deferSaveAndRefresh,
            bool skipTextureImportRules = false)
        {
            var result = new Result
            {
                GameKey = prepared?.GameKey,
                ShaderPath = prepared?.ShaderPath,
                Shader = prepared?.Shader,
                SourceJson = prepared?.SourceJson
            };

            if (prepared == null)
            {
                result.Error = "MaterialGeneration: No prepared generation context.";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Error, result.Error);
                return result;
            }

            if (!string.IsNullOrWhiteSpace(prepared.Error))
            {
                result.Error = prepared.Error;
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Error, result.Error);
                return result;
            }

            string outDir = ComputeOutputDirectory(outputDir, prepared.SourceJson);
            EditorUtil.EnsureDirectory(outDir);

            string matName = !string.IsNullOrWhiteSpace(materialName)
                ? EditorUtil.SanitizeFileName(materialName)
                : DeriveMaterialName(prepared.SourceJson, prepared.ShaderPath, prepared.MaterialData);

            string assetPath = EditorUtil.AbsoluteToUnityPath(Path.Combine(outDir, matName + ".mat"));
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(prepared.Shader) { name = matName };
                try
                {
                    AssetDatabase.CreateAsset(mat, assetPath);
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Warning, $"CreateAsset at '{assetPath}' failed: {ex.Message}. Falling back to 'Assets/HoyoToon/GeneratedMaterials'.");
                    var assetsFallbackDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
                    EditorUtil.EnsureDirectory(assetsFallbackDir);
                    var fbAssetPath = EditorUtil.AbsoluteToUnityPath(Path.Combine(assetsFallbackDir, matName + ".mat"));
                    AssetDatabase.CreateAsset(mat, fbAssetPath);
                    assetPath = fbAssetPath;
                }
            }
            else
            {
                ResetMaterialToShaderDefaults(mat, prepared.Shader, matName);
            }

            try
            {
                ApplyUnityFormat(prepared.MaterialData, mat, prepared.Metadata, textureCache);

                MaterialOverrides.Apply(mat, prepared.Metadata);
                TextureMapping.ApplyMappings(mat, prepared.Metadata, textureCache);
                if (!skipTextureImportRules)
                    ApplyTextureImportRulesForMaterialTextures(mat, prepared.GameKey);
            }
            catch (Exception ex)
            {
                result.Error = $"MaterialGeneration: Exception while applying properties: {ex.Message}";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Error, result.Error);
                return result;
            }

            EditorUtility.SetDirty(mat);
            if (!deferSaveAndRefresh)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            result.Ok = true;
            result.Material = mat;
            result.MaterialAssetPath = assetPath;
            if (!deferSaveAndRefresh)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Info, $"Generated material '{matName}' at '{assetPath}' using shader '{prepared.ShaderPath}' for game '{prepared.GameKey}'.");
            }
            return result;
        }


        public static void GenerateAuto(object contextOrSelection = null, string pathOrJson = null, string outputDir = null, string materialName = null, bool showFailureWindow = true)
        {
            if (contextOrSelection is IEnumerable<UnityEngine.Object> many)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allDetections = new List<(string gameKey, string shaderPath, string sourceJson)>();
                string firstGame = null;
                foreach (var obj in many)
                {
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        var abs = EditorUtil.ToAbsolutePath(assetPath);
                        if (MaterialDetection.TryDetectFromJsonFile(abs, out var g, out var s, out _, out _))
                        {
                            if (seen.Add(abs)) allDetections.Add((g, s, abs));
                            firstGame ??= g;
                        }
                    }
                    else
                    {
                        // Non-JSON selection: fall back to context scan
                        var dets = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(obj, null) ?? Array.Empty<(string, string, string)>();
                        foreach (var d in dets)
                        {
                            if (string.IsNullOrEmpty(d.sourceJson) || d.sourceJson == "<raw-json>") continue;
                            if (seen.Add(d.sourceJson)) allDetections.Add(d);
                            firstGame ??= d.gameKey;
                        }
                    }
                }

                if (allDetections.Count > 0)
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"Detection: Found {allDetections.Count} material(s) for game '{firstGame}' from {seen.Count} selected file(s).");

                if (!string.IsNullOrEmpty(firstGame) && allDetections.Count > 0)
                {
                    var firstSrc = allDetections[0].sourceJson;
                    var charName = MaterialDetection.TryExtractCharacterName(firstGame, firstSrc);
                    if (!string.IsNullOrEmpty(charName))
                    {
                        var entry = MaterialDetection.FindProblemEntry(firstGame, charName);
                        if (entry != null && !string.IsNullOrEmpty(entry.Message))
                        {
                            bool proceed = MaterialDetection.PromptBatchProceed(firstGame, charName, entry);
                            if (!proceed)
                            {
                                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"Batch generation cancelled by user: Game='{firstGame}', Character='{charName}'");
                                return;
                            }
                        }
                    }
                }

                RunGenerationFromDetections(
                    allDetections,
                    contextForRawJson: null,
                    pathOrJsonForRaw: null,
                    outputDir,
                    materialNameForRaw: null,
                    showFailureWindow,
                    failureWindowContext: null,
                    failureWindowPath: "<multiple selections>",
                    allowFallbackWhenNoDetections: false);

                return;
            }

            var singleContext = contextOrSelection as UnityEngine.Object;
            var detections = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(singleContext, pathOrJson) ?? Array.Empty<(string gameKey, string shaderPath, string sourceJson)>();
            RunGenerationFromDetections(
                detections,
                contextForRawJson: singleContext,
                pathOrJsonForRaw: pathOrJson,
                outputDir,
                materialNameForRaw: materialName,
                showFailureWindow,
                failureWindowContext: singleContext,
                failureWindowPath: pathOrJson,
                allowFallbackWhenNoDetections: true);
        }

        private static void RunGenerationFromDetections(
            IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> detections,
            UnityEngine.Object contextForRawJson,
            string pathOrJsonForRaw,
            string outputDir,
            string materialNameForRaw,
            bool showFailureWindow,
            UnityEngine.Object failureWindowContext,
            string failureWindowPath,
            bool allowFallbackWhenNoDetections)
        {
            if (detections == null || detections.Count == 0)
            {
                if (allowFallbackWhenNoDetections)
                {
                    HandleNoDetectionFallback(contextForRawJson, pathOrJsonForRaw, outputDir, materialNameForRaw, showFailureWindow, failureWindowContext, failureWindowPath);
                }
                return;
            }

            var failures = new List<Result>();
            var textureCache = TextureAssigner.CreateLookupCache();
            GenerateFromDetections(
                detections,
                contextForRawJson,
                pathOrJsonForRaw,
                outputDir,
                materialNameForRaw,
                textureCache,
                failures,
                out var hasSuccess,
                out var successCount,
                out var successResults);

            if (hasSuccess)
            {
                AssetDatabase.SaveAssets();
            }

            int texEvaluated = 0;
            int texChanged = 0;
            foreach (var res in successResults)
            {
                if (res?.Material == null) continue;
                var (ev, ch) = ApplyTextureImportRulesForMaterialTextures(res.Material, res.GameKey);
                texEvaluated += ev;
                texChanged += ch;
            }
            if (texEvaluated > 0)
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture import rules: Evaluated {texEvaluated} textures across {successCount} material(s), {texChanged} updated.");

            if (hasSuccess)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (showFailureWindow && failures.Count > 0)
            {
                ShowFailuresSummaryWindow(failures, failureWindowContext, failureWindowPath);
            }
        }

        private static void HandleNoDetectionFallback(
            UnityEngine.Object contextForRawJson,
            string pathOrJsonForRaw,
            string outputDir,
            string materialNameForRaw,
            bool showFailureWindow,
            UnityEngine.Object failureWindowContext,
            string failureWindowPath)
        {
            if (string.IsNullOrWhiteSpace(pathOrJsonForRaw) && contextForRawJson == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Info, "MaterialGeneration: No input/context provided; nothing to generate.");
                return;
            }

            var result = GenerateMaterialAuto(contextForRawJson, pathOrJsonForRaw, outputDir, materialNameForRaw);
            if (showFailureWindow && (result == null || !result.Ok))
            {
                ShowFailuresSummaryWindow(new[] { result }, failureWindowContext, failureWindowPath);
            }
        }

        public static int ClearMaterialsFromContext(UnityEngine.Object assetOrNull = null, string pathOrFolder = null)
        {
            int deleted = ClearMaterialsInternal(assetOrNull, pathOrFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }

        public static int ClearMaterialsFromContexts(IEnumerable<UnityEngine.Object> assetsOrNull)
        {
            int total = 0;
            foreach (var obj in assetsOrNull ?? Enumerable.Empty<UnityEngine.Object>())
            {
                total += ClearMaterialsInternal(obj, null);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return total;
        }

        private static int ClearMaterialsInternal(UnityEngine.Object assetOrNull, string pathOrFolder)
        {
            int deleted = 0;
            var detections = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(assetOrNull, pathOrFolder) ?? Array.Empty<(string, string, string)>();
            foreach (var (_, shaderPath, src) in detections)
            {
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
                var outDir = ComputeOutputDirectory(null, src);

                // Parse JSON to derive material name consistently with generation
                MaterialJsonStructure parsed = null;
                try
                {
                    var json = File.ReadAllText(src);
                    Api.Parser.TryParse<MaterialJsonStructure>(json, out parsed, out _);
                }
                catch { }

                var matName = DeriveMaterialName(src, shaderPath, parsed);
                var candidate = EditorUtil.AbsoluteToUnityPath(Path.Combine(outDir, matName + ".mat"));
                if (AssetDatabase.LoadAssetAtPath<Material>(candidate) != null)
                {
                    if (AssetDatabase.DeleteAsset(candidate)) deleted++;
                }
                else
                {
                    var fbDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
                    var fbPath = EditorUtil.AbsoluteToUnityPath(Path.Combine(fbDir, matName + ".mat"));
                    if (AssetDatabase.LoadAssetAtPath<Material>(fbPath) != null)
                    {
                        if (AssetDatabase.DeleteAsset(fbPath)) deleted++;
                    }
                }
            }
            return deleted;
        }

        private static void ShowFailuresSummaryWindow(IReadOnlyList<Result> failures, UnityEngine.Object assetOrNull, string pathOrJson)
        {
            if (failures == null || failures.Count == 0) return;

            string contextLabel = !string.IsNullOrWhiteSpace(pathOrJson)
                ? pathOrJson
                : (assetOrNull != null ? AssetDatabase.GetAssetPath(assetOrNull) : "<none>");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Material Generation Failures");
            sb.AppendLine();
            sb.AppendLine($"- Context: `{contextLabel}`");
            sb.AppendLine($"- Failed: **{failures.Count}**");
            sb.AppendLine();

            foreach (var r in failures)
            {
                if (r == null) { sb.AppendLine("- <unknown>  - Error: <no details>"); continue; }
                string jsonName = !string.IsNullOrEmpty(r.SourceJson) ? Path.GetFileName(r.SourceJson) : "<unknown>";
                string reason = string.IsNullOrEmpty(r.Error) ? "Unknown error" : r.Error;
                sb.AppendLine($"- {jsonName}  (Game: `{r.GameKey ?? "?"}`, Shader: `{r.ShaderPath ?? r.Shader?.name ?? "?"}`)\n  - Error: {reason}");
            }

            var topBar = BaseHoyoToonWindow.TopBarConfig.Default();
            DialogWindow.ShowCustom(
                title: "Material Generation",
                message: sb.ToString(),
                type: MessageType.Error,
                buttons: new[] { "OK" },
                defaultIndex: 0,
                cancelIndex: 0,
                onResultIndex: null,
                topBar: topBar
            );
        }

        private static void ApplyUnityFormat(
            MaterialJsonStructure data,
            Material mat,
            GameMetadata meta,
            TextureAssigner.TextureLookupCache textureCache = null)
        {
            ApplyUnityTopLevelFields(data, mat);

            var props = data.m_SavedProperties;
            if (props == null) return;

            if (props.m_Floats != null)
            {
                foreach (var kv in props.m_Floats)
                {
                    var name = ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(name)) continue;
                    mat.SetFloat(name, kv.Value);
                }
            }

            if (props.m_Ints != null)
            {
                foreach (var kv in props.m_Ints)
                {
                    var name = ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(name)) continue;
                    mat.SetInteger(name, kv.Value);
                }
            }

            if (props.m_Colors != null)
            {
                foreach (var kv in props.m_Colors)
                {
                    var name = ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(name)) continue;
                    mat.SetColor(name, kv.Value?.ToColor() ?? Color.white);
                }
            }

            // Delegate all texture logic to unified global assigner
            TextureAssigner.AssignTextures(data, mat, meta, textureCache);
        }

        private static void ApplyUnityTopLevelFields(MaterialJsonStructure data, Material mat)
        {
            if (data == null || mat == null) return;

            ApplyMaterialDisplayName(data, mat);
            ApplyShaderKeywordState(data, mat);
            ApplyGlobalIlluminationFlags(data, mat);

            if (data.m_EnableInstancingVariants != null)
            {
                if (data.TryGetEnableInstancing(out var enabled))
                {
                    mat.enableInstancing = enabled;
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Warning, $"MaterialGeneration: Unsupported m_EnableInstancingVariants token '{data.m_EnableInstancingVariants}' on '{mat.name}'.");
                }
            }

            if (data.m_CustomRenderQueue.HasValue)
            {
                mat.renderQueue = data.m_CustomRenderQueue.Value;
            }

            if (data.m_DisabledShaderPasses != null)
            {
                foreach (var passName in data.m_DisabledShaderPasses)
                {
                    if (string.IsNullOrWhiteSpace(passName)) continue;
                    mat.SetShaderPassEnabled(passName.Trim(), false);
                }
            }

            if (data.m_StringTagMap != null)
            {
                foreach (var kv in data.m_StringTagMap)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    mat.SetOverrideTag(kv.Key, kv.Value ?? string.Empty);
                }
            }
        }

        private static void ResetMaterialToShaderDefaults(Material mat, Shader shader, string materialName)
        {
            if (mat == null || shader == null) return;

            var originalName = string.IsNullOrWhiteSpace(materialName) ? mat.name : materialName;
            var defaultMaterial = new Material(shader)
            {
                name = originalName
            };

            try
            {
                EditorUtility.CopySerialized(defaultMaterial, mat);
                mat.shader = shader;
                mat.name = originalName;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaultMaterial);
            }
        }

        private static void ApplyMaterialDisplayName(MaterialJsonStructure data, Material mat)
        {
            var displayName = data?.m_Name;

            if (string.IsNullOrWhiteSpace(displayName)) return;

            var trimmed = displayName.Trim();
            if (!string.Equals(mat.name, trimmed, StringComparison.Ordinal))
            {
                mat.name = trimmed;
            }
        }

        private static void ApplyGlobalIlluminationFlags(MaterialJsonStructure data, Material mat)
        {
            if (!data.m_LightmapFlags.HasValue) return;
            mat.globalIlluminationFlags = (MaterialGlobalIlluminationFlags)data.m_LightmapFlags.Value;
        }

        private static void ApplyShaderKeywordState(MaterialJsonStructure data, Material mat)
        {
            if (data.m_ShaderKeywords != null)
            {
                mat.shaderKeywords = MaterialKeywordUtil.ExtractKeywords(data.m_ShaderKeywords).ToArray();
            }

            if (data.m_ValidKeywords != null)
            {
                foreach (var keyword in MaterialKeywordUtil.ExtractKeywords(data.m_ValidKeywords))
                {
                    mat.EnableKeyword(keyword);
                }
            }

            if (data.m_InvalidKeywords != null)
            {
                foreach (var keyword in MaterialKeywordUtil.ExtractKeywords(data.m_InvalidKeywords))
                {
                    mat.DisableKeyword(keyword);
                }
            }
        }

        public static string ConvertName(string key, GameMetadata meta)
        {
            if (meta?.PropertyConversions != null && !string.IsNullOrEmpty(key) && meta.PropertyConversions.TryGetValue(key, out var mapped))
                return mapped;
            return key;
        }

        private static string ComputeOutputDirectory(string desiredOutDir, string sourceJson)
        {
            // Priority: explicit > beside JSON > Assets/HoyoToon/GeneratedMaterials
            if (!string.IsNullOrWhiteSpace(desiredOutDir))
            {
                return EditorUtil.ToAbsolutePath(desiredOutDir);
            }
            if (!string.IsNullOrWhiteSpace(sourceJson) && File.Exists(sourceJson))
            {
                var srcDir = Path.GetDirectoryName(sourceJson);
                // Place material in the same folder as the source JSON
                return srcDir;
            }
            // Fallback to Assets-level generated folder (no package dependency)
            return Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
        }

        private static string DeriveMaterialName(string sourceJson, string shaderPath, MaterialJsonStructure materialData = null)
        {
            // Prefer m_Name from the JSON payload when available
            var jsonName = materialData?.m_Name;
            if (!string.IsNullOrWhiteSpace(jsonName))
                return EditorUtil.SanitizeFileName(jsonName.Trim());

            if (!string.IsNullOrWhiteSpace(sourceJson))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourceJson);
                if (!string.IsNullOrWhiteSpace(baseName)) return baseName;
            }
            // Use shader tail as a hint
            var tail = shaderPath?.Split('/')?.LastOrDefault();
            return string.IsNullOrWhiteSpace(tail) ? "GeneratedMaterial" : ($"{tail}_Material");
        }

        private static (int evaluated, int changed) ApplyTextureImportRulesForMaterialTextures(Material mat, string knownGameKey = null)
        {
            if (mat == null || mat.shader == null) return (0, 0);
            var shader = mat.shader;
            int count = shader.GetPropertyCount();
            int evaluated = 0;
            int changed = 0;
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    continue;
                var propName = shader.GetPropertyName(i);
                if (!mat.HasProperty(propName)) continue;
                var tex = mat.GetTexture(propName) as Texture2D;
                if (tex == null) continue;
                var path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrWhiteSpace(path)) continue;
                evaluated++;
                if (TextureImportRulesApplier.TryApplyForAsset(path, mat, true, knownGameKey))
                    changed++;
            }
            return (evaluated, changed);
        }

        private static void GenerateFromDetections(
            IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> detections,
            UnityEngine.Object contextForRawJson,
            string pathOrJsonForRaw,
            string outputDir,
            string materialNameForRaw,
            TextureAssigner.TextureLookupCache textureCache,
            List<Result> failures,
            out bool hasSuccess,
            out int successCount,
            out List<Result> successResults)
        {
            hasSuccess = false;
            successCount = 0;
            successResults = new List<Result>();

            int total = detections?.Count ?? 0;
            if (total == 0) return;

            if (total > 1)
                EditorUtility.DisplayProgressBar("Material Generation", $"Generating {total} materials...", 0f);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int index = 0; index < total; index++)
                {
                    var (game, shaderPath, src) = detections[index];
                    Result res;
                    try
                    {
                        bool isRaw = string.Equals(src, "<raw-json>", StringComparison.Ordinal);
                        var prepared = isRaw
                            ? PrepareGeneration(contextForRawJson, pathOrJsonForRaw, game, shaderPath, src)
                            : PrepareGeneration(null, src, game, shaderPath, src);

                        var requestedMaterialName = isRaw ? materialNameForRaw : null;
                        res = GenerateFromPrepared(prepared, outputDir, requestedMaterialName, textureCache, deferSaveAndRefresh: true, skipTextureImportRules: true);
                        if (res != null)
                        {
                            res.SourceJson = src;
                            res.GameKey = res.GameKey ?? game;
                            res.ShaderPath = res.ShaderPath ?? shaderPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        var sourceLabel = src ?? "<raw-json>";
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Error, $"Exception during generation for '{sourceLabel}': {ex.Message}");
                        res = new Result
                        {
                            Ok = false,
                            GameKey = game,
                            ShaderPath = shaderPath,
                            SourceJson = src,
                            Error = ex.Message
                        };
                    }

                    if (res == null || !res.Ok)
                    {
                        failures?.Add(res);
                    }
                    else
                    {
                        hasSuccess = true;
                        successCount++;
                        successResults.Add(res);
                    }

                    if (total > 1)
                    {
                        var label = Path.GetFileNameWithoutExtension(src ?? "material");
                        EditorUtility.DisplayProgressBar("Material Generation", $"({index + 1}/{total}) {label}", (float)(index + 1) / total);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if (total > 1)
                    EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
