#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;

namespace HoyoToon
{
    public class ScreenshotEditorWindow : EditorWindow
    {
        private readonly string fileName = "Screenshot";
        private string folderPath = "Screenshots";
        private int resolutionMultiplier = 1;
        private bool includeTimestamp = true;
        private int initialWidth = 1920;
        private int initialHeight = 1080;
        private enum ViewType { SceneView, GameView }
        private RenderTexture renderTexture;
        private ViewType selectedView = ViewType.GameView;
        private bool transparency = false;
        private bool openScreenshotOnSave = false;
        private Camera sceneCamera;


        [MenuItem("HoyoToon/Editor Screenshot", false, 30)]
        public static void ShowWindow()
        {
            GetWindow<ScreenshotEditorWindow>("Screenshot");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyImmediate(renderTexture);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            sceneCamera = sceneView.camera;
            Repaint();
        }

        private void OnGUI()
        {

            Rect bgRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(145.0f));
            bgRect.x = 0;
            bgRect.width = EditorGUIUtility.currentViewWidth;
            Rect logoRect = new(bgRect.width / 2 - 375f, bgRect.height / 2 - 65f, 750f, 130f);

            string bgPathProperty = "UI/background";
            string logoPathProperty = "UI/screenshotlogo";

            if (!string.IsNullOrEmpty(bgPathProperty))
            {
                Texture2D bg = Resources.Load<Texture2D>(bgPathProperty);

                if (bg != null)
                {
                    GUI.DrawTexture(bgRect, bg, ScaleMode.ScaleAndCrop);
                }
            }

            if (!string.IsNullOrEmpty(logoPathProperty))
            {
                Texture2D logo = Resources.Load<Texture2D>(logoPathProperty);

                if (logo != null)
                {
                    GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit);
                }
            }

            GUILayout.Label("Screenshot Settings", EditorStyles.boldLabel);
            selectedView = (ViewType)EditorGUILayout.EnumPopup("View Type", selectedView);
            EditorGUILayout.Space();
            folderPath = EditorGUILayout.TextField("Folder Name", folderPath);
            includeTimestamp = EditorGUILayout.Toggle("Include Timestamp", includeTimestamp);
            EditorGUILayout.Space();
            initialWidth = EditorGUILayout.IntField("Initial Width", initialWidth);
            initialHeight = EditorGUILayout.IntField("Initial Height", initialHeight);
            
            if (GUILayout.Button("Sync with Current View"))
            {
                SyncWithCurrentView();
            }
            
            resolutionMultiplier = EditorGUILayout.IntSlider("Resolution Multiplier", resolutionMultiplier, 1, 4);
            transparency = EditorGUILayout.Toggle("Transparency", transparency);
            openScreenshotOnSave = EditorGUILayout.Toggle("Open Screenshot on Save", openScreenshotOnSave);
            EditorGUILayout.Space();

            GUILayout.Label("Preview", EditorStyles.boldLabel);

            if (!Application.isPlaying && selectedView == ViewType.GameView)
            {
                EditorGUILayout.HelpBox("Game View preview only works in Play Mode. Please enter Play Mode or switch to Scene View to use this feature.", MessageType.Warning);
            }
            else
            {
                int width = (int)position.width;
                int height = 300;

                RenderSceneViewToTexture(width, height);

                if (renderTexture != null)
                {
                    GUILayout.Label(renderTexture, GUILayout.Width(width), GUILayout.Height(height));
                }
            }


