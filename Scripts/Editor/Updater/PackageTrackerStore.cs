#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Utf8Json;
using HoyoToon.API;
using UnityEngine;

namespace HoyoToon.Updater
{
    internal static class PackageTrackerStore
    {
        private static string BaseTrackerPath => Path.Combine(Application.persistentDataPath, "HoyoToon_GitUpdater_Tracker.json");
        private static string TrackerPathForBranch(string branch)
            => Path.Combine(Application.persistentDataPath, $"HoyoToon_GitUpdater_Tracker_{Sanitize(branch)}.json");

        private static string CurrentTrackerPath()
        {
            var branch = BranchSelector.GetCurrentBranch();
            var path = TrackerPathForBranch(branch);
            // Migrate existing base file to branch-scoped on first use
            try
            {
                if (File.Exists(BaseTrackerPath) && !File.Exists(path))
                {
                    File.Copy(BaseTrackerPath, path, overwrite: true);
                }
            }
            catch { }
            return path;
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
        }

        public static LocalPackageTracker Load()
        {
            try
            {
                var path = CurrentTrackerPath();
                                if (File.Exists(path))
                                {
                                        var bytes = File.ReadAllBytes(path);
                                        if (HoyoToonApi.Parser.TryParse<LocalPackageTracker>(bytes, out var tracker, out var _))
                                                return tracker;
                                }
            }
            catch { }
            return new LocalPackageTracker();
        }

        public static void Save(LocalPackageTracker tracker)
        {
            try
            {
                var bytes = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(tracker));
                var path = CurrentTrackerPath();
                File.WriteAllBytes(path, bytes);
            }
            catch { }
        }

        public static async Task SnapshotAsync(LocalPackageTracker tracker, string toolRootFullPath)
        {
            tracker.trackedFiles = new List<string>();
            tracker.fileHashes = new Dictionary<string, string>();

            if (!Directory.Exists(toolRootFullPath)) { Save(tracker); return; }

            var files = Directory.GetFiles(toolRootFullPath, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var full in files)
            {
                var rel = Path.GetRelativePath(toolRootFullPath, full).Replace("\\", "/");
                byte[] bytes = await File.ReadAllBytesAsync(full);
                tracker.trackedFiles.Add(rel);
                tracker.fileHashes[rel] = HashUtil.GitBlobSha(bytes);
            }

            Save(tracker);
        }
    }
}
#endif
