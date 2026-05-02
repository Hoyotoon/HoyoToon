#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API;
using HoyoToon.Editor.API.Users;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.UI.Dialogs;
using HoyoToon.Runtime.ScriptableObjects.Users;
using UnityEditor;

namespace HoyoToon.Dev.Editor.Debug.Users
{
    internal static class UserProfileDebugMenus
    {
        private const string MenuRoot = "HoyoToon/Debug/Users/";

        [MenuItem(MenuRoot + "Create Profile Through API Prompt", false, 10)]
        private static void CreateProfileThroughApiPromptMenuItem()
        {
            HoyoToonUserProfilePrompt.Show(username => _ = CreateProfileThroughApiAsync(username));
        }

        [MenuItem(MenuRoot + "Create Local Mock Profile", false, 11)]
        private static void CreateLocalMockProfileMenuItem()
        {
            HoyoToonUserProfileSO existingProfile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (existingProfile != null
                && !EditorUtility.DisplayDialog(
                    "Create Local Mock Profile",
                    "Overwrite the current local HoyoToon user profile asset with a debug-only mock profile?",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            string timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.SaveLocalProfile(
                "299" + timestamp,
                "Debug User",
                HoyoToonApi.DefaultUserAvatar,
                HoyoToonApi.DefaultUserRoleName,
                HoyoToonApi.DefaultUserRoleColor,
                updateGlobalCache: false);
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            HoyoToonDialog.DisplayDialog(
                "Local Mock Profile",
                "Created local debug profile at:\n" + HoyoToonUserProfileStorage.LocalProfileAssetPath,
                "OK");
        }

        [MenuItem(MenuRoot + "Prompt Missing Profile After Completed Onboarding", false, 12)]
        private static void PromptMissingProfileAfterCompletedOnboardingMenuItem()
        {
            if (!OnboardingPersistence.IsCompleted)
            {
                HoyoToonDialog.DisplayDialog(
                    "Prompt Missing Profile",
                    "Onboarding is not marked complete for this project, so the normal onboarding flow owns the profile prompt.",
                    "OK");
                return;
            }

            if (HoyoToonUserProfileService.HasCompleteLocalProfile)
            {
                HoyoToonDialog.DisplayDialog(
                    "Prompt Missing Profile",
                    "A complete local HoyoToon profile already exists, so the migration prompt is not needed.",
                    "OK");
                return;
            }

            OnboardingValidation.PromptForMissingLocalUserProfileAfterCompletedOnboarding(force: true);
        }

        [MenuItem(MenuRoot + "Run Background Profile Refresh Now", false, 13)]
        private static void RunBackgroundProfileRefreshNowMenuItem()
        {
            HoyoToonApiSyncRefreshStartResult result = HoyoToonApiSyncService.RefreshUserProfileNow();
            HoyoToonDialog.DisplayDialog(
                "Background Profile Refresh",
                DescribeRefreshStartResult(result),
                "OK");
        }

        [MenuItem(MenuRoot + "Show Local Profile", false, 20)]
        private static void ShowLocalProfileMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (profile == null)
            {
                HoyoToonDialog.DisplayDialog("Local Profile", "No local HoyoToon user profile asset exists.", "OK");
                return;
            }

            HoyoToonDialog.DisplayDialog(
                "Local Profile",
                "Asset: " + HoyoToonUserProfileStorage.LocalProfileAssetPath + "\n" +
                "Username: " + EmptyAsNone(profile.Username) + "\n" +
                "Role: " + EmptyAsNone(profile.RoleName) + "\n" +
                "Role Color: " + EmptyAsNone(profile.RoleColor) + "\n" +
                "UID: " + EmptyAsNone(profile.UID) + "\n" +
                "Avatar: " + EmptyAsNone(profile.Avatar),
                "OK");
        }

        [MenuItem(MenuRoot + "Show Local Profile", true)]
        private static bool ShowLocalProfileMenuItemValidate()
        {
            return HoyoToonUserProfileStorage.GetLocalProfile() != null;
        }

        [MenuItem(MenuRoot + "Show Returning User Cache", false, 22)]
        private static void ShowReturningUserCacheMenuItem()
        {
            if (!HoyoToonUserProfileGlobalStore.TryLoad(out UserProfileGlobalRecord record))
            {
                HoyoToonDialog.DisplayDialog(
                    "Returning User Cache",
                    "No global HoyoToon user cache exists.\n\nPath:\n" + HoyoToonUserProfileGlobalStore.StoreFilePath,
                    "OK");
                return;
            }

            HoyoToonDialog.DisplayDialog(
                "Returning User Cache",
                "Path: " + HoyoToonUserProfileGlobalStore.StoreFilePath + "\n" +
                "Username: " + EmptyAsNone(record.username) + "\n" +
                "Role: " + EmptyAsNone(record.roleName) + "\n" +
                "Role Color: " + EmptyAsNone(record.roleColor) + "\n" +
                "UID: " + EmptyAsNone(record.UID) + "\n" +
                "Avatar: " + EmptyAsNone(record.avatar),
                "OK");
        }

        [MenuItem(MenuRoot + "Show Profile Debug State", false, 23)]
        private static void ShowProfileDebugStateMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            bool hasCache = HoyoToonUserProfileGlobalStore.TryLoad(out UserProfileGlobalRecord record);
            HoyoToonDialog.DisplayDialog(
                "Profile Debug State",
                "Local profile complete: " + HoyoToonUserProfileStorage.IsComplete(profile) + "\n" +
                "Returning-user cache: " + hasCache + "\n" +
                "Cached UID: " + EmptyAsNone(hasCache ? record.UID : string.Empty) + "\n" +
                "Onboarding completed: " + OnboardingPersistence.IsCompleted + "\n" +
                "Onboarding completed version: " + EmptyAsNone(OnboardingPersistence.CompletedVersion) + "\n" +
                "Creating profile: " + HoyoToonUserProfileService.IsCreating + "\n" +
                "Updating avatar: " + HoyoToonUserProfileService.IsUpdatingAvatar + "\n" +
                "API sync refresh running: " + HoyoToonApiSyncService.IsUserProfileRefreshing + "\n" +
                "Last API sync refresh UTC: " + FormatUtc(HoyoToonApiSyncService.LastUserProfileRefreshUtc) + "\n" +
                "Default avatar: " + HoyoToonApi.DefaultUserAvatar + "\n" +
                "Default role: " + HoyoToonApi.DefaultUserRoleName + "\n" +
                "Default role color: " + HoyoToonApi.DefaultUserRoleColor + "\n" +
                "Local asset path: " + HoyoToonUserProfileStorage.LocalProfileAssetPath + "\n" +
                "Global cache path: " + HoyoToonUserProfileGlobalStore.StoreFilePath,
                "OK");
        }

