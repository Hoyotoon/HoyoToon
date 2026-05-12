using UnityEngine;
using UnityEngine.InputSystem;

namespace HoyoToon.Runtime.Input
{
    internal static class InputActions
    {
        internal const string DefaultActionMapName = "HoyoToon";
        internal const string LegacyActionMapName = "Simulator";

        private const string HoyoToonInputAssetPath = "Input/HoyoToon";

        private static InputActionAsset s_CachedFallbackInputActions;
        private static bool s_HasLoadedFallbackInputActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_CachedFallbackInputActions = null;
            s_HasLoadedFallbackInputActions = false;
        }

        internal static InputActionAsset Resolve(InputActionAsset serializedInputActions)
        {
            if (serializedInputActions != null)
                return serializedInputActions;

            if (!s_HasLoadedFallbackInputActions)
            {
                s_CachedFallbackInputActions = Resources.Load<InputActionAsset>(HoyoToonInputAssetPath);
                s_HasLoadedFallbackInputActions = true;
            }

            return s_CachedFallbackInputActions;
        }
    }
}

