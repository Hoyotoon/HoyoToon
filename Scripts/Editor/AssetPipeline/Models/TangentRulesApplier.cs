#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utf8Json;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.AssetPipeline.Materials;

namespace HoyoToon.Editor.AssetPipeline.Models
{
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

        public static bool TryApplyFromConfigForAsset(string assetPathOrObject)
        {
            if (string.IsNullOrWhiteSpace(assetPathOrObject)) return false;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPathOrObject);
            if (go == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent rules: Selected asset is not a model GameObject: {assetPathOrObject}");
                return false;
            }
            return TryApplyFromConfig(go);
        }

        public static bool TryApplyFromConfig(GameObject go)
        {
            if (go == null) return false;
            var assetPath = TryResolveSourceAssetPath(go);
            var idForLog = string.IsNullOrEmpty(assetPath) ? go.name : assetPath;

            var (gameKey, src) = MaterialDetection.DetectGameAutoOnly(go, string.IsNullOrEmpty(assetPath) ? go.name : assetPath, silent: true);
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent rules: Could not detect game for '{idForLog}'. Skipping.");
                return false;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent rules: No metadata found for game '{gameKey}'.");
                return false;
            }

            var tangents = gameMeta.Tangents ?? new GameTangents();
            var status = (tangents.Status ?? string.Empty).Trim();
            var skip = (tangents.SkipMeshesContaining ?? new List<string>()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

            TangentMode mode = MapStatusToMode(status);
            return Apply(go, mode, skip);
        }

        private static string TryResolveSourceAssetPath(GameObject go)
        {
            if (go == null) return null;
            var path = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(path)) return path;

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

            return null;
        }

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

        public static bool Reset(GameObject model)
        {
            if (model == null) return false;

            var rootObject = GetRootParent(model);
            var map = LoadOriginalMap();

            string modelName = rootObject.name;
            if (!map.ContainsKey(modelName))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Tangent reset: No stored mesh paths found for model: {modelName}");
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
                if (CleanupMeshesFolder(rootObject, out var cleanedMeshesFolder))
                {
                    ImportFolderOrParent(cleanedMeshesFolder);
                }
                AssetDatabase.SaveAssets();
            }

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, anyMeshesReset
                ? $"Tangent rules: Reset completed for '{model.name}'."
                : $"Tangent rules: Nothing to reset for '{model.name}'.");
            return anyMeshesReset;
        }

        private static bool Generate(GameObject model, Func<Mesh, Mesh> processor, string[] skipNameContains)
        {
            if (model == null) return false;

            StoreOriginalMeshesForModel(model);
            string canonicalMeshesFolder = GetMeshesFolderPath(TryResolveSourceAssetPath(model));

            bool processAllChildren = true;
            bool changed = false;
            var generatedBySourceMesh = new Dictionary<int, Mesh>();
            var touchedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var touchedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ensuredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool EnsureFolderExists(string folderPath)
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    return false;
                }

                if (ensuredFolders.Contains(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                {
                    ensuredFolders.Add(folderPath);
                    return true;
                }

                var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
                var folderName = Path.GetFileName(folderPath);
                if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
                {
                    return false;
                }

                if (!AssetDatabase.IsValidFolder(parent))
                {
                    return false;
                }

                string createdFolderGuid = AssetDatabase.CreateFolder(parent, folderName);
                bool created = !string.IsNullOrEmpty(createdFolderGuid);
                if (created || AssetDatabase.IsValidFolder(folderPath))
                {
                    ensuredFolders.Add(folderPath);
                    return true;
                }

                return false;
            }

            void HandleMesh(string componentName, ref Mesh mesh)
            {
                if (mesh == null) return;

                int sourceMeshId = mesh.GetInstanceID();
                string meshAssetPath = AssetDatabase.GetAssetPath(mesh);
                if (IsTangentMesh(meshAssetPath)) return;

                if (NameMatchesAny(componentName, skipNameContains))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent rules: Skipping mesh '{componentName}' due to skip rules.");
                    return;
                }

                if (generatedBySourceMesh.TryGetValue(sourceMeshId, out var cachedMesh) && cachedMesh != null)
                {
                    mesh = cachedMesh;
                    changed = true;
                    return;
                }

                var newMesh = processor(mesh);
                if (newMesh == null) return;
                newMesh.name = mesh.name;

                string meshesFolder = canonicalMeshesFolder;
                if (string.IsNullOrEmpty(meshesFolder))
                {
                    meshesFolder = GetMeshesFolderPath(meshAssetPath);
                }

                if (string.IsNullOrEmpty(meshesFolder))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Tangent rules: Could not resolve output Meshes folder for '{mesh.name}'.");
                    UnityEngine.Object.DestroyImmediate(newMesh);
                    return;
                }

                if (!EnsureFolderExists(meshesFolder))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Tangent rules: Failed to ensure Meshes folder '{meshesFolder}'.");
                    UnityEngine.Object.DestroyImmediate(newMesh);
                    return;
                }

                touchedFolders.Add(meshesFolder);
                string newPath = meshesFolder + "/" + newMesh.name + ".asset";

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(newPath);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(newPath);
                    touchedAssetPaths.Add(newPath);
                }

                AssetDatabase.CreateAsset(newMesh, newPath);
                touchedAssetPaths.Add(newPath);
                generatedBySourceMesh[sourceMeshId] = newMesh;
                mesh = newMesh;
                changed = true;
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent rules: Created mesh asset '{newPath}'.");
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(processAllChildren))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    var m = mf.sharedMesh;
                    HandleMesh(mf.name, ref m);
                    mf.sharedMesh = m;
                }

                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(processAllChildren))
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    var m = smr.sharedMesh;
                    HandleMesh(smr.name, ref m);
                    smr.sharedMesh = m;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                foreach (var path in touchedAssetPaths)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                foreach (var folder in touchedFolders)
                {
                    ImportFolderOrParent(folder);
                }
            }
            return changed;
        }

        private static string GetMeshesFolderPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            return folder.EndsWith("/Meshes", StringComparison.OrdinalIgnoreCase)
                ? folder
                : folder + "/Meshes";
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
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Failed to store original meshes for '{root?.name}': {ex.Message}");
            }
        }

        private static Dictionary<string, Dictionary<string, string[]>> LoadOriginalMap()
        {
            try
            {
                if (File.Exists(OriginalMeshPathsFile))
                {
                    var bytes = File.ReadAllBytes(OriginalMeshPathsFile);
                    if (Api.Parser.TryParse<Dictionary<string, Dictionary<string, string[]>>>(bytes, out var dict, out var _))
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
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Failed to save original mesh map: {ex.Message}");
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
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Tangent reset: Asset path not found for GUID: {guid} (mesh: {meshName})");
                    return null;
                }
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var a in assets)
                {
                    if (a is Mesh orig && orig.name == storedMeshName)
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent reset: Restored original mesh: {meshName}");
                        return orig;
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Tangent reset: Failed to restore '{meshName}': {ex.Message}");
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

        private static bool CleanupMeshesFolder(GameObject rootObject, out string cleanedMeshesFolder)
        {
            cleanedMeshesFolder = null;
            try
            {
                if (rootObject == null) return false;

                string meshesFolderUnity = null;

                foreach (var mf in rootObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf?.sharedMesh == null) continue;
                    string unityPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                    if (string.IsNullOrEmpty(unityPath)) continue;
                    var candidate = GetMeshesFolderPath(unityPath);
                    if (AssetDatabase.IsValidFolder(candidate)) { meshesFolderUnity = candidate; break; }
                    var abs = EditorUtil.ToAbsolutePath(candidate);
                    if (Directory.Exists(abs)) { meshesFolderUnity = candidate; break; }
                }

                if (string.IsNullOrEmpty(meshesFolderUnity))
                {
                    foreach (var smr in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr?.sharedMesh == null) continue;
                        string unityPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                        if (string.IsNullOrEmpty(unityPath)) continue;
                        var candidate = GetMeshesFolderPath(unityPath);
                        if (AssetDatabase.IsValidFolder(candidate)) { meshesFolderUnity = candidate; break; }
                        var abs = EditorUtil.ToAbsolutePath(candidate);
                        if (Directory.Exists(abs)) { meshesFolderUnity = candidate; break; }
                    }
                }

                if (string.IsNullOrEmpty(meshesFolderUnity)) return false;

                var meshesFolderAbs = EditorUtil.ToAbsolutePath(meshesFolderUnity);
                bool deletedAnyAsset = false;
                if (Directory.Exists(meshesFolderAbs))
                {
                    foreach (var file in Directory.GetFiles(meshesFolderAbs, "*.asset"))
                    {
                        var unityPath = EditorUtil.AbsoluteToUnityPath(file);
                        if (!string.IsNullOrEmpty(unityPath) && AssetDatabase.DeleteAsset(unityPath))
                        {
                            deletedAnyAsset = true;
                        }
                    }
                }

                bool deletedFolder = false;
                bool isEmpty = Directory.Exists(meshesFolderAbs)
                    && Directory.GetFiles(meshesFolderAbs).Length == 0
                    && Directory.GetDirectories(meshesFolderAbs).Length == 0;
                if (isEmpty)
                {
                    deletedFolder = AssetDatabase.DeleteAsset(meshesFolderUnity);
                }

                cleanedMeshesFolder = meshesFolderUnity;

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Tangent reset: Cleaned up Meshes folder: {meshesFolderUnity}");
                return deletedAnyAsset || deletedFolder;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Failed to cleanup Meshes folder: {ex.Message}");
                return false;
            }
        }

        private static void ImportFolderOrParent(string folderUnityPath)
        {
            if (string.IsNullOrEmpty(folderUnityPath)) return;
            if (AssetDatabase.IsValidFolder(folderUnityPath))
            {
                AssetDatabase.ImportAsset(folderUnityPath, ImportAssetOptions.ForceUpdate);
                return;
            }

            var normalized = folderUnityPath.Replace('\\', '/').TrimEnd('/');
            var slash = normalized.LastIndexOf('/');
            if (slash <= 0) return;

            var parent = normalized.Substring(0, slash);
            if (AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.ImportAsset(parent, ImportAssetOptions.ForceUpdate);
            }
        }

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
                tangents[i] = new Vector4(normal.x, normal.y, normal.z, mesh.tangents[i].w);

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
            {
                tangents = new Vector4[vertices.Length];
                for (int i = 0; i < tangents.Length; i++) tangents[i] = new Vector4(1, 0, 0, 0);
            }

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
            if (string.IsNullOrEmpty(status)) return TangentMode.Generate;
            if (status.Equals("No Tangents", StringComparison.OrdinalIgnoreCase)) return TangentMode.None;
            if (status.Equals("Tangent Generation", StringComparison.OrdinalIgnoreCase)) return TangentMode.Generate;
            if (status.Equals("Vertex Color", StringComparison.OrdinalIgnoreCase)) return TangentMode.FromVertexColor;
            return TangentMode.Generate;
        }
    }
}
#endif
