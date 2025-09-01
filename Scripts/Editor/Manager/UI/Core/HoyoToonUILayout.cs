using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.UI.Core
{
    /// <summary>
    /// Layout utilities for consistent spacing and positioning throughout HoyoToon UI
    /// </summary>
    public static class HoyoToonUILayout
    {
        #region Layout Constants

        public static class Spacing
        {
            public const float XSmall = 2f;
            public const float Small = 5f;
            public const float Medium = 10f;
            public const float Large = 15f;
            public const float XLarge = 20f;
        }

        public static class Sizes
        {
            // Window sizing
            public const float DEFAULT_WINDOW_WIDTH = 1200f;
            public const float DEFAULT_WINDOW_HEIGHT = 900f;
            public const float MIN_WINDOW_WIDTH = 1200f;
            public const float MIN_WINDOW_HEIGHT = 900f;

            // Component sizing
            public const float BANNER_HEIGHT = 150f;
            public const float TAB_HEIGHT = 30f;
            public const float STATUS_BAR_HEIGHT = 25f;
            public const float PREVIEW_PANEL_WIDTH = 420f;
            public const float MIN_CONTENT_WIDTH = 400f;

            // Button sizing
            public const float BUTTON_HEIGHT_SMALL = 24f;
            public const float BUTTON_HEIGHT_MEDIUM = 30f;
            public const float BUTTON_HEIGHT_LARGE = 40f;
            public const float BUTTON_MIN_WIDTH = 80f;
        }

        public static class BorderRadius
        {
            public const float Small = 3f;
            public const float Medium = 6f;
            public const float Large = 8f;
        }

        #endregion

        #region Layout Helpers

        /// <summary>
        /// Apply standard margin to an element
        /// </summary>
        public static void ApplyMargin(VisualElement element, float? top = null, float? right = null,
            float? bottom = null, float? left = null)
        {
            if (top.HasValue) element.style.marginTop = top.Value;
            if (right.HasValue) element.style.marginRight = right.Value;
            if (bottom.HasValue) element.style.marginBottom = bottom.Value;
            if (left.HasValue) element.style.marginLeft = left.Value;
        }

        /// <summary>
        /// Apply standard padding to an element
        /// </summary>
        public static void ApplyPadding(VisualElement element, float? top = null, float? right = null,
            float? bottom = null, float? left = null)
        {
            if (top.HasValue) element.style.paddingTop = top.Value;
            if (right.HasValue) element.style.paddingRight = right.Value;
            if (bottom.HasValue) element.style.paddingBottom = bottom.Value;
            if (left.HasValue) element.style.paddingLeft = left.Value;
        }

        /// <summary>
        /// Apply uniform margin to all sides
        /// </summary>
        public static void ApplyUniformMargin(VisualElement element, float margin)
        {
            ApplyMargin(element, margin, margin, margin, margin);
        }

        /// <summary>
        /// Apply uniform padding to all sides
        /// </summary>
        public static void ApplyUniformPadding(VisualElement element, float padding)
        {
            ApplyPadding(element, padding, padding, padding, padding);
        }

        /// <summary>
        /// Apply standard border radius
        /// </summary>
        public static void ApplyBorderRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>
        /// Apply standard border styling
        /// </summary>
        public static void ApplyBorder(VisualElement element, Color borderColor, float width = 1f)
        {
            element.style.borderTopColor = borderColor;
            element.style.borderRightColor = borderColor;
            element.style.borderBottomColor = borderColor;
            element.style.borderLeftColor = borderColor;

            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
        }

        /// <summary>
        /// Configure element as a flex row
        /// </summary>
        public static void MakeFlexRow(VisualElement element, Justify justifyContent = Justify.FlexStart,
            Align alignItems = Align.Stretch, Wrap flexWrap = Wrap.NoWrap)
        {
            element.style.flexDirection = FlexDirection.Row;
            element.style.justifyContent = justifyContent;
            element.style.alignItems = alignItems;
            element.style.flexWrap = flexWrap;
        }

        /// <summary>
        /// Configure element as a flex column
        /// </summary>
        public static void MakeFlexColumn(VisualElement element, Justify justifyContent = Justify.FlexStart,
            Align alignItems = Align.Stretch)
        {
            element.style.flexDirection = FlexDirection.Column;
            element.style.justifyContent = justifyContent;
            element.style.alignItems = alignItems;
        }

        /// <summary>
        /// Make an element fill available space
        /// </summary>
        public static void MakeFlexGrow(VisualElement element, float grow = 1f)
        {
            element.style.flexGrow = grow;
        }

        /// <summary>
        /// Prevent an element from shrinking
        /// </summary>
        public static void PreventShrink(VisualElement element)
        {
            element.style.flexShrink = 0;
        }

        /// <summary>
        /// Set minimum and maximum dimensions
        /// </summary>
        public static void SetDimensions(VisualElement element, float? minWidth = null, float? maxWidth = null,
            float? minHeight = null, float? maxHeight = null, float? width = null, float? height = null)
        {
            if (minWidth.HasValue) element.style.minWidth = minWidth.Value;
            if (maxWidth.HasValue) element.style.maxWidth = maxWidth.Value;
            if (minHeight.HasValue) element.style.minHeight = minHeight.Value;
            if (maxHeight.HasValue) element.style.maxHeight = maxHeight.Value;
            if (width.HasValue) element.style.width = width.Value;
            if (height.HasValue) element.style.height = height.Value;
        }

        #endregion

        #region Common Layout Patterns

        /// <summary>
        /// Create a centered content container
        /// </summary>
        public static VisualElement CreateCenteredContainer()
        {
            var container = new VisualElement();
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.flexGrow = 1;
            return container;
        }

        /// <summary>
        /// Create a two-column layout
        /// </summary>
        public static (VisualElement container, VisualElement leftColumn, VisualElement rightColumn) CreateTwoColumnLayout(
            float leftWidth = 50f, float rightWidth = 50f)
        {
            var container = new VisualElement();
            MakeFlexRow(container);

            var leftColumn = new VisualElement();
            leftColumn.style.width = Length.Percent(leftWidth);
            MakeFlexColumn(leftColumn);

            var rightColumn = new VisualElement();
            rightColumn.style.width = Length.Percent(rightWidth);
            MakeFlexColumn(rightColumn);

            container.Add(leftColumn);
            container.Add(rightColumn);

            return (container, leftColumn, rightColumn);
        }

        /// <summary>
        /// Create a header-content-footer layout
        /// </summary>
        public static (VisualElement container, VisualElement header, VisualElement content, VisualElement footer)
            CreateHeaderContentFooterLayout()
        {
            var container = new VisualElement();
            MakeFlexColumn(container);
            container.style.flexGrow = 1;

            var header = new VisualElement();
            PreventShrink(header);

            var content = new VisualElement();
            MakeFlexGrow(content);

            var footer = new VisualElement();
            PreventShrink(footer);

            container.Add(header);
            container.Add(content);
            container.Add(footer);

            return (container, header, content, footer);
        }

        /// <summary>
        /// Create a grid layout container
        /// </summary>
        public static VisualElement CreateGridContainer(int columns, float gap = Spacing.Medium)
        {
            var container = new VisualElement();
            MakeFlexRow(container, Justify.SpaceBetween, Align.Stretch, Wrap.Wrap);

            // Note: CSS Grid is not available in UIElements, so we use flexbox simulation
            return container;
        }

        /// <summary>
        /// Create a toolbar layout
        /// </summary>
        public static VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            MakeFlexRow(toolbar, Justify.SpaceBetween, Align.Center);
            toolbar.style.height = Sizes.TAB_HEIGHT;
            PreventShrink(toolbar);
            return toolbar;
        }

        #endregion

        #region Responsive Helpers

        /// <summary>
        /// Apply responsive behavior based on container width
        /// </summary>
        public static void ApplyResponsiveBehavior(VisualElement element, float breakpoint = 800f,
            Action<VisualElement> smallLayout = null, Action<VisualElement> largeLayout = null)
        {
            // Note: UIElements doesn't have built-in responsive capabilities
            // This would need to be implemented with resize callbacks
            element.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var width = evt.newRect.width;
                if (width < breakpoint)
                {
                    smallLayout?.Invoke(element);
                }
                else
                {
                    largeLayout?.Invoke(element);
                }
            });
        }

        #endregion

        #region Animation Helpers

        /// <summary>
        /// Simple fade in animation
        /// </summary>
        public static void FadeIn(VisualElement element, float duration = 0.3f)
        {
            element.style.opacity = 0f;
            element.style.display = DisplayStyle.Flex;

            // Note: UIElements has limited animation support
            // For production, consider using DOTween or Unity's Animation system
            var startTime = Time.realtimeSinceStartup;
            void UpdateOpacity()
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                element.style.opacity = progress;

                if (progress < 1f)
                {
                    EditorApplication.delayCall += UpdateOpacity;
                }
            }
            UpdateOpacity();
        }

        /// <summary>
        /// Simple fade out animation
        /// </summary>
        public static void FadeOut(VisualElement element, float duration = 0.3f, Action onComplete = null)
        {
            var startTime = Time.realtimeSinceStartup;
            var startOpacity = element.style.opacity.value;

            void UpdateOpacity()
            {
                var elapsed = Time.realtimeSinceStartup - startTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                element.style.opacity = startOpacity * (1f - progress);

                if (progress < 1f)
                {
                    EditorApplication.delayCall += UpdateOpacity;
                }
                else
                {
                    element.style.display = DisplayStyle.None;
                    onComplete?.Invoke();
                }
            }
            UpdateOpacity();
        }

        #endregion
    }
}