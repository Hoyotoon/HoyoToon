using System;
using HoyoToon.Rendering.PostProcessing.HSR.Bloom;
using HoyoToon.Rendering.PostProcessing.HSR.ChromaticAberration;
using HoyoToon.Rendering.PostProcessing.HSR.RadialBlur;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.Rendering.PostProcessing.HSR.Uber
{
    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGUber"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGUber : VolumeComponent, IPostProcessComponent
    {
        private const string DefaultLutAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/Honkai Star Rail/Textures/LUTs/LUT_HSR.png";
        private const string FallbackLutAssetPath = "Packages/HoyoToon/Resources/Honkai Star Rail/Textures/LUTs/LUT_HSR.png";
        private const string DefaultLutResourcesPath = "Honkai Star Rail/Textures/LUTs/LUT_HSR";
        private const int NeutralLutDimension = 32;

        private static Texture2D s_DefaultLutTexture;

        [Tooltip("When enabled, this component controls which RPG post-process components are allowed to render.")]
        public BoolParameter UseUberControl = new BoolParameter(false);

        [Tooltip("Allow the RPGBloom renderer to execute.")]
        public BoolParameter EnableBloom = new BoolParameter(true);

        [Tooltip("Baked LUT texture used by the Uber post pass.")]
        public NoInterpTextureParameter BakedLutTexture = new NoInterpTextureParameter(null);

        [Tooltip("Amount of LUT slices minus one (default 31).")]
        public ClampedIntParameter LutSlices = new ClampedIntParameter(31, 1, 255);

        [Tooltip("LUT lookup factors used by the strip-based LUT sampling.")]
        public Vector2Parameter LutFactor = new Vector2Parameter(new Vector2(0.00098f, 0.03125f));

        [Tooltip("Flip LUT lookup vertically (Y axis).")]
        public BoolParameter FlipLutY = new BoolParameter(false);

        [Tooltip("Allow the RPGChromaticAberration renderer to execute.")]
        public BoolParameter EnableChromaticAberration = new BoolParameter(true);

        [Tooltip("Allow the RPGRadialBlur renderer to execute.")]
        public BoolParameter EnableRadialBlur = new BoolParameter(true);

        public bool IsActive() => UseUberControl.value;

        public bool IsTileCompatible() => true;

        public static Texture2D GetDefaultLutTexture()
        {
            if (s_DefaultLutTexture != null)
            {
                return s_DefaultLutTexture;
            }

#if UNITY_EDITOR
            s_DefaultLutTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultLutAssetPath);
            if (s_DefaultLutTexture == null)
            {
                s_DefaultLutTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FallbackLutAssetPath);
            }
#endif

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

#if UNITY_EDITOR
        [ContextMenu("Add Missing RPG Components To Profile")]
        private void AddMissingRpgComponentsToProfile()
        {
            VolumeProfile owningProfile = FindOwningProfile();
            if (owningProfile == null)
            {
                Debug.LogWarning($"{nameof(RPGUber)}: could not resolve the owning VolumeProfile for this component.", this);
                return;
            }

            bool changed = false;
            changed |= AddIfMissing<RPGBloom>(owningProfile);
            changed |= AddIfMissing<RPGChromaticAberration>(owningProfile);
            changed |= AddIfMissing<RPGRadialBlur>(owningProfile);

            if (!changed)
            {
                Debug.Log($"{nameof(RPGUber)}: the profile already contains all RPG components.", owningProfile);
                return;
            }

            EditorUtility.SetDirty(owningProfile);
            AssetDatabase.SaveAssetIfDirty(owningProfile);
            Debug.Log($"{nameof(RPGUber)}: added missing RPG components to '{owningProfile.name}'.", owningProfile);
        }

        private static bool AddIfMissing<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T _))
            {
                return false;
            }

            profile.Add<T>(false);
            return true;
        }

        private VolumeProfile FindOwningProfile()
        {
            string path = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                VolumeProfile profile = assets[i] as VolumeProfile;
                if (profile == null)
                {
                    continue;
                }

                if (profile.components.Contains(this))
                {
                    return profile;
                }
            }

            return null;
        }
#endif
    }
}
