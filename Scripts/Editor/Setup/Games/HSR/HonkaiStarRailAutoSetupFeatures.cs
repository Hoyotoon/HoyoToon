#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.AutoSetup;

namespace HoyoToon.Editor.Setup.Games.HSR
{
    internal static class HonkaiStarRailAutoSetupFeatures
    {
        public static AutoSetupFeature[] Build()
        {
            return new[]
            {
                new AutoSetupFeature(
                    "prerequisites",
                    "Apply prerequisites",
                    AutoSetup.ApplyPrerequisites,
                    context => context.Options.RunPrerequisites),
                new AutoSetupFeature(
                    "convert-models",
                    "Convert models",
                    AutoSetupModelsUtility.ConvertSelectedModels,
                    context => context.Options.ConvertModels && context.SelectedFbxAssetPaths.Count > 0),
                new AutoSetupFeature(
                    "hsr-companion-materials",
                    "Copy HSR companion materials",
                    AutoSetupMaterialsUtility.CopyHsrCompanionMaterials,
                    context => context.Options.GenerateMaterials && context.SelectedAssets.Count > 0),
                new AutoSetupFeature(
                    "materials",
                    "Generate materials",
                    AutoSetupMaterialsUtility.GenerateFromSelection,
                    context => context.Options.GenerateMaterials && context.SelectedAssets.Count > 0),
                new AutoSetupFeature(
                    "model-import-settings",
                    "Apply model import settings",
                    AutoSetupModelsUtility.ApplyDetectedImportSettings,
                    context => context.Options.ApplyModelImportSettings && context.ModelAssetPaths.Count > 0),
                new AutoSetupFeature(
                    "instantiate-models",
                    "Instantiate models",
                    AutoSetupSceneUtility.InstantiateModels,
                    context => context.ModelAssetPaths.Count > 0),
                new AutoSetupFeature(
                    "hsr-components",
                    "Add HSR components",
                    AutoSetupSceneUtility.AddHsrComponents,
                    context => context.InstantiatedModels.Count > 0),
                new AutoSetupFeature(
                    "tangents",
                    "Apply tangent settings",
                    AutoSetupModelsUtility.ApplyTangents,
                    context => context.Options.ApplyTangents && (context.InstantiatedModels.Count > 0 || context.ModelAssetPaths.Count > 0)),
                new AutoSetupFeature(
                    "placement",
                    "Update placement",
                    AutoSetupSceneUtility.UpdatePlacement,
                    context => context.InstantiatedModels.Count > 0),
            };
        }
    }
}
#endif
