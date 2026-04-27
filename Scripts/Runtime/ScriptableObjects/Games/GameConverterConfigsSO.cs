using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameConverterConfigs", menuName = "HoyoToon/GameConverterConfigs")]
    public class GameConverterConfigsSO : ScriptableObject
    {
        [Serializable]
        public class Entry : GameScopedEntry
        {
            [SerializeField] private string converterType;
            [SerializeField] private string key;
            [SerializeField] private GameConverterSectionData features = new GameConverterSectionData();
            [SerializeField] private GameConverterSectionData disable = new GameConverterSectionData();
            [SerializeField] private GameConverterMappingData removeMeshes = new GameConverterMappingData();
            [SerializeField] private GameConverterMappingData removeBones = new GameConverterMappingData();
            [SerializeField] private GameConverterMappingData renameBones = new GameConverterMappingData();

            public string ConverterType => converterType;

            public string Key => key;

            public GameConverterSectionData Features => features;

            public GameConverterSectionData Disable => disable;

            public GameConverterMappingData RemoveMeshes => removeMeshes;

            public GameConverterMappingData RemoveBones => removeBones;

            public GameConverterMappingData RenameBones => renameBones;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;
    }
}