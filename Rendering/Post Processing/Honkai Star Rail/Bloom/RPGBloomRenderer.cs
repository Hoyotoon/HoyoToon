using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;
using HoyoToon.Rendering.PostProcessing.HSR.Uber;

namespace HoyoToon.Rendering.PostProcessing.HSR.Bloom
{
    public class RPGBloomRenderer : ScriptableRendererFeature
    {
        public static int LastBloomTextureFrame { get; private set; } = -1;
        public static int LastBloomTextureCameraId { get; private set; } = -1;

        public static bool HasBloomTextureForCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            return LastBloomTextureFrame == Time.frameCount && LastBloomTextureCameraId == camera.GetInstanceID();
        }

        private static void MarkBloomTextureUnavailable(int cameraId)
        {
            LastBloomTextureFrame = -1;
            LastBloomTextureCameraId = cameraId;
        }

        private static void MarkBloomTextureAvailable(int cameraId)
        {
            LastBloomTextureFrame = Time.frameCount;
            LastBloomTextureCameraId = cameraId;
        }

        private enum BloomStageTextureFormat
        {
            CameraTarget,
            R16G16B16A16_SFloat,
            B10G11R11_UFloatPack32,
            R8G8B8A8_UNorm
        }

        [SerializeField]
        private BloomStageTextureFormat _bloomStageTextureFormat = BloomStageTextureFormat.R16G16B16A16_SFloat;

        private RPGBloomRenderPass _renderPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            GraphicsFormat cameraFormat = renderingData.cameraData.cameraTargetDescriptor.graphicsFormat;
            _renderPass.SetBloomStageFormat(ResolveBloomStageGraphicsFormat(_bloomStageTextureFormat, cameraFormat));

