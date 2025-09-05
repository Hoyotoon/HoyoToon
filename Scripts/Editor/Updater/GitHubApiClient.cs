#if UNITY_EDITOR
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
            var file = JsonConvert.DeserializeObject<GitFileInfo>(json);
            // API returns base64 with newlines, remove then decode
            var b64 = (file.content ?? string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(b64);
            var text = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<PackageInfo>(text);
        }

        public async Task<GitTreeResponse> GetRepoTreeAsync()
        {
            var url = $"https://api.github.com/repos/{_owner}/{_repo}/git/trees/{_branch}?recursive=1";
            var json = await _client.GetStringAsync(url);
            return JsonConvert.DeserializeObject<GitTreeResponse>(json);
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
                return JsonConvert.DeserializeObject<ReleaseInfo>(json);
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
    }
}
#endif
