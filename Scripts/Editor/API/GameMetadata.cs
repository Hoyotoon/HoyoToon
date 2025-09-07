#if UNITY_EDITOR
using System;
using System.Collections.Generic;

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

    // Import rules grouped by name matching strategies
    public TextureImportSettings TextureImportSettings { get; set; } = new TextureImportSettings();

        // Categories like Face, Hair, Eye, etc. (optional)
        public List<string> MaterialCategories { get; set; } = new List<string>();
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
    }
}
#endif
