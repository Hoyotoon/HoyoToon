#if UNITY_EDITOR
namespace HoyoToon.Editor.Setup
{
    internal sealed class AutoSetupOptions
    {
        public bool RunPrerequisites { get; set; } = true;

        public bool PromptForRenderPipelineSelection { get; set; } = true;

        public bool PromptForKnownCharacterProblems { get; set; } = true;

        public bool GenerateMaterials { get; set; } = true;

        public bool ApplyModelImportSettings { get; set; } = true;

        public bool ApplyTangents { get; set; } = true;

        public bool ShowDialogs { get; set; } = true;

        public bool LogSummary { get; set; } = true;
    }
}
#endif
