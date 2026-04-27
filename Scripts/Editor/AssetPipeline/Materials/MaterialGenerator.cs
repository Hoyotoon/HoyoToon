using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.AssetPipeline.Textures;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Detection;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Detection.Shader;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    public static class MaterialGenerator
    {
        public sealed class AppliedSettings
        {
            public int TextureCount;
            public int FloatCount;
            public int IntCount;
            public int ColorCount;
            public int KeywordEnabledCount;
            public int KeywordDisabledCount;
            public int TagCount;
            public int DisabledPassCount;
        }

        public sealed class ItemResult
        {
            public string JsonAssetPath;
            public string MaterialAssetPath;
            public string ShaderPath;
            public string ShaderSource;
            public bool Success;
            public bool Created;
            public string Message;
            public AppliedSettings Applied = new AppliedSettings();
        }

        public sealed class Result
        {
            public int ScannedCount;
            public int CreatedCount;
            public int UpdatedCount;
            public int FailedCount;
            public bool HasSelection;
            public bool HasJsonCandidates;
            public List<string> JsonAssetPaths;
            public List<ItemResult> ItemResults;
        }

        public static Result GenerateFromSelection(UnityEngine.Object[] selectedAssets)
        {
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Materials,
                $"Material generation started for {selectedAssets?.Length ?? 0} selected asset(s).");

            MaterialPropertyConversions.Initialize();
            MaterialPropertyOverrides.Initialize();
            TextureImportSettingsApplicator.Initialize();
            TexturePropertyMappings.Initialize();

            var result = new Result
            {
                JsonAssetPaths = AssetContextJsonQueryUtility.CollectJsonAssetPaths(selectedAssets),
                ItemResults = new List<ItemResult>(),
                HasSelection = selectedAssets != null && selectedAssets.Length > 0
            };

            result.ScannedCount = result.JsonAssetPaths.Count;
            result.HasJsonCandidates = result.ScannedCount > 0;

            if (!result.HasSelection)
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Materials, "Material generation skipped because no assets were selected.");
            }
            else if (!result.HasJsonCandidates)
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Materials, "Material generation found no JSON files in the current selection.");
            }
            else
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Material generation found {result.ScannedCount} JSON file(s) to process.");
            }

            foreach (string jsonPath in result.JsonAssetPaths)
            {
                var itemResult = TryCreateOrUpdateMaterial(jsonPath);
                result.ItemResults.Add(itemResult);

                if (itemResult.Success)
                {
                    if (itemResult.Created)
                    {
                        result.CreatedCount++;
                    }
                    else
                    {
                        result.UpdatedCount++;
                    }
                }
                else
                {
                    result.FailedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (result.HasJsonCandidates)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Materials,
                    $"Material generation completed. Scanned {result.ScannedCount} JSON file(s), created {result.CreatedCount}, updated {result.UpdatedCount}, failed {result.FailedCount}.");
            }

            return result;
        }

        private static ItemResult TryCreateOrUpdateMaterial(string jsonPath)
        {
            var result = new ItemResult { JsonAssetPath = jsonPath };
            HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Processing material JSON '{jsonPath}'.");

            string absoluteJsonPath = AssetContextJsonQueryUtility.ToAbsolutePath(jsonPath);

            if (string.IsNullOrWhiteSpace(absoluteJsonPath) || !File.Exists(absoluteJsonPath))
                return Fail(result, "The material JSON file could not be found.");

            if (!TextFileUtility.TryReadAllText(absoluteJsonPath, out string rawJson))
                return Fail(result, "The material JSON file could not be read.");

            if (!MaterialJsonParser.TryParse(rawJson, out MaterialJson materialJson))
                return Fail(result, "The material JSON file could not be parsed.");

            var game = GameDetector.DetectGame(materialJson);
            if (game == null)
                return Fail(result, "The source game could not be detected from the material JSON.", HoyoToonLogCategory.Detection);

            IReadOnlyList<string> matchedGameProperties = GameDetector.GetMatchedProperties(game, materialJson);
            string matchedGamePropertiesText = matchedGameProperties == null
                ? "none"
                : string.Join(", ", matchedGameProperties.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Detection,
                $"Detected game '{game.Key}' for '{jsonPath}'. Matched properties: {(string.IsNullOrWhiteSpace(matchedGamePropertiesText) ? "none" : matchedGamePropertiesText)}.");

            MaterialPropertyConversions.Apply(game, materialJson);

            if (!ShaderDetector.TryDetectShader(game, materialJson, rawJson, out string shaderPath, out var source, out IReadOnlyList<string> lookedForKeywords, out IReadOnlyList<string> matchedKeywords)
                || string.IsNullOrWhiteSpace(shaderPath))
                return Fail(result, "A target shader could not be resolved from the material JSON.", HoyoToonLogCategory.Detection);

            MaterialPropertyOverrides.Apply(game, shaderPath, materialJson);

            result.ShaderPath = shaderPath;
            result.ShaderSource = source.ToString();
            string lookedForKeywordsText = lookedForKeywords == null
                ? "none"
                : string.Join(", ", lookedForKeywords.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
            string matchedKeywordsText = matchedKeywords == null
                ? "none"
                : string.Join(", ", matchedKeywords.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Detection,
                $"Resolved shader '{shaderPath}' for '{jsonPath}' using {result.ShaderSource}. Looked for keywords: {(string.IsNullOrWhiteSpace(lookedForKeywordsText) ? "none" : lookedForKeywordsText)}. Matched keywords: {(string.IsNullOrWhiteSpace(matchedKeywordsText) ? "none" : matchedKeywordsText)}.");

            Shader shader = Shader.Find(shaderPath);
            if (shader == null)
                return Fail(result, $"The resolved shader could not be found: '{shaderPath}'.");

            string materialAssetPath = BuildMaterialAssetPath(jsonPath);
            result.MaterialAssetPath = materialAssetPath;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath) ?? CreateMaterial(shader, materialAssetPath, out result.Created);
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Materials,
                $"Using {(result.Created ? "new" : "existing")} material asset '{materialAssetPath}'.");

            if (!result.Created)
                Undo.RecordObject(material, "Update Material Shader From Json");

            material.shader = shader;

            ApplyMaterialJson(game, material, jsonPath, materialJson, result.Applied);
            EditorUtility.SetDirty(material);
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Materials,
                $"Applied '{jsonPath}' to '{material.name}': textures={result.Applied.TextureCount}, floats={result.Applied.FloatCount}, ints={result.Applied.IntCount}, colors={result.Applied.ColorCount}, keywords+={result.Applied.KeywordEnabledCount}, keywords-={result.Applied.KeywordDisabledCount}, tags={result.Applied.TagCount}, disabledPasses={result.Applied.DisabledPassCount}.");

            result.Success = true;
            result.Message = result.Created
                ? "Created the material and applied the JSON properties."
                : "Updated the material and applied the JSON properties.";

            string characterName = CharacterNameDetector.TryExtractCharacterName(game.Key, jsonPath);
            HoyoToonLogger.Info(
                HoyoToonLogCategory.Materials,
                $"Material generation result: {(result.Created ? "created material" : "updated material")}. " +
                $"Json={result.JsonAssetPath}; Material={result.MaterialAssetPath ?? "-"}; Shader={result.ShaderPath ?? "-"} ({result.ShaderSource ?? "-"}); " +
                $"Character={characterName ?? "-"}; Mode={(result.Created ? "Created" : "Updated")}; Message={result.Message ?? "-"}; " +
                $"Applied: textures={result.Applied.TextureCount}, floats={result.Applied.FloatCount}, ints={result.Applied.IntCount}, colors={result.Applied.ColorCount}, " +
                $"keywords+={result.Applied.KeywordEnabledCount}, keywords-={result.Applied.KeywordDisabledCount}, tags={result.Applied.TagCount}, disabledPasses={result.Applied.DisabledPassCount}");
            return result;
        }

        private static Material CreateMaterial(Shader shader, string assetPath, out bool created)
        {
            var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(assetPath) };
            AssetDatabase.CreateAsset(mat, assetPath);
            created = true;
            return mat;
        }

        private static void ApplyMaterialJson(GameConfigSO game, Material material, string jsonPath, MaterialJson materialJson, AppliedSettings applied)
        {
            if (material == null || materialJson == null) return;

            ApplySavedProperties(game, material, jsonPath, materialJson.m_SavedProperties, applied);
            TexturePropertyMappings.Apply(game, material, textureName => ResolveTexture(game, textureName), ref applied.TextureCount);
            ApplyKeywords(material, materialJson, applied);
            ApplyMaterialFlags(material, materialJson);
            ApplyTags(material, materialJson, applied);
            ApplyDisabledPasses(material, materialJson.m_DisabledShaderPasses, applied);
        }

        private static void ApplySavedProperties(GameConfigSO game, Material material, string jsonPath, SavedProperties saved, AppliedSettings applied)
        {
            if (saved == null) return;

            ApplySavedPropertyMap(material, saved.m_TexEnvs, (target, name, texEnv) =>
            {
                target.SetTexture(name, ResolveTexture(game, texEnv?.m_Texture));
                if (texEnv?.m_Scale != null)
                {
                    target.SetTextureScale(name, new Vector2(texEnv.m_Scale.X, texEnv.m_Scale.Y));
                }

                if (texEnv?.m_Offset != null)
                {
                    target.SetTextureOffset(name, new Vector2(texEnv.m_Offset.X, texEnv.m_Offset.Y));
                }
            }, ref applied.TextureCount);

            ApplySavedPropertyMap(material, saved.m_Floats, (target, name, value) => target.SetFloat(name, value), ref applied.FloatCount);
            ApplySavedPropertyMap(material, saved.m_Ints, (target, name, value) => target.SetInt(name, value), ref applied.IntCount);
            ApplySavedPropertyMap(material, saved.m_Colors, (target, name, color) =>
                target.SetColor(name, new Color(color.r, color.g, color.b, color.a)), ref applied.ColorCount);
        }

        private static void ApplySavedPropertyMap<T>(
            Material material,
            IDictionary<string, T> properties,
            Action<Material, string, T> applyValue,
            ref int appliedCount)
        {
            if (material == null || properties == null)
            {
                return;
            }

            foreach ((string name, T value) in properties)
            {
                if (!CanApplyProperty(material, name))
                {
                    continue;
                }

                applyValue(material, name, value);
                appliedCount++;
            }
        }

        private static bool CanApplyProperty(Material material, string propertyName)
        {
            return material != null && !string.IsNullOrWhiteSpace(propertyName) && material.HasProperty(propertyName);
        }

        private static void ApplyKeywords(Material material, MaterialJson materialJson, AppliedSettings applied)
        {
            var enabled = new HashSet<string>(MaterialKeywordSetBuilder.Extract(materialJson.m_ShaderKeywords).Concat(MaterialKeywordSetBuilder.Extract(materialJson.m_ValidKeywords)), StringComparer.Ordinal);
            var disabled = new HashSet<string>(MaterialKeywordSetBuilder.Extract(materialJson.m_InvalidKeywords), StringComparer.Ordinal);

            enabled.ExceptWith(disabled);

            material.shaderKeywords = enabled.ToArray();
            foreach (var k in enabled) material.EnableKeyword(k);
            foreach (var k in disabled) material.DisableKeyword(k);

            applied.KeywordEnabledCount = enabled.Count;
            applied.KeywordDisabledCount = disabled.Count;
        }

        private static void ApplyMaterialFlags(Material material, MaterialJson json)
        {
            if (json.m_CustomRenderQueue.HasValue) material.renderQueue = json.m_CustomRenderQueue.Value;
            if (json.m_LightmapFlags.HasValue) material.globalIlluminationFlags = (MaterialGlobalIlluminationFlags)json.m_LightmapFlags.Value;
            if (MaterialValueCoercion.TryCoerceBoolean(json.m_EnableInstancingVariants, out bool instancing))
                material.enableInstancing = instancing;
        }

        private static void ApplyTags(Material material, MaterialJson json, AppliedSettings applied)
        {
            int count = 0;
            void ApplyDict(IDictionary<string, string> dict)
            {
                if (dict == null) return;
                foreach (var (k, v) in dict)
                    if (TryApplyTag(material, k, v)) count++;
            }
            ApplyDict(json.m_StringTagMap);
            ApplyDict(json.stringTagMap);
            applied.TagCount = count;
        }

        private static void ApplyDisabledPasses(Material material, IReadOnlyList<string> passes, AppliedSettings applied)
        {
            if (passes == null) return;
            int count = 0;
            foreach (var p in passes)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                material.SetShaderPassEnabled(p, false);
                count++;
            }
            applied.DisabledPassCount = count;
        }

        private static bool TryApplyTag(Material mat, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (key.Equals("QUEUE", StringComparison.OrdinalIgnoreCase) && TryParseQueueTag(value, out int q))
            {
                mat.renderQueue = q;
                return true;
            }

            mat.SetOverrideTag(key, value ?? string.Empty);
            return true;
        }

        private static Texture ResolveTexture(GameConfigSO game, TextureRef texRef)
        {
            if (texRef == null || texRef.IsNull || string.IsNullOrWhiteSpace(texRef.Name)) return null;

            return ResolveTexture(game, texRef.Name);
        }

        private static Texture ResolveTexture(GameConfigSO game, string textureName)
        {
            if (string.IsNullOrWhiteSpace(textureName))
            {
                return null;
            }

            string assetPath = FindTextureAssetPath(textureName);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            TextureImportSettingsApplicator.Apply(game, assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
        }

        private static string FindTextureAssetPath(string textureName)
        {
            string requestedName = Path.GetFileNameWithoutExtension(textureName.Trim());
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                return null;
            }

            return AssetDatabase.FindAssets($"{requestedName} t:Texture")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && Path.GetFileNameWithoutExtension(p).Equals(requestedName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        private static string BuildMaterialAssetPath(string jsonPath)
        {
            string dir = Path.GetDirectoryName(jsonPath)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(jsonPath);
            return $"{dir}/{name}.mat";
        }

        private static bool TryParseQueueTag(string value, out int queue)
        {
            queue = -1;
            if (string.IsNullOrWhiteSpace(value)) return false;

            if (int.TryParse(value, out queue)) return true;

            string clean = value.Replace(" ", "");
            int offsetIndex = clean.IndexOf('+');
            if (offsetIndex < 0) offsetIndex = clean.IndexOf('-', 1);

            string baseName = offsetIndex > 0 ? clean.Substring(0, offsetIndex) : clean;
            string offsetStr = offsetIndex > 0 ? clean.Substring(offsetIndex) : "";
            if (!TryGetQueueBase(baseName, out int baseQueue)) return false;

            int offset = 0;
            if (!string.IsNullOrWhiteSpace(offsetStr) && !int.TryParse(offsetStr, out offset)) return false;

            queue = baseQueue + offset;
            return true;
        }

        private static bool TryGetQueueBase(string name, out int queue)
        {
            queue = name.ToLowerInvariant() switch
            {
                "background" => 1000,
                "geometry" => 2000,
                "alphatest" => 2450,
                "transparent" => 3000,
                "overlay" => 4000,
                _ => -1
            };
            return queue != -1;
        }

        private static ItemResult Fail(ItemResult r, string msg, HoyoToonLogCategory category = HoyoToonLogCategory.Materials)
        {
            r.Message = msg;
            HoyoToonLogger.Warning(
                category,
                $"Material generation result: failed. Json={r.JsonAssetPath ?? "-"}; Material={r.MaterialAssetPath ?? "-"}; Shader={r.ShaderPath ?? "-"} ({r.ShaderSource ?? "-"}); Message={msg}");
            return r;
        }
    }
}