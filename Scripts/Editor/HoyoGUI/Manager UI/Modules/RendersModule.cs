#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using HoyoToon.Utilities;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal sealed class RendersModule : HoyoToonManagerModule
    {
        public override string DisplayName => "Renders";

        private const int DefaultWidth = 3840;
        private const int DefaultHeight = 2160;
        private const int MinScale = 1;
        private const int MaxScale = 8;
        private const string WatermarkResourcePath = "UI/hoyotoon";
        private const float WatermarkWidthPercent = 0.22f;
        private const float WatermarkPaddingPercent = 0.02f;
        private const string PrefKeyPrefix = "HoyoToon.Render.";
        private const string PrefKeyResWidth = PrefKeyPrefix + "ResWidth";
        private const string PrefKeyResHeight = PrefKeyPrefix + "ResHeight";
        private const string PrefKeyScale = PrefKeyPrefix + "Scale";
        private const string PrefKeySavePath = PrefKeyPrefix + "SavePath";
        private const string PrefKeyTransparent = PrefKeyPrefix + "Transparent";
        private const string PrefKeyOpenAfter = PrefKeyPrefix + "OpenAfter";
        private const string PrefKeyWatermark = PrefKeyPrefix + "Watermark";
        private const string PrefKeyCameraId = PrefKeyPrefix + "CameraId";

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
        private static Shader _maskShader;
        private static Texture2D _watermarkTexture;
        private static Texture2D _watermarkReadable;
        private static bool _watermarkMissingLogged;

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
            EditorGUILayout.Space(6f);
            DrawResolutionSection();
            EditorGUILayout.Space(6f);
            DrawSaveSection();
            EditorGUILayout.Space(6f);
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

            _resWidth = EditorPrefs.GetInt(PrefKeyResWidth, _resWidth);
            _resHeight = EditorPrefs.GetInt(PrefKeyResHeight, _resHeight);
            _scale = Mathf.Clamp(EditorPrefs.GetInt(PrefKeyScale, _scale), MinScale, MaxScale);
            _savePath = EditorPrefs.GetString(PrefKeySavePath, _savePath);
            _transparent = EditorPrefs.GetBool(PrefKeyTransparent, _transparent);
            _openAfter = EditorPrefs.GetBool(PrefKeyOpenAfter, _openAfter);
            _watermark = EditorPrefs.GetBool(PrefKeyWatermark, _watermark);

            var cameraId = EditorPrefs.GetString(PrefKeyCameraId, string.Empty);
            var persistedCamera = ResolveCamera(cameraId);
            if (persistedCamera != null)
            {
                _camera = persistedCamera;
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefKeyResWidth, _resWidth);
            EditorPrefs.SetInt(PrefKeyResHeight, _resHeight);
            EditorPrefs.SetInt(PrefKeyScale, _scale);
            EditorPrefs.SetString(PrefKeySavePath, _savePath ?? string.Empty);
            EditorPrefs.SetBool(PrefKeyTransparent, _transparent);
            EditorPrefs.SetBool(PrefKeyOpenAfter, _openAfter);
            EditorPrefs.SetBool(PrefKeyWatermark, _watermark);
            EditorPrefs.SetString(PrefKeyCameraId, SerializeCameraId(_camera));
        }

#if UNITY_2019_2_OR_NEWER
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
#else
        private static string SerializeCameraId(Camera camera)
        {
            return string.Empty;
        }

        private static Camera ResolveCamera(string id)
        {
            return null;
        }
