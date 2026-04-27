#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.Resources;
using HoyoToon.Editor.Resources.Cloudreve;
using HoyoToon.Editor.Utilities.Cloudreve;

namespace HoyoToon.Editor.Assets
{
    internal static class AssetDownloadCloudreveService
    {
        private const int DefaultPageSize = 200;

        internal static async Task<AssetDownloadConnectionContext> ConnectAsync(string shareUrl, CancellationToken cancellationToken)
        {
            if (!CloudreveShareUriParser.TryParse(shareUrl, out CloudreveShareDescriptor descriptor, out string parseError))
            {
                throw new InvalidOperationException(parseError);
            }

            var client = new CloudreveResourceClient(descriptor.ApiBaseUrl);
            CloudreveShareInfo shareInfo = await client.GetShareInfoAsync(descriptor, cancellationToken).ConfigureAwait(false);
            if (!CloudreveShareConnectionUtility.TryValidateFolderShare(shareInfo, descriptor, out string shareError))
            {
                throw new InvalidOperationException(shareError);
            }

            string resolvedRootUri = await CloudreveShareConnectionUtility
                .ResolveRootUriAsync(client, descriptor, cachedRootUri: null, cancellationToken)
                .ConfigureAwait(false);
            return new AssetDownloadConnectionContext(descriptor.ShareUrl, resolvedRootUri, descriptor, client);
        }

        internal static async Task<List<AssetDownloadDirectoryEntry>> ListFolderEntriesAsync(
            AssetDownloadConnectionContext context,
            string folderUri,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new InvalidOperationException("No Cloudreve connection is available.");
            }

            if (string.IsNullOrWhiteSpace(folderUri))
            {
                throw new InvalidOperationException("A valid Cloudreve folder URI is required.");
            }

            var results = new List<AssetDownloadDirectoryEntry>();
            int page = 0;
            int pageSize = DefaultPageSize;
            string nextToken = null;

            while (true)
            {
                CloudreveListData data = await context.Client
                    .ListFolderPageAsync(folderUri, page, pageSize, nextToken, cancellationToken)
                    .ConfigureAwait(false);

                int effectivePageSize = data?.props?.max_page_size > 0
                    ? Math.Min(data.props.max_page_size, DefaultPageSize)
                    : pageSize;
                pageSize = effectivePageSize;

                foreach (CloudreveFileItem item in data?.files ?? Enumerable.Empty<CloudreveFileItem>())
                {
                    results.Add(new AssetDownloadDirectoryEntry
                    {
                        Name = item?.name ?? string.Empty,
                        Uri = item?.path ?? string.Empty,
                        IsDirectory = item?.type == 1,
                        UpdatedAt = item?.updated_at ?? string.Empty,
                        Size = item?.size ?? 0L,
                    });
                }

                CloudrevePagination pagination = data?.pagination;
                if (pagination == null)
                {
                    break;
                }

                if (pagination.is_cursor)
                {
                    if (string.IsNullOrWhiteSpace(pagination.next_token))
                    {
                        break;
                    }

                    nextToken = pagination.next_token;
                    page = 0;
                    continue;
                }

                int fetchedCount = data?.files?.Count ?? 0;
                if (fetchedCount <= 0)
                {
                    break;
                }

                page++;
                if (pagination.total_items > 0 && page * pagination.page_size >= pagination.total_items)
                {
                    break;
                }
            }

            results.Sort((left, right) =>
            {
                if (left == null || right == null)
                {
                    return left == right ? 0 : left == null ? -1 : 1;
                }

                int directoryOrder = right.IsDirectory.CompareTo(left.IsDirectory);
                if (directoryOrder != 0)
                {
                    return directoryOrder;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        internal static async Task<List<RemoteResourceEntry>> ListAllFilesAsync(
            AssetDownloadConnectionContext context,
            string folderUri,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new InvalidOperationException("No Cloudreve connection is available.");
            }

            return await context.Client.ListAllFilesAsync(folderUri, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task DownloadFileAsync(
            AssetDownloadConnectionContext context,
            RemoteResourceEntry remoteFile,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new InvalidOperationException("No Cloudreve connection is available.");
            }

            if (remoteFile == null)
            {
                throw new InvalidOperationException("A valid Cloudreve file is required.");
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new InvalidOperationException("A valid local destination path is required.");
            }

            string destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            string temporaryPath = destinationPath + ".downloading";
            try
            {
                string downloadUrl = await context.Client.CreateDownloadUrlAsync(remoteFile, cancellationToken).ConfigureAwait(false);
                byte[] payload = await context.Client.DownloadBytesAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
                File.WriteAllBytes(temporaryPath, payload);
                File.Copy(temporaryPath, destinationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

    }
}
#endif
