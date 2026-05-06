using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    internal static class RendererTraversalUtility
    {
        internal static void CollectOwnedRenderers(Component owner, List<Renderer> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (owner == null)
                return;

            owner.GetComponentsInChildren(includeInactive: true, results);
        }

        internal static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
                return skinnedRenderer.sharedMesh;

            if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                return meshFilter != null ? meshFilter.sharedMesh : null;
            }

            return null;
        }

        internal static int GetSubMeshCount(Renderer renderer)
        {
            Mesh mesh = GetRendererMesh(renderer);
            return mesh != null ? mesh.subMeshCount : 0;
        }

        internal static bool HasActiveRenderer(IReadOnlyList<Renderer> renderers)
        {
            for (int i = 0; i < (renderers != null ? renderers.Count : 0); ++i)
            {
                if (IsRendererActive(renderers[i]))
                    return true;
            }

            return false;
        }

        internal static bool IsRendererActive(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && !renderer.forceRenderingOff
                && renderer.gameObject != null
                && renderer.gameObject.activeInHierarchy;
        }
    }
}
