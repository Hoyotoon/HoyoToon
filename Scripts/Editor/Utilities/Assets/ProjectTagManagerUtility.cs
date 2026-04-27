#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class ProjectTagManagerUtility
    {
        private const string TagManagerAssetPath = "ProjectSettings/TagManager.asset";

        internal static bool TagExists(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            try
            {
                if (ContainsTag(InternalEditorUtility.tags, tagName))
                {
                    return true;
                }

                SerializedObject tagManager = LoadTagManager();
                return ContainsTag(tagManager?.FindProperty("tags"), tagName);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Prerequisites,
                    $"Failed to inspect tag '{tagName}'.",
                    exception);
            }

            return false;
        }

        internal static bool EnsureTagExists(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            try
            {
                if (TagExists(tagName))
                {
                    return true;
                }

                SerializedObject tagManager = LoadTagManager();
                if (tagManager == null)
                {
                    return false;
                }

                SerializedProperty tagsProperty = tagManager.FindProperty("tags");
                if (tagsProperty == null)
                {
                    return false;
                }

                if (ContainsTag(tagsProperty, tagName))
                {
                    return true;
                }

                tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
                tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tagName;
                tagManager.ApplyModifiedProperties();
                return true;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Prerequisites,
                    $"Failed to create tag '{tagName}'.",
                    exception);
                return false;
            }
        }

        private static SerializedObject LoadTagManager()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerAssetPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                return null;
            }

            return new SerializedObject(assets[0]);
        }

        private static bool ContainsTag(string[] tags, string tagName)
        {
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; ++i)
            {
                if (string.Equals(tags[i], tagName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTag(SerializedProperty tagsProperty, string tagName)
        {
            if (tagsProperty == null)
            {
                return false;
            }

            for (int i = 0; i < tagsProperty.arraySize; ++i)
            {
                SerializedProperty tagProperty = tagsProperty.GetArrayElementAtIndex(i);
                if (string.Equals(tagProperty.stringValue, tagName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
