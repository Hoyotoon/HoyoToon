#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.Renders;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Utilities.Renders;
using HoyoToon.Runtime.Character.HSR;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CameraState = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.CameraState;
using TurnaroundCaptureScope = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundCaptureScope;
using TurnaroundLayout = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundLayout;
using TurnaroundView = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundView;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal sealed class RendersModule : ManagerModuleBase
    {
        private const string PrefsPrefix = "HoyoToon.Editor.ScreenshotTool.";
        private const string DefaultSavePath = "Assets/HoyoToon/Renders";
        private const int DefaultWidth = 3840;
        private const int DefaultHeight = 2160;
        private const int MinScale = 1;
        private const int MaxScale = 8;
        private const int MaxFinalCaptureDimension = 8192;
        private const long MaxFinalCapturePixels = 33554432L;
        private const int TurnaroundPanelCount = 4;
        private const int DefaultTurnaroundGap = 10;
        private const int MinTurnaroundGap = 0;
        private const int MaxTurnaroundGap = 256;
        private const float DefaultTurnaroundPaddingMultiplier = 1.04f;
        private const float MinTurnaroundPaddingMultiplier = 0.9f;
        private const float MaxTurnaroundPaddingMultiplier = 1.5f;
        private const float TurnaroundOuterPaddingPercent = 0.04f;
        private const int MinTurnaroundOuterPadding = 8;
        private const float TurnaroundAutoFitScale = 0.94f;
        private const float TurnaroundSelfShadowLightFollow = 0.25f;
        private const string WidthKey = PrefsPrefix + "Width";
        private const string HeightKey = PrefsPrefix + "Height";
        private const string ScaleKey = PrefsPrefix + "Scale";
        private const string SavePathKey = PrefsPrefix + "SavePath";
        private const string TransparentKey = PrefsPrefix + "Transparent";
        private const string OpenAfterKey = PrefsPrefix + "OpenAfter";
        private const string WatermarkEnabledKey = PrefsPrefix + "WatermarkEnabled";
        private const string CameraKey = PrefsPrefix + "Camera";
        private const string WatermarkTextureKey = PrefsPrefix + "WatermarkTexture";
        private const string TurnaroundBackgroundKey = PrefsPrefix + "TurnaroundBackground";
        private const string TurnaroundGapKey = PrefsPrefix + "TurnaroundGap";
        private const string TurnaroundPaddingKey = PrefsPrefix + "TurnaroundPadding";
        private const string TurnaroundEnabledKey = PrefsPrefix + "TurnaroundEnabled";

        private static readonly string[] ScaleOptions =
        {
            "1x",
            "2x",
            "3x",
            "4x",
            "5x",
            "6x",
            "7x",
            "8x"
        };

        private static Texture2D s_DefaultTurnaroundBackground;

        private readonly EditorScreenshotCaptureService captureService = new EditorScreenshotCaptureService();

        private bool prefsLoaded;
        private bool isSelected;
        private int width = DefaultWidth;
        private int height = DefaultHeight;
        private int scale = MinScale;
        private int turnaroundGap = DefaultTurnaroundGap;
        private float turnaroundPaddingMultiplier = DefaultTurnaroundPaddingMultiplier;
        private string savePath = DefaultSavePath;
        private bool transparentBackground;
        private bool openAfterCapture;
        private bool enableWatermark;
        private bool turnaroundEnabled;
        private bool syncWithSceneView;
        private Camera captureCamera;
        private Texture2D watermarkTexture;
        private Texture2D turnaroundBackgroundTexture;
        private string lastCapturePath = string.Empty;
        private Camera syncedCamera;
        private CameraState syncedCameraState;
        private readonly List<Behaviour> disabledBrains = new List<Behaviour>();

        public override string Id => "renders";

        public override string DisplayName => "Renders";

        public override int Order => 4;

        protected override string Description => "Renders";

        public override void OnSelected(ModuleContext context)
        {
            isSelected = true;
            EnsureLoaded();
            UpdateSceneViewSyncRegistration();
        }

        public override void OnDeselected(ModuleContext context)
        {
            isSelected = false;
            EndSceneViewSync();
            EditorApplication.update -= HandleEditorUpdate;
            SavePrefs();
        }

        public override VisualElement CreateContent(ModuleContext context)
        {
            EnsureLoaded();
            PrepareGuidedTurnaroundStepIfNeeded();
            ClampSettings();

            if (captureCamera == null)
            {
                captureCamera = Camera.main;
            }

            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-12");
            root.AddToClassList("ht-module-root");

            Toggle syncToggle = null;
            DropdownField scaleField = null;
            Label outputSummaryLabel = null;
            Label turnaroundSummaryLabel = null;
            ObjectField watermarkField = null;
            Button captureButton = null;
            Button turnaroundButton = null;
            Button openLastButton = null;
            Button openFolderButton = null;

            VisualElement cameraCard = CreateCard();
            AddCardHeader(cameraCard, "Camera");

            ObjectField cameraField = new ObjectField("Capture Camera")
            {
                objectType = typeof(Camera),
                allowSceneObjects = true,
                value = captureCamera
            };
            cameraField.AddToClassList("ht-field");
            cameraCard.Add(cameraField);

            VisualElement cameraActions = CreateFieldAlignedActionRow();
            Button useMainButton = CreateSecondaryButton("Use Main", () =>
            {
                captureCamera = Camera.main;
                cameraField.SetValueWithoutNotify(captureCamera);
                UpdateSceneViewSyncRegistration();
                SavePrefs();
                RefreshUi();
            });

            cameraActions.Add(useMainButton);
            cameraCard.Add(cameraActions);

            syncToggle = new Toggle("Sync With Scene View")
            {
                value = syncWithSceneView
            };
            syncToggle.AddToClassList("ht-toggle");
            cameraCard.Add(syncToggle);

            root.Add(cameraCard);
            root.Add(CreateDivider());

            VisualElement outputCard = CreateCard();
            AddCardHeader(outputCard, "Output");
            OnboardingTargetRegistry.RegisterVisualElement("Render.SettingsBox", outputCard, "Render settings", "Renders");

            IntegerField widthField = CreateIntegerField("Width", width);
            IntegerField heightField = CreateIntegerField("Height", height);
            scaleField = CreateDropdown("Scale", ScaleOptions, Mathf.Clamp(scale - 1, 0, ScaleOptions.Length - 1));

            Toggle transparentToggle = new Toggle("Transparent Background")
            {
                value = transparentBackground
            };
            transparentToggle.AddToClassList("ht-toggle");

            Toggle openAfterToggle = new Toggle("Open After Capture")
            {
                value = openAfterCapture
            };
            openAfterToggle.AddToClassList("ht-toggle");
            OnboardingTargetRegistry.RegisterVisualElement("Render.OpenAfterCaptureToggle", openAfterToggle, "Open After Capture toggle", "Renders");

            Toggle watermarkToggle = new Toggle("Watermark")
            {
                value = enableWatermark
            };
            watermarkToggle.AddToClassList("ht-toggle");

            watermarkField = new ObjectField("Watermark Texture")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = watermarkTexture
            };
            watermarkField.AddToClassList("ht-field");

            TextField savePathField = new TextField("Save Path")
            {
                value = savePath ?? string.Empty
            };
            savePathField.AddToClassList("ht-field");
            OnboardingTargetRegistry.RegisterVisualElement("Render.OutputPathField", savePathField, "Render output path", "Renders");

            VisualElement savePathRow = CreateActionRow();
            savePathRow.Add(savePathField);
            Button browseButton = CreateSecondaryButton("Browse", () =>
            {
                string selectedPath = EditorUtility.OpenFolderPanel(
                    "Save renders to",
                    GetAbsoluteSavePath(),
                    Application.dataPath);
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    return;
                }

                savePath = EditorScreenshotCaptureService.NormalizeSavePath(selectedPath);
                savePathField.SetValueWithoutNotify(savePath);
                SavePrefs();
                RefreshUi();
            });
            browseButton.style.minWidth = 84f;
            savePathRow.Add(browseButton);

            outputSummaryLabel = new Label();
            outputSummaryLabel.AddToClassList("ht-caption");

            outputCard.Add(widthField);
            outputCard.Add(heightField);
            outputCard.Add(scaleField);
            outputCard.Add(transparentToggle);
            outputCard.Add(openAfterToggle);
            outputCard.Add(watermarkToggle);
            outputCard.Add(watermarkField);
            outputCard.Add(savePathRow);
            outputCard.Add(outputSummaryLabel);
            root.Add(outputCard);
            root.Add(CreateDivider());

            VisualElement turnaroundCard = CreateCard();
            AddCardHeader(turnaroundCard, "Turnaround");
            OnboardingTargetRegistry.RegisterVisualElement("Render.TurnaroundSettingsBox", turnaroundCard, "Turnaround settings", "Renders");

            Toggle turnaroundToggle = new Toggle("Enable Turnaround")
            {
                value = turnaroundEnabled
            };
            turnaroundToggle.AddToClassList("ht-toggle");
            OnboardingTargetRegistry.RegisterVisualElement("Render.TurnaroundToggle", turnaroundToggle, "Turnaround toggle", "Renders");
            turnaroundCard.Add(turnaroundToggle);

            ObjectField backgroundField = new ObjectField("Background Texture")
            {
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
                value = turnaroundBackgroundTexture
            };
            backgroundField.AddToClassList("ht-field");
            turnaroundCard.Add(backgroundField);

            IntegerField gapField = CreateIntegerField("Panel Gap", turnaroundGap);
            Slider paddingField = new Slider("Bounds Padding", MinTurnaroundPaddingMultiplier, MaxTurnaroundPaddingMultiplier)
            {
                value = turnaroundPaddingMultiplier,
                showInputField = true
            };
            paddingField.AddToClassList("ht-field");
            paddingField.AddToClassList("ht-render-short-slider");

            turnaroundSummaryLabel = new Label();
            turnaroundSummaryLabel.AddToClassList("ht-caption");

            turnaroundCard.Add(gapField);
            turnaroundCard.Add(paddingField);
            turnaroundCard.Add(turnaroundSummaryLabel);
            root.Add(turnaroundCard);
            root.Add(CreateDivider());

            VisualElement captureCard = CreateCard();
            AddCardHeader(captureCard, "Capture");

            VisualElement primaryCaptureActions = CreateCenteredActionRow();
            captureButton = CreatePrimaryButton("Capture Screenshot", () =>
            {
                OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Render);
                OnboardingSignals.RecordAction("Render.CreateRenderButton");
                if (TryCaptureScreenshot(context))
                {
                    OnboardingSignals.RecordOperationSuccess(OnboardingOperationKind.Render);
                }
                else
                {
                    OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.Render, "Capture could not be completed. Check your camera and output settings.");
                    HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Capture Screenshot", "Capture could not be completed. Check your camera and output settings.", "OK");
                }

                RefreshUi();
            });
            captureButton.style.minWidth = 180f;
            OnboardingTargetRegistry.RegisterVisualElement("Render.CreateRenderButton", captureButton, "Capture Screenshot", "Renders");
            turnaroundButton = CreatePrimaryButton("Capture Turnaround", () =>
            {
                OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.TurnaroundRender);
                OnboardingSignals.RecordAction("Render.CreateTurnaroundButton");
                if (TryCaptureTurnaround(context))
                {
                    OnboardingSignals.RecordOperationSuccess(OnboardingOperationKind.TurnaroundRender);
                }
                else
                {
                    OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.TurnaroundRender, "Turnaround capture could not be completed. Check the active model, camera, and output settings.");
                    HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Capture Turnaround", "Turnaround capture could not be completed. Check the active model, camera, and output settings.", "OK");
                }

                RefreshUi();
            });
            turnaroundButton.style.minWidth = 180f;
            OnboardingTargetRegistry.RegisterVisualElement("Render.CreateTurnaroundButton", turnaroundButton, "Capture Turnaround", "Renders");
            primaryCaptureActions.Add(captureButton);
            primaryCaptureActions.Add(turnaroundButton);
            captureCard.Add(primaryCaptureActions);

            VisualElement secondaryCaptureActions = CreateCenteredActionRow();
            openLastButton = CreateSecondaryButton("Open Last", () =>
            {
                EditorScreenshotCaptureService.OpenFile(lastCapturePath);
                RefreshUi();
            });
            openLastButton.style.minWidth = 140f;
            openFolderButton = CreateSecondaryButton("Open Folder", () =>
            {
                EditorScreenshotCaptureService.OpenFolder(GetAbsoluteSavePath());
                RefreshUi();
            });
            openFolderButton.style.minWidth = 140f;
            secondaryCaptureActions.Add(openLastButton);
            secondaryCaptureActions.Add(openFolderButton);
            captureCard.Add(secondaryCaptureActions);
            root.Add(captureCard);

            cameraField.RegisterValueChangedCallback(evt =>
            {
                captureCamera = evt.newValue as Camera;
                UpdateSceneViewSyncRegistration();
                SavePrefs();
                RefreshUi();
            });

            syncToggle.RegisterValueChangedCallback(evt =>
            {
                syncWithSceneView = evt.newValue;
                UpdateSceneViewSyncRegistration();
                if (syncWithSceneView)
                {
                    CopySceneViewStateToCaptureCamera();
                }

                SavePrefs();
                RefreshUi();
            });

            widthField.RegisterValueChangedCallback(evt =>
            {
                width = Mathf.Max(1, evt.newValue);
                ClampSettings();
                SavePrefs();
                RefreshUi();
            });

            heightField.RegisterValueChangedCallback(evt =>
            {
                height = Mathf.Max(1, evt.newValue);
                ClampSettings();
                SavePrefs();
                RefreshUi();
            });

            scaleField.RegisterValueChangedCallback(evt =>
            {
                scale = ParseScaleValue(evt.newValue, scale);
                ClampSettings();
                SavePrefs();
                RefreshUi();
            });

            transparentToggle.RegisterValueChangedCallback(evt =>
            {
                transparentBackground = evt.newValue;
                SavePrefs();
            });

            openAfterToggle.RegisterValueChangedCallback(evt =>
            {
                openAfterCapture = evt.newValue;
                OnboardingSignals.RecordAction("Render.OpenAfterCaptureToggle");
                OnboardingSignals.RecordValueChanged("Render.OpenAfterCaptureToggle", evt.newValue);
                SavePrefs();
            });

            watermarkToggle.RegisterValueChangedCallback(evt =>
            {
                enableWatermark = evt.newValue;
                SavePrefs();
                RefreshUi();
            });

            watermarkField.RegisterValueChangedCallback(evt =>
            {
                watermarkTexture = evt.newValue as Texture2D;
                SavePrefs();
                RefreshUi();
            });

            savePathField.RegisterValueChangedCallback(evt =>
            {
                savePath = evt.newValue ?? string.Empty;
                SavePrefs();
                RefreshUi();
            });

            backgroundField.RegisterValueChangedCallback(evt =>
            {
                turnaroundBackgroundTexture = evt.newValue as Texture2D;
                SavePrefs();
            });

            turnaroundToggle.RegisterValueChangedCallback(evt =>
            {
                turnaroundEnabled = evt.newValue;
                OnboardingSignals.RecordAction("Render.TurnaroundToggle");
                OnboardingSignals.RecordValueChanged("Render.TurnaroundToggle", evt.newValue);
                SavePrefs();
                RefreshUi();
            });

            gapField.RegisterValueChangedCallback(evt =>
            {
                turnaroundGap = Mathf.Clamp(evt.newValue, MinTurnaroundGap, MaxTurnaroundGap);
                SavePrefs();
                RefreshUi();
            });

            paddingField.RegisterValueChangedCallback(evt =>
            {
                turnaroundPaddingMultiplier = Mathf.Clamp(evt.newValue, MinTurnaroundPaddingMultiplier, MaxTurnaroundPaddingMultiplier);
                SavePrefs();
                RefreshUi();
            });

            void RefreshUi()
            {
                ClampSettings();
                List<string> scaleChoices = BuildScaleOptionsForCurrentResolution();
                scaleField.choices = scaleChoices;
                scaleField.SetValueWithoutNotify(FormatScaleOption(scale));

                outputSummaryLabel.text = "Final Output: "
                    + GetFinalCaptureWidth()
                    + " x "
                    + GetFinalCaptureHeight()
                    + " px";
                string resolutionValidationMessage;
                bool resolutionValid = IsCaptureResolutionValid(out resolutionValidationMessage);
                if (!resolutionValid)
                {
                    outputSummaryLabel.text += " | " + resolutionValidationMessage;
                }

                GameObject resolvedModel = ResolveTargetModel(context);

                TurnaroundLayout layout;
                string validationMessage = string.Empty;
                if (resolvedModel != null
                    && TryBuildTurnaroundLayout(GetFinalCaptureWidth(), GetFinalCaptureHeight(), out layout, out validationMessage))
                {
                    turnaroundSummaryLabel.text =
                        "Composite: "
                        + layout.CompositeWidth
                        + " x "
                        + layout.CompositeHeight
                        + " px  |  Panel: "
                        + layout.PanelWidth
                        + " x "
                        + layout.PanelHeight
                        + " px";
                }
                else
                {
                    turnaroundSummaryLabel.text = string.IsNullOrWhiteSpace(validationMessage)
                        ? "Set an active model to preview turnaround output."
                        : validationMessage;
                }

                bool hasCamera = captureCamera != null;
                bool hasResolvedModel = resolvedModel != null;
                bool hasLastCapture = !string.IsNullOrWhiteSpace(lastCapturePath) && File.Exists(lastCapturePath);
                bool hasSaveFolder = !string.IsNullOrWhiteSpace(GetAbsoluteSavePath());

                syncToggle.SetEnabled(hasCamera);
                watermarkField.SetEnabled(enableWatermark);
                captureButton.SetEnabled(hasCamera && resolutionValid);
                turnaroundButton.SetEnabled(turnaroundEnabled && hasCamera && hasResolvedModel && resolutionValid);
                openLastButton.SetEnabled(hasLastCapture);
                openFolderButton.SetEnabled(hasSaveFolder);
            }

            RefreshUi();
            return root;
        }

        protected override IEnumerable<VisualElement> BuildCards(ModuleContext context)
        {
            yield break;
        }

        private static VisualElement CreateActionRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ht-row");
            row.AddToClassList("ht-gap-8");
            row.style.flexWrap = Wrap.Wrap;
            return row;
        }

        private static VisualElement CreateCenteredActionRow()
        {
            VisualElement row = CreateActionRow();
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static VisualElement CreateFieldAlignedActionRow()
        {
            VisualElement row = CreateActionRow();
            row.AddToClassList("ht-render-field-action-row");
            row.style.justifyContent = Justify.FlexStart;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static VisualElement CreateDivider()
        {
            VisualElement divider = new VisualElement();
            divider.AddToClassList("ht-divider");
            return divider;
        }

        private static int ParseScaleValue(string value, int fallback)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                string normalized = value.Trim().TrimEnd('x', 'X');
                if (int.TryParse(normalized, out int parsedScale))
                {
                    return Mathf.Clamp(parsedScale, MinScale, MaxScale);
                }
            }

            return Mathf.Clamp(fallback, MinScale, MaxScale);
        }

        private List<string> BuildScaleOptionsForCurrentResolution()
        {
            int maxAllowedScale = GetMaxAllowedScaleForCurrentResolution();
            var options = new List<string>();
            for (int currentScale = MinScale; currentScale <= maxAllowedScale; currentScale++)
            {
                options.Add(FormatScaleOption(currentScale));
            }

            if (options.Count == 0)
            {
                options.Add(FormatScaleOption(MinScale));
            }

            return options;
        }

        private static string FormatScaleOption(int value)
        {
            return Mathf.Clamp(value, MinScale, MaxScale).ToString() + "x";
        }

        private int GetMaxAllowedScaleForCurrentResolution()
        {
            int baseWidth = Mathf.Max(1, width);
            int baseHeight = Mathf.Max(1, height);
            int dimensionLimit = GetCaptureDimensionLimit();
            int maxByWidth = Mathf.Max(MinScale, dimensionLimit / baseWidth);
            int maxByHeight = Mathf.Max(MinScale, dimensionLimit / baseHeight);
            long basePixels = (long)baseWidth * baseHeight;
            int maxByPixels = MinScale;

            if (basePixels > 0)
            {
                double pixelScaleLimit = Math.Sqrt(MaxFinalCapturePixels / (double)basePixels);
                maxByPixels = Mathf.Max(MinScale, Mathf.FloorToInt((float)pixelScaleLimit));
            }

            int maxAllowedScale = Mathf.Min(Mathf.Min(maxByWidth, maxByHeight), maxByPixels);
            return Mathf.Clamp(maxAllowedScale, MinScale, MaxScale);
        }

        private bool IsCaptureResolutionValid(out string validationMessage)
        {
            int finalWidth = GetFinalCaptureWidth();
            int finalHeight = GetFinalCaptureHeight();
            int dimensionLimit = GetCaptureDimensionLimit();

            if (finalWidth > dimensionLimit || finalHeight > dimensionLimit)
            {
                validationMessage = "Reduce resolution or scale. Max safe side is " + dimensionLimit + " px.";
                return false;
            }

            long finalPixels = (long)finalWidth * finalHeight;
            if (finalPixels > MaxFinalCapturePixels)
            {
                validationMessage = "Reduce resolution or scale. Max safe output is " + MaxFinalCapturePixels + " pixels.";
                return false;
            }

            validationMessage = string.Empty;
            return true;
        }

        private static int GetCaptureDimensionLimit()
        {
            int systemLimit = Mathf.Max(1024, SystemInfo.maxTextureSize);
            return Mathf.Min(MaxFinalCaptureDimension, systemLimit);
        }

        private static Button CreatePrimaryButton(string label, Action onClick)
        {
            Button button = new Button(() => onClick?.Invoke())
            {
                text = label ?? string.Empty
            };
            button.AddToClassList("ht-btn-primary");
            return button;
        }

        private static Button CreateSecondaryButton(string label, Action onClick)
        {
            Button button = new Button(() => onClick?.Invoke())
            {
                text = label ?? string.Empty
            };
            button.AddToClassList("ht-btn-secondary");
            return button;
        }

        private static IntegerField CreateIntegerField(string label, int value)
        {
            IntegerField field = new IntegerField(label)
            {
                value = value
            };
            field.AddToClassList("ht-field");
            return field;
        }

        private void EnsureLoaded()
        {
            if (prefsLoaded)
            {
                return;
            }

            width = EditorPrefs.GetInt(PrefsKey(WidthKey), width);
            height = EditorPrefs.GetInt(PrefsKey(HeightKey), height);
            scale = Mathf.Clamp(EditorPrefs.GetInt(PrefsKey(ScaleKey), scale), MinScale, MaxScale);
            savePath = EditorPrefs.GetString(PrefsKey(SavePathKey), savePath);
            transparentBackground = EditorPrefs.GetBool(PrefsKey(TransparentKey), transparentBackground);
            openAfterCapture = EditorPrefs.GetBool(PrefsKey(OpenAfterKey), openAfterCapture);
            enableWatermark = EditorPrefs.GetBool(PrefsKey(WatermarkEnabledKey), enableWatermark);
            turnaroundEnabled = EditorPrefs.GetBool(PrefsKey(TurnaroundEnabledKey), turnaroundEnabled);
            turnaroundGap = Mathf.Clamp(EditorPrefs.GetInt(PrefsKey(TurnaroundGapKey), turnaroundGap), MinTurnaroundGap, MaxTurnaroundGap);
            turnaroundPaddingMultiplier = Mathf.Clamp(
                EditorPrefs.GetFloat(PrefsKey(TurnaroundPaddingKey), turnaroundPaddingMultiplier),
                MinTurnaroundPaddingMultiplier,
                MaxTurnaroundPaddingMultiplier);

            captureCamera = ResolveObject<Camera>(EditorPrefs.GetString(PrefsKey(CameraKey), string.Empty));
            watermarkTexture = ResolveObject<Texture2D>(EditorPrefs.GetString(PrefsKey(WatermarkTextureKey), string.Empty));
            turnaroundBackgroundTexture = ResolveObject<Texture2D>(EditorPrefs.GetString(PrefsKey(TurnaroundBackgroundKey), string.Empty));

            prefsLoaded = true;
        }

        private void PrepareGuidedTurnaroundStepIfNeeded()
        {
            OnboardingStep step = OnboardingManager.CurrentStep;
            if (!OnboardingManager.IsRunning
                || step == null
                || !string.Equals(step.Id, "Render.Turnaround.Enable", StringComparison.Ordinal)
                || OnboardingSignals.GetActionCountSinceStepStart("Render.TurnaroundToggle") > 0)
            {
                return;
            }

            turnaroundEnabled = false;
            EditorPrefs.SetBool(PrefsKey(TurnaroundEnabledKey), false);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefsKey(WidthKey), width);
            EditorPrefs.SetInt(PrefsKey(HeightKey), height);
            EditorPrefs.SetInt(PrefsKey(ScaleKey), scale);
            EditorPrefs.SetString(PrefsKey(SavePathKey), savePath ?? string.Empty);
            EditorPrefs.SetBool(PrefsKey(TransparentKey), transparentBackground);
            EditorPrefs.SetBool(PrefsKey(OpenAfterKey), openAfterCapture);
            EditorPrefs.SetBool(PrefsKey(WatermarkEnabledKey), enableWatermark);
            EditorPrefs.SetBool(PrefsKey(TurnaroundEnabledKey), turnaroundEnabled);
            EditorPrefs.SetString(PrefsKey(CameraKey), SerializeObject(captureCamera));
            EditorPrefs.SetString(PrefsKey(WatermarkTextureKey), SerializeObject(watermarkTexture));
            EditorPrefs.SetString(PrefsKey(TurnaroundBackgroundKey), SerializeObject(turnaroundBackgroundTexture));
            EditorPrefs.SetInt(PrefsKey(TurnaroundGapKey), turnaroundGap);
            EditorPrefs.SetFloat(PrefsKey(TurnaroundPaddingKey), turnaroundPaddingMultiplier);
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }

        private void ClampSettings()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            scale = Mathf.Clamp(scale, MinScale, GetMaxAllowedScaleForCurrentResolution());
            turnaroundGap = Mathf.Clamp(turnaroundGap, MinTurnaroundGap, MaxTurnaroundGap);
            turnaroundPaddingMultiplier = Mathf.Clamp(
                turnaroundPaddingMultiplier,
                MinTurnaroundPaddingMultiplier,
                MaxTurnaroundPaddingMultiplier);
        }

        private int GetFinalCaptureWidth()
        {
            return Mathf.Max(1, width * Mathf.Max(1, scale));
        }

        private int GetFinalCaptureHeight()
        {
            return Mathf.Max(1, height * Mathf.Max(1, scale));
        }

        private string GetAbsoluteSavePath()
        {
            return EditorScreenshotCaptureService.GetAbsoluteSavePath(savePath);
        }

        private Texture2D ResolveWatermarkTexture()
        {
            if (!enableWatermark)
            {
                return null;
            }

            return watermarkTexture != null
                ? watermarkTexture
                : EditorScreenshotCaptureService.TryLoadDefaultWatermarkTexture();
        }

        private Texture2D ResolveTurnaroundBackgroundTexture()
        {
            if (turnaroundBackgroundTexture != null)
            {
                return turnaroundBackgroundTexture;
            }

            if (s_DefaultTurnaroundBackground != null)
            {
                return s_DefaultTurnaroundBackground;
            }

            s_DefaultTurnaroundBackground = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32 fill = new Color32(18, 20, 27, 255);
            Color32[] pixels = new Color32[16];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = fill;
            }

            s_DefaultTurnaroundBackground.SetPixels32(pixels);
            s_DefaultTurnaroundBackground.Apply(false);
            return s_DefaultTurnaroundBackground;
        }

        private bool TryCaptureScreenshot(ModuleContext context)
        {
            if (captureCamera == null)
            {
                return false;
            }

            string resolutionValidationMessage;
            if (!IsCaptureResolutionValid(out resolutionValidationMessage))
            {
                Debug.LogWarning("[HoyoToon] Render capture blocked: " + resolutionValidationMessage);
                return false;
            }

            string savedPath;
            bool captured = captureService.TryCaptureToFile(
                captureCamera,
                GetFinalCaptureWidth(),
                GetFinalCaptureHeight(),
                GetAbsoluteSavePath(),
                transparentBackground,
                ResolveWatermarkTexture(),
                openAfterCapture,
                BuildFilePrefix("screen", ResolveTargetModel(context)),
                out savedPath);

            if (captured)
            {
                lastCapturePath = savedPath ?? string.Empty;
            }

            return captured;
        }

        private bool TryCaptureTurnaround(ModuleContext context)
        {
            GameObject resolvedModel = ResolveTargetModel(context);
            if (captureCamera == null || resolvedModel == null)
            {
                return false;
            }

            string resolutionValidationMessage;
            if (!IsCaptureResolutionValid(out resolutionValidationMessage))
            {
                Debug.LogWarning("[HoyoToon] Turnaround capture blocked: " + resolutionValidationMessage);
                return false;
            }

            Bounds bounds;
            if (!TurnaroundCaptureUtility.TryGetModelBounds(resolvedModel, out bounds))
            {
                return false;
            }

            TurnaroundLayout layout;
            string validationMessage;
            if (!TryBuildTurnaroundLayout(GetFinalCaptureWidth(), GetFinalCaptureHeight(), out layout, out validationMessage))
            {
                return false;
            }

            TurnaroundView[] views = TurnaroundCaptureUtility.BuildViews(resolvedModel.transform);
            float orthographicSize = TurnaroundCaptureUtility.CalculateOrthographicSize(
                bounds,
                views,
                layout.PanelWidth,
                layout.PanelHeight,
                turnaroundPaddingMultiplier,
                MinTurnaroundPaddingMultiplier,
                TurnaroundAutoFitScale);

            CameraState originalCameraState = TurnaroundCaptureUtility.CaptureState(captureCamera);
            List<Texture2D> panels = new List<Texture2D>();
            TurnaroundCaptureScope scope = null;
            Texture2D composite = null;

            try
            {
                scope = new TurnaroundCaptureScope(
                    resolvedModel,
                    TurnaroundCaptureUtility.GetDirectionalLights(resolvedModel),
                    TurnaroundSelfShadowLightFollow);

                for (int index = 0; index < views.Length; index++)
                {
                    scope.ApplyView(views[index]);
                    TurnaroundCaptureUtility.PositionCamera(captureCamera, originalCameraState, bounds, views[index], orthographicSize);

                    Texture2D panel = captureService.CaptureTexture(
                        captureCamera,
                        layout.PanelWidth,
                        layout.PanelHeight,
                        true);
                    if (panel == null)
                    {
                        return false;
                    }

                    panels.Add(panel);
                }

                composite = TurnaroundCaptureUtility.CreateComposite(panels, ResolveTurnaroundBackgroundTexture(), layout);
                string savedPath = captureService.SaveTexture(
                    composite,
                    GetAbsoluteSavePath(),
                    ResolveWatermarkTexture(),
                    openAfterCapture,
                    BuildFilePrefix("turnaround", resolvedModel));
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    return false;
                }

                lastCapturePath = savedPath;
                return true;
            }
            finally
            {
                if (scope != null)
                {
                    scope.Dispose();
                }

                TurnaroundCaptureUtility.RestoreState(captureCamera, originalCameraState);

                for (int index = 0; index < panels.Count; index++)
                {
                    if (panels[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(panels[index]);
                    }
                }

                if (composite != null)
                {
                    UnityEngine.Object.DestroyImmediate(composite);
                }
            }
        }

        private void UpdateSceneViewSyncRegistration()
        {
            EditorApplication.update -= HandleEditorUpdate;
            if (isSelected && syncWithSceneView && captureCamera != null)
            {
                BeginSceneViewSync();
                EditorApplication.update += HandleEditorUpdate;
                return;
            }

            EndSceneViewSync();
        }

        private void HandleEditorUpdate()
        {
            if (!isSelected || !syncWithSceneView)
            {
                return;
            }

            CopySceneViewStateToCaptureCamera();
        }

        private void BeginSceneViewSync()
        {
            if (captureCamera == null)
            {
                return;
            }

            if (syncedCamera == captureCamera)
            {
                DisableCinemachineBrainsForSync();
                CopySceneViewStateToCaptureCamera();
                return;
            }

            EndSceneViewSync();
            syncedCamera = captureCamera;
            syncedCameraState = TurnaroundCaptureUtility.CaptureState(captureCamera);
            DisableCinemachineBrainsForSync();
            CopySceneViewStateToCaptureCamera();
        }

        private void EndSceneViewSync()
        {
            EnableDisabledBrains();

            if (syncedCamera != null)
            {
                TurnaroundCaptureUtility.RestoreState(syncedCamera, syncedCameraState);
                syncedCamera = null;
            }
        }

        private void CopySceneViewStateToCaptureCamera()
        {
            Camera sceneViewCamera = GetSceneViewCamera();
            if (captureCamera == null || sceneViewCamera == null)
            {
                return;
            }

            TurnaroundCaptureUtility.ApplyCameraState(
                captureCamera,
                TurnaroundCaptureUtility.CaptureState(sceneViewCamera));
            EditorUtility.SetDirty(captureCamera);
            if (captureCamera.transform != null)
            {
                EditorUtility.SetDirty(captureCamera.transform);
            }
        }

        private void DisableCinemachineBrainsForSync()
        {
            DisableCinemachineBrain(captureCamera);
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera != captureCamera)
            {
                DisableCinemachineBrain(mainCamera);
            }
        }

        private void DisableCinemachineBrain(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            Behaviour[] behaviours = camera.GetComponents<Behaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                Behaviour behaviour = behaviours[index];
                if (behaviour == null
                    || !behaviour.enabled
                    || !string.Equals(behaviour.GetType().Name, "CinemachineBrain", StringComparison.Ordinal)
                    || disabledBrains.Contains(behaviour))
                {
                    continue;
                }

                behaviour.enabled = false;
                disabledBrains.Add(behaviour);
            }
        }

        private void EnableDisabledBrains()
        {
            for (int index = 0; index < disabledBrains.Count; index++)
            {
                Behaviour behaviour = disabledBrains[index];
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            disabledBrains.Clear();
        }

        private static Camera GetSceneViewCamera()
        {
            return SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : null;
        }

        private bool TryBuildTurnaroundLayout(
            int outputWidth,
            int outputHeight,
            out TurnaroundLayout layout,
            out string validationMessage)
        {
            return TurnaroundCaptureUtility.TryBuildLayout(
                outputWidth,
                outputHeight,
                TurnaroundPanelCount,
                turnaroundGap,
                TurnaroundOuterPaddingPercent,
                MinTurnaroundOuterPadding,
                out layout,
                out validationMessage,
                "Output exceeds the current texture size limit.",
                "Turnaround layout does not fit in the selected resolution.",
                "Turnaround panel width is too small.");
        }

        private static string SerializeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return globalObjectId.ToString();
        }

        private static T ResolveObject<T>(string id) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            GlobalObjectId globalObjectId;
            if (!GlobalObjectId.TryParse(id, out globalObjectId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as T;
        }

        private static GameObject NormalizeModelRoot(GameObject candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            HSRCharacterController parentController = candidate.GetComponentInParent<HSRCharacterController>(true);
            if (parentController != null)
            {
                return parentController.gameObject;
            }

            HSRCharacterController childController = candidate.GetComponentInChildren<HSRCharacterController>(true);
            if (childController != null)
            {
                return childController.gameObject;
            }

            return candidate;
        }

        private GameObject ResolveTargetModel(ModuleContext context)
        {
            if (context != null && context.PlacementActiveModel != null)
            {
                return NormalizeModelRoot(context.PlacementActiveModel);
            }

            return null;
        }

        private static string BuildFilePrefix(string prefix, GameObject targetModel)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "screen" : prefix.Trim();
            string safeName = targetModel != null ? SanitizeFileName(targetModel.name) : string.Empty;
            return string.IsNullOrWhiteSpace(safeName)
                ? safePrefix
                : safePrefix + "_" + safeName;
        }

        private static string SanitizeFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] safeChars = rawName.Trim().ToCharArray();
            for (int index = 0; index < safeChars.Length; index++)
            {
                if (Array.IndexOf(invalidChars, safeChars[index]) >= 0)
                {
                    safeChars[index] = '_';
                }
            }

            return new string(safeChars);
        }
    }
}
#endif
