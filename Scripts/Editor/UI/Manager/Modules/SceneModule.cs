using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.UI.Manager;
using HoyoToon.Editor.Utilities.UI;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Scene.HSR;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ControllerLayoutSection = HoyoToon.Editor.Utilities.UI.SerializedControllerInspectorUtility.ControllerLayoutSection;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal sealed class SceneModule : ManagerModuleBase
    {
        private const string UnknownControllerLabel = "No supported scene controller found for the active game.";
        private const string SceneLightDefaultName = "HoyoToon Scene Fill Light";
        private const float MinColorTemperature = 1000f;
        private const float MaxColorTemperature = 20000f;
        private const float CameraRelativeLightSourceYaw = 180f;
        private const float CameraRelativeLightTransformYawOffset = 180f;

        private static readonly string[] s_PreferredControllerSections =
        {
            "Character Lighting",
            "Outlines",
            "Rim Light",
            "Rim Shadow",
            "Height Light",
            "Shadow Grading",
            "Fog",
            "Height Fog"
        };

        private static readonly HashSet<string> s_ExcludedSceneLightNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CharacterLight",
            "CharacterShadowLight"
        };

        private static readonly Dictionary<int, LocalLightMotionState> s_LocalLightMotionStates =
            new Dictionary<int, LocalLightMotionState>();

        private static readonly SceneControllerResolver[] s_ControllerResolvers =
        {
            new SceneControllerResolver(
                "Honkai Star Rail",
                "HSR Scene Controller",
                typeof(HSRSceneController),
                new[] { "Honkai Star Rail" })
        };

        private static readonly Dictionary<Type, IReadOnlyList<ControllerLayoutSection>> s_ControllerSectionLayoutCache =
            new Dictionary<Type, IReadOnlyList<ControllerLayoutSection>>();
        private static readonly SerializedControllerInspectorUtility.FieldClassNames s_ControllerFieldClassNames =
            new SerializedControllerInspectorUtility.FieldClassNames
            {
                FieldClass = "ht-character-controller-field",
                ToggleClass = "ht-character-controller-toggle",
                CollectionClass = "ht-character-controller-field--collection",
                CompositeClass = "ht-character-controller-composite",
                CompositeLabelClass = "ht-character-controller-composite-label"
            };

        private int selectedLightInstanceId;

        public override string Id => "scene";

        public override string DisplayName => "Scene";

        public override int Order => 3;

        protected override string Description => "Scene";

        public override VisualElement CreateContent(ModuleContext context)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-12");
            root.AddToClassList("ht-module-root");

            SceneControllerMatch match = ResolveController(context != null ? context.PlacementActiveGameKey : string.Empty);
            root.Add(CreateSceneLightsCard(context, match));
            root.Add(CreateDivider());
            root.Add(CreateSceneControllerCard(context, match));
            return root;
        }

        protected override IEnumerable<VisualElement> BuildCards(ModuleContext context)
        {
            yield break;
        }

        private VisualElement CreateSceneLightsCard(ModuleContext context, SceneControllerMatch match)
        {
            VisualElement card = CreateCard();
            AddCardHeader(card, "Scene Lights");

            if (match.Controller == null)
            {
                card.Add(CreateHelpBox(GetMissingControllerMessage(context), GetMissingControllerMessageType(context)));
                return card;
            }

            DropdownField lightSelector = CreateDropdown(string.Empty, Array.Empty<string>(), 0);
            lightSelector.AddToClassList("ht-scene-light-selector-field");
            lightSelector.style.flexGrow = 1f;
            lightSelector.style.minWidth = 0f;

            Button addLightButton = CreateActionButton("Add Light");
            Button removeLightButton = CreateActionButton("Remove Light");

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.NoWrap;
            toolbar.style.alignItems = Align.Center;

            Label selectorLabel = new Label("Active Light");
            selectorLabel.AddToClassList("ht-body-text");
            selectorLabel.style.minWidth = 84f;
            selectorLabel.style.marginRight = 8f;
            selectorLabel.style.flexShrink = 0f;

            VisualElement selectorWrapper = new VisualElement();
            selectorWrapper.style.flexGrow = 1f;
            selectorWrapper.style.minWidth = 0f;
            selectorWrapper.style.marginRight = 8f;
            selectorWrapper.Add(lightSelector);

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.flexWrap = Wrap.NoWrap;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.flexShrink = 0f;

            addLightButton.style.marginRight = 8f;
            addLightButton.style.flexShrink = 0f;
            removeLightButton.style.flexShrink = 0f;

            buttonRow.Add(addLightButton);
            buttonRow.Add(removeLightButton);
            toolbar.Add(selectorLabel);
            toolbar.Add(selectorWrapper);
            toolbar.Add(buttonRow);
            card.Add(toolbar);

            card.Add(CreateDivider());

            VisualElement inspectorHost = new VisualElement();
            inspectorHost.AddToClassList("ht-character-controller-host");
            inspectorHost.AddToClassList("ht-column");
            inspectorHost.AddToClassList("ht-gap-8");
            card.Add(inspectorHost);

            List<SceneLightEntry> lightEntries = new List<SceneLightEntry>();

            void RefreshLightEditor()
            {
                lightEntries = BuildSceneLightEntries(match.Controller);

                Light selectedLight = ResolveSelectedLight(lightEntries, GetProtectedMainLight(match.Controller));
                selectedLightInstanceId = selectedLight != null ? selectedLight.GetInstanceID() : 0;

                List<string> lightLabels = lightEntries.Select(entry => entry.Label).ToList();
                if (lightLabels.Count <= 0)
                {
                    lightLabels.Add("No scene lights");
                }

                lightSelector.choices = lightLabels;
                lightSelector.SetEnabled(lightEntries.Count > 0);

                SceneLightEntry selectedEntry = default;
                bool hasSelectedEntry = false;
                for (int index = 0; index < lightEntries.Count; index++)
                {
                    if (lightEntries[index].Light == selectedLight)
                    {
                        selectedEntry = lightEntries[index];
                        hasSelectedEntry = true;
                        break;
                    }
                }

                lightSelector.SetValueWithoutNotify(hasSelectedEntry ? selectedEntry.Label : lightLabels[0]);
                removeLightButton.SetEnabled(hasSelectedEntry && !selectedEntry.IsMainLight);
                removeLightButton.tooltip = hasSelectedEntry && selectedEntry.IsMainLight
                    ? "The main scene light cannot be removed here."
                    : "Remove the selected scene light.";

                inspectorHost.Clear();
                if (!hasSelectedEntry)
                {
                    inspectorHost.Add(CreateHelpBox("No scene lights found. Add one to begin.", HelpBoxMessageType.Info));
                    return;
                }

                VisualElement lightSection = CreateFoldout(selectedEntry.Label, true);
                lightSection.AddToClassList("ht-column");
                lightSection.AddToClassList("ht-gap-4");
                lightSection.Add(CreateSceneLightInspector(match.Controller, selectedEntry.Light, selectedEntry.IsMainLight, RefreshLightEditor));
                inspectorHost.Add(lightSection);

                inspectorHost.Add(CreateDivider());

                VisualElement motionSection = CreateFoldout("Light Motion", true);
                motionSection.AddToClassList("ht-column");
                motionSection.AddToClassList("ht-gap-4");
                motionSection.Add(CreateLightMotionInspector(match.Controller, selectedEntry.Light, selectedEntry.IsMainLight));
                inspectorHost.Add(motionSection);
            }

            lightSelector.RegisterValueChangedCallback(evt =>
            {
                if (lightEntries.Count <= 0)
                {
                    return;
                }

                SceneLightEntry matchedEntry = default;
                bool hasMatchedEntry = false;
                for (int index = 0; index < lightEntries.Count; index++)
                {
                    if (!string.Equals(lightEntries[index].Label, evt.newValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matchedEntry = lightEntries[index];
                    hasMatchedEntry = true;
                    break;
                }

                if (!hasMatchedEntry || matchedEntry.Light == null)
                {
                    return;
                }

                selectedLightInstanceId = matchedEntry.Light.GetInstanceID();
                RefreshLightEditor();
            });

            addLightButton.clicked += () =>
            {
                Light newLight = CreateSceneLight(match.Controller);
                selectedLightInstanceId = newLight != null ? newLight.GetInstanceID() : 0;
                RefreshLightEditor();
            };

            removeLightButton.clicked += () =>
            {
                Light selectedLight = lightEntries
                    .Select(entry => entry.Light)
                    .FirstOrDefault(light => light != null && light.GetInstanceID() == selectedLightInstanceId);

                Light protectedMainLight = GetProtectedMainLight(match.Controller);
                if (selectedLight == null || selectedLight == protectedMainLight)
                {
                    return;
                }

                RemoveSceneLight(selectedLight);
                selectedLightInstanceId = 0;
                RefreshLightEditor();
            };

            RefreshLightEditor();
            return card;
        }

        private VisualElement CreateSceneControllerCard(ModuleContext context, SceneControllerMatch match)
        {
            VisualElement card = CreateCard();
            AddCardHeader(card, GetControllerSectionTitle(context, match));
            OnboardingTargetRegistry.RegisterVisualElement("Scene.GlobalControls", card, "Global scene controls", "Scene");
            SerializedControllerInspectorUtility.RegisterValueChangeSignals(card, "Scene.GlobalControls");

            if (match.Controller == null)
            {
                card.Add(CreateHelpBox(GetMissingControllerMessage(context), GetMissingControllerMessageType(context)));
                return card;
            }

            card.Add(CreateControllerInspector(match.Controller));
            return card;
        }

        private static string GetControllerSectionTitle(ModuleContext context, SceneControllerMatch match)
        {
            string activeGameKey = context != null ? context.PlacementActiveGameKey : string.Empty;
            string gameLabel = !string.IsNullOrWhiteSpace(activeGameKey)
                ? activeGameKey
                : match.Descriptor.GameDisplayName;

            return string.IsNullOrWhiteSpace(gameLabel)
                ? "Scene Controller"
                : gameLabel + " Scene Controller";
        }

        private static string GetMissingControllerMessage(ModuleContext context)
        {
            string supportedGames = string.Join(", ", s_ControllerResolvers
                .Select(resolver => !string.IsNullOrWhiteSpace(resolver.GameDisplayName)
                    ? resolver.GameDisplayName
                    : resolver.ControllerDisplayName));

            if (context != null && !string.IsNullOrWhiteSpace(context.PlacementActiveGameKey))
            {
                return UnknownControllerLabel + " Supported games: " + GetDisplayValue(supportedGames, "Honkai Star Rail");
            }

            return "Select an active character or load a supported scene controller to inspect scene settings.";
        }

        private static HelpBoxMessageType GetMissingControllerMessageType(ModuleContext context)
        {
            return context != null && !string.IsNullOrWhiteSpace(context.PlacementActiveGameKey)
                ? HelpBoxMessageType.Warning
                : HelpBoxMessageType.Info;
        }

        private static SceneControllerMatch ResolveController(string activeGameKey)
        {
            Component fallbackController = null;
            SceneControllerResolver fallbackResolver = default;
            bool hasFallback = false;

            for (int index = 0; index < s_ControllerResolvers.Length; index++)
            {
                SceneControllerResolver resolver = s_ControllerResolvers[index];
                Component controller = FindFirstSceneObjectOfType(resolver.ControllerType);
                if (controller == null)
                {
                    continue;
                }

                if (resolver.SupportsGame(activeGameKey))
                {
                    return new SceneControllerMatch(controller, resolver);
                }

                if (!hasFallback)
                {
                    fallbackController = controller;
                    fallbackResolver = resolver;
                    hasFallback = true;
                }
            }

            return hasFallback
                ? new SceneControllerMatch(fallbackController, fallbackResolver)
                : new SceneControllerMatch();
        }

        private VisualElement CreateControllerInspector(Component controller)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("ht-character-controller-host");
            host.AddToClassList("ht-column");
            host.AddToClassList("ht-gap-8");

            if (controller == null)
            {
                host.Add(CreateHelpBox("No scene controller found to inspect.", HelpBoxMessageType.Info));
                return host;
            }

            SerializedObject serializedController = new SerializedObject(controller);
            if (serializedController == null || serializedController.targetObject == null)
            {
                host.Add(CreateHelpBox("Inspector for this scene controller could not be created.", HelpBoxMessageType.Warning));
                return host;
            }

            serializedController.Update();

            IReadOnlyList<ControllerLayoutSection> sections = GetControllerLayoutSections(controller.GetType());
            if (sections.Count <= 0)
            {
                host.Add(CreateHelpBox("No serialized fields were found for this scene controller.", HelpBoxMessageType.Info));
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

                int addedFieldCount = 0;
                for (int fieldIndex = 0; fieldIndex < section.Fields.Count; fieldIndex++)
                {
                    FieldInfo field = section.Fields[fieldIndex];
                    SerializedProperty property = serializedController.FindProperty(field.Name);
                    if (property == null)
                    {
                        continue;
                    }

                    VisualElement fieldElement = CreateControllerFieldElement(field, property);
                    if (fieldElement == null)
                    {
                        continue;
                    }

                    sectionRoot.Add(fieldElement);
                    RegisterSceneFieldTarget(section.Title, field, property, fieldElement);
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
                ShouldIncludeControllerField,
                GetControllerFieldSectionTitle);
            s_ControllerSectionLayoutCache[controllerType] = result;
            return result;
        }

        private static bool ShouldIncludeControllerField(FieldInfo field)
        {
            return SerializedControllerInspectorUtility.IsInspectableField(field) && !IsExcludedControllerField(field);
        }

        private static bool IsExcludedControllerField(FieldInfo field)
        {
            if (field == null)
            {
                return true;
            }

            if (string.Equals(field.Name, "main_light", StringComparison.Ordinal)
                || string.Equals(field.Name, "CharacterLights", StringComparison.Ordinal))
            {
                return true;
            }

            return field.Name.StartsWith("MainLight", StringComparison.Ordinal);
        }

        private static string GetControllerFieldSectionTitle(Type controllerType, FieldInfo field)
        {
            if (controllerType == typeof(HSRSceneController))
            {
                string mappedTitle = GetHsrSceneFieldSectionTitle(field);
                if (!string.IsNullOrWhiteSpace(mappedTitle))
                {
                    return mappedTitle;
                }
            }

            return string.Empty;
        }

        private static string GetHsrSceneFieldSectionTitle(FieldInfo field)
        {
            if (field == null)
            {
                return string.Empty;
            }

            switch (field.Name)
            {
                case "_ES_SelfShadowLerpHair":
                case "_ES_CharacterToonRampMode":
                case "_ES_CharacterDisableLocalMainLight":
                case "_ES_AddColor":
                case "_ES_SPColor":
                case "_ES_SPIntensity":
                    return "Character Lighting";
                case "_ES_OutLineDarkenVal":
                case "_ES_OutLineLightedVal":
                case "_ES_OutlineDisableDistanceScale":
                case "_ES_OutlineFallbackScale":
                case "_OutlineScale":
                    return "Outlines";
                case "_ES_RimShadowColor":
                case "_ES_RimShadowIntensity":
                    return "Rim Shadow";
                case "_ES_RimLightOffset":
                case "_ES_RimLightMode":
                case "_ES_RimLightWidth":
                case "_ES_RimLightIntensity":
                case "_ES_RimLightAddMode":
                case "_ES_RimLightColor":
                    return "Rim Light";
                case "HeightLerpEnable":
                case "_ES_HeightLerpTop":
                case "_ES_HeightLerpBottom":
                case "_ES_HeightLerpTopColor":
                case "_ES_HeightLerpMiddleColor":
                case "_ES_HeightLerpBottomColor":
                    return "Height Light";
                case "_ES_LEVEL_ADJUST_ON":
                case "_ES_LevelSkinLightColor":
                case "_ES_LevelSkinShadowColor":
                case "_ES_LevelHighLightColor":
                case "_ES_LevelShadowColor":
                case "_ES_LevelShadow":
                case "_ES_LevelMid":
                case "_ES_LevelHighLight":
                case "_ES_LevelEyeShadowIntensity":
                    return "Shadow Grading";
                case "_ES_FogColor":
                case "_ES_FogDensity":
                case "_ES_FogNear":
                case "_ES_FogFar":
                case "_ES_FogCharacterNearFactor":
                case "_ES_DisableFogTransition":
                    return "Fog";
                case "_ES_HeightFogColor":
                case "_ES_HeightFogBaseHeight":
                case "_ES_HeightFogRange":
                case "_ES_HeightFogDensity":
                case "_ES_HeightFogFogNear":
                case "_ES_HeightFogFogFar":
                case "_ES_HeightFogAddAjust":
                    return "Height Fog";
                default:
                    return string.Empty;
            }
        }

        private static VisualElement CreateDivider()
        {
            return ManagerUiFactory.CreateDivider();
        }

        private static VisualElement CreateControllerFieldElement(FieldInfo field, SerializedProperty property)
        {
            return SerializedControllerInspectorUtility.CreateFieldElement(field, property, s_ControllerFieldClassNames);
        }

        private VisualElement CreateSceneLightInspector(Component controller, Light light, bool isMainLight, Action refreshLights)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("ht-column");
            host.AddToClassList("ht-gap-4");

            if (light == null)
            {
                host.Add(CreateHelpBox("No scene light selected.", HelpBoxMessageType.Info));
                return host;
            }

            HSRSceneController hsrMainLightController = isMainLight ? controller as HSRSceneController : null;

            void ApplyMainLightBackedChange(string undoName, Action<HSRSceneController> applyController, Action<Light> applyLight)
            {
                if (hsrMainLightController != null && applyController != null)
                {
                    ApplyHsrMainLightChange(hsrMainLightController, light, undoName, applyController, applyLight);
                    return;
                }

                ApplyLightChange(light, undoName, applyLight);
            }

            if (isMainLight)
            {
                host.Add(CreateHelpBox("The selected main light can be edited here, but it cannot be removed.", HelpBoxMessageType.Info));
            }

            host.Add(CreateTransformVector3Field(
                "Position",
                light.transform != null ? light.transform.position : Vector3.zero,
                newValue => ApplyTransformChange(light, "HoyoToon Update Scene Light Position", transform => transform.position = newValue)));

            host.Add(CreateTransformVector3Field(
                "Rotation",
                light.transform != null ? light.transform.eulerAngles : Vector3.zero,
                newValue =>
                {
                    if (hsrMainLightController != null)
                    {
                        ApplyHsrMainLightTransformChange(
                            hsrMainLightController,
                            light,
                            "HoyoToon Update Scene Main Light Rotation",
                            target => target.MainLightRotation = NormalizeYaw(newValue.y),
                            transform => transform.eulerAngles = newValue);
                        return;
                    }

                    ApplyTransformChange(light, "HoyoToon Update Scene Light Rotation", transform => transform.eulerAngles = newValue);
                }));

            EnumField lightTypeField = CreateLightEnumField(
                "Light Type",
                light.type,
                newValue =>
                {
                    ApplyLightChange(light, "HoyoToon Update Scene Light Type", target => target.type = (LightType)newValue);
                    refreshLights?.Invoke();
                });
            lightTypeField.SetEnabled(!isMainLight);
            host.Add(lightTypeField);

            host.Add(CreateLightColorField(
                "Color",
                hsrMainLightController != null ? hsrMainLightController.MainLightColor : light.color,
                newValue => ApplyMainLightBackedChange(
                    "HoyoToon Update Scene Main Light Color",
                    target => target.MainLightColor = newValue,
                    target => target.color = newValue)));

            host.Add(CreateLightToggleField(
                "Use Color Temperature",
                hsrMainLightController != null ? hsrMainLightController.MainLightUseColorTemperature : light.useColorTemperature,
                newValue =>
                {
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Temperature Mode",
                        target => target.MainLightUseColorTemperature = newValue,
                        target => target.useColorTemperature = newValue);
                    refreshLights?.Invoke();
                }));

            bool useColorTemperature = hsrMainLightController != null ? hsrMainLightController.MainLightUseColorTemperature : light.useColorTemperature;
            FloatField temperatureField = CreateLightFloatField(
                "Temperature",
                hsrMainLightController != null ? hsrMainLightController.MainLightColorTemperature : light.colorTemperature,
                newValue =>
                {
                    float clampedValue = Mathf.Clamp(newValue, MinColorTemperature, MaxColorTemperature);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Temperature",
                        target => target.MainLightColorTemperature = clampedValue,
                        target => target.colorTemperature = clampedValue);
                });
            temperatureField.SetEnabled(useColorTemperature);
            host.Add(temperatureField);

            host.Add(CreateLightFloatField(
                "Intensity",
                hsrMainLightController != null ? hsrMainLightController.MainLightIntensity : light.intensity,
                newValue =>
                {
                    float clampedValue = Mathf.Max(0f, newValue);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Intensity",
                        target => target.MainLightIntensity = clampedValue,
                        target => target.intensity = clampedValue);
                }));

            host.Add(CreateLightEnumField(
                "Mode",
                hsrMainLightController != null ? hsrMainLightController.MainLightMode : light.lightmapBakeType,
                newValue =>
                {
                    LightmapBakeType nextMode = (LightmapBakeType)newValue;
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Mode",
                        target => target.MainLightMode = nextMode,
                        target => target.lightmapBakeType = nextMode);
                }));

            host.Add(CreateLightFloatField(
                "Indirect Multiplier",
                hsrMainLightController != null ? hsrMainLightController.MainLightIndirectMultiplier : light.bounceIntensity,
                newValue =>
                {
                    float clampedValue = Mathf.Max(0f, newValue);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Indirect Multiplier",
                        target => target.MainLightIndirectMultiplier = clampedValue,
                        target => target.bounceIntensity = clampedValue);
                }));

            if (light.type != LightType.Directional)
            {
                host.Add(CreateLightFloatField(
                    "Range",
                    light.range,
                    newValue => ApplyLightChange(light, "HoyoToon Update Scene Light Range", target => target.range = Mathf.Max(0f, newValue))));
            }

            if (light.type == LightType.Spot)
            {
                host.Add(CreateLightFloatField(
                    "Spot Angle",
                    light.spotAngle,
                    newValue => ApplyLightChange(light, "HoyoToon Update Scene Light Spot Angle", target => target.spotAngle = Mathf.Clamp(newValue, 1f, 179f))));

                host.Add(CreateLightFloatField(
                    "Inner Spot Angle",
                    light.innerSpotAngle,
                    newValue => ApplyLightChange(light, "HoyoToon Update Scene Light Inner Spot Angle", target => target.innerSpotAngle = Mathf.Clamp(newValue, 0f, 179f))));
            }

            host.Add(CreateLightEnumField(
                "Shadows",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowType : light.shadows,
                newValue =>
                {
                    LightShadows nextShadows = (LightShadows)newValue;
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadows",
                        target => target.MainLightShadowType = nextShadows,
                        target => target.shadows = nextShadows);
                }));

            host.Add(CreateLightEnumField(
                "Shadow Resolution",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowResolution : light.shadowResolution,
                newValue =>
                {
                    LightShadowResolution nextResolution = (LightShadowResolution)newValue;
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadow Resolution",
                        target => target.MainLightShadowResolution = nextResolution,
                        target => target.shadowResolution = nextResolution);
                }));

            host.Add(CreateLightFloatField(
                "Shadow Strength",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowStrength : light.shadowStrength,
                newValue =>
                {
                    float clampedValue = Mathf.Clamp(newValue, 0f, 2f);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadow Strength",
                        target => target.MainLightShadowStrength = clampedValue,
                        target => target.shadowStrength = clampedValue);
                }));

            host.Add(CreateLightFloatField(
                "Shadow Bias",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowBias : light.shadowBias,
                newValue =>
                {
                    float clampedValue = Mathf.Clamp(newValue, 0f, 2f);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadow Bias",
                        target => target.MainLightShadowBias = clampedValue,
                        target => target.shadowBias = clampedValue);
                }));

            host.Add(CreateLightFloatField(
                "Shadow Normal Bias",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowNormalBias : light.shadowNormalBias,
                newValue =>
                {
                    float clampedValue = Mathf.Clamp(newValue, 0f, 3f);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadow Normal Bias",
                        target => target.MainLightShadowNormalBias = clampedValue,
                        target => target.shadowNormalBias = clampedValue);
                }));

            host.Add(CreateLightFloatField(
                "Shadow Near Plane",
                hsrMainLightController != null ? hsrMainLightController.MainLightShadowNearPlane : light.shadowNearPlane,
                newValue =>
                {
                    float clampedValue = Mathf.Clamp(newValue, 0.01f, 10f);
                    ApplyMainLightBackedChange(
                        "HoyoToon Update Scene Main Light Shadow Near Plane",
                        target => target.MainLightShadowNearPlane = clampedValue,
                        target => target.shadowNearPlane = clampedValue);
                }));

            return host;
        }

        private VisualElement CreateLightMotionInspector(Component controller, Light selectedLight, bool isMainLight)
        {
            if (selectedLight == null)
            {
                VisualElement host = new VisualElement();
                host.AddToClassList("ht-column");
                host.AddToClassList("ht-gap-4");
                host.Add(CreateHelpBox("Select a scene light to adjust its motion settings.", HelpBoxMessageType.Info));
                return host;
            }

            return isMainLight
                ? CreateMainLightMotionInspector(controller)
                : CreateLocalLightMotionInspector(selectedLight);
        }

        private VisualElement CreateMainLightMotionInspector(Component controller)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("ht-column");
            host.AddToClassList("ht-gap-4");

            if (controller == null)
            {
                host.Add(CreateHelpBox("No scene controller found for main light motion controls.", HelpBoxMessageType.Info));
                return host;
            }

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.Update();

            string[] propertyNames =
            {
                "MainLightRotation",
                "MainLightSyncToCameraRotation",
                "MainLightAutoRotate",
                "MainLightAutoRotateSpeed",
                "MainLightAutoRotateDirection"
            };

            int addedFieldCount = 0;
            for (int index = 0; index < propertyNames.Length; index++)
            {
                FieldInfo field = FindControllerField(controller.GetType(), propertyNames[index]);
                SerializedProperty property = serializedController.FindProperty(propertyNames[index]);
                if (field == null || property == null)
                {
                    continue;
                }

                VisualElement fieldElement = CreateControllerFieldElement(field, property);
                if (fieldElement == null)
                {
                    continue;
                }

                host.Add(fieldElement);
                addedFieldCount++;
            }

            if (addedFieldCount <= 0)
            {
                host.Add(CreateHelpBox("No main light motion controls were found.", HelpBoxMessageType.Info));
            }

            if (controller is HSRSceneController hsrSceneController)
            {
                host.schedule.Execute(() =>
                {
                    if (hsrSceneController != null && hsrSceneController.MainLightSyncToCameraRotation)
                        hsrSceneController.ForceSyncMainLightMotion();
                }).Every(16);
            }

            return host;
        }

        private VisualElement CreateLocalLightMotionInspector(Light light)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("ht-column");
            host.AddToClassList("ht-gap-4");

            if (light == null || light.transform == null)
            {
                host.Add(CreateHelpBox("No scene light found for motion controls.", HelpBoxMessageType.Info));
                return host;
            }

            LocalLightMotionState state = GetOrCreateLocalLightMotionState(light);

            Slider rotationSlider = new Slider("Rotation", 0f, 360f)
            {
                value = NormalizeYaw(light.transform.localEulerAngles.y),
                showInputField = true
            };
            rotationSlider.AddToClassList("ht-field");
            rotationSlider.AddToClassList("ht-character-controller-field");
            rotationSlider.RegisterValueChangedCallback(evt =>
            {
                if (Mathf.Approximately(evt.newValue, evt.previousValue))
                {
                    return;
                }

                ApplyTransformChange(
                    light,
                    "HoyoToon Update Scene Light Rotation",
                    transform =>
                    {
                        Vector3 euler = transform.localEulerAngles;
                        euler.y = NormalizeYaw(evt.newValue);
                        transform.localEulerAngles = euler;
                    });

                if (state.SyncToCameraRotation)
                    TickLocalLightMotion(light, state, rotationSlider);
            });
            host.Add(rotationSlider);

            Toggle syncToCameraToggle = new Toggle("Sync Light to Camera Rotation")
            {
                value = state.SyncToCameraRotation
            };
            syncToCameraToggle.AddToClassList("ht-toggle");
            syncToCameraToggle.AddToClassList("ht-character-controller-toggle");
            host.Add(syncToCameraToggle);

            Toggle autoRotateToggle = new Toggle("Auto Rotate")
            {
                value = state.AutoRotate
            };
            autoRotateToggle.AddToClassList("ht-toggle");
            autoRotateToggle.AddToClassList("ht-character-controller-toggle");
            host.Add(autoRotateToggle);

            Slider speedSlider = new Slider("Auto Rotate Speed", 0f, 180f)
            {
                value = state.AutoRotateSpeed,
                showInputField = true
            };
            speedSlider.AddToClassList("ht-field");
            speedSlider.AddToClassList("ht-character-controller-field");
            host.Add(speedSlider);

            EnumField directionField = new EnumField("Direction", state.Direction);
            directionField.AddToClassList("ht-field");
            directionField.AddToClassList("ht-character-controller-field");
            host.Add(directionField);

            void RefreshMotionFieldState()
            {
                bool syncToCamera = syncToCameraToggle.value;
                rotationSlider.SetEnabled(!syncToCamera);
                autoRotateToggle.SetEnabled(!syncToCamera);
                speedSlider.SetEnabled(!syncToCamera && autoRotateToggle.value);
                directionField.SetEnabled(!syncToCamera && autoRotateToggle.value);
            }

            syncToCameraToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                state.LastUpdateTime = 0d;
                if (evt.newValue)
                    BeginLocalLightCameraSyncSession(light, state, rotationSlider);
                else
                    EndLocalLightCameraSyncSession(light, state, rotationSlider);

                RefreshMotionFieldState();
                TickLocalLightMotion(light, state, rotationSlider);
            });

            autoRotateToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                state.AutoRotate = evt.newValue;
                state.LastUpdateTime = evt.newValue ? EditorApplication.timeSinceStartup : 0d;
                RefreshMotionFieldState();
            });

            speedSlider.RegisterValueChangedCallback(evt =>
            {
                if (Mathf.Approximately(evt.newValue, evt.previousValue))
                {
                    return;
                }

                state.AutoRotateSpeed = Mathf.Clamp(evt.newValue, 0f, 180f);
            });

            directionField.RegisterValueChangedCallback(evt =>
            {
                if (Equals(evt.newValue, evt.previousValue))
                {
                    return;
                }

                if (evt.newValue is LightMotionDirection direction)
                {
                    state.Direction = direction;
                }
            });

            RefreshMotionFieldState();

            host.schedule.Execute(() =>
            {
                TickLocalLightMotion(light, state, rotationSlider);
            }).Every(16);

            return host;
        }

        private static FloatField CreateLightFloatField(string label, float value, Action<float> onChanged)
        {
            FloatField field = new FloatField(label)
            {
                value = value
            };
            field.AddToClassList("ht-field");
            field.AddToClassList("ht-character-controller-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (Mathf.Approximately(evt.newValue, evt.previousValue))
                {
                    return;
                }

                onChanged?.Invoke(evt.newValue);
            });
            return field;
        }

        private static ColorField CreateLightColorField(string label, Color value, Action<Color> onChanged)
        {
            ColorField field = new ColorField(label)
            {
                value = value
            };
            field.AddToClassList("ht-field");
            field.AddToClassList("ht-character-controller-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                onChanged?.Invoke(evt.newValue);
            });
            return field;
        }

        private static Toggle CreateLightToggleField(string label, bool value, Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(label)
            {
                value = value
            };
            toggle.AddToClassList("ht-toggle");
            toggle.AddToClassList("ht-character-controller-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                onChanged?.Invoke(evt.newValue);
            });
            return toggle;
        }

        private static EnumField CreateLightEnumField(string label, Enum value, Action<Enum> onChanged)
        {
            EnumField field = new EnumField(label, value);
            field.AddToClassList("ht-field");
            field.AddToClassList("ht-character-controller-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (Equals(evt.newValue, evt.previousValue))
                {
                    return;
                }

                onChanged?.Invoke(evt.newValue);
            });
            return field;
        }

        private static Vector3Field CreateTransformVector3Field(string label, Vector3 value, Action<Vector3> onChanged)
        {
            Vector3Field field = new Vector3Field(label)
            {
                value = value
            };
            field.AddToClassList("ht-field");
            field.AddToClassList("ht-character-controller-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == evt.previousValue)
                {
                    return;
                }

                onChanged?.Invoke(evt.newValue);
            });
            return field;
        }

        private static void TickLocalLightMotion(Light light, LocalLightMotionState state, Slider rotationSlider)
        {
            if (light == null || light.transform == null || state == null)
            {
                return;
            }

            if (state.SyncToCameraRotation)
            {
                if (!TryGetReferenceCameraYaw(out float cameraYaw))
                    return;

                float offsetYaw = CameraRelativeLightSourceYaw;
                if (rotationSlider != null && !Mathf.Approximately(NormalizeYaw(rotationSlider.value), offsetYaw))
                    rotationSlider.SetValueWithoutNotify(offsetYaw);

                float targetYaw = NormalizeYaw(cameraYaw + offsetYaw + CameraRelativeLightTransformYawOffset);
                Vector3 cameraRelativeEuler = light.transform.rotation.eulerAngles;
                if (Mathf.Approximately(NormalizeYaw(cameraRelativeEuler.y), targetYaw))
                    return;

                light.transform.rotation = Quaternion.Euler(cameraRelativeEuler.x, targetYaw, cameraRelativeEuler.z);
                EditorUtility.SetDirty(light.transform);
                return;
            }

            if (!state.AutoRotate)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (state.LastUpdateTime <= 0d)
            {
                state.LastUpdateTime = now;
                return;
            }

            float deltaTime = (float)(now - state.LastUpdateTime);
            state.LastUpdateTime = now;
            if (deltaTime <= 0f || state.AutoRotateSpeed <= 0f)
            {
                return;
            }

            float direction = state.Direction == LightMotionDirection.Clockwise ? 1f : -1f;
            Vector3 euler = light.transform.localEulerAngles;
            euler.y = NormalizeYaw(euler.y + (state.AutoRotateSpeed * deltaTime * direction));
            light.transform.localEulerAngles = euler;
            EditorUtility.SetDirty(light.transform);

            if (rotationSlider != null)
            {
                rotationSlider.SetValueWithoutNotify(euler.y);
            }
        }

        private static void ApplyTransformChange(Light light, string undoName, Action<Transform> apply)
        {
            if (light == null || light.transform == null || apply == null)
            {
                return;
            }

            Undo.RecordObject(light.transform, undoName);
            apply(light.transform);
            EditorUtility.SetDirty(light.transform);
        }

        private static void BeginLocalLightCameraSyncSession(Light light, LocalLightMotionState state, Slider rotationSlider)
        {
            if (light == null || light.transform == null || state == null)
                return;

            if (!state.HasCameraSyncSnapshot)
            {
                state.CameraSyncLocalPosition = light.transform.localPosition;
                state.CameraSyncLocalRotation = light.transform.localRotation;
                state.CameraSyncLocalScale = light.transform.localScale;
                state.HasCameraSyncSnapshot = true;
            }

            state.SyncToCameraRotation = true;
            if (rotationSlider != null)
                rotationSlider.SetValueWithoutNotify(CameraRelativeLightSourceYaw);
        }

        private static void EndLocalLightCameraSyncSession(Light light, LocalLightMotionState state, Slider rotationSlider)
        {
            if (state == null)
                return;

            state.SyncToCameraRotation = false;

            if (state.HasCameraSyncSnapshot && light != null && light.transform != null)
            {
                light.transform.localPosition = state.CameraSyncLocalPosition;
                light.transform.localRotation = state.CameraSyncLocalRotation;
                light.transform.localScale = state.CameraSyncLocalScale;
                EditorUtility.SetDirty(light.transform);

                if (rotationSlider != null)
                    rotationSlider.SetValueWithoutNotify(NormalizeYaw(light.transform.localEulerAngles.y));
            }

            state.HasCameraSyncSnapshot = false;
            state.CameraSyncLocalPosition = Vector3.zero;
            state.CameraSyncLocalRotation = Quaternion.identity;
            state.CameraSyncLocalScale = Vector3.one;
        }

        private static void ApplyHsrMainLightTransformChange(
            HSRSceneController controller,
            Light light,
            string undoName,
            Action<HSRSceneController> applyController,
            Action<Transform> applyTransform)
        {
            if (controller == null || light == null || light.transform == null || applyController == null || applyTransform == null)
            {
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(controller, undoName);
            Undo.RecordObject(light.transform, undoName);
            applyController(controller);
            applyTransform(light.transform);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(light.transform);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void ApplyLightChange(Light light, string undoName, Action<Light> apply)
        {
            if (light == null || apply == null)
            {
                return;
            }

            Undo.RecordObject(light, undoName);
            apply(light);
            EditorUtility.SetDirty(light);
        }

        private static void ApplyHsrMainLightChange(
            HSRSceneController controller,
            Light light,
            string undoName,
            Action<HSRSceneController> applyController,
            Action<Light> applyLight)
        {
            if (controller == null || applyController == null)
            {
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObject(controller, undoName);
            if (light != null)
            {
                Undo.RecordObject(light, undoName);
            }

            applyController(controller);
            applyLight?.Invoke(light);
            EditorUtility.SetDirty(controller);
            if (light != null)
            {
                EditorUtility.SetDirty(light);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static Button CreateActionButton(string label)
        {
            Button button = new Button
            {
                text = label ?? string.Empty
            };
            button.AddToClassList("ht-btn-secondary");
            button.AddToClassList("ht-main-action-button");
            return button;
        }

        private static List<SceneLightEntry> BuildSceneLightEntries(Component controller)
        {
            List<Light> lights = FindSceneLights(controller);
            Light mainLight = GetProtectedMainLight(controller);

            lights.Sort((left, right) =>
            {
                bool leftIsMain = left == mainLight;
                bool rightIsMain = right == mainLight;
                if (leftIsMain != rightIsMain)
                {
                    return leftIsMain ? -1 : 1;
                }

                int nameComparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left != null ? left.gameObject.name : string.Empty,
                    right != null ? right.gameObject.name : string.Empty);
                if (nameComparison != 0)
                {
                    return nameComparison;
                }

                return (left != null ? left.GetInstanceID() : 0).CompareTo(right != null ? right.GetInstanceID() : 0);
            });

            Dictionary<string, int> labelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<SceneLightEntry> entries = new List<SceneLightEntry>(lights.Count);

            for (int index = 0; index < lights.Count; index++)
            {
                Light light = lights[index];
                if (light == null)
                {
                    continue;
                }

                bool isMainLight = light == mainLight;
                string lightName = GetDisplayValue(light.gameObject != null ? light.gameObject.name : string.Empty, "Scene Light");
                string baseLabel = (isMainLight ? "Main Light - " : string.Empty) + lightName + " (" + light.type + ")";

                if (!labelCounts.TryGetValue(baseLabel, out int duplicateCount))
                {
                    duplicateCount = 0;
                }

                labelCounts[baseLabel] = duplicateCount + 1;
                string resolvedLabel = duplicateCount > 0
                    ? baseLabel + " " + (duplicateCount + 1)
                    : baseLabel;

                entries.Add(new SceneLightEntry(light, resolvedLabel, isMainLight));
            }

            return entries;
        }

        private static List<Light> FindSceneLights(Component controller)
        {
            List<Light> lights = new List<Light>();
            if (!IsSceneComponent(controller))
            {
                return lights;
            }

            UnityEngine.SceneManagement.Scene targetScene = controller.gameObject.scene;
            IReadOnlyList<Light> sceneLights = SceneLightService.GetLights(targetScene);
            for (int index = 0; index < sceneLights.Count; index++)
            {
                Light light = sceneLights[index];
                if (!IsSceneComponent(light))
                {
                    continue;
                }

                if (light.gameObject.scene != targetScene)
                {
                    continue;
                }

                if (s_ExcludedSceneLightNames.Contains(light.gameObject.name))
                {
                    continue;
                }

                lights.Add(light);
            }

            return lights;
        }

        private Light ResolveSelectedLight(IReadOnlyList<SceneLightEntry> entries, Light preferredMainLight)
        {
            if (entries == null || entries.Count <= 0)
            {
                return null;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                Light light = entries[index].Light;
                if (light != null && light.GetInstanceID() == selectedLightInstanceId)
                {
                    return light;
                }
            }

            if (preferredMainLight != null)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    if (entries[index].Light == preferredMainLight)
                    {
                        return preferredMainLight;
                    }
                }
            }

            return entries[0].Light;
        }

        private static Light CreateSceneLight(Component controller)
        {
            if (!IsSceneComponent(controller))
            {
                return null;
            }

            Transform parent = FindOrCreateLightsParent(controller);
            GameObject lightObject = new GameObject(GetUniqueSceneLightName(parent));
            Undo.RegisterCreatedObjectUndo(lightObject, "HoyoToon Add Scene Light");

            if (parent != null)
            {
                lightObject.transform.SetParent(parent, false);
            }

            Light newLight = lightObject.AddComponent<Light>();
            newLight.type = LightType.Directional;
            newLight.color = Color.white;
            newLight.intensity = 0.75f;
            newLight.useColorTemperature = false;

            Light mainLight = GetProtectedMainLight(controller);
            if (mainLight != null && mainLight.transform != null)
            {
                lightObject.transform.position = mainLight.transform.position;
                lightObject.transform.rotation = mainLight.transform.rotation * Quaternion.Euler(0f, 35f, 0f);
            }
            else
            {
                lightObject.transform.position = Vector3.zero;
                lightObject.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            }

            EditorUtility.SetDirty(lightObject);
            EditorUtility.SetDirty(newLight);
            SceneLightService.Invalidate(controller.gameObject.scene);
            return newLight;
        }

        private static void RemoveSceneLight(Light light)
        {
            if (light == null)
            {
                return;
            }

            GameObject owner = light.gameObject;
            if (owner == null)
            {
                Undo.DestroyObjectImmediate(light);
                return;
            }

            UnityEngine.SceneManagement.Scene ownerScene = owner.scene;
            Component[] components = owner.GetComponents<Component>();
            List<Component> lightDependentComponents = new List<Component>();
            bool hasNonLightSpecificComponents = false;

            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component == light || component is Transform)
                {
                    continue;
                }

                if (RequiresLightComponent(component.GetType()))
                {
                    lightDependentComponents.Add(component);
                    continue;
                }

                hasNonLightSpecificComponents = true;
            }

            s_LocalLightMotionStates.Remove(light.GetInstanceID());

            if (!hasNonLightSpecificComponents)
            {
                Undo.DestroyObjectImmediate(owner);
                SceneLightService.Invalidate(ownerScene);
                return;
            }

            for (int index = 0; index < lightDependentComponents.Count; index++)
            {
                if (lightDependentComponents[index] != null)
                {
                    Undo.DestroyObjectImmediate(lightDependentComponents[index]);
                }
            }

            Undo.DestroyObjectImmediate(light);
            SceneLightService.Invalidate(ownerScene);
        }

        private static string GetUniqueSceneLightName(Transform parent)
        {
            if (parent == null)
            {
                return SceneLightDefaultName;
            }

            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null)
                {
                    usedNames.Add(child.name);
                }
            }

            if (!usedNames.Contains(SceneLightDefaultName))
            {
                return SceneLightDefaultName;
            }

            int suffix = 2;
            while (true)
            {
                string candidate = SceneLightDefaultName + " " + suffix;
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private static Transform FindOrCreateLightsParent(Component controller)
        {
            if (!IsSceneComponent(controller))
            {
                return null;
            }

            UnityEngine.SceneManagement.Scene targetScene = controller.gameObject.scene;
            Transform[] allTransforms = UnityEngine.Resources.FindObjectsOfTypeAll<Transform>();

            Transform resolved = null;
            int bestScore = int.MaxValue;
            int bestInstanceId = int.MaxValue;

            for (int index = 0; index < allTransforms.Length; index++)
            {
                Transform transform = allTransforms[index];
                if (transform == null
                    || transform.gameObject == null
                    || transform.gameObject.scene != targetScene
                    || EditorUtility.IsPersistent(transform)
                    || EditorUtility.IsPersistent(transform.gameObject)
                    || !string.Equals(transform.name, "Lights", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int hierarchyDepth = 0;
                Transform current = transform;
                while (current.parent != null)
                {
                    hierarchyDepth++;
                    current = current.parent;
                }

                int instanceId = transform.GetInstanceID();
                if (hierarchyDepth < bestScore || (hierarchyDepth == bestScore && instanceId < bestInstanceId))
                {
                    resolved = transform;
                    bestScore = hierarchyDepth;
                    bestInstanceId = instanceId;
                }
            }

            if (resolved != null)
            {
                return resolved;
            }

            GameObject lightsRoot = new GameObject("Lights");
            Undo.RegisterCreatedObjectUndo(lightsRoot, "HoyoToon Create Lights Root");
            if (targetScene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightsRoot, targetScene);
            }

            return lightsRoot.transform;
        }

        private static Light GetProtectedMainLight(Component controller)
        {
            if (controller == null)
            {
                return null;
            }

            FieldInfo mainLightField = controller.GetType().GetField(
                "main_light",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return mainLightField != null ? mainLightField.GetValue(controller) as Light : null;
        }

        private static Component FindFirstSceneObjectOfType(Type componentType)
        {
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                return null;
            }

            Object[] loadedObjects = UnityEngine.Resources.FindObjectsOfTypeAll(componentType);
            Component resolved = null;
            int lowestInstanceId = int.MaxValue;

            for (int index = 0; index < loadedObjects.Length; index++)
            {
                Component component = loadedObjects[index] as Component;
                if (!IsSceneComponent(component))
                {
                    continue;
                }

                int instanceId = component.GetInstanceID();
                if (instanceId >= lowestInstanceId)
                {
                    continue;
                }

                lowestInstanceId = instanceId;
                resolved = component;
            }

            return resolved;
        }

        private static FieldInfo FindControllerField(Type controllerType, string fieldName)
        {
            Type current = controllerType;
            while (current != null && current != typeof(MonoBehaviour) && current != typeof(Component) && current != typeof(object))
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static bool RequiresLightComponent(Type componentType)
        {
            if (componentType == null)
            {
                return false;
            }

            object[] attributes = componentType.GetCustomAttributes(typeof(RequireComponent), true);
            for (int index = 0; index < attributes.Length; index++)
            {
                if (!(attributes[index] is RequireComponent requireComponent))
                {
                    continue;
                }

                if (IsRequiredLightType(requireComponent.m_Type0)
                    || IsRequiredLightType(requireComponent.m_Type1)
                    || IsRequiredLightType(requireComponent.m_Type2))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRequiredLightType(Type requiredType)
        {
            return requiredType == typeof(Light)
                || (requiredType != null && requiredType.IsSubclassOf(typeof(Light)));
        }

        private static LocalLightMotionState GetOrCreateLocalLightMotionState(Light light)
        {
            int instanceId = light != null ? light.GetInstanceID() : 0;
            if (!s_LocalLightMotionStates.TryGetValue(instanceId, out LocalLightMotionState state) || state == null)
            {
                state = new LocalLightMotionState();
                s_LocalLightMotionStates[instanceId] = state;
            }

            return state;
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw, 360f);
        }

        private static bool TryGetReferenceCameraYaw(out float yaw)
        {
            Camera referenceCamera = ResolveReferenceCamera();
            if (referenceCamera == null || referenceCamera.transform == null)
            {
                yaw = 0f;
                return false;
            }

            yaw = referenceCamera.transform.rotation.eulerAngles.y;
            return true;
        }

        private static Camera ResolveReferenceCamera()
        {
            if (Camera.main != null)
                return Camera.main;

            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                return SceneView.lastActiveSceneView.camera;

            if (Camera.current != null)
                return Camera.current;

            Camera[] cameras = Camera.allCameras;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera != null && camera.isActiveAndEnabled)
                    return camera;
            }

            return null;
        }

        private static bool IsSceneComponent(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.scene.IsValid()
                && !EditorUtility.IsPersistent(component)
                && !EditorUtility.IsPersistent(component.gameObject);
        }

        private static string GetDisplayValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void RegisterSceneFieldTarget(
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

            if (SerializedControllerInspectorUtility.ContainsAny(searchable, "Outline", "_OutlineScale"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Scene.OutlineScaleField",
                    fieldElement,
                    "Outline scale field",
                    "Scene");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(fieldElement, "Scene.OutlineScaleField");
            }

            if (SerializedControllerInspectorUtility.ContainsAny(searchable, "Highlight", "HighLight"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Scene.HighlightToggle",
                    fieldElement,
                    "Highlight toggle",
                    "Scene");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(fieldElement, "Scene.HighlightToggle");
            }

            if (SerializedControllerInspectorUtility.ContainsAny(searchable, "Shadow Grading", "LevelShadow", "ShadowColor"))
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Scene.ShadowGradingToggle",
                    fieldElement,
                    "Shadow grading control",
                    "Scene");
                SerializedControllerInspectorUtility.RegisterValueChangeSignals(fieldElement, "Scene.ShadowGradingToggle");
            }
        }

        private readonly struct SceneControllerResolver
        {
            public readonly string GameDisplayName;
            public readonly string ControllerDisplayName;
            public readonly Type ControllerType;
            public readonly IReadOnlyList<string> SupportedGameKeys;

            public SceneControllerResolver(
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

        private readonly struct SceneControllerMatch
        {
            public readonly Component Controller;
            public readonly SceneControllerResolver Descriptor;

            public SceneControllerMatch(Component controller, SceneControllerResolver descriptor)
            {
                Controller = controller;
                Descriptor = descriptor;
            }
        }

        private readonly struct SceneLightEntry
        {
            public readonly Light Light;
            public readonly string Label;
            public readonly bool IsMainLight;

            public SceneLightEntry(Light light, string label, bool isMainLight)
            {
                Light = light;
                Label = label;
                IsMainLight = isMainLight;
            }
        }

        private sealed class LocalLightMotionState
        {
            public bool SyncToCameraRotation;
            public bool HasCameraSyncSnapshot;
            public Vector3 CameraSyncLocalPosition;
            public Quaternion CameraSyncLocalRotation = Quaternion.identity;
            public Vector3 CameraSyncLocalScale = Vector3.one;
            public bool AutoRotate;
            public float AutoRotateSpeed = 50f;
            public LightMotionDirection Direction = LightMotionDirection.Clockwise;
            public double LastUpdateTime;
        }

        private enum LightMotionDirection
        {
            Clockwise,
            CounterClockwise
        }
    }
}
