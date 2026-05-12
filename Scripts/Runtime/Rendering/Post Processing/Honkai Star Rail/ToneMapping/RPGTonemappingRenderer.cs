using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.ToneMapping
{
    public class RPGTonemappingRenderer : HsrPostProcessRendererFeature<RPGTonemappingRenderer.RPGTonemappingPass>
    {
        private struct CameraFrameKey : IEquatable<CameraFrameKey>
        {
            public int Frame;
            public int CameraId;

            public bool Equals(CameraFrameKey other)
            {
                return Frame == other.Frame && CameraId == other.CameraId;
            }

            public override bool Equals(object obj)
            {
                return obj is CameraFrameKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Frame * 397) ^ CameraId;
                }
            }
        }

        private static CameraFrameKey s_LastGeneratedLutKey;
        private static bool s_HasGeneratedLutKey;

        internal static bool HasGeneratedLutForCamera(Camera camera)
        {
            return camera != null
                && s_HasGeneratedLutKey
                && s_LastGeneratedLutKey.Equals(CreateCameraFrameKey(camera));
        }

        private static CameraFrameKey CreateCameraFrameKey(Camera camera)
        {
            return new CameraFrameKey
            {
                Frame = Time.frameCount,
                CameraId = camera != null ? camera.GetInstanceID() : 0,
            };
        }

        private enum TonemappingTextureFormat
        {
            R16G16B16A16_SFloat,
            B10G11R11_UFloatPack32,
            R8G8B8A8_UNorm
        }

        [SerializeField]
        private TonemappingTextureFormat _tonemappingTextureFormat = TonemappingTextureFormat.R16G16B16A16_SFloat;

        protected override void ConfigurePass(ref RenderingData renderingData, RPGTonemappingPass renderPass)
        {
            GraphicsFormat cameraFormat = renderingData.cameraData.cameraTargetDescriptor.graphicsFormat;
            renderPass.SetLutFormat(ResolveTonemappingGraphicsFormat(_tonemappingTextureFormat, cameraFormat));
        }

        protected override bool ShouldEnqueuePass(ref RenderingData renderingData, RPGTonemappingPass renderPass)
        {
            // Reset generation state per renderer enqueue cycle so global LUT handles
            // are only considered valid for the current render-graph execution.
            s_HasGeneratedLutKey = false;

            RPGTonemapping settings = VolumeManager.instance.stack.GetComponent<RPGTonemapping>();
            return settings != null && settings.IsActive();
        }

        protected override RPGTonemappingPass CreateRenderPass()
        {
            return new RPGTonemappingPass();
        }

        private static GraphicsFormat ResolveTonemappingGraphicsFormat(TonemappingTextureFormat selectedFormat, GraphicsFormat cameraFormat)
        {
            GraphicsFormat preferred = selectedFormat switch
            {
                TonemappingTextureFormat.R16G16B16A16_SFloat => GraphicsFormat.R16G16B16A16_SFloat,
                TonemappingTextureFormat.B10G11R11_UFloatPack32 => GraphicsFormat.B10G11R11_UFloatPack32,
                TonemappingTextureFormat.R8G8B8A8_UNorm => GraphicsFormat.R8G8B8A8_UNorm,
                _ => GraphicsFormat.R16G16B16A16_SFloat,
            };

            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
            {
                return preferred;
            }

            if (SystemInfo.IsFormatSupported(cameraFormat, GraphicsFormatUsage.Render))
            {
                return cameraFormat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
            {
                return GraphicsFormat.R16G16B16A16_SFloat;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render))
            {
                return GraphicsFormat.B10G11R11_UFloatPack32;
            }

            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        public sealed class RPGTonemappingPass : HsrFullscreenMaterialRenderPass
        {
            private const string TonemappingShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Lut2DBaker";
            private const string TonemappingPassName = "BakeLUT2D";
            private const string RenderGraphName = "RPG Tonemapping Pass";
            private const int LutSize = 32;

            private static readonly Vector2 NeutralHueSatConXY = new Vector2(0f, 1f);

            private readonly HableCurve _hableCurve = new HableCurve();
            private int _tonemappingPassIndex = int.MinValue;
            private GraphicsFormat _lutGraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            private bool _loggedMissingSettings;

            private readonly int _generatedLutTextureId = Shader.PropertyToID("_Lut2DTex");
            private readonly int _levelHighToneId = Shader.PropertyToID("_LevelHighTone");
            private readonly int _levelShadowToneId = Shader.PropertyToID("_LevelShadowTone");
            private readonly int _levelColorId = Shader.PropertyToID("_LevelColor");
            private readonly int _colorSaturationShadowId = Shader.PropertyToID("_ColorSaturationShadow");
            private readonly int _colorContrastShadowId = Shader.PropertyToID("_ColorContrastShadow");
            private readonly int _colorGainShadowId = Shader.PropertyToID("_ColorGainShadow");
            private readonly int _colorSaturationMidtoneId = Shader.PropertyToID("_ColorSaturationMidtone");
            private readonly int _colorContrastMidtoneId = Shader.PropertyToID("_ColorContrastMidtone");
            private readonly int _colorGainMidtoneId = Shader.PropertyToID("_ColorGainMidtone");
            private readonly int _colorSaturationHighlightId = Shader.PropertyToID("_ColorSaturationHighlight");
            private readonly int _colorContrastHighlightId = Shader.PropertyToID("_ColorContrastHighlight");
            private readonly int _colorGainHighlightId = Shader.PropertyToID("_ColorGainHighlight");
            private readonly int _colorCorrectionShadowMaxId = Shader.PropertyToID("_ColorCorrectionShadowMax");
            private readonly int _colorCorrectionHighlightMinId = Shader.PropertyToID("_ColorCorrectionHighlightMin");
            private readonly int _lut2DParamsId = Shader.PropertyToID("_Lut2D_Params");
            private readonly int _hueSatConId = Shader.PropertyToID("_HueSatCon");
            private readonly int _customToneCurveId = Shader.PropertyToID("_CustomToneCurve");
            private readonly int _toeSegmentAId = Shader.PropertyToID("_ToeSegmentA");
            private readonly int _toeSegmentBId = Shader.PropertyToID("_ToeSegmentB");
            private readonly int _midSegmentAId = Shader.PropertyToID("_MidSegmentA");
            private readonly int _midSegmentBId = Shader.PropertyToID("_MidSegmentB");
            private readonly int _shoSegmentAId = Shader.PropertyToID("_ShoSegmentA");
            private readonly int _shoSegmentBId = Shader.PropertyToID("_ShoSegmentB");
            private readonly int _expandGamutId = Shader.PropertyToID("_ExpandGamut");
            private readonly int _hdrHeadroomId = Shader.PropertyToID("_HDRHeadroom");
            private readonly int _enableHdrTonemappingId = Shader.PropertyToID("_EnableHDRTonemapping");
            private readonly int _debugHdrOutputIntermediateId = Shader.PropertyToID("_DebugHDROutputIntermediate");
            private readonly int _forceDisableToneMappingId = Shader.PropertyToID("_ForceDisableToneMapping");

            public RPGTonemappingPass()
                : base(TonemappingShaderName)
            {
            }

            public void SetLutFormat(GraphicsFormat format)
            {
                _lutGraphicsFormat = format;
            }

            private int GetPassIndex()
            {
                return FindPass(
                    ref _tonemappingPassIndex,
                    TonemappingPassName,
                    $"{nameof(RPGTonemappingRenderer)}: shader pass '{TonemappingPassName}' was not found on '{TonemappingShaderName}'.",
                    MissingShaderPassLogLevel.Warning);
            }

            private struct TonemappingShaderUniforms
            {
                internal float levelHighTone;
                internal float levelShadowTone;
                internal Vector3 levelColor;
                internal Vector4 colorSaturationShadow;
                internal Vector4 colorContrastShadow;
                internal Vector4 colorGainShadow;
                internal Vector4 colorSaturationMidtone;
                internal Vector4 colorContrastMidtone;
                internal Vector4 colorGainMidtone;
                internal Vector4 colorSaturationHighlight;
                internal Vector4 colorContrastHighlight;
                internal Vector4 colorGainHighlight;
                internal float colorCorrectionShadowMax;
                internal float colorCorrectionHighlightMin;
                internal Vector4 lut2DParams;
                internal Vector3 hueSatCon;
                internal Vector4 customToneCurve;
                internal Vector4 toeSegmentA;
                internal Vector4 toeSegmentB;
                internal Vector4 midSegmentA;
                internal Vector4 midSegmentB;
                internal Vector4 shoSegmentA;
                internal Vector4 shoSegmentB;
                internal float expandGamut;
                internal float hdrHeadroom;
                internal float enableHdrTonemapping;
                internal float debugHdrOutputIntermediate;
                internal float forceDisableToneMapping;
            }

            private static Vector4 BuildLut2DParams(int lutSize)
            {
                float lutSizeFloat = Mathf.Max(2f, lutSize);
                return new Vector4(
                    lutSizeFloat,
                    0.5f / (lutSizeFloat * lutSizeFloat),
                    0.5f / lutSizeFloat,
                    lutSizeFloat / (lutSizeFloat - 1f));
            }

            private static Vector3 BuildHueSatCon(float blueCorrection)
            {
                return new Vector3(
                    NeutralHueSatConXY.x,
                    NeutralHueSatConXY.y,
                    Mathf.Clamp01(blueCorrection));
            }

            private static Vector4 CombineTrackBall(in Vector4 globalValue, in Vector4 localValue)
            {
                // The shader expects positive coefficients and uses alpha as an intensity multiplier.
                Vector4 combined = new Vector4(
                    Mathf.Max(globalValue.x * localValue.x, 1e-4f),
                    Mathf.Max(globalValue.y * localValue.y, 1e-4f),
                    Mathf.Max(globalValue.z * localValue.z, 1e-4f),
                    Mathf.Max(globalValue.w * localValue.w, 1e-4f));

                return combined;
            }


            private TonemappingShaderUniforms BuildTonemappingShaderUniforms(RPGTonemapping settings)
            {
                _hableCurve.Init(
                    settings.ToneCurveToeStrength.value,
                    settings.ToneCurveToeLength.value,
                    settings.ToneCurveShoulderStrength.value,
                    settings.ToneCurveShoulderLength.value,
                    settings.ToneCurveShoulderAngle.value,
                    settings.ToneCurveGamma.value);

                Vector4 saturationGlobal = settings.ColorSaturationGlobal.value;
                Vector4 contrastGlobal = settings.ColorContrastGlobal.value;
                Vector4 gainGlobal = settings.ColorGainGlobal.value;
                Color levelColor = settings.LevelColor.value.linear;

                return new TonemappingShaderUniforms
                {
                    levelHighTone = settings.LevelHighLightTone.value,
                    levelShadowTone = settings.LevelShadowTone.value,
                    levelColor = new Vector3(levelColor.r, levelColor.g, levelColor.b),
                    colorSaturationShadow = CombineTrackBall(saturationGlobal, settings.ColorSaturationShadow.value),
                    colorContrastShadow = CombineTrackBall(contrastGlobal, settings.ColorContrastShadow.value),
                    colorGainShadow = CombineTrackBall(gainGlobal, settings.ColorGainShadow.value),
                    colorSaturationMidtone = CombineTrackBall(saturationGlobal, settings.ColorSaturationMidtone.value),
                    colorContrastMidtone = CombineTrackBall(contrastGlobal, settings.ColorContrastMidtone.value),
                    colorGainMidtone = CombineTrackBall(gainGlobal, settings.ColorGainMidtone.value),
                    colorSaturationHighlight = CombineTrackBall(saturationGlobal, settings.ColorSaturationHighlight.value),
                    colorContrastHighlight = CombineTrackBall(contrastGlobal, settings.ColorContrastHighlight.value),
                    colorGainHighlight = CombineTrackBall(gainGlobal, settings.ColorGainHighlight.value),
                    colorCorrectionShadowMax = Mathf.Max(settings.ColorCorrectionShadowMax.value, 1e-4f),
                    colorCorrectionHighlightMin = Mathf.Clamp01(settings.ColorCorrectionHighlightMin.value),
                    lut2DParams = BuildLut2DParams(LutSize),
                    hueSatCon = BuildHueSatCon(settings.BlueCorrection.value),
                    customToneCurve = _hableCurve.uniforms.curve,
                    toeSegmentA = _hableCurve.uniforms.toeSegmentA,
                    toeSegmentB = _hableCurve.uniforms.toeSegmentB,
                    midSegmentA = _hableCurve.uniforms.midSegmentA,
                    midSegmentB = _hableCurve.uniforms.midSegmentB,
                    shoSegmentA = _hableCurve.uniforms.shoSegmentA,
                    shoSegmentB = _hableCurve.uniforms.shoSegmentB,
                    expandGamut = settings.ExpandGamut.value,
                    hdrHeadroom = settings.HDRHeadroom.value,
                    enableHdrTonemapping = settings.ForceDisableToneMapping.value ? 0f : 1f,
                    debugHdrOutputIntermediate = 0f,
                    forceDisableToneMapping = settings.ForceDisableToneMapping.value ? 1f : 0f,
                };
            }

            private bool TryApplyVolumeSettings()
            {
                if (PassMaterial == null)
                {
                    return false;
                }

                RPGTonemapping settings = VolumeManager.instance.stack.GetComponent<RPGTonemapping>();
                if (settings == null)
                {
                    if (!_loggedMissingSettings)
                    {
                        Debug.LogWarning($"{nameof(RPGTonemappingRenderer)}: no {nameof(RPGTonemapping)} found in the active volume stack.");
                        _loggedMissingSettings = true;
                    }

                    return false;
                }

                if (!settings.IsActive())
                {
                    return false;
                }

                _loggedMissingSettings = false;

                TonemappingShaderUniforms uniforms = BuildTonemappingShaderUniforms(settings);
                PassMaterial.SetFloat(_levelHighToneId, uniforms.levelHighTone);
                PassMaterial.SetFloat(_levelShadowToneId, uniforms.levelShadowTone);
                PassMaterial.SetVector(_levelColorId, uniforms.levelColor);
                PassMaterial.SetVector(_colorSaturationShadowId, uniforms.colorSaturationShadow);
                PassMaterial.SetVector(_colorContrastShadowId, uniforms.colorContrastShadow);
                PassMaterial.SetVector(_colorGainShadowId, uniforms.colorGainShadow);
                PassMaterial.SetVector(_colorSaturationMidtoneId, uniforms.colorSaturationMidtone);
                PassMaterial.SetVector(_colorContrastMidtoneId, uniforms.colorContrastMidtone);
                PassMaterial.SetVector(_colorGainMidtoneId, uniforms.colorGainMidtone);
                PassMaterial.SetVector(_colorSaturationHighlightId, uniforms.colorSaturationHighlight);
                PassMaterial.SetVector(_colorContrastHighlightId, uniforms.colorContrastHighlight);
                PassMaterial.SetVector(_colorGainHighlightId, uniforms.colorGainHighlight);
                PassMaterial.SetFloat(_colorCorrectionShadowMaxId, uniforms.colorCorrectionShadowMax);
                PassMaterial.SetFloat(_colorCorrectionHighlightMinId, uniforms.colorCorrectionHighlightMin);
                PassMaterial.SetVector(_lut2DParamsId, uniforms.lut2DParams);
                PassMaterial.SetVector(_hueSatConId, uniforms.hueSatCon);
                PassMaterial.SetVector(_customToneCurveId, uniforms.customToneCurve);
                PassMaterial.SetVector(_toeSegmentAId, uniforms.toeSegmentA);
                PassMaterial.SetVector(_toeSegmentBId, uniforms.toeSegmentB);
                PassMaterial.SetVector(_midSegmentAId, uniforms.midSegmentA);
                PassMaterial.SetVector(_midSegmentBId, uniforms.midSegmentB);
                PassMaterial.SetVector(_shoSegmentAId, uniforms.shoSegmentA);
                PassMaterial.SetVector(_shoSegmentBId, uniforms.shoSegmentB);
                PassMaterial.SetFloat(_expandGamutId, uniforms.expandGamut);
                PassMaterial.SetFloat(_hdrHeadroomId, uniforms.hdrHeadroom);
                PassMaterial.SetFloat(_enableHdrTonemappingId, uniforms.enableHdrTonemapping);
                PassMaterial.SetFloat(_debugHdrOutputIntermediateId, uniforms.debugHdrOutputIntermediate);
                PassMaterial.SetFloat(_forceDisableToneMappingId, uniforms.forceDisableToneMapping);

                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Camera camera = cameraData.camera;

                // Guard against duplicate RecordRenderGraph invocations for the same camera/frame.
                // Only one LUT bake should ever happen per camera in a frame.
                if (s_HasGeneratedLutKey && s_LastGeneratedLutKey.Equals(CreateCameraFrameKey(camera)))
                {
                    return;
                }

                if (!TryApplyVolumeSettings())
                {
                    return;
                }

                int passIndex = GetPassIndex();
                if (passIndex < 0)
                {
                    return;
                }

                if (!TryGetSourceTexture(frameData, allowCameraColorFallback: true, skipActiveTargetBackBuffer: false, out UniversalResourceData resourceData, out TextureHandle source))
                {
                    return;
                }

                TextureDesc lutDesc = renderGraph.GetTextureDesc(source);
                lutDesc.name = "HSR-Tonemapping-Lut2D";
                lutDesc.width = LutSize * LutSize;
                lutDesc.height = LutSize;
                lutDesc.clearBuffer = false;
                lutDesc.colorFormat = _lutGraphicsFormat;

                TextureHandle generatedLut = renderGraph.CreateTexture(lutDesc);

                s_LastGeneratedLutKey = CreateCameraFrameKey(camera);
                s_HasGeneratedLutKey = true;

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           CreateBlitParameters(source, generatedLut, passIndex),
                           passName: RenderGraphName,
                           returnBuilder: true))
                {
                    builder.SetGlobalTextureAfterPass(generatedLut, _generatedLutTextureId);
                }
            }
        }
    }
}
