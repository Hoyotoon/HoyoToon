using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameCharacterIds", menuName = "HoyoToon/GameCharacterIds")]
    public class GameCharacterIdsSO : GameScopedScriptableObject, ISerializationCallbackReceiver
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
                return NamesEqual(candidate, characterName);
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        [NonSerialized] private Dictionary<NormalizedNameKey, Entry> overrideNameLookup;
        [NonSerialized] private Dictionary<NormalizedNameKey, Entry> nameLookup;
        [NonSerialized] private Dictionary<NormalizedNameKey, Entry> sourceNameLookup;
        [NonSerialized] private bool lookupCacheDirty = true;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryFindExact(string characterName, out Entry entry)
        {
            return TryFindExact(characterName, out entry, out _);
        }

        public bool TryFindExact(string characterName, out Entry entry, out MatchKind matchKind)
        {
            entry = null;
            matchKind = MatchKind.None;

            if (!NormalizedNameKey.TryCreate(characterName, out NormalizedNameKey lookupKey))
            {
                return false;
            }

            EnsureLookupCache();

            if (overrideNameLookup.TryGetValue(lookupKey, out entry))
            {
                matchKind = MatchKind.OverrideName;
                return true;
            }

            if (nameLookup.TryGetValue(lookupKey, out entry))
            {
                matchKind = MatchKind.Name;
                return true;
            }

            if (sourceNameLookup.TryGetValue(lookupKey, out entry))
            {
                matchKind = MatchKind.SourceName;
                return true;
            }

            return false;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            lookupCacheDirty = true;
        }

        private void OnEnable()
        {
            RebuildLookupCache();
        }

        private void OnValidate()
        {
            RebuildLookupCache();
        }

        private void EnsureLookupCache()
        {
            if (!lookupCacheDirty && overrideNameLookup != null && nameLookup != null && sourceNameLookup != null)
                return;

            RebuildLookupCache();
        }

        private void RebuildLookupCache()
        {
            EnsureLookupDictionaries();
            overrideNameLookup.Clear();
            nameLookup.Clear();
            sourceNameLookup.Clear();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; ++i)
                {
                    Entry entry = entries[i];
                    if (entry == null)
                        continue;

                    AddLookup(overrideNameLookup, entry.OverrideName, entry);
                    AddLookup(nameLookup, entry.Name, entry);
                    AddLookup(sourceNameLookup, entry.SourceName, entry);
                }
            }

            lookupCacheDirty = false;
        }

        private void EnsureLookupDictionaries()
        {
            int capacity = entries != null ? entries.Count : 0;
            if (overrideNameLookup == null)
                overrideNameLookup = new Dictionary<NormalizedNameKey, Entry>(capacity);

            if (nameLookup == null)
                nameLookup = new Dictionary<NormalizedNameKey, Entry>(capacity);

            if (sourceNameLookup == null)
                sourceNameLookup = new Dictionary<NormalizedNameKey, Entry>(capacity);
        }

        private static void AddLookup(Dictionary<NormalizedNameKey, Entry> lookup, string name, Entry entry)
        {
            if (lookup == null || entry == null || !NormalizedNameKey.TryCreate(name, out NormalizedNameKey key))
                return;

            if (!lookup.ContainsKey(key))
                lookup.Add(key, entry);
        }

        private static bool NamesEqual(string left, string right)
        {
            return NormalizedNameKey.TryCreate(left, out NormalizedNameKey leftKey)
                && NormalizedNameKey.TryCreate(right, out NormalizedNameKey rightKey)
                && leftKey.Equals(rightKey);
        }

        private readonly struct NormalizedNameKey : IEquatable<NormalizedNameKey>
        {
            private readonly string value;
            private readonly int start;
            private readonly int length;
            private readonly int hash;

            private NormalizedNameKey(string value, int start, int length)
            {
                this.value = value;
                this.start = start;
                this.length = length;
                hash = ComputeHash(value, start, length);
            }

            public static bool TryCreate(string value, out NormalizedNameKey key)
            {
                key = default;
                if (string.IsNullOrEmpty(value))
                    return false;

                int start = 0;
                int end = value.Length - 1;
                while (start <= end && char.IsWhiteSpace(value[start]))
                    start++;

                while (end >= start && char.IsWhiteSpace(value[end]))
                    end--;

                int length = end - start + 1;
                if (length <= 0)
                    return false;

                key = new NormalizedNameKey(value, start, length);
                return true;
            }

            public bool Equals(NormalizedNameKey other)
            {
                if (length != other.length || hash != other.hash)
                    return false;

                for (int i = 0; i < length; ++i)
                {
                    char left = char.ToUpperInvariant(value[start + i]);
                    char right = char.ToUpperInvariant(other.value[other.start + i]);
                    if (left != right)
                        return false;
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is NormalizedNameKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return hash;
            }

            private static int ComputeHash(string value, int start, int length)
            {
                unchecked
                {
                    int computedHash = 17;
                    for (int i = 0; i < length; ++i)
                        computedHash = (computedHash * 31) + char.ToUpperInvariant(value[start + i]).GetHashCode();

                    return computedHash;
                }
            }
        }
    }
}
