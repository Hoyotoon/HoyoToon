using System.Collections.Generic;

namespace HoyoToon.Editor.API.Games
{
    public sealed class RawJson
    {
        public string Json;
    }

    public sealed class GameRecordDto
    {
        public GameConfigDto config;
        public ModelImportSettingsDto modelImportSettings;
        public ProblemListDto problemList;
        public List<PropertyConversionDto> propertyConversions;
        public List<PropertyOverrideDto> propertyOverrides;
        public List<ShaderKeywordDto> shaderKeywords;
        public TangentSettingsDto tangentSettings;
        public TextureImportSettingsDto textureImportSettings;
        public List<TextureMappingDto> textureMappings;
    }

    public sealed class GameConfigDto
    {
        public string key;
        public string defaultShader;
        public List<string> gameProperties;
    }

    public sealed class EntityCatalogRecordDto
    {
        public string gameKey;
        public string version;
        public string entityKind;
        public string entityId;
        public string characterId;
        public string monsterId;
        public string weaponId;
        public string displayName;
        public string sourceName;
        public string variantName;
        public string internalName;
        public string weaponType;
        public int rarity;
        public List<string> artNames;
        public List<EntityCatalogArtNameMappingDto> artNameMappings;
        public string primaryArtName;
        public string avatarIcon;
        public string roundIcon;
        public string splashIcon;
        public RawJson displayImage;
        public RawJson icons;
        public RawJson iconPaths;
        public RawJson mediaRefs;
        public RawJson assetRefs;
        public List<string> aliases;
        public bool? available;

        public string GameKey => gameKey;

        public string EntityKind => entityKind;

        public string EntityId => entityId;

        public string DisplayName => displayName;

        public string SourceName => sourceName;

        public string VariantName => variantName;

        public string InternalName => internalName;

        public string PrimaryArtName => primaryArtName;
    }

    public sealed class EntityCatalogArtNameMappingDto
    {
        public string fileName;
        public string finalName;
    }

    public sealed class EntityCatalogPageDto
    {
        public List<EntityCatalogRecordDto> items;
        public string continueCursor;
        public bool isDone;
    }

    public sealed class ModelImportSettingsDto
    {
        public string gameKey;
        public Dictionary<string, object> defaults;
    }

    public sealed class ProblemListDto
    {
        public string gameKey;
        public string regex;
        public List<ProblemEntryDto> entries;
    }

    public sealed class ProblemEntryDto
    {
        public string Name;
        public string Message;
        public string Type;
    }

    public sealed class PropertyConversionDto
    {
        public string gameKey;
        public string sourceProperty;
        public string targetProperty;
    }

    public sealed class PropertyOverrideDto
    {
        public string gameKey;
        public string shaderPath;
        public RawJson overrides;
    }

    public sealed class ShaderKeywordDto
    {
        public string gameKey;
        public string shaderPath;
        public List<string> keywords;
    }

    public sealed class TangentSettingsDto
    {
        public string gameKey;
        public List<string> options;
        public string status;
        public List<string> skipMeshesContaining;
    }

    public sealed class TextureImportSettingsDto
    {
        public string gameKey;
        public Dictionary<string, object> defaults;
        public Dictionary<string, object> nameEquals;
        public Dictionary<string, object> nameContains;
        public Dictionary<string, object> nameEndsWith;
    }

    public sealed class TextureMappingDto
    {
        public string gameKey;
        public string propertyName;
        public string textureName;
    }
}
