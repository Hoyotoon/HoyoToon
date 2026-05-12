#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Utilities.Renders;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Scene.Placement;
using UnityEditor;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;
using CameraState = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.CameraState;
using TurnaroundCaptureScope = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundCaptureScope;
using TurnaroundLayout = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundLayout;
using TurnaroundView = HoyoToon.Editor.Utilities.Renders.TurnaroundCaptureUtility.TurnaroundView;

namespace HoyoToon.Editor.Renders
{
    internal sealed class EditorScreenshotToolWindow : EditorWindow
    {
        private const string WindowTitle = "Screenshot Tool";
        private const string PrefsPrefix = "HoyoToon.Editor.ScreenshotTool.";
        private const string DefaultSavePath = "Assets/HoyoToon/Renders";
        private const string DefaultTurnaroundBackgroundResourcePath = "UI/1x1";
        private const int DefaultWidth = 3840;
        private const int DefaultHeight = 2160;
        private const int MinScale = 1;
        private const int MaxScale = 8;
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
        private const string ModelKey = PrefsPrefix + "Model";
        private const string WatermarkTextureKey = PrefsPrefix + "WatermarkTexture";
        private const string TurnaroundBackgroundKey = PrefsPrefix + "TurnaroundBackground";
        private const string TurnaroundGapKey = PrefsPrefix + "TurnaroundGap";
        private const string TurnaroundPaddingKey = PrefsPrefix + "TurnaroundPadding";
        private const string SyncWithSceneViewKey = PrefsPrefix + "SyncWithSceneView";

        private static Texture2D s_DefaultTurnaroundBackground;

        private readonly EditorScreenshotCaptureService m_CaptureService = new EditorScreenshotCaptureService();

        private Vector2 m_ScrollPosition;
        private int m_Width = DefaultWidth;
        private int m_Height = DefaultHeight;
        private int m_Scale = MinScale;
        private Camera m_Camera;
        private GameObject m_ModelOverride;
        private string m_SavePath = DefaultSavePath;
        private bool m_TransparentBackground;
        private bool m_OpenAfterCapture;
        private bool m_EnableWatermark;
        private Texture2D m_WatermarkTexture;
        private Texture2D m_TurnaroundBackgroundTexture;
        private int m_TurnaroundGap = DefaultTurnaroundGap;
        private float m_TurnaroundPaddingMultiplier = DefaultTurnaroundPaddingMultiplier;
        private string m_LastCapturePath;
        private bool m_SyncWithSceneView;
        private Camera m_SyncedCamera;
        private CameraState m_SyncedCameraState;
        private readonly CinemachineBrainStateStore m_CinemachineBrainStates = new CinemachineBrainStateStore();

        internal static void OpenWindow()
        {
            EditorScreenshotToolWindow window = GetWindow<EditorScreenshotToolWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 540f);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void RegisterGeneratedDefaultTurnaroundBackgroundCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseGeneratedDefaultTurnaroundBackground;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseGeneratedDefaultTurnaroundBackground;
            EditorApplication.quitting -= ReleaseGeneratedDefaultTurnaroundBackground;
            EditorApplication.quitting += ReleaseGeneratedDefaultTurnaroundBackground;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(420f, 540f);
            LoadPrefs();

            if (m_Camera == null)
            {
                m_Camera = Camera.main;
            }

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            if (m_SyncWithSceneView)
            {
                BeginSceneViewSync();
            }
        }

