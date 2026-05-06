#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Utf8Json;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Models
{
    public enum TangentSettingsApplyOutcome
    {
        Applied,
        Unchanged,
        Skipped,
        Failed,
    }

    public static class TangentSettingsApplicator
    {
        public enum TangentMode
        {
            None,
            Generate,
            FromVertexColor,
        }

        private const string AssetSuffix = "/Config/GameTangentSettings.asset";

        // Keep compatibility with HoyoToon's historical storage format so Reset remains reversible.
        private static readonly string HoyoToonFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HoyoToon");
        private static readonly string OriginalMeshPathsFile = Path.Combine(HoyoToonFolder, "OriginalMeshPaths.json");

        private static Dictionary<string, GameTangentSettingsSO> settingsByGame =
            new Dictionary<string, GameTangentSettingsSO>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            settingsByGame = LoadGeneratedAssets<GameTangentSettingsSO>("t:GameTangentSettingsSO", AssetSuffix)
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.GameKey))
                .GroupBy(asset => asset.GameKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Models,
                $"Loaded tangent settings for {settingsByGame.Count} game(s).");
        }

        public static TangentSettingsApplyOutcome ApplyDetected(string modelAssetPath)
        {
            if (!TryLoadModelAsset(modelAssetPath, out GameObject model))
            {
                return TangentSettingsApplyOutcome.Skipped;
            }

            return Apply(model);
        }

        public static TangentSettingsApplyOutcome RegenerateDetected(string modelAssetPath)
        {
            if (!TryLoadModelAsset(modelAssetPath, out GameObject model))
            {
                return TangentSettingsApplyOutcome.Skipped;
            }

            return Regenerate(model);
        }

        public static TangentSettingsApplyOutcome Apply(GameObject model)
        {
            return Apply(model, regenerate: false);
        }

        public static TangentSettingsApplyOutcome Regenerate(GameObject model)
        {
            return Apply(model, regenerate: true);
        }

        public static bool TryApplyFromConfigForAsset(string assetPathOrObject)
        {
            return ApplyDetected(assetPathOrObject) == TangentSettingsApplyOutcome.Applied;
        }

        public static bool TryApplyFromConfig(GameObject go)
        {
            return Apply(go) == TangentSettingsApplyOutcome.Applied;
        }

        public static bool TryRegenerateFromConfig(GameObject go)
        {
            return Regenerate(go) == TangentSettingsApplyOutcome.Applied;
        }

        public static bool Reset(GameObject model)
        {
            if (model == null)
            {
                return false;
            }

            GameObject rootObject = GetRootParent(model) ?? model;
            Dictionary<string, Dictionary<string, string[]>> map = LoadOriginalMap();
            if (!TryGetStoredMeshEntries(map, rootObject, out Dictionary<string, string[]> storedEntries))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.Models, $"Tangent reset: No stored mesh paths found for model: {rootObject.name}");
                return false;
            }

            bool processAllChildren = true;
            bool anyMeshesReset = false;

            void TryRestoreFor(Component owner, ref Mesh mesh)
            {
                if (mesh == null)
                {
                    return;
                }

                string meshOwnerKey = GetMeshOwnerKey(rootObject, owner);
                string currentMeshPath = AssetDatabase.GetAssetPath(mesh);
                if (IsTangentMesh(currentMeshPath) || HasOriginalMeshStored(storedEntries, meshOwnerKey) || HasOriginalMeshStored(storedEntries, mesh.name))
                {
                    Mesh restored = RestoreOriginalMesh(storedEntries, meshOwnerKey, mesh.name);
                    if (restored != null)
                    {
                        mesh = restored;
                        anyMeshesReset = true;
                    }
                }
            }

            foreach (MeshFilter meshFilter in rootObject.GetComponentsInChildren<MeshFilter>(processAllChildren))
            {
                if (meshFilter?.sharedMesh == null)
                {
                    continue;
                }

                Mesh originalMesh = meshFilter.sharedMesh;
                Mesh mesh = originalMesh;
                TryRestoreFor(meshFilter, ref mesh);
                if (!ReferenceEquals(originalMesh, mesh))
                {
                    meshFilter.sharedMesh = mesh;
                    NotifyMeshComponentChanged(meshFilter);
                }
            }

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(processAllChildren))
            {
                if (skinnedMeshRenderer?.sharedMesh == null)
                {
                    continue;
                }

                Mesh originalMesh = skinnedMeshRenderer.sharedMesh;
                Mesh mesh = originalMesh;
                TryRestoreFor(skinnedMeshRenderer, ref mesh);
                if (!ReferenceEquals(originalMesh, mesh))
                {
                    skinnedMeshRenderer.sharedMesh = mesh;
                    NotifyMeshComponentChanged(skinnedMeshRenderer);
                }
            }

            if (anyMeshesReset)
            {
                if (CleanupMeshesFolder(rootObject, out string cleanedMeshesFolder))
                {
                    ImportFolderOrParent(cleanedMeshesFolder);
                }

                AssetDatabase.SaveAssets();
                FinalizeSceneMeshChanges(rootObject);
            }

            HoyoToonLogger.Info(
                HoyoToonLogCategory.Models,
                anyMeshesReset
                    ? $"Tangent rules: Reset completed for '{rootObject.name}'."
                    : $"Tangent rules: Nothing to reset for '{rootObject.name}'.");
            return anyMeshesReset;
        }

        private static TangentSettingsApplyOutcome Apply(GameObject model, bool regenerate)
        {
            if (model == null)
            {
                return TangentSettingsApplyOutcome.Skipped;
            }

            if (!TryResolveConfig(model, out TangentMode mode, out string[] skipNameContains))
            {
                return TangentSettingsApplyOutcome.Skipped;
            }

            try
            {
                bool changed = regenerate
                    ? RegenerateResolved(model, mode, skipNameContains)
                    : ApplyResolved(model, mode, skipNameContains);
                return changed ? TangentSettingsApplyOutcome.Applied : TangentSettingsApplyOutcome.Unchanged;
            }
            catch (Exception exception)
            {
                string sourceAssetPath = TryResolveSourceAssetPath(model);
                string modelId = string.IsNullOrWhiteSpace(sourceAssetPath) ? model.name : sourceAssetPath;
                HoyoToonLogger.Error(
                    HoyoToonLogCategory.Models,
                    $"Failed to {(regenerate ? "regenerate" : "apply")} tangent settings for '{modelId}'.",
                    exception,
                    context: model);
                return TangentSettingsApplyOutcome.Failed;
            }
        }

        private static bool TryResolveConfig(GameObject model, out TangentMode mode, out string[] skipNameContains)
        {
            EnsureInitialized();

            mode = TangentMode.Generate;
            skipNameContains = Array.Empty<string>();

            if (model == null)
            {
                return false;
            }

            string sourceAssetPath = TryResolveSourceAssetPath(model);
            string modelId = string.IsNullOrWhiteSpace(sourceAssetPath) ? model.name : sourceAssetPath;
            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent rules: Could not resolve a source model asset for '{modelId}'. Skipping.",
                    context: model);
                return false;
            }

            if (!GameDetector.TryDetectGameFromAssetContext(sourceAssetPath, out GameConfigSO game, out _) || game == null)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent rules: Could not detect game for '{modelId}'. Skipping.",
                    context: model);
                return false;
            }

            if (!settingsByGame.TryGetValue(game.Key, out GameTangentSettingsSO settings) || settings == null)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent rules: No generated tangent settings asset was found for game '{game.Key}'.",
                    context: game);
                return false;
            }

            string status = (settings.Status ?? string.Empty).Trim();
            skipNameContains = (settings.SkipMeshesContaining ?? Array.Empty<string>())
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .ToArray();
            mode = MapStatusToMode(status);
            return true;
        }

        private static bool TryLoadModelAsset(string modelAssetPath, out GameObject model)
        {
            model = null;

            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, "Tangent rules: No model asset path was provided.");
                return false;
            }

            model = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (model == null)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent rules: Selected asset is not a model GameObject: {modelAssetPath}");
                return false;
            }

            return true;
        }

        private static string TryResolveSourceAssetPath(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(go);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            GameObject root = GetRootParent(go) ?? go;
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(root);
                if (sourceRoot != null)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourceRoot);
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        return sourcePath;
                    }
                }
            }

            MeshFilter meshFilter = root.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                string meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    return meshPath;
                }
            }

            SkinnedMeshRenderer skinnedMeshRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            {
                string meshPath = AssetDatabase.GetAssetPath(skinnedMeshRenderer.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath))
                {
                    return meshPath;
                }
            }

            return null;
        }

        private static bool ApplyResolved(GameObject model, TangentMode mode, string[] skipNameContains = null)
        {
            if (model == null)
            {
                return false;
            }

            skipNameContains ??= Array.Empty<string>();
            GameObject targetModel = GetRootParent(model) ?? model;

            switch (mode)
            {
                case TangentMode.None:
                    return Reset(targetModel);
                case TangentMode.Generate:
                    return Generate(targetModel, ProcessTangents_ModifyMeshTangents, skipNameContains);
                case TangentMode.FromVertexColor:
                    return Generate(targetModel, ProcessTangents_MoveColors, skipNameContains);
                default:
                    return false;
            }
        }

        private static bool RegenerateResolved(GameObject model, TangentMode mode, string[] skipNameContains)
        {
            if (model == null)
            {
                return false;
            }

            bool resetChanged = Reset(model);
            if (mode == TangentMode.None)
            {
                return resetChanged;
            }

            return ApplyResolved(model, mode, skipNameContains);
        }

        private static bool Generate(GameObject model, Func<Mesh, Mesh> processor, string[] skipNameContains)
        {
            if (model == null)
            {
                return false;
            }

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch storeStopwatch = Stopwatch.StartNew();
            StoreOriginalMeshesForModel(model);
            storeStopwatch.Stop();
            string canonicalMeshesFolder = GetMeshesFolderPath(TryResolveSourceAssetPath(model));

            bool processAllChildren = true;
            bool changed = false;
            var generatedBySourceMesh = new Dictionary<int, Mesh>();
            var touchedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ensuredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int reusedMeshCount = 0;
            int generatedMeshCount = 0;
            int updatedMeshAssetCount = 0;

            var meshProcessStopwatch = new Stopwatch();
            var meshComputeStopwatch = new Stopwatch();
            var assetWriteStopwatch = new Stopwatch();

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

                string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
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
                if (mesh == null)
                {
                    return;
                }

                int sourceMeshId = mesh.GetInstanceID();
                string meshAssetPath = AssetDatabase.GetAssetPath(mesh);
                if (IsTangentMesh(meshAssetPath))
                {
                    return;
                }

                if (NameMatchesAny(componentName, skipNameContains))
                {
                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Models,
                        $"Tangent rules: Skipping mesh '{componentName}' due to skip rules.");
                    return;
                }

                if (generatedBySourceMesh.TryGetValue(sourceMeshId, out Mesh cachedMesh) && cachedMesh != null)
                {
                    mesh = cachedMesh;
                    changed = true;
                    return;
                }

                string meshesFolder = canonicalMeshesFolder;
                if (string.IsNullOrEmpty(meshesFolder))
                {
                    meshesFolder = GetMeshesFolderPath(meshAssetPath);
                }

                if (string.IsNullOrEmpty(meshesFolder))
                {
                    HoyoToonLogger.Warning(
                        HoyoToonLogCategory.Models,
                        $"Tangent rules: Could not resolve output Meshes folder for '{mesh.name}'.");
                    return;
                }

                string newPath = meshesFolder + "/" + mesh.name + ".asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(newPath);
                if (existing != null && CanReuseGeneratedMesh(meshAssetPath, newPath, mesh, existing))
                {
                    generatedBySourceMesh[sourceMeshId] = existing;
                    mesh = existing;
                    changed = true;
                    reusedMeshCount++;
                    return;
                }

                meshComputeStopwatch.Start();
                Mesh newMesh = processor(mesh);
                meshComputeStopwatch.Stop();
                if (newMesh == null)
                {
                    return;
                }

                newMesh.name = mesh.name;

                if (!EnsureFolderExists(meshesFolder))
                {
                    HoyoToonLogger.Warning(
                        HoyoToonLogCategory.Models,
                        $"Tangent rules: Failed to ensure Meshes folder '{meshesFolder}'.");
                    UnityEngine.Object.DestroyImmediate(newMesh);
                    return;
                }

                if (existing != null)
                {
                    assetWriteStopwatch.Start();
                    EditorUtility.CopySerialized(newMesh, existing);
                    EditorUtility.SetDirty(existing);
                    assetWriteStopwatch.Stop();

                    generatedBySourceMesh[sourceMeshId] = existing;
                    mesh = existing;
                    changed = true;
                    updatedMeshAssetCount++;
                    touchedAssetPaths.Add(newPath);
                    UnityEngine.Object.DestroyImmediate(newMesh);
                    return;
                }

                assetWriteStopwatch.Start();
                AssetDatabase.CreateAsset(newMesh, newPath);
                assetWriteStopwatch.Stop();
                touchedAssetPaths.Add(newPath);
                generatedBySourceMesh[sourceMeshId] = newMesh;
                mesh = newMesh;
                changed = true;
                generatedMeshCount++;
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, $"Tangent rules: Created mesh asset '{newPath}'.");
            }

            using (AssetDatabaseEditingScope.Begin(disallowAutoRefresh: false))
            {
                meshProcessStopwatch.Start();
                foreach (MeshFilter meshFilter in model.GetComponentsInChildren<MeshFilter>(processAllChildren))
                {
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    Mesh originalMesh = meshFilter.sharedMesh;
                    Mesh mesh = originalMesh;
                    HandleMesh(meshFilter.name, ref mesh);
                    if (!ReferenceEquals(originalMesh, mesh))
                    {
                        meshFilter.sharedMesh = mesh;
                        NotifyMeshComponentChanged(meshFilter);
                    }
                }

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(processAllChildren))
                {
                    if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
                    {
                        continue;
                    }

                    Mesh originalMesh = skinnedMeshRenderer.sharedMesh;
                    Mesh mesh = originalMesh;
                    HandleMesh(skinnedMeshRenderer.name, ref mesh);
                    if (!ReferenceEquals(originalMesh, mesh))
                    {
                        skinnedMeshRenderer.sharedMesh = mesh;
                        NotifyMeshComponentChanged(skinnedMeshRenderer);
                    }
                }

                meshProcessStopwatch.Stop();
            }

            if (changed)
            {
                Stopwatch saveStopwatch = Stopwatch.StartNew();
                AssetDatabase.SaveAssets();
                saveStopwatch.Stop();
                FinalizeSceneMeshChanges(model);

                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent rules: Processed '{model.name}' in {totalStopwatch.ElapsedMilliseconds} ms " +
                    $"(store={storeStopwatch.ElapsedMilliseconds} ms, mesh={meshProcessStopwatch.ElapsedMilliseconds} ms, compute={meshComputeStopwatch.ElapsedMilliseconds} ms, assetWrite={assetWriteStopwatch.ElapsedMilliseconds} ms, save={saveStopwatch.ElapsedMilliseconds} ms, reused={reusedMeshCount}, generated={generatedMeshCount}, updated={updatedMeshAssetCount}, touched={touchedAssetPaths.Count}).");
            }

            return changed;
        }

        private static bool CanReuseGeneratedMesh(string sourceMeshAssetPath, string generatedMeshAssetPath, Mesh sourceMesh, Mesh generatedMesh)
        {
            if (sourceMesh == null || generatedMesh == null)
            {
                return false;
            }

            if (sourceMesh.vertexCount != generatedMesh.vertexCount || sourceMesh.subMeshCount != generatedMesh.subMeshCount)
            {
                return false;
            }

            if (TryGetAssetWriteTimeUtc(sourceMeshAssetPath, out DateTime sourceWriteTimeUtc)
                && TryGetAssetWriteTimeUtc(generatedMeshAssetPath, out DateTime generatedWriteTimeUtc)
                && sourceWriteTimeUtc > generatedWriteTimeUtc)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetAssetWriteTimeUtc(string assetPath, out DateTime writeTimeUtc)
        {
            writeTimeUtc = default;
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string absolutePath = AssetContextJsonQueryUtility.ToAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return false;
            }

            writeTimeUtc = File.GetLastWriteTimeUtc(absolutePath);
            return true;
        }

        private static string GetMeshesFolderPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            return folder.EndsWith("/Meshes", StringComparison.OrdinalIgnoreCase)
                ? folder
                : folder + "/Meshes";
        }

        private static bool NameMatchesAny(string name, IEnumerable<string> patterns)
        {
            if (string.IsNullOrEmpty(name) || patterns == null)
            {
                return false;
            }

            foreach (string pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    continue;
                }

                if (name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTangentMesh(string meshPath)
        {
            return !string.IsNullOrEmpty(meshPath)
                && meshPath.Replace('\\', '/').Contains("/Meshes/")
                && meshPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static void StoreOriginalMeshesForModel(GameObject root)
        {
            try
            {
                Dictionary<string, Dictionary<string, string[]>> map = LoadOriginalMap();
                string modelKey = GetModelStorageKey(root);
                if (string.IsNullOrWhiteSpace(modelKey))
                {
                    HoyoToonLogger.Warning(
                        HoyoToonLogCategory.Models,
                        $"Failed to determine a stable tangent storage key for '{root?.name}'.");
                    return;
                }

                if (!map.ContainsKey(modelKey))
                {
                    map[modelKey] = new Dictionary<string, string[]>();
                }

                void Store(Component owner, Mesh mesh)
                {
                    if (mesh == null)
                    {
                        return;
                    }

                    string ownerKey = GetMeshOwnerKey(root, owner);
                    if (string.IsNullOrWhiteSpace(ownerKey) || !TryGetMeshReference(mesh, out string guid, out long localId, out string meshName))
                    {
                        return;
                    }

                    map[modelKey][ownerKey] = localId > 0
                        ? new[] { guid, localId.ToString(CultureInfo.InvariantCulture), meshName }
                        : new[] { guid, meshName };
                }

                foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter?.sharedMesh != null)
                    {
                        Store(meshFilter, meshFilter.sharedMesh);
                    }
                }

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skinnedMeshRenderer?.sharedMesh != null)
                    {
                        Store(skinnedMeshRenderer, skinnedMeshRenderer.sharedMesh);
                    }
                }

                SaveOriginalMap(map);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Models,
                    $"Failed to store original meshes for '{root?.name}': {exception.Message}");
            }
        }

        private static Dictionary<string, Dictionary<string, string[]>> LoadOriginalMap()
        {
            try
            {
                if (File.Exists(OriginalMeshPathsFile))
                {
                    byte[] bytes = File.ReadAllBytes(OriginalMeshPathsFile);
                    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(bytes, HoyoToonApi.JsonResolver)
                        ?? new Dictionary<string, Dictionary<string, string[]>>();
                }
            }
            catch (Exception)
            {
                // Ignore JSON parse and IO errors; fall back to an empty map for robustness.
            }

            return new Dictionary<string, Dictionary<string, string[]>>();
        }

        private static void SaveOriginalMap(Dictionary<string, Dictionary<string, string[]>> map)
        {
            try
            {
                if (!Directory.Exists(HoyoToonFolder))
                {
                    Directory.CreateDirectory(HoyoToonFolder);
                }

                byte[] bytes = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(map, HoyoToonApi.JsonResolver));
                File.WriteAllBytes(OriginalMeshPathsFile, bytes);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Models,
                    $"Failed to save original mesh map: {exception.Message}");
            }
        }

        private static bool TryGetStoredMeshEntries(
            Dictionary<string, Dictionary<string, string[]>> map,
            GameObject root,
            out Dictionary<string, string[]> storedEntries)
        {
            storedEntries = null;
            if (map == null || root == null)
            {
                return false;
            }

            string modelKey = GetModelStorageKey(root);
            if (!string.IsNullOrWhiteSpace(modelKey)
                && map.TryGetValue(modelKey, out storedEntries)
                && storedEntries != null)
            {
                return true;
            }

            return map.TryGetValue(root.name, out storedEntries) && storedEntries != null;
        }

        private static bool HasOriginalMeshStored(Dictionary<string, string[]> storedEntries, string meshKey)
        {
            return storedEntries != null
                && !string.IsNullOrWhiteSpace(meshKey)
                && storedEntries.ContainsKey(meshKey);
        }

        private static Mesh RestoreOriginalMesh(Dictionary<string, string[]> storedEntries, string meshOwnerKey, string legacyMeshName)
        {
            try
            {
                if (!TryGetStoredMeshEntry(storedEntries, meshOwnerKey, legacyMeshName, out string[] entry))
                {
                    return null;
                }

                if (entry == null || entry.Length < 2)
                {
                    return null;
                }

                string guid = entry[0];
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    HoyoToonLogger.Warning(
                        HoyoToonLogCategory.Models,
                        $"Tangent reset: Asset path not found for GUID: {guid} (mesh: {legacyMeshName})");
                    return null;
                }

                if (entry.Length >= 3
                    && long.TryParse(entry[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long localId)
                    && TryLoadMeshByLocalId(assetPath, localId, out Mesh identifiedMesh))
                {
                    HoyoToonLogger.Info(HoyoToonLogCategory.Models, $"Tangent reset: Restored original mesh: {identifiedMesh.name}");
                    return identifiedMesh;
                }

                string storedMeshName = entry.Length >= 3 ? entry[2] : entry[1];
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset is Mesh originalMesh && originalMesh.name == storedMeshName)
                    {
                        HoyoToonLogger.Info(HoyoToonLogCategory.Models, $"Tangent reset: Restored original mesh: {legacyMeshName}");
                        return originalMesh;
                    }
                }
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Models,
                    $"Tangent reset: Failed to restore '{legacyMeshName}': {exception.Message}");
            }

            return null;
        }

        private static bool TryGetStoredMeshEntry(
            Dictionary<string, string[]> storedEntries,
            string meshOwnerKey,
            string legacyMeshName,
            out string[] entry)
        {
            entry = null;
            if (storedEntries == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(meshOwnerKey) && storedEntries.TryGetValue(meshOwnerKey, out entry) && entry != null)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(legacyMeshName)
                && storedEntries.TryGetValue(legacyMeshName, out entry)
                && entry != null;
        }

        private static bool TryLoadMeshByLocalId(string assetPath, long localId, out Mesh mesh)
        {
            mesh = null;
            if (string.IsNullOrWhiteSpace(assetPath) || localId <= 0)
            {
                return false;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (!(asset is Mesh candidate))
                {
                    continue;
                }

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long candidateLocalId))
                {
                    continue;
                }

                if (candidateLocalId != localId)
                {
                    continue;
                }

                mesh = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetMeshReference(Mesh mesh, out string guid, out long localId, out string meshName)
        {
            guid = string.Empty;
            localId = 0L;
            meshName = mesh != null ? mesh.name : string.Empty;
            if (mesh == null)
            {
                return false;
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out guid, out localId) && !string.IsNullOrWhiteSpace(guid))
            {
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            guid = AssetDatabase.AssetPathToGUID(assetPath);
            return !string.IsNullOrWhiteSpace(guid);
        }

        private static string GetModelStorageKey(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            string assetIdentityPath = TryResolveRootAssetIdentityPath(root);
            if (!string.IsNullOrWhiteSpace(assetIdentityPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(assetIdentityPath);
                return !string.IsNullOrWhiteSpace(guid)
                    ? "asset:" + guid
                    : "assetpath:" + assetIdentityPath.Replace('\\', '/');
            }

            if (root.scene.IsValid())
            {
                return "scene:" + (root.scene.path ?? string.Empty) + "|" + GetTransformHierarchyKey(root.transform, null);
            }

            return "name:" + (root.name ?? string.Empty);
        }

        private static string TryResolveRootAssetIdentityPath(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            string directPath = AssetDatabase.GetAssetPath(root);
            if (!string.IsNullOrWhiteSpace(directPath))
            {
                return directPath;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(root))
            {
                return null;
            }

            GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (sourceRoot == null)
            {
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceRoot);
            return string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        }

        private static string GetMeshOwnerKey(GameObject root, Component owner)
        {
            if (root == null || owner == null)
            {
                return string.Empty;
            }

            return owner.GetType().Name + "|" + GetTransformHierarchyKey(owner.transform, root.transform);
        }

        private static string GetTransformHierarchyKey(Transform target, Transform exclusiveRoot)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            Transform current = target;
            while (current != null && current != exclusiveRoot)
            {
                segments.Add(current.name + "#" + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
                current = current.parent;
            }

            segments.Reverse();
            return segments.Count > 0 ? string.Join("/", segments) : ".";
        }

        private static GameObject GetRootParent(GameObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            GameObject highestDirectRenderableRoot = null;
            GameObject current = obj;
            while (current != null)
            {
                if (HasDirectTangentRenderableChildren(current))
                {
                    highestDirectRenderableRoot = current;
                }

                current = current.transform.parent != null
                    ? current.transform.parent.gameObject
                    : null;
            }

            if (highestDirectRenderableRoot != null)
            {
                return highestDirectRenderableRoot;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(obj))
            {
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
                if (prefabRoot != null)
                {
                    return prefabRoot;
                }
            }

            current = obj;
            while (current.transform.parent != null)
            {
                current = current.transform.parent.gameObject;
            }

            return current;
        }

        private static void NotifyMeshComponentChanged(Component component)
        {
            if (component == null)
            {
                return;
            }

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(component.gameObject);

            if (PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private static void FinalizeSceneMeshChanges(GameObject rootObject)
        {
            if (rootObject == null)
            {
                return;
            }

            EditorUtility.SetDirty(rootObject);
            if (PrefabUtility.IsPartOfPrefabInstance(rootObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(rootObject);
            }

            if (rootObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(rootObject.scene);
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static bool HasDirectTangentRenderableChildren(GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            foreach (Transform child in obj.transform)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponent<SkinnedMeshRenderer>() != null || child.GetComponent<MeshFilter>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CleanupMeshesFolder(GameObject rootObject, out string cleanedMeshesFolder)
        {
            cleanedMeshesFolder = null;

            try
            {
                if (rootObject == null)
                {
                    return false;
                }

                string meshesFolderAssetPath = null;

                foreach (MeshFilter meshFilter in rootObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter?.sharedMesh == null)
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    string candidate = GetMeshesFolderPath(assetPath);
                    if (AssetDatabase.IsValidFolder(candidate))
                    {
                        meshesFolderAssetPath = candidate;
                        break;
                    }

                    string candidateAbsolutePath = AssetContextJsonQueryUtility.ToAbsolutePath(candidate);
                    if (Directory.Exists(candidateAbsolutePath))
                    {
                        meshesFolderAssetPath = candidate;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(meshesFolderAssetPath))
                {
                    foreach (SkinnedMeshRenderer skinnedMeshRenderer in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (skinnedMeshRenderer?.sharedMesh == null)
                        {
                            continue;
                        }

                        string assetPath = AssetDatabase.GetAssetPath(skinnedMeshRenderer.sharedMesh);
                        if (string.IsNullOrEmpty(assetPath))
                        {
                            continue;
                        }

                        string candidate = GetMeshesFolderPath(assetPath);
                        if (AssetDatabase.IsValidFolder(candidate))
                        {
                            meshesFolderAssetPath = candidate;
                            break;
                        }

                        string candidateAbsolutePath = AssetContextJsonQueryUtility.ToAbsolutePath(candidate);
                        if (Directory.Exists(candidateAbsolutePath))
                        {
                            meshesFolderAssetPath = candidate;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(meshesFolderAssetPath))
                {
                    return false;
                }

                string meshesFolderAbsolutePath = AssetContextJsonQueryUtility.ToAbsolutePath(meshesFolderAssetPath);
                bool deletedAnyAsset = false;
                if (Directory.Exists(meshesFolderAbsolutePath))
                {
                    foreach (string file in Directory.GetFiles(meshesFolderAbsolutePath, "*.asset"))
                    {
                        string assetPath = AssetContextJsonQueryUtility.ToAssetPath(file);
                        if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath))
                        {
                            deletedAnyAsset = true;
                        }
                    }
                }

                bool deletedFolder = false;
                bool isEmpty = Directory.Exists(meshesFolderAbsolutePath)
                    && Directory.GetFiles(meshesFolderAbsolutePath).Length == 0
                    && Directory.GetDirectories(meshesFolderAbsolutePath).Length == 0;
                if (isEmpty)
                {
                    deletedFolder = AssetDatabase.DeleteAsset(meshesFolderAssetPath);
                }

                cleanedMeshesFolder = meshesFolderAssetPath;

                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Tangent reset: Cleaned up Meshes folder: {meshesFolderAssetPath}");
                return deletedAnyAsset || deletedFolder;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Models,
                    $"Failed to cleanup Meshes folder: {exception.Message}");
                return false;
            }
        }

        private static void ImportFolderOrParent(string folderAssetPath)
        {
            if (string.IsNullOrEmpty(folderAssetPath))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(folderAssetPath))
            {
                AssetDatabase.ImportAsset(folderAssetPath, ImportAssetOptions.ForceUpdate);
                return;
            }

            string normalized = folderAssetPath.Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            if (slash <= 0)
            {
                return;
            }

            string parent = normalized.Substring(0, slash);
            if (AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.ImportAsset(parent, ImportAssetOptions.ForceUpdate);
            }
        }

        private static Mesh ProcessTangents_ModifyMeshTangents(Mesh mesh)
        {
            if (mesh == null)
            {
                return null;
            }

            Mesh newMesh = UnityEngine.Object.Instantiate(mesh);

            Vector3[] vertices = newMesh.vertices;
            int[] triangles = newMesh.triangles;
            int vertexCount = vertices.Length;

            var unmerged = new Vector3[vertexCount];
            var merged = new Dictionary<Vector3, Vector3>(vertexCount);
            var tangents = new Vector4[vertexCount];

            Vector4[] oldTangents = mesh.tangents;
            bool hasOldTangents = oldTangents != null && oldTangents.Length == vertexCount;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];

                Vector3 v0 = vertices[i0] * 100f;
                Vector3 v1 = vertices[i1] * 100f;
                Vector3 v2 = vertices[i2] * 100f;

                Vector3 e0 = v1 - v0;
                Vector3 e1 = v2 - v0;
                Vector3 e2 = v2 - v1;

                Vector3 normal = Vector3.Cross(e0, e1).normalized;

                float a0 = Vector3.Angle(e0, e1);
                float a1 = Vector3.Angle(-e0, e2);
                float a2 = Vector3.Angle(-e1, -e2);

                unmerged[i0] += normal * a0;
                unmerged[i1] += normal * a1;
                unmerged[i2] += normal * a2;
            }

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 vertex = vertices[i];

                if (merged.TryGetValue(vertex, out Vector3 existing))
                {
                    merged[vertex] = existing + unmerged[i];
                }
                else
                {
                    merged.Add(vertex, unmerged[i]);
                }
            }

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 normal = merged[vertices[i]].normalized;

                float w = hasOldTangents ? oldTangents[i].w : 1f;
                tangents[i] = new Vector4(normal.x, normal.y, normal.z, w);
            }

            newMesh.tangents = tangents;

            if (hasOldTangents)
            {
                var uv7 = new Vector2[vertexCount];
                var uv8 = new Vector2[vertexCount];

                for (int i = 0; i < vertexCount; i++)
                {
                    Vector4 tangent = oldTangents[i];
                    uv7[i] = new Vector2(tangent.x, tangent.y);
                    uv8[i] = new Vector2(tangent.z, tangent.w);
                }

                newMesh.uv7 = uv7;
                newMesh.uv8 = uv8;
            }

            return newMesh;
        }

        private static Mesh ProcessTangents_MoveColors(Mesh mesh)
        {
            if (mesh == null)
            {
                return null;
            }

            Mesh newMesh = UnityEngine.Object.Instantiate(mesh);

            Vector3[] vertices = newMesh.vertices;
            Vector4[] tangents = newMesh.tangents;
            Color[] colors = newMesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = Color.white;
                }

                newMesh.colors = colors;
            }

            if (tangents == null || tangents.Length != vertices.Length)
            {
                tangents = new Vector4[vertices.Length];
                for (int i = 0; i < tangents.Length; i++)
                {
                    tangents[i] = new Vector4(1f, 0f, 0f, 0f);
                }
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
            if (string.IsNullOrEmpty(status))
            {
                return TangentMode.Generate;
            }

            if (status.Equals("No Tangents", StringComparison.OrdinalIgnoreCase))
            {
                return TangentMode.None;
            }

            if (status.Equals("Tangent Generation", StringComparison.OrdinalIgnoreCase))
            {
                return TangentMode.Generate;
            }

            if (status.Equals("Vertex Color", StringComparison.OrdinalIgnoreCase))
            {
                return TangentMode.FromVertexColor;
            }

            return TangentMode.Generate;
        }

        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
    }
}
#endif
