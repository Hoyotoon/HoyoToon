using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon
{
    [ExecuteAlways]
    public class HoyoToonPostProcessing : MonoBehaviour
    {
        public enum GameType { Off, Genshin, StarRail, WutheringWaves };
        private GameType previousGameType = GameType.Genshin;
        public GameType gameType = GameType.Genshin;

        public bool IsGameTypeOff() => gameType == GameType.Off;
        public bool IsGameTypeGenshin() => gameType == GameType.Genshin;
        public bool IsGameTypeStarRail() => gameType == GameType.StarRail;
        public bool IsGameTypeWutheringWaves() => gameType == GameType.WutheringWaves;

        public LayerMask Layer;
        private RenderTexture targetTexture;

        public Color bloomColor = Color.white;
        public float bloomThreshold = 0.6f;
        public float bloomIntensity = 0.75f;
        public float bloomScalar = 2.3f;
        [Range(0.0001f, 5.0f)]
        public float bloomRadius = 0.5f;
        public Vector4 blurLevelWeights = new Vector4(0.3f, 0.3f, 0.26f, 0.15f);


        private bool tonemapping = true;
        [SerializeField]
        private Texture2D _starRailLUT;
        public Texture2D StarRailLUT
        {
            get
            {
                if (_starRailLUT == null)
                {
                    _starRailLUT = Resources.Load<Texture2D>("HSR/Textures/LUTS/LUT_HSR");
                }
                return _starRailLUT;
            }
            set { _starRailLUT = value; }
        }

        [SerializeField]
        private Texture2D _wuwaLUT;
        public Texture2D WuwaLUT
        {
            get
            {
                if (_wuwaLUT == null)
                {
                    _wuwaLUT = Resources.Load<Texture2D>("Wuwa/Textures/LUTS/LUT_WUWA");
                }
                return _wuwaLUT;
            }
            set { _wuwaLUT = value; }
        }

        public Vector3 lut2DTexParam = new Vector3(0.00098f, 0.03125f, 31.00f);
        public float exposure = 1.0f;


        private bool isBalanced = true;


        public float sharpening = 0.0f; // the default for genshin should be 0.0f and star rail it should be 0.3f

        public Color vignetteColor = new Color(0.00983f, 0.00983f, 0.01102f, 1.0f);
        public Vector4 vignetteParams = new Vector4(0.5f, 0.49079f, 0.03718f, 0.13277f);

        public bool useDepthBuffer = true;
        private int depthBufferValue = 0;
        private int previousDepthBufferValue = 0;



        private const string _bloomBufferName = "HoyoToon Post Processing";
        private const string _layerBufferName = "HoyoToon Layer Renderer";
        private CommandBuffer _bloomBuffer;
        private CommandBuffer _layerBuffer;
        private Camera _camera;
        private Material _bloomMaterial;
        private bool isInSceneView = false;

        private void OnEnable()
        {
            SetupMaterial();
            CreateCommandBuffers();
            Camera.onPreRender += OnCameraPreRender;
#if UNITY_EDITOR
            EditorApplication.update += CheckEditorState;
            SceneView.duringSceneGui += OnSceneGUI;
#endif
            ValidateAndLoadLUTTextures();
        }

        private void OnDisable()
        {
            CleanUp();
        }

        private void OnDestroy()
        {
            CleanUp();
        }

        private void CleanUp()
        {
            Camera.onPreRender -= OnCameraPreRender;
            RemoveCommandBuffers();
            if (_bloomMaterial != null)
            {
                DestroyImmediate(_bloomMaterial);
                _bloomMaterial = null;
            }
            if (targetTexture != null)
            {
                targetTexture.Release();
                DestroyImmediate(targetTexture);
                targetTexture = null;
            }
#if UNITY_EDITOR
            EditorApplication.update -= CheckEditorState;
            SceneView.duringSceneGui -= OnSceneGUI;
#endif
        }

        private void SetupMaterial()
        {
            if (_bloomMaterial == null)
            {
                Shader bloomShader = Shader.Find("Hidden/HoyoToon/Post Processing/Bloom");
                if (bloomShader != null)
                {
                    _bloomMaterial = new Material(bloomShader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                else
                {
                    Debug.LogError("HoyoToonPostProcessing: Bloom shader not found!");
                }
            }
        }

        private void CreateCommandBuffers()
        {
            _layerBuffer = new CommandBuffer { name = _layerBufferName };
            _bloomBuffer = new CommandBuffer { name = _bloomBufferName };
        }

        private void Update()
        {
            SetMaterialProperties();
            RecreateCommandBuffers();
        }

        private void OnValidate()
        {
            SetMaterialProperties();
            ValidateAndLoadLUTTextures();
        }

        private void OnCameraPreRender(Camera cam)
        {
            if (cam == null || cam.name == "Reflection Camera" || cam.name == "Preview Camera" || cam.name == "Preview Scene Camera") return;

            if (_camera != cam)
            {
                RemoveCommandBuffers();
                _camera = cam;
            }

            RecreateCommandBuffers();
        }

        private void RemoveCommandBuffers()
        {
            if (_camera != null)
            {
                RemoveSpecificCommandBuffer(_camera, CameraEvent.BeforeImageEffects, _bloomBufferName);
                RemoveSpecificCommandBuffer(_camera, CameraEvent.BeforeImageEffects, _layerBufferName);
            }

            if (_bloomBuffer != null)
            {
                _bloomBuffer.Clear();
                _bloomBuffer.Release();
                _bloomBuffer = null;
            }

            if (_layerBuffer != null)
            {
                _layerBuffer.Clear();
                _layerBuffer.Release();
                _layerBuffer = null;
            }
        }

        private void RemoveSpecificCommandBuffer(Camera camera, CameraEvent evt, string bufferName)
        {
            CommandBuffer[] buffers = camera.GetCommandBuffers(evt);
            for (int i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] != null && buffers[i].name == bufferName)
                {
                    camera.RemoveCommandBuffer(evt, buffers[i]);
                    buffers[i].Clear();
                    buffers[i].Release();
                }
            }
        }

        private void RecreateCommandBuffers()
        {
            RemoveCommandBuffers();
            CreateCommandBuffers();
            SetupLayerBuffer();
            SetupBloomBuffer();
        }

        private void SetupBloomBuffer()
        {
            if (_camera == null || _bloomMaterial == null || targetTexture == null) return;

            // Clear any existing commands to prevent memory leaks
            _bloomBuffer.Clear();

            // Set up render texture IDs
            int _OriginalID = Shader.PropertyToID("_OriginalTexture");
            int _HDRID = Shader.PropertyToID("_HDRTexture");
            int _PreFilterID = Shader.PropertyToID("_PreFilter");
            int _BloomHID = Shader.PropertyToID("_BloomH");
            int _BloomVID = Shader.PropertyToID("_BloomV");
            int _BloomAHID = Shader.PropertyToID("_BloomAH");
            int _BloomAVID = Shader.PropertyToID("_BloomAV");
            int _BloomBHID = Shader.PropertyToID("_BloomBH");
            int _BloomBVID = Shader.PropertyToID("_BloomBV");
            int _BloomCHID = Shader.PropertyToID("_BloomCH");
            int _BloomCVID = Shader.PropertyToID("_BloomCV");
            int _MHYBloomTexID = Shader.PropertyToID("_MHYBloomTex");
            int _FinalImageID = Shader.PropertyToID("_FinalImage");

            try
            {
                // Calculate downsampled texture sizes
                int width = Mathf.RoundToInt(_camera.pixelWidth * 0.25f);
                int height = Mathf.RoundToInt(_camera.pixelHeight * 0.25f);

                // Get temporary render textures
                _bloomBuffer.GetTemporaryRT(_HDRID, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_OriginalID, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_PreFilterID, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomHID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomVID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomAHID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomAVID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomBHID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomBVID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomCHID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_BloomCVID, 152, 158, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_MHYBloomTexID, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                _bloomBuffer.GetTemporaryRT(_FinalImageID, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

                // Set the layer texture
                _bloomMaterial.SetTexture("_LayerTex", targetTexture);

                // Blit images to render textures
                _bloomBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _HDRID);
                _bloomBuffer.Blit(BuiltinRenderTextureType.CameraTarget, _OriginalID);
                _bloomBuffer.Blit(_OriginalID, _PreFilterID, _bloomMaterial, 1);
                _bloomBuffer.Blit(_PreFilterID, _BloomHID, _bloomMaterial, 2);
                _bloomBuffer.Blit(_BloomHID, _BloomVID, _bloomMaterial, 3);
                _bloomBuffer.Blit(_BloomVID, _BloomAHID, _bloomMaterial, 4);
                _bloomBuffer.Blit(_BloomAHID, _BloomAVID, _bloomMaterial, 5);
                _bloomBuffer.Blit(_BloomAVID, _BloomBHID, _bloomMaterial, 6);
                _bloomBuffer.Blit(_BloomBHID, _BloomBVID, _bloomMaterial, 7);
                _bloomBuffer.Blit(_BloomBVID, _BloomCHID, _bloomMaterial, 8);
                _bloomBuffer.Blit(_BloomCHID, _BloomCVID, _bloomMaterial, 9);
                _bloomBuffer.Blit(_BloomCVID, _MHYBloomTexID, _bloomMaterial, 10);
                _bloomBuffer.Blit(_MHYBloomTexID, _FinalImageID, _bloomMaterial, 11);
                _bloomBuffer.Blit(_FinalImageID, BuiltinRenderTextureType.CameraTarget, _bloomMaterial, 11);
            }
            finally
            {
                // Release temporary render textures - ensure this happens even if an error occurs
                _bloomBuffer.ReleaseTemporaryRT(_HDRID);
                _bloomBuffer.ReleaseTemporaryRT(_OriginalID);
                _bloomBuffer.ReleaseTemporaryRT(_PreFilterID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomHID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomVID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomAHID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomAVID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomBHID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomBVID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomCHID);
                _bloomBuffer.ReleaseTemporaryRT(_BloomCVID);
                _bloomBuffer.ReleaseTemporaryRT(_MHYBloomTexID);
                _bloomBuffer.ReleaseTemporaryRT(_FinalImageID);
            }

            // Add command buffer to camera
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _bloomBuffer);
        }

        private void SetupLayerBuffer()
        {
            if (_camera == null || _layerBuffer == null) return;

            UpdateRenderTexture(_camera);

            _layerBuffer.SetRenderTarget(targetTexture);
            _layerBuffer.ClearRenderTarget(true, true, Color.red);

            _layerBuffer.SetGlobalFloat("_LayerIndex", 1.0f);
            _layerBuffer.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);

            Renderer[] allRenderers = FindObjectsOfType<Renderer>(false);
            System.Array.Reverse(allRenderers);

            foreach (Renderer renderer in allRenderers)
            {
                if ((Layer.value & (1 << renderer.gameObject.layer)) != 0)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        if (material != null)
                        {
                            if (material.shader.name.Contains("Genshin"))
                            {
                                _layerBuffer.DrawRenderer(renderer, material, materialIndex, 0);
                            }
                            else
                            {
                                _layerBuffer.DrawRenderer(renderer, material, materialIndex, 0);
                                if (material.passCount > 1)
                                {
                                    _layerBuffer.DrawRenderer(renderer, material, materialIndex, 1);
                                }
                            }
                        }
                    }
                }
            }

            // Add command buffer to camera
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _layerBuffer);
        }

        private void UpdateRenderTexture(Camera camera)
        {
            int width = camera.pixelWidth;
            int height = camera.pixelHeight;

            if (targetTexture == null || targetTexture.width != width || targetTexture.height != height)
            {
                if (targetTexture != null)
                {
                    targetTexture.Release();
                    DestroyImmediate(targetTexture);
                }

                targetTexture = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
                targetTexture.name = "HoyoToonLayerRenderTexture";
                targetTexture.filterMode = FilterMode.Bilinear;
                targetTexture.wrapMode = TextureWrapMode.Clamp;
            }
        }

        private void SetMaterialProperties()
        {
            if (_bloomMaterial == null) return;

            // Check if the tone mapping type has changed   
            if (gameType != previousGameType)
            {
                // Validate and load LUT textures for the new game type
                ValidateAndLoadLUTTextures();

                // Reset values based on the new tone mapping type
                if (gameType == GameType.Off)
                {
                    tonemapping = false;
                    isBalanced = false;
                    bloomThreshold = 1.0f;
                    bloomColor = Color.black;
                    bloomIntensity = 0.0f;
                    bloomScalar = 1.0f;
                }
                else if (gameType == GameType.StarRail)
                {
                    bloomRadius = 1.0f;
                    tonemapping = false;
                    isBalanced = false;
                    sharpening = 0.3f;
                    bloomColor = Color.black;
                    blurLevelWeights = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                    bloomIntensity = 0.6f;
                    bloomThreshold = 0.7f;
                    bloomScalar = 1.0f;
                    lut2DTexParam = new Vector4(0.00098f, 0.03125f, 31.00f, 0.0f);
                }
                else if (gameType == GameType.Genshin)
                {
                    bloomRadius = 0.5f;
                    tonemapping = true;
                    isBalanced = true;
                    bloomColor = Color.black;
                    bloomIntensity = 0.75f;
                    bloomThreshold = 0.6f;
                    bloomScalar = 2.3f;
                    blurLevelWeights = new Vector4(0.3f, 0.3f, 0.26f, 0.15f);
                }
                else if (gameType == GameType.WutheringWaves)
                {
                    bloomRadius = 0.5f;
                    tonemapping = false;
                    isBalanced = false;
                    sharpening = 0.3f;
                    bloomColor = Color.black;
                    blurLevelWeights = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
                    bloomIntensity = 0.5f;
                    bloomThreshold = 0.5f;
                    bloomScalar = 1.5f;
                    lut2DTexParam = new Vector4(0.0011f, 0.0311f, 33.00f, 0.0f);
                }

                // Update the previous tone mapping type
                previousGameType = gameType;
            }


            // Update depth buffer value
            depthBufferValue = useDepthBuffer ? 32 : 0;

            if (depthBufferValue != previousDepthBufferValue)
            {
                // Update the previous value for the next frame
                previousDepthBufferValue = depthBufferValue;
            }

            // depending on gametype set the lut2D texture
            Texture2D lut2DTex = null;
            if (gameType == GameType.StarRail)
            {
                lut2DTex = StarRailLUT;
                if (lut2DTex == null)
                {
                    Debug.LogWarning("HoyoToonPostProcessing: StarRail LUT texture is null. Attempting to reload...");
                    ValidateAndLoadLUTTextures();
                    lut2DTex = StarRailLUT;
                }
            }
            else if (gameType == GameType.WutheringWaves)
            {
                lut2DTex = WuwaLUT;
                if (lut2DTex == null)
                {
                    Debug.LogWarning("HoyoToonPostProcessing: Wuwa LUT texture is null. Attempting to reload...");
                    ValidateAndLoadLUTTextures();
                    lut2DTex = WuwaLUT;
                }
            }

            _bloomMaterial.SetTexture("_Lut2DTex", lut2DTex);

            // 

            _bloomMaterial.SetFloat("_GameType", gameType == GameType.Off ? 0.0f :
                                                 gameType == GameType.Genshin ? 1.0f :
                                                 gameType == GameType.StarRail ? 2.0f :
                                                 gameType == GameType.WutheringWaves ? 3.0f : 0.0f);
            _bloomMaterial.SetFloat("_MHYBloomThreshold", bloomThreshold);
            _bloomMaterial.SetFloat("_BloomThreshold", bloomThreshold);
            _bloomMaterial.SetFloat("_BloomR", bloomColor.r);
            _bloomMaterial.SetFloat("_BloomG", bloomColor.g);
            _bloomMaterial.SetFloat("_BloomB", bloomColor.b);
            _bloomMaterial.SetFloat("_MHYBloomIntensity", bloomIntensity);
            _bloomMaterial.SetFloat("_BloomIntensity", bloomIntensity);
            _bloomMaterial.SetFloat("_MHYBloomScaler", bloomScalar);
            _bloomMaterial.SetFloat("_MHYBloomTonemapping", tonemapping ? 1.0f : 0.0f);
            _bloomMaterial.SetFloat("_MHYBloomExposure", exposure);
            _bloomMaterial.SetVector("_UVTransformSource", new Vector4(1, 1, 0, 0));
            _bloomMaterial.SetVector("_UVTransformTarget", new Vector4(1, 1, 0, 0));
            _bloomMaterial.SetVector("_BlurLevelWeights", blurLevelWeights);
            _bloomMaterial.SetFloat("_bloomRadius", bloomRadius);
            _bloomMaterial.SetFloat("_UseBalance", isBalanced ? 1.0f : 0.0f);
            _bloomMaterial.SetVector("_Lut2DTexParam", lut2DTexParam);
            _bloomMaterial.SetFloat("_Sharpening", sharpening);
            _bloomMaterial.SetColor("_Vignette_Params1", vignetteColor);
            _bloomMaterial.SetVector("_Vignette_Params2", vignetteParams);
        }

        /// <summary>
        /// Validates and ensures LUT textures are loaded for the current game type
        /// </summary>
        private void ValidateAndLoadLUTTextures()
        {
            switch (gameType)
            {
                case GameType.StarRail:
                    if (_starRailLUT == null)
                    {
                        _starRailLUT = Resources.Load<Texture2D>("HSR/Textures/LUTS/LUT_HSR");
                        if (_starRailLUT == null)
                        {
                            Debug.LogWarning("HoyoToonPostProcessing: StarRail LUT texture not found at 'HSR/Textures/LUTS/LUT_HSR'. Please ensure the texture is in the correct Resources folder.");
                        }
                        else
                        {
                            Debug.Log("HoyoToonPostProcessing: StarRail LUT texture loaded successfully.");
                        }
                    }
                    break;

                case GameType.WutheringWaves:
                    if (_wuwaLUT == null)
                    {
                        _wuwaLUT = Resources.Load<Texture2D>("Wuwa/Textures/LUTS/LUT_WUWA");
                        if (_wuwaLUT == null)
                        {
                            Debug.LogWarning("HoyoToonPostProcessing: Wuthering Waves LUT texture not found at 'Wuwa/Textures/LUTS/LUT_WUWA'. Please ensure the texture is in the correct Resources folder.");
                        }
                        else
                        {
                            Debug.Log("HoyoToonPostProcessing: Wuthering Waves LUT texture loaded successfully.");
                        }
                    }
                    break;

                case GameType.Genshin:
                case GameType.Off:
                    // Genshin and Off modes don't use LUT textures
                    break;
            }
        }

        /// <summary>
        /// Forces reload of all LUT textures (useful for debugging or when resources change)
        /// </summary>
        [ContextMenu("Reload LUT Textures")]
        public void ReloadLUTTextures()
        {
            _starRailLUT = null;
            _wuwaLUT = null;
            ValidateAndLoadLUTTextures();
        }

        /// <summary>
        /// Validates LUT texture setup and logs current status
        /// </summary>
        [ContextMenu("Validate LUT Setup")]
        public void ValidateLUTSetup()
        {
            Debug.Log($"HoyoToonPostProcessing LUT Status Report:");
            Debug.Log($"Current Game Type: {gameType}");

            switch (gameType)
            {
                case GameType.StarRail:
                    Debug.Log($"StarRail LUT Required: YES");
                    Debug.Log($"StarRail LUT Loaded: {(_starRailLUT != null ? "YES" : "NO")}");
                    if (_starRailLUT != null)
                        Debug.Log($"StarRail LUT Path: HSR/Textures/LUTS/LUT_HSR");
                    break;

                case GameType.WutheringWaves:
                    Debug.Log($"Wuwa LUT Required: YES");
                    Debug.Log($"Wuwa LUT Loaded: {(_wuwaLUT != null ? "YES" : "NO")}");
                    if (_wuwaLUT != null)
                        Debug.Log($"Wuwa LUT Path: Wuwa/Textures/LUTS/LUT_WUWA");
                    break;

                case GameType.Genshin:
                    Debug.Log($"LUT Required: NO (Genshin uses built-in tonemapping)");
                    break;

                case GameType.Off:
                    Debug.Log($"LUT Required: NO (Post-processing disabled)");
                    break;
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void RegisterSceneViewUpdate()
        {
            EditorApplication.update += UpdateAllPostProcesses;
        }

        static void UpdateAllPostProcesses()
        {
            if (!Application.isPlaying)
            {
                HoyoToonPostProcessing[] postProcesses = FindObjectsOfType<HoyoToonPostProcessing>();
                foreach (var postProcess in postProcesses)
                {
                    postProcess.RecreateCommandBuffers();
                }
            }
        }

        private void CheckEditorState()
        {
            if (!Application.isPlaying)
            {
                bool newIsInSceneView = IsSceneViewActive();
                if (newIsInSceneView != isInSceneView)
                {
                    isInSceneView = newIsInSceneView;
                    Camera targetCamera = isInSceneView ? SceneView.lastActiveSceneView?.camera : Camera.main;
                    if (targetCamera != null && targetCamera != _camera)
                    {
                        _camera = targetCamera;
                        RecreateCommandBuffers();
                    }
                }
            }
        }

        private bool IsSceneViewActive()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && EditorWindow.focusedWindow == sceneView)
            {
                return true;
            }

            if (EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "GameView")
            {
                return false;
            }

            return isInSceneView;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            isInSceneView = true;
            _camera = sceneView.camera;
            RecreateCommandBuffers();
        }
#endif
    }
}