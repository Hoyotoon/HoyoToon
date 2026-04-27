using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.RadialBlur
{
    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGRadialBlur"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGRadialBlur : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter RadialBlurX = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter RadialBlurY = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter RadialBlurRadius = new ClampedFloatParameter(0f, -2f, 2f);
        public ClampedFloatParameter RadialIteration = new ClampedFloatParameter(2f, 2f, 10f);
        public ClampedFloatParameter RadialBlurStart = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter RadialBlurFeather = new ClampedFloatParameter(0f, -1f, 1f);
        public BoolParameter RadialBlurRounded = new BoolParameter(false);
        [Space(8f)]
        public BoolParameter EnableDirectionBlur = new BoolParameter(false);
        public ClampedFloatParameter BlurIteration = new ClampedFloatParameter(0f, 0f, 10f);
        public ClampedFloatParameter BlurRadius = new ClampedFloatParameter(0f, 0f, 0.1f);
        public ClampedFloatParameter Angle = new ClampedFloatParameter(0f, 0f, 360f);
        public BoolParameter AvoidBrightnessBug = new BoolParameter(false);

        public bool IsActive() => true;
        public bool IsTileCompatible() => true;
    }
}