Shader "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess"
{
    Properties
    {
        _MainTex("MainTex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }


        HLSLINCLUDE
            int _GaussTaps;
            float2 _BlurScale;

            float4 _GaussOffset[32];
            float _GaussWeights[32];
            float4 _GaussianUVClamp;
            float4 _GaussianUVTransform;
            float _GaussianLayerIntensity;
            float4 _BloomAtlasUVTrans[4];
            SamplerState sampler_linear_clamp;
        ENDHLSL

        Pass
        {
            Name "ChromaticAberration"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float _ChromaticAberration_Amount;
            float4 _ChromaFilterA;
            float4 _ChromaFilterB;
            float4 _ChromaFilterC;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {
                float alpha = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0).a;
                float2 uv_expanded = i.uv * 2 - 1;
                float dist = dot(uv_expanded, uv_expanded);
                float2 thirds = -float2(1.0/3.0, 2.0/3.0);
                float4 offset = ((uv_expanded.xyxy * dist.xxxx) * _ChromaticAberration_Amount.xxxx) * thirds.xxyy + i.uv.xyxy;
                float4 main_a = _MainTex.SampleLevel(sampler_MainTex, offset.xy, 0) * _ChromaFilterA;
                float4 main_b = _MainTex.SampleLevel(sampler_MainTex, offset.zw, 0) * _ChromaFilterB;
                float4 main_c = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0) * _ChromaFilterC;
                float4 color = (main_a + main_b + main_c) / (_ChromaFilterA + _ChromaFilterB + _ChromaFilterC);
                color.a = alpha;
                return color;
			}
            

            ENDHLSL
        }

        Pass
        {
            Name "RadialBlurWithChromaticAberration"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float4 _RadialParam;
            float _ChromaticAberration_Amount;
            float4 _ChromaFilterA;
            float4 _ChromaFilterB;
            float4 _ChromaFilterC;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {

                float blurStep = _RadialParam.x;
                float sampleCount = _RadialParam.y;
                float2 blurCenterUv = _RadialParam.zw;

                float2 stepOffset = (blurCenterUv - i.uv) * blurStep;
                float2 sampleUV = i.uv;
                float3 accumulatedColor = float3(0.0, 0.0, 0.0);
                
                float2 uv_expanded = i.uv * 2 - 1;
                float dist = dot(uv_expanded, uv_expanded);
                float2 thirds = -float2(1.0/3.0, 2.0/3.0);
                float4 offset = ((uv_expanded.xyxy * dist.xxxx) * _ChromaticAberration_Amount.xxxx);

                float alpha = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0).a;
                

                // Match the original shader: sample at most 10 taps, stopping when tap index reaches sampleCount.
                [unroll]
                for (int tap = 0; tap < 10; tap++)
                {
                    if ((float)tap >= sampleCount)
                    {
                        break;
                    }
                    float4 main_a = _MainTex.SampleLevel(sampler_MainTex, sampleUV.xy, 0) * _ChromaFilterC;
                    float4 uv_offset = offset * thirds.xxyy + sampleUV.xyxy;
                    float4 main_b = _MainTex.SampleLevel(sampler_MainTex, uv_offset.xy, 0) * _ChromaFilterA;
                    float4 main_c = _MainTex.SampleLevel(sampler_MainTex, uv_offset.zw, 0) * _ChromaFilterB;
                    sampleUV = sampleUV + stepOffset;
                    accumulatedColor += (main_a + main_b + main_c);
                }

                float3 blurredColor = accumulatedColor / sampleCount;
                float4 finalColor = float4(blurredColor / (_ChromaFilterA + _ChromaFilterB + _ChromaFilterC), alpha);
                return finalColor;
			}
            

            ENDHLSL     
        }

        Pass
        {
            Name "RadialBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _RadialParam;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {
                float blurStep = _RadialParam.x;
                float sampleCount = _RadialParam.y;
                float2 blurCenterUv = _RadialParam.zw;

                float2 stepOffset = (blurCenterUv - i.uv) * blurStep;
                float2 sampleUV = i.uv;
                float3 accumulatedColor = float3(0.0, 0.0, 0.0);

                float alpha = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0).a;

                // Match the original shader: sample at most 10 taps, stopping when tap index reaches sampleCount.
                [unroll]
                for (int tap = 0; tap < 10; tap++)
                {
                    if ((float)tap >= sampleCount)
                    {
                        break;
                    }

                    accumulatedColor += _MainTex.Sample(sampler_MainTex, sampleUV).xyz;
                    sampleUV += stepOffset;
                }

                float3 blurredColor = accumulatedColor / sampleCount;
                return float4(blurredColor, alpha);
			}
            ENDHLSL
        }

        Pass
        {
            Name "DirectionalBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _DirectionalBlurParams;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float4 accumulatedColor = float4(0.0, 0.0, 0.0, 0.0);
                float count = 0;
                float alpha = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0).a;

                // [unroll]
                for(int tap = 0; tap < _DirectionalBlurParams.z; tap++)
                {
                    float2 uv = _DirectionalBlurParams.xy * count + i.uv.xy;
                    float4 main = _MainTex.Sample(sampler_MainTex, uv);
                    accumulatedColor += main;
                    count += 1;
                }
                accumulatedColor *= (1 /_DirectionalBlurParams.z);
                return float4(accumulatedColor.rgb, alpha);
			}
            ENDHLSL
        }

        Pass
        {
            Name "Downsample"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _BlitTexture_TexelSize;



            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 uvOffa : TEXCOORD1;
                float4 uvOffb : TEXCOORD2;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv.xy = GetFullScreenTriangleTexCoord(vertexID).xy;
                o.uvOffa = _BlitTexture_TexelSize.xyxy *  float4(-0.5, 0.5, -0.5, -0.5) + o.uv.xyxy;
                o.uvOffb = _BlitTexture_TexelSize.xyxy *  float4(0.5, -0.5, 0.5, 0.5) + o.uv.xyxy;
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float4 one   =  _BlitTexture.Sample(sampler_BlitTexture, i.uvOffa.xy);
                float4 two   =  _BlitTexture.Sample(sampler_BlitTexture, i.uvOffa.zw);
                float4 three =  _BlitTexture.Sample(sampler_BlitTexture, i.uvOffb.xy);
                float4 four  =  _BlitTexture.Sample(sampler_BlitTexture, i.uvOffb.zw);
                return (one + two + three + four) / 4;
			}
            ENDHLSL
        }

        Pass
        {
            Name "BloomPrefilter"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float _BloomThreshold;
            float _BloomR;
            float _BloomG;
            float _BloomB;

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 uvOffa : TEXCOORD1;
                float4 uvOffb : TEXCOORD2;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv.xy = GetFullScreenTriangleTexCoord(vertexID).xy;
                o.uvOffa = _BlitTexture_TexelSize.xyxy *  float4(-0.5, 0.5, -0.5, -0.5) + o.uv.xyxy;
                o.uvOffb = _BlitTexture_TexelSize.xyxy *  float4(0.5, -0.5, 0.5, 0.5) + o.uv.xyxy;
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float3 thresh = 1.0f - float3(_BloomR, _BloomG, _BloomB);
                thresh *= _BloomThreshold;
                float4 main = _BlitTexture.Sample(sampler_BlitTexture, i.uv.xy);
                main.xyz = main.xyz - thresh;
                main.xyz = max(main.xyz, 0.0f);
                return main;
			}
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _DirectionalBlurParams;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float4 result = float4(0, 0, 0, 0); 
                float alpha = _BlitTexture.Sample(sampler_BlitTexture, i.uv).w;

                for(int k = 0; k < _GaussTaps; k++) 
                {
                    float2 uv = _GaussOffset[k].xy * _BlurScale.xy + i.uv.xy;
                    result += _BlitTexture.Sample(sampler_BlitTexture, uv) * _GaussWeights[k];
                }

                return result * _GaussianLayerIntensity;
			}
            ENDHLSL
        }

        Pass
        {
            Name "AtlasBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _DirectionalBlurParams;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float2 uv = i.uv * _GaussianUVTransform.xy + _GaussianUVTransform.zw;
                float4 result = float4(0, 0, 0, 0); 

                for(int k = 0; k < _GaussTaps; k++)   
                {
                    float2 offset = _GaussOffset[k].xy * _BlurScale.xy;
                    
                    float2 tmp = uv.xy + offset;
                    tmp = max(tmp, _GaussianUVClamp.xy);
                    tmp = min(tmp, _GaussianUVClamp.zw);

                    result += _BlitTexture.Sample(sampler_BlitTexture, tmp) * _GaussWeights[k];
                }

                float alpha = _BlitTexture.Sample(sampler_BlitTexture, uv).w;
                return result * _GaussianLayerIntensity;
			}
            ENDHLSL
        }

        Pass
        {
            Name "AtlasCombine"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);



            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {   
                float4 tmp;
                float2 tmp_uv;
                tmp_uv = i.uv.xy * _BloomAtlasUVTrans[0].xy + _BloomAtlasUVTrans[0].zw;
                tmp = _BlitTexture.Sample(sampler_BlitTexture, tmp_uv);
                tmp_uv = i.uv.xy * _BloomAtlasUVTrans[1].xy + _BloomAtlasUVTrans[1].zw;
                tmp += _BlitTexture.Sample(sampler_BlitTexture, tmp_uv);
                tmp_uv = i.uv.xy * _BloomAtlasUVTrans[2].xy + _BloomAtlasUVTrans[2].zw;
                tmp += _BlitTexture.Sample(sampler_BlitTexture, tmp_uv);
                tmp_uv = i.uv.xy * _BloomAtlasUVTrans[3].xy + _BloomAtlasUVTrans[3].zw;
                tmp += _BlitTexture.Sample(sampler_BlitTexture  , tmp_uv);
                // float alpha = _BlitTexture.Sample(sampler_BlitTexture, i.uv).w;
                return tmp;
			}
            ENDHLSL
        }

        Pass
        {
            Name "UberPost"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D_X(_HSRBloomTexture);
            SAMPLER(sampler_HSRBloomTexture);
            TEXTURE2D(_Lut2DTex);
            SAMPLER(sampler_Lut2DTex);

            float _BloomIntensity;
            float4 _Lut2DTexParam;


            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }


            float4 frag(v2f i) : SV_Target
            {
                float4 color = _MainTex.Sample(sampler_MainTex, i.uv);
                float4 bloom = _HSRBloomTexture.Sample(sampler_HSRBloomTexture, i.uv);
                color.xyz = bloom * float4(_BloomIntensity.xxx, 0.025) + color;

                float3 lut_uvw = saturate(log2(color.zxy * 5.55f + 0.0478f) * 0.073f + 0.386f);
                
                float slices = _Lut2DTexParam.z; // Number of slices in the 3D LUT
                float2 tex_scale = _Lut2DTexParam.xy;

                float3 lut_sliced = lut_uvw * slices;
                float slice_index = floor(lut_sliced.x);

                float2 uv_a;
                uv_a.x = slice_index * tex_scale.y + lut_sliced.y * tex_scale.x + (tex_scale.x * 0.5f);
                float uvY = lut_sliced.z * tex_scale.y + (tex_scale.y * 0.5f);
                uv_a.y = (_Lut2DTexParam.w > 0.5f) ? (1.0f - uvY) : uvY;
                float3 lut_a = _Lut2DTex.SampleLevel(sampler_Lut2DTex, uv_a, 0.0f).xyz;

                float2 uv_b = uv_a + float2(tex_scale.y, 0.0f);
                float3 lut_b = _Lut2DTex.SampleLevel(sampler_Lut2DTex, uv_b, 0.0f).xyz;

                float lut_blend = lut_sliced.x - (slice_index);

                float3 lut_mixed = saturate(lerp(lut_a, lut_b, lut_blend));
                color.xyz = lut_mixed;
                color.w = color.w;

                return color;
			}
            

            ENDHLSL
        }
    }
}