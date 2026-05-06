#if UNITY_EDITOR
using System;
using System.IO;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Renders
{
    internal sealed class EditorScreenshotCaptureService
    {
        private const string DefaultWatermarkResourcePath = "UI/hoyotoon";
        private const float WatermarkWidthPercent = 0.22f;
        private const float WatermarkPaddingPercent = 0.02f;

        public string LastCapturePath { get; private set; }

        public bool TryCaptureToFile(
            Camera sourceCamera,
            int width,
            int height,
            string absoluteSavePath,
            bool transparent,
            Texture2D watermarkTexture,
            bool openAfter,
            string fileNamePrefix,
            out string savedPath)
        {
            savedPath = null;

            Texture2D capture = CaptureTexture(sourceCamera, width, height, transparent);
            if (capture == null)
            {
                return false;
            }

            try
            {
                savedPath = SaveTexture(
                    capture,
                    absoluteSavePath,
                    watermarkTexture,
                    openAfter,
                    GenerateFileName(fileNamePrefix));
                return !string.IsNullOrEmpty(savedPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(capture);
            }
        }

        public Texture2D CaptureTexture(Camera sourceCamera, int width, int height, bool transparent)
        {
            if (!ValidateCaptureRequest(sourceCamera, width, height))
            {
                return null;
            }

            try
            {
                return transparent
                    ? RenderCameraPassWithAlpha(sourceCamera, width, height)
                    : RenderCameraPassOpaque(sourceCamera, width, height);
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.General, "Screenshot capture failed.", exception);
                return null;
            }
        }

        public string SaveTexture(
            Texture2D texture,
            string absoluteSavePath,
            Texture2D watermarkTexture,
            bool openAfter,
            string fileName)
        {
            if (texture == null)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "No texture was provided to save.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(absoluteSavePath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Save path is invalid.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = GenerateFileName();
            }
            else if (!string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".png";
            }

            string destinationPath = Path.Combine(absoluteSavePath, fileName);

            try
            {
                Directory.CreateDirectory(absoluteSavePath);

                if (watermarkTexture != null)
                {
                    ApplyWatermark(texture, watermarkTexture);
                }

                File.WriteAllBytes(destinationPath, texture.EncodeToPNG());
                LastCapturePath = destinationPath;

                if (openAfter)
                {
                    Application.OpenURL(new Uri(destinationPath).AbsoluteUri);
                }

                return destinationPath;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.General, "Saving the screenshot failed.", exception);
                return null;
            }
        }

        public static Texture2D TryLoadDefaultWatermarkTexture()
        {
            return UnityEngine.Resources.Load<Texture2D>(DefaultWatermarkResourcePath);
        }

        public static string GenerateFileName(string prefix = "screen")
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "screen";
            }

            return string.Format("{0}_{1:yyyy-MM-dd_HH-mm-ss}.png", prefix, DateTime.Now);
        }

        public static void OpenFolder(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Save path is empty.");
                return;
            }

            if (!Directory.Exists(absolutePath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Save path does not exist.");
                return;
            }

            Application.OpenURL(new Uri(absolutePath).AbsoluteUri);
        }

        public static void OpenFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "No screenshot has been captured yet.");
                return;
            }

            if (!File.Exists(filePath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "The last screenshot file could not be found.");
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
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return string.Empty;
                }

                string relativePath = savePath.Substring("Assets".Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(projectRoot, "Assets", relativePath);
            }

            return savePath;
        }

        public static string NormalizeSavePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            return EditorPathUtility.TryAssetPathFromAbsolute(absolutePath, out string assetPath)
                && assetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)
                    ? assetPath
                    : absolutePath;
        }

        private static bool ValidateCaptureRequest(Camera sourceCamera, int width, int height)
        {
            if (width < 1 || height < 1)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Screenshot resolution must be at least 1x1.");
                return false;
            }

            if (sourceCamera == null)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Select a camera before taking a screenshot.");
                return false;
            }

            return true;
        }

        private static Texture2D RenderCameraPassWithAlpha(Camera captureCamera, int width, int height)
        {
            return RenderCameraToTexture(
                captureCamera,
                width,
                height,
                CameraClearFlags.SolidColor,
                new Color(0f, 0f, 0f, 0f),
                disablePostProcessing: true);
        }

        private static Texture2D RenderCameraPassOpaque(Camera captureCamera, int width, int height)
        {
            return RenderCameraToTexture(
                captureCamera,
                width,
                height,
                captureCamera.clearFlags,
                captureCamera.backgroundColor,
                disablePostProcessing: false);
        }

        private static Texture2D RenderCameraToTexture(
            Camera captureCamera,
            int width,
            int height,
            CameraClearFlags clearFlags,
            Color backgroundColor,
            bool disablePostProcessing)
        {
            RenderTexture renderTexture = null;
            RenderTexture previousTarget = captureCamera.targetTexture;
            CameraClearFlags previousClearFlags = captureCamera.clearFlags;
            Color previousBackground = captureCamera.backgroundColor;
            float previousAspect = captureCamera.aspect;
            Component additionalCameraData = captureCamera.GetComponent("UniversalAdditionalCameraData");
            var renderPostProcessingProperty = additionalCameraData != null
                ? additionalCameraData.GetType().GetProperty("renderPostProcessing")
                : null;
            bool previousRenderPostProcessing = false;

            try
            {
                if (disablePostProcessing
                    && renderPostProcessingProperty != null
                    && renderPostProcessingProperty.PropertyType == typeof(bool)
                    && renderPostProcessingProperty.CanRead
                    && renderPostProcessingProperty.CanWrite)
                {
                    previousRenderPostProcessing = (bool)renderPostProcessingProperty.GetValue(additionalCameraData, null);
                    renderPostProcessingProperty.SetValue(additionalCameraData, false, null);
                }

                captureCamera.clearFlags = clearFlags;
                captureCamera.backgroundColor = backgroundColor;
                captureCamera.aspect = width / (float)height;

                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                captureCamera.targetTexture = renderTexture;
                captureCamera.Render();

                return ReadTexture(renderTexture, width, height);
            }
            finally
            {
                captureCamera.targetTexture = previousTarget;
                captureCamera.clearFlags = previousClearFlags;
                captureCamera.backgroundColor = previousBackground;
                captureCamera.aspect = previousAspect;

                if (disablePostProcessing
                    && renderPostProcessingProperty != null
                    && renderPostProcessingProperty.CanWrite)
                {
                    renderPostProcessingProperty.SetValue(additionalCameraData, previousRenderPostProcessing, null);
                }

                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static Texture2D ReadTexture(RenderTexture renderTexture, int width, int height)
        {
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static void ApplyWatermark(Texture2D screenshot, Texture2D watermarkTexture)
        {
            Texture2D readableWatermark = GetReadableTexture(watermarkTexture);
            if (readableWatermark == null)
            {
                return;
            }

            try
            {
                int destinationWidth = screenshot.width;
                int destinationHeight = screenshot.height;
                int targetWidth = Mathf.Clamp(Mathf.RoundToInt(destinationWidth * WatermarkWidthPercent), 32, destinationWidth);
                int targetHeight = Mathf.RoundToInt(targetWidth * (readableWatermark.height / (float)readableWatermark.width));
                if (targetHeight > destinationHeight)
                {
                    targetHeight = destinationHeight;
                    targetWidth = Mathf.RoundToInt(targetHeight * (readableWatermark.width / (float)readableWatermark.height));
                }

                int padding = Mathf.RoundToInt(Mathf.Min(destinationWidth, destinationHeight) * WatermarkPaddingPercent);
                int startX = Mathf.Max(0, destinationWidth - targetWidth - padding);
                int startY = Mathf.Max(0, padding);

                Color32[] destinationPixels = screenshot.GetPixels32();
                Color32[] sourcePixels = readableWatermark.GetPixels32();
                int sourceWidth = readableWatermark.width;
                int sourceHeight = readableWatermark.height;

                float inverseScaleX = sourceWidth / (float)targetWidth;
                float inverseScaleY = sourceHeight / (float)targetHeight;

                var xMap = new int[targetWidth];
                for (int x = 0; x < targetWidth; x++)
                {
                    xMap[x] = Mathf.Clamp((int)(x * inverseScaleX), 0, sourceWidth - 1);
                }

                var yMap = new int[targetHeight];
                for (int y = 0; y < targetHeight; y++)
                {
                    yMap[y] = Mathf.Clamp((int)(y * inverseScaleY), 0, sourceHeight - 1) * sourceWidth;
                }

                const float InverseByte = 1f / 255f;

                for (int y = 0; y < targetHeight; y++)
                {
                    int sourceRow = yMap[y];
                    int destinationRow = (startY + y) * destinationWidth + startX;

                    for (int x = 0; x < targetWidth; x++)
                    {
                        Color32 source = sourcePixels[sourceRow + xMap[x]];
                        if (source.a == 0)
                        {
                            continue;
                        }

                        int destinationIndex = destinationRow + x;
                        Color32 destination = destinationPixels[destinationIndex];
                        float alpha = source.a * InverseByte;
                        float inverseAlpha = 1f - alpha;
                        destinationPixels[destinationIndex] = new Color32(
                            (byte)(source.r * alpha + destination.r * inverseAlpha),
                            (byte)(source.g * alpha + destination.g * inverseAlpha),
                            (byte)(source.b * alpha + destination.b * inverseAlpha),
                            (byte)Mathf.Min(source.a + destination.a * inverseAlpha, 255f));
                    }
                }

                screenshot.SetPixels32(destinationPixels);
                screenshot.Apply(false);
            }
            finally
            {
                if (readableWatermark != watermarkTexture)
                {
                    UnityEngine.Object.DestroyImmediate(readableWatermark);
                }
            }
        }

        private static Texture2D GetReadableTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture.isReadable)
            {
                return texture;
            }

            RenderTexture renderTexture = null;
            RenderTexture previousActive = null;

            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, renderTexture);
                previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;

                var readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readableTexture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                readableTexture.Apply(false);
                return readableTexture;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.General, "Preparing the watermark texture failed.", exception);
                return null;
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
    }
}
#endif
