using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameProblemLists", menuName = "HoyoToon/GameProblemLists")]
    public class GameProblemListsSO : GameScopedScriptableObject
    {
        [SerializeField] [TextArea(2, 6)] private string regex;
        [SerializeField] private List<GameProblemEntryData> entries = new List<GameProblemEntryData>();

        public string Regex => regex;

        public IReadOnlyList<GameProblemEntryData> Entries => entries;
    }
}