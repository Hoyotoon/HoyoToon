using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace HoyoToon.PostProcessing
{
    [Serializable]
    [PostProcess(typeof(HonkaiImpactRenderer), PostProcessEvent.AfterStack, "HoyoToon/HonkaiImpactBloom")]
    public sealed class HonkaiImpactBloom : PostProcessEffectSettings
    {
        [Range(0f, 1f)]
        public FloatParameter postProcessingRatio = new() { value = 1f };
        public FloatParameter exposure = new() { value = 1f };
        public FloatParameter contrast = new() { value = 1f };
        public ParameterOverride<BrightPassSettings> brightPassSettings = new()
        {
            overrideState = true,
            value = new BrightPassSettings(bloomIntensity: 0.75f, bloomThreshold: 0.65f, bloomScale: 1.06f, enableLum: false)
        };

        public ParameterOverride<GaussianCoeffs> gaussianLayerSettings = new()
        {
            overrideState = true,
            value = new GaussianCoeffs(level0: 0.18f, level1: 0.18f, level2: 0.3f, level3: 0.3f)
        };
    }
    [Serializable]
    public class BrightPassSettings
    {
        public float BloomIntensity = 0.75f;
        public float BloomThreshold = 0.65f;
        public float BloomScale = 1.06f;
        public bool EnableLuminance = false;

        public BrightPassSettings() { }

        public BrightPassSettings(float bloomIntensity, float bloomThreshold, float bloomScale, bool enableLum)
        {
            this.BloomIntensity = bloomIntensity;
            this.BloomScale = bloomScale;
            this.BloomThreshold = bloomThreshold;
            this.EnableLuminance = enableLum;
        }
    }

    [Serializable]
    public class GaussianCoeffs
    {
        public float Level0 = 0.18f;
        public float Level1 = 0.18f;
        public float Level2 = 0.3f;
        public float Level3 = 0.3f;

        public GaussianCoeffs() { }

        public GaussianCoeffs(float level0, float level1, float level2, float level3)
        {
            this.Level0 = level0;
            this.Level1 = level1;
            this.Level2 = level2;
            this.Level3 = level3;
        }
    }

    public sealed class HonkaiImpactRenderer : PostProcessEffectRenderer<HonkaiImpactBloom>
    {
        static int HASH_PP_RATIO;
        static int HASH_BIGHT_PASS_TEX;
        static int HASH_DOWNSAMPLE_TEX;
        static int HASH_COEFF;
        static int HASH_CONSTRAST;
        static int HASH_EXPOSURE;
        static int HASH_LUM_ENABLE;
        static int HASH_BLUR_DOWN_1;
        static int HASH_BLUR_DOWN_2;
        static int HASH_BLUR_DOWN_3;
        static int HASH_BLUR_TEMP_TEX_0;
        static int HASH_BLUR_TEMP_TEX_1;
        static int HASH_BLUR_TEMP_TEX_2;
        static int HASH_BLUR_TEMP_TEX_3;
        static int HASH_MAIN_TEX_0;
        static int HASH_MAIN_TEX_1;
        static int HASH_MAIN_TEX_2;
        static int HASH_MAIN_TEX_3;
        static int HASH_GAUSSCOMP_TEX;
        static int HASH_SCALER_LOWER_CASE;
        static int HASH_SCALER_UPPER_CASE;
        static int HASH_THRESHOLD;
        public override void Render(PostProcessRenderContext context)
        {
            // get the shaders and their material sheets. There are 6 so far for hi3:
            var downsample4XSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/DownSample4X"));
            var brightPassSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/BrightPassEX"));
            var downsampleSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/DownSample"));
            var multiGaussSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/MultipleGaussPassFilterHQ"));
            var gaussCompSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/GaussCompositionEx"));
            var glareCompSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/HonkaiImpact/GlareCompositionEx"));

            // the starting resolutions, avoid updating these values so theyre maintained.
            int width = context.width;
            int height = context.height;

            float aspectRatio = (float)context.width;
            aspectRatio /= (float)context.height;

            // INIT ALL HASHES  
            GetPropertyHashes();

            // srgb holder 
            int HASH_SRGB = Shader.PropertyToID("_SRGBHolder");

            // get temporary rts for holding things in
            context.command.GetTemporaryRT(HASH_SRGB, width, height, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_SRGB);
            context.command.GetTemporaryRT(HASH_DOWNSAMPLE_TEX, width / 4, height / 4, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BIGHT_PASS_TEX, 512, 512, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_GAUSSCOMP_TEX, 512, 512, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_MAIN_TEX_0, 512, 512, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_TEMP_TEX_0, 512, 512, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_MAIN_TEX_1, 256, 256, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_TEMP_TEX_1, 256, 256, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_DOWN_1, 256, 256, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_MAIN_TEX_2, 128, 128, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_TEMP_TEX_2, 128, 128, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_DOWN_2, 128, 128, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_MAIN_TEX_3, 64, 64, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_TEMP_TEX_3, 64, 64, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
            context.command.GetTemporaryRT(HASH_BLUR_DOWN_3, 64, 64, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);

            // --------------------------------------
            // attemnpt to force into srgb 
            context.command.BlitFullscreenTriangle(context.source, HASH_SRGB);
            // downsample at 1/4 res
            context.command.BlitFullscreenTriangle(context.source, HASH_DOWNSAMPLE_TEX, downsample4XSheet, 0);

            // send values for the bright pass
            brightPassSheet.properties.SetFloat(HASH_THRESHOLD, settings.brightPassSettings.value.BloomThreshold);
            brightPassSheet.properties.SetFloat(HASH_SCALER_UPPER_CASE, settings.brightPassSettings.value.BloomScale);
            float lumCheck = 0;
            if (settings.brightPassSettings.value.EnableLuminance == true)
            {
                lumCheck = 1;
            }
            brightPassSheet.properties.SetFloat(HASH_LUM_ENABLE, lumCheck);

            context.command.BlitFullscreenTriangle(HASH_DOWNSAMPLE_TEX, HASH_BIGHT_PASS_TEX, brightPassSheet, 0);

            // // level 0 blur
            Blur(HASH_BIGHT_PASS_TEX, HASH_MAIN_TEX_0, HASH_BLUR_TEMP_TEX_0, multiGaussSheet, context, aspectRatio, 0);
            context.command.SetGlobalTexture(HASH_MAIN_TEX_0, HASH_MAIN_TEX_0);
            // level 1 blur
            context.command.BlitFullscreenTriangle(HASH_MAIN_TEX_0, HASH_BLUR_DOWN_1, downsampleSheet, 0);
            Blur(HASH_BLUR_DOWN_1, HASH_MAIN_TEX_1, HASH_BLUR_TEMP_TEX_1, multiGaussSheet, context, aspectRatio, 1);
            context.command.SetGlobalTexture(HASH_MAIN_TEX_1, HASH_MAIN_TEX_1);
            // level 2 blur
            context.command.BlitFullscreenTriangle(HASH_MAIN_TEX_1, HASH_BLUR_DOWN_2, downsampleSheet, 0);
            Blur(HASH_BLUR_DOWN_2, HASH_MAIN_TEX_2, HASH_BLUR_TEMP_TEX_2, multiGaussSheet, context, aspectRatio, 2);
            context.command.SetGlobalTexture(HASH_MAIN_TEX_2, HASH_MAIN_TEX_2);
            // level 3 blur
            context.command.BlitFullscreenTriangle(HASH_MAIN_TEX_2, HASH_BLUR_DOWN_3, downsampleSheet, 0);
            Blur(HASH_BLUR_DOWN_3, HASH_MAIN_TEX_3, HASH_BLUR_TEMP_TEX_3, multiGaussSheet, context, aspectRatio, 3);
            context.command.SetGlobalTexture(HASH_MAIN_TEX_3, HASH_MAIN_TEX_3);

            // composite the 4 levels together
            gaussCompSheet.properties.SetVector(HASH_COEFF, new Vector4(settings.gaussianLayerSettings.value.Level0, settings.gaussianLayerSettings.value.Level1, settings.gaussianLayerSettings.value.Level2, settings.gaussianLayerSettings.value.Level3));
            context.command.BlitFullscreenTriangle(HASH_MAIN_TEX_0, HASH_GAUSSCOMP_TEX, gaussCompSheet, 0);

            context.command.SetGlobalTexture("_BloomTex", HASH_GAUSSCOMP_TEX);

            glareCompSheet.properties.SetFloat(HASH_CONSTRAST, settings.contrast.value);
            glareCompSheet.properties.SetFloat(HASH_EXPOSURE, settings.exposure.value);
            glareCompSheet.properties.SetFloat(HASH_PP_RATIO, settings.postProcessingRatio.value);
            glareCompSheet.properties.SetVector(HASH_COEFF, new Vector2(1.0f - settings.brightPassSettings.value.BloomIntensity, settings.brightPassSettings.value.BloomIntensity));
            context.command.BlitFullscreenTriangle(HASH_SRGB, context.destination, glareCompSheet, 0);

            // cleanup:
            context.command.ReleaseTemporaryRT(HASH_DOWNSAMPLE_TEX);
            context.command.ReleaseTemporaryRT(HASH_BIGHT_PASS_TEX);
            context.command.ReleaseTemporaryRT(HASH_MAIN_TEX_0);
            context.command.ReleaseTemporaryRT(HASH_BLUR_TEMP_TEX_0);
            context.command.ReleaseTemporaryRT(HASH_MAIN_TEX_1);
            context.command.ReleaseTemporaryRT(HASH_BLUR_TEMP_TEX_1);
            context.command.ReleaseTemporaryRT(HASH_BLUR_DOWN_1);
            context.command.ReleaseTemporaryRT(HASH_MAIN_TEX_2);
            context.command.ReleaseTemporaryRT(HASH_BLUR_TEMP_TEX_2);
            context.command.ReleaseTemporaryRT(HASH_BLUR_DOWN_2);
            context.command.ReleaseTemporaryRT(HASH_MAIN_TEX_3);
            context.command.ReleaseTemporaryRT(HASH_BLUR_TEMP_TEX_3);
            context.command.ReleaseTemporaryRT(HASH_BLUR_DOWN_3);
            context.command.ReleaseTemporaryRT(HASH_GAUSSCOMP_TEX);
        }

        private static void GetPropertyHashes()
        {
            HASH_PP_RATIO = Shader.PropertyToID("_PostProcessRatio");
            HASH_BIGHT_PASS_TEX = Shader.PropertyToID("_BrightPass");
            HASH_DOWNSAMPLE_TEX = Shader.PropertyToID("_DownSampled");
            HASH_THRESHOLD = Shader.PropertyToID("_Threshhold");
            HASH_SCALER_UPPER_CASE = Shader.PropertyToID("_Scaler");
            HASH_SCALER_LOWER_CASE = Shader.PropertyToID("_scaler");
            HASH_MAIN_TEX_0 = Shader.PropertyToID("_MainTex0");
            HASH_MAIN_TEX_1 = Shader.PropertyToID("_MainTex1");
            HASH_MAIN_TEX_2 = Shader.PropertyToID("_MainTex2");
            HASH_MAIN_TEX_3 = Shader.PropertyToID("_MainTex3");
            HASH_BLUR_TEMP_TEX_0 = Shader.PropertyToID("_Temp0");
            HASH_BLUR_TEMP_TEX_1 = Shader.PropertyToID("_Temp1");
            HASH_BLUR_TEMP_TEX_2 = Shader.PropertyToID("_Temp3");
            HASH_BLUR_TEMP_TEX_3 = Shader.PropertyToID("_Temp4");
            HASH_BLUR_DOWN_1 = Shader.PropertyToID("_DownTemp1");
            HASH_BLUR_DOWN_2 = Shader.PropertyToID("_DownTemp2");
            HASH_BLUR_DOWN_3 = Shader.PropertyToID("_DownTemp3");
            HASH_LUM_ENABLE = Shader.PropertyToID("_EnableLum");
            HASH_COEFF = Shader.PropertyToID("coeff");
            HASH_EXPOSURE = Shader.PropertyToID("exposure");
            HASH_CONSTRAST = Shader.PropertyToID("constrast");
        }
        public void Blur(int source, int destination, int temp, PropertySheet sheet, PostProcessRenderContext context, float aspectRatio, int level)
        {
            sheet.properties.SetVector(HASH_SCALER_LOWER_CASE, new Vector2(((1f / aspectRatio) <= 1f) ? (1f / aspectRatio) : 1f, 0f));
            context.command.BlitFullscreenTriangle(source, temp, sheet, level);
            sheet.properties.SetVector(HASH_SCALER_LOWER_CASE, new Vector2(0f, ((1f / aspectRatio) <= 1f) ? (1f / aspectRatio) : 1f));
            context.command.BlitFullscreenTriangle(temp, destination, sheet, level);
        }
    }
}