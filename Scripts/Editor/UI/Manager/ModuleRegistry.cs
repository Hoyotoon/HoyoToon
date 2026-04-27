using System.Collections.Generic;
using HoyoToon.Editor.UI.Manager.Modules;

namespace HoyoToon.Editor.UI.Manager
{
    public static class ModuleRegistry
    {
        public static IReadOnlyList<IManagerModule> CreateModules()
        {
            return new IManagerModule[]
            {
                new SetupModule(),
                new AssetsModule(),
                new CharacterModule(),
                new SceneModule(),
                new RendersModule(),
            };
        }
    }
}
