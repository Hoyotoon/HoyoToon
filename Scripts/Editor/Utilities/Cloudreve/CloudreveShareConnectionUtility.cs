#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.Resources.Cloudreve;

namespace HoyoToon.Editor.Utilities.Cloudreve
{
    internal static class CloudreveShareConnectionUtility
    {
        internal static bool TryValidateFolderShare(
            CloudreveShareInfo shareInfo,
            CloudreveShareDescriptor descriptor,
            out string error)
        {
            error = null;
            string shareUrl = descriptor != null ? descriptor.ShareUrl : string.Empty;

            if (shareInfo == null)
            {
                error = $"Cloudreve did not return share information for '{shareUrl}'.";
                return false;
            }

            if (!shareInfo.unlocked)
            {
                error = $"The Cloudreve share '{shareUrl}' is locked or inaccessible.";
                return false;
            }

            if (shareInfo.expired)
            {
                error = $"The Cloudreve share '{shareUrl}' has expired.";
                return false;
            }

            if (shareInfo.source_type != 1)
            {
                error = $"The Cloudreve share '{shareUrl}' must point to a folder.";
                return false;
            }

            return true;
        }

        internal static async Task<string> ResolveRootUriAsync(
            CloudreveResourceClient client,
            CloudreveShareDescriptor descriptor,
            string cachedRootUri,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new InvalidOperationException("No Cloudreve client is available.");
            }

            if (descriptor == null)
            {
                throw new InvalidOperationException("No Cloudreve share descriptor is available.");
            }

            if (!string.IsNullOrWhiteSpace(cachedRootUri)
                && await CanListFolderAsync(client, cachedRootUri, cancellationToken).ConfigureAwait(false))
            {
                return cachedRootUri;
            }

            var failures = new List<string>();
            foreach (string candidateRootUri in descriptor.EnumerateCandidateRootUris())
            {
                try
                {
                    await client.ListFolderPageAsync(candidateRootUri, 0, 1, null, cancellationToken).ConfigureAwait(false);
                    return candidateRootUri;
                }
                catch (Exception exception)
                {
                    failures.Add($"{candidateRootUri} ({exception.Message})");
                }
            }

            throw new InvalidOperationException(
                $"Unable to detect a Cloudreve root URI for '{descriptor.ShareUrl}'. Tried: {string.Join("; ", failures)}");
        }

        private static async Task<bool> CanListFolderAsync(
            CloudreveResourceClient client,
            string folderUri,
            CancellationToken cancellationToken)
        {
            try
            {
                await client.ListFolderPageAsync(folderUri, 0, 1, null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
