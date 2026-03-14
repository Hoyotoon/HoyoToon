Shader "HoyoToon/Utility/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "QUEUE" = "Geometry" "RenderType" = "Opaque" }
        Cull [_CullMode]
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
        ENDHLSL

        Pass 
        {
            Name "LightingGBuffer"
            Tags { "LightMode"="LightingGBuffer" }
            
            
            HLSLPROGRAM
            CBUFFER_START(CRP_PerCamera)
                
                
                
            CBUFFER_END
            #ifndef UNITY_MATRIX_MV
                #define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
            #endif

            #ifndef UNITY_MATRIX_MVP
                #define UNITY_MATRIX_MVP mul(unity_MatrixVP, unity_ObjectToWorld)
            #endif

            #define unity_MatrixMV  UNITY_MATRIX_MV
            #define unity_MatrixMVP UNITY_MATRIX_MVP

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct buffer_out
            {
                float4 forward  : SV_Target0; // Albedo
                float4 normal  : SV_Target1; // Normals
                float4 ssrMask : SV_Target2; // SSR Mask (R8)
            };

            Varyings vert_base(Attributes v)
            {
                Varyings output;
                float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                float4 position =  mul(unity_MatrixMVP, v.vertex);
                float4 custom_position = mul(unity_MatrixVP, ws_pos);
                output.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.vertex = custom_position;
                
                return output;
            }

            buffer_out frag_base(Varyings input)
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                buffer_out output;
                output.forward = texColor * _Color;
                output.forward.a = 1.0f;
                output.normal = float4(0, 0, 1, 0);
                output.ssrMask = float4(1, 0, 0, 0);
                return output;
            }


            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 

        Pass 
        {
            Name "ForwardEmission"
            Tags { "LightMode"="ForwardEmission" }
            
            HLSLPROGRAM
            CBUFFER_START(CRP_PerCamera)
                
                
                
            CBUFFER_END
            #ifndef UNITY_MATRIX_MV
                #define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
            #endif

            #ifndef UNITY_MATRIX_MVP
                #define UNITY_MATRIX_MVP mul(unity_MatrixVP, unity_ObjectToWorld)
            #endif

            #define unity_MatrixMV  UNITY_MATRIX_MV
            #define unity_MatrixMVP UNITY_MATRIX_MVP
            

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct buffer_out
            {
                float4 forward  : SV_Target0; // Albedo
                float4 normal  : SV_Target1; // Normals
                float4 ssrMask : SV_Target2; // SSR Mask (R8)
            };

            Varyings vert_base(Attributes v)
            {
                Varyings output;
                float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                float4 position =  mul(unity_MatrixMVP, v.vertex);
                float4 custom_position = mul(unity_MatrixVP, ws_pos);
                output.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.vertex = custom_position;
                
                return output;
            }

            buffer_out frag_base(Varyings input)
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                buffer_out output;
                output.forward = texColor * _Color;
                output.forward.a = 1.0f;
                output.normal = float4(0, 0, 1, 0);
                output.ssrMask = float4(1, 0, 0, 0);
                return output;
            }


            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 
    }
     CustomEditor "LWGUI.LWGUI" 
     
    FallBack "Diffuse"
}
