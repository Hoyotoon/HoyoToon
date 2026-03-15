#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.Updater
{
    [Serializable]
    public sealed class PackageMetadata
    {
        public string version;
    }

    [Serializable]
    public sealed class PendingInstallState
    {
        public string branch;
        public bool cleanMissingFiles;
        public List<string> filesToCopy = new List<string>();
        public List<string> filesToDelete = new List<string>();
    }

    [Serializable]
    public sealed class Manifest
    {
        public string version;
        public Dictionary<string, string> files;
        public List<string> keys;
        public List<string> values;

        public Dictionary<string, string> GetFiles()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (files != null)
            {
                foreach (KeyValuePair<string, string> entry in files)
                {
                    string key = PackageUpdater.NormalizeRelativePath(entry.Key);
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    dict[key] = entry.Value ?? string.Empty;
                }

                return dict;
            }

            if (keys == null || values == null)
            {
                return dict;
            }

            int count = Math.Min(keys.Count, values.Count);
            for (int i = 0; i < count; i++)
            {
                string key = PackageUpdater.NormalizeRelativePath(keys[i]);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                dict[key] = values[i] ?? string.Empty;
            }

            return dict;
        }
    }
}
#endif