        [MenuItem(MenuRoot + "Select Local Profile Asset", false, 21)]
        private static void SelectLocalProfileAssetMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (profile == null)
            {
                return;
            }

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        [MenuItem(MenuRoot + "Select Local Profile Asset", true)]
        private static bool SelectLocalProfileAssetMenuItemValidate()
        {
            return HoyoToonUserProfileStorage.GetLocalProfile() != null;
        }

        [MenuItem(MenuRoot + "Restore Local Profile From Cache/API", false, 30)]
        private static void RestoreLocalProfileFromCacheMenuItem()
        {
            _ = RestoreLocalProfileFromCacheAsync();
        }

        [MenuItem(MenuRoot + "Restore Local Profile From Cache/API", true)]
        private static bool RestoreLocalProfileFromCacheMenuItemValidate()
        {
            return HoyoToonUserProfileGlobalStore.TryLoad(out _) && !HoyoToonUserProfileService.IsCreating;
        }

        [MenuItem(MenuRoot + "Check Local UID In API", false, 31)]
        private static void CheckLocalUidInApiMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (profile == null || !HoyoToonUserProfileStorage.IsNumericUid(profile.UID))
            {
                HoyoToonDialog.DisplayDialog("Check Local UID", "No numeric local profile UID is available.", "OK");
                return;
            }

            _ = CheckLocalUidInApiAsync(profile.UID);
        }

        [MenuItem(MenuRoot + "Refresh Local Profile From API", false, 32)]
        private static void RefreshLocalProfileFromApiMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (!HoyoToonUserProfileStorage.IsComplete(profile))
            {
                HoyoToonDialog.DisplayDialog(
                    "Refresh Local Profile",
                    "Create or restore a complete local HoyoToon profile first.",
                    "OK");
                return;
            }

