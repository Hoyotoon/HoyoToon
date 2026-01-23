Shader "Hidden/HoyoToon/Genshin/MultiPassGaussFilter"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
            #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
            float2 _Scaler;
            float4 _UVTransformSource;
            float4 _UVTransformTarget;

            Texture2D _MainTex;
            float4 _MainTex_TexelSize;
            SamplerState sampler_linear_repeat;
            SamplerState sampler_linear_clamp;

            struct VaryingsMihoyo
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 texcoordClamp : TEXCOORD1;
            };

            VaryingsMihoyo VertUVTransformST(AttributesDefault v)
            {
                VaryingsMihoyo o = (VaryingsMihoyo)0;

                o.vertex = float4(v.vertex.xy * _UVTransformTarget.xy + _UVTransformTarget.zw, 0.0, 1.0);

                // 1. Calculate the base UV
                float2 uv = TransformTriangleVertexToUV(v.vertex.xy) * _UVTransformSource.xy + _UVTransformSource.zw;

                // 2. Define the base bounds from Source (Scale: xy, Translation: zw)
                float2 bMin = _UVTransformSource.zw;
                float2 bMax = _UVTransformSource.zw + _UVTransformSource.xy;

                // 3. Define the margin (Half-texel inset)
                float2 margin = _MainTex_TexelSize.xy * 0.5;

                // 4. Apply Y-Flip logic to both UV and Bounds
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                    
                    // Flip the bounds: The old Y-min becomes the new Y-max (distanced from the top)
                    float flippedYMin = 1.0 - bMax.y; 
                    float flippedYMax = 1.0 - bMin.y;
                    
                    // Assign flipped values
                    float2 finalMin = float2(bMin.x + margin.x, flippedYMin + margin.y);
                    float2 finalMax = float2(bMax.x - margin.x, flippedYMax - margin.y);
                #else
                    float2 finalMin = bMin + margin;
                    float2 finalMax = bMax - margin;
                #endif

                o.texcoord = uv;

                // 5. Pack into (minX, maxX, minY, maxY) to match your fragment swizzle:
                // tmp6 = max(tmp6, i.texcoordClamp.xzxz); -> x=minX, z=minY
                // tmp6 = min(tmp6, i.texcoordClamp.ywyw); -> y=maxX, w=maxY
                o.texcoordClamp = float4(finalMin.x, finalMax.x, finalMin.y, finalMax.y);
                return o;
            }
            
        ENDHLSL

        Pass
        {
            Name "DownSample"
            HLSLPROGRAM

            #pragma vertex VertUVTransformST
            #pragma fragment frag


            float4 frag (VaryingsMihoyo i) : SV_Target
            {   
                float4 tmp_uva = _MainTex_TexelSize.xyxy * float4(0.95, 0.25, 0.25, -0.95) +i.texcoord.xyxy;
                float4 tmp_uvb = _MainTex_TexelSize.xyxy * float4(-0.95, -0.25, -0.25, 0.95) +i.texcoord.xyxy;
                float2 uva = tmp_uva.xy;
                float2 uvb = tmp_uva.zw;
                float2 uvc = tmp_uvb.xy;
                float2 uvd = tmp_uvb.zw;
                float4 col = _MainTex.Sample(sampler_linear_clamp, uva);
                col += _MainTex.Sample(sampler_linear_clamp, uvb);
                col += _MainTex.Sample(sampler_linear_clamp, uvc);
                col += _MainTex.Sample(sampler_linear_clamp, uvd);
                col *= 0.25f;
                col.w = 1.0f;
                return col;
            } 
            
            ENDHLSL
        }
        
        Pass
        {
            Name "Gaussian 8x8"
            HLSLPROGRAM

            #pragma vertex VertUVTransformST
            #pragma fragment frag


            float4 frag (VaryingsMihoyo i) : SV_Target
            {   
                float4 tmp;
                float4 tmp2;
                float4 tmp3;
                float4 tmp4;
                float4 tmp5;
                float4 tmp6;
                float4 tmp7;
                float4 output;
                tmp = _Scaler.xyxy * float4(-6.13840723, -6.13840723, -4.21995306, -4.21995306) + i.texcoord.xyxy;
                tmp = max(tmp, i.texcoordClamp.xzxz);
                tmp = min(tmp, i.texcoordClamp.ywyw);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp.zw);
                tmp3 = _MainTex.Sample(sampler_linear_clamp, tmp.xy);
                tmp4 = tmp2 * float4(0.0285764094, 0.0285764094, 0.0285764094, 0.0285764094);
                tmp5 = tmp3 * float4(0.00155262905, 0.00155262905, 0.00155262905, 0.00155262905) + tmp4;
                tmp6 = _Scaler.xyxy * float4(-2.33108091, -2.33108091, -0.464892805, -0.464892805) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.180222899, 0.180222899, 0.180222899, 0.180222899) + tmp5;
                tmp5 = tmp2 * float4(0.395452797, 0.395452797, 0.395452797, 0.395452797) + tmp5;
                tmp6 = _Scaler.xyxy * float4(1.39604294, 1.39604294, 3.27197599, 3.27197599) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.304397702, 0.304397702, 0.304397702, 0.304397702) + tmp5;
                tmp5 = tmp2 * float4(0.0819592923, 0.0819592923, 0.0819592923, 0.0819592923) + tmp5;
                tmp6 = _Scaler.xyxy * float4(5.1754818, 5.1754818, 7.0, 7.0) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.00762319518, 0.00762319518, 0.00762319518, 0.00762319518) + tmp5;
                output = tmp2 * float4(0.000214895306, 0.000214895306, 0.000214895306, 0.000214895306) + tmp5;
                output.w = 1.0f;
                return output;
            } 
            
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian 14"
            HLSLPROGRAM

            #pragma vertex VertUVTransformST
            #pragma fragment frag


            float4 frag (VaryingsMihoyo i) : SV_Target
            {   
                
                float4 tmp;
                float4 tmp2;
                float4 tmp3;
                float4 tmp4;
                float4 tmp5;
                float4 tmp6;
                float4 tmp7;
                float4 output;
                // output.xyzw = _MainTex.Sample(sampler_linear_clamp, i.texcoord);
                tmp = _Scaler.xyxy * float4(-14.26509, -14.26509, -12.2933798, -12.2933798) + i.texcoord.xyxy;
                tmp = max(tmp, i.texcoordClamp.xzxz);
                tmp = min(tmp, i.texcoordClamp.ywyw);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp.zw);
                tmp3 = _MainTex.Sample(sampler_linear_clamp, tmp.xy);
                tmp4 = tmp2 * float4(0.000947096676, 0.000947096676, 0.000947096676, 0.000947096676);
                tmp5 = tmp3 * float4(0.000146320002, 0.000146320002, 0.000146320002, 0.000146320002) + tmp4;
                tmp6 = _Scaler.xyxy * float4(-10.3233604, -10.3233604, -8.35486317, -8.35486317) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.00464627193, 0.00464627193, 0.00464627193, 0.00464627193) + tmp5;
                tmp5 = tmp2 * float4(0.0172795802, 0.0172795802, 0.0172795802, 0.0172795802) + tmp5;
                tmp6 = _Scaler.xyxy * float4(-6.38767719, -6.38767719, -4.42154217, -4.42154217) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.0487266295, 0.0487266295, 0.0487266295, 0.0487266295) + tmp5;
                tmp5 = tmp2 * float4(0.104202203, 0.104202203, 0.104202203, 0.104202203) + tmp5;
                tmp6 = _Scaler.xyxy * float4(-2.45616198, -2.45616198, -0.491210788, -0.491210788) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.169012904, 0.169012904, 0.169012904, 0.169012904) + tmp5;
                tmp5 = tmp2 * float4(0.207937002, 0.207937002, 0.207937002, 0.207937002) + tmp5;
                tmp6 = _Scaler.xyxy * float4(1.47365403, 1.47365403, 3.43877792, 3.43877792) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.194056496, 0.194056496, 0.194056496, 0.194056496) + tmp5;
                tmp5 = tmp2 * float4(0.137373805, 0.137373805, 0.137373805, 0.137373805) + tmp5;
                tmp6 = _Scaler.xyxy * float4(5.40449619, 5.40449619, 7.37112093, 7.37112093) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.0737620592, 0.0737620592, 0.0737620592, 0.0737620592) + tmp5;
                tmp5 = tmp2 * float4(0.0300378799, 0.0300378799, 0.0300378799, 0.0300378799) + tmp5;
                tmp6 = _Scaler.xyxy * float4(9.33893299, 9.33893299, 11.3081703, 11.3081703) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.00927573629, 0.00927573629, 0.00927573629, 0.00927573629) + tmp5;
                tmp5 = tmp2 * float4(0.00217165402, 0.00217165402, 0.00217165402, 0.00217165402) + tmp5;
                tmp6 = _Scaler.xyxy * float4(13.2790203, 13.2790203, 15.0, 15.0) + i.texcoord.xyxy;
                tmp6 = max(tmp6, i.texcoordClamp.xzxz);
                tmp6 = min(tmp6, i.texcoordClamp.ywyw);
                tmp7 = _MainTex.Sample(sampler_linear_clamp, tmp6.xy);
                tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp6.zw);
                tmp5 = tmp7 * float4(0.000385392312, 0.000385392312, 0.000385392312, 0.000385392312) + tmp5;
                output = tmp2 * float4(3.87885193e-05, 3.87885193e-05, 3.87885193e-05, 3.87885193e-05) + tmp5;
                
                return output;
            } 
            
            ENDHLSL
        }
    }
}
