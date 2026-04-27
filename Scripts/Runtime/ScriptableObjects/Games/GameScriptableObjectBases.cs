using System;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    public abstract class GameScopedScriptableObject : ScriptableObject
    {
        [SerializeField] private string gameKey;

        public string GameKey => gameKey;
    }

    [Serializable]
    public abstract class GameScopedEntry
    {
        [SerializeField] private string gameKey;

        public string GameKey => gameKey;
    }
}