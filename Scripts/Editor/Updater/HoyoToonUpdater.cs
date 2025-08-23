#if UNITY_EDITOR
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Unity.EditorCoroutines.Editor;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Linq;

#if VRC_SDK_VRCSDK3
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Resolver;
#endif



namespace HoyoToon
{
    [InitializeOnLoad]
    public class HoyoToonUpdater : Editor
    {
        private const string RemotePackageJsonUrl = "https://raw.githubusercontent.com/HoyoToon/HoyoToon/main/package.json";
        private const string ReleasesApiUrl = "https://api.github.com/repos/HoyoToon/HoyoToon/releases";
        private static List<string> assetsToDelete = new List<string>();
        private static List<string> metasToDelete = new List<string>();

        static HoyoToonUpdater()
        {
            HoyoToonPreferences.LoadPrefs();
            if (EditorPrefs.GetBool(HoyoToonPreferences.CheckForUpdatesPref, true))
            {
                HoyoToonLogs.LogDebug("Checking for updates on startup...");
                CheckForUpdates();
            }
        }

        [MenuItem("HoyoToon/Check for updates", false, 0)]
        public static void CheckForUpdates()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(CheckVersions((localVersion, remoteVersion) => { }));
        }


        public static IEnumerator CheckVersions(Action<string, string> onVersionsFetched)
        {
            // Get local version
            string localVersion = null;
            try
            {
                string scriptPath = HoyoToonParseManager.GetPackagePath("com.meliverse.hoyotoon");
                string packageJsonPath = Path.Combine(scriptPath, "package.json");
                string jsonContent = File.ReadAllText(Path.GetFullPath(packageJsonPath));
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);
                localVersion = jsonObject != null && jsonObject.TryGetValue("version", out var version) ? version.ToString() : null;
                HoyoToonLogs.LogDebug($"Local version: {localVersion}");
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Error reading local package.json: {ex.Message}");
            }

            // Get remote version
            using (UnityWebRequest webRequest = UnityWebRequest.Get(RemotePackageJsonUrl))
            {
                yield return webRequest.SendWebRequest();

                string remoteVersion = null;
                if (!(webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError))
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(webRequest.downloadHandler.text);
                    if (dict != null && dict.TryGetValue("version", out var version)) remoteVersion = version?.ToString();
                }
                
                if (remoteVersion == null)
                {
                    HoyoToonLogs.ErrorDebug($"Error fetching remote package.json: {webRequest.error}");
                }
                else
                {
                    HoyoToonLogs.LogDebug($"Remote version: {remoteVersion}");
                }

                onVersionsFetched?.Invoke(localVersion, remoteVersion);

