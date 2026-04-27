#if UNITY_EDITOR
namespace HoyoToon.Editor.Prerequisites.RenderPipeline
{
    internal enum RenderPipelineSelection
    {
        Unspecified,
        Universal,
        BuiltIn,
    }

    internal enum RenderPipelineKind
    {
        BuiltIn,
        Universal,
        HighDefinition,
        CustomScriptable,
        Unknown,
    }

    internal readonly struct RenderPipelineDetectionResult
    {
        public RenderPipelineKind Kind { get; }

        public string Source { get; }

        public string AssetName { get; }

        public string AssetPath { get; }

        public string AssetTypeName { get; }

        public bool HasAssignedAsset => !string.IsNullOrWhiteSpace(AssetTypeName);

        public RenderPipelineDetectionResult(
            RenderPipelineKind kind,
            string source,
            string assetName,
            string assetPath,
            string assetTypeName)
        {
            Kind = kind;
            Source = source;
            AssetName = assetName;
            AssetPath = assetPath;
            AssetTypeName = assetTypeName;
        }
    }
}
#endif