using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Resources
{
    [CreateAssetMenu(fileName = "HoyoToonResources", menuName = "HoyoToon/HoyoToonResources")]
    public class HoyoToonResourcesSO : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private string key;
        [SerializeField] private string localPath;
        [SerializeField] private string webdavUrl;

        public string DisplayName => displayName;

        public string Key => key;

        public string LocalPath => localPath;

        public string WebdavUrl => webdavUrl;
    }
}