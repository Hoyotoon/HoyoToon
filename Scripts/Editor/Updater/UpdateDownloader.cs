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
            for (int i = 0; i < files.Count; i += BatchSize)
            {
                int count = Mathf.Min(BatchSize, files.Count - i);
                var requests = new List<UnityWebRequest>(count);
                var batchFiles = new List<string>(count);

                for (int j = 0; j < count; j++)
                {
                    string file = files[i + j];
                    string requestUrl = PackageUpdater.BuildRawFileUrl(baseUrl, file);
                    var request = UnityWebRequest.Get(requestUrl);
                    request.SendWebRequest();

                    requests.Add(request);
                    batchFiles.Add(file);
                }

                bool finished = false;
                while (!finished)
                {
                    finished = true;
                    float batchProgress = 0f;
                    for (int j = 0; j < requests.Count; j++)
                    {
                        batchProgress += requests[j].downloadProgress < 0f ? 0f : requests[j].downloadProgress;
                        if (!requests[j].isDone)
                        {
                            finished = false;
                        }
                    }

                    float overall = Mathf.Clamp01((completed + (batchProgress / requests.Count)) / files.Count);
                    onProgress?.Invoke(overall, $"Downloading files... {completed}/{files.Count}");
                    yield return null;
                }

                for (int j = 0; j < requests.Count; j++)
                {
                    UnityWebRequest request = requests[j];
                    string file = batchFiles[j];
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Failed to download '{file}': {request.error}";
                            yield break;
                        }

                        string destination = Path.Combine(tempRoot, PackageUpdater.ToPlatformPath(file));
                        string directory = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.WriteAllBytes(destination, request.downloadHandler.data);
                        completed++;
                        onProgress?.Invoke(Mathf.Clamp01((float)completed / files.Count), $"Downloaded {completed}/{files.Count} files");
                    }
                    finally
                    {
                        request.Dispose();
                    }
                }
            }
        }
    }
}
#endif
