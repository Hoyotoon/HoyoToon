#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Serialization
{
    internal static class EditorJsonFileStore
    {
        internal static T Read<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A JSON file path is required.", nameof(filePath));

            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<T>(json);
        }

        internal static bool TryRead<T>(string filePath, out T value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                value = Read<T>(filePath);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        internal static void WriteAtomic<T>(string filePath, T value, bool prettyPrint = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A JSON file path is required.", nameof(filePath));

            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string tempPath = filePath + ".tmp";
            string json = JsonUtility.ToJson(value, prettyPrint);
            File.WriteAllText(tempPath, json);

            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, null);
            else
                File.Move(tempPath, filePath);
        }
    }
}
#endif
