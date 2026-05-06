#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Utilities;
using UnityEditor;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Editor.UI.Manager
{
    [InitializeOnLoad]
    internal static class SceneLightService
    {
        private static readonly Dictionary<int, SceneLightCache> s_LightCacheBySceneHandle =
            new Dictionary<int, SceneLightCache>();

        static SceneLightService()
        {
            EditorApplication.hierarchyChanged -= InvalidateAll;
            EditorApplication.hierarchyChanged += InvalidateAll;
        }

        internal static Light FindNamedLight(UnityScene scene, string lightName)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene) || string.IsNullOrWhiteSpace(lightName))
                return null;

            SceneLightCache cache = GetCache(scene);
            for (int i = 0; i < cache.Lights.Count; ++i)
            {
                Light light = cache.Lights[i];
                if (light != null
                    && light.gameObject != null
                    && light.gameObject.scene == scene
                    && string.Equals(light.name, lightName, StringComparison.OrdinalIgnoreCase))
                {
                    return light;
                }
            }

            return null;
        }

        internal static IReadOnlyList<Light> GetLights(UnityScene scene)
        {
            return GetCache(scene).Lights;
        }

        internal static void Invalidate(UnityScene scene)
        {
            int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
            if (sceneHandle != 0)
                s_LightCacheBySceneHandle.Remove(sceneHandle);
        }

        internal static void InvalidateAll()
        {
            s_LightCacheBySceneHandle.Clear();
        }

        private static SceneLightCache GetCache(UnityScene scene)
        {
            int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
            if (!s_LightCacheBySceneHandle.TryGetValue(sceneHandle, out SceneLightCache cache))
            {
                cache = RebuildCache(scene);
                s_LightCacheBySceneHandle[sceneHandle] = cache;
            }

            return cache;
        }

        private static SceneLightCache RebuildCache(UnityScene scene)
        {
            SceneLightCache cache = new SceneLightCache();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return cache;

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; ++i)
            {
                Light light = lights[i];
                if (light != null && light.gameObject != null && light.gameObject.scene == scene)
                    cache.Lights.Add(light);
            }

            return cache;
        }

        private sealed class SceneLightCache
        {
            internal readonly List<Light> Lights = new List<Light>();
        }
    }
}
#endif
