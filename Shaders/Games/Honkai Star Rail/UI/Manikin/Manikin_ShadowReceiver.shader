Shader "HoyoToon/Honkai Star Rail/UI/Manikin/ShadowReceiver"
{
    Properties
    {
        [HideInInspector] _MainTex("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }

        Pass
        {
            Name "ManikinDrawDepth"
            Tags { "LightMode" = "ManikinDrawDepth" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings VertDepth(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 FragDepth(Varyings input) : SV_Target
            {
                float deviceDepth = input.positionCS.z / max(input.positionCS.w, 1e-6);
                float linearEye = LinearEyeDepth(deviceDepth, _ZBufferParams);
                return float4(linearEye, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ManikinShadowReceiver"
            Tags { "LightMode" = "ManikinShadowReceiver" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex VertReceiver
            #pragma fragment FragReceiver
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define HSR_MANIKIN_MAX_SLOTS 12

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            TEXTURE2D(_ManikinRawShadowAtlas);
            SAMPLER(sampler_ManikinRawShadowAtlas);
            TEXTURE2D(_ManikinDepth);
            SAMPLER(sampler_ManikinDepth);

            CBUFFER_START(UnityPerFrame)
                float4x4 _ManikinWorldToShadowArr[HSR_MANIKIN_MAX_SLOTS];
                float4 _ManikinShadowAtlasRectArr[HSR_MANIKIN_MAX_SLOTS];
                float4 _ManikinShadowOriginArr[HSR_MANIKIN_MAX_SLOTS];
                float4 _ManikinRawShadowAtlasTexelSize;
                float _ManikinShadowSlotCount;
                float _ManikinShadowStrength;
                float _ManikinPcssSearchRadius;
                float _ManikinPcssMinFilterRadius;
                float _ManikinPcssMaxFilterRadius;
                float _ManikinRadialBlurStart;
                float _ManikinRadialBlurEnd;
                float _ManikinRadialBlurStrength;
                float _ManikinRadialFadeStart;
                float _ManikinRadialFadeEnd;
                float _ManikinFloorDepthThreshold;
            CBUFFER_END

            static const float2 k_PcfDisk8[8] =
            {
                float2(-0.94201624, -0.39906216),
                float2(0.94558609, -0.76890725),
                float2(-0.09418410, -0.92938870),
                float2(0.34495938, 0.29387760),
                float2(-0.91588581, 0.45771432),
                float2(-0.81544232, -0.87912464),
                float2(-0.38277543, 0.27676845),
                float2(0.97484398, 0.75648379)
            };

            static const float2 k_PcfDisk12[12] =
            {
                float2(-0.326212, -0.405810),
                float2(-0.840144, -0.073580),
                float2(-0.695914, 0.457137),
                float2(-0.203345, 0.620716),
                float2(0.962340, -0.194983),
                float2(0.473434, -0.480026),
                float2(0.519456, 0.767022),
                float2(0.185461, -0.893124),
                float2(0.507431, 0.064425),
                float2(0.896420, 0.412458),
                float2(-0.321940, -0.932615),
                float2(-0.791559, -0.597710)
            };

            Varyings VertReceiver(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                return output;
            }

            float SampleAtlasDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_ManikinRawShadowAtlas, sampler_ManikinRawShadowAtlas, uv).r;
            }

            bool IsUvInsideAtlasRect(float2 uv, float4 atlasRect, float2 texelSize)
            {
                float2 rectMin = atlasRect.xy + texelSize * 0.5;
                float2 rectMax = atlasRect.xy + atlasRect.zw - texelSize * 0.5;
                return all(uv >= rectMin) && all(uv <= rectMax);
            }

            float ComputeReceiverVisibility(float4 shadowCoord, float4 atlasRect, float3 positionWS, float3 originWS)
            {
                if (abs(shadowCoord.w) < 1e-6)
                    return 1.0;

                float3 sts = shadowCoord.xyz / shadowCoord.w;
                float2 rectMin = atlasRect.xy;
                float2 rectMax = atlasRect.xy + atlasRect.zw;
                if (any(sts.xy < rectMin) || any(sts.xy > rectMax) || sts.z < 0.0 || sts.z > 1.0)
                    return 1.0;

                float receiverDepth = saturate(sts.z);
                float2 texelSize = max(_ManikinRawShadowAtlasTexelSize.xy, (1.0 / 4096.0).xx);
                float depthBias = 0.0006;

                float blockerDepthSum = 0.0;
                float blockerCount = 0.0;
                float2 blockerRadius = texelSize * _ManikinPcssSearchRadius;
                [unroll]
                for (int i = 0; i < 8; ++i)
                {
                    float2 uv = sts.xy + k_PcfDisk8[i] * blockerRadius;
                    if (!IsUvInsideAtlasRect(uv, atlasRect, texelSize))
                        continue;

                    float sampleDepth = SampleAtlasDepth(uv);
                    #if defined(UNITY_REVERSED_Z)
                    if (sampleDepth - depthBias > receiverDepth)
                    #else
                    if (sampleDepth + depthBias < receiverDepth)
                    #endif
                    {
                        blockerDepthSum += sampleDepth;
                        blockerCount += 1.0;
                    }
                }

                float filterRadius = _ManikinPcssMinFilterRadius;
                if (blockerCount > 0.0)
                {
                    float avgBlockerDepth = blockerDepthSum / blockerCount;
                    float penumbra;
                    #if defined(UNITY_REVERSED_Z)
                    float depthDelta = max(avgBlockerDepth - receiverDepth, 0.0);
                    penumbra = saturate(depthDelta / max(receiverDepth, 1e-4));
                    #else
                    float depthDelta = max(receiverDepth - avgBlockerDepth, 0.0);
                    penumbra = saturate(depthDelta / max(avgBlockerDepth, 1e-4));
                    #endif
                    filterRadius = lerp(_ManikinPcssMinFilterRadius, _ManikinPcssMaxFilterRadius, penumbra);
                }

                float radialRange = max(_ManikinRadialBlurEnd - _ManikinRadialBlurStart, 1.0e-4);
                float radialDistance = distance(positionWS.xz, originWS.xz);
                float radialT = saturate((radialDistance - _ManikinRadialBlurStart) / radialRange);
                float radialScale = lerp(1.0, 1.0 + _ManikinRadialBlurStrength, radialT);
                float filterRadiusMax = _ManikinPcssMaxFilterRadius * (1.0 + _ManikinRadialBlurStrength);
                filterRadius = min(filterRadius * radialScale, filterRadiusMax);

                float2 pcfRadius = texelSize * filterRadius;
                float visibility = 0.0;
                float validSampleCount = 0.0;
                [unroll]
                for (int i = 0; i < 12; ++i)
                {
                    float2 uv = sts.xy + k_PcfDisk12[i] * pcfRadius;
                    if (!IsUvInsideAtlasRect(uv, atlasRect, texelSize))
                        continue;

                    validSampleCount += 1.0;

                    float sampleDepth = SampleAtlasDepth(uv);
                    #if defined(UNITY_REVERSED_Z)
                    visibility += (receiverDepth + depthBias >= sampleDepth) ? 1.0 : 0.0;
                    #else
                    visibility += (receiverDepth <= sampleDepth + depthBias) ? 1.0 : 0.0;
                    #endif
                }

                if (validSampleCount <= 0.0)
                    return 1.0;

                return visibility / validSampleCount;
            }

            bool IsProjectedIntoSlot(float4 shadowCoord, float4 atlasRect)
            {
                if (abs(shadowCoord.w) < 1e-6)
                    return false;

                float3 sts = shadowCoord.xyz / shadowCoord.w;
                float2 rectMin = atlasRect.xy;
                float2 rectMax = atlasRect.xy + atlasRect.zw;
                return all(sts.xy >= rectMin)
                    && all(sts.xy <= rectMax)
                    && sts.z >= 0.0
                    && sts.z <= 1.0;
            }

            float ComputeAtlasShadow(float3 positionWS)
            {
                int slotCount = (int)clamp(floor(_ManikinShadowSlotCount + 0.5), 0.0, (float)HSR_MANIKIN_MAX_SLOTS);
                if (slotCount <= 0)
                    return 1.0;

                float visibility = 1.0;
                float hasProjectedContribution = 0.0;
                [loop]
                for (int i = 0; i < HSR_MANIKIN_MAX_SLOTS; ++i)
                {
                    if (i >= slotCount)
                        break;

                    float4 atlasRect = _ManikinShadowAtlasRectArr[i];
                    if (atlasRect.z <= 0.0 || atlasRect.w <= 0.0)
                        continue;

                    float3 originWS = _ManikinShadowOriginArr[i].xyz;

                    float4 shadowCoord = mul(_ManikinWorldToShadowArr[i], float4(positionWS, 1.0));
                    if (!IsProjectedIntoSlot(shadowCoord, atlasRect))
                        continue;

                    float sliceVisibility = ComputeReceiverVisibility(shadowCoord, atlasRect, positionWS, originWS);
                    float fadeRange = max(_ManikinRadialFadeEnd - _ManikinRadialFadeStart, 1.0e-4);
                    float radialDistance = distance(positionWS.xz, originWS.xz);
                    float fadeT = saturate((radialDistance - _ManikinRadialFadeStart) / fadeRange);
                    float fadedSliceVisibility = lerp(sliceVisibility, 1.0, fadeT);

                    visibility = min(visibility, fadedSliceVisibility);
                    hasProjectedContribution = 1.0;
                }

                if (hasProjectedContribution < 0.5)
                    return 1.0;

                return visibility;
            }

            float4 FragReceiver(Varyings input) : SV_Target
            {
                float floorMask = 1.0;

                float visibility = ComputeAtlasShadow(input.positionWS);
                float shadow = lerp(1.0, visibility, saturate(_ManikinShadowStrength));
                float maskedShadow = lerp(1.0, shadow, floorMask);

                return float4(maskedShadow.xxx, floorMask);
            }
            ENDHLSL
        }
    }
}
