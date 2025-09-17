#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;
using HoyoToon.Materials;
using HoyoToon.Textures;

namespace HoyoToon.Debugging
{
    public static class MaterialGenerationDebugMenu
    {
        private sealed class YamlResult
        {
            public bool Ok { get; set; }
            public string GameKey { get; set; }
            public string SourceJson { get; set; }
            public string OutputPath { get; set; }
            public string Error { get; set; }
        }

        [MenuItem("Assets/HoyoToon/Material/Generate Materials (Auto)")]
        public static void GenerateAutoFromSelection()
        {
            var selection = Selection.objects;
            HoyoToonLogger.MaterialInfo("MaterialGeneration: Starting auto generation from selection/context...");
            if (selection != null && selection.Length > 1)
            {
                MaterialGeneration.GenerateAuto((object)selection, pathOrJson: null, outputDir: null, materialName: null, showFailureWindow: true);
            }
            else
            {
                var sel = Selection.activeObject;
                MaterialGeneration.GenerateAuto((object)sel, pathOrJson: null, outputDir: null, materialName: null, showFailureWindow: true);
            }
            HoyoToonLogger.MaterialInfo("MaterialGeneration: Auto generation completed. See console for details or a dialog if failures occurred.");
        }

        [MenuItem("Assets/HoyoToon/Material/Clear Generated Materials")]
        public static void ClearGeneratedMaterialsFromSelection()
        {
            var selection = Selection.objects;
            int deleted = 0;
            if (selection != null && selection.Length > 1)
            {
                deleted = MaterialGeneration.ClearMaterialsFromContexts(selection);
            }
            else
            {
                var sel = Selection.activeObject;
                deleted = MaterialGeneration.ClearMaterialsFromContext(sel, null);
            }

            HoyoToonDialogWindow.ShowInfo("Clear Generated Materials", $"Deleted {deleted} material(s) from the selected context.");
        }

        [MenuItem("Assets/HoyoToon/Material/Generate YAML Stub (Shaderless)")]
        public static void GenerateYamlStubFromSelection()
        {
            var selection = Selection.objects;
            HoyoToonLogger.MaterialInfo("MaterialGeneration: Starting YAML stub generation from selection/context...");

            void GenerateFor(object ctx)
            {
                var res = GenerateStubYamlAuto(ctx as UnityEngine.Object, pathOrJson: null, outputDir: null, materialName: null);
                if (res == null || !res.Ok)
                {
                    var msg = res?.Error ?? "Unknown error";
                    HoyoToonDialogWindow.ShowError("YAML Stub Generation", msg);
                }
                else
                {
                    HoyoToonDialogWindow.ShowInfo("YAML Stub Generation", $"Created: {res.OutputPath}");
                }
            }

            if (selection != null && selection.Length > 1)
            {
                foreach (var obj in selection) GenerateFor(obj);
            }
            else
            {
                GenerateFor(Selection.activeObject);
            }

            HoyoToonLogger.MaterialInfo("MaterialGeneration: YAML stub generation completed.");
        }

        // ---- YAML Stub Generator (debug-only) ----
        private static YamlResult GenerateStubYamlAuto(UnityEngine.Object assetOrNull = null, string pathOrJson = null, string outputDir = null, string materialName = null)
        {
            var result = new YamlResult();

            // Detect game and source; ignore shader requirements for stub
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson);
            result.GameKey = gameKey;
            result.SourceJson = sourceJson;

