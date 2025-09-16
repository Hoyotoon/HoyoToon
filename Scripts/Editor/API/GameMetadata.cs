#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HoyoToon.API
{
    /// <summary>
    /// Describes per-game configuration such as game-wide shader properties,
    /// per-shader keywords, and material categories (Face, Hair, etc.).
    /// </summary>
    [Serializable]
    public class GameMetadata
    {
        public string Key { get; set; }

    // Global properties for this Game (e.g., _NyxStateOutlineColorOnBodyOpacity for Genshin)
    public List<string> GameProperties { get; set; } = new List<string>();

    // Shader path => list of keywords specific to that shader in this game
    public Dictionary<string, List<string>> ShaderKeywords { get; set; } = new Dictionary<string, List<string>>();

    // Optional default shader path for this game
    public string DefaultShader { get; set; }

    // Optional friendly property name conversions per-game
    public Dictionary<string, string> PropertyConversions { get; set; } = new Dictionary<string, string>();

    // Tangent generation preferences and skip patterns
    public GameTangents Tangents { get; set; } = new GameTangents();

    // Texture property name -> default texture asset name mapping
    public Dictionary<string, string> TextureMappings { get; set; } = new Dictionary<string, string>();

    // Per-shader property overrides: ShaderPath -> (PropertyName -> numeric value)
    // These are applied after JSON mapping but before texture mappings/import rules.
    public Dictionary<string, Dictionary<string, float>> PropertyOverrides { get; set; } = new Dictionary<string, Dictionary<string, float>>();

    // Import rules grouped by name matching strategies
    public TextureImportSettings TextureImportSettings { get; set; } = new TextureImportSettings();

    // Model (FBX) importer defaults and rules
    public ModelImportSettings ModelImportSettings { get; set; } = new ModelImportSettings();

    // Unified problem configuration:
    // - Texture: Texture to find by contains name (e.g., "Albedo" or "Diffuse")
    // - Regex: a single global regex to extract character name from texture filenames (named group 'name' or first group)
    // - Entries: list of problems with Name (character) and Message (warning)
    public ProblemListConfig ProblemList { get; set; } = new ProblemListConfig();
    }

    [Serializable]
    public class ProblemListConfig
    {
        public string Texture { get; set; }
        public string Regex { get; set; }
        public List<ProblemEntry> Entries { get; set; } = new List<ProblemEntry>();
    }

    [Serializable]
    public class ProblemEntry
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
    }

    [Serializable]
    public class GameTangents
    {
        public List<string> Options { get; set; } = new List<string>();
        public string Status { get; set; }
        public List<string> SkipMeshesContaining { get; set; } = new List<string>();
    }

    [Serializable]
    public class TextureImportSettings
    {
        // Defaults applied first for all textures before specific matches
        public TextureImportRule Defaults { get; set; } = new TextureImportRule();
        // Exact name match rules
        public Dictionary<string, TextureImportRule> NameEquals { get; set; } = new Dictionary<string, TextureImportRule>();
        // Substring match rules
        public Dictionary<string, TextureImportRule> NameContains { get; set; } = new Dictionary<string, TextureImportRule>();
        // Suffix match rules
        public Dictionary<string, TextureImportRule> NameEndsWith { get; set; } = new Dictionary<string, TextureImportRule>();
    }

    [Serializable]
    public class TextureImportRule
    {
        // Nullable flags so we only write what is present
        public bool? SRGBTexture { get; set; }
        public bool? MipmapEnabled { get; set; }
        public string TextureCompression { get; set; } // e.g., CompressedHQ, Uncompressed
        public string NPOTScale { get; set; } // e.g., None
        public string TextureType { get; set; } // e.g., Default
        public string Compression { get; set; } // optional alternative naming

        // Additional options aligned with legacy manager for parity
        public bool? StreamingMipmaps { get; set; }
        public string WrapMode { get; set; } // TextureWrapMode enum name
        public int? MaxTextureSize { get; set; }
        public string FilterMode { get; set; } // FilterMode enum name
    }

    [Serializable]
    public class ModelImportSettings
    {
        // For now we support only a Defaults block to keep things simple and predictable
        public ModelImportRule Defaults { get; set; } = new ModelImportRule();
    }

    /// <summary>
    /// JSON-driven FBX importer defaults; string enums map to Unity's ModelImporter enums.
    /// Nullable fields mean "only apply when present".
    /// </summary>
    [Serializable]
    public class ModelImportRule
    {
        // MODEL TAB
        public float? ScaleFactor { get; set; }
        public bool? UseFileScale { get; set; }
        public bool? ImportBlendShapes { get; set; }
        public bool? ImportVisibility { get; set; }
        public bool? ImportCameras { get; set; }
        public bool? ImportLights { get; set; }
        public bool? IsReadable { get; set; }
        public bool? OptimizeMeshPolygons { get; set; }
        public bool? OptimizeMeshVertices { get; set; }
        public string Normals { get; set; } // ModelImporterNormals
        public string Tangents { get; set; } // ModelImporterTangents

        // RIG TAB
        public string AnimationType { get; set; } // ModelImporterAnimationType
        public string AvatarSetup { get; set; }   // ModelImporterAvatarSetup
        public bool? BakeAxisConversion { get; set; }
        // Optional future: public string SourceAvatarGUID { get; set; }

        // ANIMATION TAB
        public bool? ImportAnimation { get; set; }
        public string AnimationCompression { get; set; } // ModelImporterAnimationCompression
        public bool? ResampleCurves { get; set; }

        // MATERIALS TAB
        public string MaterialImportMode { get; set; }   // ModelImporterMaterialImportMode
        public string MaterialSearch { get; set; }       // ModelImporterMaterialSearch (aka "MaterialSearchMethod")
        public string MaterialName { get; set; }         // ModelImporterMaterialName (aka "MaterialNamingMethod")
        public string MaterialLocation { get; set; }     // ModelImporterMaterialLocation

        // Optional post-import behavior: accept both JSON keys
        private bool? _materialSearchAndRemap;
        [DataMember(Name = "MaterialSearchAndRemap")] // canonical
        public bool? MaterialSearchAndRemap { get => _materialSearchAndRemap; set => _materialSearchAndRemap = value; }
        [DataMember(Name = "MaterialSearch&Remap")] // legacy/alternate
        public bool? MaterialSearchAndRemap_Alt { get => _materialSearchAndRemap; set => _materialSearchAndRemap = value; }  // triggers post-import search & external object remap

        // Extra (non-public) flag: legacy compute normals when mesh has blendshapes
        public bool? LegacyBlendshapeNormals { get; set; }
    }
}
#endif
