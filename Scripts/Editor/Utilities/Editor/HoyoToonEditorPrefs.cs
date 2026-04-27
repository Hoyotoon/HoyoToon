#if UNITY_EDITOR
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Editor
{
    internal static class HoyoToonEditorPrefs
    {
        public static string ProjectKey(string key)
        {
            return (key ?? string.Empty) + "." + GetProjectScopeId();
        }

        public static void DeleteProjectKeyAndLegacy(string key)
        {
            EditorPrefs.DeleteKey(ProjectKey(key));
            EditorPrefs.DeleteKey(key);
        }

        private static string GetProjectScopeId()
        {
            string projectPath = Application.dataPath ?? string.Empty;
            string normalizedPath = projectPath.Replace('\\', '/').ToLowerInvariant();

            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < normalizedPath.Length; index++)
                {
                    hash ^= normalizedPath[index];
                    hash *= 16777619u;
                }

                return hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}
#endif