#endif

        private void DrawCameraSection()
        {
            DrawInlineCalloutIfNeeded("renders_camera",
                "Click Use Main to pick the main camera.\n\nThis matches what you see in the scene.");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _camera = (Camera)EditorGUILayout.ObjectField("Select Camera", _camera, typeof(Camera), true);
                bool cameraChanged = EditorGUI.EndChangeCheck();
                var cameraRect = GUILayoutUtility.GetLastRect();
                if (cameraChanged && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_camera", StringComparison.OrdinalIgnoreCase) && _camera != null)
                {
                    HoyoToonGuidedTourController.NotifyRendersCameraSelected();
                }
                if (GUILayout.Button("Use Main", GUILayout.Width(80f)))
                {
                    _camera = Camera.main;
                    if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_camera", StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        HoyoToonGuidedTourController.NotifyRendersCameraSelected();
                    }
                }
                var useMainRect = GUILayoutUtility.GetLastRect();
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.renders.camera", useMainRect, "Use Main", onClick: () =>
                {
                    _camera = Camera.main;
                    if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_camera", StringComparison.OrdinalIgnoreCase) && _camera != null)
                    {
                        HoyoToonGuidedTourController.NotifyRendersCameraSelected();
                    }
                });
            }

            DrawInlineCalloutIfNeeded("renders_transparent",
                "Toggle Transparent Background on for alpha. Off makes opaque renders.\n\nUse alpha for cutouts and compositing.");
            bool newTransparent = EditorGUILayout.Toggle("Transparent Background", _transparent);
            var transparentRect = GUILayoutUtility.GetLastRect();
            HoyoToonTourOverlay.DrawHighlightIfActive("tour.renders.transparent", transparentRect, "Transparent");
            if (newTransparent != _transparent)
            {
                _transparent = newTransparent;
                if (_transparent && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_transparent", StringComparison.OrdinalIgnoreCase))
                {
                    HoyoToonGuidedTourController.NotifyRendersTransparentEnabled();
                }
            }
            else if (_transparent && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_transparent", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonGuidedTourController.NotifyRendersTransparentEnabled();
            }

            DrawInlineCalloutIfNeeded("renders_sync",
                "Toggle Sync with Scene Camera on to follow the Scene view.\n\nThis mirrors Scene view framing.");
            bool newSync = EditorGUILayout.Toggle("Sync with Scene Camera", _syncSceneCamera);
            var syncRect = GUILayoutUtility.GetLastRect();
            HoyoToonTourOverlay.DrawHighlightIfActive("tour.renders.sync", syncRect, "Sync");
            if (newSync != _syncSceneCamera)
            {
                _syncSceneCamera = newSync;
                if (_syncSceneCamera)
                {
                    BeginSync();
                    if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_sync", StringComparison.OrdinalIgnoreCase))
                    {
                        HoyoToonGuidedTourController.NotifyRendersSyncEnabled();
                    }
                }
                else
                {
                    EndSync();
                }
            }
            else if (_syncSceneCamera && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_sync", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonGuidedTourController.NotifyRendersSyncEnabled();
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
                        _savePath = NormalizeSavePath(selected);
                    }
                }
            }

            _openAfter = EditorGUILayout.Toggle("Open Last File", _openAfter);
            DrawInlineCalloutIfNeeded("renders_watermark",
                "Toggle Watermark on to add the logo.\n\nUse this to match in-game branding.");
            bool newWatermark = EditorGUILayout.Toggle("Watermark", _watermark);
            var watermarkRect = GUILayoutUtility.GetLastRect();
            HoyoToonTourOverlay.DrawHighlightIfActive("tour.renders.watermark", watermarkRect, "Watermark");
            if (newWatermark != _watermark)
            {
                _watermark = newWatermark;
                if (_watermark && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_watermark", StringComparison.OrdinalIgnoreCase))
                {
                    HoyoToonGuidedTourController.NotifyRendersWatermarkEnabled();
                }
            }
            else if (_watermark && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "renders_watermark", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonGuidedTourController.NotifyRendersWatermarkEnabled();
            }
            if (_watermark && GetWatermarkTexture() == null)
            {
                EditorGUILayout.HelpBox("Add a watermark texture at Resources/UI/hoyotoon.png to enable it.", MessageType.Info);
            }
        }

        private static void DrawInlineCalloutIfNeeded(string stepId, string body)
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            if (!string.Equals(HoyoToonGuidedTourController.CurrentStep.id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HoyoToonTourCallout.Draw(
                $"Guided Tour: {HoyoToonGuidedTourController.CurrentStep.title}",
                body,
                null,
                null);
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
                        OpenLastScreenshot();
                    }
                }

                if (GUILayout.Button("Open Folder", GUILayout.MaxWidth(120), GUILayout.MinHeight(30)))
                {
                    OpenSaveFolder();
                }
            }
        }

        private void TakeScreenshot(int width, int height)
        {
            if (_camera == null)
            {
                HoyoToonLogCore.WarnCategory("Renders", "Select a camera before taking a screenshot.");
                return;
            }

            if (string.IsNullOrEmpty(_savePath))
            {
                HoyoToonLogCore.WarnCategory("Renders", "Select a save path before taking a screenshot.");
                return;
            }

            var absoluteSavePath = GetAbsoluteSavePath();
            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                HoyoToonLogCore.WarnCategory("Renders", "Save path is invalid.");
                return;
            }

            string fileName = GenerateScreenshotName(width, height);
            string path = Path.Combine(absoluteSavePath, fileName);

            RenderTexture rt = null;
            RenderTexture previous = null;
            Texture2D screenshot = null;
            Texture2D alphaMask = null;

            var postLayer = _camera.GetComponent<PostProcessLayer>();
            bool hasPostLayer = postLayer != null;

            var originalFlags = _camera.clearFlags;
            var originalColor = _camera.backgroundColor;
            bool originalPostEnabled = hasPostLayer && postLayer.enabled;

            try
            {
                if (!Directory.Exists(absoluteSavePath))
                {
                    Directory.CreateDirectory(absoluteSavePath);
                }

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                previous = _camera.targetTexture;

                if (_transparent)
                {
                    _camera.clearFlags = CameraClearFlags.Color;
                    _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                }

                if (_transparent && hasPostLayer)
                {
                    postLayer.enabled = false;
                }

                if (_transparent)
                {
                    var maskShader = GetMaskShader();
                    if (maskShader != null)
                    {
                        _camera.targetTexture = rt;
                        _camera.RenderWithShader(maskShader, string.Empty);

                        RenderTexture.active = rt;
                        alphaMask = new Texture2D(width, height, TextureFormat.ARGB32, false);
                        alphaMask.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        alphaMask.Apply(false);
                    }
                }

                if (_transparent && hasPostLayer)
                {
                    postLayer.enabled = true;
                }

                _camera.targetTexture = rt;

                var maskRenderer = UnityEngine.Object.FindObjectOfType<HoyoToon.EditorTools.ManagerScene.HoyoToonHairShadowMaskRenderer>(true);
                if (maskRenderer != null)
                {
                    maskRenderer.RenderForCamera(_camera, width, height);
                }

                _camera.Render();

                RenderTexture.active = rt;
                var format = _transparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
                screenshot = new Texture2D(width, height, format, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply(false);

                if (_transparent)
                {
                    SetAlphaChannel(screenshot, alphaMask);
                }

                if (_watermark)
                {
                    ApplyWatermark(screenshot);
                }

                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                _lastScreenshot = path;

                if (_openAfter)
                {
                    Application.OpenURL("file:///" + path);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
            }
            finally
            {
                _camera.targetTexture = previous;
                RenderTexture.active = null;
                _camera.clearFlags = originalFlags;
                _camera.backgroundColor = originalColor;
                if (hasPostLayer)
                {
                    postLayer.enabled = originalPostEnabled;
                }

                if (rt != null)
                {
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (screenshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }
                if (alphaMask != null)
                {
                    UnityEngine.Object.DestroyImmediate(alphaMask);
                }
            }
        }

        private void SetAlphaChannel(Texture2D screenshot, Texture2D mask)
        {
            var pixels = screenshot.GetPixels32();
            var maskPixels = mask != null ? mask.GetPixels32() : null;
            int maskCount = maskPixels != null ? maskPixels.Length : 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte baseAlpha = pixels[i].a > 0 ? (byte)255 : (byte)0;
                if (maskPixels != null && i < maskCount)
                {
                    int combined = baseAlpha + maskPixels[i].a;
                    pixels[i].a = (byte)Mathf.Clamp(combined, 0, 255);
                }
                else
                {
                    pixels[i].a = baseAlpha;
                }
            }
            screenshot.SetPixels32(pixels);
            screenshot.Apply();
        }

        private void ApplyAlphaMask(Texture2D screenshot, Texture2D mask)
        {
            var pixels = screenshot.GetPixels32();
            var maskPixels = mask.GetPixels32();
            int count = Mathf.Min(pixels.Length, maskPixels.Length);
            for (int i = 0; i < count; i++)
            {
                int combined = pixels[i].a + maskPixels[i].a;
                pixels[i].a = (byte)Mathf.Clamp(combined, 0, 255);
            }
            screenshot.SetPixels32(pixels);
            screenshot.Apply();
        }

        private void ApplyWatermark(Texture2D screenshot)
        {
            var watermark = GetReadableWatermarkTexture();
            if (watermark == null)
            {
                return;
            }

            int dstWidth = screenshot.width;
            int dstHeight = screenshot.height;
            int targetWidth = Mathf.Clamp(Mathf.RoundToInt(dstWidth * WatermarkWidthPercent), 32, dstWidth);
            int targetHeight = Mathf.RoundToInt(targetWidth * (watermark.height / (float)watermark.width));
            if (targetHeight > dstHeight)
            {
                targetHeight = dstHeight;
                targetWidth = Mathf.RoundToInt(targetHeight * (watermark.width / (float)watermark.height));
            }

            int padding = Mathf.RoundToInt(Mathf.Min(dstWidth, dstHeight) * WatermarkPaddingPercent);
            int startX = Mathf.Max(0, dstWidth - targetWidth - padding);
            int startY = Mathf.Max(0, padding);

            var dstPixels = screenshot.GetPixels32();
            var srcPixels = watermark.GetPixels32();
            int srcWidth = watermark.width;
            int srcHeight = watermark.height;

            for (int y = 0; y < targetHeight; y++)
            {
                int srcY = Mathf.Clamp(Mathf.FloorToInt(y * (srcHeight / (float)targetHeight)), 0, srcHeight - 1);
                int dstRow = (startY + y) * dstWidth;
                int srcRow = srcY * srcWidth;

                for (int x = 0; x < targetWidth; x++)
                {
                    int srcX = Mathf.Clamp(Mathf.FloorToInt(x * (srcWidth / (float)targetWidth)), 0, srcWidth - 1);
                    Color32 src = srcPixels[srcRow + srcX];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    int dstIndex = dstRow + startX + x;
                    Color32 dst = dstPixels[dstIndex];
                    float a = src.a / 255f;
                    byte r = (byte)(src.r * a + dst.r * (1f - a));
                    byte g = (byte)(src.g * a + dst.g * (1f - a));
                    byte b = (byte)(src.b * a + dst.b * (1f - a));
                    byte outA = (byte)Mathf.Clamp(Mathf.RoundToInt(src.a + dst.a * (1f - a)), 0, 255);
                    dstPixels[dstIndex] = new Color32(r, g, b, outA);
                }
            }

            screenshot.SetPixels32(dstPixels);
            screenshot.Apply();
        }

        private void OpenSaveFolder()
        {
            var absolutePath = GetAbsoluteSavePath();
            if (string.IsNullOrEmpty(absolutePath))
            {
                HoyoToonLogCore.WarnCategory("Renders", "Save path is empty.");
                return;
            }

            if (!Directory.Exists(absolutePath))
            {
                HoyoToonLogCore.WarnCategory("Renders", "Save path does not exist.");
                return;
            }

            Application.OpenURL("file:///" + absolutePath);
        }

        private void OpenLastScreenshot()
        {
            if (string.IsNullOrEmpty(_lastScreenshot))
            {
                HoyoToonLogCore.WarnCategory("Renders", "No screenshot has been taken yet.");
                return;
            }

            if (!File.Exists(_lastScreenshot))
            {
                HoyoToonLogCore.WarnCategory("Renders", "Last screenshot file does not exist.");
                return;
            }

            Application.OpenURL("file:///" + _lastScreenshot);
        }

        private string GenerateScreenshotName(int width, int height)
        {
            return $"screen_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        }

        private string GetAbsoluteSavePath()
        {
            if (string.IsNullOrWhiteSpace(_savePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(_savePath))
            {
                return _savePath;
            }

            if (_savePath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return string.Empty;
                }

                var relative = _savePath.Substring("Assets".Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(projectRoot, "Assets", relative);
            }

            return _savePath;
        }

        private static string NormalizeSavePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                return absolutePath;
            }

            var normalizedProject = projectRoot.Replace('\\', '/').TrimEnd('/');
            var normalizedAbsolute = absolutePath.Replace('\\', '/');
            if (normalizedAbsolute.StartsWith(normalizedProject + "/Assets", StringComparison.OrdinalIgnoreCase))
            {
                var relative = normalizedAbsolute.Substring(normalizedProject.Length + 1);
                return relative;
            }

            return absolutePath;
        }

        private static Shader GetMaskShader()
        {
            if (_maskShader != null)
            {
                return _maskShader;
            }

            _maskShader = Shader.Find("Hidden/HoyoToon/ScreenshotMask");
            return _maskShader;
        }

        private static Texture2D GetWatermarkTexture()
        {
            if (_watermarkTexture != null)
            {
                return _watermarkTexture;
            }

            _watermarkTexture = Resources.Load<Texture2D>(WatermarkResourcePath);
            if (_watermarkTexture == null && !_watermarkMissingLogged)
            {
                _watermarkMissingLogged = true;
                HoyoToonLogCore.WarnCategory("Renders", "HoyoToon watermark texture not found. Place a PNG at Resources/UI/hoyotoon.png.");
            }

            return _watermarkTexture;
        }

        private static Texture2D GetReadableWatermarkTexture()
        {
            var watermark = GetWatermarkTexture();
            if (watermark == null)
            {
                return null;
            }

            if (watermark.isReadable)
            {
                return watermark;
            }

            if (_watermarkReadable != null && _watermarkReadable.width == watermark.width && _watermarkReadable.height == watermark.height)
            {
                return _watermarkReadable;
            }

            RenderTexture rt = null;
            RenderTexture previous = null;
            try
            {
                rt = RenderTexture.GetTemporary(watermark.width, watermark.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(watermark, rt);
                previous = RenderTexture.active;
                RenderTexture.active = rt;

                _watermarkReadable = new Texture2D(watermark.width, watermark.height, TextureFormat.ARGB32, false);
                _watermarkReadable.ReadPixels(new Rect(0, 0, watermark.width, watermark.height), 0, 0);
                _watermarkReadable.Apply(false);
                return _watermarkReadable;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
            }
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
        }

        private void EndSync()
        {
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
