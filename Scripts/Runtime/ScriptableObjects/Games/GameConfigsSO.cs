using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "HoyoToon/GameConfig")]
    public class GameConfigSO : ScriptableObject
    {
        [SerializeField] private string key;
        [SerializeField] private string defaultShader;
        [SerializeField] private List<string> gameProperties = new List<string>();

        public string Key => key;

        public string DefaultShader => defaultShader;

        public IReadOnlyList<string> GameProperties => gameProperties;
    }
}