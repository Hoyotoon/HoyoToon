#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API;
using Utf8Json;

namespace HoyoToon.Editor.Resources.Cloudreve
{
    internal sealed class CloudreveResourceClient
    {
        private static readonly HttpClient Client = CreateClient();
        private const int DefaultPageSize = 200;

        internal CloudreveResourceClient(string apiBaseUrl)
        {
            ApiBaseUrl = (apiBaseUrl ?? string.Empty).TrimEnd('/');
        }

        internal string ApiBaseUrl { get; }

        internal async Task<CloudreveShareInfo> GetShareInfoAsync(CloudreveShareDescriptor descriptor, CancellationToken cancellationToken)
        {
            string requestUrl = BuildShareInfoUrl(descriptor);
            CloudreveResponse<CloudreveShareInfo> response = await GetJsonAsync<CloudreveResponse<CloudreveShareInfo>>(requestUrl, null, cancellationToken)
                .ConfigureAwait(false);
            return EnsureSuccess(response, $"reading share info from '{requestUrl}'");
        }

        internal async Task<CloudreveListData> ListFolderPageAsync(
            string folderUri,
            int page,
            int pageSize,
            string nextToken,
            CancellationToken cancellationToken)
        {
            var queryParameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["uri"] = folderUri,
                ["page"] = Math.Max(0, page).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["page_size"] = Math.Max(1, pageSize).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["order_by"] = "name",
                ["order_direction"] = "asc",
            };

            if (!string.IsNullOrWhiteSpace(nextToken))
            {
                queryParameters["next_page_token"] = nextToken;
            }

            string requestUrl = BuildApiUrl("file", queryParameters);
            CloudreveResponse<CloudreveListData> response = await GetJsonAsync<CloudreveResponse<CloudreveListData>>(requestUrl, null, cancellationToken)
                .ConfigureAwait(false);
            return EnsureSuccess(response, $"listing files from '{folderUri}'");
        }

        internal async Task<List<RemoteResourceEntry>> ListAllFilesAsync(string rootUri, CancellationToken cancellationToken)
        {
            var results = new List<RemoteResourceEntry>();
            await ListFolderRecursiveAsync(rootUri, string.Empty, results, cancellationToken).ConfigureAwait(false);
            results.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        internal async Task<string> CreateDownloadUrlAsync(RemoteResourceEntry remoteFile, CancellationToken cancellationToken)
        {
            if (remoteFile == null || string.IsNullOrWhiteSpace(remoteFile.Uri))
            {
                throw new InvalidOperationException("A valid Cloudreve file URI is required to create a download URL.");
            }

            var request = new CloudreveDownloadUrlRequest
            {
                redirect = false,
                archive = false,
            };
            request.uris.Add(remoteFile.Uri);

            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(remoteFile.ContextHint))
            {
                headers["X-Cr-Context-Hint"] = remoteFile.ContextHint;
            }

            string requestUrl = BuildApiUrl("file/url");
            CloudreveResponse<CloudreveDownloadUrlData> response = await PostJsonAsync<CloudreveDownloadUrlRequest, CloudreveResponse<CloudreveDownloadUrlData>>(
                    requestUrl,
                    request,
                    headers,
                    cancellationToken)
                .ConfigureAwait(false);

            CloudreveDownloadUrlData data = EnsureSuccess(response, $"creating a download URL for '{remoteFile.RelativePath}'");
            string downloadUrl = data?.urls?.FirstOrDefault()?.url;
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException($"Cloudreve did not return a download URL for '{remoteFile.RelativePath}'.");
            }

