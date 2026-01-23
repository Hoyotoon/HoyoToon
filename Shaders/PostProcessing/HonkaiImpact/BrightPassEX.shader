Shader "Hidden/HoyoToon/HonkaiImpact/BrightPassEX"
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
            float _Threshhold;
            float _Scaler;
            float _EnableLum;
        ENDHLSL

        Pass
        {
            Name "BrightPassEX"
            HLSLPROGRAM
                #pragma vertex BrightPassVertex
                #pragma fragment frag

                struct BrightPassStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                    float4 texcoord1 : TEXCOORD1;
                    float4 texcoord2 : TEXCOORD2;
                    float4 texcoord3 : TEXCOORD3;
                };

                BrightPassStruct BrightPassVertex(AttributesDefault v)
                {
                    BrightPassStruct o = (BrightPassStruct)0;
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

                float4 frag (BrightPassStruct i) : SV_Target
                {
                    float4 main = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                    main += _MainTex.Sample(sampler_linear_clamp, i.texcoord1);
                    main += _MainTex.Sample(sampler_linear_clamp, i.texcoord2);
                    main += _MainTex.Sample(sampler_linear_clamp, i.texcoord3);
                    main /= 4;
                   
                    float3 bright = normalize(main.xyz);
                    float3 tmpa;
                    tmpa.x = dot(main.xyz, float3(0.219999999, 0.707000017, 0.0710000023)) - _Threshhold;
                    tmpa.xyz = main.www * ((tmpa.xxx * bright.xyz) * _Scaler);
                    tmpa.xyz = max(tmpa.xyz, 0);

                    float3 tmpb;
                    bright.xyz = main.xyz - _Threshhold;
                    tmpb.xyz = main.www * ((main.xyz - _Threshhold) * _Scaler);
                    tmpb.xyz = max(tmpb.xyz, 0);
                    main.xyz = (_EnableLum) ? tmpa.xyz : tmpb.xyz;
                    return main;
                }
            ENDHLSL
        }
    }
}