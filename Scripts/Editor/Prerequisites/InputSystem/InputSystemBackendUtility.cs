#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Prerequisites.InputSystem
{
    internal enum InputSystemBackendMode
    {
        Unknown,
        OldInputManager,
        NewInputSystem,
        Both,
    }

    internal static class InputSystemBackendUtility
    {
        private const int OldInputManagerValue = 0;
        private const int NewInputSystemValue = 1;
        private const int BothValue = 2;

        public static InputSystemBackendMode GetConfiguredBackendMode()
        {
            if (!TryGetPlayerSettingsObject(out Object playerSettings))
            {
                return InputSystemBackendMode.Unknown;
            }

            using var serializedObject = new SerializedObject(playerSettings);
            SerializedProperty property = serializedObject.FindProperty("activeInputHandler");
            if (property == null)
            {
                return InputSystemBackendMode.Unknown;
            }

            return property.intValue switch
            {
                OldInputManagerValue => InputSystemBackendMode.OldInputManager,
                NewInputSystemValue => InputSystemBackendMode.NewInputSystem,
                BothValue => InputSystemBackendMode.Both,
                _ => InputSystemBackendMode.Unknown,
            };
        }

        public static bool TrySetConfiguredBackendMode(InputSystemBackendMode mode)
        {
            if (!TryGetPlayerSettingsObject(out Object playerSettings))
            {
                return false;
            }

            using var serializedObject = new SerializedObject(playerSettings);
            SerializedProperty property = serializedObject.FindProperty("activeInputHandler");
            if (property == null)
            {
                return false;
            }

            property.intValue = mode switch
            {
                InputSystemBackendMode.NewInputSystem => NewInputSystemValue,
                InputSystemBackendMode.Both => BothValue,
                _ => OldInputManagerValue,
            };

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TryGetPlayerSettingsObject(out Object playerSettings)
        {
            playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings") as Object;
            return playerSettings != null;
        }
    }
}
#endif