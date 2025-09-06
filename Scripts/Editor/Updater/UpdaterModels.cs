#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Updater
{
    [Serializable]
    public class PackageInfo
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unity;
    }

    [Serializable]
    public class GitTreeResponse
    {
        public string sha;
        public string url;
        public GitTreeItem[] tree;
        public bool truncated;
    }

    [Serializable]
    public class GitTreeItem
    {
        public string path;
        public string mode;
        public string type;
        public string sha;
        public long? size;
        public string url;
    }

    [Serializable]
    public class GitFileInfo
    {
        public string name;
        public string path;
        public string sha;
        public long size;
        public string url;
        public string html_url;
        public string git_url;
        public string download_url;
        public string type;
        public string content;
        public string encoding;
    }

    [Serializable]
    public class LocalPackageTracker
    {
        public string currentVersion;
        public string lastUpdateCheck;
        public string lastTreeSha;
        public Dictionary<string, string> fileHashes = new Dictionary<string, string>();
        public List<string> trackedFiles = new List<string>();
    }

    [Serializable]
    public class UpdateBatch
    {
        public List<FileUpdate> fileUpdates = new List<FileUpdate>();
        public List<string> filesToDelete = new List<string>();
        public string sourceCommitSha; // commit used when building this batch
        public int totalOperations => fileUpdates.Count + filesToDelete.Count;
    }

    [Serializable]
    public class FileUpdate
    {
        public string path;
        public string downloadUrl;
        public string expectedSha;
        public bool isNew;
    }
}
#endif