            if (GUILayout.Button("Take Screenshot"))
            {
                TakeScreenshot();
            }
            if (GUILayout.Button("Open Screenshot Folder"))
            {
                OpenScreenshotFolder();
            }

        }

        private void RenderSceneViewToTexture(int width, int height)
        {
            if (selectedView == ViewType.SceneView)
            {
                if (sceneCamera == null)
                {
                    EditorGUILayout.LabelField("No active SceneView found.");
                    return;
                }
            }

            if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
            {
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    DestroyImmediate(renderTexture);
                }

                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8
                };
            }

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderTexture;

            GL.Clear(true, true, Color.clear);

            if (selectedView == ViewType.SceneView)
            {
                sceneCamera.targetTexture = renderTexture;
                sceneCamera.Render();
                sceneCamera.targetTexture = null;
            }
            else if (selectedView == ViewType.GameView)
            {
                Camera gameCamera = UnityEngine.Object.FindObjectOfType<Camera>();
                if (gameCamera != null)
                {
                    gameCamera.targetTexture = renderTexture;
                    gameCamera.Render();
                    gameCamera.targetTexture = null;
                }
                else
                {
                    EditorGUILayout.LabelField("No active Game Camera found.");
                }
            }

            RenderTexture.active = currentRT;

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void TakeScreenshot()
        {
            int width = initialWidth * resolutionMultiplier;
            int height = initialHeight * resolutionMultiplier;

            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
                depth = 24
            };
            renderTexture.Create();

            Camera camera = GetCamera();
            if (camera == null) return;

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture currentCameraRT = camera.targetTexture;
            CameraClearFlags currentClearFlags = camera.clearFlags;
            Color currentBackgroundColor = camera.backgroundColor;

            RenderTexture.active = renderTexture;
            camera.targetTexture = renderTexture;

            if (transparency)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0, 0, 0, 0);
            }

            camera.Render();

            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            camera.targetTexture = currentCameraRT;
            camera.clearFlags = currentClearFlags;
            camera.backgroundColor = currentBackgroundColor;
            RenderTexture.active = currentRT;

            SetAlphaChannel(screenshot);

            SaveScreenshot(screenshot);
        }

        private Camera GetCamera()
        {
            Camera camera = null;

            if (selectedView == ViewType.SceneView)
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    camera = sceneView.camera;
                }
                else
                {
                    HoyoToonLogs.ErrorDebug("No active SceneView found.");
                }
            }
            else if (selectedView == ViewType.GameView)
            {
                camera = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
                if (camera == null)
                {
                    HoyoToonLogs.ErrorDebug("No camera found in the scene.");
                }
            }

            return camera;
        }

        private void SetAlphaChannel(Texture2D screenshot)
        {
            Color32[] pixels = screenshot.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                {
                    pixels[i].a = 255;
                }
            }
            screenshot.SetPixels32(pixels);
            screenshot.Apply();
        }

        private void SaveScreenshot(Texture2D screenshot)
        {
            byte[] bytes = screenshot.EncodeToPNG();
            string timestamp = includeTimestamp ? "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") : "";
            string screenshotName = $"{fileName}{timestamp}.png";
            string fullFolderPath = Path.Combine(Application.dataPath, "../", folderPath);
            Directory.CreateDirectory(fullFolderPath);
            string screenshotPath = Path.Combine(fullFolderPath, screenshotName);

            File.WriteAllBytes(screenshotPath, bytes);

            HoyoToonLogs.LogDebug($"Screenshot saved to: {screenshotPath}");

            if (openScreenshotOnSave)
            {
                OpenScreenshot(screenshotPath);
            }
        }

        private void OpenScreenshot(string path)
        {
            System.Diagnostics.Process.Start(path);
        }

        private void OpenScreenshotFolder()
        {
            string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../" + folderPath));
            if (System.IO.Directory.Exists(fullPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", fullPath);
            }
            else
            {
                HoyoToonLogs.LogDebug("Screenshot folder does not exist: " + fullPath);
            }
        }

        private void SyncWithCurrentView()
        {
            if (selectedView == ViewType.SceneView)
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    initialWidth = (int)sceneView.position.width;
                    initialHeight = (int)sceneView.position.height;
                    HoyoToonLogs.LogDebug($"Synced with Scene View: {initialWidth}x{initialHeight}");
                }
                else
                {
                    HoyoToonLogs.ErrorDebug("No active SceneView found.");
                }
            }
            else if (selectedView == ViewType.GameView)
            {
                // Get Game View rendering dimensions using reflection
                try
                {
                    var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                    
                    if (gameViewType != null)
                    {
                        var gameViewWindows = Resources.FindObjectsOfTypeAll(gameViewType);
                        if (gameViewWindows.Length > 0)
                        {
                            var gameView = gameViewWindows[0];
                            
                            // Try to get the target size directly
                            var targetSizeProperty = gameViewType.GetProperty("targetSize", 
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            
                            if (targetSizeProperty != null)
                            {
                                var targetSize = (Vector2)targetSizeProperty.GetValue(gameView);
                                initialWidth = (int)targetSize.x;
                                initialHeight = (int)targetSize.y;
                                
                                // Get the display name for logging
                                var selectedSizeIndexProperty = gameViewType.GetProperty("selectedSizeIndex", 
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                
                                string sizeName = "Unknown";
                                if (selectedSizeIndexProperty != null)
                                {
                                    try
                                    {
                                        int selectedIndex = (int)selectedSizeIndexProperty.GetValue(gameView);
                                        var gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                                        var gameViewSizesInstance = gameViewSizesType.GetProperty("instance", 
                                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                                        var gameViewSizes = gameViewSizesInstance.GetValue(null);
                                        var currentGroupProperty = gameViewSizes.GetType().GetProperty("currentGroup");
                                        var currentGroup = currentGroupProperty.GetValue(gameViewSizes);
                                        var getGameViewSizeMethod = currentGroup.GetType().GetMethod("GetGameViewSize");
                                        var gameViewSize = getGameViewSizeMethod.Invoke(currentGroup, new object[] { selectedIndex });
                                        var displayTextProperty = gameViewSize.GetType().GetProperty("displayText");
                                        sizeName = (string)displayTextProperty.GetValue(gameViewSize);
                                    }
                                    catch
                                    {
                                        sizeName = $"{initialWidth}x{initialHeight}";
                                    }
                                }
                                
                                HoyoToonLogs.LogDebug($"Synced with Game View '{sizeName}': {initialWidth}x{initialHeight}");
                            }
                            else
                            {
                                // Fallback: try the previous method
                                var selectedSizeIndexProperty = gameViewType.GetProperty("selectedSizeIndex", 
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                
                                if (selectedSizeIndexProperty != null)
                                {
                                    int selectedSizeIndex = (int)selectedSizeIndexProperty.GetValue(gameView);
                                    
                                    var gameViewSizesType = System.Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                                    var gameViewSizesInstance = gameViewSizesType.GetProperty("instance", 
                                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                                    var gameViewSizes = gameViewSizesInstance.GetValue(null);
                                    var currentGroupProperty = gameViewSizes.GetType().GetProperty("currentGroup");
                                    var currentGroup = currentGroupProperty.GetValue(gameViewSizes);
                                    var getGameViewSizeMethod = currentGroup.GetType().GetMethod("GetGameViewSize");
                                    var gameViewSize = getGameViewSizeMethod.Invoke(currentGroup, new object[] { selectedSizeIndex });
                                    
                                    var widthProperty = gameViewSize.GetType().GetProperty("width");
                                    var heightProperty = gameViewSize.GetType().GetProperty("height");
                                    var displayTextProperty = gameViewSize.GetType().GetProperty("displayText");
                                    
                                    initialWidth = (int)widthProperty.GetValue(gameViewSize);
                                    initialHeight = (int)heightProperty.GetValue(gameViewSize);
                                    string displayText = (string)displayTextProperty.GetValue(gameViewSize);
                                    
                                    HoyoToonLogs.LogDebug($"Synced with Game View '{displayText}': {initialWidth}x{initialHeight}");
                                }
                                else
                                {
                                    HoyoToonLogs.ErrorDebug("Could not access Game View size properties.");
                                }
                            }
                        }
                        else
                        {
                            HoyoToonLogs.ErrorDebug("No Game View windows found.");
                        }
                    }
                    else
                    {
                        HoyoToonLogs.ErrorDebug("Could not access Game View type.");
                    }
                }
                catch (System.Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to sync with Game View: {ex.Message}");
                    // Fallback: try to get main camera screen dimensions
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        initialWidth = Screen.width;
                        initialHeight = Screen.height;
                        HoyoToonLogs.LogDebug($"Using fallback screen dimensions: {initialWidth}x{initialHeight}");
                    }
                    else
                    {
                        // Ultimate fallback
                        initialWidth = 1920;
                        initialHeight = 1080;
                        HoyoToonLogs.LogDebug($"Using default resolution: {initialWidth}x{initialHeight}");
                    }
                }
            }
        }
    }
}
#endif