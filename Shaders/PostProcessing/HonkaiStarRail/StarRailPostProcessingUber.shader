Shader "Hidden/StarRail/PostProcessingUber"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            int _GaussTaps;
            float2 _BlurScale;

            float4 _GaussOffset[32];
            float _GaussWeights[32];
            float4 _GaussianUVClamp;
            float4 _GaussianUVTransform;
            float _GaussianLayerIntensity;
            float _BloomThreshold;
            float _BloomIntensity;
            float _BloomR;
            float _BloomG;
            float _BloomB;
            float4 _BloomAtlasUVTrans[4];
            float _EnableEffect0; 
            float4 _Vignette_Params1;
            float4 _Vignette_Params2;
            float3 _Lut2DTexParam;

            Texture2D _Lut2DTex;
            SamplerState sampler_Lut2DTex;


            Texture2D _MainTex;
            Texture2D _MainTex1;
            float4 _MainTex_TexelSize;
            SamplerState sampler_linear_clamp;
            // SamplerState sampler_linear_clamp;

            struct MihoyoDefault
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            MihoyoDefault DefaultVertex(AttributesDefault v)
            {
                MihoyoDefault o = (MihoyoDefault)0;
                o.vertex = float4(v.vertex.xy, 0, 1);
                float2 tmp = v.vertex + 1.f;
                o.texcoord = v.vertex.xy * 0.5f + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                o.texcoord.y = 1 - o.texcoord.y;
                #endif
                // o.texcoord.xy += _MainTex_TexelSize.xy * float2(0.5, -0.5);
                return o;
            }

        ENDHLSL
        

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            float4 frag (MihoyoDefault i) : SV_Target 
            {
                float3 bloom_threshold = (1.0f - float3(_BloomR, _BloomG, _BloomB)) * _BloomThreshold;
                float4 main = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                main.xyz = max(main.xyz - bloom_threshold, 0.0f);
                return main;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            float4 frag (MihoyoDefault i) : SV_Target 
            {
                float4 downUVA = _MainTex_TexelSize.xyxy * float4(-0.5, 0.5, -0.5, -0.5) + i.texcoord.xyxy;
                float4 downUVB = _MainTex_TexelSize.xyxy * float4(0.5, -0.5, 0.5, 0.5) + i.texcoord.xyxy;
                float4 tmp1;
                float4 tmp2;
                float4 tmp3;
                tmp1 = _MainTex.Sample(sampler_linear_clamp, downUVA.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, downUVA.zw);
                tmp3 = tmp1 + tmp2;
                tmp2 = _MainTex.Sample(sampler_linear_clamp, downUVB.xy);
                tmp3 = tmp3 + tmp2;
                tmp2 = _MainTex.Sample(sampler_linear_clamp, downUVB.zw);
                tmp3 = tmp3 + tmp2;
                return tmp3 / 4;
            }


            
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian H"
            // dynamic gaussian 1D blur pass
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            float4 frag(MihoyoDefault i) : SV_Target
            {
                float4 result = float4(0, 0, 0, 0); 

                for(int k = 0; k < _GaussTaps; k++) 
                {
                    float2 uv = _GaussOffset[k].xy * _BlurScale.xy + i.texcoord.xy;
                    result += _MainTex.Sample(sampler_linear_clamp, uv) * _GaussWeights[k];
                }

                return result * _GaussianLayerIntensity;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Gaussian V"
            // dynamic gaussian 1D blur pass
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            

            float4 frag(MihoyoDefault i) : SV_Target
            {
                float2 uv = i.texcoord * _GaussianUVTransform.xy + _GaussianUVTransform.zw;
                float4 result = float4(0, 0, 0, 0); 

                for(int k = 0; k < _GaussTaps; k++) 
                {
                    float2 offset = _GaussOffset[k].xy * _BlurScale.xy;
                    
                    float2 tmp = uv.xy + offset;
                    tmp = max(tmp, _GaussianUVClamp.xy);
                    tmp = min(tmp, _GaussianUVClamp.zw);

                    result += _MainTex.Sample(sampler_linear_clamp, tmp) * _GaussWeights[k];
                }

                return result * _GaussianLayerIntensity;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Compose"
            // this is for taking all the faces from the atlas and composing them into 1 bloom texture
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            float4 frag(MihoyoDefault i) : SV_Target
            {
                float4 tmp;
                float2 tmp_uv;
                tmp_uv = i.texcoord.xy * _BloomAtlasUVTrans[0].xy + _BloomAtlasUVTrans[0].zw;
                tmp = _MainTex.Sample(sampler_linear_clamp, tmp_uv);
                tmp_uv = i.texcoord.xy * _BloomAtlasUVTrans[1].xy + _BloomAtlasUVTrans[1].zw;
                tmp += _MainTex.Sample(sampler_linear_clamp, tmp_uv);
                tmp_uv = i.texcoord.xy * _BloomAtlasUVTrans[2].xy + _BloomAtlasUVTrans[2].zw;
                tmp += _MainTex.Sample(sampler_linear_clamp, tmp_uv);
                tmp_uv = i.texcoord.xy * _BloomAtlasUVTrans[3].xy + _BloomAtlasUVTrans[3].zw;
                tmp += _MainTex.Sample(sampler_linear_clamp, tmp_uv);
                return tmp;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Compositee"
            HLSLPROGRAM
            #pragma vertex DefaultVertex
            #pragma fragment frag

            float noise_hash(in float2 screen)
            {
                float tmp;
                tmp = dot(screen, float2(0.0671, 0.0058));
                tmp = floor((frac(frac(tmp) * 52.9829178)) * 5.0) + -2.0;
                return tmp;
            }

            float4 frag (MihoyoDefault i) : SV_Target 
            {
                float2 uv = i.texcoord;
                float4 output = float4(0,0,0,0);
                float4 main = _MainTex.Sample(sampler_linear_clamp, uv);
                float4 bloom = _MainTex1.Sample(sampler_linear_clamp, uv);
                output.xyz = bloom * _BloomIntensity + main;
                output.w = main.w;
                if(_EnableEffect0 > 0)
                {
                    
                    float2 screenuv = uv;
                    // vignette
                    float2 d = abs(uv.xy + (-_Vignette_Params2.xy)) * _Vignette_Params2.zz;
                    d.x *=  _Vignette_Params1.w;

                    float base = max(1 - dot(d, d), 0);

                    float v = exp2(log2(base) * _Vignette_Params2.w);

                    float3 vignette = lerp(_Vignette_Params1.xyz, 1.0f, v);

                    float3 vignette_dither;

                    float dither_scale = 0.0039f;

                    float r_noise = noise_hash(uv);
                    float g_noise = noise_hash(uv + float2(2.08299994, 4.8670001));
                    float b_noise = noise_hash(uv + float2(4.16599989, 9.73400021));
                    vignette_dither.x = r_noise * dither_scale + vignette.x;
                    vignette_dither.y = g_noise * dither_scale + vignette.y;
                    vignette_dither.z = b_noise * dither_scale + vignette.z;

                    output.xyz = output.xyz * vignette_dither;
                }

                float3 lut_uvw = saturate(log2(output.zxy * 5.55f + 0.0478f) * 0.073f + 0.386f);
                
                float slices = _Lut2DTexParam.z; // Number of slices in the 3D LUT
                float2 tex_scale = _Lut2DTexParam.xy;

                float3 lut_sliced = lut_uvw * slices;
                float slice_index = floor(lut_sliced.x);

                float2 uv_a;
                uv_a.x = slice_index * tex_scale.y + lut_sliced.y * tex_scale.x + (tex_scale.x * 0.5f);
                uv_a.y = lut_sliced.z * tex_scale.y + (tex_scale.y * 0.5f);
                float3 lut_a = _Lut2DTex.SampleLevel(sampler_Lut2DTex, uv_a, 0.0f).xyz;

                float2 uv_b = uv_a + float2(tex_scale.y, 0.0f);
                float3 lut_b = _Lut2DTex.SampleLevel(sampler_Lut2DTex, uv_b, 0.0f).xyz;

                float lut_blend = lut_sliced.x - (slice_index);

                float3 lut_mixed = saturate(lerp(lut_a, lut_b, lut_blend));
                output.xyz = lut_mixed;

                return output;
                // main.xyz += bloom.xyz * _BloomIntensity;
                // return main;
            }
            ENDHLSL    
        }
    }
}
