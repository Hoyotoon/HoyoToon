#if UNITY_EDITOR
using System;

namespace HoyoToon.Editor.Setup
{
    internal delegate void AutoSetupFeatureHandler(AutoSetupContext context, AutoSetupResult result);

    internal sealed class AutoSetupFeature
    {
        public AutoSetupFeature(
            string id,
            string displayName,
            AutoSetupFeatureHandler execute,
            Func<AutoSetupContext, bool> shouldRun = null)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            ShouldRun = shouldRun;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public AutoSetupFeatureHandler Execute { get; }

        public Func<AutoSetupContext, bool> ShouldRun { get; }

        public bool CanRun(AutoSetupContext context)
        {
            return ShouldRun == null || ShouldRun.Invoke(context);
        }
    }
}
#endif
