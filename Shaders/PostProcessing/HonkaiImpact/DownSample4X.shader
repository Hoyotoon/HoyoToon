Shader "Hidden/HoyoToon/HonkaiImpact/DownSample4X"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            Texture2D _MainTex;
            SamplerState sampler_linear_clamp;
            float4 _MainTex_TexelSize;
        ENDHLSL

        Pass
        {
            Name "DownSample4X"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                    float4 texcoord1 : TEXCOORD1;
                    float4 texcoord2 : TEXCOORD2;
                    float4 texcoord3 : TEXCOORD3;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord.xy = _MainTex_TexelSize.xy + tmp.xy;
                    o.texcoord1.xy = _MainTex_TexelSize.xy * float2(1,-1) + tmp.xy;
                    o.texcoord2.xy = -_MainTex_TexelSize.xy + tmp.xy;
                    o.texcoord3.xy = _MainTex_TexelSize.xy * float2(-1,1) + tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    float4 tmp = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                    tmp += _MainTex.Sample(sampler_linear_clamp, i.texcoord1);
                    tmp += _MainTex.Sample(sampler_linear_clamp, i.texcoord2);
                    tmp += _MainTex.Sample(sampler_linear_clamp, i.texcoord3);

                    tmp /= 4;

                    return tmp;
                }
            ENDHLSL
        }
    }
}