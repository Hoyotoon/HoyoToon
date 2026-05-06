using System.Collections.Generic;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    internal static class HoyoToonRenderParticipantRegistry
    {
        static readonly List<HoyoToonPlanarReflectionParticipant> s_PlanarReflectionParticipants =
            new List<HoyoToonPlanarReflectionParticipant>();

        static readonly List<HsrManikinShadowReceiver> s_ManikinShadowReceivers =
            new List<HsrManikinShadowReceiver>();

        static readonly Dictionary<int, int> s_VersionBySceneHandle = new Dictionary<int, int>();
        static int s_Version;
        static bool s_PruneNeeded;

        static HoyoToonRenderParticipantRegistry()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        internal static int Version => s_Version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            s_PlanarReflectionParticipants.Clear();
            s_ManikinShadowReceivers.Clear();
            s_VersionBySceneHandle.Clear();
            s_Version = 0;
            s_PruneNeeded = false;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        internal static int GetVersion(UnityScene scene)
        {
            if (!scene.IsValid())
                return s_Version;

            int sceneHandle = scene.handle;
            return sceneHandle != 0 && s_VersionBySceneHandle.TryGetValue(sceneHandle, out int sceneVersion)
                ? sceneVersion
                : 0;
        }

        internal static void Register(HoyoToonPlanarReflectionParticipant participant)
        {
            if (participant == null)
                return;

            PruneNullIfNeeded();
            if (!s_PlanarReflectionParticipants.Contains(participant))
            {
                s_PlanarReflectionParticipants.Add(participant);
                MarkDirty(participant.Scene);
            }
        }

        internal static void Unregister(HoyoToonPlanarReflectionParticipant participant)
        {
            if (participant == null)
                return;

            s_PruneNeeded = true;
            if (s_PlanarReflectionParticipants.Remove(participant))
                MarkDirty(participant.Scene);
        }

        internal static void Register(HsrManikinShadowReceiver receiver)
        {
            if (receiver == null)
                return;

            PruneNullIfNeeded();
            if (!s_ManikinShadowReceivers.Contains(receiver))
            {
                s_ManikinShadowReceivers.Add(receiver);
                MarkDirty(receiver.Scene);
            }
        }

        internal static void Unregister(HsrManikinShadowReceiver receiver)
        {
            if (receiver == null)
                return;

            s_PruneNeeded = true;
            if (s_ManikinShadowReceivers.Remove(receiver))
                MarkDirty(receiver.Scene);
        }

        internal static void GetPlanarReflectionParticipants(
            UnityScene scene,
            List<HoyoToonPlanarReflectionParticipant> results)
        {
            if (results == null)
                return;

            PruneNullIfNeeded();
            results.Clear();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return;

            for (int i = 0; i < s_PlanarReflectionParticipants.Count; ++i)
            {
                HoyoToonPlanarReflectionParticipant participant = s_PlanarReflectionParticipants[i];
                if (participant != null
                    && participant.isActiveAndEnabled
                    && participant.Scene == scene)
                {
                    results.Add(participant);
                }
            }
        }

        internal static bool HasManikinShadowReceiver(UnityScene scene)
        {
            PruneNullIfNeeded();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return false;

            for (int i = 0; i < s_ManikinShadowReceivers.Count; ++i)
            {
                HsrManikinShadowReceiver receiver = s_ManikinShadowReceivers[i];
                if (receiver != null
                    && receiver.isActiveAndEnabled
                    && receiver.Scene == scene
                    && receiver.HasActiveRenderer())
                {
                    return true;
                }
            }

            return false;
        }

        internal static void MarkDirty()
        {
            unchecked
            {
                s_Version++;
            }
        }

        internal static void MarkDirty(UnityScene scene)
        {
            MarkDirty();

            if (!scene.IsValid())
                return;

            int sceneHandle = scene.handle;
            if (sceneHandle == 0)
                return;

            unchecked
            {
                s_VersionBySceneHandle.TryGetValue(sceneHandle, out int sceneVersion);
                s_VersionBySceneHandle[sceneHandle] = sceneVersion + 1;
            }
        }

        internal static void MarkPruneNeeded(UnityScene scene)
        {
            s_PruneNeeded = true;
            MarkDirty(scene);
        }

        static void HandleSceneUnloaded(UnityScene scene)
        {
            s_PruneNeeded = true;
            MarkDirty(scene);
        }

        static void PruneNullIfNeeded()
        {
            if (!s_PruneNeeded)
                return;

            PruneNull();
            s_PruneNeeded = false;
        }

        static void PruneNull()
        {
            bool removed = false;
            for (int i = s_PlanarReflectionParticipants.Count - 1; i >= 0; --i)
            {
                if (s_PlanarReflectionParticipants[i] == null)
                {
                    s_PlanarReflectionParticipants.RemoveAt(i);
                    removed = true;
                }
            }

            for (int i = s_ManikinShadowReceivers.Count - 1; i >= 0; --i)
            {
                if (s_ManikinShadowReceivers[i] == null)
                {
                    s_ManikinShadowReceivers.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
                MarkDirty();
        }
    }
}
