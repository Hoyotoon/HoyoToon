using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.ScriptableObjects.Games
{
    [CreateAssetMenu(fileName = "GameEntityCatalog", menuName = "HoyoToon/GameEntityCatalog")]
    public class GameEntityCatalogSO : GameScopedScriptableObject, ISerializationCallbackReceiver
    {
        public enum EntityKind
        {
            Character,
            Monster,
            Weapon,
        }

        public enum MatchKind
        {
            None,
            ArtName,
            PrimaryArtName,
            Alias,
            InternalName,
            DisplayName,
            SourceName,
            VariantName,
            EntityId,
        }

        [Serializable]
        public class ArtNameMapping
        {
            [SerializeField] private string fileName;
            [SerializeField] private string finalName;

            public string FileName => fileName;

            public string FinalName => finalName;
        }

        [Serializable]
        public class Entry
        {
            [SerializeField] private EntityKind entityKind;
            [SerializeField] private string entityId;
            [SerializeField] private string characterId;
            [SerializeField] private string monsterId;
            [SerializeField] private string weaponId;
            [SerializeField] private string displayName;
            [SerializeField] private string sourceName;
            [SerializeField] private string variantName;
            [SerializeField] private string internalName;
            [SerializeField] private string weaponType;
            [SerializeField] private int rarity;
            [SerializeField] private string primaryArtName;
            [SerializeField] private List<string> artNames = new List<string>();
            [SerializeField] private List<ArtNameMapping> artNameMappings = new List<ArtNameMapping>();
            [SerializeField] private List<string> aliases = new List<string>();
            [SerializeField] private string avatarIcon;
            [SerializeField] private string roundIcon;
            [SerializeField] private string splashIcon;
            [SerializeField] private string displayImageJson;
            [SerializeField] private string iconsJson;
            [SerializeField] private string iconPathsJson;
            [SerializeField] private string mediaRefsJson;
            [SerializeField] private string assetRefsJson;
            [SerializeField] private bool available = true;

            public EntityKind Kind => entityKind;

            public string EntityId => entityId;

            public string CharacterId => characterId;

            public string MonsterId => monsterId;

            public string WeaponId => weaponId;

            public string DisplayName => displayName;

            public string SourceName => sourceName;

            public string VariantName => variantName;

            public string InternalName => internalName;

            public string WeaponType => weaponType;

            public int Rarity => rarity;

            public string PrimaryArtName => primaryArtName;

            public IReadOnlyList<string> ArtNames => artNames;

            public IReadOnlyList<ArtNameMapping> ArtNameMappings => artNameMappings;

            public IReadOnlyList<string> Aliases => aliases;

            public string AvatarIcon => avatarIcon;

            public string RoundIcon => roundIcon;

            public string SplashIcon => splashIcon;

            public string DisplayImageJson => displayImageJson;

            public string IconsJson => iconsJson;

            public string IconPathsJson => iconPathsJson;

            public string MediaRefsJson => mediaRefsJson;

            public string AssetRefsJson => assetRefsJson;

            public bool Available => available;
        }

        [SerializeField] private string version;
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [NonSerialized] private Dictionary<LookupKey, LookupMatch> lookup;
        [NonSerialized] private bool lookupCacheDirty = true;

        public string Version => version;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryFindCharacterExact(string name, out Entry entry)
        {
            return TryFindExact(EntityKind.Character, name, out entry, out _);
        }

        public bool TryFindCharacterExact(string name, out Entry entry, out MatchKind matchKind)
        {
            return TryFindExact(EntityKind.Character, name, out entry, out matchKind);
        }

        public bool TryFindExact(EntityKind entityKind, string name, out Entry entry, out MatchKind matchKind)
        {
            entry = null;
            matchKind = MatchKind.None;

            if (!NormalizedNameKey.TryCreate(name, out NormalizedNameKey nameKey))
            {
                return false;
            }

            EnsureLookupCache();
            if (!lookup.TryGetValue(new LookupKey(entityKind, nameKey), out LookupMatch match))
            {
                return false;
            }

            entry = match.Entry;
            matchKind = match.MatchKind;
            return entry != null;
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
            if (!lookupCacheDirty && lookup != null)
            {
                return;
            }

            RebuildLookupCache();
        }

        private void RebuildLookupCache()
        {
            lookup ??= new Dictionary<LookupKey, LookupMatch>(entries?.Count ?? 0);
            lookup.Clear();

            foreach (Entry entry in entries ?? new List<Entry>())
            {
                if (entry == null)
                {
                    continue;
                }

                AddLookup(entry.Kind, entry.PrimaryArtName, entry, MatchKind.PrimaryArtName);

                foreach (ArtNameMapping mapping in entry.ArtNameMappings ?? Array.Empty<ArtNameMapping>())
                {
                    AddLookup(entry.Kind, mapping?.FileName, entry, MatchKind.ArtName);
                    AddLookup(entry.Kind, mapping?.FinalName, entry, MatchKind.ArtName);
                }

                foreach (string artName in entry.ArtNames ?? Array.Empty<string>())
                {
                    AddLookup(entry.Kind, artName, entry, MatchKind.ArtName);
                }

                foreach (string alias in entry.Aliases ?? Array.Empty<string>())
                {
                    AddLookup(entry.Kind, alias, entry, MatchKind.Alias);
                }

                AddLookup(entry.Kind, entry.InternalName, entry, MatchKind.InternalName);
                AddLookup(entry.Kind, entry.DisplayName, entry, MatchKind.DisplayName);
                AddLookup(entry.Kind, entry.SourceName, entry, MatchKind.SourceName);
                AddLookup(entry.Kind, entry.VariantName, entry, MatchKind.VariantName);
                AddLookup(entry.Kind, entry.EntityId, entry, MatchKind.EntityId);
                AddLookup(entry.Kind, entry.CharacterId, entry, MatchKind.EntityId);
                AddLookup(entry.Kind, entry.MonsterId, entry, MatchKind.EntityId);
                AddLookup(entry.Kind, entry.WeaponId, entry, MatchKind.EntityId);
            }

            lookupCacheDirty = false;
        }

        private void AddLookup(EntityKind entityKind, string name, Entry entry, MatchKind matchKind)
        {
            if (entry == null || !NormalizedNameKey.TryCreate(name, out NormalizedNameKey nameKey))
            {
                return;
            }

            var key = new LookupKey(entityKind, nameKey);
            if (!lookup.ContainsKey(key))
            {
                lookup.Add(key, new LookupMatch(entry, matchKind));
            }
        }

        private readonly struct LookupMatch
        {
            public LookupMatch(Entry entry, MatchKind matchKind)
            {
                Entry = entry;
                MatchKind = matchKind;
            }

            public Entry Entry { get; }

            public MatchKind MatchKind { get; }
        }

        private readonly struct LookupKey : IEquatable<LookupKey>
        {
            private readonly EntityKind entityKind;
            private readonly NormalizedNameKey nameKey;

            public LookupKey(EntityKind entityKind, NormalizedNameKey nameKey)
            {
                this.entityKind = entityKind;
                this.nameKey = nameKey;
            }

            public bool Equals(LookupKey other)
            {
                return entityKind == other.entityKind && nameKey.Equals(other.nameKey);
            }

            public override bool Equals(object obj)
            {
                return obj is LookupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)entityKind * 397) ^ nameKey.GetHashCode();
                }
            }
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
                {
                    return false;
                }

                int start = 0;
                int end = value.Length - 1;
                while (start <= end && char.IsWhiteSpace(value[start]))
                {
                    start++;
                }

                while (end >= start && char.IsWhiteSpace(value[end]))
                {
                    end--;
                }

                int length = end - start + 1;
                if (length <= 0)
                {
                    return false;
                }

                key = new NormalizedNameKey(value, start, length);
                return true;
            }

            public bool Equals(NormalizedNameKey other)
            {
                if (length != other.length || hash != other.hash)
                {
                    return false;
                }

                for (int i = 0; i < length; ++i)
                {
                    char left = char.ToUpperInvariant(value[start + i]);
                    char right = char.ToUpperInvariant(other.value[other.start + i]);
                    if (left != right)
                    {
                        return false;
                    }
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
                    {
                        computedHash = (computedHash * 31) + char.ToUpperInvariant(value[start + i]).GetHashCode();
                    }

                    return computedHash;
                }
            }
        }
    }
}
