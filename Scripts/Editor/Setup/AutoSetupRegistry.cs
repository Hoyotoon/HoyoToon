#if UNITY_EDITOR
using System;
using HoyoToon.Editor.Setup.Games.HSR;

namespace HoyoToon.Editor.Setup
{
    internal static class AutoSetupRegistry
    {
        private static readonly AutoSetupGameProfile[] Profiles =
        {
            HonkaiStarRailAutoSetupProfile.Instance,
        };

        public static bool TryGetProfile(string gameKey, out AutoSetupGameProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(gameKey))
            {
                for (int i = 0; i < Profiles.Length; i++)
                {
                    AutoSetupGameProfile candidate = Profiles[i];
                    if (candidate != null
                        && string.Equals(candidate.GameKey, gameKey, StringComparison.OrdinalIgnoreCase))
                    {
                        profile = candidate;
                        return true;
                    }
                }
            }

            profile = null;
            return false;
        }

        public static AutoSetupGameProfile Resolve(AutoSetupContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (TryGetProfile(context.DetectedGameKey, out AutoSetupGameProfile detectedProfile))
            {
                return detectedProfile;
            }

            for (int i = 0; i < Profiles.Length; i++)
            {
                AutoSetupGameProfile candidate = Profiles[i];
                if (candidate != null && candidate.CanHandle(context))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
#endif
