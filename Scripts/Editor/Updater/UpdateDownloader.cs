#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HoyoToon.Editor.Updater
{
    internal static class UpdateDownloader
    {
        private const int BatchSize = 4;

        internal sealed class DownloadResult
        {
            public bool Success = true;
            public string ErrorMessage;
        }

        public static IEnumerator DownloadFiles(
            List<string> files,
            string baseUrl,
            string tempRoot,
            IReadOnlyDictionary<string, string> expectedHashes,
            DownloadResult result,
            Action<float, string> onProgress = null)
        {
            if (result == null)
            {
                yield break;
            }

            result.Success = true;
            result.ErrorMessage = null;

            if (files == null || files.Count == 0)
            {
                yield break;
            }

            Directory.CreateDirectory(tempRoot);

            int completed = 0;
            for (int index = 0; index < files.Count; index += BatchSize)
            {
                int count = Mathf.Min(BatchSize, files.Count - index);
                var requests = new List<UnityWebRequest>(count);
                var batchFiles = new List<string>(count);
                var disposedRequests = new HashSet<UnityWebRequest>();

                try
                {
                    for (int batchIndex = 0; batchIndex < count; batchIndex++)
                    {
                        string file = files[index + batchIndex];
                        if (!PackageUpdaterStorage.TryNormalizeRelativePath(file, out string normalizedFile, out string pathError))
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Unsafe updater path '{file}': {pathError}";
                            yield break;
                        }

                        string requestUrl = PackageUpdaterManifestClient.BuildRawFileUrlFromBase(baseUrl, normalizedFile);
                        var request = UnityWebRequest.Get(requestUrl);
                        PackageUpdaterManifestClient.ApplyRawContentHeaders(request);
                        request.SendWebRequest();

                        requests.Add(request);
                        batchFiles.Add(normalizedFile);
                    }

                    bool finished = false;
                    while (!finished)
                    {
                        finished = true;
                        float batchProgress = 0f;
                        for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                        {
                            batchProgress += requests[requestIndex].downloadProgress < 0f ? 0f : requests[requestIndex].downloadProgress;
                            if (!requests[requestIndex].isDone)
                            {
                                finished = false;
                            }
                        }

                        float overallProgress = Mathf.Clamp01((completed + (batchProgress / requests.Count)) / files.Count);
                        onProgress?.Invoke(overallProgress, $"Downloading files... {completed}/{files.Count}");
                        yield return null;
                    }

                    for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                    {
                        UnityWebRequest request = requests[requestIndex];
                        string file = batchFiles[requestIndex];
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            result.Success = false;
                            result.ErrorMessage = PackageUpdaterManifestClient.BuildRequestFailureMessage(request, $"Downloading '{file}'");
                            yield break;
                        }

                        if (!HoyoToon.Editor.Utilities.IO.PackagePathUtility.TryResolveUnderRoot(tempRoot, file, out string destination, out string pathError))
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Unsafe updater staging path '{file}': {pathError}";
                            yield break;
                        }

                        string directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        byte[] downloadedBytes = request.downloadHandler?.data;
                        if (downloadedBytes == null)
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Downloaded '{file}' but the response body was empty.";
                            yield break;
                        }

                        if (!IsDownloadedHashValid(file, downloadedBytes, expectedHashes, out string hashError))
                        {
                            result.Success = false;
                            result.ErrorMessage = hashError;
                            yield break;
                        }

                        File.WriteAllBytes(destination, downloadedBytes);
                        completed++;
                        onProgress?.Invoke(Mathf.Clamp01((float)completed / files.Count), $"Downloaded {completed}/{files.Count} files");
                        request.Dispose();
                        disposedRequests.Add(request);
                    }
                }
                finally
                {
                    for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                    {
                        UnityWebRequest request = requests[requestIndex];
                        if (request != null && !disposedRequests.Contains(request))
                        {
                            request.Dispose();
                        }
                    }
                }
            }
        }

        private static bool IsDownloadedHashValid(
            string relativePath,
            byte[] downloadedBytes,
            IReadOnlyDictionary<string, string> expectedHashes,
            out string error)
        {
            error = null;
            if (expectedHashes == null || !expectedHashes.TryGetValue(relativePath, out string expectedHash) || string.IsNullOrWhiteSpace(expectedHash))
            {
                return true;
            }

            string actualHash = PackageUpdaterPlanBuilder.ComputeManifestCompatibleSha1(downloadedBytes);
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            error = $"Downloaded '{relativePath}' did not match the updater manifest hash. Expected {expectedHash}, got {actualHash}.";
            return false;
        }
    }
}
#endif
