Shader "Hidden/HoyoToon/Genshin/Bloom"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float _MHYBloomThreshold;
            float _MHYBloomThresholdCharacter;
            float _MHYBloomScaler;
            float4 _UVTransformSource;
            float4 _UVTransformTarget;

            float4 _MHYBloomUVTransform1;
            float4 _MHYBloomUVTransform2;
            float4 _MHYBloomUVTransform3;
            float4 _MHYBloomBlurComposeWeights;

            Texture2D _MainTex;
            Texture2D _MainTex0;
            Texture2D _MainTex1;
            float4 _MainTex_TexelSize;
            float4 _MainTex0_TexelSize;
            float4 _MainTex1_TexelSize;
            SamplerState sampler_linear_repeat;
            SamplerState sampler_linear_clamp;

        ENDHLSL

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM

            #pragma vertex VertDefault
            #pragma fragment frag

            float4 frag (VaryingsDefault i) : SV_Target
            {
                float4 tmp_uva = _MainTex_TexelSize.xyxy * float4(0.95, 0.25, 0.25, -0.95) + i.texcoord.xyxy;
                float4 tmp_uvb = _MainTex_TexelSize.xyxy * float4(-0.95, -0.25, -0.25, 0.95) + i.texcoord.xyxy;
                float2 uva = tmp_uva.xy;
                float2 uvb = tmp_uva.zw;
                float2 uvc = tmp_uvb.xy;
                float2 uvd = tmp_uvb.zw;
                float3 bloom_thresh =  _MHYBloomThreshold;
                float4 col = _MainTex.Sample(sampler_linear_clamp, uva);
                col.xyz += _MainTex.Sample(sampler_linear_clamp, uvb);
                col.xyz += _MainTex.Sample(sampler_linear_clamp, uvc);
                col.xyz += _MainTex.Sample(sampler_linear_clamp, uvd);
                col.xyz *= 0.25f;

                col.xyz = max(col.xyz - bloom_thresh, 0.0f) * max(_MHYBloomScaler - 1, 0.0f);
                col.w = 1.0f;

                return col;
            }
            
            ENDHLSL
        }
        
        Pass
        {
            Name "Compose Atlas"
            HLSLPROGRAM

            #pragma vertex VertUVTransformST
            #pragma fragment frag
            struct MihoyoDefault
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                float4 texcoord3 : TEXCOORD3;
            };

            MihoyoDefault VertUVTransformST(AttributesDefault v)
            {
                MihoyoDefault o = (MihoyoDefault)0;

                o.vertex = float4(v.vertex.xy, 0.0, 1.0);
                o.texcoord.xy = v.vertex.xy * 0.5f + 0.5;
                o.texcoord1.xy = (((v.vertex.xy + 1.0f) * 0.5) * _MHYBloomUVTransform1.xy + _MHYBloomUVTransform1.zw);
                o.texcoord1.x -= _MainTex1_TexelSize.x*0.5;
                o.texcoord1.y += _MainTex1_TexelSize.y*0.5;
                o.texcoord2.xy = (((v.vertex.xy + 1.0f) * 0.5) * _MHYBloomUVTransform2.xy + _MHYBloomUVTransform2.zw);
                o.texcoord2.x -= _MainTex1_TexelSize.x*0.5;
                o.texcoord2.y += _MainTex1_TexelSize.y*0.5;
                o.texcoord3.xy = (((v.vertex.xy + 1.0f) * 0.5) * _MHYBloomUVTransform3.xy + _MHYBloomUVTransform3.zw);
                o.texcoord3.x -= _MainTex1_TexelSize.x*0.5;
                o.texcoord3.y += _MainTex1_TexelSize.y*0.5;
                #if UNITY_UV_STARTS_AT_TOP
                o.texcoord.y = 1 - o.texcoord.y;
                o.texcoord1.y = 1 - o.texcoord1.y;
                o.texcoord2.y = 1 - o.texcoord2.y;
                o.texcoord3.y = 1 - o.texcoord3.y;
                #endif
                return o;
            }

            float4 frag (MihoyoDefault i) : SV_Target
            {
                float4 tmp;
                float4 tmp2;
                float4 tmp3;
                tmp = _MainTex1.Sample(sampler_linear_clamp, i.texcoord1.xy );
                tmp2 = tmp * _MHYBloomBlurComposeWeights.yyyy;
                tmp3 = _MainTex0.Sample(sampler_linear_clamp, i.texcoord.xy);
                tmp2 = tmp3 * _MHYBloomBlurComposeWeights.xxxx + tmp2;
                tmp3 = _MainTex1.Sample(sampler_linear_clamp, i.texcoord2.xy);
                tmp2 = tmp3 * _MHYBloomBlurComposeWeights.zzzz + tmp2;
                tmp3 = _MainTex1.Sample(sampler_linear_clamp, i.texcoord3.xy);
                tmp2 = tmp3 * _MHYBloomBlurComposeWeights.wwww + tmp2;
                return tmp2;
            }
            
            ENDHLSL
        }
    }
}
