#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
using System.Threading.Tasks;

using HoyoToon.Editor.UI.Windows;

using HoyoToon.Editor.UI.ResourcesUI;

namespace HoyoToon.Editor.ResourceSystem
{
    public static class ResourceManager
    {
        public static void SaveCacheData() => ResourceCacheService.SaveCacheData();

        public static ResourceCacheData GetCacheData() => ResourceCacheService.GetCacheData();

        public static void ClearCacheData() => ResourceCacheService.ClearCacheData();

        public static Task DownloadResourcesAsync(string[] gameKeys, bool forceDownload = false, System.Threading.CancellationToken cancellationToken = default)
            => ResourceDownloadService.DownloadResourcesAsync(gameKeys, forceDownload, cancellationToken);

        public static Task SynchronizeFilesForMultipleGamesAsync(Dictionary<string, FileUpdateInfo> updateInfoMap, System.Threading.CancellationToken cancellationToken = default)
            => ResourceDownloadService.SynchronizeFilesForMultipleGamesAsync(updateInfoMap, cancellationToken);

        public static void CheckResourceStatus() => ResourceMenuItems.CheckResourceStatus();

        public static void DownloadAllResources() => ResourceMenuItems.DownloadAllResources();

        public static void DownloadGameResources(string gameKey) => ResourceMenuItems.DownloadGameResources(gameKey);

        public static void DeleteGameResourcesByKey(string gameKey) => ResourceMenuItems.DeleteGameResourcesByKey(gameKey);

        public static void ClearResourceCache() => ResourceMenuItems.ClearResourceCache();
        public static void DeleteAllResources() => ResourceMenuItems.DeleteAllResources();

        public static Dictionary<string, ResourceStatus> GetResourceStatus()
            => ResourceValidation.GetResourceStatus();

        public static bool HasResourcesForGame(string gameKey)
            => ResourceValidation.HasResourcesForGame(gameKey);

        public static string GetGameResourcePath(string gameKey)
            => ResourceValidation.GetGameResourcePath(gameKey);

        public static List<LocalResourceInfo> GetAllLocalResources()
            => ResourceValidation.GetAllLocalResources();

        public static List<LocalResourceInfo> GetLocalResourcesForGame(string gameKey)
            => ResourceValidation.GetLocalResourcesForGame(gameKey);

        public static bool AreResourcesAvailable()
            => ResourceValidation.AreResourcesAvailable();

        public static bool ValidateResourcesAvailable()
            => ResourceValidation.ValidateResourcesAvailable();

        public static bool ValidateResourcesForGames(params string[] gameKeys)
            => ResourceValidation.ValidateResourcesForGames(gameKeys);
    }
}
#endif
