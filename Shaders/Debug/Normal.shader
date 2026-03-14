Shader "HoyoToon/Debug/Normal"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
       
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        ENDHLSL

        Pass
        {
            Name "BaseTest"
            Tags { "LightMode"="DebugNormal" }
            Cull Off
            HLSLPROGRAM
            #include "DebugCore.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            vertex_out vert(vertex_in v)
            {
                vertex_out o = (vertex_out)0;
                // Manual Matrix multiplication
                float4 worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, worldPos);
                o.normal = mul((float3x3)UNITY_MATRIX_M, v.normal);
                return o;
            }

            float4 frag(vertex_out i) : SV_Target
            {
                float4 col = float4(i.normal, 1.f);
                return col;
            }  
            ENDHLSL
        }   
    }
    FallBack "Diffuse"
}