Shader "Hidden/HoyoToon/HonkaiImpact/MultipleGaussPassFilterHQ"
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
            float2 _scaler;
        ENDHLSL

        Pass
        {
            Name "Guassian 512"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord = tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    float4 tmp5;
                    float4 tmp6;
                    tmp0 = _scaler.xyxy * float4(-0.00592100015, -0.00592100015, -0.00231799996, -0.00231799996) + i.texcoord.xyxy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp0.zw);
                    tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp0.xy);
                    tmp3 = tmp1 * 0.297576994;
                    tmp4 = tmp2 * 0.00495600002 + tmp3;
                    tmp5 = _scaler.xyxy * float4(0.000742000004, 0.000742000004, 0.00406099996, 0.00406099996) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.636880994 + tmp4;
                    tmp4 = tmp1 * float4(0.0604300015, 0.0604300015, 0.0604300015, 0.0604300015) + tmp4;
                    tmp5.xy = _scaler.xy * float2(0.00781300012, 0.00781300012) + i.texcoord.xy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp4 = tmp1 * 0.000155000002 + tmp4;
                    return tmp4;
                }
            ENDHLSL
        }

        Pass
        {
            Name "Guassian 256"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord = tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    float4 tmp5;
                    float4 tmp6;
                    tmp0 = _scaler.xyxy * float4(-0.0238710009, -0.0238710009, -0.0163729992, -0.0163729992) + i.texcoord.xyxy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp0.zw);
                    tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp0.xy);
                    tmp3 = tmp1 * 0.0215799995;
                    tmp4 = tmp2 * 0.00079999998 + tmp3;
                    tmp5 = _scaler.xyxy * float4(-0.0090239998, -0.0090239998, -0.00179699995, -0.00179699995) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.172469005 + tmp4;
                    tmp4 = tmp1 * 0.417991012 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0053989999, 0.0053989999, 0.0126799997, 0.0126799997) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.311311007 + tmp4;
                    tmp4 = tmp1 * 0.070915997 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0201050006, 0.0201050006, 0.0273439996, 0.0273439996) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.00484499987 + tmp4;
                    tmp4 = tmp1 * 8.9000001e-05 + tmp4;
                    return tmp4;
                }
            ENDHLSL
        }

        Pass
        {
            Name "Guassian 128"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord = tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    float4 tmp5;
                    float4 tmp6;
                    tmp0 = _scaler.xyxy * float4(-0.0874369964, -0.0874369964, -0.0721379966, -0.0721379966) + i.texcoord.xyxy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp0.zw);
                    tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp0.xy);
                    tmp3 = tmp1 * 0.00116500002;
                    tmp4 = tmp2 * float4(9.10000017e-05, 9.10000017e-05, 9.10000017e-05, 9.10000017e-05) + tmp3;
                    tmp5 = _scaler.xyxy * float4(-0.0568859987, -0.0568859987, -0.0416759998, -0.0416759998) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.00918000005 + tmp4;
                    tmp4 = tmp1 * 0.0444319993 + tmp4;
                    tmp5 = _scaler.xyxy * float4(-0.0265030004, -0.0265030004, -0.0113540003, -0.0113540003) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.132256001 + tmp4;
                    tmp4 = tmp1 * 0.242351994 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.00378399994, 0.00378399994, 0.0189260002, 0.0189260002) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.273552001 + tmp4;
                    tmp4 = tmp1 * 0.190216005 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0340860002, 0.0340860002, 0.0492760018, 0.0492760018) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.0814540014 + tmp4;
                    tmp4 = tmp1 * 0.0214629993 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0645070001, 0.0645070001, 0.0797820017, 0.0797820017) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.00347599993 + tmp4;
                    tmp4 = tmp1 * 0.000345999986 + tmp4;
                    tmp5.xy = _scaler.xy * 0.09375 + i.texcoord.xy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp4 = tmp1 * 1.70000003e-05 + tmp4;
                    return tmp4;
                }
            ENDHLSL
        }

         Pass
        {
            Name "Guassian 64"
            HLSLPROGRAM
                #pragma vertex DownSampleVertex
                #pragma fragment frag

                struct DownSampleStruct
                {
                    float4 vertex : SV_POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                DownSampleStruct DownSampleVertex(AttributesDefault v)
                {
                    DownSampleStruct o = (DownSampleStruct)0;
                    o.vertex = float4(v.vertex.xy, 0, 1);
                    float2 tmp = v.vertex.xy * 0.5f + 0.5f;
                    #if UNITY_UV_STARTS_AT_TOP
                        tmp.y = 1.0f - tmp.y;
                    #endif
                    o.texcoord = tmp.xy;
                    return o;
                }

                float4 frag (DownSampleStruct i) : SV_Target
                {
                    float4 tmp0;
                    float4 tmp1;
                    float4 tmp2;
                    float4 tmp3;
                    float4 tmp4;
                    float4 tmp5; 
                    float4 tmp6;
                    tmp0 = _scaler.xyxy * float4(-0.285986006, -0.285986006, -0.255037993, -0.255037993) + i.texcoord.xyxy;
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp0.zw);
                    tmp2 = _MainTex.Sample(sampler_linear_clamp, tmp0.xy);
                    tmp3 = tmp1 * 0.000394000002;
                    tmp4 = tmp2 * 8.2999999e-05 + tmp3;
                    tmp5 = _scaler.xyxy * float4(-0.224099994, -0.224099994, -0.193170995, -0.193170995) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.00156400003 + tmp4;
                    tmp4 = tmp1 * 0.0052029998 + tmp4;
                    tmp5 = _scaler.xyxy * float4(-0.162249997, -0.162249997, -0.131336004, -0.131336004) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.0144809997 + tmp4;
                    tmp4 = tmp1 * 0.0337300003 + tmp4;
                    tmp5 = _scaler.xyxy * float4(-0.100428, -0.100428, -0.0695239976, -0.0695239976) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.0657500029 + tmp4;
                    tmp4 = tmp1 * 0.107267 + tmp4;
                    tmp5 = _scaler.xyxy * float4(-0.0386239998, -0.0386239998, -0.00772499992, -0.00772499992) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.146464005 + tmp4;
                    tmp4 = tmp1 * 0.167380005 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0231740009, 0.0231740009, 0.0540740006, 0.0540740006) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.160096005 + tmp4;
                    tmp4 = tmp1 * 0.128162995 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.0849760026, 0.0849760026, 0.115882002, 0.115882002) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.0858699977 + tmp4;
                    tmp4 = tmp1 * 0.0481519997 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.146792993, 0.146792993, 0.177709997, 0.177709997) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.0225980002 + tmp4;
                    tmp4 = tmp1 * 0.0088759996 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.208635002, 0.208635002, 0.239567995, 0.239567995) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.00291700009 + tmp4;
                    tmp4 = tmp1 * 0.000801999995 + tmp4;
                    tmp5 = _scaler.xyxy * float4(0.270511001, 0.270511001, 0.296875, 0.296875) + i.texcoord.xyxy;
                    tmp6 = _MainTex.Sample(sampler_linear_clamp, tmp5.xy);
                    tmp1 = _MainTex.Sample(sampler_linear_clamp, tmp5.zw);
                    tmp4 = tmp6 * 0.000184999997 + tmp4;
                    tmp4 = tmp1 * 2.49999994e-05 + tmp4;
                    return tmp4;
                }
            ENDHLSL
        }
    }
}