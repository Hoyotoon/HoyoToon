#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Onboarding
{
    internal sealed class OnboardingInputBlocker : System.IDisposable
    {
        private readonly VisualElement root;
        private OnboardingStep currentStep;
        private bool disposed;

        public bool BlockingEnabled { get; set; } = true;

        public OnboardingInputBlocker(VisualElement root)
        {
            this.root = root;
            if (root == null)
            {
                return;
            }

            root.RegisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseDownEvent>(HandleMouseDown, TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(HandleWheel, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            currentStep = null;
            if (root == null)
            {
                return;
            }

            root.UnregisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
            root.UnregisterCallback<MouseDownEvent>(HandleMouseDown, TrickleDown.TrickleDown);
            root.UnregisterCallback<WheelEvent>(HandleWheel, TrickleDown.TrickleDown);
            root.UnregisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
        }

        public void SetStep(OnboardingStep step)
        {
            if (disposed)
            {
                return;
            }

            currentStep = step;
        }

        private void HandlePointerDown(PointerDownEvent evt)
        {
            if (ShouldBlockPosition(evt.position))
            {
                Block(evt);
            }
        }

        private void HandleMouseDown(MouseDownEvent evt)
        {
            if (ShouldBlockPosition(evt.mousePosition))
            {
                Block(evt);
            }
        }

        private void HandleWheel(WheelEvent evt)
        {
            if (IsBlockingStep() && !AnyAllowedTargetVisible())
            {
                return;
            }

            if (ShouldBlockPosition(evt.mousePosition))
            {
                Block(evt);
            }
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (!ShouldBlockKeyboard())
            {
                return;
            }

            Block(evt);
        }

        private bool ShouldBlockKeyboard()
        {
            if (!IsBlockingStep())
            {
                return false;
            }

            FocusController focusController = root != null && root.panel != null ? root.panel.focusController : null;
            VisualElement focusedElement = focusController != null ? focusController.focusedElement as VisualElement : null;
            return !OnboardingTargetRegistry.IsFocusedElementInsideTargets(focusedElement, currentStep.AllowedTargets);
        }

        private bool ShouldBlockPosition(Vector2 panelPosition)
        {
            if (!IsBlockingStep())
            {
                return false;
            }

            return !OnboardingTargetRegistry.IsPointInsideTargets(panelPosition, currentStep.AllowedTargets);
        }

        private bool AnyAllowedTargetVisible()
        {
            if (currentStep == null || currentStep.AllowedTargets == null)
            {
                return false;
            }

            for (int index = 0; index < currentStep.AllowedTargets.Count; index++)
            {
                if (OnboardingTargetRegistry.IsTargetVisible(currentStep.AllowedTargets[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlockingStep()
        {
            return BlockingEnabled
                && currentStep != null
                && currentStep.BlockAllOtherUI;
        }

        private static void Block(EventBase evt)
        {
            OnboardingManager.NotifyBlockedInput();
            evt.StopImmediatePropagation();
        }
    }
}
#endif
