#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class ScreenshotCapture
    {
        private const string WatermarkResourcePath = "UI/hoyotoon";
        private const float WatermarkWidthPercent = 0.22f;
        private const float WatermarkPaddingPercent = 0.02f;

        private static Texture2D _watermarkTexture;
        private static Texture2D _watermarkReadable;
        private static bool _watermarkMissingLogged;

        public string LastScreenshot { get; private set; }

        public void Capture(Camera sourceCamera, int width, int height, string absoluteSavePath, bool transparent = false, bool watermark = false, bool openAfter = false)
        {
            if (string.IsNullOrEmpty(absoluteSavePath))
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Save path is invalid.");
                return;
            }

            if (width < 1 || height < 1)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Screenshot resolution must be at least 1x1.");
                return;
            }

            if (sourceCamera == null)
            {
                HoyoToonLogger.Log("Renders", LogLevel.Warning, "Select a camera before taking a screenshot.");
                return;
            }

            string fileName = GenerateScreenshotName();
            string path = Path.Combine(absoluteSavePath, fileName);

            try
            {
                if (!Directory.Exists(absoluteSavePath))
                {
                    Directory.CreateDirectory(absoluteSavePath);
                }

                LastScreenshot = path;
                EditorFrameCoroutineRunner.Start(CaptureCameraAtEndOfFrame(sourceCamera, width, height, path, transparent, watermark, openAfter));
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
            }
        }

        private IEnumerator CaptureCameraAtEndOfFrame(Camera sourceCamera, int width, int height, string path, bool transparent, bool watermark, bool openAfter)
        {
            yield return new WaitForEndOfFrame();

            if (sourceCamera == null)
            {
                yield break;
            }

            Texture2D finalTexture = null;

            try
            {
                finalTexture = transparent
                    ? RenderCameraPassWithAlpha(sourceCamera, width, height)
                    : RenderCameraPassOpaque(sourceCamera, width, height);
                if (finalTexture == null)
                {
                    yield break;
                }

                if (watermark)
                {
                    ApplyWatermark(finalTexture);
                }

                File.WriteAllBytes(path, finalTexture.EncodeToPNG());

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

                if (finalTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(finalTexture);
                }
            }
        }

        private static Texture2D RenderCameraPassWithAlpha(Camera captureCamera, int width, int height)
        {
            return RenderCameraToTexture(captureCamera, width, height, CameraClearFlags.SolidColor, new Color(0f, 0f, 0f, 0f), true);
        }

        private static Texture2D RenderCameraPassOpaque(Camera captureCamera, int width, int height)
        {
            return RenderCameraToTexture(captureCamera, width, height, captureCamera.clearFlags, captureCamera.backgroundColor, false);
        }

        private static Texture2D RenderCameraToTexture(Camera captureCamera, int width, int height, CameraClearFlags clearFlags, Color backgroundColor, bool disablePostProcessing)
        {
            RenderTexture renderTexture = null;
            RenderTexture previousTarget = captureCamera.targetTexture;
            CameraClearFlags previousClearFlags = captureCamera.clearFlags;
            Color previousBackground = captureCamera.backgroundColor;
            float previousAspect = captureCamera.aspect;
            Component urpCameraData = captureCamera.GetComponent("UniversalAdditionalCameraData");
            System.Reflection.PropertyInfo renderPostProcessingProperty = null;
            bool previousRenderPostProcessing = false;

            try
            {
                if (disablePostProcessing && urpCameraData != null)
                {
                    renderPostProcessingProperty = urpCameraData.GetType().GetProperty("renderPostProcessing");
                    if (renderPostProcessingProperty != null && renderPostProcessingProperty.PropertyType == typeof(bool) && renderPostProcessingProperty.CanRead && renderPostProcessingProperty.CanWrite)
                    {
                        previousRenderPostProcessing = (bool)renderPostProcessingProperty.GetValue(urpCameraData, null);
                        renderPostProcessingProperty.SetValue(urpCameraData, false, null);
                    }
                }

                captureCamera.clearFlags = clearFlags;
                captureCamera.backgroundColor = backgroundColor;
                captureCamera.aspect = width / (float)height;

                renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                captureCamera.targetTexture = renderTexture;
                captureCamera.Render();

                return ReadTextureRgba(renderTexture, width, height);
            }
            finally
            {
                captureCamera.targetTexture = previousTarget;
                captureCamera.clearFlags = previousClearFlags;
                captureCamera.backgroundColor = previousBackground;
                captureCamera.aspect = previousAspect;

                if (disablePostProcessing && urpCameraData != null)
                {
                    if (renderPostProcessingProperty != null && renderPostProcessingProperty.CanWrite)
                    {
                        renderPostProcessingProperty.SetValue(urpCameraData, previousRenderPostProcessing, null);
                    }
                }

                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static Texture2D ReadTextureRgba(RenderTexture renderTexture, int width, int height)
        {
            var previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static class EditorFrameCoroutineRunner
        {
            private static readonly List<IEnumerator> ActiveRoutines = new List<IEnumerator>();
            private static bool _subscribed;

            public static void Start(IEnumerator routine)
            {
                if (routine == null)
                {
                    return;
                }

                ActiveRoutines.Add(routine);
                if (_subscribed)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                    return;
                }

                _subscribed = true;
                EditorApplication.update += Update;
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }

            private static void Update()
            {
                for (int i = ActiveRoutines.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (!Advance(ActiveRoutines[i]))
                        {
                            ActiveRoutines.RemoveAt(i);
                        }
                    }
                    catch (Exception ex)
                    {
                        ActiveRoutines.RemoveAt(i);
                        HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
                    }
                }

                if (ActiveRoutines.Count != 0)
                {
                    return;
                }

                EditorApplication.update -= Update;
                _subscribed = false;
            }

            private static bool Advance(IEnumerator routine)
            {
                if (!routine.MoveNext())
                {
                    return false;
                }

                if (routine.Current is WaitForEndOfFrame)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                }

                return true;
            }
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


        private static string GenerateScreenshotName()
        {
            return $"screen_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
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

        private static void ApplyWatermarkToFile(string path)
        {
            Texture2D screenshot = null;
            try
            {
                var pngBytes = File.ReadAllBytes(path);
                screenshot = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!screenshot.LoadImage(pngBytes, false))
                {
                    return;
                }

                ApplyWatermark(screenshot);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Always("Renders", ex.ToString(), LogType.Exception);
            }
            finally
            {
                if (screenshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(screenshot);
                }
            }
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
            screenshot.Apply(false);
        }
    }
}
#endif
