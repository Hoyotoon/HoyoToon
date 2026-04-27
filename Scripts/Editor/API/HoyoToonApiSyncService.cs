using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API.Games;
using HoyoToon.Editor.API.Resources;
using HoyoToon.Editor.Utilities.API;
using HoyoToon.Editor.Utilities.Debugging;
using UnityEditor;

namespace HoyoToon.Editor.API
{
    [InitializeOnLoad]
    internal static class HoyoToonApiSyncService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        private static readonly SyncEndpoint<GameRecordDto> GamesEndpoint = new SyncEndpoint<GameRecordDto>(
            displayName: "game data",
            payloadName: "games",
            sourceUrl: HoyoToonApi.GamesV2HttpUrl,
            lastCheckTicksKey: "HoyoToon.GamesApi.LastCheckTicks",
            lastPayloadHashKey: "HoyoToon.GamesApi.LastPayloadHash",
            lastSchemaVersionKey: "HoyoToon.GamesApi.LastSchemaVersion",
            schemaVersion: "3",
            needsWrite: GamesScriptableObjectSync.NeedsWrite,
            writeAssets: GamesScriptableObjectSync.WriteAssets);

        private static readonly SyncEndpoint<ResourceRecordDto> ResourcesEndpoint = new SyncEndpoint<ResourceRecordDto>(
            displayName: "resource data",
            payloadName: "resources",
            sourceUrl: HoyoToonApi.ResourcesHttpUrl,
            lastCheckTicksKey: "HoyoToon.ResourcesApi.LastCheckTicks",
            lastPayloadHashKey: "HoyoToon.ResourcesApi.LastPayloadHash",
            lastSchemaVersionKey: "HoyoToon.ResourcesApi.LastSchemaVersion",
            schemaVersion: "3",
            needsWrite: ResourcesScriptableObjectSync.NeedsWrite,
            writeAssets: ResourcesScriptableObjectSync.WriteAssets);

        private static double nextHeartbeatTime;

