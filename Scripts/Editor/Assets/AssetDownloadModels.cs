#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Editor.Resources;
using HoyoToon.Editor.Resources.Cloudreve;
using HoyoToon.Editor.Utilities.Assets;


namespace HoyoToon.Editor.Assets
{
    internal sealed class AssetDownloadConnectionContext
    {
        internal AssetDownloadConnectionContext(
            string shareUrl,
            string resolvedRootUri,
            CloudreveShareDescriptor descriptor,
            CloudreveResourceClient client)
        {
            ShareUrl = shareUrl ?? string.Empty;
            ResolvedRootUri = resolvedRootUri ?? string.Empty;
            Descriptor = descriptor;
            Client = client;
        }

        internal string ShareUrl { get; }

        internal string ResolvedRootUri { get; }

        internal CloudreveShareDescriptor Descriptor { get; }

        internal CloudreveResourceClient Client { get; }
    }

    internal sealed class AssetDownloadDirectoryEntry
    {
        internal string Name = string.Empty;
        internal string Uri = string.Empty;
        internal bool IsDirectory;
        internal string UpdatedAt = string.Empty;
        internal long Size;
    }

    internal sealed class AssetDownloadGameOption
    {
        internal AssetDownloadGameOption(string key, string displayName, string folderName, string folderUri)
        {
            Key = key ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName;
            FolderName = folderName ?? string.Empty;
            FolderUri = folderUri ?? string.Empty;
        }

        internal string Key { get; }

        internal string DisplayName { get; }

        internal string FolderName { get; }

        internal string FolderUri { get; }
    }

    internal sealed class AssetDownloadVariantOption
    {
        internal AssetDownloadVariantOption(string name, string folderUri)
        {
            Name = string.IsNullOrWhiteSpace(name) ? AssetDownloadPathUtility.DefaultVariantName : name;
            FolderUri = folderUri ?? string.Empty;
        }

        internal string Name { get; }

        internal string FolderUri { get; }
    }

    internal sealed class AssetDownloadGameCache
    {
        internal AssetDownloadGameCache(AssetDownloadGameOption game)
        {
            Game = game;
            CharacterFolderUrisByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            VariantsByCharacter = new Dictionary<string, List<AssetDownloadVariantOption>>(StringComparer.OrdinalIgnoreCase);
        }

        internal AssetDownloadGameOption Game { get; }

        internal string CharactersFolderName { get; set; } = string.Empty;

        internal string CharactersFolderUri { get; set; } = string.Empty;

        internal List<string> Characters { get; set; } = new List<string>();

        internal Dictionary<string, string> CharacterFolderUrisByName { get; }

        internal Dictionary<string, List<AssetDownloadVariantOption>> VariantsByCharacter { get; }

        internal bool IsLoadingCharacters { get; set; }

        internal bool IsLoadingVariants { get; set; }
    }

    internal sealed class AssetDownloadJob
    {
        internal AssetDownloadJob(AssetDownloadGameOption game, string characterName, AssetDownloadVariantOption variant)
        {
            Game = game;
            CharacterName = characterName ?? string.Empty;
            Variant = variant;
        }

        internal AssetDownloadGameOption Game { get; }

        internal string CharacterName { get; }

        internal AssetDownloadVariantOption Variant { get; }
    }

    internal sealed class AssetDownloadPreparedFile
    {
        internal AssetDownloadPreparedFile(
            AssetDownloadJob job,
            RemoteResourceEntry remoteFile,
            string localFilePath,
            string importedAssetPath)
        {
            Job = job;
            RemoteFile = remoteFile;
            LocalFilePath = localFilePath ?? string.Empty;
            ImportedAssetPath = importedAssetPath ?? string.Empty;
        }

        internal AssetDownloadJob Job { get; }

        internal RemoteResourceEntry RemoteFile { get; }

        internal string LocalFilePath { get; }

        internal string ImportedAssetPath { get; }
    }

    internal sealed class AssetDownloadPreparedJob
    {
        internal AssetDownloadPreparedJob(AssetDownloadJob job, string assetTargetPath)
        {
            Job = job;
            AssetTargetPath = assetTargetPath ?? string.Empty;
            FilesToDownload = new List<AssetDownloadPreparedFile>();
            ImportedAssetPaths = new List<string>();
        }

        internal AssetDownloadJob Job { get; }

        internal string AssetTargetPath { get; }

        internal List<AssetDownloadPreparedFile> FilesToDownload { get; }

        internal List<string> ImportedAssetPaths { get; }

        internal int SkippedFileCount { get; set; }

        internal bool Cancelled { get; set; }
    }

    internal sealed class AssetDownloadResult
    {
        internal string AssetTargetPath = string.Empty;
        internal List<string> ImportedAssetPaths = new List<string>();
        internal int DownloadedFileCount;
        internal int SkippedFileCount;
        internal bool Cancelled;
    }

    internal enum AssetDownloadOverwriteMode
    {
        Overwrite = 0,
        SkipExisting = 1,
        Cancel = 2,
    }

    internal enum AssetDownloadFbxVariantType
    {
        Unknown = 0,
        WithAnims = 1,
        NoAnims = 2,
    }

    internal enum AssetDownloadHsrFbxChoice
    {
        WithAnims = 0,
        NoAnims = 1,
        Both = 2,
    }
}
#endif
