#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Onboarding
{
    internal sealed class OnboardingOverlay : VisualElement
    {
        private readonly List<VisualElement> dynamicChildren = new List<VisualElement>();
        private OnboardingStep currentStep;
        private string reminderText = string.Empty;
        private double reminderUntil;
        private string flashTargetId = string.Empty;
        private double flashUntil;

        private struct TargetRect
        {
            public TargetRect(string id, Rect rect)
            {
                Id = id;
                Rect = rect;
            }

            public string Id { get; }
            public Rect Rect { get; }
        }

        public bool DimmingEnabled { get; set; } = true;
        public bool DrawAllTargets { get; set; }

        public OnboardingOverlay()
        {
            name = "HoyoToonOnboardingOverlay";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            style.display = DisplayStyle.None;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                schedule.Execute(Refresh).Every(33);
            });
        }

        public void SetStep(OnboardingStep step)
        {
            currentStep = step;
            style.display = step != null ? DisplayStyle.Flex : DisplayStyle.None;
            Refresh();
        }

        public void ShowReminder(string text)
        {
            reminderText = string.IsNullOrWhiteSpace(text) ? "Follow the highlighted step first." : text.Trim();
            reminderUntil = EditorApplication.timeSinceStartup + 1.7d;
            Refresh();
        }

        public void Flash(string targetId)
        {
            flashTargetId = targetId ?? string.Empty;
            flashUntil = EditorApplication.timeSinceStartup + 1.8d;
            Refresh();
        }

        private void Refresh()
        {
            style.display = currentStep != null || DrawAllTargets ? DisplayStyle.Flex : DisplayStyle.None;
            ClearDynamicChildren();

            if (currentStep == null && !DrawAllTargets)
            {
                style.backgroundColor = Color.clear;
                return;
            }

            style.backgroundColor = Color.clear;

            if (DrawAllTargets)
            {
                DrawDebugTargets();
            }

            if (currentStep == null)
            {
                return;
            }

            DrawStepTargets();
        }

        private void DrawStepTargets()
        {
            IReadOnlyList<string> ids = currentStep.HighlightTargets;
            if (ids == null || ids.Count <= 0)
            {
                DrawFullDimming();
                return;
            }

            var targets = new List<TargetRect>();
            for (int index = 0; index < ids.Count; index++)
            {
                OnboardingTarget target = OnboardingTargetRegistry.Get(ids[index]);
                if (!CanDrawTarget(target))
                {
                    continue;
                }

                Rect worldRect;
                if (!OnboardingTargetRegistry.TryGetVisibleWorldRect(target, out worldRect))
                {
                    continue;
                }

                Rect localRect = GetDrawableRect(ToLocalRect(worldRect), target.Id);
                if (localRect.width <= 1f || localRect.height <= 1f)
                {
                    continue;
                }

                targets.Add(new TargetRect(target.Id, localRect));
            }

            if (targets.Count <= 0)
            {
                DrawFullDimming();
                return;
            }

            if (DimmingEnabled)
            {
                DrawDimmingAround(GetUnionRect(targets));
            }

            bool drewPrimary = false;
            Rect primaryRect = Rect.zero;
            for (int index = 0; index < targets.Count; index++)
            {
                bool primary = !drewPrimary;
                if (primary)
                {
                    primaryRect = targets[index].Rect;
                    drewPrimary = true;
                }

                DrawHighlight(targets[index].Rect, primary, targets[index].Id);
            }

            if (drewPrimary)
            {
                DrawReminderLabel(primaryRect);
            }
        }

        private void DrawHighlight(Rect rect, bool primary, string targetId)
        {
            double now = EditorApplication.timeSinceStartup;
            float wave = 0.5f + 0.5f * Mathf.Sin((float)now * 8.5f);
            float pulse = Mathf.SmoothStep(0f, 1f, wave);
            bool flashed = !string.IsNullOrWhiteSpace(flashTargetId)
                && string.Equals(flashTargetId, targetId, StringComparison.Ordinal)
                && now < flashUntil;

            float padding = primary ? 5f + pulse * 2f : 3f;
            VisualElement halo = new VisualElement();
            halo.pickingMode = PickingMode.Ignore;
            halo.style.position = Position.Absolute;
            halo.style.left = rect.xMin - padding;
            halo.style.top = rect.yMin - padding;
            halo.style.width = rect.width + padding * 2f;
            halo.style.height = rect.height + padding * 2f;
            halo.style.borderTopLeftRadius = 7f;
            halo.style.borderTopRightRadius = 7f;
            halo.style.borderBottomLeftRadius = 7f;
            halo.style.borderBottomRightRadius = 7f;
            halo.style.borderLeftWidth = primary ? 3f : 2f;
            halo.style.borderRightWidth = primary ? 3f : 2f;
            halo.style.borderTopWidth = primary ? 3f : 2f;
            halo.style.borderBottomWidth = primary ? 3f : 2f;
            Color borderColor = primary
                ? new Color(0.42f, 0.86f, 1f, flashed ? 0.95f : 0.64f + pulse * 0.18f)
                : new Color(0.72f, 0.62f, 1f, 0.68f);
            halo.style.borderLeftColor = borderColor;
            halo.style.borderRightColor = borderColor;
            halo.style.borderTopColor = borderColor;
            halo.style.borderBottomColor = borderColor;
            halo.style.backgroundColor = primary
                ? new Color(0.1f, 0.42f, 0.7f, 0.018f + pulse * 0.018f)
                : new Color(0.45f, 0.35f, 0.8f, 0.06f);
            AddDynamic(halo);
        }

        private void DrawFullDimming()
        {
            if (!DimmingEnabled || currentStep == null)
            {
                return;
            }

            AddDimmingPanel(new Rect(0f, 0f, Mathf.Max(1f, layout.width), Mathf.Max(1f, layout.height)));
        }

        private void DrawDimmingAround(Rect clearRect)
        {
            float rootWidth = Mathf.Max(1f, layout.width);
            float rootHeight = Mathf.Max(1f, layout.height);
            Rect paddedClearRect = ClampToRootRect(
                new Rect(clearRect.x - 8f, clearRect.y - 8f, clearRect.width + 16f, clearRect.height + 16f),
                rootWidth,
                rootHeight);

            AddDimmingPanel(new Rect(0f, 0f, rootWidth, paddedClearRect.yMin));
            AddDimmingPanel(new Rect(0f, paddedClearRect.yMax, rootWidth, rootHeight - paddedClearRect.yMax));
            AddDimmingPanel(new Rect(0f, paddedClearRect.yMin, paddedClearRect.xMin, paddedClearRect.height));
            AddDimmingPanel(new Rect(paddedClearRect.xMax, paddedClearRect.yMin, rootWidth - paddedClearRect.xMax, paddedClearRect.height));
        }

        private void AddDimmingPanel(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            VisualElement dimmingPanel = new VisualElement();
            dimmingPanel.pickingMode = PickingMode.Ignore;
            dimmingPanel.style.position = Position.Absolute;
            dimmingPanel.style.left = rect.x;
            dimmingPanel.style.top = rect.y;
            dimmingPanel.style.width = rect.width;
            dimmingPanel.style.height = rect.height;
            dimmingPanel.style.backgroundColor = new Color(0f, 0f, 0f, 0.48f);
            AddDynamic(dimmingPanel);
        }

        private static Rect GetUnionRect(IReadOnlyList<TargetRect> targets)
        {
            Rect unionRect = targets[0].Rect;
            for (int index = 1; index < targets.Count; index++)
            {
                Rect rect = targets[index].Rect;
                unionRect = Rect.MinMaxRect(
                    Mathf.Min(unionRect.xMin, rect.xMin),
                    Mathf.Min(unionRect.yMin, rect.yMin),
                    Mathf.Max(unionRect.xMax, rect.xMax),
                    Mathf.Max(unionRect.yMax, rect.yMax));
            }

            return unionRect;
        }

        private static Rect ClampToRootRect(Rect rect, float rootWidth, float rootHeight)
        {
            float xMin = Mathf.Clamp(rect.xMin, 0f, rootWidth);
            float yMin = Mathf.Clamp(rect.yMin, 0f, rootHeight);
            float xMax = Mathf.Clamp(rect.xMax, xMin, rootWidth);
            float yMax = Mathf.Clamp(rect.yMax, yMin, rootHeight);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void DrawReminderLabel(Rect targetRect)
        {
            if (EditorApplication.timeSinceStartup >= reminderUntil || string.IsNullOrWhiteSpace(reminderText))
            {
                return;
            }

            const float width = 292f;
            const float minHeight = 48f;
            Rect labelRect = ChooseReminderRect(targetRect, width, minHeight);
            Label label = new Label(reminderText.Trim());
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = labelRect.x;
            label.style.top = labelRect.y;
            label.style.width = labelRect.width;
            label.style.minHeight = minHeight;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 10f;
            label.style.paddingRight = 10f;
            label.style.paddingTop = 8f;
            label.style.paddingBottom = 8f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12f;
            label.style.color = Color.white;
            label.style.backgroundColor = new Color(0.08f, 0.09f, 0.13f, 0.96f);
            label.style.borderTopLeftRadius = 6f;
            label.style.borderTopRightRadius = 6f;
            label.style.borderBottomLeftRadius = 6f;
            label.style.borderBottomRightRadius = 6f;
            label.style.borderLeftWidth = 1f;
            label.style.borderRightWidth = 1f;
            label.style.borderTopWidth = 1f;
            label.style.borderBottomWidth = 1f;
            Color border = new Color(0.42f, 0.86f, 1f, 0.8f);
            label.style.borderLeftColor = border;
            label.style.borderRightColor = border;
            label.style.borderTopColor = border;
            label.style.borderBottomColor = border;
            AddDynamic(label);
        }

        private Rect ChooseReminderRect(Rect targetRect, float width, float minHeight)
        {
            float rootWidth = Mathf.Max(1f, layout.width);
            float rootHeight = Mathf.Max(1f, layout.height);
            Rect bottom = new Rect((rootWidth - width) * 0.5f, rootHeight - minHeight - 16f, width, minHeight);
            if (!bottom.Overlaps(targetRect))
            {
                return ClampRect(bottom, rootWidth, rootHeight);
            }

            Rect top = new Rect((rootWidth - width) * 0.5f, 16f, width, minHeight);
            return ClampRect(top, rootWidth, rootHeight);
        }

        private static Rect ClampRect(Rect rect, float rootWidth, float rootHeight)
        {
            rect.x = Mathf.Clamp(rect.x, 8f, Mathf.Max(8f, rootWidth - rect.width - 8f));
            rect.y = Mathf.Clamp(rect.y, 8f, Mathf.Max(8f, rootHeight - rect.height - 8f));
            return rect;
        }

        private void DrawDebugTargets()
        {
            foreach (OnboardingTarget target in OnboardingTargetRegistry.AllTargets)
            {
                if (!CanDrawTarget(target))
                {
                    continue;
                }

                Rect worldRect;
                if (!OnboardingTargetRegistry.TryGetVisibleWorldRect(target, out worldRect))
                {
                    continue;
                }

                Rect localRect = ToLocalRect(worldRect);
                if (localRect.width <= 1f || localRect.height <= 1f)
                {
                    continue;
                }

                VisualElement debugRect = new VisualElement();
                debugRect.pickingMode = PickingMode.Ignore;
                debugRect.style.position = Position.Absolute;
                debugRect.style.left = localRect.xMin;
                debugRect.style.top = localRect.yMin;
                debugRect.style.width = localRect.width;
                debugRect.style.height = localRect.height;
                debugRect.style.borderLeftWidth = 1f;
                debugRect.style.borderRightWidth = 1f;
                debugRect.style.borderTopWidth = 1f;
                debugRect.style.borderBottomWidth = 1f;
                Color debugColor = target.Available ? new Color(0.2f, 1f, 0.45f, 0.45f) : new Color(1f, 0.25f, 0.25f, 0.45f);
                debugRect.style.borderLeftColor = debugColor;
                debugRect.style.borderRightColor = debugColor;
                debugRect.style.borderTopColor = debugColor;
                debugRect.style.borderBottomColor = debugColor;
                AddDynamic(debugRect);
            }
        }

        private bool CanDrawTarget(OnboardingTarget target)
        {
            if (target == null || !target.Available || panel == null)
            {
                return false;
            }

            VisualElement element = target.Element;
            return element != null && element.panel == panel;
        }

        private Rect ToLocalRect(Rect worldRect)
        {
            Rect overlayWorldRect = worldBound;
            return Rect.MinMaxRect(
                worldRect.xMin - overlayWorldRect.xMin,
                worldRect.yMin - overlayWorldRect.yMin,
                worldRect.xMax - overlayWorldRect.xMin,
                worldRect.yMax - overlayWorldRect.yMin);
        }

        private Rect GetDrawableRect(Rect rect, string targetId)
        {
            float rootWidth = Mathf.Max(1f, layout.width);
            float rootHeight = Mathf.Max(1f, layout.height);
            Rect clipped = ClampToRootRect(rect, rootWidth, rootHeight);
            if (clipped.width <= 1f || clipped.height <= 1f)
            {
                return Rect.zero;
            }

            if (!string.Equals(targetId, "Manager.Window", StringComparison.Ordinal))
            {
                float rootArea = rootWidth * rootHeight;
                float rectArea = clipped.width * clipped.height;
                bool almostFullWidth = clipped.width >= rootWidth * 0.92f;
                bool almostFullHeight = clipped.height >= rootHeight * 0.82f;
                if (rootArea > 1f && rectArea / rootArea > 0.84f && (almostFullWidth || almostFullHeight))
                {
                    return Rect.zero;
                }
            }

            return clipped;
        }

        private void AddDynamic(VisualElement element)
        {
            dynamicChildren.Add(element);
            Add(element);
        }

        private void ClearDynamicChildren()
        {
            for (int index = 0; index < dynamicChildren.Count; index++)
            {
                Remove(dynamicChildren[index]);
            }

            dynamicChildren.Clear();
        }
    }
}
#endif
