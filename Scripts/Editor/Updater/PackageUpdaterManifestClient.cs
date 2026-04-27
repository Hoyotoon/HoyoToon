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
        internal const string UserAgent = "HoyoToon-Unity-Updater";

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

        internal static string BuildBranchCommitApiUrl(string branch)
        {
            string normalizedBranch = NormalizeBranch(branch);
            return $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/commits/{Uri.EscapeDataString(normalizedBranch)}";
        }

        internal static string BuildManifestUrlForReference(string reference)
        {
            return BuildRawUrlForReference(reference, "updater_manifest.json");
        }

        internal static string BuildRawBaseUrlForReference(string reference)
        {
            string normalizedReference = NormalizeContentReference(reference);
            return $"https://raw.githubusercontent.com/{RepositoryOwner}/{RepositoryName}/{EscapeReference(normalizedReference)}";
        }

        internal static string ExtractCommitSha(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("GitHub branch response was empty.");
            }

            GitHubCommitReferenceResponse response = JsonSerializer.Deserialize<GitHubCommitReferenceResponse>(payload, HoyoToonApi.JsonResolver);
            if (string.IsNullOrWhiteSpace(response?.sha))
            {
                throw new InvalidOperationException("GitHub branch response did not include a commit SHA.");
            }

            return response.sha.Trim();
        }

        internal static async Task<string> ResolveBranchContentReferenceAsync(string branch, CancellationToken cancellationToken)
        {
            string requestUrl = BuildCacheBustedUrl(BuildBranchCommitApiUrl(branch));
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            ApplyNoCacheHeaders(request);
            ApplyGitHubApiHeaders(request);
            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ExtractCommitSha(payload);
        }

        internal static async Task<UpdaterManifest> FetchManifestAsync(string branch, CancellationToken cancellationToken)
        {
            string contentReference = await ResolveBranchContentReferenceAsync(branch, cancellationToken).ConfigureAwait(false);
            string payload = await HoyoToonApiFetchUtility.GetStringAsync(BuildCacheBustedUrl(BuildManifestUrlForReference(contentReference)), cancellationToken)
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
            string contentReference = await ResolveBranchContentReferenceAsync(branch, cancellationToken).ConfigureAwait(false);
            string requestUrl = BuildCacheBustedUrl(BuildRawUrlForReference(contentReference, relativePath));
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
            return BuildRawUrlFromBase(normalizedBase, relativePath);
        }

        private static string BuildRawUrlForReference(string reference, string relativePath)
        {
            string normalizedBase = BuildRawBaseUrlForReference(reference).TrimEnd('/');
            return BuildRawUrlFromBase(normalizedBase, relativePath);
        }

        private static string BuildRawUrlFromBase(string normalizedBase, string relativePath)
        {
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

        private static string NormalizeContentReference(string reference)
        {
            return string.IsNullOrWhiteSpace(reference)
                ? PackageUpdaterStorage.DefaultBranch
                : reference.Trim();
        }

        private static string EscapeReference(string reference)
        {
            string[] segments = NormalizeContentReference(reference)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            return segments.Length == 0
                ? PackageUpdaterStorage.DefaultBranch
                : string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
        }

        private static void ApplyNoCacheHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store, max-age=0");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Expires", "0");
        }

        private static void ApplyGitHubApiHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            return client;
        }
    }
}
#endif
