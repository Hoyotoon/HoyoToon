#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HoyoToon.Editor.API
{
    [Serializable]
    public class TextureImportSettings
    {
        public TextureImportRule Defaults { get; set; } = new TextureImportRule();
        public Dictionary<string, TextureImportRule> NameEquals { get; set; } = new Dictionary<string, TextureImportRule>();
        public Dictionary<string, TextureImportRule> NameContains { get; set; } = new Dictionary<string, TextureImportRule>();
        public Dictionary<string, TextureImportRule> NameEndsWith { get; set; } = new Dictionary<string, TextureImportRule>();
    }

    [Serializable]
    public class TextureImportRule
    {
        public bool? SRGBTexture { get; set; }
        public bool? MipmapEnabled { get; set; }
        [DataMember(Name = "TextureCompression")]
        public string TextureCompression { get; set; }
        public string NPOTScale { get; set; }
        public string TextureType { get; set; }

        public bool? StreamingMipmaps { get; set; }
        public string WrapMode { get; set; }
        public int? MaxTextureSize { get; set; }
        public string FilterMode { get; set; }

        // Optional per-channel swizzle overrides (e.g. R, G, B, A, One, Zero).
        [DataMember(Name = "SwizzleR")]
        public string SwizzleR { get; set; }
        [DataMember(Name = "SwizzleG")]
        public string SwizzleG { get; set; }
        [DataMember(Name = "SwizzleB")]
        public string SwizzleB { get; set; }
        [DataMember(Name = "SwizzleA")]
        public string SwizzleA { get; set; }
    }

    [Serializable]
    public class ModelImportSettings
    {
        public ModelImportRule Defaults { get; set; } = new ModelImportRule();
    }

    [Serializable]
    public class ModelImportRule
    {
        public float? ScaleFactor { get; set; }
        public bool? UseFileScale { get; set; }
        public bool? ImportBlendShapes { get; set; }
        public bool? ImportVisibility { get; set; }
        public bool? ImportCameras { get; set; }
        public bool? ImportLights { get; set; }
        public bool? IsReadable { get; set; }
        public bool? OptimizeMeshPolygons { get; set; }
        public bool? OptimizeMeshVertices { get; set; }
        public string Normals { get; set; }
        public string Tangents { get; set; }

        public string AnimationType { get; set; }
        public string AvatarSetup { get; set; }
        public bool? BakeAxisConversion { get; set; }

        public bool? ImportAnimation { get; set; }
        public string AnimationCompression { get; set; }
        public bool? ResampleCurves { get; set; }

        public string MaterialImportMode { get; set; }
        public string MaterialSearch { get; set; }
        public string MaterialName { get; set; }
        public string MaterialLocation { get; set; }
        [DataMember(Name = "MaterialSearchAndRemap")]
        public bool? MaterialSearchAndRemap { get; set; }

        public bool? LegacyBlendshapeNormals { get; set; }
    }
}
#endif
