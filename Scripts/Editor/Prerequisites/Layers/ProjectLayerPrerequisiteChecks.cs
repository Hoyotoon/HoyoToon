#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Parsing;

namespace HoyoToon.Editor.Prerequisites.Layers
{
    internal static class ProjectLayerPrerequisiteChecks
    {
        internal static IPrerequisiteCheck Require(string layerName, string dependentSystemsDescription = null)
        {
            return new ProjectLayerPrerequisiteCheck(layerName, dependentSystemsDescription);
        }

        private sealed class ProjectLayerPrerequisiteCheck : IPrerequisiteCheck
        {
            private readonly string layerName;
            private readonly string dependentSystemsDescription;

            public ProjectLayerPrerequisiteCheck(string layerName, string dependentSystemsDescription)
            {
                this.layerName = layerName;
                this.dependentSystemsDescription = dependentSystemsDescription;
            }

            public string Id => BuildCheckId(layerName);

            public string DisplayName => $"Project Layer '{layerName}'";

            public PrerequisiteEvaluation Evaluate()
            {
                if (ProjectLayerManagerUtility.LayerExists(layerName))
                {
                    return PrerequisiteEvaluation.Pass(
                        Id,
                        DisplayName,
                        $"Project layer '{layerName}' is available.");
                }

                string dependencyMessage = string.IsNullOrWhiteSpace(dependentSystemsDescription)
                    ? "project systems expect it to exist"
                    : $"{dependentSystemsDescription} expect it to exist";

                return PrerequisiteEvaluation.Error(
                    Id,
                    DisplayName,
                    $"Project layer '{layerName}' is missing, but {dependencyMessage}.",
                    canAutoFix: true,
                    actionHint: $"Add the '{layerName}' project layer or use the safe-fix command.");
            }

            public PrerequisiteFixResult TryApplySafeFix()
            {
                try
                {
                    return ProjectLayerManagerUtility.EnsureLayerExists(layerName)
                        ? PrerequisiteFixResult.AppliedFix($"Created project layer '{layerName}'.")
                        : PrerequisiteFixResult.Failed($"Failed to create project layer '{layerName}'.");
                }
                catch (System.Exception exception)
                {
                    return PrerequisiteFixResult.Failed($"Failed to create project layer '{layerName}': {exception.Message}");
                }
            }
        }

        private static string BuildCheckId(string layerName)
        {
            return StringTokenUtility.BuildSlug("project-layer", layerName);
        }
    }
}
#endif
