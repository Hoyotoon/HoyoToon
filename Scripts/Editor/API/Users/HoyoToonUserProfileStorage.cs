using System;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Runtime.ScriptableObjects.Users;
using UnityEditor;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetSyncUtility;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfileStorage
    {
        private const string UserFolderName = "User";
        private const string LocalProfileAssetName = "LocalUserProfile";

        internal static event Action ProfileChanged;

        internal static string LocalProfileFolderPath => $"{HoyoToonApi.ScriptablesAssetPath}/{UserFolderName}";

        internal static string LocalProfileAssetPath => $"{LocalProfileFolderPath}/{LocalProfileAssetName}.asset";

        internal static HoyoToonUserProfileSO GetLocalProfile()
        {
            return AssetDatabase.LoadAssetAtPath<HoyoToonUserProfileSO>(LocalProfileAssetPath);
        }

        internal static bool HasCompleteLocalProfile()
        {
            return IsComplete(GetLocalProfile());
        }

        internal static bool IsComplete(HoyoToonUserProfileSO profile)
        {
            return profile != null
                && IsNumericUid(profile.UID)
                && !string.IsNullOrWhiteSpace(profile.Username);
        }

        internal static bool IsNumericUid(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return false;
            }

            string trimmedUid = uid.Trim();
            for (int i = 0; i < trimmedUid.Length; i++)
            {
                if (!char.IsDigit(trimmedUid[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static HoyoToonUserProfileSO SaveLocalProfile(string uid, string username, string avatar, bool updateGlobalCache = true)
        {
            return SaveLocalProfile(
                uid,
                username,
                avatar,
                HoyoToonApi.DefaultUserRoleName,
                HoyoToonApi.DefaultUserRoleColor,
                updateGlobalCache);
        }

        internal static HoyoToonUserProfileSO SaveLocalProfile(
            string uid,
            string username,
            string avatar,
            string roleName,
            string roleColor,
            bool updateGlobalCache = true)
        {
            string normalizedUid = uid?.Trim() ?? string.Empty;
            string normalizedUsername = username?.Trim() ?? string.Empty;
            string normalizedAvatar = string.IsNullOrWhiteSpace(avatar)
                ? HoyoToonApi.DefaultUserAvatar
                : avatar.Trim();
            string normalizedRoleName = string.IsNullOrWhiteSpace(roleName)
                ? HoyoToonApi.DefaultUserRoleName
                : roleName.Trim();
            string normalizedRoleColor = string.IsNullOrWhiteSpace(roleColor)
                ? HoyoToonApi.DefaultUserRoleColor
                : roleColor.Trim();

            GeneratedAssetSyncUtility.EnsureAssetFolderExists(LocalProfileFolderPath);
            HoyoToonUserProfileSO profile = GeneratedAssetSyncUtility
                .LoadOrCreateAsset<HoyoToonUserProfileSO>(LocalProfileAssetPath);

            long nowTicks = DateTime.UtcNow.Ticks;
            long createdTicks = profile.CreatedAtUtcTicks > 0 ? profile.CreatedAtUtcTicks : nowTicks;
            GeneratedAssetSyncUtility.OverwriteAsset(
                profile,
                CreateObject(
                    ("uid", normalizedUid),
                    ("username", normalizedUsername),
                    ("avatar", normalizedAvatar),
                    ("roleName", normalizedRoleName),
                    ("roleColor", normalizedRoleColor),
                    ("createdAtUtcTicks", createdTicks),
                    ("updatedAtUtcTicks", nowTicks)));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (updateGlobalCache)
            {
                HoyoToonUserProfileGlobalStore.Save(
                    normalizedUid,
                    normalizedUsername,
                    normalizedAvatar,
                    normalizedRoleName,
                    normalizedRoleColor);
            }

            ProfileChanged?.Invoke();
            return profile;
        }

        internal static bool DeleteLocalProfile(bool clearGlobalCache = false)
        {
            if (AssetDatabase.LoadAssetAtPath<HoyoToonUserProfileSO>(LocalProfileAssetPath) == null)
            {
                if (clearGlobalCache)
                {
                    HoyoToonUserProfileGlobalStore.Clear();
                    ProfileChanged?.Invoke();
                }

                return false;
            }

            bool deleted = AssetDatabase.DeleteAsset(LocalProfileAssetPath);
            if (!deleted)
            {
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (clearGlobalCache)
            {
                HoyoToonUserProfileGlobalStore.Clear();
            }

            ProfileChanged?.Invoke();
            return true;
        }
    }
}
