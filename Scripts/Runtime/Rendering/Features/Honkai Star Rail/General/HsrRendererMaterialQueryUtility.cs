using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Runtime.Rendering.HSR
{
    internal static class HsrRendererMaterialQueryUtility
    {
        internal static bool TryGetSharedMaterials(Renderer renderer, List<Material> scratch, out int materialCount)
        {
            materialCount = 0;

            if (renderer == null || scratch == null)
                return false;

            scratch.Clear();
            renderer.GetSharedMaterials(scratch);
            materialCount = scratch.Count;
            return materialCount > 0;
        }

        internal static void AddSharedMaterials(Renderer renderer, List<Material> scratch, HashSet<Material> uniqueMaterials)
        {
            if (uniqueMaterials == null)
                return;

            if (!TryGetSharedMaterials(renderer, scratch, out int materialCount))
                return;

            try
            {
                for (int i = 0; i < materialCount; ++i)
                {
                    Material material = scratch[i];
                    if (material != null)
                        uniqueMaterials.Add(material);
                }
            }
            finally
            {
                scratch.Clear();
            }
        }

        internal static bool HasMaterialTagValue(Material material, string tagName, string expectedValue, StringComparison comparison)
        {
            if (material == null || string.IsNullOrEmpty(tagName) || expectedValue == null)
                return false;

            string actualValue = material.GetTag(tagName, false, string.Empty);
            return string.Equals(actualValue, expectedValue, comparison);
        }

        internal static bool HasShaderPassWithTagValue(Material material, ShaderTagId tagName, string expectedValue, StringComparison comparison)
        {
            if (material == null || expectedValue == null)
                return false;

            Shader shader = material.shader;
            if (shader == null)
                return false;

            int passCount = shader.passCount;
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, tagName);
                if (string.Equals(tagValue.name, expectedValue, comparison))
                    return true;
            }

            return false;
        }

        internal static bool HasNamedPass(Material material, string passName)
        {
            return material != null
                && !string.IsNullOrEmpty(passName)
                && material.FindPass(passName) >= 0;
        }

        internal static bool HasMaterialTagOrShaderPassOrNamedPass(
            Material material,
            string materialTagName,
            ShaderTagId shaderTagName,
            string expectedValue,
            StringComparison comparison)
        {
            return HasMaterialTagValue(material, materialTagName, expectedValue, comparison)
                || HasShaderPassWithTagValue(material, shaderTagName, expectedValue, comparison)
                || HasNamedPass(material, expectedValue);
        }

        internal static int FindFirstShaderPassWithAnyTagValue(Material material, ShaderTagId tagName, IReadOnlyList<ShaderTagId> expectedValues)
        {
            if (material == null || expectedValues == null || expectedValues.Count == 0)
                return -1;

            Shader shader = material.shader;
            if (shader == null)
                return -1;

            int passCount = shader.passCount;
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, tagName);
                for (int i = 0; i < expectedValues.Count; ++i)
                {
                    if (tagValue == expectedValues[i])
                        return passIndex;
                }
            }

            return -1;
        }
    }
}
