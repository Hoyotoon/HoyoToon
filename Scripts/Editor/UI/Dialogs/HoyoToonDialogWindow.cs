#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Dialogs
{
    internal sealed class HoyoToonDialogWindow : EditorWindow
    {
        internal static readonly Vector2 DefaultSize = new Vector2(560f, 380f);
        private static readonly Vector2 CompactAutoSize = new Vector2(520f, 320f);
        private static readonly Vector2 MinimumSize = new Vector2(480f, 320f);
        private static readonly Vector2 MaximumSize = new Vector2(1600f, 1000f);
        private static readonly Vector2 MaximumInitialSize = new Vector2(980f, 820f);
        private const float MainWindowMargin = 80f;
        private const float AverageCharacterWidth = 7.2f;
        private const float MessageLineHeight = 17.5f;

        private HoyoToonDialogOptions options;
        private HoyoToonDialogShell shell;
        private int selectedResult = -1;
        private bool hasCompleted;
        private Rect initialPosition;

        internal int SelectedResult => selectedResult;

        internal static EditorWindow ShowWindow(HoyoToonDialogOptions options)
        {
            HoyoToonDialogWindow window = CreateInstance<HoyoToonDialogWindow>();
            window.Configure(options);
            if (window.options.Utility)
            {
                window.ShowUtility();
            }
            else
            {
                window.Show();
            }

            return window;
        }

        internal static int ShowModalAndReturn(HoyoToonDialogOptions options)
        {
            HoyoToonDialogWindow window = CreateInstance<HoyoToonDialogWindow>();
            window.Configure(options);
            window.ShowModal();
            return window.SelectedResult;
        }

        internal void Configure(HoyoToonDialogOptions dialogOptions)
        {
            options = HoyoToonDialog.NormalizeOptions(dialogOptions);
            selectedResult = options.CloseResult;
            titleContent = new GUIContent(options.Title);
            ApplyResizeLimits();
            initialPosition = GetCenteredPosition(ResolveInitialSize(options));
            position = initialPosition;
        }

        public void CreateGUI()
        {
            ApplyResizeLimits();
            options = HoyoToonDialog.NormalizeOptions(options);
            selectedResult = options.CloseResult;

            if (!HoyoToonDialogShell.TryBuild(this, options.Title, options.Subtitle, out shell, out string error))
            {
                BuildFallback(error);
                return;
            }

            shell.BodyContent.Clear();
            shell.ActionContent.Clear();
            shell.ActionContent.style.display = DisplayStyle.Flex;
            shell.BodyContent.Add(CreateDialogBody());
            shell.ActionContent.Add(CreateButtonRow());

            rootVisualElement.focusable = true;
            rootVisualElement.RegisterCallback<KeyDownEvent>(HandleKeyDown);
            rootVisualElement.schedule.Execute(ApplyResizeLimits);
            rootVisualElement.schedule.Execute(ApplyInitialPosition).ExecuteLater(1);
            rootVisualElement.schedule.Execute(ApplyInitialPosition).ExecuteLater(50);
            rootVisualElement.schedule.Execute(() => rootVisualElement.Focus());
        }

        private VisualElement CreateDialogBody()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            card.AddToClassList("ht-dialog-card");

            Label statusLabel = new Label(GetDialogTypeLabel(options.DialogType));
            statusLabel.AddToClassList("ht-dialog-status");
            statusLabel.AddToClassList(GetDialogTypeClass(options.DialogType));
            card.Add(statusLabel);

            if (!string.IsNullOrWhiteSpace(options.Message))
            {
                card.Add(HoyoToonDialogMarkdownView.Create(options.Message));
            }

            if (options.BuildBody != null)
            {
                VisualElement customBody = new VisualElement();
                customBody.AddToClassList("ht-column");
                customBody.AddToClassList("ht-gap-8");
                customBody.AddToClassList("ht-dialog-custom-body");
                options.BuildBody(customBody);
                if (customBody.childCount > 0)
                {
                    card.Add(customBody);
                }
            }

            return card;
        }

        private VisualElement CreateButtonRow()
        {
            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("ht-dialog-button-row");

            foreach (HoyoToonDialogButton dialogButton in options.Buttons ?? Array.Empty<HoyoToonDialogButton>())
            {
                if (dialogButton == null)
                {
                    continue;
                }

                HoyoToonDialogButton capturedButton = dialogButton;
                Button button = new Button(() => Complete(capturedButton))
                {
                    text = capturedButton.Text,
                    tooltip = capturedButton.Text
                };
                button.AddToClassList("ht-dialog-button");
                button.AddToClassList(GetButtonClass(capturedButton.StyleKind));
                buttonRow.Add(button);
            }

            return buttonRow;
        }

        private void BuildFallback(string message)
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.paddingLeft = 12f;
            rootVisualElement.style.paddingRight = 12f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;
            rootVisualElement.Add(new HelpBox(
                string.IsNullOrWhiteSpace(message) ? "The HoyoToon dialog could not be created." : message,
                HelpBoxMessageType.Error));
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                CompleteByResult(options.CloseResult);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                HoyoToonDialogButton firstButton = options.Buttons?.FirstOrDefault(button => button != null);
                if (firstButton != null)
                {
                    Complete(firstButton);
                    evt.StopPropagation();
                }
            }
        }

        private void CompleteByResult(int result)
        {
            HoyoToonDialogButton button = options.Buttons?.FirstOrDefault(candidate => candidate != null && candidate.Result == result);
            if (button != null)
            {
                Complete(button);
                return;
            }

            selectedResult = result;
            NotifyClosed(null);
            Close();
        }

        private void Complete(HoyoToonDialogButton button)
        {
            if (button == null)
            {
                CompleteByResult(options.CloseResult);
                return;
            }

            selectedResult = button.Result;
            NotifyClosed(button);
            Close();
        }

        private void NotifyClosed(HoyoToonDialogButton button)
        {
            if (hasCompleted)
            {
                return;
            }

            hasCompleted = true;
            try
            {
                button?.Callback?.Invoke();
                options?.OnClosed?.Invoke(selectedResult);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            if (!hasCompleted)
            {
                NotifyClosed(null);
            }
        }

        private static Rect GetCenteredPosition(Vector2 size)
        {
            Vector2 resolvedSize = new Vector2(
                Mathf.Max(MinimumSize.x, size.x),
                Mathf.Max(MinimumSize.y, size.y));

            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            resolvedSize.x = Mathf.Min(resolvedSize.x, Mathf.Max(MinimumSize.x, mainWindow.width - MainWindowMargin));
            resolvedSize.y = Mathf.Min(resolvedSize.y, Mathf.Max(MinimumSize.y, mainWindow.height - MainWindowMargin));

            float x = mainWindow.x + Mathf.Max(0f, (mainWindow.width - resolvedSize.x) * 0.5f);
            float y = mainWindow.y + Mathf.Max(0f, (mainWindow.height - resolvedSize.y) * 0.5f);
            return new Rect(x, y, resolvedSize.x, resolvedSize.y);
        }

        private void ApplyInitialPosition()
        {
            ApplyResizeLimits();
            if (initialPosition.width <= 0f || initialPosition.height <= 0f)
            {
                return;
            }

            position = initialPosition;
        }

        private static Vector2 ResolveInitialSize(HoyoToonDialogOptions dialogOptions)
        {
            if (dialogOptions == null)
            {
                return DefaultSize;
            }

            Vector2 requestedSize = dialogOptions.Size.x > 0f && dialogOptions.Size.y > 0f
                ? dialogOptions.Size
                : DefaultSize;
            bool hasExplicitSize = !Mathf.Approximately(requestedSize.x, DefaultSize.x) ||
                !Mathf.Approximately(requestedSize.y, DefaultSize.y);

            if (!dialogOptions.AutoSizeToContent)
            {
                return requestedSize;
            }

            string plainMessage = HoyoToonDialogMarkdown.ToPlainText(dialogOptions.Message);
            float contentVolumeWidth = plainMessage.Length > 700
                ? 780f
                : plainMessage.Length > 280
                    ? 660f
                    : 0f;
            int longestLineLength = GetLongestLineLength(plainMessage);
            float widthFromContent = 520f + Mathf.Min(340f, Mathf.Max(0f, longestLineLength - 58) * 4.8f);
            float baselineWidth = hasExplicitSize ? requestedSize.x : CompactAutoSize.x;
            float resolvedWidth = Mathf.Clamp(
                Mathf.Max(baselineWidth, widthFromContent, contentVolumeWidth),
                MinimumSize.x,
                MaximumInitialSize.x);

            float wrapColumn = Mathf.Max(42f, (resolvedWidth - 128f) / AverageCharacterWidth);
            float estimatedVisualLines = EstimateVisualLineCount(plainMessage, wrapColumn);
            float customBodyAllowance = dialogOptions.BuildBody == null ? 0f : 112f;
            int buttonCount = dialogOptions.Buttons?.Count(button => button != null) ?? 1;
            int buttonRows = Mathf.Max(1, Mathf.CeilToInt(buttonCount / 3f));
            float buttonAllowance = 54f + ((buttonRows - 1) * 36f);

            float resolvedHeight = 174f + buttonAllowance + customBodyAllowance + estimatedVisualLines * MessageLineHeight;
            float baselineHeight = hasExplicitSize ? requestedSize.y : CompactAutoSize.y;
            return new Vector2(
                resolvedWidth,
                Mathf.Clamp(Mathf.Max(baselineHeight, resolvedHeight), MinimumSize.y, MaximumInitialSize.y));
        }

        private static int GetLongestLineLength(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int longestLineLength = 0;
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                longestLineLength = Mathf.Max(longestLineLength, string.IsNullOrEmpty(line) ? 0 : line.TrimEnd().Length);
            }

            return longestLineLength;
        }

        private static float EstimateVisualLineCount(string text, float wrapColumn)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 3f;
            }

            float visualLines = 0f;
            string[] lines = text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    visualLines += 0.65f;
                    continue;
                }

                visualLines += Mathf.Max(1f, Mathf.Ceil(line.Length / Mathf.Max(1f, wrapColumn)));
            }

            return Mathf.Max(3f, visualLines);
        }

        private void ApplyResizeLimits()
        {
            minSize = MinimumSize;
            maxSize = MaximumSize;
        }

        private static string GetDialogTypeLabel(HoyoToonDialogType dialogType)
        {
            switch (dialogType)
            {
                case HoyoToonDialogType.Success:
                    return "Complete";
                case HoyoToonDialogType.Warning:
                    return "Warning";
                case HoyoToonDialogType.Error:
                    return "Error";
                case HoyoToonDialogType.Question:
                    return "Action";
                default:
                    return "Info";
            }
        }

        private static string GetDialogTypeClass(HoyoToonDialogType dialogType)
        {
            switch (dialogType)
            {
                case HoyoToonDialogType.Success:
                    return "ht-dialog-status--success";
                case HoyoToonDialogType.Warning:
                    return "ht-dialog-status--warning";
                case HoyoToonDialogType.Error:
                    return "ht-dialog-status--error";
                case HoyoToonDialogType.Question:
                    return "ht-dialog-status--question";
                default:
                    return "ht-dialog-status--info";
            }
        }

        private static string GetButtonClass(string styleKind)
        {
            switch ((styleKind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "primary":
                    return "ht-btn-primary";
                case "ghost":
                    return "ht-btn-ghost";
                default:
                    return "ht-btn-secondary";
            }
        }
    }
}
#endif
