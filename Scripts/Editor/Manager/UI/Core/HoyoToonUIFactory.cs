using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.UI.Core
{
    /// <summary>
    /// Factory class for creating styled UI elements used throughout HoyoToon UI
    /// Centralizes all UI element creation and styling for consistency
    /// </summary>
    public static class HoyoToonUIFactory
    {
        #region Color Constants

        public static class Colors
        {
            // Primary colors
            public static readonly Color Primary = new Color(0.3f, 0.5f, 0.7f);
            public static readonly Color Secondary = new Color(0.4f, 0.4f, 0.4f);
            public static readonly Color Success = new Color(0.3f, 0.7f, 0.3f);
            public static readonly Color Warning = new Color(0.9f, 0.6f, 0.1f);
            public static readonly Color Error = new Color(0.9f, 0.2f, 0.2f);

            // Background colors
            public static readonly Color BackgroundDark = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            public static readonly Color BackgroundMedium = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            public static readonly Color BackgroundLight = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Border colors
            public static readonly Color BorderDefault = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            public static readonly Color BorderActive = new Color(0.4f, 0.5f, 0.6f, 0.8f);

            // Text colors
            public static readonly Color TextPrimary = Color.white;
            public static readonly Color TextSecondary = new Color(0.8f, 0.8f, 0.8f);
            public static readonly Color TextMuted = new Color(0.7f, 0.7f, 0.7f);
            public static readonly Color TextHeader = new Color(0.8f, 0.9f, 1f);

            // Status indicator colors
            public static readonly Color StatusReady = Color.green;
            public static readonly Color StatusWarning = Color.yellow;
            public static readonly Color StatusError = Color.red;
            public static readonly Color StatusNeutral = Color.gray;
        }

        #endregion

        #region Headers and Labels

        /// <summary>
        /// Create a section header with consistent HoyoToon styling
        /// </summary>
        public static Label CreateSectionHeader(string title)
        {
            var header = new Label(title);
            header.style.fontSize = 16;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.TextHeader;
            header.style.marginTop = 15;
            header.style.marginBottom = 10;
            header.style.paddingLeft = 5;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDefault;
            return header;
        }

        /// <summary>
        /// Create a subsection header with consistent HoyoToon styling
        /// </summary>
        public static Label CreateSubsectionHeader(string title)
        {
            var header = new Label(title);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.7f, 0.8f, 0.9f);
            header.style.marginTop = 10;
            header.style.marginBottom = 5;
            header.style.paddingLeft = 3;
            return header;
        }

        /// <summary>
        /// Create a standard text label with HoyoToon styling
        /// </summary>
        public static Label CreateLabel(string text, int fontSize = 12, Color? textColor = null)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = textColor ?? Colors.TextPrimary;
            return label;
        }

        #endregion

        #region Information Rows

        /// <summary>
        /// Create an information row with label and value in a flex layout
        /// </summary>
        public static VisualElement CreateInfoRow(string label, string value, Color? valueColor = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 3;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;

            var labelElement = new Label(label);
            labelElement.style.color = Colors.TextSecondary;
            labelElement.style.flexGrow = 0;
            labelElement.style.flexShrink = 0;
            labelElement.style.minWidth = 120;

            var valueElement = new Label(value);
            valueElement.style.color = valueColor ?? Colors.TextPrimary;
            valueElement.style.flexGrow = 1;
            valueElement.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(labelElement);
            row.Add(valueElement);
            return row;
        }

        #endregion

        #region Message Boxes

        /// <summary>
        /// Create a warning message box
        /// </summary>
        public static VisualElement CreateWarningBox(string message)
        {
            return CreateMessageBox(message, "⚠", Colors.Warning, new Color(0.6f, 0.4f, 0.1f, 0.3f));
        }

        /// <summary>
        /// Create an error message box
        /// </summary>
        public static VisualElement CreateErrorBox(string message)
        {
            return CreateMessageBox(message, "✗", Colors.Error, new Color(0.6f, 0.1f, 0.1f, 0.3f));
        }

        /// <summary>
        /// Create a success message box
        /// </summary>
        public static VisualElement CreateSuccessBox(string message)
        {
            return CreateMessageBox(message, "✓", new Color(0.2f, 0.9f, 0.2f), new Color(0.1f, 0.6f, 0.1f, 0.3f));
        }

        /// <summary>
        /// Create an info message box
        /// </summary>
        public static VisualElement CreateInfoBox(string message, Color? backgroundColor = null, Color? textColor = null)
        {
            return CreateMessageBox(message, "ℹ", textColor ?? new Color(0.8f, 0.9f, 1.0f),
                backgroundColor ?? new Color(0.2f, 0.3f, 0.4f));
        }

        /// <summary>
        /// Internal method to create a styled message box
        /// </summary>
        private static VisualElement CreateMessageBox(string message, string icon, Color iconColor, Color backgroundColor)
        {
            var box = new VisualElement();
            box.style.backgroundColor = backgroundColor;
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = iconColor;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 10;
            box.style.paddingRight = 10;
            box.style.marginTop = 5;
            box.style.marginBottom = 5;

            var contentContainer = new VisualElement();
            contentContainer.style.flexDirection = FlexDirection.Row;
            contentContainer.style.alignItems = Align.Center;

            var iconLabel = new Label(icon);
            iconLabel.style.color = iconColor;
            iconLabel.style.fontSize = 14;
            iconLabel.style.marginRight = 8;
            iconLabel.style.flexShrink = 0;

            var messageLabel = new Label(message);
            messageLabel.style.color = iconColor;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.flexGrow = 1;

            contentContainer.Add(iconLabel);
            contentContainer.Add(messageLabel);
            box.Add(contentContainer);

            return box;
        }

        #endregion

        #region Buttons

        /// <summary>
        /// Create a styled button with HoyoToon theming
        /// </summary>
        public static Button CreateStyledButton(string text, Action onClick, Color? backgroundColor = null, int height = 30)
        {
            var button = new Button(onClick);
            button.text = text;
            button.style.height = height;
            button.style.minWidth = 100;
            button.style.backgroundColor = backgroundColor ?? Colors.Primary;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.color = Colors.TextPrimary;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.marginTop = 5;
            button.style.marginBottom = 5;

            // Add hover effects
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                var currentBg = backgroundColor ?? Colors.Primary;
                button.style.backgroundColor = new Color(currentBg.r * 1.2f, currentBg.g * 1.2f, currentBg.b * 1.2f, currentBg.a);
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = backgroundColor ?? Colors.Primary;
            });

            return button;
        }

        /// <summary>
        /// Create a primary action button
        /// </summary>
        public static Button CreatePrimaryButton(string text, Action onClick)
        {
            return CreateStyledButton(text, onClick, Colors.Primary, 40);
        }

        /// <summary>
        /// Create a secondary action button
        /// </summary>
        public static Button CreateSecondaryButton(string text, Action onClick)
        {
            return CreateStyledButton(text, onClick, Colors.Secondary, 30);
        }

        /// <summary>
        /// Create a success action button
        /// </summary>
        public static Button CreateSuccessButton(string text, Action onClick)
        {
            return CreateStyledButton(text, onClick, Colors.Success, 30);
        }

        /// <summary>
        /// Create a warning action button
        /// </summary>
        public static Button CreateWarningButton(string text, Action onClick)
        {
            return CreateStyledButton(text, onClick, Colors.Warning, 30);
        }

        /// <summary>
        /// Create an error/danger action button
        /// </summary>
        public static Button CreateDangerButton(string text, Action onClick)
        {
            return CreateStyledButton(text, onClick, Colors.Error, 30);
        }

        #endregion

        #region Input Fields

        /// <summary>
        /// Create a styled text field with HoyoToon theming
        /// </summary>
        public static TextField CreateTextField(string label = "", string value = "")
        {
            var textField = new TextField(label);
            textField.value = value;
            ApplyInputFieldStyling(textField);
            return textField;
        }

        /// <summary>
        /// Create a styled dropdown field with HoyoToon theming
        /// </summary>
        public static DropdownField CreateDropdown(List<string> choices = null, int defaultIndex = 0)
        {
            var dropdown = choices != null ? new DropdownField(choices, defaultIndex) : new DropdownField();
            ApplyInputFieldStyling(dropdown);
            return dropdown;
        }

        /// <summary>
        /// Apply consistent styling to input fields
        /// </summary>
        private static void ApplyInputFieldStyling(VisualElement inputField)
        {
            // Background color
            inputField.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.24f, 0.24f, 1f)
                : new Color(0.9f, 0.9f, 0.9f, 1f);

            // Border styling
            var borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.35f, 0.35f, 1f)
                : new Color(0.6f, 0.6f, 0.6f, 1f);

            inputField.style.borderLeftColor = borderColor;
            inputField.style.borderRightColor = borderColor;
            inputField.style.borderTopColor = borderColor;
            inputField.style.borderBottomColor = borderColor;

            inputField.style.borderLeftWidth = 1;
            inputField.style.borderRightWidth = 1;
            inputField.style.borderTopWidth = 1;
            inputField.style.borderBottomWidth = 1;

            // Border radius for rounded corners
            inputField.style.borderTopLeftRadius = 3;
            inputField.style.borderTopRightRadius = 3;
            inputField.style.borderBottomLeftRadius = 3;
            inputField.style.borderBottomRightRadius = 3;

            // Padding for better text spacing
            inputField.style.paddingLeft = 4;
            inputField.style.paddingRight = 4;
            inputField.style.paddingTop = 2;
            inputField.style.paddingBottom = 2;

            // Minimum height for consistency
            inputField.style.minHeight = 20;
        }

        #endregion

        #region Containers

        /// <summary>
        /// Create a styled container with HoyoToon theming
        /// </summary>
        public static VisualElement CreateContainer(ContainerStyle style = ContainerStyle.Default)
        {
            var container = new VisualElement();
            ApplyContainerStyling(container, style);
            return container;
        }

        /// <summary>
        /// Create a card-style container with padding and borders
        /// </summary>
        public static VisualElement CreateCard()
        {
            return CreateContainer(ContainerStyle.Card);
        }

        /// <summary>
        /// Create a panel-style container for grouping content
        /// </summary>
        public static VisualElement CreatePanel()
        {
            return CreateContainer(ContainerStyle.Panel);
        }

        /// <summary>
        /// Apply container styling based on the specified style
        /// </summary>
        private static void ApplyContainerStyling(VisualElement container, ContainerStyle style)
        {
            switch (style)
            {
                case ContainerStyle.Card:
                    container.style.backgroundColor = Colors.BackgroundMedium;
                    container.style.borderTopLeftRadius = 8;
                    container.style.borderTopRightRadius = 8;
                    container.style.borderBottomLeftRadius = 8;
                    container.style.borderBottomRightRadius = 8;
                    container.style.borderTopWidth = 1;
                    container.style.borderBottomWidth = 1;
                    container.style.borderLeftWidth = 1;
                    container.style.borderRightWidth = 1;
                    container.style.borderTopColor = Colors.BorderDefault;
                    container.style.borderBottomColor = Colors.BorderDefault;
                    container.style.borderLeftColor = Colors.BorderDefault;
                    container.style.borderRightColor = Colors.BorderDefault;
                    container.style.paddingTop = 10;
                    container.style.paddingBottom = 10;
                    container.style.paddingLeft = 10;
                    container.style.paddingRight = 10;
                    container.style.marginTop = 5;
                    container.style.marginBottom = 5;
                    break;

                case ContainerStyle.Panel:
                    container.style.backgroundColor = Colors.BackgroundLight;
                    container.style.borderTopLeftRadius = 6;
                    container.style.borderTopRightRadius = 6;
                    container.style.borderBottomLeftRadius = 6;
                    container.style.borderBottomRightRadius = 6;
                    container.style.paddingTop = 8;
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 12;
                    container.style.paddingRight = 12;
                    container.style.marginTop = 5;
                    container.style.marginBottom = 5;
                    break;

                case ContainerStyle.Default:
                default:
                    // Minimal styling for default containers
                    container.style.marginTop = 2;
                    container.style.marginBottom = 2;
                    break;
            }
        }

        #endregion

        #region Foldouts

        /// <summary>
        /// Create a styled foldout with HoyoToon theming
        /// </summary>
        public static Foldout CreateFoldout(string title, bool defaultValue = false)
        {
            var foldout = new Foldout();
            foldout.text = title;
            foldout.value = defaultValue;
            foldout.style.marginTop = 5;
            foldout.style.marginBottom = 5;

            // Style the toggle
            var toggle = foldout.Q<Toggle>();
            if (toggle != null)
            {
                toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
                toggle.style.color = Colors.TextHeader;
            }

            return foldout;
        }

        #endregion

        #region Progress and Status

        /// <summary>
        /// Create a progress indicator with percentage and color coding
        /// </summary>
        public static VisualElement CreateProgressIndicator(string label, int percentage)
        {
            var container = CreateContainer(ContainerStyle.Panel);

            var headerLabel = new Label(label);
            headerLabel.style.fontSize = 14;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Color based on progress
            Color progressColor;
            if (percentage >= 80)
                progressColor = Colors.StatusReady;
            else if (percentage >= 60)
                progressColor = Colors.StatusWarning;
            else
                progressColor = Colors.StatusError;

            headerLabel.style.color = progressColor;

            var progressBar = new ProgressBar();
            progressBar.value = percentage;
            progressBar.style.marginTop = 5;

            container.Add(headerLabel);
            container.Add(progressBar);

            return container;
        }

        /// <summary>
        /// Create a status indicator with colored dot and text
        /// </summary>
        public static VisualElement CreateStatusIndicator(string text, StatusColor status)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var dot = new Label("●");
            dot.style.fontSize = 12;
            dot.style.marginRight = 5;

            Color statusColor;
            switch (status)
            {
                case StatusColor.Success:
                    statusColor = Colors.StatusReady;
                    break;
                case StatusColor.Warning:
                    statusColor = Colors.StatusWarning;
                    break;
                case StatusColor.Error:
                    statusColor = Colors.StatusError;
                    break;
                case StatusColor.Neutral:
                default:
                    statusColor = Colors.StatusNeutral;
                    break;
            }

            dot.style.color = statusColor;

            var label = new Label(text);
            label.style.color = Colors.TextPrimary;
            label.style.fontSize = 12;

            container.Add(dot);
            container.Add(label);

            return container;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Create a horizontal separator line
        /// </summary>
        public static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = Colors.BorderDefault;
            separator.style.marginTop = 10;
            separator.style.marginBottom = 10;
            return separator;
        }

        /// <summary>
        /// Create a spacer element for layout
        /// </summary>
        public static VisualElement CreateSpacer(int height = 10)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            return spacer;
        }

        /// <summary>
        /// Create a flex row container
        /// </summary>
        public static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        /// <summary>
        /// Create a flex column container
        /// </summary>
        public static VisualElement CreateColumn()
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            return column;
        }

        #endregion

        #region Enums

        public enum ContainerStyle
        {
            Default,
            Card,
            Panel
        }

        public enum StatusColor
        {
            Success,
            Warning,
            Error,
            Neutral
        }

        #endregion
    }
}