#if UNITY_EDITOR
using System;
using System.Globalization;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;

namespace HoyoToon.Editor.Detection.Character
{
    public static class CharacterIconDetector
    {
        public enum CharacterIconResolutionStatus
        {
            Success,
            CharacterNameNotDetected,
            CharacterIdsAssetMissing,
            CharacterEntryNotFound,
            IconUrlsMissing,
            UnexpectedError,
        }

        public readonly struct CharacterIconResolutionResult
        {
            public CharacterIconResolutionResult(
                CharacterIconResolutionStatus status,
                string gameKey,
                string contextAssetPath,
                string characterName,
                string characterId,
                GameCharacterIdsSO.MatchKind matchKind,
                string avatarIconUrl,
                string roundIconUrl,
                string splashIconUrl,
                string preferredIconUrl,
                string cachedIconPath)
            {
                Status = status;
                GameKey = gameKey;
                ContextAssetPath = contextAssetPath;
                CharacterName = characterName;
                CharacterId = characterId;
                MatchKind = matchKind;
                AvatarIconUrl = avatarIconUrl;
                RoundIconUrl = roundIconUrl;
                SplashIconUrl = splashIconUrl;
                PreferredIconUrl = preferredIconUrl;
                CachedIconPath = cachedIconPath;
            }

            public CharacterIconResolutionStatus Status { get; }

            public string GameKey { get; }

            public string ContextAssetPath { get; }

            public string CharacterName { get; }

            public string CharacterId { get; }

            public GameCharacterIdsSO.MatchKind MatchKind { get; }

            public string AvatarIconUrl { get; }

            public string RoundIconUrl { get; }

            public string SplashIconUrl { get; }

            public string PreferredIconUrl { get; }

            public string CachedIconPath { get; }

            public bool Succeeded => Status == CharacterIconResolutionStatus.Success;
        }

        private const string CharacterIdsAssetFileName = "GameCharacterIds.asset";
        private const string CharacterIdsAssetSuffix = "/Config/GameCharacterIds.asset";

        public static string TryGetCharacterIconUrl(string characterName)
        {
            return TryGetCharacterIconUrl(characterName, out string iconUrl)
                ? iconUrl
                : null;
        }

        public static bool TryGetCharacterIconUrl(string characterName, out string iconUrl)
        {
            return TryGetCharacterIconUrl(characterName, out iconUrl, out _);
        }

        public static bool TryGetCharacterIconUrl(string characterName, out string iconUrl, out string cachedIconPath)
        {
            iconUrl = null;
            cachedIconPath = null;

            if (!TryResolveCharacterEntry(characterName, out string gameKey, out GameCharacterIdsSO.Entry entry)
                || entry == null)
            {
                return false;
            }

            iconUrl = ResolvePreferredIconUrl(entry);
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                return false;
            }

            characterName = characterName?.Trim();
            return true;
        }

        public static bool TryGetCharacterIconUrl(string contextAssetPath, string characterName, out string iconUrl, out string cachedIconPath)
        {
            iconUrl = null;
            cachedIconPath = null;

            if (!GameDetector.TryDetectGameFromAssetContext(contextAssetPath, out GameConfigSO game, out _)
                || game == null)
            {
                return false;
            }

            if (!TryResolveCharacterEntry(game.Key, characterName, out _, out GameCharacterIdsSO.Entry entry, out _)
                || entry == null)
            {
                return false;
            }

            iconUrl = ResolvePreferredIconUrl(entry);
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                iconUrl = null;
                return false;
            }

