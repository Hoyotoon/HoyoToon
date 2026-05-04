Shader "HoyoToon/Honkai Star Rail/UI/Manikin/ShadowBlur"
{
    Properties
    {
        _BlurRadius("Blur Radius (Pixels)", Range(0.0, 6.0)) = 1.5
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 1)
        _ShadowOpacity("Shadow Opacity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags { "LIGHTMODE" = "Transparent" "QUEUE" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "CustomForward"
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Transparent" "RenderType" = "Transparent" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
                float _ShadowOpacity;
                float4 _ShadowColor;
            CBUFFER_END

            TEXTURE2D(_ManikinShadow);
            SAMPLER(sampler_ManikinShadow);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                return output;
            }

            float SampleShadow(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_ManikinShadow, sampler_ManikinShadow, uv).r;
            }

            bool IsUvInViewport(float2 uv)
            {
                return all(uv >= 0.0.xx) && all(uv <= 1.0.xx);
            }

            void AccumulateSample(float2 uv, float weight, inout float sum, inout float weightSum)
            {
                if (!IsUvInViewport(uv))
                    return;

                sum += SampleShadow(uv) * weight;
                weightSum += weight;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPos.xy / max(input.screenPos.w, 1e-6);
                float2 texelSize = _ScreenParams.zw;
                float2 stepUV = texelSize * _BlurRadius;

                float shadow = 0.0;
                float weightSum = 0.0;

                AccumulateSample(uv + stepUV * float2(-1.0, -1.0), 0.0625, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(0.0, -1.0), 0.125, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(1.0, -1.0), 0.0625, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(-1.0, 0.0), 0.125, shadow, weightSum);
                AccumulateSample(uv, 0.25, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(1.0, 0.0), 0.125, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(-1.0, 1.0), 0.0625, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(0.0, 1.0), 0.125, shadow, weightSum);
                AccumulateSample(uv + stepUV * float2(1.0, 1.0), 0.0625, shadow, weightSum);

                shadow = (weightSum > 1.0e-6) ? (shadow / weightSum) : 1.0;

                shadow = lerp(1.0, shadow, saturate(_ShadowOpacity));
                return float4(_ShadowColor.rgb, 1-shadow.x);
            }
            ENDHLSL
        }
    }
}
