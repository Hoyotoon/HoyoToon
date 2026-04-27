#if UNITY_EDITOR
namespace HoyoToon.Editor.Onboarding
{
    internal static class OnboardingFocusController
    {
        public static void FocusStepTargets(OnboardingStep step)
        {
            if (step == null || !step.AutoFocusTargets)
            {
                return;
            }

            for (int index = 0; index < step.HighlightTargets.Count; index++)
            {
                if (FocusTarget(step.HighlightTargets[index]))
                {
                    return;
                }
            }
        }

        public static bool FocusTarget(string targetId)
        {
            OnboardingTarget target = OnboardingTargetRegistry.Get(targetId);
            if (target == null || !target.Available)
            {
                return false;
            }

            target.Focus?.Invoke();
            return true;
        }
    }
}
#endif
