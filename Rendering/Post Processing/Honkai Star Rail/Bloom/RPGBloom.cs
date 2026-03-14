using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Rendering.PostProcessing.HSR.Bloom
{
    [Serializable, VolumeComponentMenu("HoyoToon/Honkai Star Rail/RPGBloom"), SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class RPGBloom : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Luminance threshold for what is considered a 'bright' pixel, between 0.1 and 5.0")]
        public ClampedFloatParameter BloomThreshold = new(0.1f, 0.1f, 5f);

        [Tooltip("Strength of the bloom filter, between 0.1 and 8.0")]
        public ClampedFloatParameter BloomIntensity = new(0.1f, 0.1f, 8.0f);
        public AllBloomStageParameter AllBloomStages = new(new AllBloomStage()
        {
            Stages = new List<BloomStage>()
            {
                new(14, 0.004564f, 1f),
                new(14, 0.004564f, 1f),
                new(14, 0.004564f, 1f),
                new(14, 0.004564f, 1f)
            }
        }, false);
        
        public ClampedFloatParameter BloomR = new(0f, 0f, 1f);
        public ClampedFloatParameter BloomG = new(0f, 0f, 1f);
        public ClampedFloatParameter BloomB = new(0f, 0f, 1f);
        



        [Serializable]
        public class BloomStage
        {
            public int KernelSize = 14;
            public float KernalSigma = 0.005f;
            public float LayerIntensity = 1f;

            public BloomStage() { }

            public BloomStage(int kernelSize, float kernelSigma, float layerIntensity)
            {
                KernelSize = kernelSize;
                KernalSigma = kernelSigma;
                LayerIntensity = layerIntensity;
            }
        }

        [Serializable]
        public class AllBloomStage
        {
            public List<BloomStage> Stages = new();
        }

        [Serializable]
        public sealed class AllBloomStageParameter : VolumeParameter<AllBloomStage>
        {
            public AllBloomStageParameter() : this(new AllBloomStage(), false) { }

            public AllBloomStageParameter(AllBloomStage value, bool overrideState = false) : base(value, overrideState) { }

            public override void Interp(AllBloomStage from, AllBloomStage to, float t)
            {
                m_Value ??= new AllBloomStage();
                m_Value.Stages ??= new List<BloomStage>();
                m_Value.Stages.Clear();

                List<BloomStage> fromStages = from?.Stages;
                List<BloomStage> toStages = to?.Stages;

                int fromCount = fromStages?.Count ?? 0;
                int toCount = toStages?.Count ?? 0;
                int maxCount = Mathf.Max(fromCount, toCount);

                for (int i = 0; i < maxCount; i++)
                {
                    BloomStage fromStage = i < fromCount ? fromStages[i] : null;
                    BloomStage toStage = i < toCount ? toStages[i] : null;

                    if (fromStage == null && toStage == null)
                    {
                        continue;
                    }

                    if (fromStage == null)
                    {
                        fromStage = toStage;
                    }

                    if (toStage == null)
                    {
                        toStage = fromStage;
                    }

                    m_Value.Stages.Add(new BloomStage(
                        Mathf.RoundToInt(Mathf.Lerp(fromStage.KernelSize, toStage.KernelSize, t)),
                        Mathf.Lerp(fromStage.KernalSigma, toStage.KernalSigma, t),
                        Mathf.Lerp(fromStage.LayerIntensity, toStage.LayerIntensity, t)));
                }
            }
        }

        public bool IsActive()
        {
            if (!active)
            {
                return false;
            }

            if (BloomIntensity.value <= 0f)
            {
                return false;
            }

            List<BloomStage> stageList = AllBloomStages.value?.Stages;
            return stageList != null && stageList.Count > 0;
        }
        public bool IsTileCompatible() => true;

    }
}