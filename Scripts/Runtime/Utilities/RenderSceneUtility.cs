using System;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace HoyoToon.Runtime.Utilities
{
    internal static class RenderSceneUtility
    {
        internal static UnityScene ResolveRenderScene(Camera camera)
        {
            return ResolveRenderScene(camera, null);
        }

        internal static UnityScene ResolveRenderScene(Camera camera, Func<UnityScene, bool> isCameraScenePreferred)
        {
            UnityScene cameraScene = camera != null ? camera.gameObject.scene : default;
            if (isCameraScenePreferred != null ? isCameraScenePreferred(cameraScene) : IsSceneUsable(cameraScene))
                return cameraScene;

            UnityScene activeScene = UnitySceneManager.GetActiveScene();
            return IsSceneUsable(activeScene) ? activeScene : cameraScene;
        }

        internal static bool IsSceneUsable(UnityScene scene)
        {
            return scene.IsValid() && scene.isLoaded;
        }

        internal static int GetSceneHandleOrDefault(UnityScene scene)
        {
            return scene.IsValid() ? scene.handle : 0;
        }
    }
}
