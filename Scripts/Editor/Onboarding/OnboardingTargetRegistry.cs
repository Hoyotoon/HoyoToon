#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Onboarding
{
    internal sealed class OnboardingTarget
    {
        public string Id;
        public Func<Rect> GetRect;
        public Func<bool> IsAvailable;
        public Action Focus;
        public Action<bool> SetInteractable;
        public Action<Action> BindClick;
        public Action<Action<object>> BindValueChanged;
        public string DisplayName;
        public string ModuleName;
        public WeakReference<VisualElement> ElementReference;

        public Rect Rect
        {
            get
            {
                try
                {
                    return GetRect != null ? GetRect() : Rect.zero;
                }
                catch
                {
                    return Rect.zero;
                }
            }
        }

        public bool Available
        {
            get
            {
                try
                {
                    return IsAvailable != null && IsAvailable();
                }
                catch
                {
                    return false;
                }
            }
        }

        public VisualElement Element
        {
            get
            {
                if (ElementReference == null)
                {
                    return null;
                }

                VisualElement element;
                return ElementReference.TryGetTarget(out element) ? element : null;
            }
        }
    }

    internal static class OnboardingTargetRegistry
    {
        private static readonly List<OnboardingTarget> Targets = new List<OnboardingTarget>();

        public static event Action TargetsChanged;

        public static IReadOnlyList<OnboardingTarget> AllTargets
        {
            get
            {
                PruneUnavailableElementTargets();
                return Targets.ToArray();
            }
        }

        public static void Register(OnboardingTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Id))
            {
                return;
            }

            Targets.RemoveAll(existing => ReferenceEquals(existing, target)
                || string.Equals(existing != null ? existing.Id : string.Empty, target.Id, StringComparison.Ordinal));
            Targets.Add(target);
            TargetsChanged?.Invoke();
        }

        public static void RegisterVisualElement(
            string id,
            VisualElement element,
            string displayName = null,
            string moduleName = null,
            Action focus = null,
            Action<bool> setInteractable = null)
        {
            if (string.IsNullOrWhiteSpace(id) || element == null)
            {
                return;
            }

            var elementReference = new WeakReference<VisualElement>(element);
            var target = new OnboardingTarget
            {
                Id = id.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Trim() : displayName.Trim(),
                ModuleName = moduleName ?? string.Empty,
                ElementReference = elementReference,
                GetRect = () => TryGetElement(elementReference, out VisualElement targetElement) && IsElementVisible(targetElement) ? targetElement.worldBound : Rect.zero,
                IsAvailable = () => TryGetElement(elementReference, out VisualElement targetElement) && IsElementVisible(targetElement),
                Focus = () =>
                {
                    focus?.Invoke();
                    if (TryGetElement(elementReference, out VisualElement targetElement))
                    {
                        FocusElement(targetElement);
                    }
                },
                SetInteractable = setInteractable ?? (enabled =>
                {
                    if (TryGetElement(elementReference, out VisualElement targetElement))
                    {
                        targetElement.SetEnabled(enabled);
                    }
                })
            };

            Register(target);
            element.RegisterCallback<DetachFromPanelEvent>(_ => Unregister(target));
        }

        public static void RegisterSyntheticTarget(
            string id,
            Func<Rect> getRect,
            Func<bool> isAvailable,
            string displayName = null,
            string moduleName = null,
            Action focus = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            Register(new OnboardingTarget
            {
                Id = id.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Trim() : displayName.Trim(),
                ModuleName = moduleName ?? string.Empty,
                GetRect = getRect ?? (() => Rect.zero),
                IsAvailable = isAvailable ?? (() => true),
                Focus = focus
            });
        }

        public static void Unregister(OnboardingTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (Targets.Remove(target))
            {
                TargetsChanged?.Invoke();
            }
        }

        public static OnboardingTarget Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            PruneUnavailableElementTargets();
            for (int index = Targets.Count - 1; index >= 0; index--)
            {
                OnboardingTarget target = Targets[index];
                if (target == null || !string.Equals(target.Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (target.Available)
                {
                    return target;
                }
            }

            return Targets.LastOrDefault(target => target != null && string.Equals(target.Id, id, StringComparison.Ordinal));
        }

        public static bool IsTargetAvailable(string id)
        {
            OnboardingTarget target = Get(id);
            return target != null && target.Available;
        }

        public static bool IsTargetVisible(string id)
        {
            OnboardingTarget target = Get(id);
            Rect rect;
            return TryGetVisibleWorldRect(target, out rect);
        }

        public static bool TryGetVisibleWorldRect(OnboardingTarget target, out Rect rect)
        {
            rect = Rect.zero;
            if (target == null || !target.Available)
            {
                return false;
            }

            VisualElement element = target.Element;
            if (element != null)
            {
                return TryGetVisibleElementWorldRect(element, out rect);
            }

            rect = target.Rect;
            return rect.width > 1f && rect.height > 1f;
        }

        public static IReadOnlyList<string> GetMissingTargets(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                return Array.Empty<string>();
            }

            return ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Where(id => !IsTargetAvailable(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public static bool IsPointInsideTargets(Vector2 panelPosition, IEnumerable<string> ids)
        {
            if (ids == null)
            {
                return false;
            }

            foreach (string id in ids)
            {
                OnboardingTarget target = Get(id);
                Rect visibleRect;
                if (!TryGetVisibleWorldRect(target, out visibleRect))
                {
                    continue;
                }

                if (visibleRect.Contains(panelPosition))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsFocusedElementInsideTargets(VisualElement focusedElement, IEnumerable<string> ids)
        {
            if (focusedElement == null || ids == null)
            {
                return false;
            }

            foreach (string id in ids)
            {
                OnboardingTarget target = Get(id);
                VisualElement targetElement = target != null ? target.Element : null;
                if (targetElement == null)
                {
                    continue;
                }

                for (VisualElement current = focusedElement; current != null; current = current.parent)
                {
                    if (ReferenceEquals(current, targetElement))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void FlashTarget(string id)
        {
            OnboardingManager.FlashTarget(id);
        }

        private static bool IsElementVisible(VisualElement element)
        {
            return element != null
                && element.panel != null
                && element.resolvedStyle.display != DisplayStyle.None
                && element.visible
                && element.worldBound.width > 1f
                && element.worldBound.height > 1f;
        }

        private static bool TryGetElement(WeakReference<VisualElement> elementReference, out VisualElement element)
        {
            element = null;
            return elementReference != null
                && elementReference.TryGetTarget(out element)
                && element != null;
        }

        private static bool TryGetVisibleElementWorldRect(VisualElement element, out Rect rect)
        {
            rect = Rect.zero;
            if (!IsElementVisible(element))
            {
                return false;
            }

            rect = element.worldBound;
            for (VisualElement current = element.parent; current != null; current = current.parent)
            {
                ScrollView scrollView = current as ScrollView;
                VisualElement clipElement = scrollView != null ? scrollView.contentViewport : null;
                if (clipElement == null || clipElement.panel != element.panel)
                {
                    continue;
                }

                rect = IntersectRects(rect, clipElement.worldBound);
                if (rect.width <= 1f || rect.height <= 1f)
                {
                    rect = Rect.zero;
                    return false;
                }
            }

            return rect.width > 1f && rect.height > 1f;
        }

        private static Rect IntersectRects(Rect first, Rect second)
        {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                return Rect.zero;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void FocusElement(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.schedule.Execute(() =>
            {
                try
                {
                    ScrollToElement(element);
                }
                catch
                {
                }
            }).ExecuteLater(1);

            element.schedule.Execute(() =>
            {
                try
                {
                    ScrollToElement(element);
                }
                catch
                {
                }
            }).ExecuteLater(80);

            element.schedule.Execute(() =>
            {
                try
                {
                    ScrollToElement(element);
                }
                catch
                {
                }
            }).ExecuteLater(180);
        }

        private static void ScrollToElement(VisualElement element)
        {
            List<ScrollView> scrollViews = FindAncestorScrollViews(element);
            for (int index = scrollViews.Count - 1; index >= 0; index--)
            {
                try
                {
                    scrollViews[index].ScrollTo(element);
                }
                catch
                {
                }
            }

            try
            {
                element.Focus();
            }
            catch
            {
            }
        }

        private static List<ScrollView> FindAncestorScrollViews(VisualElement element)
        {
            var scrollViews = new List<ScrollView>();
            for (VisualElement current = element != null ? element.parent : null; current != null; current = current.parent)
            {
                ScrollView scrollView = current as ScrollView;
                if (scrollView != null)
                {
                    scrollViews.Add(scrollView);
                }
            }

            return scrollViews;
        }

        private static void PruneUnavailableElementTargets()
        {
            bool changed = Targets.RemoveAll(target =>
            {
                if (target == null)
                {
                    return true;
                }

                if (target.ElementReference == null)
                {
                    return false;
                }

                VisualElement element;
                return !target.ElementReference.TryGetTarget(out element) || element == null;
            }) > 0;

            if (changed)
            {
                TargetsChanged?.Invoke();
            }
        }

        [InitializeOnLoadMethod]
        private static void RegisterEditorMenuSyntheticTargets()
        {
            RegisterSyntheticTarget(
                "MainMenu.OpenManager",
                () =>
                {
                    Rect main = EditorGUIUtility.GetMainWindowPosition();
                    return new Rect(main.x + 8f, main.y + 20f, 160f, 24f);
                },
                () => true,
                "HoyoToon > Manager",
                "Unity Main Menu");
        }
    }
}
#endif
