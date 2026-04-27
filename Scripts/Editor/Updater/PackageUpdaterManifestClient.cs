#if UNITY_EDITOR
using System;
using HoyoToon.Editor.API;
using UnityEngine.Networking;
using Utf8Json;

namespace HoyoToon.Editor.Updater
{
    internal static class PackageUpdaterManifestClient
    {
        private const string RepositoryOwner = "Hoyotoon";
        private const string RepositoryName = "HoyoToon";
        private const string ApiBaseUrl = "https://api.github.com";
        private const string RawBaseUrl = "https://raw.githubusercontent.com";
        private const string GitHubApiVersion = "2026-03-10";
        internal const string UserAgent = "HoyoToon-Unity-Updater";

        internal static string BuildBranchReferenceApiUrl(string branch)
        {
            string normalizedBranch = NormalizeBranch(branch);
            return $"{ApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/git/ref/heads/{Uri.EscapeDataString(normalizedBranch)}";
        }

        internal static string BuildManifestUrlForReference(string reference)
        {
            return BuildRawFileUrl(reference, "updater_manifest.json");
        }

        internal static string BuildRawBaseUrlForReference(string reference)
        {
            string normalizedReference = NormalizeContentReference(reference);
            return $"{RawBaseUrl}/{RepositoryOwner}/{RepositoryName}/{EscapeReference(normalizedReference)}";
        }

        internal static string BuildRawFileUrl(string reference, string relativePath)
        {
            return BuildRawFileUrlFromBase(BuildRawBaseUrlForReference(reference), relativePath);
        }

        internal static string BuildRawFileUrlFromBase(string baseUrl, string relativePath)
        {
            string normalizedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            if (!PackageUpdaterStorage.TryNormalizeRelativePath(relativePath, out string normalizedPath, out string error))
            {
                throw new InvalidOperationException($"Unsafe updater remote path '{relativePath}': {error}");
            }

            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string escapedPath = string.Join("/", Array.ConvertAll(segments, Uri.EscapeDataString));
            return string.Concat(normalizedBase, "/", escapedPath);
        }

        internal static string ExtractBranchHeadSha(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("GitHub reference response was empty.");
            }

            GitHubReferenceResponse response = JsonSerializer.Deserialize<GitHubReferenceResponse>(payload, HoyoToonApi.JsonResolver);
            if (string.IsNullOrWhiteSpace(response?.@object?.sha))
            {
                throw new InvalidOperationException("GitHub reference response did not include a commit SHA.");
            }

            return response.@object.sha.Trim();
        }

        internal static UpdaterManifest DeserializeManifest(string payload)
        {
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

        internal static void ApplyGitHubApiHeaders(UnityWebRequest request)
        {
            if (request == null)
            {
                return;
            }

            request.redirectLimit = 5;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("X-GitHub-Api-Version", GitHubApiVersion);
        }

        internal static void ApplyRawContentHeaders(UnityWebRequest request)
        {
            if (request == null)
            {
                return;
            }

            request.redirectLimit = 5;
            request.SetRequestHeader("User-Agent", UserAgent);
        }

        internal static string BuildRequestFailureMessage(UnityWebRequest request, string operation)
        {
            string operationLabel = string.IsNullOrWhiteSpace(operation) ? "GitHub request" : operation;
            if (request == null)
            {
                return $"{operationLabel} failed.";
            }

            string detail = TryReadGitHubErrorMessage(request);
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = string.IsNullOrWhiteSpace(request.error) ? "Unknown error." : request.error;
            }

            string rateLimitDetail = BuildRateLimitDetail(request);
            string statusDetail = request.responseCode > 0
                ? $"HTTP {request.responseCode}: "
                : string.Empty;

            return string.IsNullOrWhiteSpace(rateLimitDetail)
                ? $"{operationLabel} failed: {statusDetail}{detail}"
                : $"{operationLabel} failed: {statusDetail}{detail} {rateLimitDetail}";
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

        private static string TryReadGitHubErrorMessage(UnityWebRequest request)
        {
            string payload = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            try
            {
                GitHubApiErrorResponse error = JsonSerializer.Deserialize<GitHubApiErrorResponse>(payload, HoyoToonApi.JsonResolver);
                return error?.message ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string BuildRateLimitDetail(UnityWebRequest request)
        {
            if (request.responseCode != 403 && request.responseCode != 429)
            {
                return string.Empty;
            }

            string retryAfter = request.GetResponseHeader("Retry-After");
            if (!string.IsNullOrWhiteSpace(retryAfter))
            {
                return $"Retry after {retryAfter} second(s).";
            }

            string remaining = request.GetResponseHeader("X-RateLimit-Remaining");
            string reset = request.GetResponseHeader("X-RateLimit-Reset");
            if (string.Equals(remaining, "0", StringComparison.Ordinal) && long.TryParse(reset, out long resetEpochSeconds))
            {
                DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpochSeconds).ToLocalTime();
                return $"GitHub rate limit resets at {resetTime:yyyy-MM-dd HH:mm:ss zzz}.";
            }

            return string.Empty;
        }
    }
}
#endif
