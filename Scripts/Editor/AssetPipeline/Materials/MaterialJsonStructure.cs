#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;


namespace HoyoToon.Editor.AssetPipeline.Materials
{
    public class MaterialJsonStructure
    {
        // Unity Material Structure
        public ShaderInfo m_Shader { get; set; }
        public SavedProperties m_SavedProperties { get; set; }
        public object m_ShaderKeywords { get; set; }
        public object m_ValidKeywords { get; set; }
        public object m_InvalidKeywords { get; set; }
        public int? m_LightmapFlags { get; set; }
        public object m_EnableInstancingVariants { get; set; }
        public int? m_CustomRenderQueue { get; set; }
        public List<string> m_DisabledShaderPasses { get; set; }
        public Dictionary<string, string> m_StringTagMap { get; set; }

        private string _materialName;

        // Canonical Unity key. If both Name variants are provided, m_Name takes precedence.
        public string m_Name
        {
            get => _materialName;
            set
            {
                if (!string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(_materialName))
                {
                    _materialName = value;
                }
            }
        }

        // Compatibility alias used by some payloads.
        public string Name
        {
            get => _materialName;
            set
            {
                if (string.IsNullOrWhiteSpace(_materialName))
                {
                    _materialName = value;
                }
            }
        }

        public bool TryGetEnableInstancing(out bool enabled)
        {
            enabled = false;
            var raw = m_EnableInstancingVariants;

            return MaterialValueParsingUtil.TryParseTruthy(raw, out enabled);
        }

        #region Unity Structures
        public class ShaderInfo
        {
            public long m_FileID { get; set; }
            public long m_PathID { get; set; }
            public string Name { get; set; }
            public bool IsNull { get; set; }
        }

        public class SavedProperties
        {
            public Dictionary<string, TexturePropertyInfo> m_TexEnvs { get; set; }
            public Dictionary<string, float> m_Floats { get; set; }
            public Dictionary<string, ColorInfo> m_Colors { get; set; }
            public Dictionary<string, int> m_Ints { get; set; }
        }

        public class TexturePropertyInfo
        {
            public TextureInfo m_Texture { get; set; }
            public Vector2Info m_Scale { get; set; }
            public Vector2Info m_Offset { get; set; }
        }

        public class TextureInfo
        {
            public long m_FileID { get; set; }
            public long m_PathID { get; set; }
            public string Name { get; set; }
            public bool IsNull { get; set; }
        }
        #endregion

        #region Shared Structures
        public class Vector2Info
        {
            public float X { get; set; }
            public float Y { get; set; }

            public Vector2 ToVector2() => new Vector2(X, Y);
        }

        public class ColorInfo
        {
            public float r { get; set; }
            public float g { get; set; }
            public float b { get; set; }
            public float a { get; set; }
            public string Hex { get; set; }

            public Color ToColor()
            {
                if (!string.IsNullOrEmpty(Hex) && ColorUtility.TryParseHtmlString("#" + Hex.TrimStart('#'), out Color hexColor))
                {
                    return hexColor;
                }
                return new Color(r, g, b, a);
            }
        }
        #endregion
    }
}
#endif
