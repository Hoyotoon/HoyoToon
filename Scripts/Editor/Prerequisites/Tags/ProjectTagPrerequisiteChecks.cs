#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Parsing;

namespace HoyoToon.Editor.Prerequisites.Tags
{
    internal static class ProjectTagPrerequisiteChecks
    {
        internal static IPrerequisiteCheck Require(string tagName, string dependentSystemsDescription = null)
        {
            return new ProjectTagPrerequisiteCheck(tagName, dependentSystemsDescription);
        }

        private sealed class ProjectTagPrerequisiteCheck : IPrerequisiteCheck
        {
            private readonly string tagName;
            private readonly string dependentSystemsDescription;

            public ProjectTagPrerequisiteCheck(string tagName, string dependentSystemsDescription)
            {
                this.tagName = tagName;
                this.dependentSystemsDescription = dependentSystemsDescription;
            }

            public string Id => BuildCheckId(tagName);

            public string DisplayName => $"Project Tag '{tagName}'";

            public PrerequisiteEvaluation Evaluate()
            {
                if (ProjectTagManagerUtility.TagExists(tagName))
                {
                    return PrerequisiteEvaluation.Pass(
                        Id,
                        DisplayName,
                        $"Project tag '{tagName}' is available.");
                }

                string dependencyMessage = string.IsNullOrWhiteSpace(dependentSystemsDescription)
                    ? "project systems expect it to exist"
                    : $"{dependentSystemsDescription} expect it to exist";

                return PrerequisiteEvaluation.Error(
                    Id,
                    DisplayName,
                    $"Project tag '{tagName}' is missing, but {dependencyMessage}.",
                    canAutoFix: true,
                    actionHint: $"Add the '{tagName}' project tag or use the safe-fix command.");
            }

            public PrerequisiteFixResult TryApplySafeFix()
            {
                try
                {
                    return ProjectTagManagerUtility.EnsureTagExists(tagName)
                        ? PrerequisiteFixResult.AppliedFix($"Created project tag '{tagName}'.")
                        : PrerequisiteFixResult.Failed($"Failed to create project tag '{tagName}'.");
                }
                catch (System.Exception exception)
                {
                    return PrerequisiteFixResult.Failed($"Failed to create project tag '{tagName}': {exception.Message}");
                }
            }
        }

        private static string BuildCheckId(string tagName)
        {
            return StringTokenUtility.BuildSlug("project-tag", tagName);
        }
    }
}
#endif
