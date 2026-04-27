#if UNITY_EDITOR
namespace HoyoToon.Editor.Prerequisites.RenderPipeline
{
    internal sealed class RenderPipelineCheck : IPrerequisiteCheck
    {
        private const string CheckId = "render-pipeline";
        private const string DisplayNameValue = "Render Pipeline";

        public string Id => CheckId;

        public string DisplayName => DisplayNameValue;

        public PrerequisiteEvaluation Evaluate()
        {
            RenderPipelineSelection selection = RenderPipelineSwitcher.GetSelection();
            RenderPipelineDetectionResult detection = RenderPipelineDetector.Detect();

            if (RenderPipelineSwitcher.IsRequiredAsset(detection.AssetPath))
            {
                return PrerequisiteEvaluation.Pass(
                    CheckId,
                    DisplayNameValue,
                    $"Detected the HoyoToon URP asset '{detection.AssetName}' via {detection.Source}.{FormatAssetPathSuffix(detection.AssetPath)}");
            }

            switch (selection)
            {
                case RenderPipelineSelection.Universal:
                    return EvaluateMissingUrpConfiguration(detection);

                case RenderPipelineSelection.BuiltIn:
                    return PrerequisiteEvaluation.Error(
                        Id,
                        DisplayName,
                        $"Built-in Render Pipeline is selected, but HoyoToon only supports the packaged URP setup. Current state: {BuildCurrentStateMessage(detection)}",
                        actionHint: BuildUrpActionHint());

                case RenderPipelineSelection.Unspecified:
                default:
                    return PrerequisiteEvaluation.Warning(
                        Id,
                        DisplayName,
                        BuildSelectionRequiredMessage(detection),
                        isBlocking: true,
                        actionHint: BuildUrpActionHint());
            }
        }

        public PrerequisiteFixResult TryApplySafeFix()
        {
            return PrerequisiteFixResult.None("Render Pipeline setup stays manual because HoyoToon requires explicit confirmation before assigning the packaged URP asset.");
        }

        private static PrerequisiteEvaluation EvaluateMissingUrpConfiguration(RenderPipelineDetectionResult detection)
        {
            if (detection.Kind == RenderPipelineKind.BuiltIn)
            {
                return PrerequisiteEvaluation.Error(
                    CheckId,
                    DisplayNameValue,
                    $"HoyoToon requires the packaged URP setup, but the project is still using the Built-in Render Pipeline. {detection.Source} is null.",
                    actionHint: BuildUrpActionHint());
            }

            return PrerequisiteEvaluation.Error(
                CheckId,
                DisplayNameValue,
                $"URP is selected, but the packaged HoyoToon URP asset is not assigned. Current state: {BuildCurrentStateMessage(detection)}",
                actionHint: BuildUrpActionHint());
        }

        private static string BuildSelectionRequiredMessage(RenderPipelineDetectionResult detection)
        {
            return $"HoyoToon requires the packaged URP asset, but no valid HoyoToon render pipeline setup has been selected yet. Current state: {BuildCurrentStateMessage(detection)}";
        }

        private static string BuildUrpActionHint()
        {
            return $"Set up HoyoToon URP in the startup prompt or assign '{RenderPipelineSwitcher.RequiredAssetPath}'.";
        }

        private static string BuildCurrentStateMessage(RenderPipelineDetectionResult detection)
        {
            string friendlyKind = detection.Kind switch
            {
                RenderPipelineKind.BuiltIn => "Built-in Render Pipeline",
                RenderPipelineKind.Universal => "a Universal Render Pipeline asset",
                RenderPipelineKind.HighDefinition => "an HDRP asset",
                RenderPipelineKind.CustomScriptable => "a non-URP Scriptable Render Pipeline asset",
                RenderPipelineKind.Unknown => "an unrecognized render pipeline asset",
                _ => "an unsupported render pipeline state",
            };

            if (detection.Kind == RenderPipelineKind.BuiltIn)
            {
                return $"Detected {friendlyKind} because {detection.Source} is null.";
            }

            return $"Detected {friendlyKind} via {detection.Source}: type='{detection.AssetTypeName}', name='{detection.AssetName}'.{FormatAssetPathSuffix(detection.AssetPath)}";
        }

        private static string FormatAssetPathSuffix(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : $" Asset path: '{assetPath}'.";
        }
    }
}
#endif
