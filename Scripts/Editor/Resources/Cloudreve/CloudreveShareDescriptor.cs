#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace HoyoToon.Editor.Resources.Cloudreve
{
    internal sealed class CloudreveShareDescriptor
    {
        internal CloudreveShareDescriptor(string shareUrl, string apiBaseUrl, string shareId, string password)
        {
            ShareUrl = shareUrl ?? string.Empty;
            ApiBaseUrl = apiBaseUrl?.TrimEnd('/') ?? string.Empty;
            ShareId = shareId ?? string.Empty;
            Password = password ?? string.Empty;
        }

        internal string ShareUrl { get; }

        internal string ApiBaseUrl { get; }

        internal string ShareId { get; }

        internal string Password { get; }

        internal IEnumerable<string> EnumerateCandidateRootUris()
        {
            string encodedShareId = Uri.EscapeDataString(ShareId);
            string encodedPassword = Uri.EscapeDataString(Password ?? string.Empty);

            var candidates = new List<string>
            {
                $"cloudreve://{encodedShareId}@share",
                $"cloudreve://{encodedShareId}@share/",
                $"cloudreve://{encodedShareId}@shared_with_me",
                $"cloudreve://{encodedShareId}@shared_with_me/",
            };

            if (!string.IsNullOrWhiteSpace(Password))
            {
                candidates.Insert(0, $"cloudreve://{encodedShareId}:{encodedPassword}@share");
                candidates.Insert(1, $"cloudreve://{encodedShareId}:{encodedPassword}@share/");
                candidates.Add($"cloudreve://{encodedShareId}:{encodedPassword}@shared_with_me");
                candidates.Add($"cloudreve://{encodedShareId}:{encodedPassword}@shared_with_me/");
            }

            return candidates.Distinct(StringComparer.Ordinal);
        }
    }

    internal static class CloudreveShareUriParser
    {
        internal static bool TryParse(string rawUrl, out CloudreveShareDescriptor descriptor, out string error)
        {
            descriptor = null;
            error = null;

            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                error = "No Cloudreve share URL is configured on the selected HoyoToonResources asset.";
                return false;
            }

            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri shareUri))
            {
                error = $"'{rawUrl}' is not a valid absolute Cloudreve share URL.";
                return false;
            }

            string[] pathSegments = shareUri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int shareMarkerIndex = Array.FindIndex(pathSegments, segment => string.Equals(segment, "s", StringComparison.OrdinalIgnoreCase));
            if (shareMarkerIndex < 0 || shareMarkerIndex + 1 >= pathSegments.Length)
            {
                error = $"The share URL '{rawUrl}' must contain '/s/{{shareId}}'.";
                return false;
            }

            string shareId = Uri.UnescapeDataString(pathSegments[shareMarkerIndex + 1]);
            if (string.IsNullOrWhiteSpace(shareId))
            {
                error = $"The share URL '{rawUrl}' does not contain a valid share ID.";
                return false;
            }

            string password = shareMarkerIndex + 2 < pathSegments.Length
                ? Uri.UnescapeDataString(pathSegments[shareMarkerIndex + 2])
                : string.Empty;

            string basePath = string.Join("/", pathSegments.Take(shareMarkerIndex));
            string authority = shareUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            string apiBaseUrl = string.IsNullOrWhiteSpace(basePath)
                ? authority + "/api/v4"
                : authority + "/" + basePath.Trim('/') + "/api/v4";

            descriptor = new CloudreveShareDescriptor(rawUrl.Trim(), apiBaseUrl, shareId, password);
            return true;
        }
    }
}
#endif