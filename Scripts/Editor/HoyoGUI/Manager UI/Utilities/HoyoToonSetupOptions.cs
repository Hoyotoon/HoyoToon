#if UNITY_EDITOR
using System;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal sealed class HoyoToonSetupOptions
    {
        public bool GenerateMaterials { get; set; } = true;
        public bool ApplyModelImportSettings { get; set; } = true;
        public bool InstantiateInScene { get; set; } = true;

        public HoyoToonSetupOptions Clone()
        {
            return new HoyoToonSetupOptions
            {
                GenerateMaterials = GenerateMaterials,
                ApplyModelImportSettings = ApplyModelImportSettings,
                InstantiateInScene = InstantiateInScene
            };
        }
    }
}
#endif
