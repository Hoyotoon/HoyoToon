using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;
using HoyoToon.Editor.API;

namespace HoyoToon.Editor.Utilities.API
{
    internal static class HoyoToonApiFetchUtility
    {
        private static readonly HttpClient Client = CreateClient();

        internal static async Task<HoyoToonApiPayloadResult<TRecord>> FetchArrayPayloadAsync<TRecord>(
            string url,
            string payloadName,
            CancellationToken cancellationToken)
        {
            try
            {
                string payload = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                List<TRecord> records = DeserializeArrayPayload<TRecord>(payload, payloadName);
                return new HoyoToonApiPayloadResult<TRecord>(payload, records, url);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Unable to fetch the HoyoToon {payloadName} payload from '{url}'.",
                    exception);
            }
        }

        internal static async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        internal static List<TRecord> DeserializeArrayPayload<TRecord>(string payload, string payloadName)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new List<TRecord>();
            }

            string trimmedPayload = payload.TrimStart();
            if (!trimmedPayload.StartsWith("[", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The HoyoToon {payloadName} payload must be a JSON array.");
            }

            return JsonSerializer.Deserialize<List<TRecord>>(payload, HoyoToonApi.JsonResolver) ?? new List<TRecord>();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };

            client.DefaultRequestHeaders.Add("User-Agent", "HoyoToon-Unity-Editor");
            return client;
        }
    }

    internal sealed class HoyoToonApiPayloadResult<TRecord>
    {
        internal HoyoToonApiPayloadResult(string rawPayload, List<TRecord> records, string source)
        {
            RawPayload = rawPayload ?? string.Empty;
            Records = records ?? new List<TRecord>();
            Source = source ?? string.Empty;
        }

        internal string RawPayload { get; }

        internal List<TRecord> Records { get; }

        internal string Source { get; }
    }
}