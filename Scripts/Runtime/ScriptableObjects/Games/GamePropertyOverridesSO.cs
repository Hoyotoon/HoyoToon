using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GamePropertyOverrides", menuName = "HoyoToon/GamePropertyOverrides")]
    public class GamePropertyOverridesSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string shaderPath;
            [SerializeField] private SerializedJsonValue overrides = new SerializedJsonValue();

            public string ShaderPath => shaderPath;

            public SerializedJsonValue Overrides => overrides;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}