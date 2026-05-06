using System.Collections.Generic;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Utilities
{
    internal static class HsrCharacterLightRegistry
    {
        private static readonly List<Light> s_CharacterLights = new List<Light>();
        private static readonly HashSet<int> s_DirtySceneHandles = new HashSet<int>();
        private static readonly HashSet<int> s_CleanSceneHandles = new HashSet<int>();
        private static bool s_AllScenesDirty = true;

        internal static bool IsDirty
        {
            get
            {
                PruneNull();
                return s_AllScenesDirty || s_DirtySceneHandles.Count > 0;
            }
        }

        internal static int Count
        {
            get
            {
                PruneNull();
                return s_CharacterLights.Count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            s_CharacterLights.Clear();
            s_DirtySceneHandles.Clear();
            s_CleanSceneHandles.Clear();
            s_AllScenesDirty = true;
        }

        internal static void Register(Light characterLight)
        {
            if (characterLight == null)
            {
                return;
            }

            PruneNull();
            if (s_CharacterLights.Contains(characterLight))
            {
                return;
            }

            s_CharacterLights.Add(characterLight);
            MarkDirty(characterLight);
        }

        internal static void Unregister(Light characterLight)
        {
            if (characterLight == null)
            {
                return;
            }

            if (s_CharacterLights.Remove(characterLight))
            {
                MarkDirty(characterLight);
            }
        }

        internal static bool IsDirtyForScene(UnityScene scene)
        {
            if (!scene.IsValid())
                return IsDirty;

            PruneNull();
            return s_AllScenesDirty
                ? !s_CleanSceneHandles.Contains(scene.handle)
                : s_DirtySceneHandles.Contains(scene.handle);
        }

        internal static bool Matches(IReadOnlyList<Light> characterLights)
        {
            PruneNull();

            if (characterLights == null || characterLights.Count != s_CharacterLights.Count)
            {
                return false;
            }

            for (int i = 0; i < s_CharacterLights.Count; ++i)
            {
                if (characterLights[i] != s_CharacterLights[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static void CopySceneLights(UnityScene scene, List<Light> characterLights)
        {
            if (characterLights == null)
                return;

            characterLights.Clear();
            if (!scene.IsValid())
                return;

            PruneNull();

            int sceneHandle = scene.handle;
            for (int i = 0; i < s_CharacterLights.Count; ++i)
            {
                Light characterLight = s_CharacterLights[i];
                if (characterLight == null
                    || characterLight.gameObject == null
                    || characterLight.gameObject.scene.handle != sceneHandle)
                {
                    continue;
                }

                characterLights.Add(characterLight);
            }
        }

        internal static Light[] ToArray()
        {
            PruneNull();
            MarkClean();
            return s_CharacterLights.ToArray();
        }

        internal static void ReplaceWith(IReadOnlyList<Light> characterLights)
        {
            s_CharacterLights.Clear();

            if (characterLights != null)
            {
                for (int i = 0; i < characterLights.Count; ++i)
                {
                    Light characterLight = characterLights[i];
                    if (characterLight != null && !s_CharacterLights.Contains(characterLight))
                    {
                        s_CharacterLights.Add(characterLight);
                    }
                }
            }

            MarkClean();
        }

        internal static void MarkClean()
        {
            s_AllScenesDirty = false;
            s_DirtySceneHandles.Clear();
            s_CleanSceneHandles.Clear();
        }

        internal static void MarkCleanForScene(UnityScene scene)
        {
            if (!scene.IsValid())
                return;

            if (s_AllScenesDirty)
            {
                s_CleanSceneHandles.Add(scene.handle);
                return;
            }

            s_DirtySceneHandles.Remove(scene.handle);
        }

        private static void PruneNull()
        {
            for (int i = s_CharacterLights.Count - 1; i >= 0; --i)
            {
                if (s_CharacterLights[i] == null)
                {
                    s_CharacterLights.RemoveAt(i);
                    MarkAllDirty();
                }
            }
        }

        private static void MarkDirty(Light characterLight)
        {
            if (characterLight != null
                && characterLight.gameObject != null
                && characterLight.gameObject.scene.IsValid())
            {
                int sceneHandle = characterLight.gameObject.scene.handle;
                if (s_AllScenesDirty)
                    s_CleanSceneHandles.Remove(sceneHandle);
                else
                    s_DirtySceneHandles.Add(sceneHandle);

                return;
            }

            MarkAllDirty();
        }

        private static void MarkAllDirty()
        {
            s_AllScenesDirty = true;
            s_DirtySceneHandles.Clear();
            s_CleanSceneHandles.Clear();
        }
    }
}
