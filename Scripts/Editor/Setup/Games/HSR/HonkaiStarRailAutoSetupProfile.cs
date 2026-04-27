#if UNITY_EDITOR
using System;

namespace HoyoToon.Editor.Setup.Games.HSR
{
    internal static class HonkaiStarRailAutoSetupProfile
    {
        public const string GameKey = "Honkai Star Rail";

        public static AutoSetupGameProfile Instance { get; } = BuildProfile();

        private static AutoSetupGameProfile BuildProfile()
        {
            return new AutoSetupGameProfile(
                GameKey,
                "Honkai Star Rail",
                HonkaiStarRailAutoSetupFeatures.Build(),
                context => context != null
                    && string.Equals(context.DetectedGameKey, GameKey, StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif
