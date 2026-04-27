using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GamePropertyConversions", menuName = "HoyoToon/GamePropertyConversions")]
    public class GamePropertyConversionsSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string sourceProperty;
            [SerializeField] private string targetProperty;

            public string SourceProperty => sourceProperty;

            public string TargetProperty => targetProperty;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}