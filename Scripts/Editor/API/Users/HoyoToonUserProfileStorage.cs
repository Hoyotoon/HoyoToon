using System;
using HoyoToon.Runtime.ScriptableObjects.Users;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfileStorage
    {
        private const string LegacyUserFolderName = "User";
        private const string LegacyLocalProfileAssetName = "LocalUserProfile";

        private static HoyoToonUserProfileSO cachedProfile;

        internal static event Action ProfileChanged;

        private static string LegacyLocalProfileFolderPath => $"{HoyoToonApi.ScriptablesAssetPath}/{LegacyUserFolderName}";

        private static string LegacyLocalProfileAssetPath => $"{LegacyLocalProfileFolderPath}/{LegacyLocalProfileAssetName}.asset";

        [InitializeOnLoadMethod]
        private static void ScheduleLegacyLocalProfileMigration()
        {
            EditorApplication.delayCall += EnsureMigratedFromLegacyAsset;
        }

        internal static HoyoToonUserProfileSO GetLocalProfile()
        {
            EnsureMigratedFromLegacyAsset();

            if (!HoyoToonUserProfileLocalStore.TryLoad(out UserProfileLocalRecord record))
            {
                DestroyCachedProfile();
                return null;
            }

            return CacheRecord(record);
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

            EnsureMigratedFromLegacyAsset();
            long nowTicks = DateTime.UtcNow.Ticks;
            long createdTicks = HoyoToonUserProfileLocalStore.TryLoad(out UserProfileLocalRecord existingRecord)
                && existingRecord.createdAtUtcTicks > 0
                ? existingRecord.createdAtUtcTicks
                : nowTicks;

            var record = new UserProfileLocalRecord
            {
                UID = normalizedUid,
                username = normalizedUsername,
                avatar = normalizedAvatar,
                roleName = normalizedRoleName,
                roleColor = normalizedRoleColor,
                createdAtUtcTicks = createdTicks,
                updatedAtUtcTicks = nowTicks,
            };

            HoyoToonUserProfileLocalStore.Save(record);
            DeleteLegacyLocalProfileAssetIfPresent();

            HoyoToonUserProfileSO profile = CacheRecord(record);
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
            bool deletedLocalStore = HoyoToonUserProfileLocalStore.Clear();
            bool deletedLegacyAsset = DeleteLegacyLocalProfileAssetIfPresent();
            bool deleted = deletedLocalStore || deletedLegacyAsset;

            DestroyCachedProfile();

            if (!deleted)
            {
                if (clearGlobalCache)
                {
                    HoyoToonUserProfileGlobalStore.Clear();
                    ProfileChanged?.Invoke();
                }

                return false;
            }

            if (clearGlobalCache)
            {
                HoyoToonUserProfileGlobalStore.Clear();
            }

            ProfileChanged?.Invoke();
            return true;
        }

        private static HoyoToonUserProfileSO CacheRecord(UserProfileLocalRecord record)
        {
            HoyoToonUserProfileSO profile = GetOrCreateCachedProfile();
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("uid").stringValue = record.UID ?? string.Empty;
            serializedProfile.FindProperty("username").stringValue = record.username ?? string.Empty;
            serializedProfile.FindProperty("avatar").stringValue = NormalizeAvatar(record.avatar);
            serializedProfile.FindProperty("roleName").stringValue = NormalizeRoleName(record.roleName);
            serializedProfile.FindProperty("roleColor").stringValue = NormalizeRoleColor(record.roleColor);
            serializedProfile.FindProperty("createdAtUtcTicks").longValue = record.createdAtUtcTicks;
            serializedProfile.FindProperty("updatedAtUtcTicks").longValue = record.updatedAtUtcTicks;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static void EnsureMigratedFromLegacyAsset()
        {
            HoyoToonUserProfileSO legacyProfile = LoadLegacyLocalProfileAsset();
            if (legacyProfile == null)
            {
                return;
            }

            if (!HoyoToonUserProfileLocalStore.TryLoad(out _))
            {
                HoyoToonUserProfileLocalStore.Save(CreateRecordFromProfile(legacyProfile));
            }

            DeleteLegacyLocalProfileAssetIfPresent();
        }

        private static HoyoToonUserProfileSO GetOrCreateCachedProfile()
        {
            if (cachedProfile == null)
            {
                cachedProfile = ScriptableObject.CreateInstance<HoyoToonUserProfileSO>();
                cachedProfile.name = "HoyoToon Local User Profile";
                cachedProfile.hideFlags = HideFlags.HideAndDontSave;
            }

            return cachedProfile;
        }

        private static HoyoToonUserProfileSO LoadLegacyLocalProfileAsset()
        {
            return AssetDatabase.LoadAssetAtPath<HoyoToonUserProfileSO>(LegacyLocalProfileAssetPath);
        }

        private static UserProfileLocalRecord CreateRecordFromProfile(HoyoToonUserProfileSO profile)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            return new UserProfileLocalRecord
            {
                UID = profile != null ? profile.UID?.Trim() ?? string.Empty : string.Empty,
                username = profile != null ? profile.Username?.Trim() ?? string.Empty : string.Empty,
                avatar = NormalizeAvatar(profile != null ? profile.Avatar : string.Empty),
                roleName = NormalizeRoleName(profile != null ? profile.RoleName : string.Empty),
                roleColor = NormalizeRoleColor(profile != null ? profile.RoleColor : string.Empty),
                createdAtUtcTicks = profile != null && profile.CreatedAtUtcTicks > 0 ? profile.CreatedAtUtcTicks : nowTicks,
                updatedAtUtcTicks = profile != null && profile.UpdatedAtUtcTicks > 0 ? profile.UpdatedAtUtcTicks : nowTicks,
            };
        }

        private static bool DeleteLegacyLocalProfileAssetIfPresent()
        {
            if (LoadLegacyLocalProfileAsset() == null)
            {
                return false;
            }

            bool deleted = AssetDatabase.DeleteAsset(LegacyLocalProfileAssetPath);
            if (deleted)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return deleted;
        }

        private static void DestroyCachedProfile()
        {
            if (cachedProfile == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(cachedProfile);
            cachedProfile = null;
        }

        private static string NormalizeAvatar(string avatar)
        {
            return string.IsNullOrWhiteSpace(avatar)
                ? HoyoToonApi.DefaultUserAvatar
                : avatar.Trim();
        }

        private static string NormalizeRoleName(string roleName)
        {
            return string.IsNullOrWhiteSpace(roleName)
                ? HoyoToonApi.DefaultUserRoleName
                : roleName.Trim();
        }

        private static string NormalizeRoleColor(string roleColor)
        {
            return string.IsNullOrWhiteSpace(roleColor)
                ? HoyoToonApi.DefaultUserRoleColor
                : roleColor.Trim();
        }
    }
}
