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
using HoyoToon.Utilities;

namespace HoyoToon
{
    /// <summary>
    /// Specialized client for NextCloud public share operations
    /// </summary>
    public static class HoyoToonNextCloudClient
    {
        // Reuse a single HttpClient instance per .NET guidelines
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            return client;
        }

        /// <summary>
        /// Get file list from NextCloud public share with comprehensive directory traversal
        /// </summary>
        public static async Task<List<RemoteFileInfo>> GetFileListAsync(string shareUrl)
        {
            var files = new List<RemoteFileInfo>();
            var token = ExtractTokenFromShareUrl(shareUrl);
            
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception($"Could not extract token from share URL: {shareUrl}");
            }

            HoyoToonLogger.ResourcesInfo($"Starting comprehensive file discovery for: {shareUrl}");
            HoyoToonLogger.ResourcesInfo($"Extracted token: {token}");

            // Use shared client for all fallback strategies
            var httpClient = SharedHttpClient;

                // Method 1: Try WebDAV PROPFIND
                try
                {
                    HoyoToonLogger.ResourcesInfo("Attempting WebDAV PROPFIND...");
                    files = await TryWebDavPropFind(httpClient, shareUrl, token);
                    if (files.Any())
                    {
                        HoyoToonLogger.ResourcesInfo($"WebDAV PROPFIND successful: {files.Count} files found");
                        return files;
                    }
                    else
                    {
                        HoyoToonLogger.ResourcesWarning("WebDAV PROPFIND returned no files");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"WebDAV PROPFIND failed: {ex.Message}");
                }

                // Method 2: Try NextCloud OCS API
                try
                {
                    HoyoToonLogger.ResourcesInfo("Attempting NextCloud OCS API...");
                    files = await TryOcsApi(httpClient, shareUrl, token);
                    if (files.Any())
                    {
                        HoyoToonLogger.ResourcesInfo($"OCS API successful: {files.Count} files found");
                        return files;
                    }
                    else
                    {
                        HoyoToonLogger.ResourcesWarning("OCS API returned no files");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"OCS API failed: {ex.Message}");
                }

                // Method 3: Try parsing the share page HTML with recursive traversal
                try
                {
                    HoyoToonLogger.ResourcesInfo("Attempting HTML parsing with recursive traversal...");
                    files = await TryHtmlParsing(httpClient, shareUrl, token);
                    if (files.Any())
                    {
                        HoyoToonLogger.ResourcesInfo($"HTML parsing successful: {files.Count} files found");
                        return files;
                    }
                    else
                    {
                        HoyoToonLogger.ResourcesWarning("HTML parsing returned no files");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"HTML parsing failed: {ex.Message}");
                }

                // Method 4: Enhanced manual discovery
                try
                {
                    HoyoToonLogger.ResourcesInfo("Attempting enhanced manual discovery...");
                    files = await TryManualDiscovery(httpClient, shareUrl, token);
                    if (files.Any())
                    {
                        HoyoToonLogger.ResourcesInfo($"Manual discovery successful: {files.Count} files found");
                        return files;
                    }
                    else
                    {
                        HoyoToonLogger.ResourcesWarning("Manual discovery returned no files");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"Manual discovery failed: {ex.Message}");
                }
            