            renderer.EnqueuePass(_renderPass);
        }

        public override void Create()
        {
            _renderPass = new RPGBloomRenderPass();
        }

        protected override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            _renderPass = null;
        }

        private static GraphicsFormat ResolveBloomStageGraphicsFormat(BloomStageTextureFormat selectedFormat, GraphicsFormat cameraFormat)
        {
            GraphicsFormat preferred = selectedFormat switch
            {
                BloomStageTextureFormat.CameraTarget => cameraFormat,
                BloomStageTextureFormat.R16G16B16A16_SFloat => GraphicsFormat.R16G16B16A16_SFloat,
                // B10G11R11 has no alpha channel; remap to alpha-capable HDR format.
                BloomStageTextureFormat.B10G11R11_UFloatPack32 => GraphicsFormat.R16G16B16A16_SFloat,
                BloomStageTextureFormat.R8G8B8A8_UNorm => GraphicsFormat.R8G8B8A8_UNorm,
                _ => GraphicsFormat.R16G16B16A16_SFloat,
            };

            if (!GraphicsFormatUtility.HasAlphaChannel(preferred))
            {
                preferred = GraphicsFormat.R16G16B16A16_SFloat;
            }

            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
            {
                return preferred;
            }

            if (GraphicsFormatUtility.HasAlphaChannel(cameraFormat) && SystemInfo.IsFormatSupported(cameraFormat, GraphicsFormatUsage.Render))
            {
                return cameraFormat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
            {
                return GraphicsFormat.R16G16B16A16_SFloat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Render))
            {
                return GraphicsFormat.R8G8B8A8_UNorm;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render))
            {
                return GraphicsFormat.R8G8B8A8_SRGB;
            }

            if (SystemInfo.IsFormatSupported(cameraFormat, GraphicsFormatUsage.Render))
            {
                return cameraFormat;
            }

            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        class RPGBloomRenderPass : ScriptableRenderPass
        {
            private const int BloomAtlasBlurCount = 4;
            private const int BloomMaxKernelSize = 32;
            private const int BloomAtlasPadding = 1;

            private static readonly Vector2Int[] FixedBloomMipSizes =
            {
                new Vector2Int(960, 540),
                new Vector2Int(480, 270),
                new Vector2Int(310, 174),
                new Vector2Int(155, 87),
                new Vector2Int(72, 40),
                new Vector2Int(36, 20),
            };

            private const int BloomAtlasWidth = 466;
            private const int BloomAtlasHeight = 174;
            private const int BloomOutputWidth = 310;
            private const int BloomOutputHeight = 174;

            private static readonly Vector4 FullscreenScaleBias = new Vector4(1f, 1f, 0f, 0f);

            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string RenderGraphName = "RPG Bloom";
            private const string BloomPrefilterPass = "BloomPrefilter";
            private const string DownsamplePass = "Downsample";
            private const string GaussianPass = "Gaussian";
            private const string BlurAtlasPass = "AtlasBlur";
            private const string CombineAtlasPass = "AtlasCombine";

            private readonly int _mainTexId = Shader.PropertyToID("_MainTex");
            private readonly int _bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
            private readonly int _bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
            private readonly int _bloomRId = Shader.PropertyToID("_BloomR");
            private readonly int _bloomGId = Shader.PropertyToID("_BloomG");
            private readonly int _bloomBId = Shader.PropertyToID("_BloomB");
            private readonly int _hsrBloomTexId = Shader.PropertyToID("_HSRBloomTexture");
            private readonly int _bloomUVMinMaxId = Shader.PropertyToID("_BloomUVMinMax");
            private readonly int _bloomUVIndexId = Shader.PropertyToID("_BloomUVIndex");
            private readonly int _bloomKernelSizeId = Shader.PropertyToID("_BloomKernelSize");
            private readonly int _bloomKernelId = Shader.PropertyToID("_BloomKernel");
            private readonly int _bloomLayerIntensityId = Shader.PropertyToID("_GaussianLayerIntensity");
            private readonly int _gaussTapsId = Shader.PropertyToID("_GaussTaps");
            private readonly int _gaussOffsetId = Shader.PropertyToID("_GaussOffset");
            private readonly int _gaussWeightsId = Shader.PropertyToID("_GaussWeights");
            private readonly int _blurScaleId = Shader.PropertyToID("_BlurScale");
            private readonly int _gaussianUVClampId = Shader.PropertyToID("_GaussianUVClamp");
            private readonly int _gaussianUVTransformId = Shader.PropertyToID("_GaussianUVTransform");
            private readonly int _bloomAtlasUVTransId = Shader.PropertyToID("_BloomAtlasUVTrans");

            private Material _material;
            private readonly Dictionary<string, int> _passIndices = new Dictionary<string, int>(StringComparer.Ordinal);

            private bool _loggedMissingSettings;
            private GraphicsFormat _bloomStageFormat = GraphicsFormat.R16G16B16A16_SFloat;
            private int _blurStageCount;
            private int _mipDownCount;
            private float _bloomThreshold;
            private float _bloomIntensity;
            private float _bloomR;
            private float _bloomG;
            private float _bloomB;

            private readonly int[] _kernelSizes = new int[BloomAtlasBlurCount];
            private readonly float[] _kernelSigmas = new float[BloomAtlasBlurCount];
            private readonly float[][] _kernels = new float[BloomAtlasBlurCount][];
            private readonly float[][] _gaussWeightsHorizontal = new float[BloomAtlasBlurCount][];
            private readonly Vector4[][] _gaussOffsetsHorizontal = new Vector4[BloomAtlasBlurCount][];
            private readonly float[][] _gaussWeightsVertical = new float[BloomAtlasBlurCount][];
            private readonly Vector4[][] _gaussOffsetsVertical = new Vector4[BloomAtlasBlurCount][];
            private readonly float[] _layerIntensities = new float[BloomAtlasBlurCount];
            private readonly Rect[] _atlasViewports = new Rect[BloomAtlasBlurCount];
            private readonly Vector4[] _atlasUvMinMax = new Vector4[BloomAtlasBlurCount];
            private readonly Vector4[] _atlasUvTransforms = new Vector4[BloomAtlasBlurCount];

            private sealed class BloomAtlasPassData
            {
                internal Material material;
                internal TextureHandle source;
                internal TextureHandle bloomOutput;
                internal TextureHandle[] mipDown;
                internal int blurStartIndex;
                internal int blurStageCount;
                internal TextureHandle atlas1;
                internal TextureHandle atlas2;
                internal Rect[] atlasViewports;
                internal Vector4[] atlasUvMinMax;
                internal int outputWidth;
                internal int outputHeight;

                internal int prefilterPass;
                internal int downsamplePass;
                internal int gaussianPass;
                internal int atlasBlurPass;
                internal int atlasCombinePass;

                internal int bloomThresholdId;
                internal int bloomIntensityId;
                internal int bloomRId;
                internal int bloomGId;
                internal int bloomBId;
                internal int bloomUVMinMaxId;
                internal int bloomUVIndexId;
                internal int bloomKernelSizeId;
                internal int bloomKernelId;
                internal int bloomLayerIntensityId;
                internal int gaussTapsId;
                internal int gaussOffsetId;
                internal int gaussWeightsId;
                internal int blurScaleId;
                internal int gaussianUVClampId;
                internal int gaussianUVTransformId;
                internal int bloomAtlasUVTransId;
                internal int atlasWidth;
                internal int atlasHeight;

                internal int[] kernelSizes;
                internal float[][] kernels;
                internal float[][] gaussWeightsHorizontal;
                internal Vector4[][] gaussOffsetsHorizontal;
                internal float[][] gaussWeightsVertical;
                internal Vector4[][] gaussOffsetsVertical;
                internal float[] layerIntensities;
                internal Vector4[] atlasUvTransforms;
                internal float bloomThreshold;
                internal float bloomIntensity;
                internal float bloomR;
                internal float bloomG;
                internal float bloomB;
            }

            public RPGBloomRenderPass()
            {
                _material = CoreUtils.CreateEngineMaterial(ShaderName);
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;

                for (int i = 0; i < BloomAtlasBlurCount; i++)
                {
                    _kernels[i] = new float[BloomMaxKernelSize];
                    _gaussWeightsHorizontal[i] = new float[BloomMaxKernelSize];
                    _gaussOffsetsHorizontal[i] = new Vector4[BloomMaxKernelSize];
                    _gaussWeightsVertical[i] = new float[BloomMaxKernelSize];
                    _gaussOffsetsVertical[i] = new Vector4[BloomMaxKernelSize];
                }
            }

            public void Dispose()
            {
                if (_material == null)
                {
                    return;
                }

                CoreUtils.Destroy(_material);
                _material = null;
            }

            public void SetBloomStageFormat(GraphicsFormat format)
            {
                _bloomStageFormat = format;
            }

            private int TryGetPassByName(string passName)
            {
                if (_material == null)
                {
                    return -1;
                }

                if (_passIndices.TryGetValue(passName, out int cachedPassIndex))
                {
                    if (cachedPassIndex >= 0 && cachedPassIndex < _material.passCount)
                    {
                        return cachedPassIndex;
                    }

                    _passIndices.Remove(passName);
                }

                int passIndex = _material.FindPass(passName);
                if (passIndex < 0)
                {
                    Debug.LogWarning($"{nameof(RPGBloomRenderer)}: shader pass '{passName}' was not found on '{ShaderName}'.");
                }

                _passIndices[passName] = passIndex;
                return passIndex;
            }

            private bool TryApplyVolumeSettings()
            {
                if (_material == null)
                {
                    return false;
                }

                RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();
                bool shouldUseUberControl = uberSettings != null
                    && uberSettings.UseUberControl.overrideState
                    && uberSettings.UseUberControl.value;
                if (shouldUseUberControl && !uberSettings.EnableBloom.value)
                {
                    return false;
                }

                RPGBloom settings = VolumeManager.instance.stack.GetComponent<RPGBloom>();
                if (settings == null)
                {
                    if (!_loggedMissingSettings)
                    {
                        Debug.LogWarning($"{nameof(RPGBloomRenderer)}: no {nameof(RPGBloom)} found in the active volume stack.");
                        _loggedMissingSettings = true;
                    }

                    return false;
                }

                if (!settings.IsActive())
                {
                    return false;
                }

                List<RPGBloom.BloomStage> stageList = settings.AllBloomStages.value?.Stages;
                if (stageList == null || stageList.Count == 0)
                {
                    if (!_loggedMissingSettings)
                    {
                        Debug.LogWarning($"{nameof(RPGBloomRenderer)}: {nameof(RPGBloom)} has no bloom stages configured.");
                        _loggedMissingSettings = true;
                    }

                    return false;
                }

                _loggedMissingSettings = false;
                _blurStageCount = Mathf.Clamp(stageList.Count, 1, BloomAtlasBlurCount);
                _mipDownCount = FixedBloomMipSizes.Length;

                for (int i = 0; i < _blurStageCount; i++)
                {
                    RPGBloom.BloomStage stage = stageList[i];
                    int kernelSize = Mathf.Clamp(stage.KernelSize, 1, BloomMaxKernelSize);
                    _kernelSizes[i] = kernelSize;
                    _kernelSigmas[i] = Mathf.Max(stage.KernalSigma, 1e-5f);
                    _layerIntensities[i] = Mathf.Max(0f, stage.LayerIntensity);
                    CalculateBloomKernel(_kernels[i], kernelSize);
                }

                _bloomThreshold = settings.BloomThreshold.value;
                _bloomIntensity = settings.BloomIntensity.value;
                _bloomR = settings.BloomR.value;
                _bloomG = settings.BloomG.value;
                _bloomB = settings.BloomB.value;

                return true;
            }

            private static Vector4 ViewportToUVMinMax(Rect rect, float textureWidth, float textureHeight)
            {
                return new Vector4(rect.x / textureWidth, rect.y / textureHeight,
                    rect.xMax / textureWidth, rect.yMax / textureHeight);
            }

            private void SetupAtlasLayout(int baseWidth, int baseHeight, int atlasWidth, int atlasHeight)
            {
                Vector2 baseSize = new Vector2(baseWidth, baseHeight);

                _atlasViewports[0] = new Rect(Vector2.zero, baseSize);
                _atlasViewports[1] = new Rect(new Vector2(_atlasViewports[0].xMax + BloomAtlasPadding, 0f), baseSize * 0.5f);
                _atlasViewports[2] = new Rect(new Vector2(_atlasViewports[1].x, _atlasViewports[1].yMax + BloomAtlasPadding), baseSize * 0.25f);
                _atlasViewports[3] = new Rect(new Vector2(_atlasViewports[2].xMax + BloomAtlasPadding, _atlasViewports[2].y), baseSize * 0.125f);

                for (int i = 0; i < BloomAtlasBlurCount; i++)
                {
                    _atlasUvTransforms[i] = new Vector4(0f, 0f, 1f, 1f);
                }

                for (int i = 0; i < _blurStageCount; i++)
                {
                    _atlasUvMinMax[i] = ViewportToUVMinMax(_atlasViewports[i], atlasWidth, atlasHeight);
                    _atlasUvTransforms[i] = new Vector4(
                        _atlasUvMinMax[i].z - _atlasUvMinMax[i].x,
                        _atlasUvMinMax[i].w - _atlasUvMinMax[i].y,
                        _atlasUvMinMax[i].x,
                        _atlasUvMinMax[i].y);
                }
            }

            private void BuildGaussianKernelData(float atlasWidth, float atlasHeight)
            {
                for (int i = 0; i < _blurStageCount; i++)
                {
                    float stageWidth = Mathf.Max(1f, _atlasViewports[i].width);
                    float stageHeight = Mathf.Max(1f, _atlasViewports[i].height);

                    CalculateGaussianKernel(
                        _kernelSizes[i],
                        _kernelSigmas[i],
                        stageWidth,
                        stageHeight,
                        true,
                        out _gaussWeightsHorizontal[i],
                        out _gaussOffsetsHorizontal[i]);

                    CalculateGaussianKernel(
                        _kernelSizes[i],
                        _kernelSigmas[i],
                        Mathf.Max(1f, atlasWidth),
                        Mathf.Max(1f, atlasHeight),
                        false,
                        out _gaussWeightsVertical[i],
                        out _gaussOffsetsVertical[i]);
                }
            }

            // kernel = KernelSigma, size = KernelSize
            private static void CalculateBloomKernel(float[] kernel, int size)
            {
                int n = size + 3;
                long sum = (1L << n) - 2 * (1 + n);
                double value = n / (double)sum;

                for (int i = 0; i < size; i++)
                {
                    int k = i + 1;
                    value *= n - k;
                    value /= k + 1;
                    kernel[i] = (float)value;
                }
            }

            private static void CalculateGaussianKernel(
                int targetTaps,
                float sigma,
                float atlasWidth,
                float atlasHeight,
                bool isHorizontal,
                out float[] outWeights,
                out Vector4[] outOffsets)
            {
                outWeights = new float[BloomMaxKernelSize];
                outOffsets = new Vector4[BloomMaxKernelSize];

                int taps = Mathf.Clamp(targetTaps, 1, BloomMaxKernelSize);
                float texelX = 1.0f / Mathf.Max(1f, atlasWidth);
                float texelY = 1.0f / Mathf.Max(1f, atlasHeight);

                float totalSum = 0f;
                float[] weights = new float[taps];

                float safeSigma = Mathf.Max(sigma, 1e-5f);
                for (int i = 0; i < taps; i++)
                {
                    float p = i - (taps - 1) * 0.5f;
                    weights[i] = (float)Math.Exp(-(p * p) / (2.0f * safeSigma * safeSigma));
                    totalSum += weights[i];
                }

                if (totalSum <= 1e-8f)
                {
                    float fallbackWeight = 1f / taps;
                    for (int i = 0; i < taps; i++)
                    {
                        outWeights[i] = fallbackWeight;
                        float p = i - (taps - 1) * 0.5f;
                        if (isHorizontal)
                        {
                            outOffsets[i] = new Vector4(p * texelX, 0f, 0f, 0f);
                        }
                        else
                        {
                            outOffsets[i] = new Vector4(0f, p * texelY, 0f, 0f);
                        }
                    }

                    return;
                }

                for (int i = 0; i < taps; i++)
                {
                    outWeights[i] = weights[i] / totalSum;

                    float p = i - (taps - 1) * 0.5f;
                    if (isHorizontal)
                    {
                        outOffsets[i] = new Vector4(p * texelX, 0f, 0f, 0f);
                    }
                    else
                    {
                        outOffsets[i] = new Vector4(0f, p * texelY, 0f, 0f);
                    }
                }
            }

            private static void ExecuteBloomAtlasPass(BloomAtlasPassData data, UnsafeGraphContext context)
            {
                UnsafeCommandBuffer cmd = context.cmd;
                CommandBuffer nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);

                nativeCmd.SetGlobalFloat(data.bloomThresholdId, data.bloomThreshold);
                nativeCmd.SetGlobalFloat(data.bloomIntensityId, data.bloomIntensity);
                nativeCmd.SetGlobalFloat(data.bloomRId, data.bloomR);
                nativeCmd.SetGlobalFloat(data.bloomGId, data.bloomG);
                nativeCmd.SetGlobalFloat(data.bloomBId, data.bloomB);
                nativeCmd.SetGlobalVectorArray(data.bloomUVMinMaxId, data.atlasUvMinMax);

                // Prefilter pass.
                cmd.SetRenderTarget(data.mipDown[0]);
                Blitter.BlitTexture(nativeCmd, data.source, FullscreenScaleBias, data.material, data.prefilterPass);

                // Downsample chain.
                for (int i = 1; i < data.mipDown.Length; i++)
                {
                    cmd.SetRenderTarget(data.mipDown[i]);
                    Blitter.BlitTexture(nativeCmd, data.mipDown[i - 1], FullscreenScaleBias, data.material, data.downsamplePass);
                }

                int blurAtlasVerticalPass = data.gaussianPass;
                int blurAtlasHorizontalPass = data.atlasBlurPass >= 0 ? data.atlasBlurPass : blurAtlasVerticalPass;

                // Vertical/first blur pass into Atlas1.
                cmd.SetRenderTarget(data.atlas1);
                nativeCmd.ClearRenderTarget(false, true, Color.clear);

                for (int i = 0; i < data.blurStageCount; i++)
                {
                    int sourceIndex = data.blurStartIndex + i;
                    nativeCmd.SetGlobalInt(data.bloomUVIndexId, i);
                    nativeCmd.SetGlobalInt(data.bloomKernelSizeId, data.kernelSizes[i]);
                    nativeCmd.SetGlobalFloatArray(data.bloomKernelId, data.kernels[i]);
                    nativeCmd.SetGlobalFloat(data.bloomLayerIntensityId, data.layerIntensities[i]);
                    nativeCmd.SetGlobalInt(data.gaussTapsId, data.kernelSizes[i]);
                    nativeCmd.SetGlobalVectorArray(data.gaussOffsetId, data.gaussOffsetsHorizontal[i]);
                    nativeCmd.SetGlobalFloatArray(data.gaussWeightsId, data.gaussWeightsHorizontal[i]);
                    nativeCmd.SetGlobalVector(data.blurScaleId, new Vector4(1f, 0f, 0f, 0f));

                    cmd.SetViewport(data.atlasViewports[i]);
                    Blitter.BlitTexture(nativeCmd, data.mipDown[sourceIndex], FullscreenScaleBias, data.material, blurAtlasVerticalPass);
                }

                // Horizontal/second blur pass into Atlas2.
                cmd.SetRenderTarget(data.atlas2);
                nativeCmd.ClearRenderTarget(false, true, Color.clear);

                for (int i = 0; i < data.blurStageCount; i++)
                {
                    Vector4 horizontalBlurScale = new Vector4(0f, 1f, 0f, 0f);

                    nativeCmd.SetGlobalInt(data.bloomUVIndexId, i);
                    nativeCmd.SetGlobalInt(data.bloomKernelSizeId, data.kernelSizes[i]);
                    nativeCmd.SetGlobalFloatArray(data.bloomKernelId, data.kernels[i]);
                    nativeCmd.SetGlobalFloat(data.bloomLayerIntensityId, data.layerIntensities[i]);
                    nativeCmd.SetGlobalInt(data.gaussTapsId, data.kernelSizes[i]);
                    nativeCmd.SetGlobalVectorArray(data.gaussOffsetId, data.gaussOffsetsVertical[i]);
                    nativeCmd.SetGlobalFloatArray(data.gaussWeightsId, data.gaussWeightsVertical[i]);
                    nativeCmd.SetGlobalVector(data.blurScaleId, horizontalBlurScale);

                    Vector4 uvMinMax = data.atlasUvMinMax[i];
                    Vector4 uvTransform = new Vector4(
                        uvMinMax.z - uvMinMax.x,
                        uvMinMax.w - uvMinMax.y,
                        uvMinMax.x,
                        uvMinMax.y);
                    data.atlasUvTransforms[i] = uvTransform;

                    float texelX = 1f / Mathf.Max(1, data.atlasWidth);
                    float texelY = 1f / Mathf.Max(1, data.atlasHeight);
                    Vector4 uvClamp = new Vector4(
                        uvMinMax.x + texelX,
                        uvMinMax.y + texelY,
                        uvMinMax.z - texelX,
                        uvMinMax.w - texelY);

                    if (uvClamp.x >= uvClamp.z || uvClamp.y >= uvClamp.w)
                    {
                        uvClamp = uvMinMax;
                    }

                    nativeCmd.SetGlobalVector(data.gaussianUVTransformId, uvTransform);
                    nativeCmd.SetGlobalVector(data.gaussianUVClampId, uvClamp);

                    cmd.SetViewport(data.atlasViewports[i]);
                    Blitter.BlitTexture(nativeCmd, data.atlas1, FullscreenScaleBias, data.material, blurAtlasHorizontalPass);
                }

                // Final atlas combine back to camera-sized destination.
                cmd.SetRenderTarget(data.bloomOutput);
                cmd.SetViewport(new Rect(0f, 0f, data.outputWidth, data.outputHeight));
                nativeCmd.SetGlobalVectorArray(data.bloomAtlasUVTransId, data.atlasUvTransforms);
                Blitter.BlitTexture(nativeCmd, data.atlas2, FullscreenScaleBias, data.material, data.atlasCombinePass);

            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Camera camera = cameraData.camera;
                int cameraId = camera != null ? camera.GetInstanceID() : -1;

                if (!TryApplyVolumeSettings())
                {
                    MarkBloomTextureUnavailable(cameraId);
                    Shader.SetGlobalTexture(_hsrBloomTexId, Texture2D.blackTexture);
                    return;
                }

                int prefilterPassIndex = TryGetPassByName(BloomPrefilterPass);
                int downsamplePassIndex = TryGetPassByName(DownsamplePass);
                int gaussianPassIndex = TryGetPassByName(GaussianPass);
                int atlasBlurPassIndex = TryGetPassByName(BlurAtlasPass);
                int atlasCombinePassIndex = TryGetPassByName(CombineAtlasPass);

                if (prefilterPassIndex < 0 || downsamplePassIndex < 0 || gaussianPassIndex < 0 || atlasCombinePassIndex < 0)
                {
                    MarkBloomTextureUnavailable(cameraId);
                    Shader.SetGlobalTexture(_hsrBloomTexId, Texture2D.blackTexture);
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    source = resourceData.cameraColor;
                }

                if (!source.IsValid())
                {
                    MarkBloomTextureUnavailable(cameraId);
                    Shader.SetGlobalTexture(_hsrBloomTexId, Texture2D.blackTexture);
                    return;
                }

                TextureDesc sourceDesc = source.GetDescriptor(renderGraph);

                RenderTextureDescriptor bloomStageDesc = cameraData.cameraTargetDescriptor;
                bloomStageDesc.msaaSamples = 1;
                bloomStageDesc.depthBufferBits = 0;
                bloomStageDesc.depthStencilFormat = GraphicsFormat.None;
                bloomStageDesc.graphicsFormat = _bloomStageFormat;
                bloomStageDesc.sRGB = GraphicsFormatUtility.IsSRGBFormat(_bloomStageFormat);
                bloomStageDesc.bindMS = false;
                bloomStageDesc.enableRandomWrite = false;
                bloomStageDesc.autoGenerateMips = false;
                bloomStageDesc.useMipMap = false;
                bloomStageDesc.mipCount = 1;

                RenderTextureDescriptor prefilterSourceDesc = bloomStageDesc;
                prefilterSourceDesc.width = sourceDesc.width;
                prefilterSourceDesc.height = sourceDesc.height;
                TextureHandle prefilterSource = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    prefilterSourceDesc,
                    "RPG_BloomPrefilterSource",
                    false,
                    FilterMode.Bilinear);

                renderGraph.AddBlitPass(source, prefilterSource, Vector2.one, Vector2.zero, passName: "RPG Bloom Prefilter Source Copy");

                TextureHandle[] mipDown = new TextureHandle[_mipDownCount];
                for (int i = 0; i < _mipDownCount; i++)
                {
                    RenderTextureDescriptor mipDesc = bloomStageDesc;
                    mipDesc.width = FixedBloomMipSizes[i].x;
                    mipDesc.height = FixedBloomMipSizes[i].y;
                    mipDown[i] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, mipDesc, $"RPG_BloomMipDown_{i}", false, FilterMode.Bilinear);
                }

                int blurStartIndex = Mathf.Clamp(_mipDownCount - _blurStageCount, 0, _mipDownCount - 1);
                int baseWidth = FixedBloomMipSizes[blurStartIndex].x;
                int baseHeight = FixedBloomMipSizes[blurStartIndex].y;

                RenderTextureDescriptor atlasDesc = bloomStageDesc;
                atlasDesc.width = BloomAtlasWidth;
                atlasDesc.height = BloomAtlasHeight;
                TextureHandle atlas1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, atlasDesc, "RPG_BloomAtlas1", false, FilterMode.Bilinear);

                TextureHandle atlas2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, atlasDesc, "RPG_BloomAtlas2", false, FilterMode.Bilinear);

                SetupAtlasLayout(baseWidth, baseHeight, atlasDesc.width, atlasDesc.height);
                BuildGaussianKernelData(atlasDesc.width, atlasDesc.height);

                RenderTextureDescriptor bloomOutputDesc = bloomStageDesc;
                bloomOutputDesc.width = BloomOutputWidth;
                bloomOutputDesc.height = BloomOutputHeight;
                TextureHandle bloomOutput = UniversalRenderer.CreateRenderGraphTexture(renderGraph, bloomOutputDesc, "RPG_BloomCombined", false, FilterMode.Bilinear);

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<BloomAtlasPassData>(RenderGraphName, out BloomAtlasPassData passData))
                {
                    passData.material = _material;
                    passData.source = prefilterSource;
                    passData.bloomOutput = bloomOutput;
                    passData.mipDown = mipDown;
                    passData.blurStartIndex = blurStartIndex;
                    passData.blurStageCount = _blurStageCount;
                    passData.atlas1 = atlas1;
                    passData.atlas2 = atlas2;
                    passData.atlasViewports = _atlasViewports;
                    passData.atlasUvMinMax = _atlasUvMinMax;
                    passData.outputWidth = bloomOutputDesc.width;
                    passData.outputHeight = bloomOutputDesc.height;

                    passData.prefilterPass = prefilterPassIndex;
                    passData.downsamplePass = downsamplePassIndex;
                    passData.gaussianPass = gaussianPassIndex;
                    passData.atlasBlurPass = atlasBlurPassIndex;
                    passData.atlasCombinePass = atlasCombinePassIndex;

                    passData.bloomThresholdId = _bloomThresholdId;
                    passData.bloomIntensityId = _bloomIntensityId;
                    passData.bloomRId = _bloomRId;
                    passData.bloomGId = _bloomGId;
                    passData.bloomBId = _bloomBId;
                    passData.bloomUVMinMaxId = _bloomUVMinMaxId;
                    passData.bloomUVIndexId = _bloomUVIndexId;
                    passData.bloomKernelSizeId = _bloomKernelSizeId;
                    passData.bloomKernelId = _bloomKernelId;
                    passData.bloomLayerIntensityId = _bloomLayerIntensityId;
                    passData.gaussTapsId = _gaussTapsId;
                    passData.gaussOffsetId = _gaussOffsetId;
                    passData.gaussWeightsId = _gaussWeightsId;
                    passData.blurScaleId = _blurScaleId;
                    passData.gaussianUVClampId = _gaussianUVClampId;
                    passData.gaussianUVTransformId = _gaussianUVTransformId;
                    passData.bloomAtlasUVTransId = _bloomAtlasUVTransId;
                    passData.atlasWidth = atlasDesc.width;
                    passData.atlasHeight = atlasDesc.height;

                    passData.kernelSizes = _kernelSizes;
                    passData.kernels = _kernels;
                    passData.gaussWeightsHorizontal = _gaussWeightsHorizontal;
                    passData.gaussOffsetsHorizontal = _gaussOffsetsHorizontal;
                    passData.gaussWeightsVertical = _gaussWeightsVertical;
                    passData.gaussOffsetsVertical = _gaussOffsetsVertical;
                    passData.layerIntensities = _layerIntensities;
                    passData.atlasUvTransforms = _atlasUvTransforms;
                    passData.bloomThreshold = _bloomThreshold;
                    passData.bloomIntensity = _bloomIntensity;
                    passData.bloomR = _bloomR;
                    passData.bloomG = _bloomG;
                    passData.bloomB = _bloomB;

                    builder.UseTexture(prefilterSource, AccessFlags.Read);
                    for (int i = 0; i < mipDown.Length; i++)
                    {
                        builder.UseTexture(mipDown[i], AccessFlags.ReadWrite);
                    }

                    builder.UseTexture(atlas1, AccessFlags.ReadWrite);
                    builder.UseTexture(atlas2, AccessFlags.ReadWrite);
                    builder.UseTexture(bloomOutput, AccessFlags.WriteAll);
                    builder.SetGlobalTextureAfterPass(bloomOutput, _hsrBloomTexId);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (BloomAtlasPassData data, UnsafeGraphContext context) => ExecuteBloomAtlasPass(data, context));
                }

                MarkBloomTextureAvailable(cameraId);
            }

        }
    }
}