            return downloadUrl;
        }

        internal async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        private async Task ListFolderRecursiveAsync(
            string folderUri,
            string relativePrefix,
            List<RemoteResourceEntry> results,
            CancellationToken cancellationToken)
        {
            string nextToken = null;
            int page = 0;
            int pageSize = DefaultPageSize;

            while (true)
            {
                CloudreveListData listData = await ListFolderPageAsync(folderUri, page, pageSize, nextToken, cancellationToken)
                    .ConfigureAwait(false);
                int effectivePageSize = listData?.props?.max_page_size > 0
                    ? Math.Min(listData.props.max_page_size, DefaultPageSize)
                    : pageSize;
                pageSize = effectivePageSize;

                foreach (CloudreveFileItem item in listData?.files ?? Enumerable.Empty<CloudreveFileItem>())
                {
                    if (item.type == 0 && item.name != null && item.name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string combinedRelativePath = string.IsNullOrWhiteSpace(relativePrefix)
                        ? item.name
                        : relativePrefix + "/" + item.name;

                    if (!ResourceSyncStorage.TryNormalizeManagedRelativePath(combinedRelativePath, out string normalizedRelativePath, out string pathError))
                    {
                        throw new InvalidOperationException(
                            $"Remote item '{combinedRelativePath}' from '{folderUri}' cannot be mapped into Unity resources: {pathError}");
                    }

                    if (item.type == 1)
                    {
                        await ListFolderRecursiveAsync(item.path, normalizedRelativePath, results, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    results.Add(new RemoteResourceEntry
                    {
                        RelativePath = normalizedRelativePath,
                        Uri = item.path ?? string.Empty,
                        ContextHint = listData?.context_hint ?? string.Empty,
                        UpdatedAt = item.updated_at ?? string.Empty,
                        Md5 = TryGetMetadataValue(item.metadata, "md5"),
                        Size = item.size,
                    });
                }

                CloudrevePagination pagination = listData?.pagination;
                if (pagination == null)
                {
                    break;
                }

                if (pagination.is_cursor)
                {
                    if (string.IsNullOrWhiteSpace(pagination.next_token))
                    {
                        break;
                    }

                    nextToken = pagination.next_token;
                    page = 0;
                    continue;
                }

                int fetchedCount = listData?.files?.Count ?? 0;
                if (fetchedCount <= 0)
                {
                    break;
                }

                page++;
                if (pagination.total_items > 0 && page * pagination.page_size >= pagination.total_items)
                {
                    break;
                }
            }
        }

        private static string TryGetMetadataValue(IReadOnlyDictionary<string, string> metadata, string key)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return metadata.TryGetValue(key, out string value)
                ? value ?? string.Empty
                : string.Empty;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2),
            };

            client.DefaultRequestHeaders.Add("User-Agent", "HoyoToon-Unity-Editor");
            return client;
        }

        private static TData EnsureSuccess<TData>(CloudreveResponse<TData> response, string operation)
        {
            if (response == null)
            {
                throw new InvalidOperationException($"Cloudreve returned an empty response while {operation}.");
            }

            if (response.code != 0)
            {
                string detail = !string.IsNullOrWhiteSpace(response.msg)
                    ? response.msg
                    : !string.IsNullOrWhiteSpace(response.error)
                        ? response.error
                        : "Unknown Cloudreve error.";

                throw new InvalidOperationException($"Cloudreve failed while {operation}: {detail}");
            }

            return response.data;
        }

        private string BuildShareInfoUrl(CloudreveShareDescriptor descriptor)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(descriptor.Password))
            {
                parameters["password"] = descriptor.Password;
            }

            return BuildApiUrl("share/info/" + Uri.EscapeDataString(descriptor.ShareId), parameters);
        }

        private string BuildApiUrl(string relativePath, IReadOnlyDictionary<string, string> queryParameters = null)
        {
            string requestUrl = string.Concat(ApiBaseUrl, "/", relativePath?.TrimStart('/') ?? string.Empty);
            if (queryParameters == null || queryParameters.Count <= 0)
            {
                return requestUrl;
            }

            string queryString = string.Join(
                "&",
                queryParameters
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

            return string.IsNullOrWhiteSpace(queryString)
                ? requestUrl
                : requestUrl + "?" + queryString;
        }

        private static async Task<TResponse> GetJsonAsync<TResponse>(
            string url,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddHeaders(request, headers);

            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<TResponse>(payload, HoyoToonApi.JsonResolver);
        }

        private static async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
            string url,
            TRequest requestBody,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddHeaders(request, headers);

            byte[] payload = JsonSerializer.Serialize(requestBody, HoyoToonApi.JsonResolver);
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;

            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string responsePayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<TResponse>(responsePayload, HoyoToonApi.JsonResolver);
        }

        private static void AddHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(header.Value))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }
    }
}
#endif