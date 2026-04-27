using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameTextureMappings", menuName = "HoyoToon/GameTextureMappings")]
    public class GameTextureMappingsSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string propertyName;
            [SerializeField] private string textureName;

            public string PropertyName => propertyName;

            public string TextureName => textureName;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}