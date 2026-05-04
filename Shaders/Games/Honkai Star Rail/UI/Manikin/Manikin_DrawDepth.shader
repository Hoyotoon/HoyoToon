Shader "HoyoToon/Honkai Star Rail/UI/Manikin/DrawDepth"
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float deviceDepth = input.positionCS.z / max(input.positionCS.w, 1e-6);
                float linearEyeDepth = LinearEyeDepth(deviceDepth, _ZBufferParams);
                return float4(linearEyeDepth, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
