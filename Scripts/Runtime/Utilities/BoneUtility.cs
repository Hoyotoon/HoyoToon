using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Utilities
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
            return TransformSearchUtility.FindChildRecursive(parent, name, includeRoot: false);
        }
    }
}
