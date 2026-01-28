#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Materials;
using HoyoToon.Models;
using HoyoToon;
using HoyoToon.EditorTools.ManagerUI.Utilities;


namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal sealed class MainModule : HoyoToonManagerModule
    {
        private const float LabelColumnWidth = 110f;
        private static readonly GUIContent s_TempContent = new GUIContent();
        private bool _showBaseInfo = true;
        private bool _showContentInfo = true;
        private bool _showFbxInfo = true;

        public override string DisplayName => "Main";

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
            if (!TryBuildModelInfo(manager, out var info))
            {
                HoyoToonManagerModelCache.Clear(manager);
                return;
            }

            HoyoToonManagerModelCache.SetGameInfo(manager, info.GameKey);

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

            if (!TryResolveSourceFbxPath(activeModel, out var assetPath))
            {
                return false;
            }

            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

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
                            catch
                            {
                                // Ignore per-property exceptions to keep stats resilient.
                            }

                            if (tex != null)
                            {
                                textures.Add(tex);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore materials without accessible texture properties.
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

            var mesh = ResolveSharedMesh(renderer);

            if (mesh == null)
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
            catch
            {
                // Ignore meshes that might throw due to streaming or read/write settings.
            }
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

        private static bool TryResolveSourceFbxPath(GameObject activeModel, out string assetPath)
        {
            assetPath = null;
            if (activeModel == null)
            {
                return false;
            }

            static bool IsFbx(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(activeModel);
            if (IsFbx(prefabPath))
            {
                assetPath = prefabPath;
                return true;
            }

            var directPath = AssetDatabase.GetAssetPath(activeModel);
            if (IsFbx(directPath))
            {
                assetPath = directPath;
                return true;
            }

            var renderers = activeModel.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var mesh = ResolveSharedMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                var meshPath = AssetDatabase.GetAssetPath(mesh);
                if (IsFbx(meshPath))
                {
                    assetPath = meshPath;
                    return true;
                }
            }

            return false;
        }

        private static Mesh ResolveSharedMesh(Renderer renderer)
        {
            switch (renderer)
            {
                case SkinnedMeshRenderer skinned:
                    return skinned.sharedMesh;
                case MeshRenderer meshRenderer:
                    var filter = meshRenderer != null ? meshRenderer.GetComponent<MeshFilter>() : null;
                    return filter != null ? filter.sharedMesh : null;
                default:
                    return null;
            }
        }
    }
}
#endif
