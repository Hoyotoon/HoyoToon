Shader "Hidden/HoyoToon/Genshin/PostProcessingUber"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float2 GrainRandomFull;
            float _MHYBloomTonemapping;
            float _MHYBloomIntensity;
            float _UserInputGamma;
            float _MHYBloomExpossure;
            Texture2D _MainTex;
            Texture2D _MHYBloomTex;
            float4 _MainTex_TexelSize;
            SamplerState sampler_linear_repeat;
            SamplerState sampler_linear_clamp;

        ENDHLSL

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM

            #pragma vertex VertDefault
            #pragma fragment frag

            struct MihoyoDefault
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
            };

            MihoyoDefault VertUVTransformST(AttributesDefault v)
            {
                MihoyoDefault o = (MihoyoDefault)0;

                o.vertex.xy = v.vertex.xy;
                o.vertex.zw = float2(0.0, 1.0);
                float4 tmp;
                tmp = v.vertex.xyxy + 1;
                tmp = tmp * 0.5;
                float4 phase0_Output0_1 = tmp;
                tmp.xy = float2(tmp.z * _MainTex_TexelSize.z, tmp.w * _MainTex_TexelSize.w);
                o.texcoord2.zw = tmp.zw * _MainTex_TexelSize.zw + float2(GrainRandomFull.x, GrainRandomFull.y);
                o.texcoord2.xy = tmp.xy;
                o.texcoord.xy = v.vertex.xy * 0.5f + 0.5;
                o.texcoord1.xy = phase0_Output0_1.zw;
                #if UNITY_UV_STARTS_AT_TOP
                o.texcoord.y = 1 - o.texcoord.y;
                o.texcoord1.y = 1 - o.texcoord1.y;
                #endif
                return o;
            }

            float4 frag (VaryingsDefault i) : SV_Target
            {

                float4x4 WhiteBalanceMat;
                WhiteBalanceMat[0].xyzw = float4(1.00032, -0.00002, 0.00002, 0.00);   
                WhiteBalanceMat[1].xyzw = float4(0.0004, 0.99977, 0.00008, 0.00);     
                WhiteBalanceMat[2].xyzw = float4(-0.00002, -0.00002, 1.00058, 0.00);  
                WhiteBalanceMat[3].xyzw = float4(0.00, 0.00, 0.00, 1.00); 

                float4 main = _MainTex.Sample(sampler_linear_clamp, i.texcoord).xyzw;
                float4 bloom = _MHYBloomTex.Sample(sampler_linear_clamp, i.texcoord).xyzw;
                main = bloom * _MHYBloomIntensity + main;

                float3 whitebalanced = main.yyy * WhiteBalanceMat[1].xyz;
                whitebalanced.xyz = WhiteBalanceMat[0].xyz * main.xxx + whitebalanced.xyz;
                whitebalanced.xyz = WhiteBalanceMat[2].xyz * main.zzz + whitebalanced.xyz;
                main.xyz = whitebalanced * _MHYBloomExpossure;

                float3 tmp = main.xyz;
                float3 f0 = (1.36 * main + 0.047) * main;
                float3 f1 = (0.93 * main + 0.56) * main + 0.14;
                main.xyz = saturate(f0 / f1);
                main.xyz = ((_MHYBloomTonemapping) ? (main.xyz) : (tmp.xyz));

                main.xyz = max(main.xyz, 0);
    
                main.xyz = pow(main.xyz, _UserInputGamma);
                main.xyz = max(main, 0);
                return main;
            }
            
            ENDHLSL
        }
        
        
    }
}
