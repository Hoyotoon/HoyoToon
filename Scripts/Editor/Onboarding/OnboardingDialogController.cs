#if UNITY_EDITOR
using System;
using HoyoToon.Editor.UI.Dialogs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Onboarding
{
    internal sealed class OnboardingDialogController
    {
        private OnboardingDialogWindow window;
        private bool hasPositionedWindow;

        public void ShowStep(
            OnboardingStep step,
            int index,
            int count,
            OnboardingAsyncStatus asyncStatus,
            string errorText,
            bool canContinue,
            Rect anchorWindowRect)
        {
            if (step == null)
            {
                Close();
                return;
            }

            if (window == null)
            {
                OnboardingDialogWindow createdWindow = ScriptableObject.CreateInstance<OnboardingDialogWindow>();
                createdWindow.Destroyed += () =>
                {
                    if (ReferenceEquals(window, createdWindow))
                    {
                        window = null;
                        hasPositionedWindow = false;
                        if (OnboardingManager.IsRunning)
                        {
                            EditorApplication.delayCall += OnboardingManager.RefreshDialog;
                        }
                    }
                };
                window = createdWindow;
                window.titleContent = new GUIContent("HoyoToon Onboarding");
                window.minSize = new Vector2(420f, 300f);
                window.ShowUtility();
            }

            window.Configure(step, index, count, asyncStatus, errorText, canContinue);
            if (!hasPositionedWindow)
            {
                PositionWindow(anchorWindowRect);
                hasPositionedWindow = true;
            }
        }

        public void EnsureVisible()
        {
            if (window != null)
            {
                return;
            }

            OnboardingManager.RefreshDialog();
        }

        public void Close()
        {
            if (window == null)
            {
                return;
            }

            OnboardingDialogWindow closing = window;
            window = null;
            hasPositionedWindow = false;
            closing.Close();
        }

        private void PositionWindow(Rect anchorWindowRect)
        {
            if (window == null)
            {
                return;
            }

            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            Vector2 size = window.position.size;
            if (size.x < 420f || size.y < 300f)
            {
                size = new Vector2(520f, 360f);
            }

            Rect target = anchorWindowRect.width > 1f && anchorWindowRect.height > 1f
                ? anchorWindowRect
                : mainWindow;

            float rightX = target.xMax + 18f;
            float x = rightX + size.x < mainWindow.xMax - 20f
                ? rightX
                : Mathf.Max(mainWindow.x + 20f, target.xMin - size.x - 18f);
            float y = Mathf.Clamp(target.y + 42f, mainWindow.y + 20f, Mathf.Max(mainWindow.y + 20f, mainWindow.yMax - size.y - 20f));
            window.position = new Rect(x, y, size.x, size.y);
        }
    }

    internal sealed class OnboardingDialogWindow : EditorWindow
    {
        private OnboardingStep step;
        private int index;
        private int count;
        private OnboardingAsyncStatus asyncStatus;
        private string errorText = string.Empty;
        private bool canContinue;
        private string configurationKey = string.Empty;
        private HoyoToonDialogShell shell;
        public event Action Destroyed;

        public void Configure(
            OnboardingStep newStep,
            int newIndex,
            int newCount,
            OnboardingAsyncStatus newAsyncStatus,
            string newErrorText,
            bool newCanContinue)
        {
            string newConfigurationKey = BuildConfigurationKey(newStep, newIndex, newCount, newAsyncStatus, newErrorText, newCanContinue);
            if (string.Equals(configurationKey, newConfigurationKey, StringComparison.Ordinal)
                && rootVisualElement != null
                && rootVisualElement.childCount > 0)
            {
                return;
            }

            step = newStep;
            index = newIndex;
            count = newCount;
            asyncStatus = newAsyncStatus ?? OnboardingAsyncStatus.Idle();
            errorText = newErrorText ?? string.Empty;
            canContinue = newCanContinue;
            configurationKey = newConfigurationKey;
            titleContent = new GUIContent("HoyoToon Onboarding");

            if (rootVisualElement != null && rootVisualElement.panel != null)
            {
                Build();
            }
        }

        public void CreateGUI()
        {
            Build();
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke();
        }

        private static string BuildConfigurationKey(
            OnboardingStep step,
            int index,
            int count,
            OnboardingAsyncStatus asyncStatus,
            string errorText,
            bool canContinue)
        {
            OnboardingAsyncStatus status = asyncStatus ?? OnboardingAsyncStatus.Idle();
            return string.Join(
                "|",
                step != null ? step.Id : string.Empty,
                index.ToString(),
                count.ToString(),
                status.State.ToString(),
                status.Message ?? string.Empty,
                status.ErrorMessage ?? string.Empty,
                status.Progress.ToString(System.Globalization.CultureInfo.InvariantCulture),
                errorText ?? string.Empty,
                canContinue ? "1" : "0");
        }

        private void Build()
        {
            rootVisualElement.Clear();
            if (step == null)
            {
                return;
            }

            if (!HoyoToonDialogShell.TryBuild(this, step.Title, BuildSubtitle(), out shell, out string error))
            {
                rootVisualElement.Add(new HelpBox(error, HelpBoxMessageType.Error));
                return;
            }

            shell.BodyContent.Clear();
            shell.ActionContent.Clear();
            shell.BodyContent.Add(CreateBody());
            shell.ActionContent.Add(CreateButtonRow());
        }

        private string BuildSubtitle()
        {
            int total = Math.Max(1, count);
            int displayIndex = Mathf.Clamp(index + 1, 1, total);
            return "Step " + displayIndex + " of " + total;
        }

        private VisualElement CreateBody()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            card.AddToClassList("ht-dialog-card");

            Label statusLabel = new Label(GetStepTypeLabel(step.StepType));
            statusLabel.AddToClassList("ht-dialog-status");
            statusLabel.AddToClassList("ht-dialog-status--info");
            card.Add(statusLabel);

            if (!string.IsNullOrWhiteSpace(step.InstructionText))
            {
                card.Add(HoyoToonDialogMarkdownView.Create(step.InstructionText));
            }

            if (!string.IsNullOrWhiteSpace(step.RequiredValue) && !step.EvaluateCompletion())
            {
                card.Add(CreateDetail("Required value: " + step.RequiredValue));
            }

            if (!string.IsNullOrWhiteSpace(step.CorrectionText) && !step.EvaluateCompletion())
            {
                card.Add(CreateWarning(step.CorrectionText));
            }

            if (asyncStatus != null && asyncStatus.State != OnboardingAsyncState.Idle)
            {
                card.Add(CreateAsyncStatus(asyncStatus));
            }

            if (!string.IsNullOrWhiteSpace(errorText))
            {
                card.Add(CreateWarning(errorText));
            }

            return card;
        }

        private VisualElement CreateAsyncStatus(OnboardingAsyncStatus status)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-progress-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");

            string message = status.HasFailed
                ? status.ErrorMessage
                : status.Message;
            card.Add(CreateDetail(string.IsNullOrWhiteSpace(message) ? status.State.ToString() : message));

            VisualElement track = new VisualElement();
            track.AddToClassList("ht-progress-track");
            VisualElement fill = new VisualElement();
            fill.AddToClassList("ht-progress-fill");
            float progress = status.Progress >= 0f ? Mathf.Clamp01(status.Progress) : 0.35f;
            fill.style.width = Length.Percent(progress * 100f);
            if (status.Progress < 0f && status.IsRunning)
            {
                fill.AddToClassList("ht-progress-fill--indeterminate");
            }

            track.Add(fill);
            card.Add(track);
            return card;
        }

        private static Label CreateDetail(string text)
        {
            Label label = new Label(text ?? string.Empty);
            label.AddToClassList("ht-dialog-detail");
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static HelpBox CreateWarning(string text)
        {
            HelpBox box = new HelpBox(text ?? string.Empty, HelpBoxMessageType.Warning);
            box.AddToClassList("ht-helpbox-warning");
            return box;
        }

        private VisualElement CreateButtonRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ht-dialog-button-row");

            if (step.Retry != null || (asyncStatus != null && asyncStatus.HasFailed))
            {
                Button retry = CreateButton("Retry", "secondary", OnboardingManager.RetryCurrentStep);
                OnboardingTargetRegistry.RegisterVisualElement("Dialog.RetryButton", retry, "Retry", "Onboarding Dialog");
                row.Add(retry);
            }

            if (step.ShowContinueButton)
            {
                string buttonText = step.StepType == OnboardingStepType.Confirm
                    ? "Confirm"
                    : IsFinalStep()
                        ? "Finish"
                        : "Continue";
                string targetId = step.StepType == OnboardingStepType.Confirm && step.Id == "DockManager"
                    ? "Dialog.DockConfirmButton"
                    : "Dialog.ContinueButton";

                Button button = CreateButton(buttonText, "primary", OnboardingManager.HandleDialogContinue);
                button.SetEnabled(canContinue);
                OnboardingTargetRegistry.RegisterVisualElement(targetId, button, buttonText, "Onboarding Dialog");
                row.Add(button);
            }

            return row;
        }

        private Button CreateButton(string text, string style, Action onClick)
        {
            Button button = new Button(() => onClick?.Invoke())
            {
                text = text ?? string.Empty,
                tooltip = text ?? string.Empty
            };
            button.AddToClassList("ht-dialog-button");
            button.AddToClassList(style == "primary" ? "ht-btn-primary" : "ht-btn-secondary");
            return button;
        }

        private bool IsFinalStep()
        {
            return count > 0 && index >= count - 1;
        }

        private static string GetStepTypeLabel(OnboardingStepType stepType)
        {
            switch (stepType)
            {
                case OnboardingStepType.Click:
                    return "Click highlighted control";
                case OnboardingStepType.FieldSelection:
                    return "Choose required value";
                case OnboardingStepType.Toggle:
                    return "Set required toggle";
                case OnboardingStepType.MultiAction:
                    return "Try highlighted control";
                case OnboardingStepType.Action:
                    return "Guided action";
                case OnboardingStepType.Validation:
                    return "Validation";
                case OnboardingStepType.Confirm:
                    return "Confirmation";
                default:
                    return "Guided tutorial";
            }
        }
    }
}
#endif
