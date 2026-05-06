using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.UI.Manager;
using HoyoToon.Editor.Utilities.UI;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Character.HSR;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ControllerLayoutSection = HoyoToon.Editor.Utilities.UI.SerializedControllerInspectorUtility.ControllerLayoutSection;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal sealed class CharacterModule : ManagerModuleBase
    {
        private const string UnknownControllerLabel = "No supported character controller found for the active model.";
        private static readonly string[] s_PreferredControllerSections =
        {
            "Light Sources",
            "Lighting Controls",
            "Character Self Shadows",
            "Stencils",
            "Renderers",
            "Effects"
        };

        private static readonly CharacterControllerResolver[] s_ControllerResolvers =
        {
            new CharacterControllerResolver(
                "Honkai Star Rail",
                "HSR Character Controller",
                typeof(HSRCharacterController),
                new[] { "Honkai Star Rail" })
        };

        private static readonly Dictionary<Type, IReadOnlyList<ControllerLayoutSection>> s_ControllerSectionLayoutCache =
            new Dictionary<Type, IReadOnlyList<ControllerLayoutSection>>();
        private static readonly SerializedControllerInspectorUtility.FieldClassNames s_ControllerFieldClassNames =
            new SerializedControllerInspectorUtility.FieldClassNames
            {
                FieldClass = "ht-character-controller-field",
                ToggleClass = "ht-character-controller-toggle",
                ObjectFieldClass = "ht-character-controller-object-field",
                CollectionClass = "ht-character-controller-field--collection",
                CompositeClass = "ht-character-controller-composite",
                CompositeLabelClass = "ht-character-controller-composite-label"
            };

        public override string Id => "character";

        public override string DisplayName => "Character";

        public override int Order => 2;

        protected override string Description => "Character";

        public override VisualElement CreateContent(ModuleContext context)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-12");
            root.AddToClassList("ht-module-root");
            root.Add(CreateCharacterControllerCard(context));
            return root;
        }

        protected override IEnumerable<VisualElement> BuildCards(ModuleContext context)
        {
            yield break;
        }

        private VisualElement CreateCharacterControllerCard(ModuleContext context)
        {
            VisualElement card = CreateCard();
            OnboardingTargetRegistry.RegisterVisualElement("Character.MainSettingsSection", card, "Character settings section", "Character");

            GameObject activeModel = context?.PlacementActiveModel;
            string activeGameKey = context?.PlacementActiveGameKey ?? string.Empty;
            string sectionTitle = GetControllerSectionTitle(activeGameKey);
            AddCardHeader(card, sectionTitle);

            if (activeModel == null)
            {
                card.Add(
                    CreateHelpBox(
                        "Select a character first to inspect its controller settings.",
                        HelpBoxMessageType.Info));
                return card;
            }

            CharacterControllerMatch match = ResolveController(activeModel, activeGameKey);
            if (match.Controller == null)
            {
                string supportedGames = string.Join(", ", s_ControllerResolvers
                    .Select(resolver => !string.IsNullOrWhiteSpace(resolver.GameDisplayName)
                        ? resolver.GameDisplayName
                        : resolver.ControllerDisplayName));

                card.Add(
                    CreateHelpBox(
                        UnknownControllerLabel + " Supported games: " + GetDisplayValue(supportedGames, "Honkai Star Rail"),
                        HelpBoxMessageType.Warning));
                return card;
            }

            card.Add(CreateControllerInspector(match.Controller));
            return card;
        }

        private static string GetControllerSectionTitle(string gameKey)
        {
            return string.IsNullOrWhiteSpace(gameKey)
                ? "Character Controller"
                : gameKey + " Character Controller";
        }

        private static CharacterControllerMatch ResolveController(GameObject activeModel, string activeGameKey)
        {
            if (activeModel == null)
            {
                return new CharacterControllerMatch();
            }

            Component fallbackController = null;
            CharacterControllerResolver fallbackResolver = default;
            bool hasFallback = false;

            foreach (CharacterControllerResolver resolver in s_ControllerResolvers)
            {
                Component controller = (Component)activeModel.GetComponentInChildren(resolver.ControllerType, true);
                if (controller == null)
                {
                    continue;
                }

                if (resolver.SupportsGame(activeGameKey))
                {
                    return new CharacterControllerMatch(controller, resolver);
                }

                if (!hasFallback)
                {
                    fallbackController = controller;
                    fallbackResolver = resolver;
                    hasFallback = true;
                }
            }

            return hasFallback
                ? new CharacterControllerMatch(fallbackController, fallbackResolver)
                : new CharacterControllerMatch();
        }

        private VisualElement CreateControllerInspector(Component controller)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("ht-character-controller-host");
            host.AddToClassList("ht-column");
            host.AddToClassList("ht-gap-8");

            if (controller == null)
            {
                host.Add(CreateHelpBox("No character controller found to inspect.", HelpBoxMessageType.Info));
                return host;
            }

            SerializedObject serializedController = new SerializedObject(controller);
            if (serializedController == null || serializedController.targetObject == null)
            {
                host.Add(CreateHelpBox("Inspector for this controller could not be created.", HelpBoxMessageType.Warning));
                return host;
            }

            serializedController.Update();

            IReadOnlyList<ControllerLayoutSection> sections = GetControllerLayoutSections(controller.GetType());
            if (sections.Count <= 0)
            {
                host.Add(CreateHelpBox("No serialized fields were found for this controller.", HelpBoxMessageType.Info));
                return host;
            }

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                if (sectionIndex > 0)
                {
                    host.Add(CreateDivider());
                }

                ControllerLayoutSection section = sections[sectionIndex];
                VisualElement sectionRoot = CreateFoldout(section.Title, true);
                sectionRoot.AddToClassList("ht-column");
                sectionRoot.AddToClassList("ht-gap-4");
                RegisterCharacterSectionTarget(section.Title, sectionRoot);

                int addedFieldCount = 0;
                for (int fieldIndex = 0; fieldIndex < section.Fields.Count; fieldIndex++)
                {
                    FieldInfo field = section.Fields[fieldIndex];
                    SerializedProperty property = serializedController.FindProperty(field.Name);
                    if (property == null)
                    {
                        continue;
                    }

                    VisualElement fieldElement = CreateFieldElement(field, property);
                    if (fieldElement == null)
                    {
                        continue;
                    }

                    sectionRoot.Add(fieldElement);
                    RegisterCharacterFieldTarget(section.Title, field, property, fieldElement);
                    addedFieldCount++;
                }

                if (addedFieldCount <= 0)
                {
                    sectionRoot.Add(CreateHelpBox("No editable fields in this section.", HelpBoxMessageType.Info));
                }

                host.Add(sectionRoot);
            }

            return host;
        }

        private static IReadOnlyList<ControllerLayoutSection> GetControllerLayoutSections(Type controllerType)
        {
            if (controllerType == null)
            {
                return Array.Empty<ControllerLayoutSection>();
            }

            if (s_ControllerSectionLayoutCache.TryGetValue(controllerType, out IReadOnlyList<ControllerLayoutSection> cachedSections))
            {
                return cachedSections;
            }

            IReadOnlyList<ControllerLayoutSection> result = SerializedControllerInspectorUtility.BuildLayoutSections(
                controllerType,
                s_PreferredControllerSections,
                SerializedControllerInspectorUtility.IsInspectableField);
            s_ControllerSectionLayoutCache[controllerType] = result;
            return result;
        }

        private static VisualElement CreateDivider()
        {
            return ManagerUiFactory.CreateDivider();
        }

        private static VisualElement CreateFieldElement(FieldInfo field, SerializedProperty property)
        {
            return SerializedControllerInspectorUtility.CreateFieldElement(field, property, s_ControllerFieldClassNames);
        }

        private static void RegisterCharacterSectionTarget(string sectionTitle, VisualElement sectionRoot)
        {
            if (sectionRoot == null || string.IsNullOrWhiteSpace(sectionTitle))
            {
                return;
            }

            if (SerializedControllerInspectorUtility.ContainsAny(sectionTitle, "Light Sources", "Lighting Controls"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Character.LightSourcesBox",
                    sectionRoot,
                    "Character light sources",
                    "Character");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(sectionRoot, "Character.LightSourcesBox");
            }
        }

        private static void RegisterCharacterFieldTarget(
            string sectionTitle,
            FieldInfo field,
            SerializedProperty property,
            VisualElement fieldElement)
        {
            if (fieldElement == null || property == null)
            {
                return;
            }

            PropertyLabelAttribute propertyLabel = field != null ? field.GetCustomAttribute<PropertyLabelAttribute>() : null;
            string label = SerializedControllerInspectorUtility.ResolveFieldLabel(property, propertyLabel);
            string fieldName = field != null ? field.Name : property.name;
            string searchable = (sectionTitle ?? string.Empty) + " " + label + " " + fieldName;

            if (SerializedControllerInspectorUtility.ContainsAll(searchable, "Self", "Shadow") && property.propertyType == SerializedPropertyType.Boolean)
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Character.SelfShadowToggle",
                    fieldElement,
                    "Self Shadows toggle",
                    "Character");
                fieldElement.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    OnboardingSignals.RecordValueChanged("Character.SelfShadowToggle", evt.newValue);
                    OnboardingSignals.RecordAction(evt.newValue
                        ? "Character.SelfShadowToggle.True"
                        : "Character.SelfShadowToggle.False");
                });
                return;
            }

            if (SerializedControllerInspectorUtility.ContainsAny(searchable, "Light", "Lighting")
                && SerializedControllerInspectorUtility.ContainsAny(searchable, "Intensity", "Strength", "Power"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Character.LightIntensityField",
                    fieldElement,
                    "Character light intensity",
                    "Character");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(fieldElement, "Character.LightIntensityField");
            }

            if (SerializedControllerInspectorUtility.ContainsAny(searchable, "Light", "Lighting")
                && SerializedControllerInspectorUtility.ContainsAny(searchable, "Color", "Colour"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Character.LightColorField",
                    fieldElement,
                    "Character light color",
                    "Character");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(fieldElement, "Character.LightColorField");
            }
        }

        private static string GetDisplayValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private readonly struct CharacterControllerResolver
        {
            public readonly string GameDisplayName;
            public readonly string ControllerDisplayName;
            public readonly Type ControllerType;
            public readonly IReadOnlyList<string> SupportedGameKeys;

            public CharacterControllerResolver(
                string gameDisplayName,
                string controllerDisplayName,
                Type controllerType,
                IReadOnlyList<string> supportedGameKeys)
            {
                GameDisplayName = gameDisplayName;
                ControllerDisplayName = controllerDisplayName;
                ControllerType = controllerType;
                SupportedGameKeys = supportedGameKeys ?? Array.Empty<string>();
            }

            public bool SupportsGame(string gameKey)
            {
                if (SupportedGameKeys.Count <= 0)
                {
                    return true;
                }

                return SupportedGameKeys.Any(
                    key => string.Equals(key, gameKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        private readonly struct CharacterControllerMatch
        {
            public readonly Component Controller;
            public readonly CharacterControllerResolver Descriptor;

            public CharacterControllerMatch(Component controller, CharacterControllerResolver descriptor)
            {
                Controller = controller;
                Descriptor = descriptor;
            }
        }

    }
}
