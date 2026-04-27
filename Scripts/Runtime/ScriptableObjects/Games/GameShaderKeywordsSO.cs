using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameShaderKeywords", menuName = "HoyoToon/GameShaderKeywords")]
    public class GameShaderKeywordsSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string shaderPath;
            [SerializeField] private List<string> keywords = new List<string>();

            public string ShaderPath => shaderPath;

            public IReadOnlyList<string> Keywords => keywords;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}