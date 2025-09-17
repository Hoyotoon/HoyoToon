#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utf8Json;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;
using HoyoToon.Materials;

namespace HoyoToon.Models
{
    /// <summary>
    /// Applies tangent reset/regeneration rules for a Model (FBX) based on per-game metadata config.
    /// This operates on meshes contained in the selected model asset and writes generated meshes
    /// to a sibling "Meshes" folder, mirroring HoyoToonMeshManager behavior.
    /// </summary>
    public static class TangentRulesApplier
    {
        public enum TangentMode
        {
            None,           // Reset/remove custom tangents (restore originals)
            Generate,       // Procedural generation (ModifyMeshTangents algorithm)
            FromVertexColor // Move vertex colors into tangents (MoveColors algorithm)
        }

        // Keep compatibility with HoyoToonMeshManager's storage format so ResetTangents works
        private static readonly string HoyoToonFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HoyoToon");
        private static readonly string OriginalMeshPathsFile = Path.Combine(HoyoToonFolder, "OriginalMeshPaths.json");

        /// <summary>
        /// Entry (asset path): Detect game and apply tangent behavior from config.
        /// Use only for Project assets; for Scene objects, prefer TryApplyFromConfig(GameObject).
        /// </summary>
        public static bool TryApplyFromConfigForAsset(string assetPathOrObject)
        {
            if (string.IsNullOrWhiteSpace(assetPathOrObject)) return false;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPathOrObject);
            if (go == null)
            {
                HoyoToonLogger.ModelInfo($"Tangent rules: Selected asset is not a model GameObject: {assetPathOrObject}");
                return false;
            }
            return TryApplyFromConfig(go);
        }

