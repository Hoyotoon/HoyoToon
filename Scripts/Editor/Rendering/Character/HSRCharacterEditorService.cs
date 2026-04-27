#if UNITY_EDITOR
using HoyoToon.Runtime.Character.HSR;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Rendering.Character
{
    [InitializeOnLoad]
    internal static class HSRCharacterEditorService
    {
        private const string DefaultSkinningComputeShaderAssetPath = "Packages/com.hoyotoon.hoyotoon/Shaders/Utility/ComputeShaders/SkinningUVCoords.compute";

        static HSRCharacterEditorService()
        {
            HSRCharacterEditorHooks.ResolveDefaultComputeShaderHandler = ResolveDefaultComputeShader;
        }

        private static ComputeShader ResolveDefaultComputeShader()
        {
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultSkinningComputeShaderAssetPath);
        }
    }
}
#endif
