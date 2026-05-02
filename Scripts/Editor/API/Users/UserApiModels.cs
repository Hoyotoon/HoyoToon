using System;

namespace HoyoToon.Editor.API.Users
{
    [Serializable]
    public sealed class UserRecordDto
    {
        public string _id;
        public double _creationTime;
        public string UID;
        public string username;
        public string avatar;
        public string roleName;
        public string roleColor;
    }

    [Serializable]
    public sealed class CreateUserRequestDto
    {
        public string username;
        public string avatar;
    }

    [Serializable]
    public sealed class CreateUserResponseDto
    {
        public string id;
        public string UID;
        public string username;
        public string avatar;
        public string roleName;
        public string roleColor;
    }

    [Serializable]
    public sealed class UpdateUserAvatarRequestDto
    {
        public string UID;
        public string avatar;
    }

    [Serializable]
    public sealed class UpdateUserAvatarResponseDto
    {
        public string id;
        public string UID;
        public string username;
        public string avatar;
        public string roleName;
        public string roleColor;
    }

    [Serializable]
    public sealed class UserErrorDto
    {
        public string error;
    }
}
