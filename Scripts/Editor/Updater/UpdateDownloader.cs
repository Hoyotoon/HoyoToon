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

                        string requestUrl = PackageUpdaterManifestClient.BuildCacheBustedUrl(BuildRawFileUrl(baseUrl, normalizedFile));
                        var request = UnityWebRequest.Get(requestUrl);
                        request.SetRequestHeader("Cache-Control", "no-cache, no-store, max-age=0");
                        request.SetRequestHeader("Pragma", "no-cache");
                        request.SetRequestHeader("Expires", "0");
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
                            result.ErrorMessage = $"Failed to download '{file}': {request.error}";
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

        private static string BuildRawFileUrl(string baseUrl, string relativePath)
        {
            string normalizedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            string[] segments = PackageUpdaterStorage.NormalizeRelativePath(relativePath)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                return normalizedBase;
            }

            string escapedPath = string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
            return string.Concat(normalizedBase, "/", escapedPath);
        }
    }
}
#endif
