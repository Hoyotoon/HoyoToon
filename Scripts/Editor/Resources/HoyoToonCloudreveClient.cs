#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HoyoToon.Utilities;
using Utf8Json;
using System.Linq;

namespace HoyoToon
{
    /// <summary>
    /// Specialized client for Cloudreve V4 public share operations
    /// </summary>
    public static class HoyoToonCloudreveClient
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public static HttpClient SharedClient => SharedHttpClient;

        private static bool IsThumbMarkerFile(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) && relativePath.EndsWith("._thumb", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get file list from Cloudreve public share with recursive traversal via V4 API
        /// </summary>
        public static async Task<List<RemoteFileInfo>> GetFileListAsync(string shareUrl)
        {
            return await GetFileListAsync(shareUrl, string.Empty, true);
        }

        /// <summary>
        /// Get file list from Cloudreve public share, optionally scoped to a subdirectory.
        /// </summary>
        public static async Task<List<RemoteFileInfo>> GetFileListAsync(string shareUrl, string relativePath, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(shareUrl))
                throw new ArgumentException("shareUrl is required");

            var (baseApiUrl, shareId, password) = ParseCloudreveShareUrl(shareUrl);
            if (string.IsNullOrEmpty(baseApiUrl) || string.IsNullOrEmpty(shareId))
            {
                throw new Exception($"Could not parse Cloudreve share URL: {shareUrl}");
            }

            var normalizedPath = relativePath ?? string.Empty;
            normalizedPath = normalizedPath.Trim().Trim('/');

            HoyoToonLogger.ResourcesInfo($"Starting Cloudreve V4 file discovery for: {shareUrl} (path='{normalizedPath}')");

            // Public share: we only need the 'share' filesystem.
            var preferredHosts = new[] { "share" };
            var allFiles = new List<RemoteFileInfo>();
            bool success = false;

            foreach (var host in preferredHosts)
            {
                var rootAuthority = BuildRootAuthority(shareId, password, host);

                try
                {
                    if (recursive)
                    {
                        await TraverseDirectoryV4Async(SharedHttpClient, baseApiUrl, rootAuthority, normalizedPath, allFiles);
                    }
                    else
                    {
                        var entries = await ListDirectoryV4Async(SharedHttpClient, baseApiUrl, rootAuthority, normalizedPath);
                        foreach (var entry in entries)
                        {
                            allFiles.Add(new RemoteFileInfo
                            {
                                RelativePath = entry.RelativePath,
                                Size = entry.Size,
                                IsDirectory = entry.IsDirectory
                            });
                        }
                    }

                    success = true;
                    break; // Success with this host
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"Host '{host}' failed: {ex.Message}. Trying next host...");
                }
            }

            if (!success)
            {
                throw new Exception("Failed to list files from Cloudreve share using all available hosts.");
            }

