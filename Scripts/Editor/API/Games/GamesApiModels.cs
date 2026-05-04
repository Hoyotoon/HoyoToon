using System.Collections.Generic;

namespace HoyoToon.Editor.API.Games
{
    public sealed class RawJson
    {
        public string Json;
    }

    public sealed class GameRecordDto
    {
        public List<BoneConstraintDto> boneConstraints;
        public GameConfigDto config;
        public List<ConverterConfigDto> converterConfigs;
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

    public sealed class CharacterIdRecordDto
    {
        public string gameKey;
        public int characterId;
        public string name;
        public string sourceName;
        public string overrideName;
        public string avatarIcon;
        public string roundIcon;
        public string splashIcon;

        public string GameKey
        {
            get => gameKey;
            set => gameKey = value;
        }

        public int CharacterId
        {
            get => characterId;
            set => characterId = value;
        }

        public string Name
        {
            get => name;
            set => name = value;
        }

        public string SourceName
        {
            get => sourceName;
            set => sourceName = value;
        }

        public string OverrideName
        {
            get => overrideName;
            set => overrideName = value;
        }

        public string AvatarIcon
        {
            get => avatarIcon;
            set => avatarIcon = value;
        }

        public string RoundIcon
        {
            get => roundIcon;
            set => roundIcon = value;
        }

        public string SplashIcon
        {
            get => splashIcon;
            set => splashIcon = value;
        }
    }

    public sealed class BoneConstraintDto
    {
        public string gameKey;
        public string TargetBone;
        public string SourceBone;
        public string ConstraintType;
        public float Weight = 1f;
        public float SourceWeight = 1f;
        public AxesDto PositionAxes;
        public AxesDto RotationAxes;
        public bool MaintainOffset;
        public bool? Active;
        public bool Locked;
    }

    public sealed class AxesDto
    {
        public bool X = true;
        public bool Y = true;
        public bool Z = true;
    }

    public sealed class ConverterConfigDto
    {
        public string gameKey;
        public string converterType;
        public string key;
        public ConverterSectionDto Features;
        public ConverterSectionDto Disable;
        public ConverterMappingDto RemoveMeshes;
        public ConverterMappingDto RemoveBones;
        public ConverterMappingDto RenameBones;
    }

    public sealed class ConverterSectionDto
    {
        public string Default;
        public List<string> DefaultList;
        public List<string> Options;
    }

    public sealed class ConverterMappingDto
    {
        public string List;
        public string Mapping;
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