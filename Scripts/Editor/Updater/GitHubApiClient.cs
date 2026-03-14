#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Utf8Json;
using HoyoToon.Editor.API;

namespace HoyoToon.Editor.Updater
{
    internal sealed class GitHubApiClient
    {
        private static readonly HttpClient SharedClient = CreateSharedClient();
        private const int RetryAttempts = 3;
        private const int RetryBaseDelayMs = 1000;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;
        private readonly string _token;

        public GitHubApiClient(string owner, string repo, string branch, string token)
        {
            _owner = owner; _repo = repo; _branch = branch;
            _token = token;
        }

        public async Task<PackageInfo> GetPackageInfoAsync(string packageJsonPath)
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{packageJsonPath}?ref={_branch}";
            var json = await GetStringAsync(url);
            GitFileInfo file = null;
            if (!Api.Parser.TryParse<GitFileInfo>(Encoding.UTF8.GetBytes(json), out file, out var _))
                return null;
            // API returns base64 with newlines, remove then decode
            var b64 = (file.content ?? string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(b64);
            var text = Encoding.UTF8.GetString(bytes);
            if (!Api.Parser.TryParse<PackageInfo>(Encoding.UTF8.GetBytes(text), out var pkg, out var _))
                return null;
            return pkg;
        }

        public async Task<GitTreeResponse> GetRepoTreeAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/git/trees/{_branch}?recursive=1";
            var json = await GetStringAsync(url);
            if (!Api.Parser.TryParse<GitTreeResponse>(Encoding.UTF8.GetBytes(json), out var tree, out var _))
                return null;
            return tree;
        }

        public async Task<string> GetBranchHeadShaAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/commits/{_branch}";
            var json = await GetStringAsync(url);
            if (!Api.Parser.TryParse<HeadCommit>(Encoding.UTF8.GetBytes(json), out var head, out var _))
                return null;
            return head.sha;
        }

        public async Task<byte[]> DownloadRawAsync(string relativePath)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{relativePath}";
            return await RetryAsync(() => GetBytesAsync(url), $"download {relativePath}");
        }

        public async Task<byte[]> DownloadRawAtCommitAsync(string relativePath, string commitSha)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{commitSha}/{relativePath}";
            return await RetryAsync(() => GetBytesAsync(url), $"download {relativePath} at {commitSha}");
        }

        public async Task<string[]> GetBranchNamesAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/branches?per_page=100";
            var json = await RetryAsync(() => GetStringAsync(url), "list branches");
            if (!Api.Parser.TryParse<BranchInfo[]>(Encoding.UTF8.GetBytes(json), out var branches, out var _))
                return Array.Empty<string>();

            if (branches == null || branches.Length == 0) return Array.Empty<string>();
            var names = new string[branches.Length];
            for (int i = 0; i < branches.Length; i++)
                names[i] = branches[i]?.name;
            return names;
        }

        public class ReleaseInfo
        {
            public string tag_name;
            public string name;
            public string body;
            public string html_url;
        }

        public async Task<ReleaseInfo> GetReleaseByTagAsync(string tag)
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/tags/{Uri.EscapeDataString(tag)}";
            using (var resp = await SendAsync(url))
            {
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                if (!Api.Parser.TryParse<ReleaseInfo>(Encoding.UTF8.GetBytes(json), out var rel, out var _))
                    return null;
                return rel;
            }
        }

        public async Task<string> GetRawTextAsync(string relativePath)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{relativePath}";
            using (var resp = await SendAsync(url))
            {
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private static HttpClient CreateSharedClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HoyoToon-Updater");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(_token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }
            return request;
        }

        private async Task<HttpResponseMessage> SendAsync(string url)
        {
            using (var request = CreateRequest(HttpMethod.Get, url))
            {
                return await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
        }

        private async Task<string> GetStringAsync(string url)
        {
            using (var request = CreateRequest(HttpMethod.Get, url))
            using (var resp = await SharedClient.SendAsync(request))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private async Task<byte[]> GetBytesAsync(string url)
        {
            using (var request = CreateRequest(HttpMethod.Get, url))
            using (var resp = await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync();
            }
        }

        private static bool IsTransient(HttpRequestException ex)
        {
            return ex != null;
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, string operation)
        {
            Exception last = null;
            for (int i = 0; i < RetryAttempts; i++)
            {
                try
                {
                    return await action();
                }
                catch (HttpRequestException ex) when (i < RetryAttempts - 1 && IsTransient(ex))
                {
                    last = ex;
                }
                catch (TaskCanceledException ex) when (i < RetryAttempts - 1)
                {
                    last = ex;
                }

                if (i < RetryAttempts - 1)
                    await Task.Delay(RetryBaseDelayMs * (i + 1));
            }

            throw new HttpRequestException($"GitHub API retry failed for operation '{operation}'.", last);
        }

        private class HeadCommit
        {
            public string sha;
        }

        private class BranchInfo
        {
            public string name;
        }
    }
}
#endif
