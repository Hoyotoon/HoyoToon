using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal abstract class ManagerModuleBase : IManagerModule
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract int Order { get; }

        protected abstract string Description { get; }

        public virtual VisualElement CreateContent(ModuleContext context)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-8");
            root.AddToClassList("ht-module-root");

            root.Add(CreateOverviewCard(context));

            VisualElement moduleGrid = new VisualElement();
            moduleGrid.AddToClassList("ht-column");
            moduleGrid.AddToClassList("ht-gap-8");
            moduleGrid.AddToClassList("ht-module-grid");

            foreach (VisualElement card in BuildCards(context) ?? Enumerable.Empty<VisualElement>())
            {
                if (card != null)
                {
                    moduleGrid.Add(card);
                }
            }

            if (moduleGrid.childCount > 0)
            {
                root.Add(moduleGrid);
            }

            return root;
        }

        public virtual void OnSelected(ModuleContext context)
        {
        }

        public virtual void OnDeselected(ModuleContext context)
        {
        }

        protected abstract IEnumerable<VisualElement> BuildCards(ModuleContext context);

        protected virtual string GetOverviewMessage(ModuleContext context)
        {
            return "Use this module to inspect the current target without disturbing the persistent manager shell.";
        }

        protected virtual HelpBoxMessageType GetOverviewMessageType(ModuleContext context)
        {
            return HelpBoxMessageType.Info;
        }

        protected virtual string GetStatusText(ModuleContext context)
        {
            return context != null && context.HasModelSelection ? "Model Ready" : "Awaiting FBX";
        }

        protected virtual IReadOnlyList<(string Label, string Value)> GetOverviewStats(ModuleContext context)
        {
            if (context == null || !context.HasValidTarget)
            {
                return Array.Empty<(string Label, string Value)>();
            }

            GameObject targetRoot = context.TargetRoot;
            int objectCount = Mathf.Max(1, targetRoot.GetComponentsInChildren<Transform>(true).Length);
            int rendererCount = targetRoot.GetComponentsInChildren<Renderer>(true).Length;
            int materialCount = targetRoot.GetComponentsInChildren<Renderer>(true)
                .Sum(renderer => renderer != null ? renderer.sharedMaterials.Count(material => material != null) : 0);
            int componentCount = targetRoot.GetComponentsInChildren<Component>(true).Count(component => component != null);

            return new[]
            {
                ("Objects", objectCount.ToString()),
                ("Renderers", rendererCount.ToString()),
                ("Materials", materialCount.ToString()),
                ("Components", componentCount.ToString())
            };
        }

        protected VisualElement CreateCard()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            return card;
        }

        protected void AddCardHeader(VisualElement card, string title, string subtitle = null)
        {
            if (card == null)
            {
                return;
            }

            Label titleLabel = new Label(title ?? string.Empty);
            titleLabel.AddToClassList("ht-section-title");
            card.Add(titleLabel);

            if (string.IsNullOrWhiteSpace(subtitle))
            {
                return;
            }

            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("ht-body-text");
            card.Add(subtitleLabel);
        }

        protected Label CreateStatusPill(string text, bool positive)
        {
            Label pill = new Label(text ?? string.Empty);
            pill.AddToClassList("ht-status-pill");
            pill.AddToClassList(positive ? "ht-status-pill--positive" : "ht-status-pill--neutral");
            return pill;
        }

        protected TextField CreateTextField(string label, string value)
        {
            TextField textField = new TextField(label)
            {
                value = value ?? string.Empty
            };
            textField.AddToClassList("ht-field");
            return textField;
        }

        protected TextField CreateReadOnlyTextField(string label, string value)
        {
            TextField textField = CreateTextField(label, value);
            textField.isReadOnly = true;
            return textField;
        }

        protected ObjectField CreateReadOnlyObjectField(string label, Type objectType, Object value)
        {
            ObjectField objectField = new ObjectField(label)
            {
                objectType = objectType ?? typeof(Object),
                allowSceneObjects = false,
                value = value
            };
            objectField.AddToClassList("ht-field");
            objectField.SetEnabled(false);
            return objectField;
        }

        protected DropdownField CreateDropdown(string label, IEnumerable<string> options, int defaultIndex)
        {
            List<string> values = (options ?? Enumerable.Empty<string>())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .ToList();

            if (values.Count == 0)
            {
                values.Add("None");
            }

            int resolvedIndex = Mathf.Clamp(defaultIndex, 0, values.Count - 1);
            DropdownField dropdown = new DropdownField(label, values, resolvedIndex);
            dropdown.AddToClassList("ht-field");
            return dropdown;
        }

        protected Toggle CreateToggle(string label, bool value)
        {
            Toggle toggle = new Toggle(label)
            {
                value = value
            };
            toggle.AddToClassList("ht-toggle");
            return toggle;
        }

        protected Slider CreateSlider(string label, float lowValue, float highValue, float value)
        {
            Slider slider = new Slider(label, lowValue, highValue)
            {
                value = value
            };
            slider.AddToClassList("ht-field");
            return slider;
        }

        protected Foldout CreateFoldout(string title, bool expanded = true)
        {
            string stateKey = BuildFoldoutStateKey(title);
            bool resolvedExpanded = !string.IsNullOrWhiteSpace(stateKey)
                ? SessionState.GetBool(stateKey, expanded)
                : expanded;

            Foldout foldout = new Foldout
            {
                text = title ?? string.Empty,
                value = resolvedExpanded
            };
            foldout.AddToClassList("ht-foldout");
            foldout.contentContainer?.RegisterCallback<ChangeEvent<bool>>(StopNestedFoldoutBooleanChangePropagation);

            if (!string.IsNullOrWhiteSpace(stateKey))
            {
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (!IsFoldoutStateChange(foldout, evt.target))
                        return;

                    SessionState.SetBool(stateKey, evt.newValue);
                });
            }

            return foldout;
        }

        private static void StopNestedFoldoutBooleanChangePropagation(ChangeEvent<bool> evt)
        {
            evt?.StopPropagation();
        }

        private static bool IsFoldoutStateChange(Foldout foldout, object target)
        {
            if (foldout == null)
            {
                return false;
            }

            if (ReferenceEquals(target, foldout))
            {
                return true;
            }

            VisualElement targetElement = target as VisualElement;
            if (targetElement == null)
            {
                return false;
            }

            for (VisualElement current = targetElement; current != null; current = current.parent)
            {
                if (ReferenceEquals(current, foldout.contentContainer))
                {
                    return false;
                }

                if (ReferenceEquals(current, foldout))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildFoldoutStateKey(string title)
        {
            string safeModuleId = string.IsNullOrWhiteSpace(Id) ? "module" : Id.Trim();
            string safeTitle = string.IsNullOrWhiteSpace(title) ? "foldout" : title.Trim();
            return "HoyoToon.Manager.Foldout." + safeModuleId + "." + safeTitle;
        }

        protected HelpBox CreateHelpBox(string message, HelpBoxMessageType type)
        {
            HelpBox helpBox = new HelpBox(message ?? string.Empty, type);
            switch (type)
            {
                case HelpBoxMessageType.Warning:
                    helpBox.AddToClassList("ht-helpbox-warning");
                    break;
                case HelpBoxMessageType.Error:
                    helpBox.AddToClassList("ht-helpbox-error");
                    break;
                default:
                    helpBox.AddToClassList("ht-helpbox-info");
                    break;
            }

            return helpBox;
        }

        protected ListView CreateReadOnlyListView(IEnumerable<string> items)
        {
            List<string> values = (items ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            if (values.Count == 0)
            {
                values.Add("No items available.");
            }

            ListView listView = new ListView
            {
                itemsSource = values,
                fixedItemHeight = 22f,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };

            listView.makeItem = () =>
            {
                Label label = new Label();
                label.AddToClassList("ht-list-item");
                return label;
            };

            listView.bindItem = (element, index) =>
            {
                if (element is Label label)
                {
                    label.text = values[index];
                }
            };

            listView.AddToClassList("ht-listview");
            listView.style.height = Mathf.Clamp((values.Count * 22f) + 8f, 56f, 118f);
            return listView;
        }

        protected VisualElement CreateStatGrid(IReadOnlyList<(string Label, string Value)> stats)
        {
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ht-stat-grid");

            if (stats == null)
            {
                return grid;
            }

            foreach ((string label, string value) in stats)
            {
                VisualElement statItem = new VisualElement();
                statItem.AddToClassList("ht-stat-item");

                Label valueLabel = new Label(value ?? string.Empty);
                valueLabel.AddToClassList("ht-title-md");
                statItem.Add(valueLabel);

                Label labelLabel = new Label(label ?? string.Empty);
                labelLabel.AddToClassList("ht-caption");
                statItem.Add(labelLabel);

                grid.Add(statItem);
            }

            return grid;
        }

        protected VisualElement CreateOverviewCard(ModuleContext context)
        {
            VisualElement card = CreateCard();
            AddCardHeader(card, DisplayName, Description);
            card.Add(CreateStatusPill(GetStatusText(context), context != null && context.HasModelSelection));
            card.Add(CreateReadOnlyObjectField("Add Model", typeof(GameObject), context != null ? context.SelectedModelAsset : null));

            IReadOnlyList<(string Label, string Value)> stats = GetOverviewStats(context);
            if (stats != null && stats.Count > 0)
            {
                card.Add(CreateStatGrid(stats));
            }

            string overviewMessage = GetOverviewMessage(context);
            if (!string.IsNullOrWhiteSpace(overviewMessage))
            {
                card.Add(CreateHelpBox(overviewMessage, GetOverviewMessageType(context)));
            }

            return card;
        }

        protected static T FindFirstInTarget<T>(ModuleContext context) where T : Component
        {
            if (context == null || !context.HasValidTarget)
            {
                return null;
            }

            return context.TargetRoot.GetComponentInChildren<T>(true);
        }
    }
}