        /// <summary>
        /// Entry (GameObject): Works for both Project assets and Scene hierarchy selections.
        /// Detects the game using materials context and applies per-game Tangents config.
        /// Returns true if any changes were made.
        /// </summary>
        public static bool TryApplyFromConfig(GameObject go)
        {
            if (go == null) return false;
            // Resolve a source asset path for detection when working with Scene objects.
            var assetPath = TryResolveSourceAssetPath(go);
            var idForLog = string.IsNullOrEmpty(assetPath) ? go.name : assetPath;

            var (gameKey, src) = MaterialDetection.DetectGameAutoOnly(go, string.IsNullOrEmpty(assetPath) ? go.name : assetPath);
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.ModelInfo($"Tangent rules: Could not detect game for '{idForLog}'. Skipping.");
                return false;
            }

            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.ModelInfo($"Tangent rules: No metadata found for game '{gameKey}'.");
                return false;
            }

            var tangents = gameMeta.Tangents ?? new GameTangents();
            var status = (tangents.Status ?? string.Empty).Trim();
            var skip = (tangents.SkipMeshesContaining ?? new List<string>()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

            TangentMode mode = MapStatusToMode(status);
            return Apply(go, mode, skip);
        }

        /// <summary>
        /// Try to resolve the backing source asset path of a GameObject. For Project assets this is the
        /// object's asset path; for Scene instances, this tries to locate the corresponding source model prefab
        /// or, failing that, the path of any referenced sharedMesh.
        /// </summary>
        private static string TryResolveSourceAssetPath(GameObject go)
        {
            if (go == null) return null;
            // If this is a Project asset (model prefab), return directly
            var path = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(path)) return path;

            // Prefer the outermost prefab instance root, then map to source asset
            var root = GetRootParent(go) ?? go;
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                var sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (sourceRoot != null)
                {
                    var srcPath = AssetDatabase.GetAssetPath(sourceRoot);
                    if (!string.IsNullOrEmpty(srcPath)) return srcPath;
                }
            }

            // Fallback: use the first mesh asset path we can find
            var mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
            {
                var mp = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(mp)) return mp;
            }
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null)
            {
                var mp = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(mp)) return mp;
            }

            // Last resort: no path; return null so caller can fallback to name-based log id
            return null;
        }

        /// <summary>
        /// Apply a specific tangent mode to the model. Returns true if any changes applied.
        /// </summary>
        public static bool Apply(GameObject model, TangentMode mode, string[] skipNameContains = null)
        {
            if (model == null) return false;
            skipNameContains = skipNameContains ?? Array.Empty<string>();

            switch (mode)
            {
                case TangentMode.None:
                    return Reset(model);
                case TangentMode.Generate:
                    return Generate(model, ProcessTangents_ModifyMeshTangents, skipNameContains);
                case TangentMode.FromVertexColor:
                    return Generate(model, ProcessTangents_MoveColors, skipNameContains);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reset tangents by restoring original meshes (from stored GUID map) and cleaning up generated Meshes folder.
        /// </summary>
        public static bool Reset(GameObject model)
        {
            if (model == null) return false;

            var rootObject = GetRootParent(model);
            var map = LoadOriginalMap();

            string modelName = rootObject.name;
            if (!map.ContainsKey(modelName))
            {
                HoyoToonLogger.ModelWarning($"Tangent reset: No stored mesh paths found for model: {modelName}");
                return false;
            }

            bool processAllChildren = true;
            bool anyMeshesReset = false;

            void TryRestoreFor(ref Mesh mesh)
            {
                if (mesh == null) return;
                string currentMeshPath = AssetDatabase.GetAssetPath(mesh);
                if (IsTangentMesh(currentMeshPath) || HasOriginalMeshStored(map, modelName, mesh.name))
                {
                    var restored = RestoreOriginalMesh(map, modelName, mesh.name);
                    if (restored != null)
                    {
                        mesh = restored;
                        anyMeshesReset = true;
                    }
                }
            }

            foreach (var mf in rootObject.GetComponentsInChildren<MeshFilter>(processAllChildren))
            {
                if (mf?.sharedMesh == null) continue;
                var m = mf.sharedMesh;
                TryRestoreFor(ref m);
                mf.sharedMesh = m;
            }

            foreach (var smr in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(processAllChildren))
            {
                if (smr?.sharedMesh == null) continue;
                var m = smr.sharedMesh;
                TryRestoreFor(ref m);
                smr.sharedMesh = m;
            }

            if (anyMeshesReset)
            {
                CleanupMeshesFolder(rootObject);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            HoyoToonLogger.ModelInfo(anyMeshesReset
                ? $"Tangent rules: Reset completed for '{model.name}'."
                : $"Tangent rules: Nothing to reset for '{model.name}'.");
            return anyMeshesReset;
        }

        private static bool Generate(GameObject model, Func<Mesh, Mesh> processor, string[] skipNameContains)
        {
            if (model == null) return false;

            // Store original meshes (compatible format) so reset works later
            StoreOriginalMeshesForModel(model);

            bool processAllChildren = true; // apply to whole model by default
            bool changed = false;

            void HandleMesh(string componentName, ref Mesh mesh)
            {
                if (mesh == null) return;

                string meshAssetPath = AssetDatabase.GetAssetPath(mesh);
                if (IsTangentMesh(meshAssetPath)) return; // already processed

                if (NameMatchesAny(componentName, skipNameContains))
                {
                    HoyoToonLogger.ModelInfo($"Tangent rules: Skipping mesh '{componentName}' due to skip rules.");
                    return;
                }

                var newMesh = processor(mesh);
                if (newMesh == null) return;
                newMesh.name = mesh.name;

                // Save in sibling Meshes folder next to original mesh asset
                string unityPath = meshAssetPath; // this is already in Assets/... format
                string folder = Path.GetDirectoryName(unityPath).Replace('\\', '/');
                string meshesFolder = folder + "/Meshes";
                if (!AssetDatabase.IsValidFolder(meshesFolder))
                {
                    var parent = folder;
                    AssetDatabase.CreateFolder(parent, "Meshes");
                }
                string newPath = meshesFolder + "/" + newMesh.name + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(newPath);
                if (existing != null) AssetDatabase.DeleteAsset(newPath);

                AssetDatabase.CreateAsset(newMesh, newPath);
                mesh = newMesh; // reassign to component
                changed = true;
                HoyoToonLogger.ModelInfo($"Tangent rules: Created mesh asset '{newPath}'.");
            }

            // MeshFilter
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(processAllChildren))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh;
                HandleMesh(mf.name, ref m);
                mf.sharedMesh = m;
            }

            // SkinnedMeshRenderer
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(processAllChildren))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var m = smr.sharedMesh;
                HandleMesh(smr.name, ref m);
                smr.sharedMesh = m;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            return changed;
        }

        private static bool NameMatchesAny(string name, IEnumerable<string> patterns)
        {
            if (string.IsNullOrEmpty(name) || patterns == null) return false;
            foreach (var p in patterns)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool IsTangentMesh(string meshPath)
        {
            return !string.IsNullOrEmpty(meshPath) && meshPath.Replace('\\', '/').Contains("/Meshes/") && meshPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static void StoreOriginalMeshesForModel(GameObject root)
        {
            try
            {
                var map = LoadOriginalMap();
                var modelName = root.name;
                if (!map.ContainsKey(modelName)) map[modelName] = new Dictionary<string, string[]>();

                void Store(Mesh mesh)
                {
                    if (mesh == null) return;
                    string assetPath = AssetDatabase.GetAssetPath(mesh);
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    map[modelName][mesh.name] = new[] { guid, mesh.name };
                }

                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf?.sharedMesh != null) Store(mf.sharedMesh);
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr?.sharedMesh != null) Store(smr.sharedMesh);

                SaveOriginalMap(map);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Failed to store original meshes for '{root?.name}': {ex.Message}");
            }
        }

        private static Dictionary<string, Dictionary<string, string[]>> LoadOriginalMap()
        {
            try
            {
                if (File.Exists(OriginalMeshPathsFile))
                {
                    var bytes = File.ReadAllBytes(OriginalMeshPathsFile);
                    if (HoyoToonApi.Parser.TryParse<Dictionary<string, Dictionary<string, string[]>>>(bytes, out var dict, out var _))
                        return dict ?? new Dictionary<string, Dictionary<string, string[]>>();
                }
            }
            catch (Exception)
            {
                // Ignore JSON parse/IO errors; return empty map for robustness.
            }
            return new Dictionary<string, Dictionary<string, string[]>>();
        }

        private static void SaveOriginalMap(Dictionary<string, Dictionary<string, string[]>> map)
        {
            try
            {
                if (!Directory.Exists(HoyoToonFolder)) Directory.CreateDirectory(HoyoToonFolder);
                var bytes = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(map));
                File.WriteAllBytes(OriginalMeshPathsFile, bytes);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Failed to save original mesh map: {ex.Message}");
            }
        }

        private static bool HasOriginalMeshStored(Dictionary<string, Dictionary<string, string[]>> map, string modelName, string meshName)
        {
            return map != null && map.ContainsKey(modelName) && map[modelName] != null && map[modelName].ContainsKey(meshName);
        }

        private static Mesh RestoreOriginalMesh(Dictionary<string, Dictionary<string, string[]>> map, string modelName, string meshName)
        {
            try
            {
                if (!HasOriginalMeshStored(map, modelName, meshName)) return null;
                var entry = map[modelName][meshName];
                if (entry == null || entry.Length < 2) return null;
                string guid = entry[0];
                string storedMeshName = entry[1];
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    HoyoToonLogger.ModelWarning($"Tangent reset: Asset path not found for GUID: {guid} (mesh: {meshName})");
                    return null;
                }
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var a in assets)
                {
                    if (a is Mesh orig && orig.name == storedMeshName)
                    {
                        HoyoToonLogger.ModelInfo($"Tangent reset: Restored original mesh: {meshName}");
                        return orig;
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Tangent reset: Failed to restore '{meshName}': {ex.Message}");
            }
            return null;
        }

        private static GameObject GetRootParent(GameObject obj)
        {
            if (obj == null) return null;
            var current = obj;
            while (current.transform.parent != null)
                current = current.transform.parent.gameObject;

            if (PrefabUtility.IsPartOfPrefabInstance(current))
            {
                var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(current);
                if (prefabRoot != null) current = prefabRoot;
            }
            return current;
        }

        private static void CleanupMeshesFolder(GameObject rootObject)
        {
            try
            {
                if (rootObject == null) return;

                string meshesFolderUnity = null;

                // Try MeshFilters first
                foreach (var mf in rootObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf?.sharedMesh == null) continue;
                    string unityPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                    if (string.IsNullOrEmpty(unityPath)) continue;
                    var candidate = Path.GetDirectoryName(unityPath).Replace('\\', '/') + "/Meshes";
                    if (AssetDatabase.IsValidFolder(candidate)) { meshesFolderUnity = candidate; break; }
                    var abs = UnityPathToAbsolute(candidate);
                    if (Directory.Exists(abs)) { meshesFolderUnity = candidate; break; }
                }

                // Fallback: SkinnedMeshRenderers
                if (string.IsNullOrEmpty(meshesFolderUnity))
                {
                    foreach (var smr in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr?.sharedMesh == null) continue;
                        string unityPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                        if (string.IsNullOrEmpty(unityPath)) continue;
                        var candidate = Path.GetDirectoryName(unityPath).Replace('\\', '/') + "/Meshes";
                        if (AssetDatabase.IsValidFolder(candidate)) { meshesFolderUnity = candidate; break; }
                        var abs = UnityPathToAbsolute(candidate);
                        if (Directory.Exists(abs)) { meshesFolderUnity = candidate; break; }
                    }
                }

                if (string.IsNullOrEmpty(meshesFolderUnity)) return;

                // Delete all .asset files in the Meshes folder
                var meshesFolderAbs = UnityPathToAbsolute(meshesFolderUnity);
                if (Directory.Exists(meshesFolderAbs))
                {
                    foreach (var file in Directory.GetFiles(meshesFolderAbs, "*.asset"))
                    {
                        var unityPath = AbsoluteToUnityPath(file);
                        if (!string.IsNullOrEmpty(unityPath)) AssetDatabase.DeleteAsset(unityPath);
                    }
                }

                // Remove folder if empty
                bool isEmpty = Directory.Exists(meshesFolderAbs)
                    && Directory.GetFiles(meshesFolderAbs).Length == 0
                    && Directory.GetDirectories(meshesFolderAbs).Length == 0;
                if (isEmpty)
                {
                    AssetDatabase.DeleteAsset(meshesFolderUnity);
                }

                HoyoToonLogger.ModelInfo($"Tangent reset: Cleaned up Meshes folder: {meshesFolderUnity}");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Failed to cleanup Meshes folder: {ex.Message}");
            }
        }

        private static string UnityPathToAbsolute(string unityPath)
            => HoyoToonEditorUtil.UnityPathToAbsolute(unityPath);

        private static string AbsoluteToUnityPath(string absPath)
            => HoyoToonEditorUtil.AbsoluteToUnityPath(absPath);

        // --- Tangent processors (modular, self-contained copies of HoyoToonMeshManager algorithms) ---
        private static Mesh ProcessTangents_ModifyMeshTangents(Mesh mesh)
        {
            if (mesh == null) return null;
            var newMesh = UnityEngine.Object.Instantiate(mesh);

            var vertices = newMesh.vertices;
            var triangles = newMesh.triangles;
            var unmerged = new Vector3[newMesh.vertexCount];
            var merged = new Dictionary<Vector3, Vector3>();
            var tangents = new Vector4[newMesh.vertexCount];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i + 0];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                var v0 = vertices[i0] * 100f;
                var v1 = vertices[i1] * 100f;
                var v2 = vertices[i2] * 100f;

                var normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                unmerged[i0] += normal * Vector3.Angle(v1 - v0, v2 - v0);
                unmerged[i1] += normal * Vector3.Angle(v0 - v1, v2 - v1);
                unmerged[i2] += normal * Vector3.Angle(v0 - v2, v1 - v2);
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!merged.ContainsKey(vertices[i]))
                    merged[vertices[i]] = unmerged[i];
                else
                    merged[vertices[i]] += unmerged[i];
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                var normal = merged[vertices[i]].normalized;
                tangents[i] = new Vector4(normal.x, normal.y, normal.z, 0f);
            }

            newMesh.tangents = tangents;
            return newMesh;
        }

        private static Mesh ProcessTangents_MoveColors(Mesh mesh)
        {
            if (mesh == null) return null;
            var newMesh = UnityEngine.Object.Instantiate(mesh);

            var vertices = newMesh.vertices;
            var tangents = newMesh.tangents;
            var colors = newMesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                newMesh.colors = colors;
            }

            if (tangents == null || tangents.Length != vertices.Length)
                tangents = Enumerable.Repeat(new Vector4(1, 0, 0, 0), vertices.Length).ToArray();

            for (int i = 0; i < vertices.Length; i++)
            {
                tangents[i].x = colors[i].r * 2f - 1f;
                tangents[i].y = colors[i].g * 2f - 1f;
                tangents[i].z = colors[i].b * 2f - 1f;
            }
            newMesh.SetTangents(tangents);
            return newMesh;
        }

        private static TangentMode MapStatusToMode(string status)
        {
            if (string.IsNullOrEmpty(status)) return TangentMode.Generate; // default behavior
            if (status.Equals("No Tangents", StringComparison.OrdinalIgnoreCase)) return TangentMode.None;
            if (status.Equals("Tangent Generation", StringComparison.OrdinalIgnoreCase)) return TangentMode.Generate;
            if (status.Equals("Vertex Color", StringComparison.OrdinalIgnoreCase)) return TangentMode.FromVertexColor;
            return TangentMode.Generate;
        }
    }
}
#endif
