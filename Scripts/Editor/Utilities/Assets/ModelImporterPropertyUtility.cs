#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class ModelImporterPropertyUtility
    {
        private sealed class BoolPropertyAccessor
        {
            private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private readonly string propertyName;
            private PropertyInfo property;
            private bool propertyResolved;

            public BoolPropertyAccessor(string propertyName)
            {
                this.propertyName = propertyName;
            }

            public bool TryGet(ModelImporter importer, out bool value)
            {
                value = false;
                return importer != null
                    && (TryGetFromProperty(importer, out value) || TryGetFromSerializedObject(importer, out value));
            }

            public bool TrySet(ModelImporter importer, bool value)
            {
                return importer != null
                    && (TrySetFromProperty(importer, value) || TrySetFromSerializedObject(importer, value));
            }

            private bool TryGetFromProperty(ModelImporter importer, out bool value)
            {
                value = false;
                PropertyInfo cachedProperty = GetProperty(importer.GetType());
                if (cachedProperty == null || !cachedProperty.CanRead)
                {
                    return false;
                }

                try
                {
                    if (cachedProperty.GetValue(importer) is bool boolValue)
                    {
                        value = boolValue;
                        return true;
                    }
                }
                catch
                {
                }

                return false;
            }

            private bool TrySetFromProperty(ModelImporter importer, bool value)
            {
                PropertyInfo cachedProperty = GetProperty(importer.GetType());
                if (cachedProperty == null || !cachedProperty.CanWrite)
                {
                    return false;
                }

                try
                {
                    cachedProperty.SetValue(importer, value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private bool TryGetFromSerializedObject(ModelImporter importer, out bool value)
            {
                value = false;
                using var serializedObject = new SerializedObject(importer);
                SerializedProperty serializedProperty = serializedObject.FindProperty(propertyName);
                if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Boolean)
                {
                    return false;
                }

                value = serializedProperty.boolValue;
                return true;
            }

            private bool TrySetFromSerializedObject(ModelImporter importer, bool value)
            {
                using var serializedObject = new SerializedObject(importer);
                SerializedProperty serializedProperty = serializedObject.FindProperty(propertyName);
                if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Boolean)
                {
                    return false;
                }

                serializedProperty.boolValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            private PropertyInfo GetProperty(Type importerType)
            {
                if (propertyResolved)
                {
                    return property;
                }

                while (importerType != null)
                {
                    PropertyInfo candidate = importerType.GetProperty(propertyName, InstanceBindingFlags);
                    if (candidate != null && candidate.PropertyType == typeof(bool))
                    {
                        property = candidate;
                        break;
                    }

                    importerType = importerType.BaseType;
                }

                propertyResolved = true;
                return property;
            }
        }

        private static readonly BoolPropertyAccessor LegacyBlendshapeNormalsAccessor =
            new BoolPropertyAccessor("legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes");

        public static bool TryGetLegacyBlendshapeNormals(ModelImporter importer, out bool value)
        {
            return LegacyBlendshapeNormalsAccessor.TryGet(importer, out value);
        }

        public static bool TrySetLegacyBlendshapeNormals(ModelImporter importer, bool value)
        {
            return LegacyBlendshapeNormalsAccessor.TrySet(importer, value);
        }
    }
}
#endif