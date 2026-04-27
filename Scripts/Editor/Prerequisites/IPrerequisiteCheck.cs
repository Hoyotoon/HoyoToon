#if UNITY_EDITOR
namespace HoyoToon.Editor.Prerequisites
{
    internal interface IPrerequisiteCheck
    {
        string Id { get; }

        string DisplayName { get; }

        PrerequisiteEvaluation Evaluate();

        PrerequisiteFixResult TryApplySafeFix();
    }
}
#endif