            var errorMessage = $"All file discovery methods failed for NextCloud share: {shareUrl}";
            HoyoToonLogger.ResourcesError(errorMessage);
            throw new Exception("All file discovery methods failed for NextCloud share");
        }

        /// <summary>
        /// Method 1: WebDAV PROPFIND
        /// </summary>
        private static async Task<List<RemoteFileInfo>> TryWebDavPropFind(HttpClient httpClient, string shareUrl, string token)
        {
            var files = new List<RemoteFileInfo>();
            var baseUri = new Uri(shareUrl);
            var webdavUrl = $"{baseUri.Scheme}://{baseUri.Host}/public.php/webdav";

            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), webdavUrl);
            request.Headers.Add("Depth", "infinity");

            // Add authentication
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{token}:"));
            request.Headers.Add("Authorization", $"Basic {authToken}");

            var propfindBody = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <D:propfind xmlns:D=""DAV:"">
                    <D:prop>
                        <D:displayname/>
                        <D:getcontentlength/>
                        <D:getlastmodified/>
                        <D:getetag/>
                        <D:resourcetype/>
                    </D:prop>
                </D:propfind>";

            request.Content = new StringContent(propfindBody, Encoding.UTF8, "application/xml");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return ParseWebDavResponse(responseContent, webdavUrl, shareUrl, token);
        }

        /// <summary>
        /// Method 2: NextCloud OCS API
        /// </summary>
        private static async Task<List<RemoteFileInfo>> TryOcsApi(HttpClient httpClient, string shareUrl, string token)
        {
            var files = new List<RemoteFileInfo>();
            var baseUri = new Uri(shareUrl);

            // Try different OCS API endpoints
            var apiUrls = new[]
            {
                $"{baseUri.Scheme}://{baseUri.Host}/ocs/v2.php/apps/files_sharing/api/v1/shares/{token}?format=json",
                $"{baseUri.Scheme}://{baseUri.Host}/ocs/v1.php/apps/files_sharing/api/v1/shares/{token}?format=json",
                $"{baseUri.Scheme}://{baseUri.Host}/index.php/s/{token}/download"
            };

            foreach (var apiUrl in apiUrls)
            {
                try
                {
                    var response = await httpClient.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        
                        // Try to parse as JSON
                        if (content.StartsWith("{"))
                        {
                            var json = JObject.Parse(content);
                            // Parse the JSON response to extract file information
                            // This would need specific implementation based on the actual API response
                            HoyoToonLogger.ResourcesInfo($"OCS API response from {apiUrl}: {content}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"OCS API endpoint {apiUrl} failed: {ex.Message}");
                }
            }

            return files;
        }

        /// <summary>
        /// Method 3: Parse HTML from share page with recursive traversal
        /// </summary>
        private static async Task<List<RemoteFileInfo>> TryHtmlParsing(HttpClient httpClient, string shareUrl, string token)
        {
            var files = new List<RemoteFileInfo>();

            var response = await httpClient.GetAsync(shareUrl);
            response.EnsureSuccessStatusCode();

            var htmlContent = await response.Content.ReadAsStringAsync();
            
            HoyoToonLogger.ResourcesInfo($"Starting HTML parsing with recursive directory traversal for {shareUrl}");
            
            // Look for JSON data embedded in the page
            var jsonStart = htmlContent.IndexOf("window.OC.Share");
            if (jsonStart >= 0)
            {
                // Try to extract embedded JSON data
                var lines = htmlContent.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("window.OC") && line.Contains("files"))
                    {
                        HoyoToonLogger.ResourcesInfo($"Found potential file data in HTML: {line.Trim()}");
                    }
                }
            }

            // Look for file links in the HTML with recursive traversal
            files = await ParseFileLinksFromHtmlAsync(httpClient, htmlContent, shareUrl, token);

            HoyoToonLogger.ResourcesInfo($"HTML parsing completed, found {files.Count} total files");
            return files;
        }

        /// <summary>
        /// Method 4: Enhanced manual discovery with comprehensive directory exploration
        /// </summary>
        private static async Task<List<RemoteFileInfo>> TryManualDiscovery(HttpClient httpClient, string shareUrl, string token)
        {
            var files = new List<RemoteFileInfo>();
            
            HoyoToonLogger.ResourcesInfo($"Starting enhanced manual discovery for {shareUrl}");

            try
            {
                // First, try to get the root directory listing with various URL patterns
                var urlPatterns = new[]
                {
                    shareUrl,
                    $"{shareUrl}/",
                    $"{shareUrl}?path=/",
                    $"{shareUrl}&path=/"
                };

                foreach (var url in urlPatterns)
                {
                    try
                    {
                        var response = await httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            
                            // Use the new recursive HTML parsing
                            var discoveredFiles = await ParseFileLinksFromHtmlAsync(httpClient, content, shareUrl, token);
                            if (discoveredFiles.Any())
                            {
                                files.AddRange(discoveredFiles);
                                HoyoToonLogger.ResourcesInfo($"Manual discovery found {discoveredFiles.Count} files using URL pattern: {url}");
                                break; // Use the first successful pattern
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.ResourcesWarning($"Manual discovery URL pattern {url} failed: {ex.Message}");
                    }
                }

                // If still no files found, try alternative approaches
                if (!files.Any())
                {
                    // Try some common directory names (but don't limit to them)
                    var commonDirs = new[] { "Textures", "Materials", "Prefabs", "Shaders", "Assets", "Resources", "Common", "Shared" };
                    
                    foreach (var dir in commonDirs)
                    {
                        try
                        {
                            var dirUrl = $"{shareUrl.TrimEnd('/')}/{Uri.EscapeDataString(dir)}";
                            var response = await httpClient.GetAsync(dirUrl);
                            
                            if (response.IsSuccessStatusCode)
                            {
                                var content = await response.Content.ReadAsStringAsync();
                                var dirFiles = await ParseFileLinksFromHtmlAsync(httpClient, content, shareUrl, token, dir);
                                files.AddRange(dirFiles);
                                HoyoToonLogger.ResourcesInfo($"Manual discovery found {dirFiles.Count} files in directory: {dir}");
                            }
                        }
                        catch (Exception ex)
                        {
                            HoyoToonLogger.ResourcesWarning($"Manual discovery for directory {dir} failed: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ResourcesWarning($"Enhanced manual discovery failed: {ex.Message}");
            }

            HoyoToonLogger.ResourcesInfo($"Manual discovery completed, found {files.Count} total files");
            return files;
        }

        /// <summary>
        /// Parse file links from HTML content with recursive directory traversal
        /// </summary>
        private static async Task<List<RemoteFileInfo>> ParseFileLinksFromHtmlAsync(HttpClient httpClient, string htmlContent, string baseUrl, string token, string currentPath = "")
        {
            var files = new List<RemoteFileInfo>();
            var directories = new List<string>();
            var baseUri = new Uri(baseUrl);

            var lines = htmlContent.Split('\n');
            foreach (var line in lines)
            {
                // Look for various link patterns
                if (line.Contains("href=") && IsResourceFile(line))
                {
                    try
                    {
                        var href = ExtractHrefFromLine(line);
                        if (!string.IsNullOrEmpty(href) && !href.StartsWith("http") && !href.StartsWith("#") && href != "../")
                        {
                            // Decode URL encoding (convert %20 to spaces, etc.)
                            var originalHref = href;
                            var decodedHref = Uri.UnescapeDataString(href);
                            var isDir = IsDirectory(decodedHref);
                            
                            var fullPath = string.IsNullOrEmpty(currentPath) ? decodedHref : $"{currentPath}/{decodedHref}";
                            
                            if (isDir)
                            {
                                // Store directory for recursive traversal
                                directories.Add(fullPath.TrimEnd('/'));
                                HoyoToonLogger.ResourcesInfo($"Found directory: {fullPath}");
                            }
                            else
                            {
                                // Add file
                                var downloadUrl = CreateDownloadUrl(baseUri, string.IsNullOrEmpty(currentPath) ? originalHref : $"{currentPath}/{originalHref}", token);
                                
                                files.Add(new RemoteFileInfo
                                {
                                    RelativePath = fullPath.TrimStart('/'), // Use decoded path for local file system
                                    DownloadUrl = downloadUrl,              // Use encoded path for HTTP requests
                                    Size = 0, // Unknown from HTML
                                    ETag = "",
                                    IsDirectory = false
                                });

                                // Debug log for URL decoding
                                if (originalHref != decodedHref)
                                {
                                    HoyoToonLogger.ResourcesInfo($"Decoded HTML file path: '{originalHref}' -> '{decodedHref}' in '{currentPath}'");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.ResourcesWarning($"Error parsing link from line: {ex.Message}");
                    }
                }
            }

            // Recursively traverse directories
            foreach (var directory in directories)
            {
                try
                {
                    var directoryUrl = string.IsNullOrEmpty(currentPath) 
                        ? $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(directory)}"
                        : $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(directory)}";
                    
                    HoyoToonLogger.ResourcesInfo($"Exploring directory: {directoryUrl}");
                    
                    var response = await httpClient.GetAsync(directoryUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var dirContent = await response.Content.ReadAsStringAsync();
                        var subFiles = await ParseFileLinksFromHtmlAsync(httpClient, dirContent, baseUrl, token, directory);
                        files.AddRange(subFiles);
                    }
                    else
                    {
                        HoyoToonLogger.ResourcesWarning($"Failed to access directory {directoryUrl}: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ResourcesWarning($"Error exploring directory {directory}: {ex.Message}");
                }
            }

            return files;
        }

        /// <summary>
        /// Parse file links from HTML content (synchronous wrapper)
        /// </summary>
        private static List<RemoteFileInfo> ParseFileLinksFromHtml(string htmlContent, string baseUrl, string token)
        {
            // This is a synchronous wrapper for backward compatibility
            // In practice, we should use the async version
            return new List<RemoteFileInfo>();
        }

        /// <summary>
        /// Check if line contains a file or directory link
        /// </summary>
        private static bool IsResourceFile(string line)
        {
            // Accept any href link that's not a navigation link
            return line.Contains("href=") && 
                   !line.Contains("href=\"#\"") && 
                   !line.Contains("href=\"/\"") &&
                   !line.Contains("href=\"..\"") &&
                   !line.Contains("href=\"?") &&
                   (line.Contains("data-file") || line.Contains("data-dir") || 
                    line.Contains("class=") || line.Contains("title="));
        }

        /// <summary>
        /// Extract href attribute from HTML line
        /// </summary>
        private static string ExtractHrefFromLine(string line)
        {
            var hrefStart = line.IndexOf("href=\"");
            if (hrefStart < 0) return null;
            
            hrefStart += 6; // Length of "href=\""
            var hrefEnd = line.IndexOf("\"", hrefStart);
            
            if (hrefEnd > hrefStart)
            {
                return line.Substring(hrefStart, hrefEnd - hrefStart);
            }
            
            return null;
        }

        /// <summary>
        /// Check if path represents a directory
        /// </summary>
        private static bool IsDirectory(string path)
        {
            return path.EndsWith("/") || path.EndsWith("\\") || !Path.HasExtension(path);
        }

        /// <summary>
        /// Create download URL for NextCloud file
        /// </summary>
        private static string CreateDownloadUrl(Uri baseUri, string relativePath, string token)
        {
            // Keep the original URL encoding for the HTTP request
            var cleanPath = relativePath.TrimStart('/');
            
            // Try different download URL patterns
            var patterns = new[]
            {
                $"{baseUri.Scheme}://{baseUri.Host}/public.php/webdav/{cleanPath}",
                $"{baseUri.Scheme}://{baseUri.Host}/s/{token}/download?path=/&files={Uri.EscapeDataString(Path.GetFileName(Uri.UnescapeDataString(relativePath)))}",
                $"{baseUri.Scheme}://{baseUri.Host}/index.php/s/{token}/download?path={Uri.EscapeDataString(Path.GetDirectoryName(Uri.UnescapeDataString(relativePath)) ?? "")}&files={Uri.EscapeDataString(Path.GetFileName(Uri.UnescapeDataString(relativePath)))}"
            };

            return patterns[0]; // Start with WebDAV URL
        }

        /// <summary>
        /// Parse WebDAV XML response
        /// </summary>
        private static List<RemoteFileInfo> ParseWebDavResponse(string xmlContent, string webdavUrl, string shareUrl, string token)
        {
            var files = new List<RemoteFileInfo>();

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var ns = XNamespace.Get("DAV:");

                foreach (var response in doc.Descendants(ns + "response"))
                {
                    var href = response.Element(ns + "href")?.Value;
                    if (string.IsNullOrEmpty(href))
                        continue;

                    var propstat = response.Element(ns + "propstat");
                    var prop = propstat?.Element(ns + "prop");

                    if (prop == null)
                        continue;

                    var resourceType = prop.Element(ns + "resourcetype");
                    var isDirectory = resourceType?.Element(ns + "collection") != null;

                    var sizeElement = prop.Element(ns + "getcontentlength");
                    long.TryParse(sizeElement?.Value, out var size);

                    var etagElement = prop.Element(ns + "getetag");
                    var etag = etagElement?.Value?.Trim('"');

                    // Convert href to relative path and decode URL encoding
                    var uri = new Uri(href, UriKind.RelativeOrAbsolute);
                    var relativePath = uri.IsAbsoluteUri ? uri.AbsolutePath : href;

                    // Remove WebDAV base path
                    var baseUri = new Uri(webdavUrl);
                    var basePath = baseUri.AbsolutePath;
                    if (relativePath.StartsWith(basePath))
                    {
                        relativePath = relativePath.Substring(basePath.Length).TrimStart('/');
                    }

                    // Decode URL encoding (convert %20 to spaces, etc.)
                    var originalPath = relativePath;
                    relativePath = Uri.UnescapeDataString(relativePath);

                    if (!string.IsNullOrEmpty(relativePath) && relativePath != "/" && !isDirectory)
                    {
                        // Create download URL using original encoded path for HTTP request
                        var downloadUrl = CreateDownloadUrl(new Uri(shareUrl), originalPath, token);

                        files.Add(new RemoteFileInfo
                        {
                            RelativePath = relativePath, // Use decoded path for local file system
                            DownloadUrl = downloadUrl,   // Use encoded path for HTTP requests
                            Size = size,
                            ETag = etag,
                            IsDirectory = false
                        });

                        // Debug log for URL decoding
                        if (originalPath != relativePath)
                        {
                            HoyoToonLogger.ResourcesInfo($"Decoded file path: '{originalPath}' -> '{relativePath}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ResourcesError($"Failed to parse WebDAV response: {ex.Message}");
                throw;
            }

            return files;
        }

        /// <summary>
        /// Extract token from NextCloud share URL
        /// </summary>
        public static string ExtractTokenFromShareUrl(string shareUrl)
        {
            try
            {
                var uri = new Uri(shareUrl);
                var pathSegments = uri.AbsolutePath.Split('/');

                // Look for /s/TOKEN pattern
                for (int i = 0; i < pathSegments.Length - 1; i++)
                {
                    if (pathSegments[i] == "s" && !string.IsNullOrEmpty(pathSegments[i + 1]))
                    {
                        return pathSegments[i + 1];
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ResourcesError($"Error extracting token from URL {shareUrl}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Download file with NextCloud authentication
        /// </summary>
        public static async Task DownloadFileAsync(HttpClient httpClient, string downloadUrl, string localPath, string token)
        {
            try
            {
                // Validate that we're not trying to download a directory
                if (localPath.EndsWith("\\") || localPath.EndsWith("/"))
                {
                    throw new ArgumentException($"Invalid file path - appears to be a directory: {localPath}");
                }

                // Validate that the download URL doesn't point to a directory
                if (downloadUrl.EndsWith("/"))
                {
                    throw new ArgumentException($"Invalid download URL - appears to be a directory: {downloadUrl}");
                }

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(localPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

                // Add authentication for NextCloud public share
                if (!string.IsNullOrEmpty(token))
                {
                    var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{token}:"));
                    request.Headers.Add("Authorization", $"Basic {authToken}");
                }

                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
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
                HoyoToonLogger.ResourcesError($"Failed to download file {downloadUrl} to {localPath}: {ex.Message}");
                throw;
            }
        }
    }
}
#endif