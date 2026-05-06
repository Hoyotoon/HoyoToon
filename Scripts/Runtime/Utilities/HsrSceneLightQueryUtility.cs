using System.Collections.Generic;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Utilities
{
    internal static class HsrSceneLightQueryUtility
    {
        private static readonly List<GameObject> s_RootScratch = new List<GameObject>();
        private static readonly List<Light> s_LightScratch = new List<Light>();
        private static readonly List<Light> s_RegisteredSceneLights = new List<Light>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            s_RootScratch.Clear();
            s_LightScratch.Clear();
            s_RegisteredSceneLights.Clear();
        }

        internal static bool IsNamedLight(Light light, string lightName)
        {
            return light != null
                && light.gameObject != null
                && string.Equals(light.gameObject.name, lightName, System.StringComparison.Ordinal);
        }

        internal static void RegisterSceneLight(Light sceneLight)
        {
            if (sceneLight == null)
                return;

            PruneRegisteredSceneLights();
            if (!s_RegisteredSceneLights.Contains(sceneLight))
                s_RegisteredSceneLights.Add(sceneLight);
        }

        internal static void UnregisterSceneLight(Light sceneLight)
        {
            if (sceneLight == null)
                return;

            s_RegisteredSceneLights.Remove(sceneLight);
        }

        internal static Light FindRegisteredNamedLightInScene(UnityScene scene, string lightName)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return null;

            PruneRegisteredSceneLights();

            Light resolved = null;
            int resolvedId = int.MaxValue;
            int sceneHandle = scene.handle;
            for (int i = 0; i < s_RegisteredSceneLights.Count; ++i)
            {
                Light light = s_RegisteredSceneLights[i];
                if (light.gameObject.scene.handle != sceneHandle || !IsNamedLight(light, lightName))
                    continue;

                int instanceId = light.GetInstanceID();
                if (instanceId >= resolvedId)
                    continue;

                resolved = light;
                resolvedId = instanceId;
            }

            return resolved;
        }

        internal static Light FindNamedLightInScene(UnityScene scene, string lightName)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return null;

            Light resolved = null;
            int resolvedId = int.MaxValue;

            s_RootScratch.Clear();
            scene.GetRootGameObjects(s_RootScratch);

            for (int rootIndex = 0; rootIndex < s_RootScratch.Count; ++rootIndex)
            {
                GameObject root = s_RootScratch[rootIndex];
                if (root == null)
                    continue;

                s_LightScratch.Clear();
                root.GetComponentsInChildren<Light>(true, s_LightScratch);
                for (int i = 0; i < s_LightScratch.Count; ++i)
                {
                    Light light = s_LightScratch[i];
                    if (!IsNamedLight(light, lightName))
                        continue;

                    int instanceId = light.GetInstanceID();
                    if (instanceId >= resolvedId)
                        continue;

                    resolved = light;
                    resolvedId = instanceId;
                }
            }

            s_RootScratch.Clear();
            s_LightScratch.Clear();

            if (resolved != null)
                RegisterSceneLight(resolved);

            return resolved;
        }

        internal static Light RebuildCharacterLightsAndResolveSceneMain(
            UnityScene scene,
            string characterLightName,
            string sceneLightName,
            List<Light> discoveredCharacterLights)
        {
            if (discoveredCharacterLights == null)
                return null;

            discoveredCharacterLights.Clear();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return null;

            Light resolvedSceneLight = null;
            int resolvedSceneLightId = int.MaxValue;

            s_RootScratch.Clear();
            scene.GetRootGameObjects(s_RootScratch);

            for (int rootIndex = 0; rootIndex < s_RootScratch.Count; ++rootIndex)
            {
                GameObject root = s_RootScratch[rootIndex];
                if (root == null)
                    continue;

                s_LightScratch.Clear();
                root.GetComponentsInChildren<Light>(true, s_LightScratch);
                for (int i = 0; i < s_LightScratch.Count; ++i)
                {
                    Light light = s_LightScratch[i];
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
            }

            s_RootScratch.Clear();
            s_LightScratch.Clear();

            if (resolvedSceneLight != null)
                RegisterSceneLight(resolvedSceneLight);

            return resolvedSceneLight;
        }

        private static void PruneRegisteredSceneLights()
        {
            for (int i = s_RegisteredSceneLights.Count - 1; i >= 0; --i)
            {
                Light light = s_RegisteredSceneLights[i];
                if (light == null || light.gameObject == null || !light.gameObject.scene.IsValid())
                    s_RegisteredSceneLights.RemoveAt(i);
            }
        }
    }
}