                // Check if the remote version is newer
                if (remoteVersion != null && localVersion != null && new Version(remoteVersion) > new Version(localVersion))
                {
                    EditorCoroutineUtility.StartCoroutineOwnerless(FetchUpdate(localVersion, remoteVersion));
                }
            }
        }

        private static IEnumerator FetchUpdate(string localVersion, string remoteVersion)
        {
            HoyoToonLogs.LogDebug($"Updating HoyoToon to version {remoteVersion}...");

            using (UnityWebRequest webRequest = UnityWebRequest.Get(ReleasesApiUrl))
            {
                webRequest.SetRequestHeader("User-Agent", "request");
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    HoyoToonLogs.ErrorDebug($"Error fetching releases: {webRequest.error}");
                    yield break;
                }

                var releases = JsonConvert.DeserializeObject<List<Release>>(webRequest.downloadHandler.text);
                var release = releases?.Find(r => r.tag_name == remoteVersion);

                if (release == null)
                {
                    HoyoToonLogs.ErrorDebug($"Release {remoteVersion} still cooking. Maybe we should explore this release later?");
                    yield break;
                }

                var asset = release.assets?.Find(a => a.browser_download_url.Contains("unitypackage"));

                if (asset == null)
                {
                    HoyoToonLogs.ErrorDebug($"No package found for release {remoteVersion}.");
                    yield break;
                }

                // Fetch the release notes and file size
                string bodyContent = release.body;
                string downloadSize = asset.size.ToString();

                // Open the GUI with the fetched information
                HoyoToonUpdaterGUI.ShowWindow(localVersion, remoteVersion, downloadSize, bodyContent, () => InstallUpdate(asset.browser_download_url, localVersion, remoteVersion), () => { });
            }
        }

        private static void InstallUpdate(string downloadUrl, string currentVersion, string remoteVersion)
        {
            HoyoToonLogs.LogDebug($"Installing update from {downloadUrl}...");

            // VPM Installation method
#if VRC_SDK_VRCSDK3
            HoyoToonLogs.LogDebug("Installing update using VPM...");
            if (Resolver.VPMManifestExists())
            {
                try
                {
                    var project = new UnityProject(Resolver.ProjectDir);
                    var package = Repos.GetPackageWithVersionMatch("com.meliverse.hoyotoon", remoteVersion);

                    if (package != null)
                    {
                        project.UpdateVPMPackage(package);
                        AssetDatabase.Refresh();
                        HoyoToonLogs.LogDebug("HoyoToon has been updated to version " + remoteVersion);
                        return;
                    }
                }
                catch (Exception e) { HoyoToonLogs.LogDebug(e.ToString()); };
            }
#endif

            HoyoToonLogs.LogDebug("VPM not found, falling back to manual installation...");

            // Move existing assets to a temporary folder
            GetAssetsToDelete();

            // Download and import the new Unity package
            UnityWebRequest webRequest = UnityWebRequest.Get(downloadUrl);
            webRequest.SendWebRequest().completed += (asyncOperation) =>
            {
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string tempFilePath = Path.Combine(Application.temporaryCachePath, "update.unitypackage");
                    File.WriteAllBytes(tempFilePath, webRequest.downloadHandler.data);
                    AssetDatabase.ImportPackage(tempFilePath, false);

                    // Delete the old assets
                    DeleteAssets();

                    HoyoToonLogs.LogDebug("HoyoToon has been updated to version " + remoteVersion);
                }
                else
                {
                    HoyoToonLogs.ErrorDebug($"Error downloading update: {webRequest.error}");
                }
            };
        }

        public static void GetAssetsToDelete()
        {
            assetsToDelete.Clear();
            metasToDelete.Clear();

            // Find the package path dynamically
            string packageName = "com.meliverse.hoyotoon";
            string packagePath = HoyoToonParseManager.GetPackagePath(packageName);

            if (string.IsNullOrEmpty(packagePath))
            {
                HoyoToonLogs.ErrorDebug($"Package {packageName} not found.");
                return;
            }

            // Convert absolute package path to relative path
            string relativePackagePath = "Packages/" + packageName;

            string[] assetGUIDs = AssetDatabase.FindAssets("", new[] { relativePackagePath }).ToArray();
            string[] foldersToExclude = Directory.GetDirectories(packagePath)
                .Where(x => !Path.GetFileName(x).Equals("VRChat", StringComparison.OrdinalIgnoreCase))
                .Select(x => AssetDatabase.AssetPathToGUID("Packages/" + packageName + "/" + Path.GetFileName(x)))
                .ToArray();
            assetGUIDs = assetGUIDs.Except(foldersToExclude).ToArray();

            foreach (string guid in assetGUIDs)
            {
                string oldPath = AssetDatabase.GUIDToAssetPath(guid);
                if (oldPath.Contains("/VRChat/", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Skip files in VRChat folder
                }
                string ext = Path.GetExtension(oldPath);
                string newPath = Path.Combine(relativePackagePath, "HoyoTemp", guid + ext);

                AssetDatabase.MoveAsset(oldPath, newPath);

                assetsToDelete.Add(newPath);
                metasToDelete.Add(oldPath + ".meta");
            }
        }

        public static void DeleteAssets()
        {
            foreach (string metaPath in metasToDelete)
            {
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }

            foreach (string assetPath in assetsToDelete)
            {
                if (File.Exists(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }

        private class Release
        {
            public string tag_name { get; set; }
            public List<Asset> assets { get; set; }
            public string body { get; set; }
        }

        private class Asset
        {
            public string browser_download_url { get; set; }
            public long size { get; set; }
        }

    }
}
#endif
