Shader "Hidden/ZenlessZoneZero/NapBloom"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float4 _UVTransformSource;
            float4 _UVTransformSource2;
            float4 _UVTransformSource3;
            float4 _UVTransformTarget;
            float2 _NapGaussScaler;
            float4 _NapBloomParams0;
            float4 _BloomBlurComposeWeights;

            Texture2D _MainTex;
            Texture2D _MainTex0;
            Texture2D _NapBloomAtlas;
            float4 _MainTex_TexelSize;
            SamplerState sampler_linear_clamp;

            struct MihoyoAttributes
            {
                float3 vertex : POSITION;
                uint vrtId : SV_VertexID;
            };

            struct MihoyoDefault
            {
                float4 vertex : SV_POSITION;
                float4 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
            };

            struct fout
            {
                float4 sv_target : SV_Target;
            };


        ENDHLSL

        Pass
        {
            Name "4-Tap 2x DownSampling"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o;
                
                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0, 1);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                tmp.xy = tmp * _UVTransformSource.xy + _UVTransformSource.zw;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.vertex.zw = float2(1.0, 1.0);
                o.texcoord = _MainTex_TexelSize.xyxy * float4(0.96, 0.25, 0.96, -0.25) + tmp.xyxy;
                o.texcoord1 = _MainTex_TexelSize.xyxy * float4(-0.96, -0.25, -0.96, 0.25) + tmp.xyxy;
                return o;
            }

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1; 
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.xy,0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.zw,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.xy,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.zw,0);
                tmp0 = tmp0 + tmp1;
                o.sv_target = tmp0 * float4(0.25, 0.25, 0.25, 0.25);
                return o;
            }

            ENDHLSL
        }

        Pass
        {
            Name "4x DownSampling"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o;
                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0, 1);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                tmp.xy = tmp * _UVTransformSource.xy + _UVTransformSource.zw;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.texcoord.xy = _MainTex_TexelSize.xy + tmp.xy;
                o.texcoord.zw = _MainTex_TexelSize.xy * float2(1,-1) + tmp.xy;
                o.texcoord1.xy = -_MainTex_TexelSize.xy + tmp.xy;
                o.texcoord1.zw = _MainTex_TexelSize.xy * float2(-1,1) + tmp.xy;
                return o;
            }

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.xy,0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.zw,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.xy,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.zw,0);
                tmp0 = tmp0 + tmp1;
                o.sv_target = tmp0 * float4(0.25, 0.25, 0.25, 0.25);
                return o;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "4x DownSampling Bright Pass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o;

                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0, 1);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                tmp.xy = tmp * _UVTransformSource.xy + _UVTransformSource.zw;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.texcoord.xy = _MainTex_TexelSize.xy + tmp.xy;
                o.texcoord.zw = _MainTex_TexelSize.xy * float2(1,-1) + tmp.xy;
                o.texcoord1.xy = -_MainTex_TexelSize.xy + tmp.xy;
                o.texcoord1.zw = _MainTex_TexelSize.xy * float2(-1,1) + tmp.xy;
                return o;
            }

            fout frag(MihoyoDefault inp)
            {
                fout o;
                // o.sv_target = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.xy);
                float4 tmp0;
                float4 tmp1;
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.xy,0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord.zw,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.xy,0);
                tmp0 = tmp0 + tmp1;
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, inp.texcoord1.zw,0);
                tmp0 = tmp0 + tmp1;
                tmp0.xyz = tmp0.xyz * float3(0.25, 0.25, 0.25) + -_NapBloomParams0.xxx;
                tmp0.w = tmp0.w * 0.25;
                o.sv_target.w = tmp0.w;
                tmp0.xyz = max(tmp0.xyz, float3(0.0, 0.0, 0.0));
                o.sv_target.xyz = tmp0.xyz * _NapBloomParams0.yyy;
                return o;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Compose Blurred with Secondary Bloom"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o = (MihoyoDefault)0;
                
                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0, 1);
                o.vertex.zw = float2(1.0, 1.0);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.texcoord = tmp.xyxy;
                return o;
            }

            float2 getBounds(float2 uv, float4 transform)
            {
                float2 epsilon = _MainTex_TexelSize.xy * 0.5; 

                float2 minBounds = transform.zw + epsilon;
                float2 maxBounds = (transform.xy + transform.zw) - epsilon;

                return min(max(uv, minBounds), maxBounds);
            }
            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                
                float2 epsilon = _MainTex_TexelSize.xy * 0.5; 
    
                float2 minBounds = _UVTransformSource.zw + epsilon;
                float2 maxBounds = (_UVTransformSource.xy + _UVTransformSource.zw) - epsilon;
                
                float4 tmp2 = float4(minBounds, maxBounds);

                tmp0.xy = inp.texcoord.xy * _UVTransformSource.xy + _UVTransformSource.zw;
                tmp0.xy = getBounds(tmp0.xy, _UVTransformSource);
                tmp0 = _NapBloomAtlas.SampleLevel(sampler_linear_clamp, tmp0.xy, 0);
                tmp0 = tmp0 * _BloomBlurComposeWeights.yyyy;
                tmp1 = _MainTex0.SampleLevel(sampler_linear_clamp, inp.texcoord.xy, 0);
                tmp0 = tmp1 * _BloomBlurComposeWeights.xxxx + tmp0;
                tmp1.xy = inp.texcoord.xy * _UVTransformSource2.xy + _UVTransformSource2.zw;
                tmp1.xy = getBounds(tmp1.xy, _UVTransformSource2);
                tmp1 = _NapBloomAtlas.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp0 = tmp1 * _BloomBlurComposeWeights.zzzz + tmp0;
                tmp1.xy = inp.texcoord.xy * _UVTransformSource3.xy + _UVTransformSource3.zw;
                tmp1.xy = getBounds(tmp1.xy, _UVTransformSource3);
                tmp1 = _NapBloomAtlas.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                o.sv_target = tmp1 * _BloomBlurComposeWeights.wwww + tmp0;
                return o;
            }

            ENDHLSL
        }
    }
}