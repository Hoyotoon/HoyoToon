#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon
{
    public static class HoyoToonPreferences
    {
        #region Constants

        public const string PreferencesPrefix = "HoyoToon: ";
        public const string CheckForUpdatesPref = "HoyoToon_CheckForUpdates";
        public const string AutoImportPref = "HoyoToon_AutoImport";
        public static string bgPathProperty = "UI/background";
        public static string logoPathProperty = "UI/managerlogo";

        #endregion

        #region Preferences
        public static void LoadPrefs()
        {
            bool checkForUpdates = EditorPrefs.GetBool(CheckForUpdatesPref, true);
            EditorPrefs.SetBool(CheckForUpdatesPref, checkForUpdates);
            bool autoImport = EditorPrefs.GetBool(AutoImportPref, false);
            EditorPrefs.SetBool(AutoImportPref, autoImport);
        }

        public static void SavePrefs()
        {
            bool checkForUpdates = EditorPrefs.GetBool(CheckForUpdatesPref, true);
            EditorPrefs.SetBool(CheckForUpdatesPref, checkForUpdates);
            bool autoImport = EditorPrefs.GetBool(AutoImportPref, false);
            EditorPrefs.SetBool(AutoImportPref, autoImport);
        }

        #endregion

        #region GUI
        [MenuItem("HoyoToon/Settings/Preferences", false, 99)]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<HoyoToonPreferencesWindow>("HoyoToon Manager");
        }

        private class HoyoToonPreferencesWindow : EditorWindow
        {
            private void OnGUI()
            {
                Rect bgRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(145.0f));
                bgRect.x = 0;
                bgRect.width = EditorGUIUtility.currentViewWidth;
                Rect logoRect = new Rect(bgRect.width / 2 - 375f, bgRect.height / 2 - 65f, 750f, 130f);

                if (!string.IsNullOrEmpty(bgPathProperty))
                {
                    Texture2D bg = Resources.Load<Texture2D>(bgPathProperty);

                    if (bg != null)
                    {
                        GUI.DrawTexture(bgRect, bg, ScaleMode.ScaleAndCrop);
                    }
                }

                if (!string.IsNullOrEmpty(logoPathProperty))
                {
                    Texture2D logo = Resources.Load<Texture2D>(logoPathProperty);

                    if (logo != null)
                    {
                        GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit);
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("HoyoToon Manager", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                EditorGUI.indentLevel++;

                bool newCheckForUpdates = EditorGUILayout.ToggleLeft("Check for Updates on Startup", EditorPrefs.GetBool(CheckForUpdatesPref, true));
                if (newCheckForUpdates != EditorPrefs.GetBool(CheckForUpdatesPref, true))
                {
                    EditorPrefs.SetBool(CheckForUpdatesPref, newCheckForUpdates);
                }

                bool newAutoImport = EditorGUILayout.ToggleLeft("Auto-Import Package", EditorPrefs.GetBool(AutoImportPref, false));
                if (newAutoImport != EditorPrefs.GetBool(AutoImportPref, false))
                {
                    EditorPrefs.SetBool(AutoImportPref, newAutoImport);
                }

                EditorGUI.indentLevel--;
            }
        }
        #endregion
    }
}
#endif