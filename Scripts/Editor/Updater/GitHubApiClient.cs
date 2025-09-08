#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Utf8Json;
using HoyoToon.API;

namespace HoyoToon.Updater
{
    internal sealed class GitHubApiClient : IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;

        public GitHubApiClient(string owner, string repo, string branch, string token)
        {
            _owner = owner; _repo = repo; _branch = branch;
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("HoyoToon-Updater");
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (!string.IsNullOrEmpty(token))
            {
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<PackageInfo> GetPackageInfoAsync(string packageJsonPath)
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{packageJsonPath}?ref={_branch}";
            var json = await _client.GetStringAsync(url);
            GitFileInfo file = null;
            if (!HoyoToonApi.Parser.TryParse<GitFileInfo>(Encoding.UTF8.GetBytes(json), out file, out var _))
                return null;
            // API returns base64 with newlines, remove then decode
            var b64 = (file.content ?? string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(b64);
            var text = Encoding.UTF8.GetString(bytes);
            if (!HoyoToonApi.Parser.TryParse<PackageInfo>(Encoding.UTF8.GetBytes(text), out var pkg, out var _))
                return null;
            return pkg;
        }

        public async Task<GitTreeResponse> GetRepoTreeAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/git/trees/{_branch}?recursive=1";
            var json = await _client.GetStringAsync(url);
            if (!HoyoToonApi.Parser.TryParse<GitTreeResponse>(Encoding.UTF8.GetBytes(json), out var tree, out var _))
                return null;
            return tree;
        }

        public async Task<string> GetBranchHeadShaAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/commits/{_branch}";
            var json = await _client.GetStringAsync(url);
            // Minimal DTO for head commit response
            if (!HoyoToonApi.Parser.TryParse<HeadCommit>(Encoding.UTF8.GetBytes(json), out var head, out var _))
                return null;
            return head.sha;
        }

        public async Task<byte[]> DownloadRawAsync(string relativePath)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{relativePath}";
            // Simple retry (3 attempts)
            for (int i = 0; i < 3; i++)
            {
                try { return await _client.GetByteArrayAsync(url); }
                catch when (i < 2) { await Task.Delay(1000 * (i + 1)); }
            }
            throw new HttpRequestException($"Failed to download {relativePath}");
        }

        public async Task<byte[]> DownloadRawAtCommitAsync(string relativePath, string commitSha)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{commitSha}/{relativePath}";
            for (int i = 0; i < 3; i++)
            {
                try { return await _client.GetByteArrayAsync(url); }
                catch when (i < 2) { await Task.Delay(1000 * (i + 1)); }
            }
            throw new HttpRequestException($"Failed to download {relativePath} at {commitSha}");
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
            using (var resp = await _client.GetAsync(url))
            {
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                if (!HoyoToonApi.Parser.TryParse<ReleaseInfo>(Encoding.UTF8.GetBytes(json), out var rel, out var _))
                    return null;
                return rel;
            }
        }

        public async Task<string> GetRawTextAsync(string relativePath)
        {
            var url = $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{relativePath}";
            using (var resp = await _client.GetAsync(url))
            {
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        public void Dispose() => _client?.Dispose();

        private class HeadCommit
        {
            public string sha;
        }
    }
}
#endif
