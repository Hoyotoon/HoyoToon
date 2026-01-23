using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace HoyoToon.PostProcessing
{

    [Serializable]
    [PostProcess(typeof(ZenlessBloomRenderer), PostProcessEvent.BeforeStack, "HoyoToon/ZenlessBloom")]
    public sealed class ZenlessBloom : PostProcessEffectSettings
    {
        public ParameterOverride<NapBloomSettings> bloomSettings = new()
        {
            overrideState = true,
            value = new NapBloomSettings(threshold: 0.3f, intensity: 0.3f, scaler: 1.3f)
        };
        public ParameterOverride<NapBloomWeights> bloomWeights = new()
        {
            overrideState = true,
            value = new NapBloomWeights(stage1: 0.3f, stage2: 0.3f, stage3: 0.26f, stage4: 0.15f)
        };

        public ParameterOverride<NapCharaLutSettings> charaLutSettings = new()
        {
            overrideState = true,
            value = new NapCharaLutSettings(lut: null, slices: 31, factorX: 0.00098f, factorY: 0.03125f)
        };
    }

    [Serializable]
    public class NapBloomSettings
    {
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomScaler;

        public NapBloomSettings() { }

        public NapBloomSettings(float threshold, float intensity, float scaler)
        {
            this.BloomIntensity = intensity;
            this.BloomThreshold = threshold;
            this.BloomScaler = scaler;
        }
    }

    [Serializable]
    public class NapCharaLutSettings
    {
        public Texture2D lut;
        public int slices;
        public float factorX;
        public float factorY;

        public NapCharaLutSettings() { }
        public NapCharaLutSettings(Texture2D lut, int slices, float factorX, float factorY)
        {
            this.lut = lut;
            this.slices = slices;
            this.factorX = factorX;
            this.factorY = factorY;
        }
    }

    [Serializable]
    public class NapBloomWeights
    {
        public float stage1;
        public float stage2;
        public float stage3;
        public float stage4;

        public NapBloomWeights() { }

        public NapBloomWeights(float stage1, float stage2, float stage3, float stage4)
        {
            this.stage1 = stage1;
            this.stage2 = stage2;
            this.stage3 = stage3;
            this.stage4 = stage4;
        }
    }

    public sealed class ZenlessBloomRenderer : PostProcessEffectRenderer<ZenlessBloom>
    {
        static int HASH_TEMP_SCREEN;
        static int HASH_BLOOM_PARAM;
        static int HASH_PREFILTER;
        static int HASH_DOWNTMP_A;
        static int HASH_DOWNTMP_B;
        static int HASH_ATLAS_TMP;
        static int HASH_ATLAS;
        static int HASH_UVTRANS;
        static int HASH_UVSRC;
        static int HASH_NAP_SCALER;
        static int HASH_MAIN_0;
        static int HASH_NAP_ATLAS;
        static int HASH_NAP_BLOOM;
        static int HASH_NAP_ATLASTMP;
        static int HASH_UVSRC_2;
        static int HASH_UVSRC_3;
        static int HASH_WEIGHTS;
        static int HASH_CHARA_LUT;
        static int HASH_CHARA_PARAMS;

        public override void Render(PostProcessRenderContext context)
        {
            var deferredSheet = context.propertySheets.Get(Shader.Find("Hidden/ZenlessZoneZero/DeferredPost"));
            var bloomSheet = context.propertySheets.Get(Shader.Find("Hidden/ZenlessZoneZero/NapBloom"));
            var gaussSheet = context.propertySheets.Get(Shader.Find("Hidden/ZenlessZoneZero/MultipleGaussPassFilter"));
            var uberSheet = context.propertySheets.Get(Shader.Find("Hidden/ZenlessZoneZero/UberPost"));

            context.command.BeginSample("HoyoToon Zenless Zone Zero Bloom");

            // get the starting resolution :
            int startWidth = context.width;
            int startHeight = context.height;

            float aspectRatio = (float)context.width;
            aspectRatio /= (float)context.height;


            GetPropertyHashes();

            int setHeight = 192;
            int setWidth = 341;

            // before anything apply the tonemapping because for some reason they do that...
            // well, its a character LUT for a reason
            deferredSheet.properties.SetVector(HASH_CHARA_PARAMS, new(settings.charaLutSettings.value.factorX, settings.charaLutSettings.value.factorY, settings.charaLutSettings.value.slices, 0f));
            deferredSheet.properties.SetTexture(HASH_CHARA_LUT, settings.charaLutSettings.value.lut);
            context.command.SetGlobalVector(HASH_CHARA_PARAMS, new(settings.charaLutSettings.value.factorX, settings.charaLutSettings.value.factorY, settings.charaLutSettings.value.slices, 0f));
            context.command.SetGlobalTexture(HASH_CHARA_LUT, settings.charaLutSettings.value.lut);
            context.command.GetTemporaryRT(HASH_TEMP_SCREEN, startWidth, startHeight, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.BlitFullscreenTriangle(context.source, HASH_TEMP_SCREEN, deferredSheet, 0);



            context.command.GetTemporaryRT(HASH_PREFILTER, setWidth, setHeight, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNTMP_A, setWidth, setHeight, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNTMP_B, setWidth, setHeight, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_ATLAS_TMP, 151, 161, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_ATLAS, 151, 161, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_NAP_ATLASTMP, 726, 409, 0, FilterMode.Bilinear, context.sourceFormat);

            SetBloomSettings(bloomSheet);

            // downscaling
            bloomSheet.properties.SetVector(HASH_UVTRANS, new Vector4(1f, 1f, 0f, 0f));
            bloomSheet.properties.SetVector(HASH_UVSRC, new Vector4(1f, 1f, 0f, 0f));
            context.command.BlitFullscreenTriangle(HASH_TEMP_SCREEN, HASH_PREFILTER, bloomSheet, 2);

            // first set of blurring: 
            gaussSheet.properties.SetVector(HASH_UVTRANS, new Vector4(1f, 1f, 0f, 0f));
            gaussSheet.properties.SetVector(HASH_UVSRC, new Vector4(1f, 1f, 0f, 0f));
            gaussSheet.properties.SetVector(HASH_NAP_SCALER, new(1f / setWidth, 0f, 0f, 0f));
            context.command.BlitFullscreenTriangle(HASH_PREFILTER, HASH_DOWNTMP_A, gaussSheet, 0);
            gaussSheet.properties.SetVector(HASH_NAP_SCALER, new(0f, 1f / setHeight, 0f, 0f));
            context.command.BlitFullscreenTriangle(HASH_DOWNTMP_A, HASH_DOWNTMP_B, gaussSheet, 0);

            // create atlas 
            // similar to genshins so we'll store the mip contants ahead of time: 
            Vector4 tMip1 = new Vector4(1.00f, 0.52795f, 0.00f, -0.47205f);
            Vector4 tMip2 = new Vector4(0.5894f, 0.31056f, -0.4106f, 0.3913f);
            Vector4 tMip3 = new Vector4(0.23841f, 0.12422f, -0.76159f, 0.85093f);

            Vector4 sMip1 = new Vector4(1.00f, 0.52795f, 0.00f, 0.47205f);
            Vector4 sMip2 = new Vector4(0.5894f, 0.31056f, 0.0f, 0.14907f);
            Vector4 sMip3 = new Vector4(0.23841f, 0.12422f, 0.0f, 0.01242f);

            context.command.BeginSample("Create Atlas");
            // first, downsample all the faces and assemble:    
            bloomSheet.properties.SetVector(HASH_UVTRANS, tMip1);
            context.command.BlitFullscreenTriangle(HASH_DOWNTMP_B, HASH_ATLAS, bloomSheet, 0, false);
            bloomSheet.properties.SetVector(HASH_UVTRANS, tMip2);
            context.command.BlitFullscreenTriangle(HASH_DOWNTMP_B, HASH_ATLAS, bloomSheet, 0, false);
            bloomSheet.properties.SetVector(HASH_UVTRANS, tMip3);
            context.command.BlitFullscreenTriangle(HASH_DOWNTMP_B, HASH_ATLAS, bloomSheet, 0, false);
            context.command.EndSample("Create Atlas");

            context.command.BeginSample("Horizontal Atlas");
            // horizontal blur: 
            gaussSheet.properties.SetVector(HASH_NAP_SCALER, new(1f / 151, 0f, 0f, 0f));

            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip1);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip1);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLAS_TMP, gaussSheet, 0, false);
            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip2);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip2);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLAS_TMP, gaussSheet, 2, false);
            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip3);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip3);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_ATLAS_TMP, gaussSheet, 3, false);
            context.command.EndSample("Horizontal Atlas");
            context.command.BeginSample("Vertical Atlas");
            // vertical blur: 
            gaussSheet.properties.SetVector(HASH_NAP_SCALER, new(0f, 1f / 161f, 0f, 0f));

            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip1);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip1);
            context.command.BlitFullscreenTriangle(HASH_ATLAS_TMP, HASH_ATLAS, gaussSheet, 0, false);
            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip2);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip2);
            context.command.BlitFullscreenTriangle(HASH_ATLAS_TMP, HASH_ATLAS, gaussSheet, 2, false);
            gaussSheet.properties.SetVector(HASH_UVTRANS, tMip3);
            gaussSheet.properties.SetVector(HASH_UVSRC, sMip3);
            context.command.BlitFullscreenTriangle(HASH_ATLAS_TMP, HASH_ATLAS, gaussSheet, 3, false);
            context.command.EndSample("Vertical Atlas");

            // time to compile them togehter: 
            context.command.SetGlobalTexture(HASH_MAIN_0, HASH_DOWNTMP_B);
            context.command.SetGlobalTexture(HASH_NAP_ATLAS, HASH_ATLAS);

            gaussSheet.properties.SetVector(HASH_UVTRANS, new Vector4(1f, 1f, 0f, 0f));
            bloomSheet.properties.SetVector(HASH_UVTRANS, new Vector4(1f, 1f, 0f, 0f));


            bloomSheet.properties.SetVector(HASH_UVSRC, sMip1);
            bloomSheet.properties.SetVector(HASH_UVSRC_2, sMip2);
            bloomSheet.properties.SetVector(HASH_UVSRC_3, sMip3);
            bloomSheet.properties.SetVector(HASH_WEIGHTS, new(settings.bloomWeights.value.stage1, settings.bloomWeights.value.stage2, settings.bloomWeights.value.stage3, settings.bloomWeights.value.stage4));
            context.command.BlitFullscreenTriangle(HASH_DOWNTMP_B, HASH_NAP_ATLASTMP, bloomSheet, 3);

            context.command.SetGlobalTexture(HASH_NAP_BLOOM, HASH_NAP_ATLASTMP);
            uberSheet.properties.SetFloat("_BloomScaler", settings.bloomSettings.value.BloomScaler);
            context.command.BlitFullscreenTriangle(context.source, context.destination, uberSheet, 0);




            // context.command.BlitFullscreenTriangle(HASH_NAP_ATLASTMP, context.destination);

            context.command.ReleaseTemporaryRT(HASH_PREFILTER);
            context.command.ReleaseTemporaryRT(HASH_TEMP_SCREEN);
            context.command.ReleaseTemporaryRT(HASH_DOWNTMP_A);
            context.command.ReleaseTemporaryRT(HASH_DOWNTMP_B);
            context.command.ReleaseTemporaryRT(HASH_NAP_ATLASTMP);
            context.command.EndSample("HoyoToon Zenless Zone Zero Bloom");
        }

        public void SetBloomSettings(PropertySheet sheet)
        {
            sheet.properties.SetVector(HASH_BLOOM_PARAM, new(settings.bloomSettings.value.BloomThreshold, settings.bloomSettings.value.BloomIntensity, 0f, 0f));
        }

        public static void GetPropertyHashes()
        {
            HASH_TEMP_SCREEN = Shader.PropertyToID("_TEMPSCREENID");
            HASH_BLOOM_PARAM = Shader.PropertyToID("_NapBloomParams0");
            HASH_PREFILTER = Shader.PropertyToID("_Prefilter");
            HASH_DOWNTMP_A = Shader.PropertyToID("_DownScaleA");
            HASH_DOWNTMP_B = Shader.PropertyToID("_DownScaleB");
            HASH_UVTRANS = Shader.PropertyToID("_UVTransformTarget");
            HASH_UVSRC = Shader.PropertyToID("_UVTransformSource");
            HASH_NAP_SCALER = Shader.PropertyToID("_NapGaussScaler");
            HASH_ATLAS_TMP = Shader.PropertyToID("_AtlasID");
            HASH_ATLAS = Shader.PropertyToID("_Atlas");
            HASH_MAIN_0 = Shader.PropertyToID("_MainTex0");
            HASH_NAP_ATLASTMP = Shader.PropertyToID("_NapBloomAtlasTmp");
            HASH_NAP_ATLAS = Shader.PropertyToID("_NapBloomAtlas");
            HASH_NAP_BLOOM = Shader.PropertyToID("_NapBloomTex");
            HASH_UVSRC_2 = Shader.PropertyToID("_UVTransformSource2");
            HASH_UVSRC_3 = Shader.PropertyToID("_UVTransformSource3");
            HASH_WEIGHTS = Shader.PropertyToID("_BloomBlurComposeWeights");
            HASH_CHARA_LUT = Shader.PropertyToID("_CharaLut");
            HASH_CHARA_PARAMS = Shader.PropertyToID("_CharaLutParams");
        }
    }
}