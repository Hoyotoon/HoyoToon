#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HoyoToon.Editor.API
{
    // Converter profiles (used by FBX converter selection)
    [Serializable]
    public class ConverterProfile
    {
        public string Key { get; set; }
        public ConverterFeatureConfig Features { get; set; } = new ConverterFeatureConfig();
        public ConverterFeatureConfig Disable { get; set; } = new ConverterFeatureConfig();
        public ConverterListConfig RemoveMeshes { get; set; } = new ConverterListConfig();
        public ConverterListConfig RemoveBones { get; set; } = new ConverterListConfig();
        public ConverterListConfig RenameBones { get; set; } = new ConverterListConfig();
    }

    [Serializable]
    public class ConverterFeatureConfig
    {
        public string Default { get; set; } = string.Empty;
        [DataMember(Name = "DefaultList")]
        public List<string> DefaultList { get; set; } = new List<string>();
        public List<string> Options { get; set; } = new List<string>();
    }

    [Serializable]
    public class ConverterListConfig
    {
        public string List { get; set; } = string.Empty;

        [DataMember(Name = "Mapping")]
        public string Mapping
        {
            get => List;
            set => List = value;
        }
    }
}
#endif