            CharacterIconCacheUtility.TryEnsureCharacterIconsCached(
                contextAssetPath,
                game.Key,
                entry.CharacterId,
                entry.AvatarIcon,
                entry.RoundIcon,
                entry.SplashIcon,
                out cachedIconPath);
            return true;
        }

        public static string TryDetectCharacterIconUrl(string gameKey, string contextAssetPath)
        {
            return TryDetectCharacterIconUrl(gameKey, contextAssetPath, out _, out string iconUrl)
                ? iconUrl
                : null;
        }

        public static bool TryDetectCharacterIconUrl(
            string gameKey,
            string contextAssetPath,
            out string characterName,
            out string iconUrl)
        {
            return TryDetectCharacterIconUrl(gameKey, contextAssetPath, out characterName, out iconUrl, out _);
        }

        public static bool TryDetectCharacterIconUrl(
            string gameKey,
            string contextAssetPath,
            out string characterName,
            out string iconUrl,
            out string cachedIconPath)
        {
            return TryDetectCharacterIconUrl(gameKey, contextAssetPath, out characterName, out _, out iconUrl, out cachedIconPath);
        }

        public static bool TryDetectCharacterIconUrl(
            string gameKey,
            string contextAssetPath,
            out string characterName,
            out string characterId,
            out string iconUrl,
            out string cachedIconPath)
        {
            characterName = null;
            characterId = null;
            iconUrl = null;
            cachedIconPath = null;

            try
            {
                CharacterIconResolutionResult result = ResolveCharacterIcon(gameKey, contextAssetPath);
                characterName = result.CharacterName;
                characterId = result.CharacterId;
                iconUrl = result.PreferredIconUrl;
                cachedIconPath = result.CachedIconPath;
                return result.Succeeded;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Detection,
                    $"Character icon detection failed for '{contextAssetPath}' in game '{gameKey}'. Continuing without a detected character icon.",
                    exception);
                return false;
            }
        }

        public static bool TryGetCachedCharacterIconPath(string contextAssetPath, string characterName, out string cachedIconPath)
        {
            cachedIconPath = null;

            if (!GameDetector.TryDetectGameFromAssetContext(contextAssetPath, out GameConfigSO game, out _)
                || game == null)
            {
                return false;
            }

            if (!TryResolveCharacterEntry(game.Key, characterName, out GameCharacterIdsSO.Entry entry)
                || entry == null)
            {
                return false;
            }

            string iconUrl = ResolvePreferredIconUrl(entry);
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                return false;
            }

            return CharacterIconCacheUtility.TryGetCachedCharacterIconPath(
                contextAssetPath,
                entry.RoundIcon,
                entry.AvatarIcon,
                entry.SplashIcon,
                out cachedIconPath);
        }

        public static bool TryGetCharacterIconUrl(
            string gameKey,
            string characterName,
            out string characterId,
            out string iconUrl,
            out string cachedIconPath)
        {
            characterId = null;
            iconUrl = null;
            cachedIconPath = null;

            if (!TryResolveCharacterEntry(gameKey, characterName, out _, out GameCharacterIdsSO.Entry entry, out _)
                || entry == null)
            {
                return false;
            }

            iconUrl = ResolvePreferredIconUrl(entry);
            if (string.IsNullOrWhiteSpace(iconUrl))
            {
                return false;
            }

            characterId = entry.CharacterId.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public static CharacterIconResolutionResult ResolveCharacterIcon(string gameKey, string contextAssetPath)
        {
            try
            {
                string characterContextAssetPath = ResolveCharacterNameContextAssetPath(gameKey, contextAssetPath);
                string characterName = CharacterNameDetector.TryExtractCharacterName(gameKey, characterContextAssetPath);
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    return new CharacterIconResolutionResult(
                        CharacterIconResolutionStatus.CharacterNameNotDetected,
                        gameKey,
                        contextAssetPath,
                        null,
                        null,
                        GameCharacterIdsSO.MatchKind.None,
                        null,
                        null,
                        null,
                        null,
                        null);
                }

                if (!TryResolveCharacterEntry(gameKey, characterName, out GameCharacterIdsSO characterIds, out GameCharacterIdsSO.Entry entry, out GameCharacterIdsSO.MatchKind matchKind))
                {
                    CharacterIconResolutionStatus status = characterIds == null
                        ? CharacterIconResolutionStatus.CharacterIdsAssetMissing
                        : CharacterIconResolutionStatus.CharacterEntryNotFound;

                    return new CharacterIconResolutionResult(
                        status,
                        gameKey,
                        contextAssetPath,
                        characterName,
                        null,
                        matchKind,
                        null,
                        null,
                        null,
                        null,
                        null);
                }

                string avatarIconUrl = entry.AvatarIcon;
                string roundIconUrl = entry.RoundIcon;
                string splashIconUrl = entry.SplashIcon;
                string preferredIconUrl = ResolvePreferredIconUrl(entry);
                string characterId = entry.CharacterId > 0
                    ? entry.CharacterId.ToString(CultureInfo.InvariantCulture)
                    : null;

                if (string.IsNullOrWhiteSpace(preferredIconUrl))
                {
                    return new CharacterIconResolutionResult(
                        CharacterIconResolutionStatus.IconUrlsMissing,
                        gameKey,
                        contextAssetPath,
                        characterName,
                        characterId,
                        matchKind,
                        avatarIconUrl,
                        roundIconUrl,
                        splashIconUrl,
                        null,
                        null);
                }

                CharacterIconCacheUtility.TryEnsureCharacterIconsCached(
                    contextAssetPath,
                    gameKey,
                    entry.CharacterId,
                    avatarIconUrl,
                    roundIconUrl,
                    splashIconUrl,
                    out string cachedIconPath);

                return new CharacterIconResolutionResult(
                    CharacterIconResolutionStatus.Success,
                    gameKey,
                    contextAssetPath,
                    characterName,
                    characterId,
                    matchKind,
                    avatarIconUrl,
                    roundIconUrl,
                    splashIconUrl,
                    preferredIconUrl,
                    cachedIconPath);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Detection,
                    $"Character icon detection failed for '{contextAssetPath}' in game '{gameKey}'. Continuing without a detected character icon.",
                    exception);

                return new CharacterIconResolutionResult(
                    CharacterIconResolutionStatus.UnexpectedError,
                    gameKey,
                    contextAssetPath,
                    null,
                    null,
                    GameCharacterIdsSO.MatchKind.None,
                    null,
                    null,
                    null,
                    null,
                    null);
            }
        }

        private static bool TryResolveCharacterEntry(
            string gameKey,
            string characterName,
            out GameCharacterIdsSO characterIds,
            out GameCharacterIdsSO.Entry entry,
            out GameCharacterIdsSO.MatchKind matchKind)
        {
            characterIds = null;
            entry = null;
            matchKind = GameCharacterIdsSO.MatchKind.None;

            if (string.IsNullOrWhiteSpace(gameKey) || string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            characterIds = LoadCharacterIds(gameKey);
            return characterIds != null && characterIds.TryFindExact(characterName, out entry, out matchKind);
        }

        private static bool TryResolveCharacterEntry(string gameKey, string characterName, out GameCharacterIdsSO.Entry entry)
        {
            return TryResolveCharacterEntry(gameKey, characterName, out _, out entry, out _);
        }

        private static bool TryResolveCharacterEntry(string characterName, out string gameKey, out GameCharacterIdsSO.Entry entry)
        {
            gameKey = null;
            entry = null;

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            foreach (GameCharacterIdsSO characterIds in GeneratedAssetQueryUtility.LoadGeneratedAssets<GameCharacterIdsSO>("t:GameCharacterIdsSO", CharacterIdsAssetSuffix))
            {
                if (characterIds == null
                    || string.IsNullOrWhiteSpace(characterIds.GameKey)
                    || !characterIds.TryFindExact(characterName, out GameCharacterIdsSO.Entry candidate)
                    || candidate == null)
                {
                    continue;
                }

                if (entry != null
                    && !string.Equals(gameKey, characterIds.GameKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                gameKey = characterIds.GameKey;
                entry = candidate;
            }

            return !string.IsNullOrWhiteSpace(gameKey) && entry != null;
        }

        private static GameCharacterIdsSO LoadCharacterIds(string gameKey)
        {
            string assetPath = $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}/{HoyoToonApi.GeneratedGamesFolderName}/{CharacterIdsAssetFileName}";
            return AssetDatabase.LoadAssetAtPath<GameCharacterIdsSO>(assetPath);
        }

        private static string ResolveCharacterNameContextAssetPath(string gameKey, string contextAssetPath)
        {
            if (string.IsNullOrWhiteSpace(contextAssetPath))
            {
                return contextAssetPath;
            }

            if (!GameDetector.TryDetectGameFromAssetContext(contextAssetPath, out GameConfigSO detectedGame, out string matchedJsonAssetPath)
                || detectedGame == null
                || !string.Equals(detectedGame.Key, gameKey, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(matchedJsonAssetPath))
            {
                return contextAssetPath;
            }

            return matchedJsonAssetPath;
        }

        private static string ResolvePreferredIconUrl(GameCharacterIdsSO.Entry entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(entry.RoundIcon))
            {
                return entry.RoundIcon;
            }

            if (!string.IsNullOrWhiteSpace(entry.AvatarIcon))
            {
                return entry.AvatarIcon;
            }

            return string.IsNullOrWhiteSpace(entry.SplashIcon)
                ? null
                : entry.SplashIcon;
        }
    }
}
#endif