            // Resolve JSON payload
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
                    result.Error = $"MaterialGeneration (YAML): Failed reading JSON file '{sourceJson}': {ex.Message}";
                    HoyoToonLogger.MaterialError(result.Error);
                    return result;
                }
            }
            else if (!string.IsNullOrWhiteSpace(pathOrJson) && File.Exists(pathOrJson))
            {
                try { jsonPayload = File.ReadAllText(pathOrJson); }
                catch (Exception ex)
                {
                    result.Error = $"MaterialGeneration (YAML): Failed reading JSON file '{pathOrJson}': {ex.Message}";
                    HoyoToonLogger.MaterialError(result.Error);
                    return result;
                }
            }

            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                result.Error = "MaterialGeneration (YAML): No JSON payload to parse.";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            // Parse
            if (!HoyoToonApi.Parser.TryParse<MaterialJsonStructure>(jsonPayload, out var data, out var parseError) || data == null)
            {
                result.Error = $"MaterialGeneration (YAML): Parse failed: {parseError}";
                HoyoToonLogger.MaterialError(result.Error);
                return result;
            }

            // Metadata conversions
            var metaMap = HoyoToonApi.GetGameMetadata();
            GameMetadata meta = null;
            if (metaMap != null && !string.IsNullOrEmpty(gameKey)) metaMap.TryGetValue(gameKey, out meta);
            data = MaterialConversion.ApplyPropertyConversions(data, meta ?? new GameMetadata());

            // Compute output location and name
            string outDir = ComputeOutputDirectory(outputDir, sourceJson);
            HoyoToonEditorUtil.EnsureDirectory(outDir);
            string matName = !string.IsNullOrWhiteSpace(materialName)
                ? HoyoToonEditorUtil.SanitizeFileName(materialName)
                : DeriveMaterialName(sourceJson, shaderPath);
            string absoluteOutPath = Path.Combine(outDir, matName + ".mat");

            // Build YAML
            var yaml = BuildShaderlessYaml(matName, data, meta);

            // Write to disk
            try
            {
                File.WriteAllText(absoluteOutPath, yaml);
            }
            catch (Exception ex)
            {
                // Fallback to Assets if Packages folder is not writable
                HoyoToonLogger.MaterialWarning($"YAML write at '{absoluteOutPath}' failed: {ex.Message}. Falling back to 'Assets/HoyoToon/GeneratedMaterials'.");
                var fbDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
                HoyoToonEditorUtil.EnsureDirectory(fbDir);
                absoluteOutPath = Path.Combine(fbDir, matName + ".mat");
                File.WriteAllText(absoluteOutPath, yaml);
            }

            AssetDatabase.Refresh();

            result.Ok = true;
            result.OutputPath = HoyoToonEditorUtil.ToProjectRelative(absoluteOutPath);
            HoyoToonLogger.MaterialInfo($"Generated shaderless YAML material stub '{matName}' at '{result.OutputPath}' (Game='{gameKey ?? "?"}').");
            return result;
        }

        private static string BuildShaderlessYaml(string matName, MaterialJsonStructure data, GameMetadata meta)
        {
            // Collect lists for YAML emission (sorted for determinism)
            var texEnvs = new SortedDictionary<string, (MaterialJsonStructure.Vector2Info scale, MaterialJsonStructure.Vector2Info offset, string guid)>(StringComparer.Ordinal);
            var ints = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var floats = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var colors = new SortedDictionary<string, MaterialJsonStructure.ColorInfo>(StringComparer.Ordinal);

            // Helper: try resolve a texture GUID for a property using the JSON-provided name first, then metadata mapping
            static string ResolveTextureGuid(string nameCandidate)
            {
                if (string.IsNullOrWhiteSpace(nameCandidate)) return null;
                var tex = TextureAssigner.FindTextureGlobal(nameCandidate);
                if (tex == null) return null;
                var path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrWhiteSpace(path)) return null;
                var guid = AssetDatabase.AssetPathToGUID(path);
                return string.IsNullOrWhiteSpace(guid) ? null : guid;
            }

            if (data != null)
            {
                if (data.IsUnityFormat && data.m_SavedProperties != null)
                {
                    var sp = data.m_SavedProperties;
                    if (sp.m_TexEnvs != null)
                    {
                        foreach (var kv in sp.m_TexEnvs)
                        {
                            var sc = kv.Value?.m_Scale ?? new MaterialJsonStructure.Vector2Info { X = 1, Y = 1 };
                            var of = kv.Value?.m_Offset ?? new MaterialJsonStructure.Vector2Info { X = 0, Y = 0 };
                            string guid = null;
                            // Try JSON-provided texture name first
                            var jsonTexName = kv.Value?.m_Texture?.Name;
                            guid = ResolveTextureGuid(jsonTexName);
                            // If not found, try metadata mapping for this property
                            if (guid == null && meta?.TextureMappings != null && meta.TextureMappings.TryGetValue(kv.Key, out var mappedTexName))
                            {
                                guid = ResolveTextureGuid(mappedTexName);
                            }
                            texEnvs[kv.Key] = (sc, of, guid);
                        }
                    }
                    if (sp.m_Ints != null)
                    {
                        foreach (var kv in sp.m_Ints) ints[kv.Key] = kv.Value;
                    }
                    if (sp.m_Floats != null)
                    {
                        foreach (var kv in sp.m_Floats) floats[kv.Key] = kv.Value;
                    }
                    if (sp.m_Colors != null)
                    {
                        foreach (var kv in sp.m_Colors) if (kv.Value != null) colors[kv.Key] = kv.Value;
                    }
                }
                else if (data.IsUnrealFormat)
                {
                    // Textures: create default scale/offset
                    if (data.Textures != null)
                    {
                        foreach (var kv in data.Textures)
                        {
                            string guid = null;
                            // Try JSON dictionary value as preferred name
                            guid = ResolveTextureGuid(kv.Value);
                            // If not found, try metadata mapping for this property key
                            if (guid == null && meta?.TextureMappings != null && meta.TextureMappings.TryGetValue(kv.Key, out var mappedTexName))
                            {
                                guid = ResolveTextureGuid(mappedTexName);
                            }
                            texEnvs[kv.Key] = (
                                new MaterialJsonStructure.Vector2Info { X = 1, Y = 1 },
                                new MaterialJsonStructure.Vector2Info { X = 0, Y = 0 },
                                guid
                            );
                        }
                    }
                    var p = data.Parameters;
                    if (p != null)
                    {
                        if (p.Scalars != null)
                        {
                            foreach (var kv in p.Scalars) floats[kv.Key] = kv.Value;
                        }
                        if (p.Switches != null)
                        {
                            foreach (var kv in p.Switches) ints[kv.Key] = kv.Value ? 1 : 0;
                        }
                        if (p.Colors != null)
                        {
                            foreach (var kv in p.Colors) if (kv.Value != null) colors[kv.Key] = kv.Value;
                        }
                        if (p.Properties != null)
                        {
                            foreach (var kv in p.Properties)
                            {
                                if (kv.Value == null) continue;
                                switch (kv.Value)
                                {
                                    case bool b:
                                        ints[kv.Key] = b ? 1 : 0;
                                        break;
                                    case sbyte sb: ints[kv.Key] = sb; break;
                                    case byte by: ints[kv.Key] = by; break;
                                    case short sh: ints[kv.Key] = sh; break;
                                    case ushort ush: ints[kv.Key] = ush; break;
                                    case int i: ints[kv.Key] = i; break;
                                    case uint ui: ints[kv.Key] = unchecked((int)ui); break;
                                    case long l: ints[kv.Key] = unchecked((int)l); break;
                                    case ulong ul: ints[kv.Key] = unchecked((int)ul); break;
                                    case float f: floats[kv.Key] = f; break;
                                    case double d: floats[kv.Key] = (float)d; break;
                                    case string s:
                                        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                                            floats[kv.Key] = parsed;
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            var builder = new System.Text.StringBuilder(4096);
            var inv = CultureInfo.InvariantCulture;

            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!21 &2100000");
            builder.AppendLine("Material:");
            builder.AppendLine("  serializedVersion: 8");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
            builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
            builder.AppendLine($"  m_Name: {matName}");
            builder.AppendLine("  m_Shader: {fileID: 0}");
            builder.AppendLine("  m_Parent: {fileID: 0}");
            builder.AppendLine("  m_ModifiedSerializedProperties: 0");
            builder.AppendLine("  m_ValidKeywords: []");
            builder.AppendLine("  m_InvalidKeywords: []");
            builder.AppendLine("  m_LightmapFlags: 4");
            builder.AppendLine("  m_EnableInstancingVariants: 0");
            builder.AppendLine("  m_DoubleSidedGI: 0");
            builder.AppendLine("  m_CustomRenderQueue: -1");
            builder.AppendLine("  stringTagMap: {}");
            builder.AppendLine("  disabledShaderPasses: []");
            builder.AppendLine("  m_LockedProperties: ");
            builder.AppendLine("  m_SavedProperties:");
            builder.AppendLine("    serializedVersion: 3");

            if (texEnvs.Count == 0)
            {
                builder.AppendLine("    m_TexEnvs: []");
            }
            else
            {
                builder.AppendLine("    m_TexEnvs:");
                foreach (var kv in texEnvs)
                {
                    var sc = kv.Value.scale ?? new MaterialJsonStructure.Vector2Info { X = 1, Y = 1 };
                    var of = kv.Value.offset ?? new MaterialJsonStructure.Vector2Info { X = 0, Y = 0 };
                    var guid = kv.Value.guid;
                    builder.AppendLine($"    - {kv.Key}:");
                    if (!string.IsNullOrWhiteSpace(guid))
                        builder.AppendLine($"        m_Texture: {{fileID: 2800000, guid: {guid}, type: 3}}");
                    else
                        builder.AppendLine("        m_Texture: {fileID: 0}");
                    builder.AppendLine($"        m_Scale: {{x: {sc.X.ToString("G9", inv)}, y: {sc.Y.ToString("G9", inv)} }}");
                    builder.AppendLine($"        m_Offset: {{x: {of.X.ToString("G9", inv)}, y: {of.Y.ToString("G9", inv)} }}");
                }
            }

            if (ints.Count == 0)
            {
                builder.AppendLine("    m_Ints: []");
            }
            else
            {
                builder.AppendLine("    m_Ints:");
                foreach (var kv in ints)
                {
                    builder.AppendLine($"    - {kv.Key}: {kv.Value}");
                }
            }

            if (floats.Count == 0)
            {
                builder.AppendLine("    m_Floats: []");
            }
            else
            {
                builder.AppendLine("    m_Floats:");
                foreach (var kv in floats)
                {
                    builder.AppendLine($"    - {kv.Key}: {kv.Value.ToString("G9", inv)}");
                }
            }

            if (colors.Count == 0)
            {
                builder.AppendLine("    m_Colors: []");
            }
            else
            {
                builder.AppendLine("    m_Colors:");
                foreach (var kv in colors)
                {
                    var c = kv.Value;
                    builder.AppendLine($"    - {kv.Key}: {{r: {c.r.ToString("G9", inv)}, g: {c.g.ToString("G9", inv)}, b: {c.b.ToString("G9", inv)}, a: {c.a.ToString("G9", inv)} }}");
                }
            }

            builder.AppendLine("  m_BuildTextureStacks: []");
            return builder.ToString();
        }

        private static string ComputeOutputDirectory(string desiredOutDir, string sourceJson)
        {
            if (!string.IsNullOrWhiteSpace(desiredOutDir))
            {
                return HoyoToonEditorUtil.ToAbsolutePath(desiredOutDir);
            }
            if (!string.IsNullOrWhiteSpace(sourceJson) && File.Exists(sourceJson))
            {
                var srcDir = Path.GetDirectoryName(sourceJson);
                return srcDir;
            }
            return Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
        }

        private static string DeriveMaterialName(string sourceJson, string shaderPath)
        {
            if (!string.IsNullOrWhiteSpace(sourceJson))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourceJson);
                if (!string.IsNullOrWhiteSpace(baseName)) return baseName;
            }
            string tail = null;
            if (!string.IsNullOrEmpty(shaderPath))
            {
                var parts = shaderPath.Split('/');
                tail = parts.Length > 0 ? parts[parts.Length - 1] : null;
            }
            return string.IsNullOrWhiteSpace(tail) ? "GeneratedMaterial" : ($"{tail}_Material");
        }
    }
}
#endif
