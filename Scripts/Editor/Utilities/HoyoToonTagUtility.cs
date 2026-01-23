#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace HoyoToon.Utilities
{
    internal static class HoyoToonTagUtility
    {
        public static void EnsureTagExists(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    return;
                }
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }

        public static bool NameContainsAny(string value, IEnumerable<string> tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