            HoyoToonLogger.ResourcesInfo($"Cloudreve discovery completed, found {allFiles.Count} total entries");
            return allFiles;
        }

        /// <summary>
        /// List entries within a single Cloudreve directory (non-recursive).
        /// </summary>
        public static async Task<List<RemoteEntryInfo>> GetDirectoryEntriesAsync(string shareUrl, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(shareUrl))
                throw new ArgumentException("shareUrl is required");

            var (baseApiUrl, shareId, password) = ParseCloudreveShareUrl(shareUrl);
            if (string.IsNullOrEmpty(baseApiUrl) || string.IsNullOrEmpty(shareId))
            {
                throw new Exception($"Could not parse Cloudreve share URL: {shareUrl}");
            }

            var normalizedPath = relativePath ?? string.Empty;
            normalizedPath = normalizedPath.Trim().Trim('/');

            var rootAuthority = BuildRootAuthority(shareId, password, "share");
            return await ListDirectoryV4Async(SharedHttpClient, baseApiUrl, rootAuthority, normalizedPath);
        }

        private static async Task TraverseDirectoryV4Async(HttpClient client, string baseApiUrl, string rootAuthority, string currentPath, List<RemoteFileInfo> allFiles)
        {
            // Build folder URI
            var uri = BuildShareUri(rootAuthority, currentPath);
            
            string nextPageToken = "";
            int page = 1;
            bool isCursor = true;
            string contextHint = "";

            // We need to collect files in this directory to batch resolve their URLs later
            var filesInThisDir = new List<RemoteFileInfo>();
            var fileUrisToResolve = new List<string>();

            do
            {
                var listUrlBuilder = new StringBuilder();
                listUrlBuilder.Append($"{baseApiUrl}/file?uri={Uri.EscapeDataString(uri)}");
                
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    listUrlBuilder.Append($"&next_page_token={Uri.EscapeDataString(nextPageToken)}");
                }
                
                // Always send page/page_size
                listUrlBuilder.Append($"&page={page}&page_size=1000");

                var response = await client.GetAsync(listUrlBuilder.ToString());
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"List files HTTP {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CloudreveV4ListResponse>(json);

                if (result.Code != 0)
                {
                    throw new Exception($"Cloudreve list error: {result.Msg} (code {result.Code})");
                }

                if (result.Data == null) break;

                contextHint = result.Data.ContextHint;
                nextPageToken = result.Data.NextPageToken;
                
                if (result.Data.Pagination != null)
                {
                    isCursor = result.Data.Pagination.IsCursor;
                    page = result.Data.Pagination.Page;
                }

                if (result.Data.Files != null)
                {
                    foreach (var f in result.Data.Files)
                    {
                        var name = f.Name;
                        var type = f.Type; // 0 file, 1 folder
                        
                        var nextPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";
                        
                        if (type == 1)
                        {
                            // Folder - recurse
                            await TraverseDirectoryV4Async(client, baseApiUrl, rootAuthority, nextPath, allFiles);
                        }
                        else
                        {
                            // File
                            var decodedRelPath = nextPath.Replace("\\", "/"); // Simple normalization

                            // We never download or cache Cloudreve thumbnail marker files.
                            if (IsThumbMarkerFile(decodedRelPath))
                                continue;

                            var fileUri = BuildShareUri(rootAuthority, nextPath);

                            var rfi = new RemoteFileInfo
                            {
                                RelativePath = decodedRelPath,
                                Size = f.Size,
                                IsDirectory = false,
                                ETag = f.Id, // Use ID as ETag
                                DownloadUrl = "" // To be resolved
                            };

                            filesInThisDir.Add(rfi);
                            fileUrisToResolve.Add(fileUri);
                            allFiles.Add(rfi);
                        }
                    }
                }

                if (!isCursor) page++;

            } while (isCursor && !string.IsNullOrEmpty(nextPageToken));

            // Batch resolve download URLs for files in this directory
            if (fileUrisToResolve.Count > 0)
            {
                var urls = await CreateDownloadUrlsAsync(client, baseApiUrl, fileUrisToResolve, contextHint);
                for (int i = 0; i < filesInThisDir.Count && i < urls.Count; i++)
                {
                    filesInThisDir[i].DownloadUrl = urls[i];
                }
            }
        }

        private static async Task<List<RemoteEntryInfo>> ListDirectoryV4Async(HttpClient client, string baseApiUrl, string rootAuthority, string currentPath)
        {
            var entries = new List<RemoteEntryInfo>();
            var uri = BuildShareUri(rootAuthority, currentPath);

            string nextPageToken = "";
            int page = 1;
            bool isCursor = true;

            do
            {
                var listUrlBuilder = new StringBuilder();
                listUrlBuilder.Append($"{baseApiUrl}/file?uri={Uri.EscapeDataString(uri)}");

                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    listUrlBuilder.Append($"&next_page_token={Uri.EscapeDataString(nextPageToken)}");
                }

                listUrlBuilder.Append($"&page={page}&page_size=1000");

                var response = await client.GetAsync(listUrlBuilder.ToString());
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"List files HTTP {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CloudreveV4ListResponse>(json);

                if (result.Code != 0)
                {
                    throw new Exception($"Cloudreve list error: {result.Msg} (code {result.Code})");
                }

                if (result.Data == null) break;

                nextPageToken = result.Data.NextPageToken;
                if (result.Data.Pagination != null)
                {
                    isCursor = result.Data.Pagination.IsCursor;
                    page = result.Data.Pagination.Page;
                }

                if (result.Data.Files != null)
                {
                    foreach (var f in result.Data.Files)
                    {
                        var name = f.Name;
                        var type = f.Type;
                        var nextPath = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath}/{name}";
                        var decodedRelPath = nextPath.Replace("\\", "/");

                        entries.Add(new RemoteEntryInfo
                        {
                            Name = name,
                            RelativePath = decodedRelPath,
                            IsDirectory = type == 1,
                            Size = f.Size
                        });
                    }
                }

                if (!isCursor) page++;
            } while (isCursor && !string.IsNullOrEmpty(nextPageToken));

            return entries;
        }

        private static async Task<List<string>> CreateDownloadUrlsAsync(HttpClient client, string baseApiUrl, List<string> uris, string contextHint)
        {
            var result = new List<string>(new string[uris.Count]);
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

                var payload = new CloudreveV4UrlRequest
                {
                    Uris = batch,
                    Download = true
                };
                
                var jsonBody = JsonSerializer.ToJsonString(payload);
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var resp = await client.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                
                var json = await resp.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<CloudreveV4UrlResponse>(json);

                if (responseData.Data?.Urls != null)
                {
                    for (int j = 0; j < responseData.Data.Urls.Count; j++)
                    {
                        var idx = i + j;
                        if (idx < result.Count)
                        {
                            result[idx] = responseData.Data.Urls[j].Url;
                        }
                    }
                }
            }
            return result;
        }

        public static async Task DownloadFileAsync(HttpClient client, RemoteFileInfo file, string shareUrl, string localPath)
        {
             string downloadUrl = file.DownloadUrl;

             if (string.IsNullOrEmpty(downloadUrl))
             {
                 throw new Exception($"Download URL not found for {file.RelativePath}. V4 API requires URL resolution during listing.");
             }

            try
            {
                var directory = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
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
                HoyoToonLogger.ResourcesError($"Failed to download file content for {file.RelativePath}: {ex.Message}");
                throw;
            }
        }

        private static (string baseApiUrl, string shareId, string password) ParseCloudreveShareUrl(string shareUrl)
        {
            try
            {
                var uri = new Uri(shareUrl);
                var baseApi = $"{uri.Scheme}://{uri.Host}/api/v4";
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

                if (string.IsNullOrEmpty(shareId))
                {
                    var pathParam = GetQueryParam(uri, "path");
                    if (!string.IsNullOrEmpty(pathParam) && pathParam.StartsWith("cloudreve://", StringComparison.OrdinalIgnoreCase))
                    {
                        TryParseCloudreveAuthority(pathParam, out shareId, out password);
                    }
                }
                return (baseApi, shareId, password);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Cloudreve.ParseUrl", $"Failed to parse Cloudreve share URL: {ex.Message}");
                return (null, null, null);
            }
        }

        private static string BuildRootAuthority(string shareId, string password, string host)
        {
            return string.IsNullOrEmpty(password)
                ? $"cloudreve://{shareId}@{host}"
                : $"cloudreve://{shareId}:{password}@{host}";
        }

        private static bool TryParseCloudreveAuthority(string cloudrevePath, out string shareId, out string password)
        {
            shareId = null;
            password = null;
            if (string.IsNullOrEmpty(cloudrevePath))
            {
                return false;
            }

            var working = cloudrevePath.StartsWith("cloudreve://", StringComparison.OrdinalIgnoreCase)
                ? cloudrevePath.Substring("cloudreve://".Length)
                : cloudrevePath;

            var atIndex = working.IndexOf('@');
            var userInfo = atIndex >= 0 ? working.Substring(0, atIndex) : working;
            if (string.IsNullOrEmpty(userInfo))
            {
                return false;
            }

            var parts = userInfo.Split(new[] { ':' }, 2, StringSplitOptions.None);
            shareId = parts.Length > 0 ? parts[0] : null;
            password = parts.Length > 1 ? parts[1] : null;

            return !string.IsNullOrEmpty(shareId);
        }

        private static string GetQueryParam(Uri uri, string key)
        {
            if (uri == null || string.IsNullOrEmpty(uri.Query) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var query = uri.Query;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            var pairs = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var idx = pair.IndexOf('=');
                var k = idx >= 0 ? pair.Substring(0, idx) : pair;
                if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var v = idx >= 0 ? pair.Substring(idx + 1) : string.Empty;
                return Uri.UnescapeDataString(v ?? string.Empty);
            }

            return null;
        }

        private static string BuildShareUri(string rootAuthority, string path)
        {
            if (string.IsNullOrEmpty(path)) return rootAuthority;
            var segments = path.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => Uri.EscapeDataString(s));
            return $"{rootAuthority}/{string.Join("/", segments)}";
        }

        public class RemoteEntryInfo
        {
            public string Name { get; set; }
            public string RelativePath { get; set; }
            public long Size { get; set; }
            public bool IsDirectory { get; set; }
        }

        // DTOs
        public class CloudreveV4ListResponse
        {
            [System.Runtime.Serialization.DataMember(Name = "code")]
            public int Code { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "msg")]
            public string Msg { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "data")]
            public CloudreveV4Data Data { get; set; }
        }

        public class CloudreveV4Data
        {
            [System.Runtime.Serialization.DataMember(Name = "files")]
            public List<CloudreveV4File> Files { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "pagination")]
            public CloudreveV4Pagination Pagination { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "context_hint")]
            public string ContextHint { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "next_page_token")]
            public string NextPageToken { get; set; }
        }

        public class CloudreveV4File
        {
            [System.Runtime.Serialization.DataMember(Name = "id")]
            public string Id { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "name")]
            public string Name { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "type")]
            public int Type { get; set; } // 0=file, 1=dir
            [System.Runtime.Serialization.DataMember(Name = "size")]
            public long Size { get; set; }
        }

        public class CloudreveV4Pagination
        {
            [System.Runtime.Serialization.DataMember(Name = "page")]
            public int Page { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "is_cursor")]
            public bool IsCursor { get; set; }
        }

        public class CloudreveV4UrlRequest
        {
            [System.Runtime.Serialization.DataMember(Name = "uris")]
            public List<string> Uris { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "download")]
            public bool Download { get; set; }
        }

        public class CloudreveV4UrlResponse
        {
            [System.Runtime.Serialization.DataMember(Name = "code")]
            public int Code { get; set; }
            [System.Runtime.Serialization.DataMember(Name = "data")]
            public CloudreveV4UrlData Data { get; set; }
        }

        public class CloudreveV4UrlData
        {
            [System.Runtime.Serialization.DataMember(Name = "urls")]
            public List<CloudreveV4UrlItem> Urls { get; set; }
        }

        public class CloudreveV4UrlItem
        {
            [System.Runtime.Serialization.DataMember(Name = "url")]
            public string Url { get; set; }
        }
    }
}
#endif