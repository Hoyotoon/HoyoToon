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

        internal static HoyoToonUserProfileSO LocalProfile => HoyoToonUserProfileStorage.GetLocalProfile();

        internal static bool HasCompleteLocalProfile => HoyoToonUserProfileStorage.HasCompleteLocalProfile();

        internal static async Task<HoyoToonUserProfileSO> CreateLocalUserProfileAsync(
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

                HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.SaveLocalProfile(
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

        internal static async Task<HoyoToonUserProfileSO> RestoreLocalUserProfileAsync(CancellationToken cancellationToken)
        {
            HoyoToonUserProfileSO localProfile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                return await RefreshLocalProfileFromApiAsync(localProfile, cancellationToken);
            }

            if (!HoyoToonUserProfileGlobalStore.TryLoad(out UserProfileGlobalRecord cachedProfile))
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
            catch
            {
                if (!string.IsNullOrWhiteSpace(cachedProfile.username))
                {
                    return HoyoToonUserProfileStorage.SaveLocalProfile(
                        cachedProfile.UID,
                        cachedProfile.username,
                        cachedProfile.avatar,
                        cachedProfile.roleName,
                        cachedProfile.roleColor);
                }

                throw;
            }

            HoyoToonUserProfileGlobalStore.Clear();
            return null;
        }

        internal static async Task<HoyoToonUserProfileSO> UpdateLocalUserAvatarAsync(
            HoyoToonUserProfileSO localProfile,
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

                HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.SaveLocalProfile(
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

        internal static async Task<HoyoToonUserProfileSO> RefreshLocalProfileFromApiAsync(
            HoyoToonUserProfileSO localProfile,
            CancellationToken cancellationToken)
        {
            if (!HoyoToonUserProfileStorage.IsComplete(localProfile))
            {
                return localProfile;
            }

            try
            {
                UserRecordDto apiUser = await HoyoToonUserApiClient.GetUserAsync(localProfile.UID, cancellationToken);
                if (apiUser != null)
                {
                    return SaveApiUser(apiUser, localProfile);
                }
            }
            catch
            {
                HoyoToonUserProfileGlobalStore.Save(
                    localProfile.UID,
                    localProfile.Username,
                    localProfile.Avatar,
                    localProfile.RoleName,
                    localProfile.RoleColor);
                throw;
            }

            HoyoToonUserProfileGlobalStore.Save(
                localProfile.UID,
                localProfile.Username,
                localProfile.Avatar,
                localProfile.RoleName,
                localProfile.RoleColor);
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

        internal static HoyoToonUserProfileSO SaveApiUserProfile(UserRecordDto apiUser)
        {
            return SaveApiUser(apiUser, HoyoToonUserProfileStorage.GetLocalProfile());
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

        private static HoyoToonUserProfileSO SaveApiUser(
            UserRecordDto apiUser,
            HoyoToonUserProfileSO currentProfile = null)
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

            if (apiUser == null || string.IsNullOrWhiteSpace(apiUser.UID))
            {
                return false;
            }

            if (!HoyoToonUserProfileStorage.IsNumericUid(apiUser.UID))
            {
                return false;
            }

            normalizedUid = apiUser.UID.Trim();
            normalizedUsername = apiUser.username?.Trim() ?? string.Empty;
            normalizedAvatar = string.IsNullOrWhiteSpace(apiUser.avatar)
                ? HoyoToonApi.DefaultUserAvatar
                : apiUser.avatar.Trim();
            normalizedRoleName = string.IsNullOrWhiteSpace(apiUser.roleName)
                ? HoyoToonApi.DefaultUserRoleName
                : apiUser.roleName.Trim();
            normalizedRoleColor = string.IsNullOrWhiteSpace(apiUser.roleColor)
                ? HoyoToonApi.DefaultUserRoleColor
                : apiUser.roleColor.Trim();
            return true;
        }

        private static bool IsSameProfile(
            HoyoToonUserProfileSO profile,
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
