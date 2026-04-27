using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Runtime.Scene.HSR
{
    internal static class HsrSceneLightQueryUtility
    {
        internal static bool IsNamedLight(Light light, string lightName)
        {
            return light != null
                && light.gameObject != null
                && string.Equals(light.gameObject.name, lightName, System.StringComparison.Ordinal);
        }

        internal static Light FindLowestInstanceIdNamedLight(string lightName)
        {
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light resolved = null;
            int resolvedId = int.MaxValue;

            for (int i = 0; i < allLights.Length; ++i)
            {
                Light light = allLights[i];
                if (!IsNamedLight(light, lightName))
                    continue;

                int instanceId = light.GetInstanceID();
                if (instanceId >= resolvedId)
                    continue;

                resolved = light;
                resolvedId = instanceId;
            }

            return resolved;
        }

        internal static Light RebuildCharacterLightsAndResolveSceneMain(
            string characterLightName,
            string sceneLightName,
            List<Light> discoveredCharacterLights)
        {
            if (discoveredCharacterLights == null)
                return null;

            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            discoveredCharacterLights.Clear();

            Light resolvedSceneLight = null;
            int resolvedSceneLightId = int.MaxValue;

            for (int i = 0; i < allLights.Length; ++i)
            {
                Light light = allLights[i];
                if (light == null || light.gameObject == null)
                    continue;

                if (IsNamedLight(light, characterLightName))
                    discoveredCharacterLights.Add(light);

                if (!IsNamedLight(light, sceneLightName))
                    continue;

                int instanceId = light.GetInstanceID();
                if (instanceId >= resolvedSceneLightId)
                    continue;

                resolvedSceneLight = light;
                resolvedSceneLightId = instanceId;
            }

            return resolvedSceneLight;
        }
    }
}
