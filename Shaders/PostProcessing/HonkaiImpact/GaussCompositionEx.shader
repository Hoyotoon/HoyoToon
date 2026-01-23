Shader "Hidden/HoyoToon/HonkaiImpact/GaussCompositionEx"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            Texture2D _MainTex;
            Texture2D _MainTex0;
            Texture2D _MainTex1;
            Texture2D _MainTex2;
            Texture2D _MainTex3;
            float4 coeff;
            SamplerState sampler_linear_clamp;
            float4 _MainTex_TexelSize;
        ENDHLSL

        Pass
        {
            Name "DownSample"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord.xy = tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    // this is a glorified copy pass
                    float4 tmp = _MainTex0.Sample(sampler_linear_clamp, i.texcoord) * coeff.x;
                    tmp += (_MainTex1.Sample(sampler_linear_clamp, i.texcoord) * coeff.y);
                    tmp += (_MainTex2.Sample(sampler_linear_clamp, i.texcoord) * coeff.z);
                    tmp += (_MainTex3.Sample(sampler_linear_clamp, i.texcoord) * coeff.w);
                    return tmp;
                }
            ENDHLSL
        }
    }
}