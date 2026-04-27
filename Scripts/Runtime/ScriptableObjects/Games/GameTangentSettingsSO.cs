using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameTangentSettings", menuName = "HoyoToon/GameTangentSettings")]
    public class GameTangentSettingsSO : GameScopedScriptableObject
    {
        [SerializeField] private List<string> options = new List<string>();
        [SerializeField] private string status;
        [SerializeField] private List<string> skipMeshesContaining = new List<string>();

        public IReadOnlyList<string> Options => options;

        public string Status => status;

        public IReadOnlyList<string> SkipMeshesContaining => skipMeshesContaining;
    }
}