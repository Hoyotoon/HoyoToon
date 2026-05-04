#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.Setup
{
    internal sealed class AutoSetupResult
    {
        private readonly List<string> executedFeatures = new List<string>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();

        public AutoSetupResult(string profileKey, string profileDisplayName)
        {
            ProfileKey = profileKey;
            ProfileDisplayName = string.IsNullOrWhiteSpace(profileDisplayName)
                ? "Auto Setup"
                : profileDisplayName;
        }

        public string ProfileKey { get; }

        public string ProfileDisplayName { get; }

        public IReadOnlyList<string> ExecutedFeatures => executedFeatures;

        public IReadOnlyList<string> Warnings => warnings;

        public IReadOnlyList<string> Errors => errors;

        public bool Succeeded => errors.Count == 0;

        public int MaterialsCreated { get; set; }

        public int MaterialsUpdated { get; set; }

        public int ModelsConverted { get; set; }

        public int ModelImportSettingsApplied { get; set; }

        public int CharacterIconsCached { get; set; }

        public int TangentApplications { get; set; }

        public int PrerequisiteFailures { get; set; }

        public void RecordFeature(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
            {
                return;
            }

            if (!executedFeatures.Exists(candidate => string.Equals(candidate, featureId, StringComparison.Ordinal)))
            {
                executedFeatures.Add(featureId);
            }
        }

        public void RecordWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                warnings.Add(message);
            }
        }

        public void RecordError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }
    }
}
#endif
