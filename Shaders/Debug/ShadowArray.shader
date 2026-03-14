Shader "HoyoToon/Debug/ShadowArray"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_MainLightShadowmapTexture);
            SAMPLER(sampler_MainLightShadowmapTexture);
            float _DebugShadowSlice;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.positionCS = float4(o.uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv.y = 1.0 - o.uv.y;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D_ARRAY_LOD(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, i.uv, (int)_DebugShadowSlice, 0).r;
                float view = 1.0 - saturate(depth);
                return float4(view.xxx, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}