using System;
using System.IO;
using HoyoToon.Editor.Utilities.Debugging;
using UnityEditor;
using Utf8Json;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfileGlobalStore
    {
        private const string PrefsUidKey = "HoyoToon.UserProfile.UID";
        private const string PrefsUsernameKey = "HoyoToon.UserProfile.Username";
        private const string PrefsAvatarKey = "HoyoToon.UserProfile.Avatar";
        private const string PrefsRoleNameKey = "HoyoToon.UserProfile.RoleName";
        private const string PrefsRoleColorKey = "HoyoToon.UserProfile.RoleColor";
        private const string FolderName = "HoyoToon";
        private const string FileName = "user-profile.json";

        internal static string StoreFilePath => Path.Combine(GetStoreDirectory(), FileName);

        internal static bool TryLoad(out UserProfileGlobalRecord record)
        {
            record = null;
            if (TryLoadFromFile(out record) && IsUsable(record))
            {
                return true;
            }

            record = LoadFromEditorPrefs();
            return IsUsable(record);
        }

        internal static void Save(
            string uid,
            string username,
            string avatar,
            string roleName = null,
            string roleColor = null)
        {
            var record = new UserProfileGlobalRecord
            {
                UID = uid?.Trim() ?? string.Empty,
                username = username?.Trim() ?? string.Empty,
                avatar = string.IsNullOrWhiteSpace(avatar) ? HoyoToonApi.DefaultUserAvatar : avatar.Trim(),
                roleName = string.IsNullOrWhiteSpace(roleName) ? HoyoToonApi.DefaultUserRoleName : roleName.Trim(),
                roleColor = string.IsNullOrWhiteSpace(roleColor) ? HoyoToonApi.DefaultUserRoleColor : roleColor.Trim(),
                updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            };

            if (!IsUsable(record))
            {
                return;
            }

            EditorPrefs.SetString(PrefsUidKey, record.UID);
            EditorPrefs.SetString(PrefsUsernameKey, record.username);
            EditorPrefs.SetString(PrefsAvatarKey, record.avatar);
            EditorPrefs.SetString(PrefsRoleNameKey, record.roleName);
            EditorPrefs.SetString(PrefsRoleColorKey, record.roleColor);

            try
            {
                Directory.CreateDirectory(GetStoreDirectory());
                File.WriteAllBytes(StoreFilePath, JsonSerializer.Serialize(record, HoyoToonApi.JsonResolver));
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    "Failed to write the global HoyoToon user profile cache.",
                    exception);
            }
        }

        internal static void Clear()
        {
            EditorPrefs.DeleteKey(PrefsUidKey);
            EditorPrefs.DeleteKey(PrefsUsernameKey);
            EditorPrefs.DeleteKey(PrefsAvatarKey);
            EditorPrefs.DeleteKey(PrefsRoleNameKey);
            EditorPrefs.DeleteKey(PrefsRoleColorKey);

            try
            {
                if (File.Exists(StoreFilePath))
                {
                    File.Delete(StoreFilePath);
                }
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    "Failed to delete the global HoyoToon user profile cache.",
                    exception);
            }
        }

        private static bool TryLoadFromFile(out UserProfileGlobalRecord record)
        {
            record = null;
            try
            {
                if (!File.Exists(StoreFilePath))
                {
                    return false;
                }

                record = JsonSerializer.Deserialize<UserProfileGlobalRecord>(
                    File.ReadAllBytes(StoreFilePath),
                    HoyoToonApi.JsonResolver);
                return record != null;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    "Failed to read the global HoyoToon user profile cache.",
                    exception);
                return false;
            }
        }

        private static UserProfileGlobalRecord LoadFromEditorPrefs()
        {
            return new UserProfileGlobalRecord
            {
                UID = EditorPrefs.GetString(PrefsUidKey, string.Empty),
                username = EditorPrefs.GetString(PrefsUsernameKey, string.Empty),
                avatar = EditorPrefs.GetString(PrefsAvatarKey, HoyoToonApi.DefaultUserAvatar),
                roleName = EditorPrefs.GetString(PrefsRoleNameKey, HoyoToonApi.DefaultUserRoleName),
                roleColor = EditorPrefs.GetString(PrefsRoleColorKey, HoyoToonApi.DefaultUserRoleColor),
            };
        }

        private static bool IsUsable(UserProfileGlobalRecord record)
        {
            return record != null
                && HoyoToonUserProfileStorage.IsNumericUid(record.UID)
                && !string.IsNullOrWhiteSpace(record.username);
        }

        private static string GetStoreDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrWhiteSpace(appData))
            {
                appData = Directory.GetCurrentDirectory();
            }

            return Path.Combine(appData, FolderName);
        }
    }

    public sealed class UserProfileGlobalRecord
    {
        public string UID;
        public string username;
        public string avatar;
        public string roleName;
        public string roleColor;
        public long updatedAtUtcTicks;
    }
}