        private void OnDisable()
        {
            SavePrefs();
            EndSceneViewSync();
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private void OnBeforeAssemblyReload()
        {
            SavePrefs();
            EndSceneViewSync();
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private void OnGUI()
        {
            ClampSettings();

            using (var scrollView = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;

                EditorGUILayout.HelpBox(
                    "Standalone screenshot and turnaround capture for the current scene.",
                    MessageType.None);

                EditorGUI.BeginChangeCheck();
                DrawSection("Camera", DrawCameraSection);
                DrawSection("Output", DrawOutputSection);
                DrawSection("Turnaround", DrawTurnaroundSection);
                DrawSection("Capture", DrawCaptureSection);
                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                }
            }
        }

        private void DrawCameraSection()
        {
            EditorGUI.BeginChangeCheck();
            Camera nextCamera = (Camera)EditorGUILayout.ObjectField("Capture Camera", m_Camera, typeof(Camera), true);
            if (EditorGUI.EndChangeCheck())
            {
                SetCaptureCamera(nextCamera);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Main"))
                {
                    SetCaptureCamera(Camera.main);
                }

                using (new EditorGUI.DisabledScope(m_Camera == null || GetSceneViewCamera() == null))
                {
                    if (GUILayout.Button("Align To Scene"))
                    {
                        CopySceneViewStateToCaptureCamera();
                    }
                }
            }

            bool nextSync = EditorGUILayout.Toggle("Sync With Scene View", m_SyncWithSceneView);
            if (nextSync != m_SyncWithSceneView)
            {
                m_SyncWithSceneView = nextSync;
                if (m_SyncWithSceneView)
                {
                    BeginSceneViewSync();
                }
                else
                {
                    EndSceneViewSync();
                }
            }

            if (m_Camera == null)
            {
                EditorGUILayout.HelpBox("Select a camera before taking a screenshot.", MessageType.Info);
            }
            else if (m_SyncWithSceneView && GetSceneViewCamera() == null)
            {
                EditorGUILayout.HelpBox("Open a Scene view to sync the selected camera.", MessageType.Info);
            }
        }

        private void DrawOutputSection()
        {
            m_Width = Mathf.Max(1, EditorGUILayout.IntField("Width", m_Width));
            m_Height = Mathf.Max(1, EditorGUILayout.IntField("Height", m_Height));
            m_Scale = EditorGUILayout.IntSlider("Scale", m_Scale, MinScale, MaxScale);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Game View Size"))
                {
                    Vector2 gameViewSize = Handles.GetMainGameViewSize();
                    m_Width = Mathf.Max(1, Mathf.RoundToInt(gameViewSize.x));
                    m_Height = Mathf.Max(1, Mathf.RoundToInt(gameViewSize.y));
                }

                if (GUILayout.Button("Reset 4K"))
                {
                    m_Width = DefaultWidth;
                    m_Height = DefaultHeight;
                    m_Scale = MinScale;
                }
            }

            EditorGUILayout.Space(2f);

            m_TransparentBackground = EditorGUILayout.Toggle("Transparent Background", m_TransparentBackground);
            m_OpenAfterCapture = EditorGUILayout.Toggle("Open After Capture", m_OpenAfterCapture);
            m_EnableWatermark = EditorGUILayout.Toggle("Watermark", m_EnableWatermark);
            if (m_EnableWatermark)
            {
                m_WatermarkTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Watermark Texture",
                    m_WatermarkTexture,
                    typeof(Texture2D),
                    false);

                if (ResolveWatermarkTexture() == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a watermark texture, or add Resources/UI/hoyotoon.png if you want a default watermark.",
                        MessageType.Info);
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Save Path", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                m_SavePath = EditorGUILayout.TextField(m_SavePath);
                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel(
                        "Save screenshots to",
                        GetAbsoluteSavePath(),
                        Application.dataPath);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        m_SavePath = EditorScreenshotCaptureService.NormalizeSavePath(selectedPath);
                    }
                }
            }

            string absoluteSavePath = GetAbsoluteSavePath();
            if (!string.IsNullOrEmpty(absoluteSavePath) && !string.Equals(absoluteSavePath, m_SavePath, StringComparison.Ordinal))
            {
                EditorGUILayout.LabelField(absoluteSavePath, EditorStyles.miniLabel);
            }
        }

        private void DrawTurnaroundSection()
        {
            m_ModelOverride = (GameObject)EditorGUILayout.ObjectField("Model Override", m_ModelOverride, typeof(GameObject), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Placement Active"))
                {
                    GameObject activeModel = FindPlacementActiveModel();
                    if (activeModel != null)
                    {
                        m_ModelOverride = activeModel;
                    }
                    else
                    {
                        HoyoToonLogger.Warning(HoyoToonLogCategory.General, "No active model could be resolved from the placement controller.");
                    }
                }

                if (GUILayout.Button("Use Selection"))
                {
                    GameObject selectedModel = NormalizeModelRoot(Selection.activeGameObject);
                    if (selectedModel != null)
                    {
                        m_ModelOverride = selectedModel;
                    }
                    else
                    {
                        HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Select a scene object to use it as the turnaround model.");
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(72f)))
                {
                    m_ModelOverride = null;
                }
            }

            GameObject resolvedModel = ResolveTargetModel();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Resolved Model", resolvedModel, typeof(GameObject), true);
            }

            m_TurnaroundBackgroundTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Background Texture",
                m_TurnaroundBackgroundTexture,
                typeof(Texture2D),
                false);
            if (m_TurnaroundBackgroundTexture == null)
            {
                EditorGUILayout.LabelField("Using Resources/UI/1x1.png as the default background.", EditorStyles.miniLabel);
            }

            m_TurnaroundGap = EditorGUILayout.IntSlider("Panel Gap", m_TurnaroundGap, MinTurnaroundGap, MaxTurnaroundGap);
            m_TurnaroundPaddingMultiplier = EditorGUILayout.Slider(
                "Bounds Padding",
                m_TurnaroundPaddingMultiplier,
                MinTurnaroundPaddingMultiplier,
                MaxTurnaroundPaddingMultiplier);

            int finalWidth = GetFinalCaptureWidth();
            int finalHeight = GetFinalCaptureHeight();
            TurnaroundLayout layout;
            string layoutValidationMessage;
            if (TryBuildTurnaroundLayout(finalWidth, finalHeight, out layout, out layoutValidationMessage))
            {
                EditorGUILayout.LabelField(
                    string.Format(
                        "Composite: {0} x {1}px  |  Panel: {2} x {3}px",
                        finalWidth,
                        finalHeight,
                        layout.PanelWidth,
                        layout.PanelHeight),
                    EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(layoutValidationMessage))
            {
                EditorGUILayout.HelpBox(layoutValidationMessage, MessageType.Warning);
            }
        }

        private void DrawCaptureSection()
        {
            int finalWidth = GetFinalCaptureWidth();
            int finalHeight = GetFinalCaptureHeight();
            GameObject resolvedModel = ResolveTargetModel();

            EditorGUILayout.LabelField(
                string.Format("Screenshot Output: {0} x {1}px", finalWidth, finalHeight),
                EditorStyles.miniBoldLabel);

            string screenshotPrerequisiteMessage = GetScreenshotPrerequisiteMessage();
            if (!string.IsNullOrEmpty(screenshotPrerequisiteMessage))
            {
                EditorGUILayout.HelpBox(screenshotPrerequisiteMessage, MessageType.Info);
            }

            string turnaroundPrerequisiteMessage = GetTurnaroundPrerequisiteMessage(resolvedModel, finalWidth, finalHeight);
            if (!string.IsNullOrEmpty(turnaroundPrerequisiteMessage))
            {
                EditorGUILayout.HelpBox(turnaroundPrerequisiteMessage, MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(screenshotPrerequisiteMessage)))
                {
                    if (GUILayout.Button("Take Screenshot", GUILayout.MinHeight(38f)))
                    {
                        TakeScreenshot(finalWidth, finalHeight);
                    }
                }

                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(turnaroundPrerequisiteMessage)))
                {
                    if (GUILayout.Button("Take Turnaround", GUILayout.MinHeight(38f)))
                    {
                        TakeTurnaround(resolvedModel, finalWidth, finalHeight);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(m_LastCapturePath)))
                {
                    if (GUILayout.Button("Open Last Capture"))
                    {
                        EditorScreenshotCaptureService.OpenFile(m_LastCapturePath);
                    }
                }

                if (GUILayout.Button("Open Folder"))
                {
                    EditorScreenshotCaptureService.OpenFolder(GetAbsoluteSavePath());
                }
            }

            if (!string.IsNullOrEmpty(m_LastCapturePath))
            {
                EditorGUILayout.LabelField(m_LastCapturePath, EditorStyles.miniLabel);
            }
        }

        private void TakeScreenshot(int width, int height)
        {
            string absoluteSavePath = EnsureSavePath();
            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                return;
            }

            string savedPath;
            bool succeeded = m_CaptureService.TryCaptureToFile(
                m_Camera,
                width,
                height,
                absoluteSavePath,
                m_TransparentBackground,
                ResolveWatermarkTexture(),
                m_OpenAfterCapture,
                "screen",
                out savedPath);

            if (!succeeded)
            {
                return;
            }

            m_LastCapturePath = savedPath;
            ShowNotification(new GUIContent("Screenshot saved."));
            SavePrefs();
        }

        private void TakeTurnaround(GameObject activeModel, int outputWidth, int outputHeight)
        {
            if (activeModel == null)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Select a model before creating a turnaround.");
                return;
            }

            string absoluteSavePath = EnsureSavePath();
            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                return;
            }

            TurnaroundLayout layout;
            string validationMessage;
            if (!TryBuildTurnaroundLayout(outputWidth, outputHeight, out layout, out validationMessage))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, validationMessage);
                return;
            }

            Bounds modelBounds;
            if (!TurnaroundCaptureUtility.TryGetModelBounds(activeModel, out modelBounds))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "The selected model does not have any renderers to frame.");
                return;
            }

            CameraState originalCameraState = TurnaroundCaptureUtility.CaptureState(m_Camera);
            Texture2D compositeTexture = null;
            var panelTextures = new List<Texture2D>(TurnaroundPanelCount);

            try
            {
                TurnaroundView[] views = TurnaroundCaptureUtility.BuildViews(activeModel.transform);
                float orthographicSize = TurnaroundCaptureUtility.CalculateOrthographicSize(
                    modelBounds,
                    views,
                    layout.PanelWidth,
                    layout.PanelHeight,
                    m_TurnaroundPaddingMultiplier,
                    MinTurnaroundPaddingMultiplier,
                    TurnaroundAutoFitScale);
                List<Light> directionalLights = TurnaroundCaptureUtility.GetDirectionalLights(activeModel);

                using (var turnaroundScope = new TurnaroundCaptureScope(
                           activeModel,
                           directionalLights,
                           TurnaroundSelfShadowLightFollow))
                {
                    for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
                    {
                        turnaroundScope.ApplyView(views[viewIndex]);
                        TurnaroundCaptureUtility.PositionCamera(
                            m_Camera,
                            originalCameraState,
                            modelBounds,
                            views[viewIndex],
                            orthographicSize);

                        Texture2D panelTexture = m_CaptureService.CaptureTexture(
                            m_Camera,
                            layout.PanelWidth,
                            layout.PanelHeight,
                            transparent: true);
                        if (panelTexture == null)
                        {
                            return;
                        }

                        panelTextures.Add(panelTexture);
                    }
                }

                compositeTexture = TurnaroundCaptureUtility.CreateComposite(
                    panelTextures,
                    ResolveTurnaroundBackgroundTexture(),
                    layout);
                if (compositeTexture == null)
                {
                    return;
                }

                string savedPath = m_CaptureService.SaveTexture(
                    compositeTexture,
                    absoluteSavePath,
                    ResolveWatermarkTexture(),
                    m_OpenAfterCapture,
                    EditorScreenshotCaptureService.GenerateFileName("turnaround"));
                if (string.IsNullOrEmpty(savedPath))
                {
                    return;
                }

                m_LastCapturePath = savedPath;
                ShowNotification(new GUIContent("Turnaround saved."));
                SavePrefs();
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.General, "Turnaround capture failed.", exception);
            }
            finally
            {
                TurnaroundCaptureUtility.RestoreState(m_Camera, originalCameraState);

                if (compositeTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(compositeTexture);
                }

                for (int index = 0; index < panelTextures.Count; index++)
                {
                    if (panelTextures[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(panelTextures[index]);
                    }
                }
            }
        }

        private void SetCaptureCamera(Camera camera)
        {
            if (m_Camera == camera)
            {
                return;
            }

            if (m_SyncWithSceneView)
            {
                EndSceneViewSync();
            }

            m_Camera = camera;

            if (m_SyncWithSceneView)
            {
                BeginSceneViewSync();
            }
        }

        private void OnEditorUpdate()
        {
            if (!m_SyncWithSceneView || m_Camera == null)
            {
                return;
            }

            Camera sceneViewCamera = GetSceneViewCamera();
            if (sceneViewCamera == null)
            {
                return;
            }

            TurnaroundCaptureUtility.ApplyCameraState(m_Camera, TurnaroundCaptureUtility.CaptureState(sceneViewCamera));
        }

        private void BeginSceneViewSync()
        {
            if (m_Camera == null)
            {
                return;
            }

            if (m_SyncedCamera == m_Camera)
            {
                m_CinemachineBrainStates.DisableForSync(m_Camera);
                CopySceneViewStateToCaptureCamera();
                return;
            }

            EndSceneViewSync();
            m_SyncedCamera = m_Camera;
            m_SyncedCameraState = TurnaroundCaptureUtility.CaptureState(m_Camera);
            m_CinemachineBrainStates.DisableForSync(m_Camera);
            CopySceneViewStateToCaptureCamera();
        }

        private void EndSceneViewSync()
        {
            m_CinemachineBrainStates.Restore();

            if (m_SyncedCamera != null)
            {
                TurnaroundCaptureUtility.RestoreState(m_SyncedCamera, m_SyncedCameraState);
            }

            m_SyncedCamera = null;
        }

        private void CopySceneViewStateToCaptureCamera()
        {
            Camera sceneViewCamera = GetSceneViewCamera();
            if (m_Camera == null || sceneViewCamera == null)
            {
                return;
            }

            TurnaroundCaptureUtility.ApplyCameraState(m_Camera, TurnaroundCaptureUtility.CaptureState(sceneViewCamera));
        }

        private static Camera GetSceneViewCamera()
        {
            return SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : null;
        }

        private string GetScreenshotPrerequisiteMessage()
        {
            return m_Camera == null
                ? "Select a camera before taking a screenshot."
                : null;
        }

        private string GetTurnaroundPrerequisiteMessage(GameObject activeModel, int outputWidth, int outputHeight)
        {
            if (m_Camera == null)
            {
                return "Select a camera before creating a turnaround.";
            }

            if (activeModel == null)
            {
                return "Select a model or use the placement controller's active model before creating a turnaround.";
            }

            string layoutValidationMessage;
            TurnaroundLayout layout;
            if (!TryBuildTurnaroundLayout(outputWidth, outputHeight, out layout, out layoutValidationMessage))
            {
                return layoutValidationMessage;
            }

            Bounds bounds;
            if (!TurnaroundCaptureUtility.TryGetModelBounds(activeModel, out bounds))
            {
                return "The resolved model does not have any renderers to frame.";
            }

            return null;
        }

        private GameObject ResolveTargetModel()
        {
            GameObject overrideModel = NormalizeModelRoot(m_ModelOverride);
            if (overrideModel != null)
            {
                return overrideModel;
            }

            GameObject placementModel = FindPlacementActiveModel();
            if (placementModel != null)
            {
                return placementModel;
            }

            return NormalizeModelRoot(Selection.activeGameObject);
        }

        private GameObject FindPlacementActiveModel()
        {
            UnityScene contextScene = GetContextScene();
            if (contextScene.IsValid())
            {
                GameObject sceneModel = CharacterPlacementController.FindActiveModel(contextScene);
                if (sceneModel != null)
                {
                    return NormalizeModelRoot(sceneModel);
                }
            }

            CharacterPlacementController controller = CharacterPlacementController.GetPrimaryCachedOrFind();
            return controller != null ? NormalizeModelRoot(controller.ActiveModel) : null;
        }

        private UnityScene GetContextScene()
        {
            if (m_ModelOverride != null)
            {
                return m_ModelOverride.scene;
            }

            if (m_Camera != null)
            {
                return m_Camera.gameObject.scene;
            }

            if (Selection.activeGameObject != null)
            {
                return Selection.activeGameObject.scene;
            }

            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        private static GameObject NormalizeModelRoot(GameObject candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            HSRCharacterController controller = candidate.GetComponent<HSRCharacterController>();
            if (controller == null)
            {
                controller = candidate.GetComponentInParent<HSRCharacterController>();
            }

            if (controller == null)
            {
                controller = candidate.GetComponentInChildren<HSRCharacterController>(true);
            }

            return controller != null ? controller.gameObject : candidate;
        }

        private Texture2D ResolveWatermarkTexture()
        {
            if (!m_EnableWatermark)
            {
                return null;
            }

            return m_WatermarkTexture != null
                ? m_WatermarkTexture
                : EditorScreenshotCaptureService.TryLoadDefaultWatermarkTexture();
        }

        private Texture2D ResolveTurnaroundBackgroundTexture()
        {
            return m_TurnaroundBackgroundTexture != null
                ? m_TurnaroundBackgroundTexture
                : GetDefaultTurnaroundBackground();
        }

        private static Texture2D GetDefaultTurnaroundBackground()
        {
            if (s_DefaultTurnaroundBackground != null)
            {
                return s_DefaultTurnaroundBackground;
            }

            Texture2D resourceTexture = UnityEngine.Resources.Load<Texture2D>(DefaultTurnaroundBackgroundResourcePath);
            if (resourceTexture != null)
            {
                s_DefaultTurnaroundBackground = resourceTexture;
                return s_DefaultTurnaroundBackground;
            }

            const int Size = 128;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "HoyoToon Generated Turnaround Background",
                hideFlags = HideFlags.HideAndDontSave
            };

            Color top = new Color(0.94f, 0.95f, 0.97f, 1f);
            Color bottom = new Color(0.84f, 0.87f, 0.91f, 1f);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float verticalT = y / (float)(Size - 1);
                for (int x = 0; x < Size; x++)
                {
                    float diagonalT = (x + y) / (float)((Size * 2) - 2);
                    Color baseColor = Color.Lerp(bottom, top, verticalT);
                    Color finalColor = Color.Lerp(baseColor, Color.white, diagonalT * 0.08f);
                    pixels[(y * Size) + x] = finalColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            s_DefaultTurnaroundBackground = texture;
            return s_DefaultTurnaroundBackground;
        }

        private static void ReleaseGeneratedDefaultTurnaroundBackground()
        {
            if (s_DefaultTurnaroundBackground == null)
            {
                return;
            }

            if ((s_DefaultTurnaroundBackground.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                UnityEngine.Object.DestroyImmediate(s_DefaultTurnaroundBackground);
            }

            s_DefaultTurnaroundBackground = null;
        }

        private string EnsureSavePath()
        {
            string absoluteSavePath = GetAbsoluteSavePath();
            if (!string.IsNullOrEmpty(absoluteSavePath))
            {
                return absoluteSavePath;
            }

            string selectedPath = EditorUtility.OpenFolderPanel(
                "Save screenshots to",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return null;
            }

            m_SavePath = EditorScreenshotCaptureService.NormalizeSavePath(selectedPath);
            SavePrefs();
            return GetAbsoluteSavePath();
        }

        private string GetAbsoluteSavePath()
        {
            return EditorScreenshotCaptureService.GetAbsoluteSavePath(m_SavePath);
        }

        private int GetFinalCaptureWidth()
        {
            return Mathf.Max(1, m_Width) * Mathf.Max(1, m_Scale);
        }

        private int GetFinalCaptureHeight()
        {
            return Mathf.Max(1, m_Height) * Mathf.Max(1, m_Scale);
        }

        private void ClampSettings()
        {
            m_Width = Mathf.Max(1, m_Width);
            m_Height = Mathf.Max(1, m_Height);
            m_Scale = Mathf.Clamp(m_Scale, MinScale, MaxScale);
            m_TurnaroundGap = Mathf.Clamp(m_TurnaroundGap, MinTurnaroundGap, MaxTurnaroundGap);
            m_TurnaroundPaddingMultiplier = Mathf.Clamp(
                m_TurnaroundPaddingMultiplier,
                MinTurnaroundPaddingMultiplier,
                MaxTurnaroundPaddingMultiplier);
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
                m_TurnaroundGap,
                TurnaroundOuterPaddingPercent,
                MinTurnaroundOuterPadding,
                out layout,
                out validationMessage,
                "Turnaround output must stay within the system texture limit of {0}px.",
                "Turnaround layout does not fit in the selected resolution. Lower the panel gap or increase the output size.",
                "Turnaround panel width is too small. Lower the panel gap or increase the output width.");
        }

        private void LoadPrefs()
        {
            m_Width = EditorPrefs.GetInt(PrefsKey(WidthKey), m_Width);
            m_Height = EditorPrefs.GetInt(PrefsKey(HeightKey), m_Height);
            m_Scale = Mathf.Clamp(EditorPrefs.GetInt(PrefsKey(ScaleKey), m_Scale), MinScale, MaxScale);
            m_SavePath = EditorPrefs.GetString(PrefsKey(SavePathKey), m_SavePath);
            m_TransparentBackground = EditorPrefs.GetBool(PrefsKey(TransparentKey), m_TransparentBackground);
            m_OpenAfterCapture = EditorPrefs.GetBool(PrefsKey(OpenAfterKey), m_OpenAfterCapture);
            m_EnableWatermark = EditorPrefs.GetBool(PrefsKey(WatermarkEnabledKey), m_EnableWatermark);
            string syncPrefsKey = PrefsKey(SyncWithSceneViewKey);
            m_SyncWithSceneView = SessionState.GetBool(
                syncPrefsKey,
                EditorPrefs.GetBool(syncPrefsKey, m_SyncWithSceneView));
            m_TurnaroundGap = Mathf.Clamp(EditorPrefs.GetInt(PrefsKey(TurnaroundGapKey), m_TurnaroundGap), MinTurnaroundGap, MaxTurnaroundGap);
            m_TurnaroundPaddingMultiplier = Mathf.Clamp(
                EditorPrefs.GetFloat(PrefsKey(TurnaroundPaddingKey), m_TurnaroundPaddingMultiplier),
                MinTurnaroundPaddingMultiplier,
                MaxTurnaroundPaddingMultiplier);

            m_Camera = ResolveObject<Camera>(EditorPrefs.GetString(PrefsKey(CameraKey), string.Empty));
            m_ModelOverride = ResolveObject<GameObject>(EditorPrefs.GetString(PrefsKey(ModelKey), string.Empty));
            m_WatermarkTexture = ResolveObject<Texture2D>(EditorPrefs.GetString(PrefsKey(WatermarkTextureKey), string.Empty));
            string turnaroundBackgroundId = EditorPrefs.GetString(PrefsKey(TurnaroundBackgroundKey), string.Empty);
            m_TurnaroundBackgroundTexture = !string.IsNullOrWhiteSpace(turnaroundBackgroundId)
                ? ResolveObject<Texture2D>(turnaroundBackgroundId)
                : GetDefaultTurnaroundBackground();
            if (m_TurnaroundBackgroundTexture == null)
            {
                m_TurnaroundBackgroundTexture = GetDefaultTurnaroundBackground();
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefsKey(WidthKey), m_Width);
            EditorPrefs.SetInt(PrefsKey(HeightKey), m_Height);
            EditorPrefs.SetInt(PrefsKey(ScaleKey), m_Scale);
            EditorPrefs.SetString(PrefsKey(SavePathKey), m_SavePath ?? string.Empty);
            EditorPrefs.SetBool(PrefsKey(TransparentKey), m_TransparentBackground);
            EditorPrefs.SetBool(PrefsKey(OpenAfterKey), m_OpenAfterCapture);
            EditorPrefs.SetBool(PrefsKey(WatermarkEnabledKey), m_EnableWatermark);
            string syncPrefsKey = PrefsKey(SyncWithSceneViewKey);
            EditorPrefs.SetBool(syncPrefsKey, m_SyncWithSceneView);
            SessionState.SetBool(syncPrefsKey, m_SyncWithSceneView);
            EditorPrefs.SetString(PrefsKey(CameraKey), SerializeObject(m_Camera));
            EditorPrefs.SetString(PrefsKey(ModelKey), SerializeObject(m_ModelOverride));
            EditorPrefs.SetString(PrefsKey(WatermarkTextureKey), SerializeObject(m_WatermarkTexture));
            EditorPrefs.SetString(PrefsKey(TurnaroundBackgroundKey), SerializeObject(m_TurnaroundBackgroundTexture));
            EditorPrefs.SetInt(PrefsKey(TurnaroundGapKey), m_TurnaroundGap);
            EditorPrefs.SetFloat(PrefsKey(TurnaroundPaddingKey), m_TurnaroundPaddingMultiplier);
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
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

        private static void DrawSection(string title, Action drawContents)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            drawContents();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }
}
#endif
