#if UNITY_EDITOR
using System;
using HoyoToon.Editor.Utilities;
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

        private int _resWidth = DefaultWidth;
        private int _resHeight = DefaultHeight;
        private int _scale = 1;
        private Camera _camera;
        private string _savePath = "Assets/HoyoToon/Renders";
        private bool _transparent;
        private bool _openAfter;
        private bool _watermark;
        private string _lastScreenshot;
        private bool _syncSceneCamera;
        private Camera _syncedCamera;
        private CameraState _syncedState;
        private bool _disposed;
        private bool _prefsLoaded;
        private Behaviour _disabledBrain;

        private readonly ScreenshotCapture _screenshotCapture = new ScreenshotCapture();

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
            DrawCameraSection();
            EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
            DrawResolutionSection();
            EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
            DrawSaveSection();
            EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
            DrawActionsSection();
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
            EditorPrefs.SetString(PrefsKeys.RenderCameraId, SerializeCameraId(_camera));
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


        private void DrawCameraSection()
        {
            DrawInlineCalloutIfNeeded(StepIds.RendersCamera,
                "Click Use Main to pick the main camera.\n\nThis matches what you see in the scene.");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _camera = (Camera)EditorGUILayout.ObjectField("Select Camera", _camera, typeof(Camera), true);
                bool cameraChanged = EditorGUI.EndChangeCheck();
                var cameraRect = GUILayoutUtility.GetLastRect();
                if (cameraChanged && string.Equals(GuidedTourController.CurrentStep.id, StepIds.RendersCamera, StringComparison.OrdinalIgnoreCase) && _camera != null)
                {
                    GuidedTourController.NotifyRendersCameraSelected();
                }
                if (GUILayout.Button("Use Main", GUILayout.Width(80f)))
                {
                    _camera = Camera.main;
                    if (string.Equals(GuidedTourController.CurrentStep.id, StepIds.RendersCamera, StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        GuidedTourController.NotifyRendersCameraSelected();
                    }
                }
                var useMainRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.renders.camera", useMainRect, "Use Main", onClick: () =>
                {
                    _camera = Camera.main;
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
            else if (_syncSceneCamera)
            {
                EditorGUILayout.HelpBox("Sync mirrors Scene view (Ctrl + Shift + F behavior).", MessageType.None);
            }
        }

        private void DrawResolutionSection()
        {
            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
            _resWidth = EditorGUILayout.IntField("Width", Mathf.Max(1, _resWidth));
            _resHeight = EditorGUILayout.IntField("Height", Mathf.Max(1, _resHeight));
            _scale = EditorGUILayout.IntSlider("Scale", _scale, MinScale, MaxScale);
            EditorGUILayout.HelpBox("Scale multiplies width/height without losing quality.", MessageType.None);

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
        }

        private void DrawSaveSection()
        {
            EditorGUILayout.LabelField("Save Path", EditorStyles.boldLabel);
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

            _openAfter = EditorGUILayout.Toggle("Open Last File", _openAfter);
            DrawInlineCalloutIfNeeded(StepIds.RendersWatermark,
                "Toggle Watermark on to add the logo.\n\nUse this to match in-game branding.");
            bool newWatermark = EditorGUILayout.Toggle("Watermark", _watermark);
            var watermarkRect = GUILayoutUtility.GetLastRect();
            TourOverlay.DrawHighlightIfActive("tour.renders.watermark", watermarkRect, "Watermark");
            SetToggleAndNotifyTour(ref _watermark, newWatermark, StepIds.RendersWatermark, GuidedTourController.NotifyRendersWatermarkEnabled);
            if (_watermark && ScreenshotCapture.GetWatermarkTexture() == null)
            {
                EditorGUILayout.HelpBox("Add a watermark texture at Resources/UI/hoyotoon.png to enable it.", MessageType.Info);
            }
        }

        private void DrawActionsSection()
        {
            int finalWidth = Mathf.Max(1, _resWidth) * Mathf.Max(1, _scale);
            int finalHeight = Mathf.Max(1, _resHeight) * Mathf.Max(1, _scale);
            EditorGUILayout.LabelField($"Screenshot will be taken at {finalWidth} x {finalHeight} px", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_camera == null))
            {
                if (GUILayout.Button("Take Screenshot", GUILayout.MinHeight(42)))
                {
                    if (string.IsNullOrEmpty(_savePath))
                    {
                        _savePath = EditorUtility.OpenFolderPanel("Save screenshots to", _savePath, Application.dataPath);
                    }

                    if (!string.IsNullOrEmpty(_savePath))
                    {
                        TakeScreenshot(finalWidth, finalHeight);
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


        private void TakeScreenshot(int width, int height)
        {
            var absoluteSavePath = GetAbsoluteSavePath();
            _screenshotCapture.Capture(_camera, width, height, absoluteSavePath, _transparent, _watermark, _openAfter);
            _lastScreenshot = _screenshotCapture.LastScreenshot ?? _lastScreenshot;
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
