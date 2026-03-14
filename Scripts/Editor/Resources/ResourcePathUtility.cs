#if UNITY_EDITOR
using System;
using System.IO;

namespace HoyoToon.Editor.ResourceSystem
{
    internal static class ResourcePathUtility
    {
        internal static string ResourcesBasePath => ResourceCacheService.ResourcesBasePath;

        internal static bool IsThumbMarkerFile(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) && relativePath.EndsWith("._thumb", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDownloadableFile(CloudreveClient.RemoteEntryInfo entry)
        {
            return entry != null
                && !entry.IsDirectory
                && !IsThumbMarkerFile(entry.RelativePath)
                && !entry.RelativePath.EndsWith("/")
                && !entry.RelativePath.EndsWith("\\");
        }

        internal static bool IsDownloadableFile(RemoteFileInfo file)
        {
            return file != null
                && !file.IsDirectory
                && !IsThumbMarkerFile(file.RelativePath)
                && !file.RelativePath.EndsWith("/")
                && !file.RelativePath.EndsWith("\\");
        }

        internal static string GetLocalBasePath(GameConfig gameConfig)
        {
            if (gameConfig == null || string.IsNullOrEmpty(gameConfig.LocalPath))
            {
                return null;
            }

            return Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
        }
    }
}
#endif