        static HoyoToonApiSyncService()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += TriggerInitialRefresh;
        }

        [MenuItem("HoyoToon/API/Check for updates")]
        private static void CheckForApiUpdatesMenuItem()
        {
            LogManualRefreshResult(GamesEndpoint, TryStartRefresh(GamesEndpoint, true));
            LogManualRefreshResult(ResourcesEndpoint, TryStartRefresh(ResourcesEndpoint, true));
        }

        private static void TriggerInitialRefresh()
        {
            TryStartRefresh(GamesEndpoint, false);
            TryStartRefresh(ResourcesEndpoint, false);
        }

        private static void OnEditorUpdate()
        {
            if (!HoyoToonApiSyncUtility.ShouldRunHeartbeat(ref nextHeartbeatTime, 10d))
            {
                return;
            }

            TryStartRefresh(GamesEndpoint, false);
            TryStartRefresh(ResourcesEndpoint, false);
        }

        private static RefreshStartResult TryStartRefresh<TRecord>(SyncEndpoint<TRecord> endpoint, bool force)
        {
            if (endpoint.IsRefreshing)
            {
                return RefreshStartResult.AlreadyRefreshing;
            }

            if (!HoyoToonApiSyncUtility.IsEditorReadyForRefresh())
            {
                return RefreshStartResult.EditorNotReady;
            }

            if (!force && !HoyoToonApiSyncUtility.IsRefreshDue(endpoint.LastCheckTicksKey, PollInterval))
            {
                return RefreshStartResult.NotDue;
            }

            _ = RefreshAsync(endpoint, force);
            return RefreshStartResult.Started;
        }

        private static async Task RefreshAsync<TRecord>(SyncEndpoint<TRecord> endpoint, bool force)
        {
            endpoint.IsRefreshing = true;

            try
            {
                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Api,
                    $"Starting {(force ? "manual" : "background")} refresh for {endpoint.DisplayName} from {endpoint.SourceUrl}.",
                    isBackgroundOperation: !force);

                HoyoToonApiPayloadResult<TRecord> payload = await HoyoToonApiFetchUtility
                    .FetchArrayPayloadAsync<TRecord>(endpoint.SourceUrl, endpoint.PayloadName, CancellationToken.None);
                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Api,
                    $"Fetched {payload.Records.Count} record(s) for {endpoint.DisplayName}.",
                    isBackgroundOperation: !force);

                string payloadHash = HoyoToonApiSyncUtility.ComputeSha256(payload.RawPayload);

                if (!force
                    && string.Equals(HoyoToonApiSyncUtility.GetSavedPayloadHash(endpoint.LastPayloadHashKey), payloadHash, StringComparison.Ordinal)
                    && string.Equals(HoyoToonApiSyncUtility.GetSavedSchemaVersion(endpoint.LastSchemaVersionKey), endpoint.SchemaVersion, StringComparison.Ordinal)
                    && !endpoint.NeedsWrite(payload.Records))
                {
                    HoyoToonApiSyncUtility.SaveLastCheckMetadata(
                        endpoint.LastCheckTicksKey,
                        endpoint.LastPayloadHashKey,
                        endpoint.LastSchemaVersionKey,
                        endpoint.SchemaVersion,
                        payloadHash);

                    string noChangesMessage = $"{FormatDisplayName(endpoint.DisplayName)} is already up to date.";
                    if (force)
                    {
                        HoyoToonLogger.Info(HoyoToonLogCategory.Api, noChangesMessage);
                    }
                    else
                    {
                        HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, noChangesMessage, isBackgroundOperation: true);
                    }

                    return;
                }

                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Api,
                    $"Writing generated assets for {endpoint.DisplayName}.",
                    isBackgroundOperation: !force);

                endpoint.WriteAssets(payload.Records);
                HoyoToonApiSyncUtility.SaveLastCheckMetadata(
                    endpoint.LastCheckTicksKey,
                    endpoint.LastPayloadHashKey,
                    endpoint.LastSchemaVersionKey,
                    endpoint.SchemaVersion,
                    payloadHash);

                string syncedMessage = $"Refreshed {endpoint.DisplayName} from {payload.Source}.";
                if (force)
                {
                    HoyoToonLogger.Info(HoyoToonLogCategory.Api, syncedMessage);
                }
                else
                {
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, syncedMessage, isBackgroundOperation: true);
                }
            }
            catch (Exception exception)
            {
                HoyoToonApiSyncUtility.SaveLastCheckMetadata(
                    endpoint.LastCheckTicksKey,
                    endpoint.LastPayloadHashKey,
                    endpoint.LastSchemaVersionKey,
                    endpoint.SchemaVersion,
                    HoyoToonApiSyncUtility.GetSavedPayloadHash(endpoint.LastPayloadHashKey));
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    $"Failed to refresh {endpoint.DisplayName} from {endpoint.SourceUrl}.",
                    exception,
                    isBackgroundOperation: !force);
            }
            finally
            {
                endpoint.IsRefreshing = false;
            }
        }

        private static void LogManualRefreshResult<TRecord>(SyncEndpoint<TRecord> endpoint, RefreshStartResult result)
        {
            switch (result)
            {
                case RefreshStartResult.Started:
                    HoyoToonLogger.Info(HoyoToonLogCategory.Api, $"Refreshing {endpoint.DisplayName}.");
                    break;

                case RefreshStartResult.AlreadyRefreshing:
                    HoyoToonLogger.Info(HoyoToonLogCategory.Api, $"{FormatDisplayName(endpoint.DisplayName)} refresh is already in progress.");
                    break;

                case RefreshStartResult.EditorNotReady:
                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Api,
                        $"Cannot refresh {endpoint.DisplayName} while the editor is compiling, updating assets, or entering Play Mode.");
                    break;
            }
        }

        private static string FormatDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Data";
            }

            return char.ToUpperInvariant(displayName[0]) + displayName.Substring(1);
        }

        private enum RefreshStartResult
        {
            Started,
            AlreadyRefreshing,
            EditorNotReady,
            NotDue,
        }

        private sealed class SyncEndpoint<TRecord>
        {
            internal SyncEndpoint(
                string displayName,
                string payloadName,
                string sourceUrl,
                string lastCheckTicksKey,
                string lastPayloadHashKey,
                string lastSchemaVersionKey,
                string schemaVersion,
                Func<IReadOnlyList<TRecord>, bool> needsWrite,
                Action<IReadOnlyList<TRecord>> writeAssets)
            {
                DisplayName = displayName;
                PayloadName = payloadName;
                SourceUrl = sourceUrl;
                LastCheckTicksKey = lastCheckTicksKey;
                LastPayloadHashKey = lastPayloadHashKey;
                LastSchemaVersionKey = lastSchemaVersionKey;
                SchemaVersion = schemaVersion;
                NeedsWrite = needsWrite;
                WriteAssets = writeAssets;
            }

            internal string DisplayName { get; }

            internal string PayloadName { get; }

            internal string SourceUrl { get; }

            internal string LastCheckTicksKey { get; }

            internal string LastPayloadHashKey { get; }

            internal string LastSchemaVersionKey { get; }

            internal string SchemaVersion { get; }

            internal Func<IReadOnlyList<TRecord>, bool> NeedsWrite { get; }

            internal Action<IReadOnlyList<TRecord>> WriteAssets { get; }

            internal bool IsRefreshing { get; set; }
        }
    }
}
