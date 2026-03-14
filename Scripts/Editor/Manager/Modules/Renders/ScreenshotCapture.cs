#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class ScreenshotCapture
    {
        private const string WatermarkResourcePath = "UI/hoyotoon";
        private const float WatermarkWidthPercent = 0.22f;
        private const float WatermarkPaddingPercent = 0.02f;

        private static Shader _maskShader;
        private static Texture2D _watermarkTexture;
        private static Texture2D _watermarkReadable;
        private static bool _watermarkMissingLogged;

        public string LastScreenshot { get; private set; }

        public void Capture(Camera camera, int width, int height, string absoluteSavePath, bool transparent, bool watermark, bool openAfter)
        {
            if (camera == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Select a camera before taking a screenshot.");
                return;
            }

            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Save path is invalid.");
                return;
            }

            string fileName = GenerateScreenshotName(width, height);
            string path = Path.Combine(absoluteSavePath, fileName);

            RenderTexture rt = null;
            Texture2D screenshot = null;
            Texture2D blackCapture = null;
            Texture2D whiteCapture = null;
            GameObject captureCameraObject = null;
            Camera captureCamera = null;

            captureCameraObject = UnityEngine.Object.Instantiate(camera.gameObject);
            captureCameraObject.hideFlags = HideFlags.HideAndDontSave;
            captureCamera = captureCameraObject.GetComponent<Camera>();
            if (captureCamera == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Selected camera could not be cloned for screenshot capture.");
                UnityEngine.Object.DestroyImmediate(captureCameraObject);
                return;
            }

            captureCamera.enabled = false;
            DisableCinemachineBrain(captureCamera.gameObject);

            var postLayer = ResolvePostProcessingLayer(captureCamera);
            bool hasPostLayer = postLayer != null;
            bool originalPostEnabled = hasPostLayer && postLayer.enabled;

            try
            {
                if (!Directory.Exists(absoluteSavePath))
                {
                    Directory.CreateDirectory(absoluteSavePath);
                }

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

                if (transparent && hasPostLayer)
                {
                    postLayer.enabled = false;
                }

                if (transparent)
                {
                    blackCapture = CaptureTexture(captureCamera, rt, CameraClearFlags.Color, Color.black);
                    whiteCapture = CaptureTexture(captureCamera, rt, CameraClearFlags.Color, Color.white);
                    screenshot = ComposeTransparentScreenshot(blackCapture, whiteCapture);
                }
                else
                {
                    screenshot = CaptureTexture(captureCamera, rt, captureCamera.clearFlags, captureCamera.backgroundColor);
                }

                if (watermark)
                {
                    ApplyWatermark(screenshot);
                }

                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                LastScreenshot = path;

                if (openAfter)
                {
                    Application.OpenURL(new Uri(path).AbsoluteUri);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
            }
            finally
            {
                RenderTexture.active = null;
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
                if (blackCapture != null)
                {
                    UnityEngine.Object.DestroyImmediate(blackCapture);
                }
                if (whiteCapture != null)
                {
                    UnityEngine.Object.DestroyImmediate(whiteCapture);
                }
                if (captureCameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(captureCameraObject);
                }
            }
        }

        private static Texture2D CaptureTexture(Camera camera, RenderTexture renderTexture, CameraClearFlags clearFlags, Color backgroundColor)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousFlags = camera.clearFlags;
            var previousColor = camera.backgroundColor;

            try
            {
                camera.targetTexture = renderTexture;
                camera.clearFlags = clearFlags;
                camera.backgroundColor = backgroundColor;
                camera.Render();

                RenderTexture.active = renderTexture;
                var capture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
                capture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                capture.Apply(false);
                return capture;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.clearFlags = previousFlags;
                camera.backgroundColor = previousColor;
                RenderTexture.active = previousActive;
            }
        }

        private static Texture2D ComposeTransparentScreenshot(Texture2D blackCapture, Texture2D whiteCapture)
        {
            if (blackCapture == null)
            {
                return null;
            }

            if (whiteCapture == null)
            {
                var fallback = new Texture2D(blackCapture.width, blackCapture.height, TextureFormat.ARGB32, false);
                fallback.SetPixels32(blackCapture.GetPixels32());
                fallback.Apply(false);
                return fallback;
            }

            var blackPixels = blackCapture.GetPixels();
            var whitePixels = whiteCapture.GetPixels();
            var output = new Color[blackPixels.Length];

            for (int i = 0; i < output.Length; i++)
            {
                Color black = blackPixels[i];
                Color white = whitePixels[i];
                float alpha = 1f - Mathf.Max(white.r - black.r, Mathf.Max(white.g - black.g, white.b - black.b));
                alpha = Mathf.Clamp01(alpha);

                if (alpha <= 0.0001f)
                {
                    output[i] = Color.clear;
                    continue;
                }

                output[i] = new Color(
                    Mathf.Clamp01(black.r / alpha),
                    Mathf.Clamp01(black.g / alpha),
                    Mathf.Clamp01(black.b / alpha),
                    alpha);
            }

            var composed = new Texture2D(blackCapture.width, blackCapture.height, TextureFormat.ARGB32, false);
            composed.SetPixels(output);
            composed.Apply(false);
            return composed;
        }

        private static void DisableCinemachineBrain(GameObject cameraObject)
        {
            if (cameraObject == null)
            {
                return;
            }

            foreach (var component in cameraObject.GetComponents<Behaviour>())
            {
                if (component != null && component.GetType().Name == "CinemachineBrain")
                {
                    component.enabled = false;
                }
            }
        }

        private static Behaviour ResolvePostProcessingLayer(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            return camera.GetComponent("PostProcessLayer") as Behaviour;
        }


        public static void OpenFolder(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Save path is empty.");
                return;
            }

            if (!Directory.Exists(absolutePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Save path does not exist.");
                return;
            }

            Application.OpenURL(new Uri(absolutePath).AbsoluteUri);
        }

        public static void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "No screenshot has been taken yet.");
                return;
            }

            if (!File.Exists(filePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Last screenshot file does not exist.");
                return;
            }

            Application.OpenURL(new Uri(filePath).AbsoluteUri);
        }

        public static string GetAbsoluteSavePath(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(savePath))
            {
                return savePath;
            }

            if (savePath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return string.Empty;
                }

                var relative = savePath.Substring("Assets".Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(projectRoot, "Assets", relative);
            }

            return savePath;
        }

        public static string NormalizeSavePath(string absolutePath)
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


        private static string GenerateScreenshotName(int width, int height)
        {
            return $"screen_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        }

        private static void ApplyWatermark(Texture2D screenshot)
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

            float invScaleX = srcWidth / (float)targetWidth;
            float invScaleY = srcHeight / (float)targetHeight;

            var xMap = new int[targetWidth];
            for (int x = 0; x < targetWidth; x++)
            {
                xMap[x] = Mathf.Clamp((int)(x * invScaleX), 0, srcWidth - 1);
            }

            var yMap = new int[targetHeight];
            for (int y = 0; y < targetHeight; y++)
            {
                yMap[y] = Mathf.Clamp((int)(y * invScaleY), 0, srcHeight - 1) * srcWidth;
            }

            const float inv255 = 1f / 255f;

            for (int y = 0; y < targetHeight; y++)
            {
                int srcRow = yMap[y];
                int dstRow = (startY + y) * dstWidth + startX;

                for (int x = 0; x < targetWidth; x++)
                {
                    Color32 src = srcPixels[srcRow + xMap[x]];
                    if (src.a == 0)
                    {
                        continue;
                    }

                    int dstIndex = dstRow + x;
                    Color32 dst = dstPixels[dstIndex];
                    float a = src.a * inv255;
                    float oneMinusA = 1f - a;
                    dstPixels[dstIndex] = new Color32(
                        (byte)(src.r * a + dst.r * oneMinusA),
                        (byte)(src.g * a + dst.g * oneMinusA),
                        (byte)(src.b * a + dst.b * oneMinusA),
                        (byte)Mathf.Min(src.a + dst.a * oneMinusA, 255f));
                }
            }

            screenshot.SetPixels32(dstPixels);
            screenshot.Apply();
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

        internal static Texture2D GetWatermarkTexture()
        {
            if (_watermarkTexture != null)
            {
                return _watermarkTexture;
            }

            _watermarkTexture = Resources.Load<Texture2D>(WatermarkResourcePath);
            if (_watermarkTexture == null && !_watermarkMissingLogged)
            {
                _watermarkMissingLogged = true;
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "HoyoToon watermark texture not found. Place a PNG at Resources/UI/hoyotoon.png.");
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
    }
}
#endif
