using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Runtime.ScriptableObjects.Users;

namespace HoyoToon.Editor.API.Users
{
    internal static class HoyoToonUserProfileService
    {
        private const int MinUsernameLength = 2;
        private const int MaxUsernameLength = 32;
        private const int MaxAvatarUrlLength = 2048;

        internal static bool IsCreating { get; private set; }

        internal static bool IsUpdatingAvatar { get; private set; }

        internal static string LastError { get; private set; } = string.Empty;

        internal static UserProfileSO LocalProfile => HoyoToonUserProfileStorage.GetLocalProfile();

        internal static bool HasCompleteLocalProfile => HoyoToonUserProfileStorage.HasCompleteLocalProfile();

        internal static async Task<UserProfileSO> CreateLocalUserProfileAsync(
            string username,
            CancellationToken cancellationToken)
        {
            if (IsCreating)
            {
                throw new InvalidOperationException("A HoyoToon user profile is already being created.");
            }

            if (!TryNormalizeUsername(username, out string normalizedUsername, out string validationMessage))
            {
                LastError = validationMessage;
                throw new ArgumentException(validationMessage, nameof(username));
            }

            IsCreating = true;
            LastError = string.Empty;

            try
            {
                string avatar = HoyoToonApi.DefaultUserAvatar;
                CreateUserResponseDto createdUser = await HoyoToonUserApiClient.CreateUserAsync(
                    normalizedUsername,
                    avatar,
                    cancellationToken);
                if (createdUser == null || string.IsNullOrWhiteSpace(createdUser.UID))
                {
                    throw new InvalidOperationException("The HoyoToon API did not return a UID for the created user.");
                }

                if (!HoyoToonUserProfileStorage.IsNumericUid(createdUser.UID))
                {
                    throw new InvalidOperationException("The HoyoToon API returned a non-numeric UID.");
                }

                UserProfileSO profile = HoyoToonUserProfileStorage.SaveLocalProfile(
                    createdUser.UID,
                    string.IsNullOrWhiteSpace(createdUser.username) ? normalizedUsername : createdUser.username,
                    string.IsNullOrWhiteSpace(createdUser.avatar) ? avatar : createdUser.avatar,
                    string.IsNullOrWhiteSpace(createdUser.roleName) ? HoyoToonApi.DefaultUserRoleName : createdUser.roleName,
                    string.IsNullOrWhiteSpace(createdUser.roleColor) ? HoyoToonApi.DefaultUserRoleColor : createdUser.roleColor);
                LastError = string.Empty;
                return profile;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                throw;
            }
            finally
            {
                IsCreating = false;
            }
        }

        internal static async Task<UserProfileSO> RestoreLocalUserProfileAsync(CancellationToken cancellationToken)
        {
            UserProfileSO localProfile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                return await RefreshLocalProfileFromApiAsync(localProfile, cancellationToken);
            }

            if (!TryLoadCachedProfile(out UserProfileGlobalRecord cachedProfile))
            {
                return null;
            }

            try
            {
                UserRecordDto apiUser = await HoyoToonUserApiClient.GetUserAsync(cachedProfile.UID, cancellationToken);
                if (apiUser != null)
                {
                    return SaveApiUser(apiUser, localProfile);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return SaveCachedUser(cachedProfile, localProfile);
            }

            HoyoToonUserProfileGlobalStore.Clear();
            return null;
        }

