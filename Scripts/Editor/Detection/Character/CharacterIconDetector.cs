#if UNITY_EDITOR
using System;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;

namespace HoyoToon.Editor.Detection.Character
{
    public static class CharacterIconDetector
    {
        private const string EntityCatalogAssetFileName = "GameEntityCatalog.asset";
        private const string EntityCatalogAssetSuffix = "/Config/GameEntityCatalog.asset";

        public static bool TryResolveCharacterIconUrls(
            string gameKey,
            string characterName,
            out string characterId,
            out string avatarIconUrl,
            out string roundIconUrl,
            out string splashIconUrl)
        {
            characterId = null;
            avatarIconUrl = null;
            roundIconUrl = null;
            splashIconUrl = null;

            if (!TryResolveCharacterEntry(gameKey, characterName, out GameEntityCatalogSO.Entry entry)
                || entry == null)
            {
                return false;
            }

            characterId = ResolveCharacterId(entry);
            avatarIconUrl = ResolveAvatarIconUrl(entry);
            roundIconUrl = ResolveRoundIconUrl(entry);
            splashIconUrl = ResolveSplashIconUrl(entry);
            return !string.IsNullOrWhiteSpace(avatarIconUrl)
                || !string.IsNullOrWhiteSpace(roundIconUrl)
                || !string.IsNullOrWhiteSpace(splashIconUrl);
        }

        public static bool TryResolveCharacterIconUrls(
            string characterName,
            out string gameKey,
            out string characterId,
            out string avatarIconUrl,
            out string roundIconUrl,
            out string splashIconUrl)
        {
            gameKey = null;
            characterId = null;
            avatarIconUrl = null;
            roundIconUrl = null;
            splashIconUrl = null;

            if (!TryResolveCharacterEntry(characterName, out gameKey, out GameEntityCatalogSO.Entry entry)
                || entry == null)
            {
                return false;
            }

            characterId = ResolveCharacterId(entry);
            avatarIconUrl = ResolveAvatarIconUrl(entry);
            roundIconUrl = ResolveRoundIconUrl(entry);
            splashIconUrl = ResolveSplashIconUrl(entry);
            return !string.IsNullOrWhiteSpace(avatarIconUrl)
                || !string.IsNullOrWhiteSpace(roundIconUrl)
                || !string.IsNullOrWhiteSpace(splashIconUrl);
        }

        private static bool TryResolveCharacterEntry(string gameKey, string characterName, out GameEntityCatalogSO.Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(gameKey) || string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            GameEntityCatalogSO catalog = LoadEntityCatalog(gameKey);
            return catalog != null && catalog.TryFindCharacterExact(characterName, out entry);
        }

        private static bool TryResolveCharacterEntry(string characterName, out string gameKey, out GameEntityCatalogSO.Entry entry)
        {
            gameKey = null;
            entry = null;

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            foreach (GameEntityCatalogSO catalog in GeneratedAssetQueryUtility.LoadGeneratedAssets<GameEntityCatalogSO>("t:GameEntityCatalogSO", EntityCatalogAssetSuffix))
            {
                if (catalog == null
                    || string.IsNullOrWhiteSpace(catalog.GameKey)
                    || !catalog.TryFindCharacterExact(characterName, out GameEntityCatalogSO.Entry candidate)
                    || candidate == null)
                {
                    continue;
                }

                if (entry != null
                    && !string.Equals(gameKey, catalog.GameKey, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                gameKey = catalog.GameKey;
                entry = candidate;
            }

            return !string.IsNullOrWhiteSpace(gameKey) && entry != null;
        }

        private static GameEntityCatalogSO LoadEntityCatalog(string gameKey)
        {
            string assetPath = $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}/{HoyoToonApi.GeneratedGamesFolderName}/{EntityCatalogAssetFileName}";
            return AssetDatabase.LoadAssetAtPath<GameEntityCatalogSO>(assetPath);
        }

        private static string ResolveCharacterId(GameEntityCatalogSO.Entry entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                return entry.CharacterId;
            }

            return string.IsNullOrWhiteSpace(entry.EntityId) ? null : entry.EntityId;
        }

        private static string ResolveAvatarIconUrl(GameEntityCatalogSO.Entry entry)
        {
            return ImageProxyUrlUtility.ToUnitySupportedPngUrl(entry?.AvatarIcon);
        }

        private static string ResolveRoundIconUrl(GameEntityCatalogSO.Entry entry)
        {
            return ImageProxyUrlUtility.ToUnitySupportedPngUrl(entry?.RoundIcon);
        }

        private static string ResolveSplashIconUrl(GameEntityCatalogSO.Entry entry)
        {
            return ImageProxyUrlUtility.ToUnitySupportedPngUrl(entry?.SplashIcon);
        }
    }
}
#endif
