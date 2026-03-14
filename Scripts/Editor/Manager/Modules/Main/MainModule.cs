#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.AssetPipeline.Models;
using HoyoToon;
using HoyoToon.Editor.UI.ManagerInspector.Setup;
using HoyoToon.Editor.Utilities;


namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class MainModule : ManagerModule
    {
        private const float LabelColumnWidth = 110f;
        private static readonly GUIContent s_TempContent = new GUIContent();
        private bool _showBaseInfo = true;
        private bool _showContentInfo = true;
        private bool _showFbxInfo = true;

        private GameObject _cachedModel;
        private ModelInfoSummary _cachedInfo;
        private bool _hasCachedInfo;

        public override string DisplayName => "Main";
        internal override string NavbarTourTarget => "tour.modules.main";

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                return;
            }

            DrawModelInfoSection(targetManager);
        }

        private readonly struct ModelInfoSummary
        {
            public readonly string GameKey;
            public readonly string CharacterName;
            public readonly string AssetPath;
            public readonly int MaterialCount;
            public readonly int TextureCount;
            public readonly int VertexCount;
            public readonly HoyoToonModelImportSnapshot Snapshot;
            public readonly bool HasSnapshot;

            public ModelInfoSummary(string gameKey, string characterName, string assetPath, int materialCount, int textureCount, int vertexCount, HoyoToonModelImportSnapshot snapshot, bool hasSnapshot)
            {
                GameKey = gameKey;
                CharacterName = characterName;
                AssetPath = assetPath;
                MaterialCount = materialCount;
                TextureCount = textureCount;
                VertexCount = vertexCount;
                Snapshot = snapshot;
                HasSnapshot = hasSnapshot;
            }
        }

        private void DrawModelInfoSection(HoyoToonManager manager)
        {
            var activeModel = manager != null ? manager.ActiveModel : null;

            if (activeModel != _cachedModel)
            {
                _cachedModel = activeModel;
                _hasCachedInfo = activeModel != null && TryBuildModelInfo(manager, out _cachedInfo);
            }

            if (!_hasCachedInfo)
            {
                ManagerModelCache.Clear(manager);
                return;
            }

            var info = _cachedInfo;

            ManagerModelCache.SetGameInfo(manager, info.GameKey);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Model Info", EditorStyles.boldLabel);

                _showBaseInfo = DrawFoldoutSection("Base Info", _showBaseInfo, () =>
                {
                    DrawInfoRow("Game", info.GameKey);
                    DrawInfoRow("Character", info.CharacterName);
                });

                _showContentInfo = DrawFoldoutSection("Content", _showContentInfo, () =>
                {
                    DrawInfoRow("Materials", info.MaterialCount.ToString());
                    DrawInfoRow("Textures", info.TextureCount.ToString());
                });

                _showFbxInfo = DrawFoldoutSection("FBX Settings", _showFbxInfo, () =>
                {
                    if (info.HasSnapshot)
                    {
                        DrawInfoRow("Global Scale", info.Snapshot.globalScale.ToString("0.###"));
                        DrawInfoRow("Rig Type", info.Snapshot.importAnimation ? info.Snapshot.animationType.ToString() : "Disabled");
                        DrawInfoRow("Material Import", info.Snapshot.materialImportMode.ToString());
                        DrawInfoRow("Meshes", $"Skinned: {info.Snapshot.skinnedMeshCount} || Normal: {info.Snapshot.meshCount}");
                        DrawInfoRow("Bone count", info.Snapshot.boneCount.ToString());
                        DrawInfoRow("Vertices", info.VertexCount.ToString("N0"));
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Could not read FBX importer settings for this asset.", MessageType.Info);
                    }
                });
            }
        }

        private static void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelColumnWidth));
                s_TempContent.text = string.IsNullOrEmpty(value) ? "-" : value;
                EditorGUILayout.LabelField(s_TempContent, EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true));
            }
        }

        private static bool TryBuildModelInfo(HoyoToonManager manager, out ModelInfoSummary info)
        {
            info = default;
            if (manager == null)
            {
                return false;
            }

            var activeModel = manager.ActiveModel;
            if (activeModel == null)
            {
                return false;
            }

            if (!FbxAssetResolver.TryResolve(activeModel, out var sourceAsset, out string assetPath))
            {
                return false;
            }

            var (gameKey, _) = MaterialDetection.DetectGameAutoOnly(sourceAsset ?? activeModel, assetPath);
            if (string.IsNullOrEmpty(gameKey))
            {
                return false;
            }

            var character = MaterialDetection.TryExtractCharacterName(gameKey, assetPath);
            if (string.IsNullOrEmpty(character))
            {
                character = BuildFallbackName(activeModel.name);
            }

            var (materialCount, textureCount, vertexCount) = BuildRendererStats(activeModel);
            var hasSnapshot = ModelImportRulesApplier.TryReadModelSnapshot(assetPath, out var snapshot);
            info = new ModelInfoSummary(gameKey, character, assetPath, materialCount, textureCount, vertexCount, snapshot, hasSnapshot);
            return true;
        }

        private static (int materialCount, int textureCount, int vertexCount) BuildRendererStats(GameObject root)
        {
            if (root == null)
            {
                return (0, 0, 0);
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var processedMeshes = new HashSet<Mesh>();
            int vertexCount = 0;

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                TryAccumulateVertices(renderer, processedMeshes, ref vertexCount);

                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    continue;
                }

                foreach (var mat in sharedMaterials)
                {
                    if (mat == null || !materials.Add(mat))
                    {
                        continue;
                    }

                    try
                    {
                        var textureProps = mat.GetTexturePropertyNames();
                        if (textureProps == null)
                        {
                            continue;
                        }

                        foreach (var prop in textureProps)
                        {
                            if (string.IsNullOrEmpty(prop))
                            {
                                continue;
                            }

                            Texture tex = null;
                            try
                            {
                                tex = mat.GetTexture(prop);
                            }
                            catch (Exception ex)
                            {
                                LogSwallowedException($"Ignored texture read failure on '{mat.name}.{prop}'", ex);
                            }

                            if (tex != null)
                            {
                                textures.Add(tex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSwallowedException($"Ignored texture property scan failure on '{mat.name}'", ex);
                    }
                }
            }

            return (materials.Count, textures.Count, vertexCount);
        }

        private static void TryAccumulateVertices(Renderer renderer, HashSet<Mesh> processedMeshes, ref int vertexCount)
        {
            if (renderer == null || processedMeshes == null)
            {
                return;
            }

            if (!FbxAssetResolver.TryResolveSharedMesh(renderer, out var mesh))
            {
                return;
            }

            if (!processedMeshes.Add(mesh))
            {
                return;
            }

            try
            {
                vertexCount += mesh.vertexCount;
            }
            catch (Exception ex)
            {
                LogSwallowedException($"Ignored vertex count read failure on mesh '{mesh.name}'", ex);
            }
        }

        private static void LogSwallowedException(string message, Exception ex)
        {
            LogCore.LogCategory(HoyoToonLogger.Categories.Manager, $"[Debug] {message}: {ex.Message}");
        }

        private static string BuildFallbackName(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return "Unknown";
            }

            var trimmed = original.Replace('_', ' ').Replace('-', ' ').Trim();
            return string.IsNullOrEmpty(trimmed) ? original : trimmed;
        }

    }
}
#endif
