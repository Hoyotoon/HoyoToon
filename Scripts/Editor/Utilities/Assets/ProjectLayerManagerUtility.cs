#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class ProjectLayerManagerUtility
    {
        private const string TagManagerAssetPath = "ProjectSettings/TagManager.asset";
        private const int FirstUserLayerIndex = 8;

        internal static bool LayerExists(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            try
            {
                if (ContainsLayer(InternalEditorUtility.layers, layerName))
                {
                    return true;
                }

                SerializedObject tagManager = LoadTagManager();
                return ContainsLayer(tagManager?.FindProperty("layers"), layerName);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Prerequisites,
                    $"Failed to inspect layer '{layerName}'.",
                    exception);
            }

            return false;
        }

        internal static bool EnsureLayerExists(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            try
            {
                if (LayerExists(layerName))
                {
                    return true;
                }

                SerializedObject tagManager = LoadTagManager();
                if (tagManager == null)
                {
                    return false;
                }

                SerializedProperty layersProperty = tagManager.FindProperty("layers");
                if (layersProperty == null)
                {
                    return false;
                }

                if (ContainsLayer(layersProperty, layerName))
                {
                    return true;
                }

                for (int i = FirstUserLayerIndex; i < layersProperty.arraySize; ++i)
                {
                    SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrWhiteSpace(layerProperty.stringValue))
                    {
                        continue;
                    }

                    layerProperty.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return true;
                }

                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Prerequisites,
                    $"Failed to create layer '{layerName}' because no user layer slots are available.");
                return false;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Prerequisites,
                    $"Failed to create layer '{layerName}'.",
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

        private static bool ContainsLayer(string[] layers, string layerName)
        {
            if (layers == null)
            {
                return false;
            }

            for (int i = 0; i < layers.Length; ++i)
            {
                if (string.Equals(layers[i], layerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsLayer(SerializedProperty layersProperty, string layerName)
        {
            if (layersProperty == null)
            {
                return false;
            }

            for (int i = 0; i < layersProperty.arraySize; ++i)
            {
                SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                if (string.Equals(layerProperty.stringValue, layerName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
