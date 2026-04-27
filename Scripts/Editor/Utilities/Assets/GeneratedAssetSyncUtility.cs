using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Assets
{
    internal static class GeneratedAssetSyncUtility
    {
        internal static bool AssetMatchesPayload<TAsset>(TAsset asset, Dictionary<string, object> payload) where TAsset : ScriptableObject
        {
            if (asset == null)
            {
                return false;
            }

            return ObjectMatches(asset, payload);
        }

        internal static TAsset LoadOrCreateAsset<TAsset>(string assetPath) where TAsset : ScriptableObject
        {
            TAsset existingAsset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
            if (existingAsset != null)
            {
                return existingAsset;
            }

            var asset = ScriptableObject.CreateInstance<TAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        internal static void OverwriteAsset<TAsset>(TAsset asset, Dictionary<string, object> payload) where TAsset : ScriptableObject
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            ApplyObject(asset, payload ?? new Dictionary<string, object>(StringComparer.Ordinal));
            EditorUtility.SetDirty(asset);
        }

        internal static void EnsureAssetFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string[] segments = assetFolderPath.Split('/');
            string currentPath = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string nextPath = $"{currentPath}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[i]);
                }

                currentPath = nextPath;
            }
        }

        internal static Dictionary<string, object> CreateObject(params (string FieldName, object Value)[] fields)
        {
            var result = new Dictionary<string, object>(fields.Length, StringComparer.Ordinal);
            foreach ((string fieldName, object value) in fields)
            {
                result[fieldName] = value;
            }

            return result;
        }

        private static void ApplyObject(object target, Dictionary<string, object> payload)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (payload == null)
            {
                return;
            }

            foreach ((string fieldName, object rawValue) in payload)
            {
                FieldInfo field = FindField(target.GetType(), fieldName);
                if (field == null)
                {
                    throw new InvalidOperationException($"Serialized field '{fieldName}' was not found on '{target.GetType().FullName}'.");
                }

                object convertedValue = ConvertValue(field.FieldType, rawValue);
                field.SetValue(target, convertedValue);
            }
        }

        private static bool ObjectMatches(object target, Dictionary<string, object> payload)
        {
            if (target == null)
            {
                return false;
            }

            if (payload == null)
            {
                return true;
            }

            foreach ((string fieldName, object expectedRawValue) in payload)
            {
                FieldInfo field = FindField(target.GetType(), fieldName);
                if (field == null)
                {
                    throw new InvalidOperationException($"Serialized field '{fieldName}' was not found on '{target.GetType().FullName}'.");
                }

                object actualValue = field.GetValue(target);
                if (!ValuesMatch(field.FieldType, actualValue, expectedRawValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValuesMatch(Type targetType, object actualValue, object expectedRawValue)
        {
            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                return ValuesMatch(nullableType, actualValue, expectedRawValue);
            }

            if (targetType == typeof(string))
            {
                return string.Equals(NormalizeString(actualValue), NormalizeString(expectedRawValue), StringComparison.Ordinal);
            }

            if (expectedRawValue == null)
            {
                if (targetType.IsValueType)
                {
                    object defaultValue = Activator.CreateInstance(targetType);
                    return Equals(actualValue, defaultValue);
                }

                return actualValue == null;
            }

            if (targetType.IsEnum || targetType.IsPrimitive || targetType == typeof(decimal))
            {
                object expectedValue = ConvertValue(targetType, expectedRawValue);
                return Equals(actualValue, expectedValue);
            }

            if (typeof(IList).IsAssignableFrom(targetType))
            {
                return ListMatches(targetType, actualValue as IList, expectedRawValue);
            }

            if (expectedRawValue is Dictionary<string, object> dictionary)
            {
                return actualValue != null && ObjectMatches(actualValue, dictionary);
            }

            object convertedValue = ConvertValue(targetType, expectedRawValue);
            return Equals(actualValue, convertedValue);
        }

        private static bool ListMatches(Type targetType, IList actualList, object expectedRawValue)
        {
            if (expectedRawValue is string)
            {
                return (actualList?.Count ?? 0) == 0;
            }

            List<object> expectedItems = expectedRawValue is IEnumerable enumerable
                ? enumerable.Cast<object>().ToList()
                : new List<object>();

            int actualCount = actualList?.Count ?? 0;
            if (actualCount != expectedItems.Count)
            {
                return false;
            }

            Type elementType = targetType.IsArray
                ? targetType.GetElementType()
                : targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object);

            for (int i = 0; i < expectedItems.Count; i++)
            {
                if (!ValuesMatch(elementType, actualList[i], expectedItems[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static object ConvertValue(Type targetType, object value)
        {
            if (value == null)
            {
                return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
                    ? Activator.CreateInstance(targetType)
                    : null;
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                return ConvertValue(nullableType, value);
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            if (targetType.IsEnum)
            {
                return value is string enumName
                    ? Enum.Parse(targetType, enumName, true)
                    : Enum.ToObject(targetType, value);
            }

            if (targetType.IsPrimitive || targetType == typeof(decimal))
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }

            if (typeof(IList).IsAssignableFrom(targetType))
            {
                return ConvertList(targetType, value);
            }

            if (value is Dictionary<string, object> dictionary)
            {
                object instance = Activator.CreateInstance(targetType);
                ApplyObject(instance, dictionary);
                return instance;
            }

            return value;
        }

        private static object ConvertList(Type targetType, object value)
        {
            var list = (IList)Activator.CreateInstance(targetType);
            if (value is not IEnumerable enumerable || value is string)
            {
                return list;
            }

            Type elementType = targetType.IsArray
                ? targetType.GetElementType()
                : targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object);

            foreach (object item in enumerable)
            {
                list.Add(ConvertValue(elementType, item));
            }

            return list;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, Flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static string NormalizeString(object value)
        {
            return value?.ToString() ?? string.Empty;
        }
    }
}