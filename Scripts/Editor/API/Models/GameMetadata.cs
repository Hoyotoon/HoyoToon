#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.API
{
    [Serializable]
    public class GameMetadata
    {
        public string Key { get; set; }

        public List<string> GameProperties { get; set; } = new List<string>();
        public Dictionary<string, List<string>> ShaderKeywords { get; set; } = new Dictionary<string, List<string>>();
        public string DefaultShader { get; set; }
        public Dictionary<string, string> PropertyConversions { get; set; } = new Dictionary<string, string>();
        public GameTangents Tangents { get; set; } = new GameTangents();
        public Dictionary<string, string> TextureMappings { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, Dictionary<string, float>> PropertyOverrides { get; set; } = new Dictionary<string, Dictionary<string, float>>();
        public TextureImportSettings TextureImportSettings { get; set; } = new TextureImportSettings();
        public ModelImportSettings ModelImportSettings { get; set; } = new ModelImportSettings();
        public ProblemListConfig ProblemList { get; set; } = new ProblemListConfig();
        public ConverterProfile Hoyo2Unity { get; set; }
        public ConverterProfile Hoyo2VRC { get; set; }
        public List<BoneConstraintRule> BoneConstraints { get; set; } = new List<BoneConstraintRule>();
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
}
#endif
