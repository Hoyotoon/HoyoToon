using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameCharacterIds", menuName = "HoyoToon/GameCharacterIds")]
    public class GameCharacterIdsSO : GameScopedScriptableObject
    {
        public enum MatchKind
        {
            None,
            OverrideName,
            Name,
            SourceName,
        }

        [Serializable]
        public class Entry
        {
            [SerializeField] private int characterId;
            [SerializeField] private string name;
            [SerializeField] private string sourceName;
            [SerializeField] private string overrideName;
            [SerializeField] private string avatarIcon;
            [SerializeField] private string roundIcon;
            [SerializeField] private string splashIcon;

            public int CharacterId => characterId;

            public string Name => name;

            public string SourceName => sourceName;

            public string OverrideName => overrideName;

            public string AvatarIcon => avatarIcon;

            public string RoundIcon => roundIcon;

            public string SplashIcon => splashIcon;

            public bool MatchesExact(string characterName)
            {
                return MatchesOverrideName(characterName)
                    || MatchesName(characterName)
                    || MatchesSourceName(characterName);
            }

            public bool MatchesOverrideName(string characterName)
            {
                return Matches(OverrideName, characterName);
            }

            public bool MatchesName(string characterName)
            {
                return Matches(Name, characterName);
            }

            public bool MatchesSourceName(string characterName)
            {
                return Matches(SourceName, characterName);
            }

            private static bool Matches(string candidate, string characterName)
            {
                return !string.IsNullOrWhiteSpace(candidate)
                    && !string.IsNullOrWhiteSpace(characterName)
                    && string.Equals(candidate.Trim(), characterName.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryFindExact(string characterName, out Entry entry)
        {
            return TryFindExact(characterName, out entry, out _);
        }

        public bool TryFindExact(string characterName, out Entry entry, out MatchKind matchKind)
        {
            entry = null;
            matchKind = MatchKind.None;

            if (string.IsNullOrWhiteSpace(characterName) || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry candidate = entries[i];
                if (candidate != null && candidate.MatchesOverrideName(characterName))
                {
                    entry = candidate;
                    matchKind = MatchKind.OverrideName;
                    return true;
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry candidate = entries[i];
                if (candidate != null && candidate.MatchesName(characterName))
                {
                    entry = candidate;
                    matchKind = MatchKind.Name;
                    return true;
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry candidate = entries[i];
                if (candidate != null && candidate.MatchesSourceName(characterName))
                {
                    entry = candidate;
                    matchKind = MatchKind.SourceName;
                    return true;
                }
            }

            return false;
        }
    }
}