        internal static async Task<UserProfileSO> UpdateLocalUserAvatarAsync(
            UserProfileSO localProfile,
            string avatarUrl,
            CancellationToken cancellationToken)
        {
            if (IsUpdatingAvatar)
            {
                throw new InvalidOperationException("A HoyoToon user avatar update is already running.");
            }

            if (!HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                throw new InvalidOperationException("Create or restore a HoyoToon profile before updating the avatar.");
            }

            if (!TryNormalizeAvatarUrl(avatarUrl, out string normalizedAvatarUrl, out string validationMessage))
            {
                LastError = validationMessage;
                throw new ArgumentException(validationMessage, nameof(avatarUrl));
            }

            IsUpdatingAvatar = true;
            LastError = string.Empty;

            try
            {
                UpdateUserAvatarResponseDto updatedUser = await HoyoToonUserApiClient.UpdateUserAvatarAsync(
                    localProfile.UID,
                    normalizedAvatarUrl,
                    cancellationToken);

                UserProfileSO profile = HoyoToonUserProfileStorage.SaveLocalProfile(
                    localProfile.UID,
                    localProfile.Username,
                    normalizedAvatarUrl,
                    string.IsNullOrWhiteSpace(updatedUser?.roleName) ? localProfile.RoleName : updatedUser.roleName,
                    string.IsNullOrWhiteSpace(updatedUser?.roleColor) ? localProfile.RoleColor : updatedUser.roleColor);
                LastError = string.Empty;
                return profile;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                throw;
            }
            finally
            {
                IsUpdatingAvatar = false;
            }
        }

        internal static async Task<UserProfileSO> RefreshLocalProfileFromApiAsync(
            UserProfileSO localProfile,
            CancellationToken cancellationToken)
        {
            if (!TryResolveAuthoritativeRefreshUid(localProfile, out string refreshUid, out UserProfileGlobalRecord cachedProfile))
            {
                return localProfile;
            }

            try
            {
                UserRecordDto apiUser = await HoyoToonUserApiClient.GetUserAsync(refreshUid, cancellationToken);
                if (apiUser != null)
                {
                    return SaveApiUser(apiUser, localProfile);
                }
            }
            catch
            {
                SaveProfileSnapshotForFallback(localProfile, cachedProfile);
                throw;
            }

            UserProfileSO cachedProfileAsset = SaveCachedUser(cachedProfile, localProfile);
            if (cachedProfileAsset != null)
            {
                return cachedProfileAsset;
            }

            HoyoToonUserProfileGlobalStore.Clear();
            return localProfile;
        }

        internal static bool LocalProfileMatchesApiUser(UserRecordDto apiUser)
        {
            if (!TryNormalizeApiUser(
                apiUser,
                out string normalizedUid,
                out string normalizedUsername,
                out string normalizedAvatar,
                out string normalizedRoleName,
                out string normalizedRoleColor))
            {
                return false;
            }

            return IsSameProfile(
                HoyoToonUserProfileStorage.GetLocalProfile(),
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor);
        }

        internal static bool LocalProfileMatchesCachedUser()
        {
            if (!TryLoadCachedProfile(out UserProfileGlobalRecord cachedProfile)
                || !TryNormalizeCachedUser(
                    cachedProfile,
                    out string normalizedUid,
                    out string normalizedUsername,
                    out string normalizedAvatar,
                    out string normalizedRoleName,
                    out string normalizedRoleColor))
            {
                return false;
            }

            return IsSameProfile(
                HoyoToonUserProfileStorage.GetLocalProfile(),
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor);
        }

        internal static UserProfileSO SaveApiUserProfile(UserRecordDto apiUser)
        {
            return SaveApiUser(apiUser, HoyoToonUserProfileStorage.GetLocalProfile());
        }

        internal static UserProfileSO SaveCachedUserProfile()
        {
            UserProfileSO currentProfile = HoyoToonUserProfileStorage.GetLocalProfile();
            return TryLoadCachedProfile(out UserProfileGlobalRecord cachedProfile)
                ? SaveCachedUser(cachedProfile, currentProfile)
                : currentProfile;
        }

        internal static bool TryResolveAuthoritativeRefreshUid(
            UserProfileSO localProfile,
            out string uid,
            out UserProfileGlobalRecord cachedProfile)
        {
            cachedProfile = null;
            uid = string.Empty;

            if (TryLoadCachedProfile(out cachedProfile))
            {
                uid = cachedProfile.UID;
                return true;
            }

            if (!HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                return false;
            }

            uid = localProfile.UID?.Trim() ?? string.Empty;
            return HoyoToonUserProfileStorage.IsNumericUid(uid);
        }

