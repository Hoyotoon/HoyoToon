using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TrackBallParameter = UnityEngine.Rendering.Vector4Parameter;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.ToneMapping
{
    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGTonemapping"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGTonemapping : VolumeComponent, IPostProcessComponent
    {
        [Serializable]
        public enum TonemappingMethod
        {
            GenerateLUTTexture = 0,
            HableCurve = 1, 
            ACES = 2,
        };
        public TonemappingMethod tonemapping;
        [HideInInspector]
        public ClampedFloatParameter BlueCorrection = new(0.6f, 0f, 1f);
        [HideInInspector]
        public FloatParameter HDRHeadroom = new(240000f);
        [HideInInspector]
        public ClampedFloatParameter ExpandGamut = new(1f, 0f, 1f);
        public BoolParameter ForceDisableToneMapping = new(false);
        [HideInInspector] public ClampedFloatParameter Slope = new(0.8799999952316284f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter Toe = new(0.550000011920929f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter Shoulder = new(0.25999999046325684f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter BlackClip = new(0f, 0f, 1f);
        [HideInInspector] public ClampedFloatParameter WhiteClip = new(0.03999999910593033f, 0f, 1f);
        public ClampedFloatParameter ToneCurveToeStrength = new(0.41499999165534973f, 0f, 1f);
        public ClampedFloatParameter ToneCurveToeLength = new(0.6140000224113464f, 0f, 1f);
        public ClampedFloatParameter ToneCurveShoulderStrength = new(0.0f, 0f, 1f);
        public ClampedFloatParameter ToneCurveShoulderLength = new(1.0f, 0f, 1f);
        public ClampedFloatParameter ToneCurveShoulderAngle = new(0.10000000149011612f, 0f, 1f);
        public MinFloatParameter ToneCurveGamma = new(1f, 0.001f);
        
        [HideInInspector] public FloatParameter ACES_A = new(3.7100000381469727f);
        [HideInInspector] public FloatParameter ACES_B = new(0.10777200013399124f);
        [HideInInspector] public FloatParameter ACES_C = new(2.936044931411743f);
        [HideInInspector] public FloatParameter ACES_D = new(0.8871219754219055f);
        [HideInInspector] public FloatParameter ACES_E = new(0.806888997554779f);

        [Header("Color Grading")]
        public FloatParameter ColorCorrectionShadowMax = new(0.09f);
        public FloatParameter ColorCorrectionHighlightMin = new(0.5f);
        public FloatParameter LevelHighLightTone = new(1f);
        public FloatParameter LevelShadowTone = new(0f);
        public ColorParameter LevelColor = new(Color.white);
        public TrackBallParameter ColorSaturationGlobal = new(new Vector4( 1f, 1f, 1f, 1f));
        public TrackBallParameter ColorContrastGlobal = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorGainGlobal = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorSaturationShadow = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorContrastShadow = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorGainShadow = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorSaturationMidtone = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorContrastMidtone = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorGainMidtone = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorSaturationHighlight = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorContrastHighlight = new(new Vector4(1f, 1f, 1f, 1f));
        public TrackBallParameter ColorGainHighlight = new(new Vector4(1f, 1f, 1f, 1f));
        public bool IsActive()
        {
            return active
                && tonemapping == TonemappingMethod.GenerateLUTTexture
                && AnyPropertiesIsOverridden()
                && HasMeaningfulOverride();
        }

        private bool HasMeaningfulOverride()
        {
            return IsOverriddenPositive(BlueCorrection)
                || IsOverriddenPositive(ExpandGamut)
                || IsOverriddenPositive(HDRHeadroom)
                || IsOverriddenTrue(ForceDisableToneMapping)
                || IsOverriddenPositive(ToneCurveToeStrength)
                || IsOverriddenPositive(ToneCurveToeLength)
                || IsOverriddenPositive(ToneCurveShoulderStrength)
                || IsOverriddenPositive(ToneCurveShoulderLength)
                || IsOverriddenPositive(ToneCurveShoulderAngle)
                || IsOverriddenPositive(ToneCurveGamma)
                || IsOverriddenPositive(ColorCorrectionShadowMax)
                || IsOverriddenPositive(ColorCorrectionHighlightMin)
                || IsOverriddenNonZero(LevelHighLightTone)
                || IsOverriddenNonZero(LevelShadowTone)
                || IsOverriddenNonBlack(LevelColor)
                || IsOverriddenNonZero(ColorSaturationGlobal)
                || IsOverriddenNonZero(ColorContrastGlobal)
                || IsOverriddenNonZero(ColorGainGlobal)
                || IsOverriddenNonZero(ColorSaturationShadow)
                || IsOverriddenNonZero(ColorContrastShadow)
                || IsOverriddenNonZero(ColorGainShadow)
                || IsOverriddenNonZero(ColorSaturationMidtone)
                || IsOverriddenNonZero(ColorContrastMidtone)
                || IsOverriddenNonZero(ColorGainMidtone)
                || IsOverriddenNonZero(ColorSaturationHighlight)
                || IsOverriddenNonZero(ColorContrastHighlight)
                || IsOverriddenNonZero(ColorGainHighlight);
        }

        private static bool IsOverriddenPositive(FloatParameter parameter)
        {
            return parameter.overrideState && parameter.value > 0f;
        }

        private static bool IsOverriddenNonZero(FloatParameter parameter)
        {
            return parameter.overrideState && !Mathf.Approximately(parameter.value, 0f);
        }

        private static bool IsOverriddenTrue(BoolParameter parameter)
        {
            return parameter.overrideState && parameter.value;
        }

        private static bool IsOverriddenNonBlack(ColorParameter parameter)
        {
            Color value = parameter.value;
            return parameter.overrideState
                && (!Mathf.Approximately(value.r, 0f)
                    || !Mathf.Approximately(value.g, 0f)
                    || !Mathf.Approximately(value.b, 0f));
        }

        private static bool IsOverriddenNonZero(TrackBallParameter parameter)
        {
            return parameter.overrideState && parameter.value.sqrMagnitude > 0f;
        }

        public bool IsTileCompatible() => false;
    }
}
