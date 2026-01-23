Shader "Hidden/ZenlessZoneZero/UberPost"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

            Texture2D _MainTex;
            Texture2D _NapBloomTex;
            float4 _MainTex_TexelSize;
            SamplerState sampler_linear_clamp;

            Texture2D _CharaLut;
            SamplerState sampler_CharaLut;
            float4 _CharaLutParams; // x: factor x, y: factor y, z: number of slices, w: unused
            float _BloomScaler;

            struct MihoyoAttributes
            {
                float3 vertex : POSITION;
            };

            struct MihoyoDefault
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            MihoyoDefault vert(MihoyoAttributes v)
            {
                MihoyoDefault o = (MihoyoDefault)0;
                o.vertex = float4(v.vertex.xy, 0, 1);
                float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    tmp.y = 1.0f - tmp.y;
                #endif
                o.texcoord.xy = tmp;
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
                float2 uv = inp.texcoord;

                float4 bloom = _NapBloomTex.SampleLevel(sampler_linear_clamp, uv, 0);
                float4 mainTex = _MainTex.SampleLevel(sampler_linear_clamp, uv, 0);

                float3 lut_uvw = saturate(log2(mainTex.zxy * 5.55f + 0.0478f) * 0.073f + 0.386f);
                
                float slices = _CharaLutParams.z; // Number of slices in the 3D LUT
                float2 tex_scale = _CharaLutParams.xy;

                float3 lut_sliced = lut_uvw * slices;
                
                float slice_index = floor(lut_sliced.x);

                float2 uv_a;
                uv_a.x = slice_index * tex_scale.y + lut_sliced.y * tex_scale.x + (tex_scale.x * 0.5f);
                uv_a.y = lut_sliced.z * tex_scale.y + (tex_scale.y * 0.5f);
                uv_a.y = 1  - uv_a.y; // Flip Y for Unity
                float3 lut_a = _CharaLut.SampleLevel(sampler_CharaLut, uv_a, 0.0f).xyz;

                float2 uv_b = uv_a + float2(tex_scale.y, 0.0f);
                float3 lut_b = _CharaLut.SampleLevel(sampler_CharaLut, uv_b, 0.0f).xyz;

                float lut_blend = lut_sliced.x - (slice_index);

                float3 lut_mixed = saturate(lerp(lut_a, lut_b, lut_blend));
                mainTex.xyz = lut_mixed;

                float3 tmp1;
                float3 tmp2;
                tmp1.xyz = bloom;
                tmp2.xyz = pow(abs(tmp1.xyz), 0.333333343);
                tmp2.xyz = tmp1.xyz - tmp2.xyz * 1.5 ;
                tmp1.xyz = (tmp1.xyz >= (float3)0.3 ? 1.f : 0.f) * tmp2.xyz + tmp1.xyz;
                tmp1.xyz = _BloomScaler.xxx * bloom.xyz;

                o.sv_target.xyz = mainTex + tmp1;
                o.sv_target.w = mainTex.w; // preserve original alpha
                
                return o;
            }
            
            ENDHLSL
        }
    }
}