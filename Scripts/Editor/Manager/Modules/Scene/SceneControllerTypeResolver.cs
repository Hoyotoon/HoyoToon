#if UNITY_EDITOR
using System;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal static class SceneControllerTypeResolver
    {
        private const string HsrSceneControllerTypeName = "HoyoToon.Runtime.Scene.HSRSceneController";

        internal static Type ResolveHSRSceneControllerType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(HsrSceneControllerTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
#endif
