using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API.Games;
using HoyoToon.Editor.API.Resources;
using HoyoToon.Editor.API.Users;
using HoyoToon.Editor.Utilities.API;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Users;
using UnityEditor;
using Utf8Json;

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
            fetchPayload: FetchArrayPayloadAsync<GameRecordDto>,
            needsWrite: GamesScriptableObjectSync.NeedsWrite,
            writeAssets: GamesScriptableObjectSync.WriteAssets);

        private static readonly SyncEndpoint<EntityCatalogRecordDto> EntityCatalogEndpoint = new SyncEndpoint<EntityCatalogRecordDto>(
            displayName: "entity catalog data",
            payloadName: "entity catalog",
            sourceUrl: HoyoToonApi.EntityCatalogHttpUrl,
            lastCheckTicksKey: "HoyoToon.EntityCatalogApi.LastCheckTicks",
            lastPayloadHashKey: "HoyoToon.EntityCatalogApi.LastPayloadHash",
            lastSchemaVersionKey: "HoyoToon.EntityCatalogApi.LastSchemaVersion",
            schemaVersion: "1",
            fetchPayload: FetchEntityCatalogPayloadAsync,
            needsWrite: GameEntityCatalogScriptableObjectSync.NeedsWrite,
            writeAssets: GameEntityCatalogScriptableObjectSync.WriteAssets);

        private static readonly SyncEndpoint<ResourceRecordDto> ResourcesEndpoint = new SyncEndpoint<ResourceRecordDto>(
            displayName: "resource data",
            payloadName: "resources",
            sourceUrl: HoyoToonApi.ResourcesHttpUrl,
            lastCheckTicksKey: "HoyoToon.ResourcesApi.LastCheckTicks",
            lastPayloadHashKey: "HoyoToon.ResourcesApi.LastPayloadHash",
            lastSchemaVersionKey: "HoyoToon.ResourcesApi.LastSchemaVersion",
            schemaVersion: "3",
            fetchPayload: FetchArrayPayloadAsync<ResourceRecordDto>,
            needsWrite: ResourcesScriptableObjectSync.NeedsWrite,
            writeAssets: ResourcesScriptableObjectSync.WriteAssets);

        private static readonly SyncEndpoint<UserRecordDto> UserProfileEndpoint = new SyncEndpoint<UserRecordDto>(
            displayName: "user profile",
            payloadName: "user profile",
            sourceUrl: HoyoToonApi.UsersHttpUrl,
            lastCheckTicksKey: "HoyoToon.UsersApi.LastCheckTicks",
            lastPayloadHashKey: "HoyoToon.UsersApi.LastPayloadHash",
            lastSchemaVersionKey: "HoyoToon.UsersApi.LastSchemaVersion",
            schemaVersion: "1",
            fetchPayload: FetchUserProfilePayloadAsync,
            needsWrite: UserProfileNeedsWrite,
            writeAssets: WriteUserProfile,
            hasRefreshTarget: HasUserProfileRefreshTarget);

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
            LogManualRefreshResult(EntityCatalogEndpoint, TryStartRefresh(EntityCatalogEndpoint, true));
            LogManualRefreshResult(ResourcesEndpoint, TryStartRefresh(ResourcesEndpoint, true));
            LogManualRefreshResult(UserProfileEndpoint, TryStartRefresh(UserProfileEndpoint, true));
        }

        internal static bool IsUserProfileRefreshing => UserProfileEndpoint.IsRefreshing;

        internal static DateTime? LastUserProfileRefreshUtc => GetLastCheckUtc(UserProfileEndpoint.LastCheckTicksKey);

        internal static HoyoToonApiSyncRefreshStartResult RefreshUserProfileNow()
        {
            return TryStartRefresh(UserProfileEndpoint, true);
        }

        private static void TriggerInitialRefresh()
        {
            TryStartRefresh(GamesEndpoint, false);
            TryStartRefresh(EntityCatalogEndpoint, false);
            TryStartRefresh(ResourcesEndpoint, false);
            TryStartRefresh(UserProfileEndpoint, false);
        }

        private static void OnEditorUpdate()
        {
            if (!HoyoToonApiSyncUtility.ShouldRunHeartbeat(ref nextHeartbeatTime, 10d))
            {
                return;
            }

            TryStartRefresh(GamesEndpoint, false);
            TryStartRefresh(EntityCatalogEndpoint, false);
            TryStartRefresh(ResourcesEndpoint, false);
            TryStartRefresh(UserProfileEndpoint, false);
        }

        private static HoyoToonApiSyncRefreshStartResult TryStartRefresh<TRecord>(SyncEndpoint<TRecord> endpoint, bool force)
        {
            if (endpoint.IsRefreshing)
            {
                return HoyoToonApiSyncRefreshStartResult.AlreadyRefreshing;
            }

            if (!HoyoToonApiSyncUtility.IsEditorReadyForRefresh())
            {
                return HoyoToonApiSyncRefreshStartResult.EditorNotReady;
            }

            if (!endpoint.HasRefreshTarget())
            {
                return HoyoToonApiSyncRefreshStartResult.NoRefreshTarget;
            }

            if (!force && !HoyoToonApiSyncUtility.IsRefreshDue(endpoint.LastCheckTicksKey, PollInterval))
            {
                return HoyoToonApiSyncRefreshStartResult.NotDue;
            }

            _ = RefreshAsync(endpoint, force);
            return HoyoToonApiSyncRefreshStartResult.Started;
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

                HoyoToonApiPayloadResult<TRecord> payload = await endpoint.FetchPayload(endpoint, CancellationToken.None);
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

        private static Task<HoyoToonApiPayloadResult<TRecord>> FetchArrayPayloadAsync<TRecord>(
            SyncEndpoint<TRecord> endpoint,
            CancellationToken cancellationToken)
        {
            return HoyoToonApiFetchUtility.FetchArrayPayloadAsync<TRecord>(
                endpoint.SourceUrl,
                endpoint.PayloadName,
                cancellationToken);
        }

        private static async Task<HoyoToonApiPayloadResult<EntityCatalogRecordDto>> FetchEntityCatalogPayloadAsync(
            SyncEndpoint<EntityCatalogRecordDto> endpoint,
            CancellationToken cancellationToken)
        {
            try
            {
                return await HoyoToonApiFetchUtility.FetchArrayPayloadAsync<EntityCatalogRecordDto>(
                    endpoint.SourceUrl,
                    endpoint.PayloadName,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (ShouldUseEntityCatalogRouteFallback(exception))
            {
                List<EntityCatalogRecordDto> records = await FetchEntityCatalogFromPublicEntityRoutesAsync(cancellationToken)
                    .ConfigureAwait(false);
                byte[] payload = JsonSerializer.Serialize(records, HoyoToonApi.JsonResolver);
                return new HoyoToonApiPayloadResult<EntityCatalogRecordDto>(
                    Encoding.UTF8.GetString(payload),
                    records,
                    "entity route fallback");
            }
        }

        private static async Task<HoyoToonApiPayloadResult<UserRecordDto>> FetchUserProfilePayloadAsync(
            SyncEndpoint<UserRecordDto> endpoint,
            CancellationToken cancellationToken)
        {
            string uid = ResolveUserProfileRefreshUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                return new HoyoToonApiPayloadResult<UserRecordDto>(
                    string.Empty,
                    new List<UserRecordDto>(),
                    endpoint.SourceUrl);
            }

            string sourceUrl = endpoint.SourceUrl + "?UID=" + Uri.EscapeDataString(uid);
            UserRecordDto apiUser = await HoyoToonUserApiClient.GetUserAsync(uid, cancellationToken);
            var records = apiUser == null
                ? new List<UserRecordDto>()
                : new List<UserRecordDto> { apiUser };
            string rawPayload = apiUser == null
                ? string.Empty
                : Encoding.UTF8.GetString(JsonSerializer.Serialize(apiUser, HoyoToonApi.JsonResolver));
            return new HoyoToonApiPayloadResult<UserRecordDto>(rawPayload, records, sourceUrl);
        }

        private static async Task<List<EntityCatalogRecordDto>> FetchEntityCatalogFromPublicEntityRoutesAsync(
            CancellationToken cancellationToken)
        {
            HoyoToonApiPayloadResult<GameRecordDto> gamesPayload = await HoyoToonApiFetchUtility
                .FetchArrayPayloadAsync<GameRecordDto>(HoyoToonApi.GamesV2HttpUrl, "games", cancellationToken)
                .ConfigureAwait(false);

            var records = new List<EntityCatalogRecordDto>();
            foreach (GameRecordDto game in gamesPayload.Records)
            {
                string gameKey = game?.config?.key;
                if (string.IsNullOrWhiteSpace(gameKey))
                {
                    continue;
                }

                await FetchEntityPageRecordsAsync(records, gameKey, HoyoToonApi.CharactersHttpUrl, "Character", cancellationToken)
                    .ConfigureAwait(false);
                await FetchEntityPageRecordsAsync(records, gameKey, HoyoToonApi.MonstersHttpUrl, "Monster", cancellationToken)
                    .ConfigureAwait(false);
                await FetchEntityPageRecordsAsync(records, gameKey, HoyoToonApi.WeaponsHttpUrl, "Weapon", cancellationToken)
                    .ConfigureAwait(false);
            }

            return records;
        }

        private static async Task FetchEntityPageRecordsAsync(
            List<EntityCatalogRecordDto> records,
            string gameKey,
            string baseUrl,
            string entityKind,
            CancellationToken cancellationToken)
        {
            string cursor = null;
            do
            {
                string payload;
                try
                {
                    payload = await HoyoToonApiFetchUtility
                        .GetStringAsync(BuildEntityPageUrl(baseUrl, gameKey, cursor), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException exception) when (IsMissingProjectionPage(exception))
                {
                    return;
                }

                EntityCatalogPageDto page = JsonSerializer.Deserialize<EntityCatalogPageDto>(payload, HoyoToonApi.JsonResolver);

                foreach (EntityCatalogRecordDto record in page?.items ?? new List<EntityCatalogRecordDto>())
                {
                    record.gameKey = string.IsNullOrWhiteSpace(record.gameKey) ? gameKey : record.gameKey;
                    record.entityKind = entityKind;
                    record.entityId = ResolveEntityPageRecordId(record);
                    records.Add(record);
                }

                cursor = page != null && !page.isDone ? page.continueCursor : null;
            }
            while (!string.IsNullOrWhiteSpace(cursor));
        }

        private static string BuildEntityPageUrl(string baseUrl, string gameKey, string cursor)
        {
            var builder = new StringBuilder(baseUrl);
            builder.Append("?gameKey=").Append(Uri.EscapeDataString(gameKey));
            builder.Append("&limit=250");
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                builder.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
            }

            return builder.ToString();
        }

        private static string ResolveEntityPageRecordId(EntityCatalogRecordDto record)
        {
            if (!string.IsNullOrWhiteSpace(record.entityId))
            {
                return record.entityId;
            }

            if (!string.IsNullOrWhiteSpace(record.characterId))
            {
                return record.characterId;
            }

            if (!string.IsNullOrWhiteSpace(record.monsterId))
            {
                return record.monsterId;
            }

            return record.weaponId;
        }

        private static bool ShouldUseEntityCatalogRouteFallback(Exception exception)
        {
            string message = exception.ToString();
            return message.Contains("404", StringComparison.Ordinal)
                || message.Contains(nameof(HttpRequestException), StringComparison.Ordinal)
                || message.Contains("JSON array", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingProjectionPage(Exception exception)
        {
            return exception.ToString().Contains("400", StringComparison.Ordinal);
        }

        private static bool HasUserProfileRefreshTarget()
        {
            return HoyoToonUserProfileStorage.HasCompleteLocalProfile()
                || HoyoToonUserProfileGlobalStore.TryLoad(out _);
        }

        private static string ResolveUserProfileRefreshUid()
        {
            return HoyoToonUserProfileService.TryResolveAuthoritativeRefreshUid(
                HoyoToonUserProfileStorage.GetLocalProfile(),
                out string uid,
                out _)
                ? uid
                : string.Empty;
        }

        private static bool UserProfileNeedsWrite(IReadOnlyList<UserRecordDto> users)
        {
            if (users != null && users.Count > 0)
            {
                return !HoyoToonUserProfileService.LocalProfileMatchesApiUser(users[0]);
            }

            return HoyoToonUserProfileGlobalStore.TryLoad(out _)
                && !HoyoToonUserProfileService.LocalProfileMatchesCachedUser();
        }

        private static void WriteUserProfile(IReadOnlyList<UserRecordDto> users)
        {
            if (users == null || users.Count <= 0)
            {
                HoyoToonUserProfileService.SaveCachedUserProfile();
                return;
            }

            HoyoToonUserProfileService.SaveApiUserProfile(users[0]);
        }

        private static void LogManualRefreshResult<TRecord>(SyncEndpoint<TRecord> endpoint, HoyoToonApiSyncRefreshStartResult result)
        {
            switch (result)
            {
                case HoyoToonApiSyncRefreshStartResult.Started:
                    HoyoToonLogger.Info(HoyoToonLogCategory.Api, $"Refreshing {endpoint.DisplayName}.");
                    break;

                case HoyoToonApiSyncRefreshStartResult.AlreadyRefreshing:
                    HoyoToonLogger.Info(HoyoToonLogCategory.Api, $"{FormatDisplayName(endpoint.DisplayName)} refresh is already in progress.");
                    break;

                case HoyoToonApiSyncRefreshStartResult.EditorNotReady:
                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Api,
                        $"Cannot refresh {endpoint.DisplayName} while the editor is compiling, updating assets, or entering Play Mode.");
                    break;

                case HoyoToonApiSyncRefreshStartResult.NoRefreshTarget:
                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Api,
                        $"No {endpoint.DisplayName} target is available to refresh.");
                    break;
            }
        }

        private static DateTime? GetLastCheckUtc(string lastCheckTicksKey)
        {
            string rawTicks = EditorPrefs.GetString(HoyoToonApiSyncUtility.PrefsKey(lastCheckTicksKey), string.Empty);
            if (!long.TryParse(rawTicks, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                || ticks <= 0)
            {
                return null;
            }

            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static string FormatDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Data";
            }

            return char.ToUpperInvariant(displayName[0]) + displayName.Substring(1);
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
                Func<SyncEndpoint<TRecord>, CancellationToken, Task<HoyoToonApiPayloadResult<TRecord>>> fetchPayload,
                Func<IReadOnlyList<TRecord>, bool> needsWrite,
                Action<IReadOnlyList<TRecord>> writeAssets,
                Func<bool> hasRefreshTarget = null)
            {
                DisplayName = displayName;
                PayloadName = payloadName;
                SourceUrl = sourceUrl;
                LastCheckTicksKey = lastCheckTicksKey;
                LastPayloadHashKey = lastPayloadHashKey;
                LastSchemaVersionKey = lastSchemaVersionKey;
                SchemaVersion = schemaVersion;
                FetchPayload = fetchPayload;
                NeedsWrite = needsWrite;
                WriteAssets = writeAssets;
                hasRefreshTargetCallback = hasRefreshTarget;
            }

            private readonly Func<bool> hasRefreshTargetCallback;

            internal string DisplayName { get; }

            internal string PayloadName { get; }

            internal string SourceUrl { get; }

            internal string LastCheckTicksKey { get; }

            internal string LastPayloadHashKey { get; }

            internal string LastSchemaVersionKey { get; }

            internal string SchemaVersion { get; }

            internal Func<SyncEndpoint<TRecord>, CancellationToken, Task<HoyoToonApiPayloadResult<TRecord>>> FetchPayload { get; }

            internal Func<IReadOnlyList<TRecord>, bool> NeedsWrite { get; }

            internal Action<IReadOnlyList<TRecord>> WriteAssets { get; }

            internal bool IsRefreshing { get; set; }

            internal bool HasRefreshTarget()
            {
                return hasRefreshTargetCallback == null || hasRefreshTargetCallback.Invoke();
            }
        }
    }

    internal enum HoyoToonApiSyncRefreshStartResult
    {
        Started,
        AlreadyRefreshing,
        EditorNotReady,
        NoRefreshTarget,
        NotDue,
    }
}
