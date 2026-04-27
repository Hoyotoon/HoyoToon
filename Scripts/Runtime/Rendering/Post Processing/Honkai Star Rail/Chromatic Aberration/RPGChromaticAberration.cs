using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.ChromaticAberration
{

    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGChromaticAberration"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGChromaticAberration : VolumeComponent, IPostProcessComponent
    {

        RPGChromaticAberration()
        {
            FilterA.value = Color.red;
            FilterB.value = Color.green;
            FilterC.value = Color.blue;
            CombineWithRadialBlur.value = false;
            intensity.value = 0f;
        }

        public ColorParameter FilterA = new ColorParameter(Color.red);
        public ColorParameter FilterB = new ColorParameter(Color.green);
        public ColorParameter FilterC = new ColorParameter(Color.blue);
        public BoolParameter CombineWithRadialBlur = new BoolParameter(false);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public bool IsActive() => true;
        public bool IsTileCompatible() => true;
    }
}