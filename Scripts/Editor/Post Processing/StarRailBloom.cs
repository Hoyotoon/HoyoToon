using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace HoyoToon.PostProcessing
{

    [Serializable]
    [PostProcess(typeof(StarRailBloomRenderer), PostProcessEvent.BeforeStack, "HoyoToon/StarRailBloom")]
    public sealed class StarRailBloom : PostProcessEffectSettings
    {
        [Tooltip("These are the settings that control both prefilter stage and the bloom after its been composited ")]
        public ParameterOverride<BloomSettings> bloomSettings = new()
        {
            overrideState = true,
            value = new BloomSettings(bloomThreshold: 0.7f, bloomIntensity: 0.6f, bloomR: 0f, bloomG: 0f, bloomB: 0)
        };

        [Tooltip("Each Stage refers to the available levels of the bloom, 4 layers meaning 4 faces in the bloom atlas")]
        public ParameterOverride<AllBloomStage> allBloomStages = new()
        {
            overrideState = true,
            value = new AllBloomStage()
            {
                Stages = new List<BloomStage>
                {
                    // Stage 1
                    new(kernelSize: 14, kernelSigma: 0.004564f, layerIntensity: 1.0f), // stage 1
                    new(kernelSize: 14, kernelSigma: 0.004564f, layerIntensity: 1.0f), // stage 2
                    new(kernelSize: 14, kernelSigma: 0.004564f, layerIntensity: 1.0f), // stage 3
                    new(kernelSize: 14, kernelSigma: 0.004564f, layerIntensity: 1.0f)  // stage 4
                }
            }
        };
        [Tooltip("Vignette Settings:")]
        public ParameterOverride<VignetteSettings> vignette = new()
        {
            overrideState = true,
            value = new VignetteSettings(enabled: false, center: new Vector2(0.5f, 0.5f), intensity: 1f, smoothness: 1f, roundness: true, color: Color.black)
        };

        [Tooltip("Tone Mapping Settings:")]
        public ParameterOverride<ToneMapping> toneMapping = new()
        {
            overrideState = true,
            value = new ToneMapping(lut2D: null, lutSlices: 31, lutFactor: new Vector2(0.00098f, 0.03125f))
        };

    }

    [Serializable]
    public class BloomStage
    {
        public int KernelSize = 14;
        public float KernelSigma = 0.005f;
        public float LayerIntensity = 1.0f;

        public BloomStage() { }

        public BloomStage(int kernelSize, float kernelSigma, float layerIntensity)
        {
            this.KernelSize = kernelSize;
            this.KernelSigma = kernelSigma;
            this.LayerIntensity = layerIntensity;
        }
    }

    [Serializable]
    public class AllBloomStage
    {
        public List<BloomStage> Stages = new();

        public AllBloomStage() { }
    }

    [Serializable]
    public class BloomSettings
    {
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomR;
        public float BloomG;
        public float BloomB;

        public BloomSettings() { }

        public BloomSettings(float bloomThreshold, float bloomIntensity, float bloomR, float bloomG, float bloomB)
        {
            this.BloomThreshold = bloomThreshold;
            this.BloomIntensity = bloomIntensity;
            this.BloomR = bloomR;
            this.BloomG = bloomG;
            this.BloomB = bloomB;
        }
    }
    [Serializable]
    public class VignetteSettings
    {
        public bool EnableVignette;
        public Vector2 Center;
        public float Intensity;
        public float Smoothness;
        public bool Roundness;
        public Color Color;

        public VignetteSettings() { }

        public VignetteSettings(bool enabled, Vector2 center, float intensity, float smoothness, bool roundness, Color color)
        {
            this.EnableVignette = enabled;
            this.Center = center;
            this.Intensity = intensity;
            this.Smoothness = smoothness;
            this.Roundness = roundness;
            this.Color = color;

        }
    }
    [Serializable]
    public class ToneMapping
    {
        public Texture2D Lut2DTexture;
        public int LutSlices = 31;

        public Vector2 LutFactor = new Vector2(0.00098f, 0.03125f);

        public ToneMapping() { }

        public ToneMapping(Texture2D lut2D, int lutSlices, Vector2 lutFactor)
        {
            this.Lut2DTexture = lut2D;
            this.LutSlices = lutSlices;
            this.LutFactor = lutFactor;
        }
    }
    public sealed class StarRailBloomRenderer : PostProcessEffectRenderer<StarRailBloom>
    {
        // hashes: 
        static int HASH_PREFILTER_ID;
        static int HASH_DOWNSCALE_A;
        static int HASH_DOWNSCALE_B;
        static int HASH_DOWNSCALE_C;
        static int HASH_DOWNSCALE_D;
        static int HASH_TMP_A;
        static int HASH_TMP_B;
        static int HASH_TMP_C;
        static int HASH_TMP_D;
        static int HASH_ATLAS;
        static int HASH_ATLASTMP;
        static int HASH_BLOOM_THRESHOLD;
        static int HASH_BLOOM_R;
        static int HASH_BLOOM_G;
        static int HASH_BLOOM_B;
        static int HASH_BLOOM_INTENSITY;
        static int HASH_MAINTEX_1;
        static int HASH_LUT2D_TEX;
        static int HASH_LUTFACTOR;
        static int HASH_EFFECTENABLE;
        static int HASH_VIGPARAM1;
        static int HASH_VIGPARAM2;
        static int HASH_SCALER;
        static int HASH_GAUSSTAPS;
        static int HASH_GAUSS_OFFSET;
        static int HASH_GAUSS_WEIGHT;
        static int HASH_GAUSSLAYER;
        static int HASH_GAUSSTRANS;
        static int HASH_GAUSSCLAMP;
        static int HASH_BLOOMATLAS_UV;
        public override void Render(PostProcessRenderContext context)
        {
            var sheet = context.propertySheets.Get(Shader.Find("Hidden/StarRail/PostProcessingUber"));
            context.command.BeginSample("HoyoToon Star Rail Bloom");

            // get the starting resolution :
            int startWidth = context.width;
            int startHeight = context.height;

            float aspectRatio = (float)context.width;
            aspectRatio /= (float)context.height;

            GetPropertyHashes();

            // initialize the temporary RTs for downscaling the textures + the atlas pingpongs
            int halfWidth = startWidth / 2;
            int halfHeight = startHeight / 2;
            context.command.GetTemporaryRT(HASH_PREFILTER_ID, halfWidth, halfHeight, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNSCALE_A, 310, 174, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_TMP_A, 310, 174, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNSCALE_B, 155, 87, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_TMP_B, 155, 87, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNSCALE_C, 72, 40, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_TMP_C, 72, 40, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_DOWNSCALE_D, 36, 20, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_TMP_D, 36, 20, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_ATLAS, 466, 174, 0, FilterMode.Bilinear, context.sourceFormat);
            context.command.GetTemporaryRT(HASH_ATLASTMP, 466, 174, 0, FilterMode.Bilinear, context.sourceFormat);

            // -----------------------------------------------------------------------
            // Prefilter step:
            // -----------------------------------------------------------------------
            SetBloomSettings(sheet);

            context.command.BlitFullscreenTriangle(context.source, HASH_PREFILTER_ID, sheet, 0);

            // // downsample 4 times
            context.command.BlitFullscreenTriangle(HASH_PREFILTER_ID, HASH_DOWNSCALE_A, sheet, 1);
            context.command.BlitFullscreenTriangle(HASH_DOWNSCALE_A, HASH_DOWNSCALE_B, sheet, 1);
            context.command.BlitFullscreenTriangle(HASH_DOWNSCALE_B, HASH_DOWNSCALE_C, sheet, 1);
            context.command.BlitFullscreenTriangle(HASH_DOWNSCALE_C, HASH_DOWNSCALE_D, sheet, 1);

            Rect largeRect = new(0.00f, 0.00f, 310.00f, 174.00f);
            Rect biggerRect = new(311.00f, 0.00f, 155.00f, 87.00f);
            Rect mediumRect = new(311.00f, 88.00f, 72.00f, 40.00f);
            Rect smallerRect = new(384.00f, 88.00f, 36.00f, 20.00f);
            Vector2[] sizes = new Vector2[4] { new(310f, 174f), new(155f, 87f), new(72f, 40f), new(36f, 20f) };

            Rect[] atlasRects = { largeRect, biggerRect, mediumRect, smallerRect };
            int[] atlasFaces = { HASH_DOWNSCALE_A, HASH_DOWNSCALE_B, HASH_DOWNSCALE_C, HASH_DOWNSCALE_D };
            Vector4[] uv_clamps =
            {
                new(0.00107f, 0.00287f, 0.66416f, 0.99713f),
                new(0.66845f, 0.00287f, 0.99893f, 0.49713f),
                new(0.66845f, 0.50862f, 0.82082f, 0.73276f),
                new(0.82511f, 0.50862f, 0.90021f, 0.61782f)
            };

            Vector4[] uv_transform =
            {
                new(0.66524f, 1.0f, 0.0f, 0.0f),
                new(0.33262f, 0.5f, 0.66738f, 0f),
                new(0.15451f, 0.22989f, 0.66738f, 0.50575f),
                new(0.07725f, 0.11494f, 0.82403f, 0.50575f)
            };

            Vector4[] atlasUV =
            {
                new(0.66524f, 1.0f, 0.0f, 0.0f),
                new(0.33262f, 0.5f, 0.66738f, 0f),
                new(0.15451f, 0.22989f, 0.66738f, 0.50575f),
                new(0.07725f, 0.11494f, 0.82403f, 0.50575f)
            };

            context.command.BeginSample("Blur Atlas H");
            BlurAtlasH(atlasRects, atlasFaces, sizes, HASH_ATLASTMP, context, sheet, 2, aspectRatio);
            context.command.EndSample("Blur Atlas H");

            context.command.BeginSample("Blur Atlas V");
            context.command.SetViewport(new Rect(0f, 0f, 466f, 174f));
            BlurAtlasV(atlasRects, uv_clamps, uv_transform, HASH_ATLASTMP, HASH_ATLAS, context, sheet, 3, aspectRatio);
            context.command.EndSample("Blur Atlas V");

            context.command.SetViewport(new Rect(0, 0, context.width, context.height));

            // instead of generating a brand new tmp rt, reuse one that is the same size:
            sheet.properties.SetVectorArray(HASH_BLOOMATLAS_UV, atlasUV);
            context.command.BlitFullscreenTriangle(HASH_ATLAS, HASH_DOWNSCALE_A, sheet, 4);
            // set global texture: 
            context.command.SetGlobalTexture(HASH_MAINTEX_1, HASH_DOWNSCALE_A);

            // tonemapping: 
            ToneMapping toneMapper = settings.toneMapping.value;
            var lutTex = toneMapper.Lut2DTexture != null
                ? toneMapper.Lut2DTexture
                : RuntimeUtilities.blackTexture;

            sheet.properties.SetTexture(HASH_LUT2D_TEX, lutTex);
            sheet.properties.SetVector(HASH_LUTFACTOR, new Vector3(toneMapper.LutFactor.x, toneMapper.LutFactor.y, toneMapper.LutSlices));

            //  vignette 
            VignetteSettings vignette = settings.vignette.value;
            float tmpVig = 0f;
            if (vignette.EnableVignette == true)
            {
                tmpVig = 1f;
            }
            sheet.properties.SetFloat(HASH_EFFECTENABLE, tmpVig);
            tmpVig = 0f;
            if (vignette.Roundness == true)
            {
                tmpVig = 1f;
            }
            sheet.properties.SetVector(HASH_VIGPARAM1, new(vignette.Color.r, vignette.Color.g, vignette.Color.b, vignette.Intensity));
            sheet.properties.SetVector(HASH_VIGPARAM2, new(vignette.Center.x, vignette.Center.y, tmpVig, vignette.Smoothness));

            // output to screen for debugging, this is not the next standard step
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 5);

            // cleanup
            context.command.ReleaseTemporaryRT(HASH_PREFILTER_ID);
            context.command.ReleaseTemporaryRT(HASH_DOWNSCALE_A);
            context.command.ReleaseTemporaryRT(HASH_DOWNSCALE_B);
            context.command.ReleaseTemporaryRT(HASH_DOWNSCALE_C);
            context.command.ReleaseTemporaryRT(HASH_DOWNSCALE_D);
            context.command.ReleaseTemporaryRT(HASH_TMP_A);
            context.command.ReleaseTemporaryRT(HASH_TMP_B);
            context.command.ReleaseTemporaryRT(HASH_TMP_C);
            context.command.ReleaseTemporaryRT(HASH_TMP_D);
            context.command.ReleaseTemporaryRT(HASH_ATLAS);
            context.command.ReleaseTemporaryRT(HASH_ATLASTMP);
            context.command.EndSample("HoyoToon Star Rail Bloom");
        }

        public void BlurAtlasH(Rect[] r, int[] renderers, Vector2[] sizes, int dest, PostProcessRenderContext context, PropertySheet sheet, int passIndex, float ratio)
        {
            // have to initalize them at 0 firsdt so unity doesnt cry about it 
            Vector4[] offsets = new Vector4[32];
            sheet.properties.SetVectorArray(HASH_GAUSS_OFFSET, offsets);
            float[] weights = new float[32];
            sheet.properties.SetFloatArray(HASH_GAUSS_WEIGHT, weights);
            // context.command.Clear();
            context.command.ClearRenderTarget(false, false, Color.black);

            for (int i = 0; i < r.Length; i++) // making the assumption that the size of the renderers array is the same as the r array
            {
                // // get current stage
                BloomStage curr = settings.allBloomStages.value.Stages.ToArray()[i];
                // // calc gaussian blur
                CalculateGaussianKernel(curr.KernelSize, curr.KernelSigma, sizes[i].x, sizes[i].y, true, out weights, out offsets);

                // // Set tap count and shader properties
                sheet.properties.SetInt(HASH_GAUSSTAPS, curr.KernelSize);
                sheet.properties.SetVectorArray(HASH_GAUSS_OFFSET, offsets);
                sheet.properties.SetFloatArray(HASH_GAUSS_WEIGHT, weights);
                sheet.properties.SetFloat(HASH_GAUSSLAYER, curr.LayerIntensity);

                sheet.properties.SetVector(HASH_SCALER, new Vector2(1f, 0f));

                context.command.SetViewport(r[i]);
                context.command.BlitFullscreenTriangle(renderers[i], dest, sheet, passIndex, true, r[i]);
            }
        }
        public void BlurAtlasV(Rect[] r, Vector4[] clamp, Vector4[] translate, int renderer, int dest, PostProcessRenderContext context, PropertySheet sheet, int passIndex, float ratio)
        {
            Vector4[] offsets = new Vector4[32];
            sheet.properties.SetVectorArray(HASH_GAUSS_OFFSET, offsets);
            float[] weights = new float[32];
            sheet.properties.SetFloatArray(HASH_GAUSS_WEIGHT, weights);
            // context.command.Clear();
            context.command.ClearRenderTarget(false, false, Color.black);

            for (int i = 0; i < clamp.Length; i++) // making the assumption that the size of the renderers array is the same as the r array
            {
                // get current stage
                BloomStage curr = settings.allBloomStages.value.Stages.ToArray()[i];
                // // calc gaussian blur
                CalculateGaussianKernel(curr.KernelSize, curr.KernelSigma, 466f, 174f, false, out weights, out offsets);

                // Set tap count and shader properties
                sheet.properties.SetInt(HASH_GAUSSTAPS, curr.KernelSize);
                sheet.properties.SetVectorArray(HASH_GAUSS_OFFSET, offsets);
                sheet.properties.SetFloatArray(HASH_GAUSS_WEIGHT, weights);
                sheet.properties.SetFloat(HASH_GAUSSLAYER, curr.LayerIntensity);

                sheet.properties.SetVector(HASH_SCALER, new Vector2(0f, 1f));
                sheet.properties.SetVector(HASH_GAUSSCLAMP, clamp[i]);
                sheet.properties.SetVector(HASH_GAUSSTRANS, translate[i]);

                // context.command.SetViewport(r[i]);
                context.command.BlitFullscreenTriangle(renderer, dest, sheet, passIndex, false, r[i]);
            }
        }

        public void CalculateGaussianKernel(
            int targetTaps,      // Should be 4 for Stage A
            float sigma,         // Set to 1.5f in Inspector
            float atlasWidth,
            float atlasHeight,
            bool isHorizontal,
            out float[] outWeights,
            out Vector4[] outOffsets)
        {
            outWeights = new float[32];
            outOffsets = new Vector4[32];

            float texelX = 1.0f / atlasWidth;
            float texelY = 1.0f / atlasHeight;

            float totalSum = 0f;
            float[] weights = new float[targetTaps];

            // 1. Calculate Discrete Weights
            // For 4 taps, this loop creates offsets: -1.5, -0.5, 0.5, 1.5
            for (int i = 0; i < targetTaps; i++)
            {
                float p = i - (targetTaps - 1) * 0.5f;
                weights[i] = (float)Math.Exp(-(p * p) / (2.0f * sigma * sigma));
                totalSum += weights[i];
            }

            for (int i = 0; i < targetTaps; i++)
            {
                outWeights[i] = weights[i] / totalSum;

                // Use a centered index: e.g., for 4 taps: -1.5, -0.5, 0.5, 1.5
                // This ensures no 'unbalanced' offset pushes the image.
                float p = (float)i - (targetTaps - 1) * 0.5f;

                if (isHorizontal)
                {
                    // For the Horizontal Pass: Star Rail specifically offsets Y 
                    // by the same ratio to create the diagonal streak.
                    outOffsets[i] = new Vector4(p * texelX, p * texelY, 0, 0);
                }
                else
                {
                    // For the Vertical Pass: This pass should usually be pure Vertical 
                    // to settle the blur back into place.
                    outOffsets[i] = new Vector4(0, p * texelY, 0, 0);
                }
            }
        }
        public void SetBloomSettings(PropertySheet sheet)
        {
            sheet.properties.SetFloat(HASH_BLOOM_THRESHOLD, settings.bloomSettings.value.BloomThreshold);
            sheet.properties.SetFloat(HASH_BLOOM_INTENSITY, settings.bloomSettings.value.BloomIntensity);
            sheet.properties.SetFloat(HASH_BLOOM_R, settings.bloomSettings.value.BloomR);
            sheet.properties.SetFloat(HASH_BLOOM_G, settings.bloomSettings.value.BloomG);
            sheet.properties.SetFloat(HASH_BLOOM_B, settings.bloomSettings.value.BloomB);
        }
        public static void GetPropertyHashes()
        {
            HASH_BLOOM_INTENSITY = Shader.PropertyToID("_BloomIntensity");
            HASH_BLOOM_THRESHOLD = Shader.PropertyToID("_BloomThreshold");
            HASH_BLOOM_R = Shader.PropertyToID("_BloomR");
            HASH_BLOOM_G = Shader.PropertyToID("_BloomB");
            HASH_BLOOM_B = Shader.PropertyToID("_BloomB");
            HASH_PREFILTER_ID = Shader.PropertyToID("_Prefilter");
            HASH_DOWNSCALE_A = Shader.PropertyToID("_DownScaleA");
            HASH_DOWNSCALE_B = Shader.PropertyToID("_DownScaleB");
            HASH_DOWNSCALE_C = Shader.PropertyToID("_DownScaleC");
            HASH_DOWNSCALE_D = Shader.PropertyToID("_DownScaleD");
            HASH_MAINTEX_1 = Shader.PropertyToID("_MainTex1");
            HASH_TMP_A = Shader.PropertyToID("_TMPA");
            HASH_TMP_B = Shader.PropertyToID("_TMPB");
            HASH_TMP_C = Shader.PropertyToID("_TMPC");
            HASH_TMP_D = Shader.PropertyToID("_TMPD");
            HASH_ATLAS = Shader.PropertyToID("_ATLAS");
            HASH_ATLASTMP = Shader.PropertyToID("_ATLAStTMP");
            HASH_LUT2D_TEX = Shader.PropertyToID("_Lut2DTex");
            HASH_LUTFACTOR = Shader.PropertyToID("_Lut2DTexParam");
            HASH_EFFECTENABLE = Shader.PropertyToID("_EnableEffect0");
            HASH_VIGPARAM1 = Shader.PropertyToID("_Vignette_Params1");
            HASH_VIGPARAM2 = Shader.PropertyToID("_Vignette_Params2");
            HASH_GAUSSTAPS = Shader.PropertyToID("_GaussTaps");
            HASH_SCALER = Shader.PropertyToID("_BlurScale");
            HASH_GAUSS_OFFSET = Shader.PropertyToID("_GaussOffset");
            HASH_GAUSS_WEIGHT = Shader.PropertyToID("_GaussWeights");
            HASH_GAUSSLAYER = Shader.PropertyToID("_GaussianLayerIntensity");
            HASH_GAUSSTRANS = Shader.PropertyToID("_GaussianUVTransform");
            HASH_GAUSSCLAMP = Shader.PropertyToID("_GaussianUVClamp");
            HASH_BLOOMATLAS_UV = Shader.PropertyToID("_BloomAtlasUVTrans");
        }
    }
}