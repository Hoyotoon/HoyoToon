Shader "Hidden/HoyoToon/HonkaiImpact/GlareCompositionEx"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            Texture2D _MainTex;
            Texture2D _BloomTex;
            SamplerState sampler_linear_clamp;
            float4 _MainTex_TexelSize;
            float2 coeff;
            float exposure;
            float constrast;
            float _PostProcessRatio;
            // float4 _RGBSplitParam;
            // Texture2D _PostfxMask;
            // Texture2D _DistortionTex;
        ENDHLSL

        Pass
        {
            Name "Glare Composite"
            HLSLPROGRAM
                #pragma vertex GlareVertex
                #pragma fragment frag

                struct GlareStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                GlareStruct GlareVertex(AttributesDefault v)
                {
                    GlareStruct o = (GlareStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord.xy = tmp.xy;
                    return o;
                }

                float4 frag (GlareStruct i) : SV_Target
                {
                    float4 main = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                    float4 bloom = _BloomTex.Sample(sampler_linear_clamp, i.texcoord);
                    float4 bloomed = (main * coeff.x) + (bloom * coeff.y);
                    bloomed.xyz = max(1 - exp2(-exposure * bloomed.xyz ), 9.99999975e-005);
                    bloomed.xyz = pow(bloomed.xyz, (constrast + 0.01f));
                    main.xyz = lerp(main.xyz, bloomed.xyz, _PostProcessRatio);                    
                    return main;
                } 
            ENDHLSL
        }
    }
}