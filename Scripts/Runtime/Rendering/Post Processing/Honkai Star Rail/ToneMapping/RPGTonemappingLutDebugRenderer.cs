using HoyoToon.Runtime.Rendering.PostProcessing.HSR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.ToneMapping
{
    /// <summary>
    /// Temporary debug feature to visualize the generated LUT2D texture in a screen corner.
    /// </summary>
    public class RPGTonemappingLutDebugRenderer : HsrPostProcessRendererFeature<RPGTonemappingLutDebugRenderer.RPGTonemappingLutDebugPass>
    {
        private enum OverlayCorner
        {
            TopLeft = 0,
            TopRight = 1,
            BottomLeft = 2,
            BottomRight = 3,
        }

        [SerializeField] private bool _enabled = false;
        [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRendering;
        [SerializeField] private OverlayCorner _corner = OverlayCorner.TopRight;
        [SerializeField, Range(0.05f, 1.0f)] private float _width = 0.35f;
        [SerializeField, Range(0.02f, 0.5f)] private float _height = 0.12f;
        [SerializeField, Range(0f, 0.2f)] private float _padding = 0.02f;
        [SerializeField, Range(0f, 1f)] private float _opacity = 1f;
        [SerializeField, Range(0f, 6f)] private float _borderPixels = 1f;
        [SerializeField] private Color _borderColor = Color.white;
        [SerializeField] private bool _flipLutY = false;

        protected override bool ShouldEnqueuePass(ref RenderingData renderingData, RPGTonemappingLutDebugPass renderPass)
        {
            return _enabled;
        }

        protected override void ConfigurePass(ref RenderingData renderingData, RPGTonemappingLutDebugPass renderPass)
        {
            renderPass.ConfigureOverlay(
                BuildRect(_corner, _width, _height, _padding),
                Mathf.Clamp01(_opacity),
                Mathf.Max(0f, _borderPixels),
                _borderColor,
                _flipLutY);
            renderPass.SetPassEvent(_renderPassEvent);
        }

        protected override RPGTonemappingLutDebugPass CreateRenderPass()
        {
            return new RPGTonemappingLutDebugPass();
        }

        private static Rect BuildRect(OverlayCorner corner, float width, float height, float padding)
        {
            float clampedWidth = Mathf.Clamp(width, 0.01f, 1f);
            float clampedHeight = Mathf.Clamp(height, 0.01f, 1f);
            float maxPaddingX = Mathf.Max(0f, 1f - clampedWidth);
            float maxPaddingY = Mathf.Max(0f, 1f - clampedHeight);
            float clampedPaddingX = Mathf.Clamp(padding, 0f, maxPaddingX);
            float clampedPaddingY = Mathf.Clamp(padding, 0f, maxPaddingY);

            float x = (corner == OverlayCorner.TopRight || corner == OverlayCorner.BottomRight)
                ? 1f - clampedWidth - clampedPaddingX
                : clampedPaddingX;
            float y = (corner == OverlayCorner.TopLeft || corner == OverlayCorner.TopRight)
                ? 1f - clampedHeight - clampedPaddingY
                : clampedPaddingY;

            return new Rect(x, y, clampedWidth, clampedHeight);
        }

        public sealed class RPGTonemappingLutDebugPass : HsrFullscreenMaterialRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Lut2DDebugOverlay";
            private const string PassName = "OverlayLut2D";
            private const string RenderGraphPassName = "RPG Tonemapping LUT Debug Overlay";

            private readonly int _lut2DTexId = Shader.PropertyToID("_Lut2DTex");
            private readonly int _overlayRectId = Shader.PropertyToID("_OverlayRect");
            private readonly int _overlayOpacityId = Shader.PropertyToID("_OverlayOpacity");
            private readonly int _overlayBorderPixelsId = Shader.PropertyToID("_OverlayBorderPixels");
            private readonly int _overlayBorderColorId = Shader.PropertyToID("_OverlayBorderColor");
            private readonly int _flipLutYId = Shader.PropertyToID("_OverlayFlipLutY");

            private int _passIndex = int.MinValue;
            private Rect _overlayRect;
            private float _overlayOpacity = 1f;
            private float _overlayBorderPixels = 1f;
            private Color _overlayBorderColor = Color.white;
            private bool _flipLutY;

            public RPGTonemappingLutDebugPass()
                : base(ShaderName)
            {
                renderPassEvent = RenderPassEvent.AfterRendering;
            }

            public void SetPassEvent(RenderPassEvent passEvent)
            {
                renderPassEvent = passEvent;
            }

            public void ConfigureOverlay(Rect overlayRect, float opacity, float borderPixels, Color borderColor, bool flipLutY)
            {
                _overlayRect = overlayRect;
                _overlayOpacity = opacity;
                _overlayBorderPixels = borderPixels;
                _overlayBorderColor = borderColor;
                _flipLutY = flipLutY;
            }

            private int GetPassIndex()
            {
                return FindPass(
                    ref _passIndex,
                    PassName,
                    $"{nameof(RPGTonemappingLutDebugRenderer)}: shader pass '{PassName}' was not found on '{ShaderName}'.",
                    MissingShaderPassLogLevel.Warning);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (PassMaterial == null)
                {
                    return;
                }

                int passIndex = GetPassIndex();
                if (passIndex < 0)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                TextureHandle source;
                TextureHandle destination;
                bool writeBackToCameraColor;

                if (resourceData.isActiveTargetBackBuffer)
                {
                    // When active target is backbuffer, write directly to it; writing only
                    // cameraColor here would not be presented anymore this frame.
                    source = resourceData.afterPostProcessColor;
                    if (!source.IsValid())
                        source = resourceData.cameraColor;

                    destination = resourceData.activeColorTexture;
                    writeBackToCameraColor = false;
                }
                else
                {
                    source = resourceData.activeColorTexture;
                    if (!source.IsValid())
                        source = resourceData.cameraColor;

                    if (!source.IsValid())
                        return;

                    destination = CreateColorDestination(renderGraph, source, "CameraColor-RPGLutDebugOverlay");
                    writeBackToCameraColor = true;
                }

                if (!source.IsValid() || !destination.IsValid())
                {
                    return;
                }

                PassMaterial.SetVector(_overlayRectId, new Vector4(_overlayRect.x, _overlayRect.y, _overlayRect.width, _overlayRect.height));
                PassMaterial.SetFloat(_overlayOpacityId, _overlayOpacity);
                PassMaterial.SetFloat(_overlayBorderPixelsId, _overlayBorderPixels);
                PassMaterial.SetVector(_overlayBorderColorId, _overlayBorderColor);
                PassMaterial.SetFloat(_flipLutYId, _flipLutY ? 1f : 0f);

                RenderGraphUtils.BlitMaterialParameters blitParameters = CreateBlitParameters(source, destination, passIndex);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           blitParameters,
                           passName: RenderGraphPassName,
                           returnBuilder: true))
                {
                    builder.UseGlobalTexture(_lut2DTexId);
                }

                if (writeBackToCameraColor)
                {
                    resourceData.cameraColor = destination;
                }
            }
        }
    }
}
