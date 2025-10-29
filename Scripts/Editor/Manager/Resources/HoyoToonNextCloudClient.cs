#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HoyoToon
{
    /// <summary>
    /// Specialized client for Cloudreve v4 public share operations
    /// Note: We keep the original class name for compatibility with callers.
    /// </summary>
    public static class HoyoToonNextCloudClient
    {
        /// <summary>
        /// Get file list from Cloudreve v4 public share with recursive traversal via official API
        /// </summary>
        public static async Task<List<RemoteFileInfo>> GetFileListAsync(string shareUrl)
        {
            if (string.IsNullOrWhiteSpace(shareUrl))
                throw new ArgumentException("shareUrl is required");

            var (baseApiUrl, shareId, password) = ParseCloudreveShareUrl(shareUrl);
            if (string.IsNullOrEmpty(baseApiUrl) || string.IsNullOrEmpty(shareId))
                throw new Exception($"Could not parse Cloudreve share URL: {shareUrl}");

            // Public share: we only need the 'share' filesystem.
            // Do not attempt 'shared_with_me' to avoid 401 noise since we're unauthenticated.
            var preferredHosts = new[] { "share" };

            HoyoToonLogs.LogDebug($"🔍 Listing Cloudreve share: {shareUrl}");
            var allFiles = new List<RemoteFileInfo>();

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                foreach (var host in preferredHosts)
                {
                    var rootAuthority = string.IsNullOrEmpty(password)
                        ? $"cloudreve://{shareId}@{host}"
                        : $"cloudreve://{shareId}:{password}@{host}";
                    try
                    {
                        var contextHint = string.Empty;
                        var fileUris = new List<string>();
                        var fileRelPaths = new List<string>();

                        async Task TraverseAsync(string currentPath)
                        {
                            // Build folder URI
                            var uri = BuildShareUri(rootAuthority, currentPath);

                            // Handle pagination; always include page params to satisfy strict servers
                            string nextPageToken = string.Empty;
                            int page = 0;
                            bool isCursor = true; // default to true; updated by response

                            do
                            {
                                var listUrl = new StringBuilder();
                                listUrl.Append($"{baseApiUrl}/file?uri={Uri.EscapeDataString(uri)}");
                                if (!string.IsNullOrEmpty(nextPageToken))
                                {
                                    listUrl.Append($"&next_page_token={Uri.EscapeDataString(nextPageToken)}");
                                }
                                // Always send page/page_size; server may ignore for cursor pagination
                                listUrl.Append("&page=").Append(page).Append("&page_size=2000");

                                var resp = await httpClient.GetAsync(listUrl.ToString());
                                var raw = await resp.Content.ReadAsStringAsync();
                                if (!resp.IsSuccessStatusCode)
                                {
                                    HoyoToonLogs.LogDebug($"List request failed ({resp.StatusCode}) host={host}: {raw}");
                                    throw new Exception($"List files HTTP {resp.StatusCode}");
                                }

                                var jo = JObject.Parse(raw);
                                var code = jo.Value<int?>("code") ?? -1;
                                var msg = jo.Value<string>("msg") ?? string.Empty;
                                if (code != 0)
                                {
                                    HoyoToonLogs.LogDebug($"List files returned code {code} host={host}: {msg}");
                                    throw new Exception($"Cloudreve list error: {msg} (code {code})");
                                }

                                var data = jo["data"] as JObject;
                                if (data == null)
                                    break;

                                contextHint = data.Value<string>("context_hint") ?? contextHint;

                                var pagination = data["pagination"] as JObject;
                                if (pagination != null)
                                {
                                    isCursor = pagination.Value<bool?>("is_cursor") ?? true;
                                    page = pagination.Value<int?>("page") ?? page;
                                }

                                // Some deployments return next_page_token at top-level of data when using cursor
                                nextPageToken = data.Value<string>("next_page_token") ?? string.Empty;

                                var files = data["files"] as JArray;
                                if (files == null || files.Count == 0)
                                    break;

                                foreach (var f in files)
                                {
                                    int type = f.Value<int>("type"); // 0 file, 1 folder
                                    string name = f.Value<string>("name") ?? string.Empty;
                                    string id = f.Value<string>("id") ?? string.Empty;
                                    string updatedAt = f.Value<string>("updated_at") ?? string.Empty;
                                    long size = f.Value<long?>("size") ?? 0L;

                                    if (type == 1)
                                    {
                                        // Folder - recurse
                                        var nextPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";
                                        await TraverseAsync(nextPath);
                                    }
                                    else if (type == 0)
                                    {
                                        // File - collect for later URL generation
                                        var relPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";
                                        var decodedRelPath = Uri.UnescapeDataString(relPath).Replace("\\", "/");
                                        var fileUri = BuildShareUri(rootAuthority, relPath);

                                        fileUris.Add(fileUri);
                                        fileRelPaths.Add(decodedRelPath);

                                        allFiles.Add(new RemoteFileInfo
                                        {
                                            RelativePath = decodedRelPath,
                                            DownloadUrl = string.Empty,
                                            Size = size,
                                            ETag = string.IsNullOrEmpty(updatedAt) ? id : $"{id}:{updatedAt}",
                                            IsDirectory = false
                                        });
                                    }
                                }

                                if (!isCursor)
                                    page++;

                            } while (isCursor ? !string.IsNullOrEmpty(nextPageToken) : true);
                        }

                        allFiles.Clear();
                        fileUris.Clear();
                        fileRelPaths.Clear();

                        await TraverseAsync("");

                        if (fileUris.Count > 0)
                        {
                            HoyoToonLogs.LogDebug($"Creating download URLs for {fileUris.Count} files via Cloudreve API (host={host})...");
                            var urlResults = await CreateDownloadUrlsAsync(httpClient, baseApiUrl, fileUris, contextHint);
                            for (int i = 0; i < allFiles.Count && i < urlResults.Count; i++)
                            {
                                allFiles[i].DownloadUrl = urlResults[i];
                            }
                        }

                        if (allFiles.Count > 0)
                        {
                            // Success with this host
                            break;
                        }
                        else
                        {
                            HoyoToonLogs.LogDebug($"No files found via host '{host}', will try alternative host if available...");
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogs.LogDebug($"Host '{host}' failed with error: {ex.Message}. Trying next host if available...");
                        // try next host
                        continue;
                    }
                }
            }

            HoyoToonLogs.LogDebug($"✅ Cloudreve listing complete: {allFiles.Count} files found");
            return allFiles;
        }

        // ===================== Cloudreve helpers =====================
        private static (string baseApiUrl, string shareId, string password) ParseCloudreveShareUrl(string shareUrl)
        {
            try
            {
                var uri = new Uri(shareUrl);
                // Base API is /api/v4 on same origin
                var baseApi = $"{uri.Scheme}://{uri.Host}/api/v4";

                // Path like /s/{id} or /s/{id}/{password}
                var segments = uri.AbsolutePath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                string shareId = null;
                string password = null;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == "s" && i + 1 < segments.Length)
                    {
                        shareId = segments[i + 1];
                        if (i + 2 < segments.Length)
                        {
                            password = segments[i + 2];
                        }
                        break;
                    }
                }
                return (baseApi, shareId, password);
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Error parsing Cloudreve share URL {shareUrl}: {ex.Message}");
                return (null, null, null);
            }
        }

        private static string BuildShareUri(string rootAuthority, string path)
        {
            if (string.IsNullOrEmpty(path)) return rootAuthority;
            var segments = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => Uri.EscapeDataString(s));
            return $"{rootAuthority}/{string.Join("/", segments)}";
        }

        private static async Task<List<string>> CreateDownloadUrlsAsync(HttpClient httpClient, string baseApiUrl, List<string> uris, string contextHint)
        {
            var result = new List<string>(new string[uris.Count]);

            // Chunk requests to avoid server limits
            const int chunkSize = 50;
            for (int i = 0; i < uris.Count; i += chunkSize)
            {
                var count = Math.Min(chunkSize, uris.Count - i);
                var batch = uris.GetRange(i, count);

                var req = new HttpRequestMessage(HttpMethod.Post, $"{baseApiUrl}/file/url");
                if (!string.IsNullOrEmpty(contextHint))
                {
                    req.Headers.Add("X-Cr-Context-Hint", contextHint);
                }

                var payload = new JObject
                {
                    ["uris"] = new JArray(batch),
                    ["download"] = true
                };
                req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                using (var resp = await httpClient.SendAsync(req))
                {
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();
                    var jo = JObject.Parse(json);
                    var data = jo["data"] as JObject;
                    var urls = data?["urls"] as JArray;
                    if (urls != null)
                    {
                        for (int j = 0; j < urls.Count; j++)
                        {
                            var idx = i + j;
                            if (idx >= result.Count) break;
                            result[idx] = urls[j]?["url"]?.ToString();
                        }
                    }
                }
            }

            // Fallback for any missing entries
            for (int k = 0; k < result.Count; k++)
            {
                if (string.IsNullOrEmpty(result[k]))
                {
                    result[k] = string.Empty;
                }
            }

            return result;
        }


        /// <summary>
        /// Download file via direct URL (Cloudreve-generated). Token is ignored for compatibility.
        /// </summary>
        public static async Task DownloadFileAsync(HttpClient httpClient, string downloadUrl, string localPath, string token)
        {
            try
            {
                if (localPath.EndsWith("\\") || localPath.EndsWith("/"))
                    throw new ArgumentException($"Invalid file path - appears to be a directory: {localPath}");
                if (string.IsNullOrEmpty(downloadUrl) || downloadUrl.EndsWith("/"))
                    throw new ArgumentException($"Invalid download URL: {downloadUrl}");

                var directory = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to download file {downloadUrl} to {localPath}: {ex.Message}");
                throw;
            }
        }
    }
}
#endif