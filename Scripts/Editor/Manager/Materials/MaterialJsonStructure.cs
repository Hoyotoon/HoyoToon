#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;

namespace HoyoToon
{
    /// <summary>
    /// Lightweight DTO for parsing material JSONs (Unity and Unreal variants).
    /// </summary>
    public class MaterialJsonStructure
    {
        // Unity Material Structure
        public ShaderInfo m_Shader { get; set; }
        public SavedProperties m_SavedProperties { get; set; }

        // Unreal Material Structure
        public Dictionary<string, string> Textures { get; set; }
        public UnrealParameters Parameters { get; set; }

        public bool IsUnityFormat => m_SavedProperties != null;
        public bool IsUnrealFormat => Parameters != null && Textures != null;

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
                if (!string.IsNullOrEmpty(Hex) && ColorUtility.TryParseHtmlString("#" + Hex, out Color hexColor))
                {
                    return hexColor;
                }
                return new Color(r, g, b, a);
            }
        }
        #endregion

        #region Unreal Structures
        public class UnrealParameters
        {
            public Dictionary<string, ColorInfo> Colors { get; set; }
            public Dictionary<string, float> Scalars { get; set; }
            public Dictionary<string, bool> Switches { get; set; }
            public Dictionary<string, object> Properties { get; set; }
            public int BlendMode { get; set; }
            public int ShadingModel { get; set; }
            public int RenderQueue { get; set; }
        }
        #endregion
    }
}
#endif