        internal static bool TryNormalizeUsername(string username, out string normalizedUsername, out string validationMessage)
        {
            normalizedUsername = username?.Trim() ?? string.Empty;
            validationMessage = string.Empty;

            if (normalizedUsername.Length < MinUsernameLength)
            {
                validationMessage = "Username must be at least two characters.";
                return false;
            }

            if (normalizedUsername.Length > MaxUsernameLength)
            {
                validationMessage = $"Username must be {MaxUsernameLength} characters or fewer.";
                return false;
            }

            if (normalizedUsername.Any(char.IsControl))
            {
                validationMessage = "Username cannot contain control characters.";
                return false;
            }

            return true;
        }

        internal static bool TryNormalizeAvatarUrl(string avatarUrl, out string normalizedAvatarUrl, out string validationMessage)
        {
            normalizedAvatarUrl = avatarUrl?.Trim() ?? string.Empty;
            validationMessage = string.Empty;

            if (normalizedAvatarUrl.Length <= 0)
            {
                validationMessage = "Avatar URL is required.";
                return false;
            }

            if (normalizedAvatarUrl.Length > MaxAvatarUrlLength)
            {
                validationMessage = $"Avatar URL must be {MaxAvatarUrlLength} characters or fewer.";
                return false;
            }

            if (normalizedAvatarUrl.Any(char.IsControl))
            {
                validationMessage = "Avatar URL cannot contain control characters.";
                return false;
            }

            if (!Uri.TryCreate(normalizedAvatarUrl, UriKind.Absolute, out Uri uri)
                || string.IsNullOrWhiteSpace(uri.Host)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                validationMessage = "Avatar must be a full http or https image URL.";
                return false;
            }

            return true;
        }

        private static UserProfileSO SaveApiUser(
            UserRecordDto apiUser,
            UserProfileSO currentProfile = null)
        {
            if (!TryNormalizeApiUser(
                apiUser,
                out string normalizedUid,
                out string normalizedUsername,
                out string normalizedAvatar,
                out string normalizedRoleName,
                out string normalizedRoleColor))
            {
                return null;
            }

            if (IsSameProfile(
                currentProfile,
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor))
            {
                HoyoToonUserProfileGlobalStore.Save(
                    normalizedUid,
                    normalizedUsername,
                    normalizedAvatar,
                    normalizedRoleName,
                    normalizedRoleColor);
                return currentProfile;
            }

            return HoyoToonUserProfileStorage.SaveLocalProfile(
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor);
        }

        private static UserProfileSO SaveCachedUser(
            UserProfileGlobalRecord cachedProfile,
            UserProfileSO currentProfile = null)
        {
            if (!TryNormalizeCachedUser(
                cachedProfile,
                out string normalizedUid,
                out string normalizedUsername,
                out string normalizedAvatar,
                out string normalizedRoleName,
                out string normalizedRoleColor))
            {
                return currentProfile;
            }

            if (IsSameProfile(
                currentProfile,
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor))
            {
                return currentProfile;
            }

            return HoyoToonUserProfileStorage.SaveLocalProfile(
                normalizedUid,
                normalizedUsername,
                normalizedAvatar,
                normalizedRoleName,
                normalizedRoleColor);
        }

        private static bool TryNormalizeApiUser(
            UserRecordDto apiUser,
            out string normalizedUid,
            out string normalizedUsername,
            out string normalizedAvatar,
            out string normalizedRoleName,
            out string normalizedRoleColor)
        {
            normalizedUid = string.Empty;
            normalizedUsername = string.Empty;
            normalizedAvatar = HoyoToonApi.DefaultUserAvatar;
            normalizedRoleName = HoyoToonApi.DefaultUserRoleName;
            normalizedRoleColor = HoyoToonApi.DefaultUserRoleColor;

            if (apiUser == null)
            {
                return false;
            }

            return TryNormalizeProfileSnapshot(
                apiUser.UID,
                apiUser.username,
                apiUser.avatar,
                apiUser.roleName,
                apiUser.roleColor,
                out normalizedUid,
                out normalizedUsername,
                out normalizedAvatar,
                out normalizedRoleName,
                out normalizedRoleColor);
        }

