using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserApiClient
    {
        private static readonly HttpClient Client = CreateClient();

        internal static async Task<bool> UserExistsAsync(string uid, CancellationToken cancellationToken)
        {
            UserRecordDto user = await GetUserAsync(uid, cancellationToken);
            return user != null;
        }

        internal static async Task<UserRecordDto> GetUserAsync(string uid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return null;
            }

            string requestUrl = HoyoToonApi.UsersHttpUrl + "?UID=" + Uri.EscapeDataString(uid.Trim());
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            string payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw BuildRequestException("fetch user", response.StatusCode, payload);
            }

            return JsonSerializer.Deserialize<UserRecordDto>(payload, HoyoToonApi.JsonResolver);
        }

        internal static async Task<CreateUserResponseDto> CreateUserAsync(
            string username,
            string avatar,
            CancellationToken cancellationToken)
        {
            var dto = new CreateUserRequestDto
            {
                username = username,
                avatar = avatar,
            };

            byte[] payload = JsonSerializer.Serialize(dto, HoyoToonApi.JsonResolver);
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, HoyoToonApi.UsersHttpUrl)
            {
                Content = content,
            };

            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
            string responsePayload = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new HoyoToonUserAlreadyExistsException();
            }

            if (!response.IsSuccessStatusCode)
            {
                throw BuildRequestException("create user", response.StatusCode, responsePayload);
            }

            return JsonSerializer.Deserialize<CreateUserResponseDto>(responsePayload, HoyoToonApi.JsonResolver);
        }

        internal static async Task<UpdateUserAvatarResponseDto> UpdateUserAvatarAsync(
            string uid,
            string avatar,
            CancellationToken cancellationToken)
        {
            var dto = new UpdateUserAvatarRequestDto
            {
                UID = uid,
                avatar = avatar,
            };

            byte[] payload = JsonSerializer.Serialize(dto, HoyoToonApi.JsonResolver);
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), HoyoToonApi.UsersHttpUrl)
            {
                Content = content,
            };

            using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
            string responsePayload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw BuildRequestException("update user avatar", response.StatusCode, responsePayload);
            }

            return JsonSerializer.Deserialize<UpdateUserAvatarResponseDto>(responsePayload, HoyoToonApi.JsonResolver);
        }

        private static Exception BuildRequestException(string action, HttpStatusCode statusCode, string payload)
        {
            string apiMessage = TryReadApiError(payload);
            string message = string.IsNullOrWhiteSpace(apiMessage)
                ? $"Unable to {action}. API returned {(int)statusCode} {statusCode}."
                : $"Unable to {action}: {apiMessage}";
            return new InvalidOperationException(message);
        }

        private static string TryReadApiError(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            try
            {
                UserErrorDto error = JsonSerializer.Deserialize<UserErrorDto>(payload, HoyoToonApi.JsonResolver);
                return error != null ? error.error ?? string.Empty : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
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

    internal sealed class HoyoToonUserAlreadyExistsException : Exception
    {
        internal HoyoToonUserAlreadyExistsException()
            : base("User already exists")
        {
        }
    }
}
