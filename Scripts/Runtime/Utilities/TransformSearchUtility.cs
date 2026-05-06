using System;
using System.Collections.Generic;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Utilities
{
    public static class TransformSearchUtility
    {
        public static Transform FindChildRecursive(Transform root, string name, bool includeRoot = true)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (includeRoot && string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; ++i)
            {
                Transform child = root.GetChild(i);
                Transform found = FindChildRecursive(child, name, includeRoot: true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static Transform FindChildRecursive(Transform root, IReadOnlyList<string> candidateNames, bool includeRoot = true)
        {
            if (root == null || candidateNames == null || candidateNames.Count == 0)
            {
                return null;
            }

            if (includeRoot && MatchesAny(root.name, candidateNames))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; ++i)
            {
                Transform found = FindChildRecursive(root.GetChild(i), candidateNames, includeRoot: true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static Transform FindInScene(UnityScene scene, string name)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; ++i)
            {
                Transform found = FindChildRecursive(roots[i] != null ? roots[i].transform : null, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static Transform FindInScene(UnityScene scene, IReadOnlyList<string> candidateNames)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene) || candidateNames == null || candidateNames.Count == 0)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; ++i)
            {
                Transform found = FindChildRecursive(roots[i] != null ? roots[i].transform : null, candidateNames);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static int CompareHierarchyOrder(
            Transform left,
            Transform right,
            List<Transform> leftScratch,
            List<Transform> rightScratch)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            leftScratch ??= new List<Transform>();
            rightScratch ??= new List<Transform>();

            BuildHierarchyPath(left, leftScratch);
            BuildHierarchyPath(right, rightScratch);

            int sharedCount = Mathf.Min(leftScratch.Count, rightScratch.Count);
            for (int i = 0; i < sharedCount; ++i)
            {
                Transform leftPathTransform = leftScratch[i];
                Transform rightPathTransform = rightScratch[i];

                int siblingComparison = leftPathTransform.GetSiblingIndex().CompareTo(rightPathTransform.GetSiblingIndex());
                if (siblingComparison != 0)
                {
                    return siblingComparison;
                }

                int nameComparison = string.CompareOrdinal(leftPathTransform.name, rightPathTransform.name);
                if (nameComparison != 0)
                {
                    return nameComparison;
                }
            }

            return leftScratch.Count.CompareTo(rightScratch.Count);
        }

        public static void BuildHierarchyPath(Transform transform, List<Transform> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            Transform current = transform;
            while (current != null)
            {
                results.Add(current);
                current = current.parent;
            }

            results.Reverse();
        }

        private static bool MatchesAny(string name, IReadOnlyList<string> candidateNames)
        {
            for (int i = 0; i < candidateNames.Count; ++i)
            {
                if (string.Equals(name, candidateNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
