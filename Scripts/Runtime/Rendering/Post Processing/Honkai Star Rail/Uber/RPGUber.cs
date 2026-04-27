using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber
{
    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGUber"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGUber : VolumeComponent, IPostProcessComponent
    {
        private const string DefaultLutResourcesPath = "Honkai Star Rail/Textures/LUTs/LUT_HSR";
        private const int NeutralLutDimension = 32;

        private static Texture2D s_DefaultLutTexture;

        [Tooltip("When enabled, this component controls which RPG post-process components are allowed to render.")]
        [HideInInspector] public BoolParameter UseUberControl = new BoolParameter(false);

        [Tooltip("Allow the RPGBloom renderer to execute.")]
        [HideInInspector] public BoolParameter EnableBloom = new BoolParameter(true);

        [Tooltip("Baked LUT texture used by the Uber post pass.")]
        public NoInterpTextureParameter BakedLutTexture = new NoInterpTextureParameter(null);

        [Tooltip("Amount of LUT slices minus one (default 31).")]
        public ClampedIntParameter LutSlices = new ClampedIntParameter(31, 1, 255);

        [Tooltip("LUT lookup factors used by the strip-based LUT sampling.")]
        public Vector2Parameter LutFactor = new Vector2Parameter(new Vector2(0.00098f, 0.03125f));

        [Tooltip("Flip LUT lookup vertically (Y axis).")]
        public BoolParameter FlipLutY = new BoolParameter(false);

        [Tooltip("Allow the RPGChromaticAberration renderer to execute.")]
        [HideInInspector] public BoolParameter EnableChromaticAberration = new BoolParameter(true);

        [Tooltip("Allow the RPGRadialBlur renderer to execute.")]
        [HideInInspector] public BoolParameter EnableRadialBlur = new BoolParameter(true);

        public bool IsActive() => UseUberControl.value;

        public bool IsTileCompatible() => true;

        public static Texture2D GetDefaultLutTexture()
        {
            if (s_DefaultLutTexture != null)
            {
                return s_DefaultLutTexture;
            }

            if (s_DefaultLutTexture == null)
            {
                s_DefaultLutTexture = Resources.Load<Texture2D>(DefaultLutResourcesPath);
            }

            if (s_DefaultLutTexture == null)
            {
                s_DefaultLutTexture = CreateNeutralLutTexture();
            }

            return s_DefaultLutTexture;
        }

        private static Texture2D CreateNeutralLutTexture()
        {
            int width = NeutralLutDimension * NeutralLutDimension;
            int height = NeutralLutDimension;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "RPGUber_DefaultNeutralLUT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[width * height];
            float maxIndex = NeutralLutDimension - 1f;
            for (int blue = 0; blue < NeutralLutDimension; blue++)
            {
                int blueOffset = blue * NeutralLutDimension;
                float blueValue = blue / maxIndex;
                for (int green = 0; green < NeutralLutDimension; green++)
                {
                    float greenValue = green / maxIndex;
                    int rowOffset = green * width;
                    for (int red = 0; red < NeutralLutDimension; red++)
                    {
                        float redValue = red / maxIndex;
                        pixels[rowOffset + blueOffset + red] = new Color(redValue, greenValue, blueValue, 1f);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
