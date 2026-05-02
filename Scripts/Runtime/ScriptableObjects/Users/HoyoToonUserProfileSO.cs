using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Users
{
    public class HoyoToonUserProfileSO : ScriptableObject
    {
        [SerializeField] private string uid;
        [SerializeField] private string username;
        [SerializeField] private string avatar;
        [SerializeField] private string roleName;
        [SerializeField] private string roleColor;
        [SerializeField] private long createdAtUtcTicks;
        [SerializeField] private long updatedAtUtcTicks;

        public string UID => uid;

        public string Username => username;

        public string Avatar => avatar;

        public string RoleName => roleName;

        public string RoleColor => roleColor;

        public long CreatedAtUtcTicks => createdAtUtcTicks;

        public long UpdatedAtUtcTicks => updatedAtUtcTicks;
    }
}
