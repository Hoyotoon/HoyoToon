#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Utilities;
using HoyoToon.API;
using HoyoToon.Textures;

namespace HoyoToon.Materials
{
    /// <summary>
    /// Global, editor-only material generation utilities.
    /// Input is a JSON payload or path; integrates detection + per-game metadata
    /// to create a Material with the detected shader and map properties safely.
    /// </summary>
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

        

        /// <summary>
        /// Generate a Material using either:
        /// - raw JSON (pathOrJson contains '{')
        /// - JSON file path (absolute or relative)
        /// - context asset/folder (auto-detect nearest JSON)
        ///
        /// When outputDir is null, attempts to place the material beside the JSON file (or in a Materials subfolder).
        /// Returns a Result describing success/failure and created asset.
        /// </summary>
        public static Result GenerateMaterialAuto(UnityEngine.Object assetOrNull = null, string pathOrJson = null, string outputDir = null, string materialName = null)
        {
            var result = new Result();

            // 1) Detect game + shader + source
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson);
            result.GameKey = gameKey;
            result.ShaderPath = shaderPath;
            result.SourceJson = sourceJson;

            if (string.IsNullOrEmpty(gameKey))
            {
                result.Error = "MaterialGeneration: Unsupported material JSON (no matching game metadata).";
                HoyoToonLogger.MaterialError(result.Error);
                return result; // collected by batch failure summary if enabled
            }
            if (string.IsNullOrEmpty(shaderPath))
            {
                result.Error = $"MaterialGeneration: Game '{gameKey}' detected but shader path missing.";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            var shader = Shader.Find(shaderPath);
            result.Shader = shader;
            if (shader == null)
            {
                result.Error = $"MaterialGeneration: Shader '{shaderPath}' not found in project.";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            // 2) Resolve JSON payload
            string jsonPayload = null;
            if (!string.IsNullOrWhiteSpace(pathOrJson) && HoyoToonEditorUtil.LooksLikeJson(pathOrJson))
            {
                jsonPayload = pathOrJson;
            }
            else if (!string.IsNullOrEmpty(sourceJson) && File.Exists(sourceJson))
            {
                try { jsonPayload = File.ReadAllText(sourceJson); }
                catch (Exception ex)
                {
                    result.Error = $"MaterialGeneration: Failed reading JSON file '{sourceJson}': {ex.Message}";
                    HoyoToonLogger.MaterialError(result.Error);
                    return result;
                }
            }
            else if (!string.IsNullOrWhiteSpace(pathOrJson) && File.Exists(pathOrJson))
            {
                try { jsonPayload = File.ReadAllText(pathOrJson); }
                catch (Exception ex)
                {
                    result.Error = $"MaterialGeneration: Failed reading JSON file '{pathOrJson}': {ex.Message}";
                    HoyoToonLogger.MaterialError(result.Error);
                    return result;
                }
            }

            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                result.Error = "MaterialGeneration: No JSON payload to parse.";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            // 3) Parse material data
            if (!HoyoToonApi.Parser.TryParse<MaterialJsonStructure>(jsonPayload, out var data, out var parseError) || data == null)
            {
                result.Error = $"MaterialGeneration: Parse failed: {parseError}";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            // 4) Load metadata for mapping
            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var meta) || meta == null)
            {
                HoyoToonLogger.MaterialWarning($"MaterialGeneration: No metadata configured for game '{gameKey}'. Using direct property names only.");
                meta = new GameMetadata();
            }

            // Apply property conversions from metadata to the parsed structure prior to assignment
            data = MaterialConversion.ApplyPropertyConversions(data, meta);

            // No game-specific config needed for textures; TextureAssigner performs global lookups.

            // 5) Create/locate material asset path
            string outDir = ComputeOutputDirectory(outputDir, sourceJson);
            HoyoToonEditorUtil.EnsureDirectory(outDir);

            string matName = !string.IsNullOrWhiteSpace(materialName)
                ? HoyoToonEditorUtil.SanitizeFileName(materialName)
                : DeriveMaterialName(sourceJson, shaderPath);

            string assetPath = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(outDir, matName + ".mat"));
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = matName };
                try
                {
                    AssetDatabase.CreateAsset(mat, assetPath);
                }
                catch (Exception ex)
                {
                    // Fallback to Assets if Packages is read-only or path invalid
                    HoyoToonLogger.MaterialWarning($"CreateAsset at '{assetPath}' failed: {ex.Message}. Falling back to 'Assets/HoyoToon/GeneratedMaterials'.");
                    var assetsFallbackDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
                    HoyoToonEditorUtil.EnsureDirectory(assetsFallbackDir);
                    var fbAssetPath = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(assetsFallbackDir, matName + ".mat"));
                    AssetDatabase.CreateAsset(mat, fbAssetPath);
                    assetPath = fbAssetPath; // update to new path
                }
            }
            else
            {
                // Ensure shader is correct if we are updating an existing asset
                if (mat.shader != shader) mat.shader = shader;
            }

            // 6) Apply properties based on detected format
            try
            {
                if (data.IsUnityFormat)
                    ApplyUnityFormat(data, mat, meta);
                else if (data.IsUnrealFormat)
                    ApplyUnrealFormat(data, mat, meta);
                else
                    HoyoToonLogger.MaterialWarning("MaterialGeneration: JSON did not match Unity or Unreal formats.");

                // Apply explicit property overrides (after JSON assignment, before texture mappings)
                MaterialOverrides.Apply(mat, meta);

                // Final pass: configured texture mappings override assigned textures if specified
                TextureMapping.ApplyMappings(mat, meta);

                // Apply texture import rules to the textures actually used by this material
                ApplyTextureImportRulesForMaterialTextures(mat);
            }
            catch (Exception ex)
            {
                result.Error = $"MaterialGeneration: Exception while applying properties: {ex.Message}";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.Ok = true;
            result.Material = mat;
            result.MaterialAssetPath = assetPath;
            HoyoToonLogger.MaterialInfo($"Generated material '{matName}' at '{assetPath}' using shader '{shaderPath}' for game '{gameKey}'.");
            return result;
        }


        /// <summary>
        /// Centralized entry point: generate materials from raw JSON, a JSON file, a single context asset/folder,
        /// or a multi-selection (IEnumerable of assets). Continues on errors and only shows a dialog when failures occur.
        /// Pass either:
        /// - contextOrSelection: a UnityEngine.Object (file/folder/asset), or an IEnumerable<UnityEngine.Object> for multi-selection, or null
        /// - pathOrJson: optional raw JSON or a specific path
        /// </summary>
        public static void GenerateAuto(object contextOrSelection = null, string pathOrJson = null, string outputDir = null, string materialName = null, bool showFailureWindow = true)
        {
            // Handle multi-selection case when an IEnumerable of assets is provided.
            if (contextOrSelection is IEnumerable<UnityEngine.Object> many)
            {
                var failures = new List<Result>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allDetections = new List<(string gameKey, string shaderPath, string sourceJson)>();

                foreach (var obj in many ?? Enumerable.Empty<UnityEngine.Object>())
                {
                    var dets = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(obj, null) ?? Array.Empty<(string, string, string)>();
                    foreach (var d in dets)
                    {
                        if (string.IsNullOrEmpty(d.sourceJson) || d.sourceJson == "<raw-json>") continue;
                        if (seen.Add(d.sourceJson)) allDetections.Add(d);
                    }
                }

                foreach (var (game, shaderPath, src) in allDetections)
                {
                    Result res = null;
                    try
                    {
                        res = GenerateMaterialAuto(null, src, outputDir, null);
                        if (res != null)
                        {
                            res.SourceJson = src;
                            res.GameKey = res.GameKey ?? game;
                            res.ShaderPath = res.ShaderPath ?? shaderPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.MaterialError($"Exception during generation for '{src}': {ex.Message}");
                        res = new Result
                        {
                            Ok = false,
                            GameKey = game,
                            ShaderPath = shaderPath,
                            SourceJson = src,
                            Error = ex.Message
                        };
                    }
                    if (res == null || !res.Ok) failures.Add(res);
                }

                if (showFailureWindow && failures.Count > 0)
                {
                    ShowFailuresSummaryWindow(failures, null, "<multiple selections>");
                }
                return;
            }

            // Single context asset/folder or raw input path/json
            var singleContext = contextOrSelection as UnityEngine.Object;
            var detections = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(singleContext, pathOrJson) ?? Array.Empty<(string gameKey, string shaderPath, string sourceJson)>();

            if (detections.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(pathOrJson) && singleContext == null)
                {
                    HoyoToonLogger.MaterialInfo("MaterialGeneration: No input/context provided; nothing to generate.");
                    return;
                }

                var single = GenerateMaterialAuto(singleContext, pathOrJson, outputDir, materialName);
                if (showFailureWindow && (single == null || !single.Ok))
                {
                    ShowFailuresSummaryWindow(new[] { single }, singleContext, pathOrJson);
                }
                return;
            }

            var singleFailures = new List<Result>();
            foreach (var (game, shaderPath, src) in detections)
            {
                Result res = null;
                try
                {
                    if (string.Equals(src, "<raw-json>", StringComparison.Ordinal))
                    {
                        res = GenerateMaterialAuto(singleContext, pathOrJson, outputDir, materialName);
                    }
                    else
                    {
                        res = GenerateMaterialAuto(null, src, outputDir, null);
                        if (res != null)
                        {
                            res.SourceJson = src;
                            res.GameKey = res.GameKey ?? game;
                            res.ShaderPath = res.ShaderPath ?? shaderPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.MaterialError($"Exception during generation for '{src ?? "<raw-json>"}': {ex.Message}");
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
                    singleFailures.Add(res);
            }

            if (showFailureWindow && singleFailures.Count > 0)
            {
                ShowFailuresSummaryWindow(singleFailures, singleContext, pathOrJson);
            }
        }

        /// <summary>
        /// Delete generated materials associated with JSONs discovered from a context (asset/folder/path).
        /// Assumes materials were created next to their source JSONs using the derived name.
        /// </summary>
        public static int ClearMaterialsFromContext(UnityEngine.Object assetOrNull = null, string pathOrFolder = null)
        {
            int deleted = 0;
            var detections = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(assetOrNull, pathOrFolder) ?? Array.Empty<(string, string, string)>();
            foreach (var (_, shaderPath, src) in detections)
            {
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
                var outDir = ComputeOutputDirectory(null, src);
                var matName = DeriveMaterialName(src, shaderPath);
                var candidate = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(outDir, matName + ".mat"));
                if (AssetDatabase.LoadAssetAtPath<Material>(candidate) != null)
                {
                    if (AssetDatabase.DeleteAsset(candidate)) deleted++;
                }
                else
                {
                    // Also attempt fallback folder deletion if original generation fell back
                    var fbDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
                    var fbPath = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(fbDir, matName + ".mat"));
                    if (AssetDatabase.LoadAssetAtPath<Material>(fbPath) != null)
                    {
                        if (AssetDatabase.DeleteAsset(fbPath)) deleted++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }

        /// <summary>
        /// Delete generated materials for multiple selected contexts; returns total count deleted.
        /// </summary>
        public static int ClearMaterialsFromContexts(IEnumerable<UnityEngine.Object> assetsOrNull)
        {
            int total = 0;
            foreach (var obj in assetsOrNull ?? Enumerable.Empty<UnityEngine.Object>())
            {
                total += ClearMaterialsFromContext(obj, null);
            }
            return total;
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

            var topBar = HoyoToon.Utilities.BaseHoyoToonWindow.TopBarConfig.Default();
            HoyoToonDialogWindow.ShowCustom(
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

        private static void ApplyUnityFormat(MaterialJsonStructure data, Material mat, GameMetadata meta)
        {
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
                    mat.SetInt(name, kv.Value);
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
            TextureAssigner.AssignTextures(data, mat, meta);
        }

        private static void ApplyUnrealFormat(MaterialJsonStructure data, Material mat, GameMetadata meta)
        {
            var p = data.Parameters;
            if (p != null)
            {
                if (p.Scalars != null)
                {
                    foreach (var kv in p.Scalars)
                    {
                        var name = ConvertName(kv.Key, meta);
                        if (!mat.HasProperty(name)) continue;
                        mat.SetFloat(name, kv.Value);
                    }
                }

                if (p.Switches != null)
                {
                    foreach (var kv in p.Switches)
                    {
                        var name = ConvertName(kv.Key, meta);
                        if (!mat.HasProperty(name)) continue;
                        // Prefer int if property looks like an int, otherwise float
                        try { mat.SetInt(name, kv.Value ? 1 : 0); }
                        catch { mat.SetFloat(name, kv.Value ? 1f : 0f); }
                    }
                }

                if (p.Colors != null)
                {
                    foreach (var kv in p.Colors)
                    {
                        var name = ConvertName(kv.Key, meta);
                        if (!mat.HasProperty(name)) continue;
                        mat.SetColor(name, kv.Value?.ToColor() ?? Color.white);
                    }
                }

                if (p.Properties != null)
                {
                    foreach (var kv in p.Properties)
                    {
                        var name = ConvertName(kv.Key, meta);
                        if (!mat.HasProperty(name) || kv.Value == null) continue;
                        if (kv.Value is bool b)
                        {
                            try { mat.SetInt(name, b ? 1 : 0); }
                            catch { mat.SetFloat(name, b ? 1f : 0f); }
                        }
                        else if (kv.Value is double d)
                        {
                            mat.SetFloat(name, (float)d);
                        }
                        else if (kv.Value is float f)
                        {
                            mat.SetFloat(name, f);
                        }
                        else if (kv.Value is long li)
                        {
                            try { mat.SetInt(name, (int)li); }
                            catch { mat.SetFloat(name, li); }
                        }
                        else if (kv.Value is int ii)
                        {
                            try { mat.SetInt(name, ii); }
                            catch { mat.SetFloat(name, ii); }
                        }
                        else if (kv.Value is string s)
                        {
                            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                                mat.SetFloat(name, parsed);
                        }
                    }
                }
            }

            // Delegate all texture logic to unified global assigner
            TextureAssigner.AssignTextures(data, mat, meta);
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
                return HoyoToonEditorUtil.ToAbsolutePath(desiredOutDir);
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

        private static string DeriveMaterialName(string sourceJson, string shaderPath)
        {
            if (!string.IsNullOrWhiteSpace(sourceJson))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourceJson);
                if (!string.IsNullOrWhiteSpace(baseName)) return baseName;
            }
            // Use shader tail as a hint
            var tail = shaderPath?.Split('/')?.LastOrDefault();
            return string.IsNullOrWhiteSpace(tail) ? "GeneratedMaterial" : ($"{tail}_Material");
        }

        /// <summary>
        /// For each texture property used by this material, apply texture import rules
        /// so that importer settings match per-game configuration.
        /// </summary>
        private static void ApplyTextureImportRulesForMaterialTextures(Material mat)
        {
            if (mat == null || mat.shader == null) return;
            var shader = mat.shader;
            int count = UnityEditor.ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (UnityEditor.ShaderUtil.GetPropertyType(shader, i) != UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                var propName = UnityEditor.ShaderUtil.GetPropertyName(shader, i);
                if (!mat.HasProperty(propName)) continue;
                var tex = mat.GetTexture(propName) as Texture2D;
                if (tex == null) continue;
                var path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrWhiteSpace(path)) continue;
                // Use the material as context to help detection infer the correct game
                TextureImportRulesApplier.TryApplyForAsset(path, mat, true);
            }
        }
    }
}
#endif
