#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal static class FbxAssetResolver
    {
        internal static bool TryResolve(GameObject sceneInstance, out GameObject sourceAsset, out string assetPath)
        {
            sourceAsset = null;
            assetPath = null;
            if (!TryResolve(sceneInstance, out assetPath))
            {
                return false;
            }

            sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return sourceAsset != null;
        }

        internal static bool TryResolve(GameObject sceneInstance, out GameObject sourceAsset)
        {
            return TryResolve(sceneInstance, out sourceAsset, out _);
        }

        internal static bool TryResolve(GameObject sceneInstance, out string assetPath)
        {
            assetPath = null;
            if (sceneInstance == null)
            {
                return false;
            }

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sceneInstance);
            if (IsFbxPath(prefabPath))
            {
                assetPath = prefabPath;
                return true;
            }

            var directPath = AssetDatabase.GetAssetPath(sceneInstance);
            if (IsFbxPath(directPath))
            {
                assetPath = directPath;
                return true;
            }

            var renderers = sceneInstance.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!TryResolveSharedMesh(renderer, out var mesh))
                {
                    continue;
                }

                var meshPath = AssetDatabase.GetAssetPath(mesh);
                if (IsFbxPath(meshPath))
                {
                    assetPath = meshPath;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryResolveSharedMesh(Renderer renderer, out Mesh mesh)
        {
            mesh = null;
            if (renderer == null)
            {
                return false;
            }

            switch (renderer)
            {
                case SkinnedMeshRenderer skinned:
                    mesh = skinned.sharedMesh;
                    break;
                case MeshRenderer meshRenderer:
                    var filter = meshRenderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                    break;
                default:
                    break;
            }

            return mesh != null;
        }

        private static bool IsFbxPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif