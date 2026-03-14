#if UNITY_EDITOR
namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal sealed class SetupOptions
    {
        public bool GenerateMaterials { get; set; } = true;
        public bool ApplyModelImportSettings { get; set; } = true;
        public bool InstantiateInScene { get; set; } = true;

        public SetupOptions Clone()
        {
            return new SetupOptions
            {
                GenerateMaterials = GenerateMaterials,
                ApplyModelImportSettings = ApplyModelImportSettings,
                InstantiateInScene = InstantiateInScene
            };
        }
    }
}
#endif
