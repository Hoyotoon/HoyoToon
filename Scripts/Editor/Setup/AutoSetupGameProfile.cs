#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.Setup
{
    internal sealed class AutoSetupGameProfile
    {
        private readonly Func<AutoSetupContext, bool> canHandleContext;

        public AutoSetupGameProfile(
            string gameKey,
            string displayName,
            IReadOnlyList<AutoSetupFeature> features,
            Func<AutoSetupContext, bool> canHandleContext = null)
        {
            GameKey = gameKey;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? gameKey : displayName;
            Features = features ?? Array.Empty<AutoSetupFeature>();
            this.canHandleContext = canHandleContext;
        }

        public string GameKey { get; }

        public string DisplayName { get; }

        public IReadOnlyList<AutoSetupFeature> Features { get; }

        public bool CanHandle(AutoSetupContext context)
        {
            if (canHandleContext != null)
            {
                return canHandleContext.Invoke(context);
            }

            return context != null
                && string.Equals(context.DetectedGameKey, GameKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
