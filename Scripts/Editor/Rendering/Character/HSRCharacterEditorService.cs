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

        internal static bool TryAssignDefaultComputeShader(HSRCharacterController controller)
        {
            if (controller == null || controller.CustomSkinningCompute != null)
                return false;

            ComputeShader computeShader = ResolveDefaultComputeShader();
            if (computeShader == null)
                return false;

            controller.CustomSkinningCompute = computeShader;
            EditorUtility.SetDirty(controller);
            return true;
        }
    }
}
#endif