            _ = RefreshLocalProfileFromApiAsync(profile);
        }

        [MenuItem(MenuRoot + "Refresh Local Profile From API", true)]
        private static bool RefreshLocalProfileFromApiMenuItemValidate()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            return HoyoToonUserProfileStorage.IsComplete(profile)
                && !HoyoToonUserProfileService.IsCreating
                && !HoyoToonUserProfileService.IsUpdatingAvatar;
        }

        [MenuItem(MenuRoot + "Check Local UID In API", true)]
        private static bool CheckLocalUidInApiMenuItemValidate()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            return profile != null
                && HoyoToonUserProfileStorage.IsNumericUid(profile.UID)
                && !HoyoToonUserProfileService.IsCreating;
        }

        [MenuItem(MenuRoot + "Update Local Avatar URL Through API", false, 33)]
        private static void UpdateLocalAvatarUrlThroughApiMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (!HoyoToonUserProfileStorage.IsComplete(profile))
            {
                HoyoToonDialog.DisplayDialog(
                    "Update Avatar URL",
                    "Create or restore a complete local HoyoToon profile first.",
                    "OK");
                return;
            }

            HoyoToonUserProfilePrompt.ShowAvatarEditor(
                profile,
                avatarUrl => _ = UpdateLocalAvatarUrlThroughApiAsync(profile, avatarUrl));
        }

        [MenuItem(MenuRoot + "Update Local Avatar URL Through API", true)]
        private static bool UpdateLocalAvatarUrlThroughApiMenuItemValidate()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            return HoyoToonUserProfileStorage.IsComplete(profile)
                && !HoyoToonUserProfileService.IsCreating
                && !HoyoToonUserProfileService.IsUpdatingAvatar;
        }

        [MenuItem(MenuRoot + "Clear Local Profile Asset", false, 40)]
        private static void ClearLocalProfileAssetMenuItem()
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (profile == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Clear Local Profile Asset",
                "Delete the local HoyoToon user profile asset?\n\nThis does not delete the API user record.",
                "Delete Local Asset",
                "Cancel"))
            {
                return;
            }

            bool deleted = HoyoToonUserProfileStorage.DeleteLocalProfile();
            HoyoToonDialog.DisplayDialog(
                "Clear Local Profile Asset",
                deleted ? "Deleted the local profile asset." : "Could not delete the local profile asset.",
                "OK");
        }

        [MenuItem(MenuRoot + "Clear Local Profile Asset", true)]
        private static bool ClearLocalProfileAssetMenuItemValidate()
        {
            return HoyoToonUserProfileStorage.GetLocalProfile() != null;
        }

        [MenuItem(MenuRoot + "Clear Returning User Cache", false, 41)]
        private static void ClearReturningUserCacheMenuItem()
        {
            if (!EditorUtility.DisplayDialog(
                "Clear Returning User Cache",
                "Clear the cross-project HoyoToon user cache?\n\nThis does not delete the local ScriptableObject or the API user record.",
                "Clear Cache",
                "Cancel"))
            {
                return;
            }

            HoyoToonUserProfileGlobalStore.Clear();
            HoyoToonDialog.DisplayDialog("Returning User Cache", "Cleared the global returning-user cache.", "OK");
        }

        [MenuItem(MenuRoot + "Clear Returning User Cache", true)]
        private static bool ClearReturningUserCacheMenuItemValidate()
        {
            return HoyoToonUserProfileGlobalStore.TryLoad(out _);
        }

        [MenuItem(MenuRoot + "Clear Local Profile And Cache", false, 42)]
        private static void ClearLocalProfileAndCacheMenuItem()
        {
            if (!EditorUtility.DisplayDialog(
                "Clear Local Profile And Cache",
                "Delete the local profile asset and clear the cross-project returning-user cache?\n\nThis does not delete the API user record.",
                "Clear Both",
                "Cancel"))
            {
                return;
            }

            bool deletedLocal = HoyoToonUserProfileStorage.DeleteLocalProfile(clearGlobalCache: true);
            if (!deletedLocal)
            {
                HoyoToonUserProfileGlobalStore.Clear();
            }

            HoyoToonDialog.DisplayDialog("Clear Local Profile And Cache", "Cleared local/global user profile state.", "OK");
        }

        [MenuItem(MenuRoot + "Clear Local Profile And Cache", true)]
        private static bool ClearLocalProfileAndCacheMenuItemValidate()
        {
            return HoyoToonUserProfileStorage.GetLocalProfile() != null
                || HoyoToonUserProfileGlobalStore.TryLoad(out _);
        }

        private static async Task CreateProfileThroughApiAsync(string username)
        {
            try
            {
                HoyoToonUserProfileSO profile = await HoyoToonUserProfileService
                    .CreateLocalUserProfileAsync(username, CancellationToken.None);
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
                HoyoToonDialog.DisplayDialog(
                    "Create HoyoToon Profile",
                    "Created API-backed local profile for " + profile.Username + ".\n" +
                    BuildProfileSummary(profile),
                    "OK");
            }
            catch (Exception exception)
            {
                HoyoToonDialog.DisplayDialog("Create HoyoToon Profile", exception.Message, "OK");
            }
        }

        private static async Task RestoreLocalProfileFromCacheAsync()
        {
            try
            {
                HoyoToonUserProfileSO profile = await HoyoToonUserProfileService
                    .RestoreLocalUserProfileAsync(CancellationToken.None);
                if (profile == null)
                {
                    HoyoToonDialog.DisplayDialog(
                        "Restore Local Profile",
                        "No API user could be restored from the global cache.",
                        "OK");
                    return;
                }

                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
                HoyoToonDialog.DisplayDialog(
                    "Restore Local Profile",
                    "Restored local profile for " + profile.Username + ".\n" +
                    BuildProfileSummary(profile),
                    "OK");
            }
            catch (Exception exception)
            {
                HoyoToonDialog.DisplayDialog("Restore Local Profile", exception.Message, "OK");
            }
        }

        private static async Task CheckLocalUidInApiAsync(string uid)
        {
            try
            {
                UserRecordDto apiUser = await HoyoToonUserApiClient.GetUserAsync(uid, CancellationToken.None);
                string message = apiUser == null
                    ? "No API user was found for UID: " + uid
                    : "API user found.\n" + BuildApiUserSummary(apiUser);
                HoyoToonDialog.DisplayDialog("Check Local UID", message, "OK");
            }
            catch (Exception exception)
            {
                HoyoToonDialog.DisplayDialog("Check Local UID", exception.Message, "OK");
            }
        }

        private static async Task RefreshLocalProfileFromApiAsync(HoyoToonUserProfileSO profile)
        {
            try
            {
                HoyoToonUserProfileSO refreshedProfile = await HoyoToonUserProfileService
                    .RefreshLocalProfileFromApiAsync(profile, CancellationToken.None);
                Selection.activeObject = refreshedProfile;
                EditorGUIUtility.PingObject(refreshedProfile);
                HoyoToonDialog.DisplayDialog(
                    "Refresh Local Profile",
                    "Refreshed local profile from the API.\n" +
                    BuildProfileSummary(refreshedProfile),
                    "OK");
            }
            catch (Exception exception)
            {
                HoyoToonDialog.DisplayDialog("Refresh Local Profile", exception.Message, "OK");
            }
        }

        private static async Task UpdateLocalAvatarUrlThroughApiAsync(
            HoyoToonUserProfileSO profile,
            string avatarUrl)
        {
            try
            {
                HoyoToonUserProfileSO updatedProfile = await HoyoToonUserProfileService
                    .UpdateLocalUserAvatarAsync(profile, avatarUrl, CancellationToken.None);
                Selection.activeObject = updatedProfile;
                EditorGUIUtility.PingObject(updatedProfile);
                HoyoToonDialog.DisplayDialog(
                    "Update Avatar URL",
                    "Updated avatar URL for " + updatedProfile.Username + ".\n" +
                    BuildProfileSummary(updatedProfile),
                    "OK");
            }
            catch (Exception exception)
            {
                HoyoToonDialog.DisplayDialog("Update Avatar URL", exception.Message, "OK");
            }
        }

        private static string BuildProfileSummary(HoyoToonUserProfileSO profile)
        {
            if (profile == null)
            {
                return "(none)";
            }

            return "UID: " + EmptyAsNone(profile.UID) + "\n" +
                "Username: " + EmptyAsNone(profile.Username) + "\n" +
                "Role: " + EmptyAsNone(profile.RoleName) + "\n" +
                "Role Color: " + EmptyAsNone(profile.RoleColor) + "\n" +
                "Avatar: " + EmptyAsNone(profile.Avatar);
        }

        private static string BuildApiUserSummary(UserRecordDto apiUser)
        {
            if (apiUser == null)
            {
                return "(none)";
            }

            return "UID: " + EmptyAsNone(apiUser.UID) + "\n" +
                "Username: " + EmptyAsNone(apiUser.username) + "\n" +
                "Role: " + EmptyAsNone(apiUser.roleName) + "\n" +
                "Role Color: " + EmptyAsNone(apiUser.roleColor) + "\n" +
                "Avatar: " + EmptyAsNone(apiUser.avatar);
        }

        private static string DescribeRefreshStartResult(HoyoToonApiSyncRefreshStartResult result)
        {
            switch (result)
            {
                case HoyoToonApiSyncRefreshStartResult.Started:
                    return "Started a HoyoToon user profile refresh from the API.";

                case HoyoToonApiSyncRefreshStartResult.AlreadyRefreshing:
                    return "A HoyoToon user profile refresh is already running.";

                case HoyoToonApiSyncRefreshStartResult.EditorNotReady:
                    return "The editor is not ready for API refresh work yet.";

                case HoyoToonApiSyncRefreshStartResult.NoRefreshTarget:
                    return "No local profile or returning-user cache is available to refresh.";

                case HoyoToonApiSyncRefreshStartResult.NotDue:
                    return "The next automatic refresh is not due yet.";

                default:
                    return "Profile refresh did not start.";
            }
        }

        private static string FormatUtc(DateTime? utcDateTime)
        {
            return utcDateTime.HasValue
                ? utcDateTime.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                : "(never)";
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        }
    }
}
#endif
