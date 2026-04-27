#if UNITY_EDITOR
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.API;
using Utf8Json;

namespace HoyoToon.Editor.Updater
{
    internal static class PackageUpdaterManifestClient
    {
        private const string RepositoryOwner = "HoyoToon";
        private const string RepositoryName = "HoyoToon";

        private static readonly HttpClient Client = CreateClient();

        internal static string BuildManifestUrl(string branch)
        {
            string normalizedBranch = NormalizeBranch(branch);
            return BuildRawUrl(normalizedBranch, "updater_manifest.json");
        }

        internal static string BuildRawBaseUrl(string branch)
        {
            string normalizedBranch = NormalizeBranch(branch);
            return $"https://raw.githubusercontent.com/{RepositoryOwner}/{RepositoryName}/{normalizedBranch}";
        }

        internal static async Task<UpdaterManifest> FetchManifestAsync(string branch, CancellationToken cancellationToken)
        {
            string payload = await HoyoToonApiFetchUtility.GetStringAsync(BuildCacheBustedUrl(BuildManifestUrl(branch)), cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("Updater manifest response was empty.");
            }

            UpdaterManifest manifest = JsonSerializer.Deserialize<UpdaterManifest>(payload, HoyoToonApi.JsonResolver);
            if (manifest == null)
            {
                throw new InvalidOperationException("Updater manifest response was empty.");
            }

            return manifest;
        }

        internal static async Task<byte[]> DownloadFileBytesAsync(string branch, string relativePath, CancellationToken cancellationToken)
        {
            string requestUrl = BuildCacheBustedUrl(BuildRawUrl(NormalizeBranch(branch), relativePath));
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            ApplyNoCacheHeaders(request);
            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes == null)
            {
                throw new InvalidOperationException($"Downloaded '{relativePath}' but the response body was empty.");
            }

            return bytes;
        }

        internal static string BuildCacheBustedUrl(string url)
        {
            string separator = (url ?? string.Empty).Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return string.Concat(url ?? string.Empty, separator, "ts=", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        }

        private static string BuildRawUrl(string branch, string relativePath)
        {
            string normalizedBase = BuildRawBaseUrl(branch).TrimEnd('/');
            if (!PackageUpdaterStorage.TryNormalizeRelativePath(relativePath, out string normalizedPath, out string error))
            {
                throw new InvalidOperationException($"Unsafe updater remote path '{relativePath}': {error}");
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string escapedPath = string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
            return string.Concat(normalizedBase, "/", escapedPath);
        }

        private static string NormalizeBranch(string branch)
        {
            return PackageUpdaterStorage.NormalizeBranch(branch);
        }

        private static void ApplyNoCacheHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store, max-age=0");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Expires", "0");
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "HoyoToon-Unity-Updater");
            return client;
        }
    }
}
#endif
