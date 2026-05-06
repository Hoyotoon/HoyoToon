#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Assets
{
    [InitializeOnLoad]
    internal static class EditorGeneratedTextureCache
    {
        private static readonly Dictionary<Color32, Texture2D> s_SolidTexturesByColor =
            new Dictionary<Color32, Texture2D>();

        static EditorGeneratedTextureCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAllGenerated;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAllGenerated;
            EditorApplication.quitting -= ReleaseAllGenerated;
            EditorApplication.quitting += ReleaseAllGenerated;
        }

        internal static Texture2D GetSolidTexture(Color color)
        {
            Color32 key = color;
            if (s_SolidTexturesByColor.TryGetValue(key, out Texture2D texture) && texture != null)
                return texture;

            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = $"HoyoToon Generated Solid {key.r:X2}{key.g:X2}{key.b:X2}{key.a:X2}"
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            s_SolidTexturesByColor[key] = texture;
            return texture;
        }

        internal static void Release(Texture2D texture)
        {
            if (texture == null)
                return;

            Color32? removedKey = null;
            foreach (KeyValuePair<Color32, Texture2D> pair in s_SolidTexturesByColor)
            {
                if (pair.Value == texture)
                {
                    removedKey = pair.Key;
                    break;
                }
            }

            if (removedKey.HasValue)
                s_SolidTexturesByColor.Remove(removedKey.Value);

            Object.DestroyImmediate(texture);
        }

        internal static void ReleaseAllGenerated()
        {
            foreach (Texture2D texture in s_SolidTexturesByColor.Values)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }

            s_SolidTexturesByColor.Clear();
        }
    }
}
#endif
