using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace HoyoToon.PostProcessing
{

    [Serializable]
    [PostProcess(typeof(GenshinBloomRenderer), PostProcessEvent.BeforeStack, "HoyoToon/GenshinBloom")]
    public sealed class GenshinBloom : PostProcessEffectSettings
    {
        public ParameterOverride<PreFilterSettings> prefilterBloom = new()
        {
            overrideState = true,
            value = new PreFilterSettings(thresh: 0.72f, charaThresh: 0.72f, scalar: 2.5f)
        };

        public ParameterOverride<BloomBlurSettings> blurStageSettings = new()
        {
            overrideState = true,
            value = new BloomBlurSettings(scalar: 1.0f, weight1: 0.3f, weight2: 0.3f, weight3: 0.26f, weight4: 0.15f)
        };

        public ParameterOverride<BloomToneSettings> bloomToneSettings = new()
        {
            overrideState = true,
            value = new BloomToneSettings(enabled: true, intensity: 0.7f, exposure: 1.0f, gamma: 1.0f)
        };
    }

    [Serializable]
    public class PreFilterSettings
    {
        public float BloomThreshold;
        public float BloomCharaThreshold;
        public float BloomScalar;

        public PreFilterSettings() { }

        public PreFilterSettings(float thresh, float charaThresh, float scalar)
        {
            this.BloomThreshold = thresh;
            this.BloomCharaThreshold = charaThresh;
            this.BloomScalar = scalar;
        }
    }

    [Serializable]
    public class BloomToneSettings
    {
        public bool EnableTonemap = true;
        public float BloomIntensity = 0.7f;
        public float BloomExposure = 1.0f;
        public float BloomGamma = 0.7f;

        public BloomToneSettings()
        { }

        public BloomToneSettings(bool enabled, float intensity, float exposure, float gamma)
        {
            this.EnableTonemap = enabled;
            this.BloomIntensity = intensity;
            this.BloomExposure = exposure;
            this.BloomGamma = gamma;
        }

    }

    [Serializable]
    public class BloomBlurSettings
    {
        public float GaussianScalar = 1.0f;
        public float BloomWeight1 = 0.3f;
        public float BloomWeight2 = 0.3f;
        public float BloomWeight3 = 0.26f;
        public float BloomWeight4 = 0.15f;
        public BloomBlurSettings()
        { }

        public BloomBlurSettings(float scalar, float weight1, float weight2, float weight3, float weight4)
        {
            this.GaussianScalar = scalar;
            this.BloomWeight1 = weight1;
            this.BloomWeight2 = weight2;
            this.BloomWeight3 = weight3;
            this.BloomWeight4 = weight4;
        }
    }
    public sealed class GenshinBloomRenderer : PostProcessEffectRenderer<GenshinBloom>
    {

        static int HASH_MHYTHRESH;
        static int HASH_MHYTHRESHCHARA;
        static int HASH_MHYSCALER;
        static int HASH_BLOOMTMP;
        static int HASH_BLURTMP;
        static int HASH_ATLAS;
        static int HASH_ATLASTMP;
        static int HASH_UVTRGT;
        static int HASH_UVTSRC;
        static int HASH_MAIN0;
        static int HASH_MAIN1;
        static int HASH_SCALER;
        static int HASH_WEIGHTS;
        static int HASH_TRANS1;
        static int HASH_TRANS2;
        static int HASH_TRANS3;
        static int HASH_MHYBLOOM;
        static int HASH_MHYINTSTY;
        static int HASH_MHYEXPOSURE;
        static int HASH_MHYTONEMAP;
        static int HASH_MHYINPUTGAMMA;

        public override void Render(PostProcessRenderContext context)
        {
            var bloomSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/Genshin/Bloom"));
            var gaussianSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/Genshin/MultiPassGaussFilter"));
            var uberSheet = context.propertySheets.Get(Shader.Find("Hidden/HoyoToon/Genshin/PostProcessingUber"));

            GetHashes();

            // 1. Setup Resolutions (Quarter Res)
            int tw = context.width / 4;
            int th = context.height / 4;


            context.command.GetTemporaryRT(HASH_BLOOMTMP, tw, th, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_BLURTMP, tw, th, 0, FilterMode.Bilinear, context.sourceFormat);



            SetPrefilterSettings(bloomSheet);

            // Result is now 1/4 resolution
            context.command.BlitFullscreenTriangle(context.source, HASH_BLOOMTMP, bloomSheet, 0);

            // -----------------------------------------------------------------------
            // DRAW CALL 2: Vertical Blur (1/4 Res -> 1/4 Res)
            // -----------------------------------------------------------------------
            gaussianSheet.properties.SetVector(HASH_UVTSRC, new Vector4(1, 1, 0, 0));

            gaussianSheet.properties.SetVector(HASH_UVTRGT, new Vector4(1, 1, 0, 0));
            Vector2 blurStep = new Vector2((1 / tw) * settings.blurStageSettings.value.GaussianScalar, (1 / th) * settings.blurStageSettings.value.GaussianScalar);


            gaussianSheet.properties.SetVector(HASH_SCALER, new Vector2(blurStep.x, 0.0f));
            context.command.BlitFullscreenTriangle(HASH_BLOOMTMP, HASH_BLURTMP, gaussianSheet, 1);
            gaussianSheet.properties.SetVector(HASH_SCALER, new Vector2(0.0f, blurStep.y));
            context.command.BlitFullscreenTriangle(HASH_BLURTMP, HASH_BLOOMTMP, gaussianSheet, 1);

            context.command.Blit(HASH_BLOOMTMP, context.destination);

            context.command.SetGlobalTexture(HASH_MAIN0, HASH_BLURTMP);
            // 2. Setup the Atlas (152x158)
            context.command.GetTemporaryRT(HASH_ATLAS, 152, 158, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.ClearRenderTarget(true, true, Color.black);
            context.command.GetTemporaryRT(HASH_ATLASTMP, 152, 158, 0, FilterMode.Bilinear, context.sourceFormat);

            // Largest Mip Constants
            Vector4 tMip1 = new Vector4(1.00f, 0.53797f, 0.00f, -0.46203f);
            Vector4 sMip1 = new Vector4(1.00f, 0.53797f, 0.00f, 0.00f);

            // Medium Mip Constants
            Vector4 tMip2 = new Vector4(0.58553f, 0.31646f, -0.41447f, 0.40506f);
            Vector4 sMip2 = new Vector4(0.58553f, 0.31646f, 0.00f, 0.5443f);

            // Small Mip Constants
            Vector4 tMip3 = new Vector4(0.23684f, 0.12658f, -0.76316f, 0.86076f);
            Vector4 sMip3 = new Vector4(0.23684f, 0.12658f, 0.00f, 0.86709f);

            context.command.BeginSample("Create Atlas");
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip1); // Top Half example
            context.command.BlitFullscreenTriangle(HASH_BLOOMTMP, HASH_ATLAS, gaussianSheet, 0);

            // medium mip
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip2);
            context.command.BlitFullscreenTriangle(HASH_BLOOMTMP, HASH_ATLAS, gaussianSheet, 0);

            // small mip
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip3);
            context.command.BlitFullscreenTriangle(HASH_BLOOMTMP, HASH_ATLAS, gaussianSheet, 0);
            context.command.EndSample("Create Atlas");
            context.command.BeginSample("Horizontal Atlas Blur");
            gaussianSheet.properties.SetVector(HASH_SCALER, new Vector2(1f / 152f, 0.0f));


            // 1. Largest mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip1);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip1);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLASTMP, gaussianSheet, 2);

            // 2. Medium mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip2);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip2);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLASTMP, gaussianSheet, 2);

            // 3. Small mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip3);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip3);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLASTMP, gaussianSheet, 2);
            context.command.EndSample("Horizontal Atlas Blur");

            context.command.BeginSample("Vertical Atlas Blur");
            gaussianSheet.properties.SetVector(HASH_SCALER, new Vector2(0.0f, 1f / 158f));

            // largest mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip1);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip1);
            context.command.BlitFullscreenTriangle(HASH_ATLASTMP, HASH_ATLAS, gaussianSheet, 2);

            // medium mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip2);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip2);
            context.command.BlitFullscreenTriangle(HASH_ATLASTMP, HASH_ATLAS, gaussianSheet, 2);

            // small mip
            gaussianSheet.properties.SetVector(HASH_UVTSRC, sMip3);
            gaussianSheet.properties.SetVector(HASH_UVTRGT, tMip3);
            context.command.BlitFullscreenTriangle(HASH_ATLASTMP, HASH_ATLAS, gaussianSheet, 2);

            context.command.SetGlobalTexture(HASH_MAIN1, HASH_ATLAS);
            context.command.EndSample("Vertical Atlas Blur");

            Vector4 weights = new(settings.blurStageSettings.value.BloomWeight1, settings.blurStageSettings.value.BloomWeight2, settings.blurStageSettings.value.BloomWeight3, settings.blurStageSettings.value.BloomWeight4);

            bloomSheet.properties.SetVector(HASH_WEIGHTS, weights);
            bloomSheet.properties.SetVector(HASH_TRANS1, sMip1);
            bloomSheet.properties.SetVector(HASH_TRANS2, sMip2);
            bloomSheet.properties.SetVector(HASH_TRANS3, sMip3);
            context.command.BlitFullscreenTriangle(context.source, context.destination, bloomSheet, 1);

            context.command.GetTemporaryRT(HASH_MHYBLOOM, context.width, context.height, 0, FilterMode.Bilinear, context.sourceFormat);

            context.command.Blit(context.destination, HASH_MHYBLOOM);


            SetToneSettings(uberSheet);
            context.command.BlitFullscreenTriangle(context.source, context.destination, uberSheet, 0);

            context.command.ReleaseTemporaryRT(HASH_BLOOMTMP);
            context.command.ReleaseTemporaryRT(HASH_BLURTMP);
            context.command.ReleaseTemporaryRT(HASH_ATLAS);
            context.command.ReleaseTemporaryRT(HASH_ATLASTMP);
        }

        public void SetPrefilterSettings(PropertySheet sheet)
        {
            PreFilterSettings pre = settings.prefilterBloom.value;
            sheet.properties.SetFloat(HASH_MHYTHRESHCHARA, pre.BloomCharaThreshold);
            sheet.properties.SetFloat(HASH_MHYTHRESH, pre.BloomThreshold);
            sheet.properties.SetFloat(HASH_MHYSCALER, pre.BloomScalar);
        }

        public void SetToneSettings(PropertySheet sheet)
        {

            BloomToneSettings tone = settings.bloomToneSettings.value;

            float isToneMapped = 0;
            if (tone.EnableTonemap)
            {
                isToneMapped = 1;
            }

            sheet.properties.SetFloat(HASH_MHYINTSTY, tone.BloomIntensity);
            sheet.properties.SetFloat(HASH_MHYEXPOSURE, tone.BloomExposure);
            sheet.properties.SetFloat(HASH_MHYTONEMAP, isToneMapped);
            sheet.properties.SetFloat(HASH_MHYINPUTGAMMA, tone.BloomGamma);

        }

        public static void GetHashes()
        {
            HASH_MHYTHRESH = Shader.PropertyToID("_MHYBloomThreshold");
            HASH_MHYTHRESHCHARA = Shader.PropertyToID("_MHYBloomThresholdCharacter");
            HASH_MHYSCALER = Shader.PropertyToID("_MHYBloomScaler");
            HASH_BLOOMTMP = Shader.PropertyToID("_BloomTempTex");
            HASH_BLURTMP = Shader.PropertyToID("_BlurTempTex");
            HASH_ATLASTMP = Shader.PropertyToID("_AtlasBlurScratch");
            HASH_UVTSRC = Shader.PropertyToID("_UVTransformSource");
            HASH_UVTRGT = Shader.PropertyToID("_UVTransformTarget");
            HASH_SCALER = Shader.PropertyToID("_Scaler");
            HASH_ATLAS = Shader.PropertyToID("_BloomAtlas");
            HASH_WEIGHTS = Shader.PropertyToID("_MHYBloomBlurComposeWeights");
            HASH_TRANS1 = Shader.PropertyToID("_MHYBloomUVTransform1");
            HASH_TRANS2 = Shader.PropertyToID("_MHYBloomUVTransform2");
            HASH_TRANS3 = Shader.PropertyToID("_MHYBloomUVTransform3");
            HASH_MHYBLOOM = Shader.PropertyToID("_MHYBloomTex");
            HASH_MAIN0 = Shader.PropertyToID("_MainTex0");
            HASH_MAIN1 = Shader.PropertyToID("_MainTex1");
            HASH_MHYINTSTY = Shader.PropertyToID("_MHYBloomIntensity");
            HASH_MHYEXPOSURE = Shader.PropertyToID("_MHYBloomExpossure");
            HASH_MHYTONEMAP = Shader.PropertyToID("_MHYBloomTonemapping");
            HASH_MHYINPUTGAMMA = Shader.PropertyToID("_UserInputGamma");
        }
    }
}