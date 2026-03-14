#if UNITY_EDITOR
using System;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    internal static class RendererUtils
    {
        internal static bool RendererUsesHoyoToon(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var materials = renderer.sharedMaterials;
            foreach (var material in materials)
            {
                if (!material || material.shader == null)
                {
                    continue;
                }

                if (material.shader.name.IndexOf("HoyoToon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static GameObject ResolveRendererRoot(Renderer renderer, HoyoToonManager manager)
        {
            if (renderer == null)
            {
                return null;
            }

            var managerTransform = manager != null ? manager.transform : null;
            var current = renderer.transform;

            while (current != null && current.parent != null && current.parent != managerTransform)
            {
                current = current.parent;
            }

            if (current == null || current == managerTransform)
            {
                return null;
            }

            return current.gameObject;
        }
    }
}
#endif
