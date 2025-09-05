#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Prerequisites
{
    public enum PrerequisiteSeverity { Info, Warning, Error }

    public readonly struct PrerequisiteResult
    {
        public readonly bool Passed;
        public readonly PrerequisiteSeverity Severity;
        public readonly string Message;

        public PrerequisiteResult(bool passed, PrerequisiteSeverity severity, string message)
        {
            Passed = passed; Severity = severity; Message = message;
        }

        public static PrerequisiteResult Ok(string msg = "") => new PrerequisiteResult(true, PrerequisiteSeverity.Info, msg);
        public static PrerequisiteResult Fail(PrerequisiteSeverity severity, string msg) => new PrerequisiteResult(false, severity, msg);
    }

    public interface IPrerequisiteCheck
    {
        string Name { get; }
        PrerequisiteResult Evaluate();
        bool TryFix();
    }
}
#endif
