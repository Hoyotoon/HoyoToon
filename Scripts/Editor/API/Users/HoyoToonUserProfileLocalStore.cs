#if UNITY_EDITOR
using System;
using System.IO;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Editor;
using Utf8Json;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfileLocalStore
    {
        private const string FolderName = "HoyoToon";
        private const string ProjectsFolderName = "Projects";
        private const string ProjectFolderKey = "HoyoToon.UserProfile.Local";
        private const string FileName = "local-user-profile.json";

        internal static string StoreDirectoryPath => Path.Combine(
            GetStoreRootDirectory(),
            ProjectsFolderName,
            HoyoToonEditorPrefs.ProjectKey(ProjectFolderKey));

        internal static string StoreFilePath => Path.Combine(StoreDirectoryPath, FileName);

        internal static bool TryLoad(out UserProfileLocalRecord record)
        {
            record = null;

            try
            {
                if (!File.Exists(StoreFilePath))
                {
                    return false;
                }

                record = JsonSerializer.Deserialize<UserProfileLocalRecord>(
                    File.ReadAllBytes(StoreFilePath),
                    HoyoToonApi.JsonResolver);
                return HasData(record);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    "Failed to read the local HoyoToon user profile store.",
                    exception);
                return false;
            }
        }

        internal static void Save(UserProfileLocalRecord record)
        {
            if (!HasData(record))
            {
                throw new InvalidOperationException("The local HoyoToon user profile store cannot save an empty record.");
            }

            Directory.CreateDirectory(StoreDirectoryPath);
            File.WriteAllBytes(StoreFilePath, JsonSerializer.Serialize(record, HoyoToonApi.JsonResolver));
        }

        internal static bool Clear()
        {
            try
            {
                if (!File.Exists(StoreFilePath))
                {
                    return false;
                }

                File.Delete(StoreFilePath);
                return true;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Api,
                    "Failed to delete the local HoyoToon user profile store.",
                    exception);
                return false;
            }
        }

        private static bool HasData(UserProfileLocalRecord record)
        {
            return record != null
                && (!string.IsNullOrWhiteSpace(record.UID)
                    || !string.IsNullOrWhiteSpace(record.username)
                    || !string.IsNullOrWhiteSpace(record.avatar)
                    || !string.IsNullOrWhiteSpace(record.roleName)
                    || !string.IsNullOrWhiteSpace(record.roleColor)
                    || record.createdAtUtcTicks > 0
                    || record.updatedAtUtcTicks > 0);
        }

        private static string GetStoreRootDirectory()
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

    [Serializable]
    public sealed class UserProfileLocalRecord
    {
        public string UID;
        public string username;
        public string avatar;
        public string roleName;
        public string roleColor;
        public long createdAtUtcTicks;
        public long updatedAtUtcTicks;
    }
}
#endif