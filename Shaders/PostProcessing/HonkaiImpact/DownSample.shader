Shader "Hidden/HoyoToon/HonkaiImpact/DownSample"
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
                    float4 tmp = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                    return tmp;
                }
            ENDHLSL
        }
    }
}