        private static bool TryNormalizeCachedUser(
            UserProfileGlobalRecord cachedProfile,
            out string normalizedUid,
            out string normalizedUsername,
            out string normalizedAvatar,
            out string normalizedRoleName,
            out string normalizedRoleColor)
        {
            normalizedUid = string.Empty;
            normalizedUsername = string.Empty;
            normalizedAvatar = HoyoToonApi.DefaultUserAvatar;
            normalizedRoleName = HoyoToonApi.DefaultUserRoleName;
            normalizedRoleColor = HoyoToonApi.DefaultUserRoleColor;

            if (cachedProfile == null)
            {
                return false;
            }

            return TryNormalizeProfileSnapshot(
                cachedProfile.UID,
                cachedProfile.username,
                cachedProfile.avatar,
                cachedProfile.roleName,
                cachedProfile.roleColor,
                out normalizedUid,
                out normalizedUsername,
                out normalizedAvatar,
                out normalizedRoleName,
                out normalizedRoleColor);
        }

        private static bool TryNormalizeProfileSnapshot(
            string uid,
            string username,
            string avatar,
            string roleName,
            string roleColor,
            out string normalizedUid,
            out string normalizedUsername,
            out string normalizedAvatar,
            out string normalizedRoleName,
            out string normalizedRoleColor)
        {
            normalizedUid = string.Empty;
            normalizedUsername = string.Empty;
            normalizedAvatar = HoyoToonApi.DefaultUserAvatar;
            normalizedRoleName = HoyoToonApi.DefaultUserRoleName;
            normalizedRoleColor = HoyoToonApi.DefaultUserRoleColor;

            if (string.IsNullOrWhiteSpace(uid) || !HoyoToonUserProfileStorage.IsNumericUid(uid))
            {
                return false;
            }

            normalizedUid = uid.Trim();
            normalizedUsername = username?.Trim() ?? string.Empty;
            normalizedAvatar = string.IsNullOrWhiteSpace(avatar)
                ? HoyoToonApi.DefaultUserAvatar
                : avatar.Trim();
            normalizedRoleName = string.IsNullOrWhiteSpace(roleName)
                ? HoyoToonApi.DefaultUserRoleName
                : roleName.Trim();
            normalizedRoleColor = string.IsNullOrWhiteSpace(roleColor)
                ? HoyoToonApi.DefaultUserRoleColor
                : roleColor.Trim();
            return true;
        }

        private static bool TryLoadCachedProfile(out UserProfileGlobalRecord cachedProfile)
        {
            return HoyoToonUserProfileGlobalStore.TryLoad(out cachedProfile);
        }

        private static void SaveProfileSnapshotForFallback(
            UserProfileSO localProfile,
            UserProfileGlobalRecord cachedProfile)
        {
            if (cachedProfile != null || !HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                return;
            }

            HoyoToonUserProfileGlobalStore.Save(
                localProfile.UID,
                localProfile.Username,
                localProfile.Avatar,
                localProfile.RoleName,
                localProfile.RoleColor);
        }

        private static bool IsSameProfile(
            UserProfileSO profile,
            string uid,
            string username,
            string avatar,
            string roleName,
            string roleColor)
        {
            return HoyoToonUserProfileStorage.IsComplete(profile)
                && string.Equals(profile.UID?.Trim(), uid, StringComparison.Ordinal)
                && string.Equals(profile.Username?.Trim(), username, StringComparison.Ordinal)
                && string.Equals(NormalizeAvatar(profile.Avatar), NormalizeAvatar(avatar), StringComparison.Ordinal)
                && string.Equals(NormalizeRoleName(profile.RoleName), roleName, StringComparison.Ordinal)
                && string.Equals(NormalizeRoleColor(profile.RoleColor), roleColor, StringComparison.Ordinal);
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

