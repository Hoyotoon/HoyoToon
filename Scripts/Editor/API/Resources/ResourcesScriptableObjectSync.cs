using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Detection;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Resources;
using UnityEditor;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetSyncUtility;

namespace HoyoToon.Editor.API.Resources
{
    internal static class ResourcesScriptableObjectSync
    {
        private const string AssetFileName = "HoyoToonResources";
        private const string GeneratedResourcesFolderName = "Resources";

        internal static bool NeedsWrite(IReadOnlyList<ResourceRecordDto> resources)
        {
            List<ResourceRecordDto> syncResources = GetSyncResources(resources);
            if (GetStaleGeneratedResourceFolders(syncResources.Select(resource => resource.Key)).Count > 0)
            {
                return true;
            }

            if (syncResources.Count <= 0)
            {
                return false;
            }

            foreach (ResourceRecordDto resource in syncResources)
            {
                string assetFolderPath = GetGameGeneratedAssetsFolder(resource.Key);
                if (!AssetDatabase.IsValidFolder(assetFolderPath))
                {
                    return true;
                }

                string assetPath = GetAssetPath(assetFolderPath);
                ResourcesSO asset = AssetDatabase.LoadAssetAtPath<ResourcesSO>(assetPath);
                if (!GeneratedAssetSyncUtility.AssetMatchesPayload(asset, BuildPayload(resource)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void WriteAssets(IReadOnlyList<ResourceRecordDto> resources)
        {
            List<ResourceRecordDto> syncResources = GetSyncResources(resources);
            List<string> staleGeneratedResourceFolders = GetStaleGeneratedResourceFolders(syncResources.Select(resource => resource.Key));
            if (syncResources.Count <= 0 && staleGeneratedResourceFolders.Count <= 0)
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, "No resource records were eligible for generated asset sync.");
                return;
            }

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Synchronizing generated resource assets for {syncResources.Count} record(s) and removing {staleGeneratedResourceFolders.Count} stale folder(s).");

            using (AssetDatabaseEditingScope.Begin(disallowAutoRefresh: false))
            {
                CleanupStaleGeneratedResourceFolders(staleGeneratedResourceFolders);

                foreach (ResourceRecordDto resource in syncResources)
                {
                    string assetFolderPath = GetGameGeneratedAssetsFolder(resource.Key);
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Updating generated resource assets for '{resource.Key}'.");
                    GeneratedAssetSyncUtility.EnsureAssetFolderExists(assetFolderPath);

                    string assetPath = GetAssetPath(assetFolderPath);
                    ResourcesSO asset = GeneratedAssetSyncUtility.LoadOrCreateAsset<ResourcesSO>(assetPath);
                    GeneratedAssetSyncUtility.OverwriteAsset(asset, BuildPayload(resource));
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GeneratedAssetCacheRefreshUtility.RefreshAll();
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Finished synchronizing generated resource assets for {syncResources.Count} record(s) and {staleGeneratedResourceFolders.Count} stale folder(s).");
        }

        private static Dictionary<string, object> BuildPayload(ResourceRecordDto resource)
        {
            return CreateObject(
                ("displayName", resource.DisplayName),
                ("key", resource.Key),
                ("localPath", resource.LocalPath),
                ("webdavUrl", resource.WebdavUrl));
        }

        private static List<ResourceRecordDto> GetSyncResources(IReadOnlyList<ResourceRecordDto> resources)
        {
            return resources?
                .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.Key))
                .OrderBy(resource => resource.Key, StringComparer.Ordinal)
                .ThenBy(resource => resource.DisplayName, StringComparer.Ordinal)
                .Select(resource => new ResourceRecordDto
                {
                    DisplayName = string.IsNullOrWhiteSpace(resource.DisplayName) ? resource.Key : resource.DisplayName,
                    Key = resource.Key,
                    LocalPath = resource.LocalPath,
                    WebdavUrl = resource.WebdavUrl,
                })
                .ToList() ?? new List<ResourceRecordDto>();
        }

        private static List<string> GetStaleGeneratedResourceFolders(IEnumerable<string> activeResourceKeys)
        {
            if (!AssetDatabase.IsValidFolder(HoyoToonApi.ScriptablesAssetPath))
            {
                return new List<string>();
            }

            var activeResourceKeySet = new HashSet<string>(
                activeResourceKeys?.Where(key => !string.IsNullOrWhiteSpace(key)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            return AssetDatabase.GetSubFolders(HoyoToonApi.ScriptablesAssetPath)
                .Select(gameFolderPath => new
                {
                    ResourceKey = GetPathLeafName(gameFolderPath),
                    GeneratedFolderPath = GetGameGeneratedAssetsFolderFromGameFolder(gameFolderPath),
                })
                .Where(candidate => AssetDatabase.IsValidFolder(candidate.GeneratedFolderPath))
                .Where(candidate => !activeResourceKeySet.Contains(candidate.ResourceKey))
                .Select(candidate => candidate.GeneratedFolderPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static void CleanupStaleGeneratedResourceFolders(IEnumerable<string> staleGeneratedResourceFolders)
        {
            foreach (string generatedFolderPath in staleGeneratedResourceFolders ?? Enumerable.Empty<string>())
            {
                if (!AssetDatabase.IsValidFolder(generatedFolderPath))
                {
                    continue;
                }

                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing stale generated resource assets at '{generatedFolderPath}'.");
                if (!AssetDatabase.DeleteAsset(generatedFolderPath))
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove stale generated resource assets at '{generatedFolderPath}'.");
                    continue;
                }

                DeleteGameFolderIfEmpty(GetParentFolderPath(generatedFolderPath));
            }
        }

        private static string GetGameGeneratedAssetsFolder(string gameKey)
        {
            return $"{GetGameScriptablesFolder(gameKey)}/{GeneratedResourcesFolderName}";
        }

        private static string GetGameGeneratedAssetsFolderFromGameFolder(string gameFolderPath)
        {
            return $"{gameFolderPath}/{GeneratedResourcesFolderName}";
        }

        private static string GetGameScriptablesFolder(string gameKey)
        {
            return $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}";
        }

        private static string GetAssetPath(string assetFolderPath)
        {
            return $"{assetFolderPath}/{AssetFileName}.asset";
        }

        private static void DeleteGameFolderIfEmpty(string gameFolderPath)
        {
            if (!AssetDatabase.IsValidFolder(gameFolderPath))
            {
                return;
            }

            if (AssetDatabase.GetSubFolders(gameFolderPath).Length > 0)
            {
                return;
            }

            bool hasRemainingAssets = AssetDatabase.FindAssets(string.Empty, new[] { gameFolderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(assetPath =>
                    !string.Equals(assetPath, gameFolderPath, StringComparison.Ordinal)
                    && !AssetDatabase.IsValidFolder(assetPath));

            if (hasRemainingAssets)
            {
                return;
            }

            HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing empty generated resource folder '{gameFolderPath}'.");
            if (!AssetDatabase.DeleteAsset(gameFolderPath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove empty generated resource folder '{gameFolderPath}'.");
            }
        }

        private static string GetParentFolderPath(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex > 0 ? assetPath.Substring(0, separatorIndex) : string.Empty;
        }

        private static string GetPathLeafName(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex >= 0 ? assetPath.Substring(separatorIndex + 1) : assetPath;
        }
    }
}

