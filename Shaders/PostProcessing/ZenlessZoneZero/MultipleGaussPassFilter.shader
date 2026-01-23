Shader "Hidden/ZenlessZoneZero/MultipleGaussPassFilter"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float4 _UVTransformSource;
            float4 _UVTransformTarget;
            float2 _NapGaussScaler;

            Texture2D _MainTex;
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
                float2 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
            };

            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o = (MihoyoDefault)0;
                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0, 1);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.texcoord.xy = tmp * _UVTransformSource.xy + _UVTransformSource.zw;
                float2 epsilon = _MainTex_TexelSize.xy * 0.5; 
    
                float2 minBounds = _UVTransformSource.zw + epsilon;
                float2 maxBounds = (_UVTransformSource.xy + _UVTransformSource.zw) - epsilon;
                
                o.texcoord1 = float4(minBounds, maxBounds);
                return o;
            }
        ENDHLSL

        Pass
        {
            Name "fragGaussianFilter1D_6"

            HLSLPROGRAM
            #pragma  vertex vert
            #pragma fragment frag

            struct fout
            {
                float4 sv_target : SV_Target;
            };

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                float4 tmp2;
                tmp0 = _NapGaussScaler.xyxy * float4(-7.15882, -7.15882, -5.227498, -5.227498) + inp.texcoord.xyxy;
                tmp0 = max(tmp0, inp.texcoord1.xyxy);
                tmp0 = min(tmp0, inp.texcoord1.zwzw);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.zw, 0);
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.xy, 0);
                tmp1 = tmp1 * float4(0.01512981, 0.01512981, 0.01512981, 0.01512981);
                tmp0 = tmp0 * float4(0.00096487, 0.00096487, 0.00096487, 0.00096487) + tmp1;
                tmp1 = _NapGaussScaler.xyxy * float4(-3.314762, -3.314762, -1.417412, -1.417412) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.1009583, 0.1009583, 0.1009583, 0.1009583) + tmp0;
                tmp0 = tmp1 * float4(0.2889, 0.2889, 0.2889, 0.2889) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(0.4722446, 0.4722446, 2.364548, 2.364548) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.3564036, 0.3564036, 0.3564036, 0.3564036) + tmp0;
                tmp0 = tmp1 * float4(0.1897708, 0.1897708, 0.1897708, 0.1897708) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(4.268898, 4.268898, 6.190808, 6.190808) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.04346563, 0.04346563, 0.04346563, 0.04346563) + tmp0;
                tmp0 = tmp1 * float4(0.00425363, 0.00425363, 0.00425363, 0.00425363) + tmp0;
                tmp1.xy = _NapGaussScaler * float2(8.0, 8.0) + inp.texcoord.xy;
                tmp1.xy = max(tmp1.xy, inp.texcoord1.xy);
                tmp1.xy = min(tmp1.xy, inp.texcoord1.zw);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                o.sv_target = tmp1 * float4(0.00015324, 0.00015324, 0.00015324, 0.00015324) + tmp0;
                return o;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "fragGaussianFilter1D_9"

            HLSLPROGRAM
            #pragma  vertex vert
            #pragma fragment frag

            struct fout
            {
                float4 sv_target : SV_Target;
            };

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                float4 tmp2;
                tmp0 = _NapGaussScaler.xyxy * float4(-4.095285, -4.095285, -2.222628, -2.222628) + inp.texcoord.xyxy;
                tmp0 = max(tmp0, inp.texcoord1.xyxy);
                tmp0 = min(tmp0, inp.texcoord1.zwzw);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.zw, 0);
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.xy, 0);
                tmp1 = tmp1 * float4(0.1334844, 0.1334844, 0.1334844, 0.1334844);
                tmp0 = tmp0 * float4(0.00570466, 0.00570466, 0.00570466, 0.00570466) + tmp1;
                tmp1 = _NapGaussScaler.xyxy * float4(-0.437803, -0.437803, 1.320767, 1.320767) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.501892, 0.501892, 0.501892, 0.501892) + tmp0;
                tmp0 = tmp1 * float4(0.3234969, 0.3234969, 0.3234969, 0.3234969) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(3.147974, 3.147974, 5.0, 5.0) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.03487847, 0.03487847, 0.03487847, 0.03487847) + tmp0;
                o.sv_target = tmp1 * float4(0.00054357, 0.00054357, 0.00054357, 0.00054357) + tmp0;
                return o;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "fragGaussianFilter1D_16"

            HLSLPROGRAM
            #pragma  vertex vert
            #pragma fragment frag

            struct fout
            {
                float4 sv_target : SV_Target;
            };

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                float4 tmp2;
                tmp0 = _NapGaussScaler.xyxy * float4(-14.26509, -14.26509, -12.29338, -12.29338) + inp.texcoord.xyxy;
                tmp0 = max(tmp0, inp.texcoord1.xyxy);
                tmp0 = min(tmp0, inp.texcoord1.zwzw);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.zw, 0);
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.xy, 0);
                tmp1 = tmp1 * float4(0.0009471, 0.0009471, 0.0009471, 0.0009471);
                tmp0 = tmp0 * float4(0.00014632, 0.00014632, 0.00014632, 0.00014632) + tmp1;
                tmp1 = _NapGaussScaler.xyxy * float4(-10.32336, -10.32336, -8.354863, -8.354863) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00464627, 0.00464627, 0.00464627, 0.00464627) + tmp0;
                tmp0 = tmp1 * float4(0.01727958, 0.01727958, 0.01727958, 0.01727958) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(-6.387677, -6.387677, -4.421542, -4.421542) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.04872663, 0.04872663, 0.04872663, 0.04872663) + tmp0;
                tmp0 = tmp1 * float4(0.1042022, 0.1042022, 0.1042022, 0.1042022) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(-2.456162, -2.456162, -0.4912108, -0.4912108) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.1690129, 0.1690129, 0.1690129, 0.1690129) + tmp0;
                tmp0 = tmp1 * float4(0.207937, 0.207937, 0.207937, 0.207937) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(1.473654, 1.473654, 3.438778, 3.438778) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.1940565, 0.1940565, 0.1940565, 0.1940565) + tmp0;
                tmp0 = tmp1 * float4(0.1373738, 0.1373738, 0.1373738, 0.1373738) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(5.404496, 5.404496, 7.371121, 7.371121) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.07376206, 0.07376206, 0.07376206, 0.07376206) + tmp0;
                tmp0 = tmp1 * float4(0.03003788, 0.03003788, 0.03003788, 0.03003788) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(9.338933, 9.338933, 11.30817, 11.30817) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00927574, 0.00927574, 0.00927574, 0.00927574) + tmp0;
                tmp0 = tmp1 * float4(0.00217165, 0.00217165, 0.00217165, 0.00217165) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(13.27902, 13.27902, 15.0, 15.0) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00038539, 0.00038539, 0.00038539, 0.00038539) + tmp0;
                o.sv_target = tmp1 * float4(0.00003879, 0.00003879, 0.00003879, 0.00003879) + tmp0;
                return o;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "fragGaussianFilter1D_20"

            HLSLPROGRAM
            #pragma  vertex vert
            #pragma fragment frag

            struct fout
            {
                float4 sv_target : SV_Target;
            };

            fout frag(MihoyoDefault inp)
            {
                fout o;
                float4 tmp0;
                float4 tmp1;
                float4 tmp2;
                tmp0 = _NapGaussScaler.xyxy * float4(-18.3031, -18.3031, -16.32244, -16.32244) + inp.texcoord.xyxy;
                tmp0 = max(tmp0, inp.texcoord1.xyxy);
                tmp0 = min(tmp0, inp.texcoord1.zwzw);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.zw, 0);
                tmp0 = _MainTex.SampleLevel(sampler_linear_clamp, tmp0.xy, 0);
                tmp1 = tmp1 * float4(0.00039339, 0.00039339, 0.00039339, 0.00039339);
                tmp0 = tmp0 * float4(0.00008281, 0.00008281, 0.00008281, 0.00008281) + tmp1;
                tmp1 = _NapGaussScaler.xyxy * float4(-14.34241, -14.34241, -12.36296, -12.36296) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00156376, 0.00156376, 0.00156376, 0.00156376) + tmp0;
                tmp0 = tmp1 * float4(0.0052015, 0.0052015, 0.0052015, 0.0052015) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(-10.38401, -10.38401, -8.405515, -8.405515) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.01447843, 0.01447843, 0.01447843, 0.01447843) + tmp0;
                tmp0 = tmp1 * float4(0.03372603, 0.03372603, 0.03372603, 0.03372603) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(-6.427385, -6.427385, -4.449542, -4.449542) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.06574705, 0.06574705, 0.06574705, 0.06574705) + tmp0;
                tmp0 = tmp1 * float4(0.1072673, 0.1072673, 0.1072673, 0.1072673) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(-2.471902, -2.471902, -0.4943747, -0.4943747) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.1464697, 0.1464697, 0.1464697, 0.1464697) + tmp0;
                tmp0 = tmp1 * float4(0.1673879, 0.1673879, 0.1673879, 0.1673879) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(1.48313, 1.48313, 3.460702, 3.460702) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.1601027, 0.1601027, 0.1601027, 0.1601027) + tmp0;
                tmp0 = tmp1 * float4(0.1281654, 0.1281654, 0.1281654, 0.1281654) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(5.438433, 5.438433, 7.416409, 7.416409) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.08586894, 0.08586894, 0.08586894, 0.08586894) + tmp0;
                tmp0 = tmp1 * float4(0.04814892, 0.04814892, 0.04814892, 0.04814892) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(9.394713, 9.394713, 11.37342, 11.37342) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.02259492, 0.02259492, 0.02259492, 0.02259492) + tmp0;
                tmp0 = tmp1 * float4(0.0088735, 0.0088735, 0.0088735, 0.0088735) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(13.35262, 13.35262, 15.33235, 15.33235) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00291622, 0.00291622, 0.00291622, 0.00291622) + tmp0;
                tmp0 = tmp1 * float4(0.00080199, 0.00080199, 0.00080199, 0.00080199) + tmp0;
                tmp1 = _NapGaussScaler.xyxy * float4(17.31269, 17.31269, 19.0, 19.0) + inp.texcoord.xyxy;
                tmp1 = max(tmp1, inp.texcoord1.xyxy);
                tmp1 = min(tmp1, inp.texcoord1.zwzw);
                tmp2 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.xy, 0);
                tmp1 = _MainTex.SampleLevel(sampler_linear_clamp, tmp1.zw, 0);
                tmp0 = tmp2 * float4(0.00018455, 0.00018455, 0.00018455, 0.00018455) + tmp0;
                o.sv_target = tmp1 * float4(0.0000251, 0.0000251, 0.0000251, 0.0000251) + tmp0;
                return o;
            }
            
            ENDHLSL
        }

    }
}