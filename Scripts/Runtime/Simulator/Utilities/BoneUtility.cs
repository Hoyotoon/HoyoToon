using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Simulator.Utilities
{
    internal static class BoneUtility
    {
        public static Transform FindBone(Transform root, string primaryName, List<string> alternatives)
        {
            if (root == null) return null;

            var result = FindChildRecursive(root, primaryName);
            if (result != null) return result;

            if (alternatives != null)
            {
                for (int i = 0; i < alternatives.Count; i++)
                {
                    result = FindChildRecursive(root, alternatives[i]);
                    if (result != null) return result;
                }
            }

            return null;
        }

        public static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
