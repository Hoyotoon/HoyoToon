#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Dialogs
{
    internal sealed class HoyoToonProgressWindow : EditorWindow
    {
        private static readonly Vector2 DefaultSize = new Vector2(520f, 300f);
        private static readonly Vector2 MinimumSize = new Vector2(480f, 280f);
        private static readonly Vector2 MaximumSize = new Vector2(1600f, 1000f);
        private const float MainWindowMargin = 80f;
        private static HoyoToonProgressWindow current;

        private HoyoToonDialogShell shell;
        private Label statusLabel;
        private Label messageLabel;
        private Label percentLabel;
        private VisualElement progressFill;
        private Button cancelButton;
        private string currentTitle = "HoyoToon Progress";
        private string currentMessage = "Working...";
        private float currentProgress;
        private bool currentIndeterminate;
        private bool currentCanCancel;
        private bool opened;
        private bool closingFromApi;

        internal bool CancelRequested { get; private set; }

        internal static HoyoToonProgressWindow GetOrCreate(string title)
        {
            if (current == null)
            {
                current = UnityEngine.Resources.FindObjectsOfTypeAll<HoyoToonProgressWindow>().FirstOrDefault();
            }

            if (current == null)
            {
                current = CreateInstance<HoyoToonProgressWindow>();
                current.titleContent = new GUIContent(NormalizeTitle(title));
                current.ApplyResizeLimits();
                current.position = GetCenteredPosition(DefaultSize);
            }

            if (!current.opened)
            {
                current.ShowUtility();
                current.opened = true;
            }

            return current;
        }

        internal static void CloseCurrent()
        {
            if (current == null)
            {
                return;
            }

            current.closingFromApi = true;
            current.Close();
            current = null;
        }

        internal void SetProgress(string title, string message, float progress, bool canCancel)
        {
            string normalizedTitle = NormalizeTitle(title);
            if (!string.Equals(currentTitle, normalizedTitle, System.StringComparison.Ordinal))
            {
                CancelRequested = false;
            }

            currentTitle = normalizedTitle;
            currentMessage = string.IsNullOrWhiteSpace(message) ? "Working..." : message.Trim();
            currentIndeterminate = progress < 0f;
            currentProgress = Mathf.Clamp01(progress);
            currentCanCancel = canCancel;
            if (!canCancel)
            {
                CancelRequested = false;
            }

            titleContent = new GUIContent(currentTitle);
            shell?.SetHeader(currentTitle, "Progress");
            RefreshBody();
            Repaint();
        }

        private void OnEnable()
        {
            current = this;
            titleContent = new GUIContent(currentTitle);
            ApplyResizeLimits();
        }

        private void OnDisable()
        {
            opened = false;
            if (ReferenceEquals(current, this))
            {
                current = null;
            }
        }

        private void OnDestroy()
        {
            if (currentCanCancel && !closingFromApi)
            {
                CancelRequested = true;
            }
        }

        public void CreateGUI()
        {
            if (!HoyoToonDialogShell.TryBuild(this, currentTitle, "Progress", out shell, out string error))
            {
                BuildFallback(error);
                return;
            }

            shell.BodyContent.Clear();
            shell.ActionContent.Clear();
            shell.ActionContent.style.display = DisplayStyle.None;
            shell.BodyContent.Add(CreateProgressBody());
            RefreshBody();
            rootVisualElement.schedule.Execute(ApplyResizeLimits);
        }

        private VisualElement CreateProgressBody()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            card.AddToClassList("ht-progress-card");

            VisualElement summaryRow = new VisualElement();
            summaryRow.AddToClassList("ht-row");
            summaryRow.AddToClassList("ht-gap-8");
            summaryRow.AddToClassList("ht-progress-summary-row");

            statusLabel = new Label("In progress");
            statusLabel.AddToClassList("ht-dialog-status");
            statusLabel.AddToClassList("ht-dialog-status--info");
            summaryRow.Add(statusLabel);

            percentLabel = new Label("0%");
            percentLabel.AddToClassList("ht-progress-percent");
            summaryRow.Add(percentLabel);
            card.Add(summaryRow);

            messageLabel = new Label(currentMessage);
            messageLabel.AddToClassList("ht-progress-message");
            card.Add(messageLabel);

            VisualElement progressTrack = new VisualElement();
            progressTrack.AddToClassList("ht-progress-track");
            progressFill = new VisualElement();
            progressFill.AddToClassList("ht-progress-fill");
            progressTrack.Add(progressFill);
            card.Add(progressTrack);

            VisualElement cancelRow = new VisualElement();
            cancelRow.AddToClassList("ht-row");
            cancelRow.AddToClassList("ht-progress-cancel-row");

            cancelButton = new Button(() =>
            {
                CancelRequested = true;
                RefreshBody();
            })
            {
                text = "Cancel"
            };
            cancelButton.AddToClassList("ht-btn-secondary");
            cancelButton.AddToClassList("ht-progress-cancel-button");
            cancelRow.Add(cancelButton);
            card.Add(cancelRow);

            return card;
        }

        private void RefreshBody()
        {
            if (messageLabel != null)
            {
                messageLabel.text = currentMessage;
            }

            if (statusLabel != null)
            {
                statusLabel.text = CancelRequested ? "Cancelling" : "In progress";
            }

            if (percentLabel != null)
            {
                percentLabel.text = currentIndeterminate
                    ? "Working"
                    : Mathf.RoundToInt(currentProgress * 100f) + "%";
            }

            if (progressFill != null)
            {
                progressFill.EnableInClassList("ht-progress-fill--indeterminate", currentIndeterminate);
                progressFill.style.width = currentIndeterminate
                    ? new Length(35f, LengthUnit.Percent)
                    : new Length(Mathf.Clamp01(currentProgress) * 100f, LengthUnit.Percent);
            }

            if (cancelButton != null)
            {
                cancelButton.style.display = currentCanCancel ? DisplayStyle.Flex : DisplayStyle.None;
                cancelButton.SetEnabled(!CancelRequested);
                cancelButton.text = CancelRequested ? "Cancelling" : "Cancel";
            }
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
                string.IsNullOrWhiteSpace(message) ? "The HoyoToon progress window could not be created." : message,
                HelpBoxMessageType.Error));
        }

        private static string NormalizeTitle(string title)
        {
            return string.IsNullOrWhiteSpace(title) ? "HoyoToon Progress" : title.Trim();
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

        private void ApplyResizeLimits()
        {
            minSize = MinimumSize;
            maxSize = MaximumSize;
        }
    }
}
#endif
