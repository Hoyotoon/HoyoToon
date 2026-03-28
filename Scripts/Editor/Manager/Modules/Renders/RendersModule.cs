#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Editor.Utilities;
using HoyoToon.Runtime.Character;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class RendersModule : ManagerModule
    {
        public override string DisplayName => "Renders";
        internal override string NavbarTourTarget => "tour.modules.renders";

        private const int DefaultWidth = 3840;
        private const int DefaultHeight = 2160;
        private const int MinScale = 1;
        private const int MaxScale = 8;
        private const string DefaultTurnaroundBackgroundResourcePath = "UI/1x1";
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

        private int _resWidth = DefaultWidth;
        private int _resHeight = DefaultHeight;
        private int _scale = 1;
        private Camera _camera;
        private string _savePath = "Assets/HoyoToon/Renders";
        private bool _transparent;
        private bool _openAfter;
        private bool _watermark;
        private string _lastScreenshot;
        private bool _turnaroundEnabled;
        private int _turnaroundGap = DefaultTurnaroundGap;
        private float _turnaroundPaddingMultiplier = DefaultTurnaroundPaddingMultiplier;
        private Texture2D _turnaroundBackgroundTexture;
        private bool _syncSceneCamera;
        private Camera _syncedCamera;
        private CameraState _syncedState;
        private bool _disposed;
        private bool _prefsLoaded;
        private Behaviour _disabledBrain;
        private bool _showCameraSettings = true;
        private bool _showOutputSettings = true;
        private bool _showCaptureActions = true;

        private static Texture2D _defaultTurnaroundBackgroundTexture;

        private readonly ScreenshotCapture _screenshotCapture = new ScreenshotCapture();

        private struct TurnaroundView
        {
            public Vector3 CameraOffsetDirection;
            public float LightYaw;

            public TurnaroundView(Vector3 cameraOffsetDirection, float lightYaw)
            {
                CameraOffsetDirection = cameraOffsetDirection;
                LightYaw = lightYaw;
            }
        }

        private struct TurnaroundLayout
        {
            public int CompositeWidth;
            public int CompositeHeight;
            public int PanelWidth;
            public int PanelHeight;
            public int StartX;
            public int StartY;
            public int Gap;
        }

        private sealed class TurnaroundCaptureScope : IDisposable
        {
            private readonly List<Renderer> _disabledRenderers = new List<Renderer>();
            private readonly List<Light> _lights = new List<Light>();
            private readonly List<Vector3> _originalEulerAngles = new List<Vector3>();
            private readonly HSRCharacterController _controller;
            private readonly float _originalValue;
            private readonly bool _hasOverride;

            public TurnaroundCaptureScope(GameObject activeModel, IEnumerable<Light> lights, float selfShadowOverride)
            {
                CaptureIsolatedRenderers(activeModel);
                CaptureDirectionalLights(lights);
                _controller = ResolveCharacterController(activeModel);
                if (_controller == null)
                {
                    return;
                }

                _originalValue = _controller.CharacterSelfShadowLightFollow;
                if (Mathf.Approximately(_originalValue, selfShadowOverride))
                {
                    return;
                }

                _controller.CharacterSelfShadowLightFollow = selfShadowOverride;
                _controller.SyncToRenderer();
                _hasOverride = true;
            }

            public void ApplyView(TurnaroundView view)
            {
                ApplyLightYaw(view.LightYaw);
            }

            private void CaptureIsolatedRenderers(GameObject activeModel)
            {
                if (activeModel == null)
                {
                    return;
                }

                var activeRoot = activeModel.transform;
                var activeScene = activeModel.scene;
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (renderer.gameObject.scene != activeScene || renderer.transform.IsChildOf(activeRoot))
                    {
                        continue;
                    }

                    renderer.enabled = false;
                    _disabledRenderers.Add(renderer);
                }
            }

            private void CaptureDirectionalLights(IEnumerable<Light> lights)
            {
                if (lights == null)
                {
                    return;
                }

                foreach (var light in lights)
                {
                    if (light == null || light.type != LightType.Directional)
                    {
                        continue;
                    }

                    _lights.Add(light);
                    _originalEulerAngles.Add(light.transform.localEulerAngles);
                }
            }

            private void ApplyLightYaw(float yaw)
            {
                float normalizedYaw = Mathf.Repeat(yaw, 360f);
                for (int i = 0; i < _lights.Count; i++)
                {
                    var light = _lights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    Vector3 originalEuler = _originalEulerAngles[i];
                    light.transform.localEulerAngles = new Vector3(originalEuler.x, normalizedYaw, originalEuler.z);
                }
            }

            private void ClearCapturedState()
            {
                _disabledRenderers.Clear();
                _lights.Clear();
                _originalEulerAngles.Clear();
            }

            public void Dispose()
            {
                for (int i = 0; i < _disabledRenderers.Count; i++)
                {
                    if (_disabledRenderers[i] != null)
                    {
                        _disabledRenderers[i].enabled = true;
                    }
                }

                for (int i = 0; i < _lights.Count; i++)
                {
                    var light = _lights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    light.transform.localEulerAngles = _originalEulerAngles[i];
                }

                if (!_hasOverride || _controller == null)
                {
                    ClearCapturedState();
                    return;
                }

                _controller.CharacterSelfShadowLightFollow = _originalValue;
                _controller.SyncToRenderer();
                ClearCapturedState();
            }
        }

        private struct CameraState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
            public float NearClip;
            public float FarClip;
            public bool Orthographic;
            public float OrthoSize;
        }

        public RendersModule()
        {
            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            LoadPrefs();
        }

        public override void OnGUI(HoyoToonManager targetManager)
        {
            EnsurePrefsLoaded();

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            EditorGUI.BeginChangeCheck();
            _showCameraSettings = DrawFoldoutSection("Camera", _showCameraSettings, DrawCameraSection);
            EditorGUILayout.Space(4f);
            _showOutputSettings = DrawFoldoutSection("Output", _showOutputSettings, DrawOutputSection);
            EditorGUILayout.Space(4f);
            _showCaptureActions = DrawFoldoutSection("Actions", _showCaptureActions, () => DrawActionsSection(targetManager));
            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }
        }


        private void EnsurePrefsLoaded()
        {
            if (_prefsLoaded)
            {
                return;
            }

            LoadPrefs();
        }

        private void LoadPrefs()
        {
            _prefsLoaded = true;

            _resWidth = EditorPrefs.GetInt(PrefsKeys.RenderResWidth, _resWidth);
            _resHeight = EditorPrefs.GetInt(PrefsKeys.RenderResHeight, _resHeight);
            _scale = Mathf.Clamp(EditorPrefs.GetInt(PrefsKeys.RenderScale, _scale), MinScale, MaxScale);
            _savePath = EditorPrefs.GetString(PrefsKeys.RenderSavePath, _savePath);
            _transparent = EditorPrefs.GetBool(PrefsKeys.RenderTransparent, _transparent);
            _openAfter = EditorPrefs.GetBool(PrefsKeys.RenderOpenAfter, _openAfter);
            _watermark = EditorPrefs.GetBool(PrefsKeys.RenderWatermark, _watermark);
            _turnaroundEnabled = EditorPrefs.GetBool(PrefsKeys.RenderTurnaroundEnabled, _turnaroundEnabled);
            _turnaroundGap = Mathf.Clamp(EditorPrefs.GetInt(PrefsKeys.RenderTurnaroundGap, _turnaroundGap), MinTurnaroundGap, MaxTurnaroundGap);
            _turnaroundPaddingMultiplier = Mathf.Clamp(EditorPrefs.GetFloat(PrefsKeys.RenderTurnaroundPadding, _turnaroundPaddingMultiplier), MinTurnaroundPaddingMultiplier, MaxTurnaroundPaddingMultiplier);
            _turnaroundBackgroundTexture = ResolveTexture(EditorPrefs.GetString(PrefsKeys.RenderTurnaroundBackground, string.Empty)) ?? LoadDefaultTurnaroundBackground();

            var cameraId = EditorPrefs.GetString(PrefsKeys.RenderCameraId, string.Empty);
            var persistedCamera = ResolveCamera(cameraId);
            if (persistedCamera != null)
            {
                _camera = persistedCamera;
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefsKeys.RenderResWidth, _resWidth);
            EditorPrefs.SetInt(PrefsKeys.RenderResHeight, _resHeight);
            EditorPrefs.SetInt(PrefsKeys.RenderScale, _scale);
            EditorPrefs.SetString(PrefsKeys.RenderSavePath, _savePath ?? string.Empty);
            EditorPrefs.SetBool(PrefsKeys.RenderTransparent, _transparent);
            EditorPrefs.SetBool(PrefsKeys.RenderOpenAfter, _openAfter);
            EditorPrefs.SetBool(PrefsKeys.RenderWatermark, _watermark);
            EditorPrefs.SetBool(PrefsKeys.RenderTurnaroundEnabled, _turnaroundEnabled);
            EditorPrefs.SetString(PrefsKeys.RenderCameraId, SerializeCameraId(_camera));
            EditorPrefs.SetInt(PrefsKeys.RenderTurnaroundGap, _turnaroundGap);
            EditorPrefs.SetFloat(PrefsKeys.RenderTurnaroundPadding, _turnaroundPaddingMultiplier);
            EditorPrefs.SetString(PrefsKeys.RenderTurnaroundBackground, SerializeTexturePath(_turnaroundBackgroundTexture));
        }

        private static string SerializeCameraId(Camera camera)
        {
            if (camera == null)
            {
                return string.Empty;
            }

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(camera);
            return id.ToString();
        }

        private static Camera ResolveCamera(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (!GlobalObjectId.TryParse(id, out var globalId))
            {
                return null;
            }

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as Camera;
        }

        private static string SerializeTexturePath(Texture2D texture)
        {
            return texture != null ? AssetDatabase.GetAssetPath(texture) : string.Empty;
        }

        private static Texture2D ResolveTexture(string path)
        {
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D LoadDefaultTurnaroundBackground()
        {
            if (_defaultTurnaroundBackgroundTexture == null)
            {
                _defaultTurnaroundBackgroundTexture = Resources.Load<Texture2D>(DefaultTurnaroundBackgroundResourcePath);
            }

            return _defaultTurnaroundBackgroundTexture;
        }

        private Texture2D GetTurnaroundBackgroundTexture()
        {
            return _turnaroundBackgroundTexture != null ? _turnaroundBackgroundTexture : LoadDefaultTurnaroundBackground();
        }

        private void DrawCameraSection()
        {
            DrawInlineCalloutIfNeeded(StepIds.RendersCamera,
                "Click Use Main to pick the main camera.\n\nThis matches what you see in the scene.");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                Camera nextCamera = (Camera)EditorGUILayout.ObjectField("Select Camera", _camera, typeof(Camera), true);
                bool cameraChanged = EditorGUI.EndChangeCheck();
                _camera = nextCamera;
                if (cameraChanged)
                {
                    HandleCameraChanged();
                    if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.RendersCamera, StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        GuidedTourController.NotifyRendersCameraSelected();
                    }
                }

                if (GUILayout.Button("Use Main", GUILayout.Width(80f)))
                {
                    _camera = Camera.main;
                    HandleCameraChanged();
                    if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.RendersCamera, StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        GuidedTourController.NotifyRendersCameraSelected();
                    }
                }

                var useMainRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.renders.camera", useMainRect, "Use Main", onClick: () =>
                {
                    _camera = Camera.main;
                    HandleCameraChanged();
                    if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.RendersCamera, StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        GuidedTourController.NotifyRendersCameraSelected();
                    }
                });
            }

            DrawInlineCalloutIfNeeded(StepIds.RendersTransparent,
                "Toggle Transparent Background on for alpha. Off makes opaque renders.\n\nUse alpha for cutouts and compositing.");
            bool newTransparent = EditorGUILayout.Toggle("Transparent Background", _transparent);
            var transparentRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.renders.transparent", transparentRect, "Transparent");
            SetToggleAndNotifyTour(ref _transparent, newTransparent, StepIds.RendersTransparent, GuidedTourController.NotifyRendersTransparentEnabled);

            DrawInlineCalloutIfNeeded(StepIds.RendersSync,
                "Toggle Sync with Scene Camera on to follow the Scene view.\n\nThis mirrors Scene view framing.");
            bool newSync = EditorGUILayout.Toggle("Sync with Scene Camera", _syncSceneCamera);
            var syncRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.renders.sync", syncRect, "Sync");
            bool syncChanged = newSync != _syncSceneCamera;
            bool previousSync = _syncSceneCamera;
            SetToggleAndNotifyTour(ref _syncSceneCamera, newSync, StepIds.RendersSync, GuidedTourController.NotifyRendersSyncEnabled);
            if (syncChanged)
            {
                if (!previousSync && _syncSceneCamera)
                {
                    BeginSync();
                }
                else if (previousSync && !_syncSceneCamera)
                {
                    EndSync();
                }
            }

            if (_syncSceneCamera && _camera == null)
            {
                EditorGUILayout.HelpBox("Select a camera to sync with the Scene view.", MessageType.Info);
            }
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Resolution", EditorStyles.miniBoldLabel);
            _resWidth = EditorGUILayout.IntField("Width", Mathf.Max(1, _resWidth));
            _resHeight = EditorGUILayout.IntField("Height", Mathf.Max(1, _resHeight));
            _scale = EditorGUILayout.IntSlider("Scale", _scale, MinScale, MaxScale);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set To Screen Size", GUILayout.Height(26)))
                {
                    Vector2 size = Handles.GetMainGameViewSize();
                    _resWidth = Mathf.Max(1, Mathf.RoundToInt(size.x));
                    _resHeight = Mathf.Max(1, Mathf.RoundToInt(size.y));
                }
                if (GUILayout.Button("Default Size", GUILayout.Height(26)))
                {
                    _resWidth = DefaultWidth;
                    _resHeight = DefaultHeight;
                    _scale = 1;
                }
            }

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            _openAfter = EditorGUILayout.Toggle("Open Last File", _openAfter);
            DrawInlineCalloutIfNeeded(StepIds.RendersWatermark,
                "Toggle Watermark on to add the logo.\n\nUse this to match in-game branding.");
            bool newWatermark = EditorGUILayout.Toggle("Watermark", _watermark);
            var watermarkRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.renders.watermark", watermarkRect, "Watermark");
            SetToggleAndNotifyTour(ref _watermark, newWatermark, StepIds.RendersWatermark, GuidedTourController.NotifyRendersWatermarkEnabled);
            DrawTurnaroundControls();

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.LabelField("Save Path", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _savePath = EditorGUILayout.TextField(_savePath, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Save screenshots to", GetAbsoluteSavePath(), Application.dataPath);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _savePath = ScreenshotCapture.NormalizeSavePath(selected);
                    }
                }
            }
        }

        private void DrawTurnaroundControls()
        {
            _turnaroundEnabled = EditorGUILayout.Toggle("Turnaround", _turnaroundEnabled);
            if (!_turnaroundEnabled)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Uses the current resolution and always stamps the watermark.", EditorStyles.miniLabel);
            _turnaroundBackgroundTexture = (Texture2D)EditorGUILayout.ObjectField("Background Texture", GetTurnaroundBackgroundTexture(), typeof(Texture2D), false);
            _turnaroundGap = EditorGUILayout.IntSlider("Panel Gap", _turnaroundGap, MinTurnaroundGap, MaxTurnaroundGap);
            _turnaroundPaddingMultiplier = EditorGUILayout.Slider("Bounds Padding", _turnaroundPaddingMultiplier, MinTurnaroundPaddingMultiplier, MaxTurnaroundPaddingMultiplier);

            int outputWidth = GetFinalCaptureWidth();
            int outputHeight = GetFinalCaptureHeight();
            if (!TryBuildTurnaroundLayout(outputWidth, outputHeight, out _, out string outputValidationMessage) && !string.IsNullOrEmpty(outputValidationMessage))
            {
                EditorGUILayout.HelpBox(outputValidationMessage, MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawActionsSection(HoyoToonManager targetManager)
        {
            int finalWidth = GetFinalCaptureWidth();
            int finalHeight = GetFinalCaptureHeight();
            if (_turnaroundEnabled && TryBuildTurnaroundLayout(finalWidth, finalHeight, out TurnaroundLayout layout, out _))
            {
                EditorGUILayout.LabelField($"Turnaround output: {finalWidth} x {finalHeight} px  |  Panel: {layout.PanelWidth} x {layout.PanelHeight} px", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(_turnaroundEnabled
                    ? $"Turnaround output: {finalWidth} x {finalHeight} px"
                    : $"Screenshot output: {finalWidth} x {finalHeight} px", EditorStyles.miniLabel);
            }

            string prerequisiteMessage = GetCapturePrerequisiteMessage(targetManager, finalWidth, finalHeight);
            if (!string.IsNullOrEmpty(prerequisiteMessage))
            {
                EditorGUILayout.HelpBox(prerequisiteMessage, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(prerequisiteMessage)))
            {
                if (GUILayout.Button("Take Screenshot", GUILayout.MinHeight(42)))
                {
                    if (string.IsNullOrEmpty(_savePath))
                    {
                        _savePath = EditorUtility.OpenFolderPanel("Save screenshots to", _savePath, Application.dataPath);
                    }

                    if (!string.IsNullOrEmpty(_savePath))
                    {
                        TakeCapture(targetManager, finalWidth, finalHeight);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_lastScreenshot)))
                {
                    if (GUILayout.Button("Open Last Screenshot", GUILayout.MaxWidth(160), GUILayout.MinHeight(30)))
                    {
                        ScreenshotCapture.OpenFile(_lastScreenshot);
                    }
                }

                if (GUILayout.Button("Open Folder", GUILayout.MaxWidth(120), GUILayout.MinHeight(30)))
                {
                    ScreenshotCapture.OpenFolder(GetAbsoluteSavePath());
                }
            }
        }


        private void TakeCapture(HoyoToonManager targetManager, int width, int height)
        {
            if (_turnaroundEnabled)
            {
                TakeTurnaround(targetManager, width, height);
                return;
            }

            TakeScreenshot(width, height);
        }

        private void TakeScreenshot(int width, int height)
        {
            var absoluteSavePath = GetAbsoluteSavePath();
            _screenshotCapture.Capture(_camera, width, height, absoluteSavePath, _transparent, _watermark, _openAfter);
            _lastScreenshot = _screenshotCapture.LastScreenshot ?? _lastScreenshot;
        }

        private void TakeTurnaround(HoyoToonManager targetManager, int outputWidth, int outputHeight)
        {
            if (_camera == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Select a camera before creating a turnaround.");
                return;
            }

            var activeModel = targetManager != null ? targetManager.ActiveModel : null;
            if (activeModel == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Select an active model before creating a turnaround.");
                return;
            }

            if (!TryBuildTurnaroundLayout(outputWidth, outputHeight, out TurnaroundLayout layout, out string outputValidationMessage))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, outputValidationMessage);
                return;
            }

            if (!TryGetModelBounds(activeModel, out Bounds modelBounds))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "The active model does not have any renderers to frame.");
                return;
            }

            var backgroundTexture = GetTurnaroundBackgroundTexture();
            if (backgroundTexture == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Assign a background texture before creating a turnaround.");
                return;
            }

            var absoluteSavePath = GetAbsoluteSavePath();
            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Save path is invalid.");
                return;
            }

            CameraState originalState = CaptureState(_camera);
            Texture2D compositeTexture = null;
            var panelTextures = new List<Texture2D>(TurnaroundPanelCount);

            try
            {
                var views = BuildTurnaroundViews(activeModel.transform);
                float orthographicSize = CalculateTurnaroundOrthographicSize(modelBounds, views, layout.PanelWidth, layout.PanelHeight, _turnaroundPaddingMultiplier);
                var lightTargets = GetTurnaroundDirectionalLights(targetManager, activeModel);
                using var turnaroundScope = new TurnaroundCaptureScope(activeModel, lightTargets, TurnaroundSelfShadowLightFollow);
                foreach (var view in views)
                {
                    turnaroundScope.ApplyView(view);
                    PositionCameraForTurnaround(_camera, originalState, modelBounds, view, orthographicSize);
                    var panelTexture = _screenshotCapture.CaptureTexture(_camera, layout.PanelWidth, layout.PanelHeight, transparent: true);
                    if (panelTexture == null)
                    {
                        return;
                    }

                    panelTextures.Add(panelTexture);
                }

                compositeTexture = CreateTurnaroundComposite(panelTextures, backgroundTexture, layout);
                if (compositeTexture == null)
                {
                    return;
                }

                string screenshotPath = _screenshotCapture.SaveTexture(
                    compositeTexture,
                    absoluteSavePath,
                    watermark: true,
                    openAfter: _openAfter,
                    fileName: ScreenshotCapture.GenerateScreenshotName("turnaround"));
                _lastScreenshot = screenshotPath ?? _lastScreenshot;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
            }
            finally
            {
                RestoreState(_camera, originalState);

                if (compositeTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(compositeTexture);
                }

                for (int i = 0; i < panelTextures.Count; i++)
                {
                    if (panelTextures[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(panelTextures[i]);
                    }
                }
            }
        }

        private int GetFinalCaptureWidth()
        {
            return Mathf.Max(1, _resWidth) * Mathf.Max(1, _scale);
        }

        private int GetFinalCaptureHeight()
        {
            return Mathf.Max(1, _resHeight) * Mathf.Max(1, _scale);
        }

        private bool TryBuildTurnaroundLayout(int outputWidth, int outputHeight, out TurnaroundLayout layout, out string validationMessage)
        {
            layout = default;
            validationMessage = null;

            int maxTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);

            if (outputWidth > maxTextureSize || outputHeight > maxTextureSize)
            {
                validationMessage = $"Turnaround output must stay within the system texture limit of {maxTextureSize}px.";
                return false;
            }

            int outerPadding = Mathf.Max(MinTurnaroundOuterPadding, Mathf.RoundToInt(Mathf.Min(outputWidth, outputHeight) * TurnaroundOuterPaddingPercent));
            int availableWidth = outputWidth - (outerPadding * 2) - (_turnaroundGap * (TurnaroundPanelCount - 1));
            int availableHeight = outputHeight - (outerPadding * 2);
            if (availableWidth < TurnaroundPanelCount || availableHeight < 1)
            {
                validationMessage = "Turnaround layout does not fit in the selected resolution. Lower the panel gap or increase the screenshot size.";
                return false;
            }

            int panelWidth = availableWidth / TurnaroundPanelCount;
            if (panelWidth < 1)
            {
                validationMessage = "Turnaround panel width is too small. Lower the panel gap or increase the screenshot width.";
                return false;
            }

            int usedWidth = (panelWidth * TurnaroundPanelCount) + (_turnaroundGap * (TurnaroundPanelCount - 1));
            layout = new TurnaroundLayout
            {
                CompositeWidth = outputWidth,
                CompositeHeight = outputHeight,
                PanelWidth = panelWidth,
                PanelHeight = availableHeight,
                StartX = Mathf.Max(0, (outputWidth - usedWidth) / 2),
                StartY = outerPadding,
                Gap = _turnaroundGap
            };

            return true;
        }

        private string GetTurnaroundPrerequisiteMessage(HoyoToonManager targetManager, Texture2D backgroundTexture)
        {
            if (_camera == null)
            {
                return "Select a camera before creating a turnaround.";
            }

            if (targetManager == null || targetManager.ActiveModel == null)
            {
                return "Select an active model in the manager before creating a turnaround.";
            }

            if (backgroundTexture == null)
            {
                return "Assign a background texture. The packaged default background could not be loaded.";
            }

            return null;
        }

        private string GetCapturePrerequisiteMessage(HoyoToonManager targetManager, int outputWidth, int outputHeight)
        {
            if (!_turnaroundEnabled)
            {
                return _camera == null ? "Select a camera before taking a screenshot." : null;
            }

            if (!TryBuildTurnaroundLayout(outputWidth, outputHeight, out _, out string layoutValidationMessage))
            {
                return layoutValidationMessage;
            }

            return GetTurnaroundPrerequisiteMessage(targetManager, GetTurnaroundBackgroundTexture());
        }

        private static bool TryGetModelBounds(GameObject activeModel, out Bounds bounds)
        {
            bounds = default;
            if (activeModel == null)
            {
                return false;
            }

            var renderers = activeModel.GetComponentsInChildren<Renderer>(true);
            if (TryCollectBounds(renderers, requireVisibleRenderers: true, out bounds))
            {
                return true;
            }

            return TryCollectBounds(renderers, requireVisibleRenderers: false, out bounds);
        }

        private static HSRCharacterController ResolveCharacterController(GameObject activeModel)
        {
            if (activeModel == null)
            {
                return null;
            }

            return activeModel.GetComponentInChildren<HSRCharacterController>(true);
        }

        private static bool TryCollectBounds(Renderer[] renderers, bool requireVisibleRenderers, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (renderers == null)
            {
                return false;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (requireVisibleRenderers && (!renderer.enabled || !renderer.gameObject.activeInHierarchy))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static TurnaroundView[] BuildTurnaroundViews(Transform modelTransform)
        {
            Vector3 up = Vector3.up;
            Vector3 forward = modelTransform != null ? Vector3.ProjectOnPlane(modelTransform.forward, up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            return new[]
            {
                new TurnaroundView(forward, 180f),
                new TurnaroundView(-right, 90f),
                new TurnaroundView(right, -90f),
                new TurnaroundView(-forward, 0f)
            };
        }

        private static float CalculateTurnaroundOrthographicSize(Bounds bounds, TurnaroundView[] views, int panelWidth, int panelHeight, float paddingMultiplier)
        {
            float aspect = panelWidth / (float)panelHeight;
            float requiredSize = 0.01f;
            for (int i = 0; i < views.Length; i++)
            {
                Vector3 right = Vector3.Cross(Vector3.up, views[i].CameraOffsetDirection);
                if (right.sqrMagnitude < 0.0001f)
                {
                    right = Vector3.right;
                }
                else
                {
                    right.Normalize();
                }

                float horizontalExtent = CalculateProjectedExtent(bounds, right);
                float verticalExtent = CalculateProjectedExtent(bounds, Vector3.up);
                requiredSize = Mathf.Max(requiredSize, Mathf.Max(verticalExtent, horizontalExtent / aspect));
            }

            return requiredSize * Mathf.Max(MinTurnaroundPaddingMultiplier, paddingMultiplier) * TurnaroundAutoFitScale;
        }

        private static float CalculateProjectedExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 normalizedAxis = axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized;
            normalizedAxis = new Vector3(Mathf.Abs(normalizedAxis.x), Mathf.Abs(normalizedAxis.y), Mathf.Abs(normalizedAxis.z));
            return Vector3.Dot(bounds.extents, normalizedAxis);
        }

        private static void PositionCameraForTurnaround(Camera camera, CameraState originalState, Bounds bounds, TurnaroundView view, float orthographicSize)
        {
            Vector3 offsetDirection = view.CameraOffsetDirection.sqrMagnitude < 0.0001f ? Vector3.forward : view.CameraOffsetDirection.normalized;
            Vector3 cameraForward = -offsetDirection;
            float depthExtent = CalculateProjectedExtent(bounds, cameraForward);
            float distancePadding = Mathf.Max(bounds.extents.magnitude * 1.5f, 1f);
            float distance = depthExtent + distancePadding;

            camera.transform.SetPositionAndRotation(bounds.center + (offsetDirection * distance), Quaternion.LookRotation(cameraForward, Vector3.up));
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(originalState.FarClip, distance + depthExtent + distancePadding);
        }

        private static List<Light> GetTurnaroundDirectionalLights(HoyoToonManager targetManager, GameObject activeModel)
        {
            var lights = new List<Light>();
            var seen = new HashSet<int>();

            void AddLightIfValid(Light light)
            {
                if (light == null || light.type != LightType.Directional)
                {
                    return;
                }

                if (!seen.Add(light.GetInstanceID()))
                {
                    return;
                }

                lights.Add(light);
            }

            if (targetManager != null)
            {
                var lightsParent = targetManager.transform.Find("Lights");
                if (lightsParent != null)
                {
                    var managerLights = lightsParent.GetComponentsInChildren<Light>(true);
                    for (int i = 0; i < managerLights.Length; i++)
                    {
                        AddLightIfValid(managerLights[i]);
                    }
                }
            }

            AddLightIfValid(RenderSettings.sun);

            if (lights.Count == 0 && activeModel != null)
            {
                var sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < sceneLights.Length; i++)
                {
                    var light = sceneLights[i];
                    if (light == null || light.gameObject.scene != activeModel.scene)
                    {
                        continue;
                    }

                    AddLightIfValid(light);
                }
            }

            return lights;
        }

        private static Texture2D CreateTurnaroundComposite(IReadOnlyList<Texture2D> panels, Texture2D backgroundTexture, TurnaroundLayout layout)
        {
            Texture2D composite = ResizeTexture(backgroundTexture, layout.CompositeWidth, layout.CompositeHeight);
            if (composite == null)
            {
                composite = new Texture2D(layout.CompositeWidth, layout.CompositeHeight, TextureFormat.RGBA32, false);
                composite.SetPixels32(new Color32[layout.CompositeWidth * layout.CompositeHeight]);
                composite.Apply(false);
            }

            var compositePixels = composite.GetPixels32();
            for (int panelIndex = 0; panelIndex < panels.Count; panelIndex++)
            {
                var panel = panels[panelIndex];
                if (panel == null)
                {
                    continue;
                }

                int startX = layout.StartX + (panelIndex * (layout.PanelWidth + layout.Gap));
                AlphaBlendTexture(compositePixels, layout.CompositeWidth, layout.CompositeHeight, panel.GetPixels32(), panel.width, panel.height, startX, layout.StartY);
            }

            composite.SetPixels32(compositePixels);
            composite.Apply(false);
            return composite;
        }

        private static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            if (source == null)
            {
                return null;
            }

            RenderTexture renderTexture = null;
            RenderTexture previousActive = null;
            try
            {
                renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, renderTexture);
                previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;

                var resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resizedTexture.Apply(false);
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static void AlphaBlendTexture(Color32[] destinationPixels, int destinationWidth, int destinationHeight, Color32[] sourcePixels, int sourceWidth, int sourceHeight, int startX, int startY)
        {
            const float inv255 = 1f / 255f;

            for (int y = 0; y < sourceHeight; y++)
            {
                int destinationY = startY + y;
                if (destinationY < 0 || destinationY >= destinationHeight)
                {
                    continue;
                }

                int sourceRow = y * sourceWidth;
                int destinationRow = destinationY * destinationWidth;
                for (int x = 0; x < sourceWidth; x++)
                {
                    int destinationX = startX + x;
                    if (destinationX < 0 || destinationX >= destinationWidth)
                    {
                        continue;
                    }

                    Color32 source = sourcePixels[sourceRow + x];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    int destinationIndex = destinationRow + destinationX;
                    if (source.a == byte.MaxValue)
                    {
                        destinationPixels[destinationIndex] = source;
                        continue;
                    }

                    Color32 destination = destinationPixels[destinationIndex];
                    float alpha = source.a * inv255;
                    float oneMinusAlpha = 1f - alpha;
                    destinationPixels[destinationIndex] = new Color32(
                        (byte)(source.r * alpha + destination.r * oneMinusAlpha),
                        (byte)(source.g * alpha + destination.g * oneMinusAlpha),
                        (byte)(source.b * alpha + destination.b * oneMinusAlpha),
                        (byte)Mathf.Min(source.a + destination.a * oneMinusAlpha, 255f));
                }
            }
        }

        private static void SetToggleAndNotifyTour(ref bool currentValue, bool nextValue, string stepId, Action notify)
        {
            if (nextValue != currentValue)
            {
                currentValue = nextValue;
            }

            if (currentValue && string.Equals(GuidedTourController.CurrentStep.id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                notify?.Invoke();
            }
        }

        private string GetAbsoluteSavePath()
        {
            return ScreenshotCapture.GetAbsoluteSavePath(_savePath);
        }

        private void HandleCameraChanged()
        {
            if (!_syncSceneCamera)
            {
                return;
            }

            EndSync();
            BeginSync();
        }

        private void HandleEditorUpdate()
        {
            if (_disposed || !_syncSceneCamera || _camera == null)
            {
                return;
            }

            var sceneCam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            if (sceneCam == null)
            {
                return;
            }

            _camera.transform.SetPositionAndRotation(sceneCam.transform.position, sceneCam.transform.rotation);
            _camera.orthographic = sceneCam.orthographic;
            _camera.orthographicSize = sceneCam.orthographicSize;
            _camera.fieldOfView = sceneCam.fieldOfView;
            _camera.nearClipPlane = sceneCam.nearClipPlane;
            _camera.farClipPlane = sceneCam.farClipPlane;
        }

        private void BeginSync()
        {
            if (_camera == null)
            {
                return;
            }

            _syncedCamera = _camera;
            _syncedState = CaptureState(_camera);
            DisableCinemachineBrain(_camera);
        }

        private void EndSync()
        {
            EnableCinemachineBrain();

            if (_syncedCamera != null)
            {
                RestoreState(_syncedCamera, _syncedState);
            }

            _syncedCamera = null;
        }

        private static CameraState CaptureState(Camera camera)
        {
            return new CameraState
            {
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
                FieldOfView = camera.fieldOfView,
                NearClip = camera.nearClipPlane,
                FarClip = camera.farClipPlane,
                Orthographic = camera.orthographic,
                OrthoSize = camera.orthographicSize
            };
        }

        private static void RestoreState(Camera camera, CameraState state)
        {
            camera.transform.SetPositionAndRotation(state.Position, state.Rotation);
            camera.fieldOfView = state.FieldOfView;
            camera.nearClipPlane = state.NearClip;
            camera.farClipPlane = state.FarClip;
            camera.orthographic = state.Orthographic;
            camera.orthographicSize = state.OrthoSize;
        }

        private void DisableCinemachineBrain(Camera camera)
        {
            foreach (var component in camera.GetComponents<Behaviour>())
            {
                if (component != null && component.GetType().Name == "CinemachineBrain")
                {
                    component.enabled = false;
                    _disabledBrain = component;
                    return;
                }
            }
        }

        private void EnableCinemachineBrain()
        {
            if (_disabledBrain != null)
            {
                _disabledBrain.enabled = true;
                _disabledBrain = null;
            }
        }


        private void HandleBeforeAssemblyReload()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EndSync();
            EditorApplication.update -= HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        }
    }
}
#endif
