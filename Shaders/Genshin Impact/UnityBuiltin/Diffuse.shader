Shader "HoyoToon/Genshin Impact/UnityBuiltin/Legacy Shaders/Diffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB)", 2D) = "white" { }
        _MaterialShadowBias ("Shadow Bias", Range(0, 1)) = 0
        [Header(Element View)] _ElementViewEleID ("Element ID", Float) = 0
        [Header(Emission)] [KeywordEnum(None, Normal, Time)] Emission_Type ("Emission Type", Float) = 0
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionStrength ("Emission Intensity", Range(0, 20)) = 10
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color  : COLOR0;
            };

            struct output
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR0;
            };

            Texture2D _MainTex;
            SamplerState sampler_linear_repeat;
            float4 _MainTex_ST;
            
            float4 _Color;

            output vert(input v)
            {
                output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal =  normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            fixed4 frag(output i) : SV_Target
            {
                float4 main = (_MainTex.Sample(sampler_linear_repeat, i.uv) * _Color) * _LightColor0;
                float ndotl = max(dot(i.normal, _WorldSpaceLightPos0), 0.0f);
                
                float4 output = 1.0f;
                output.xyz = main.xyz * ndotl;
                               
                
                return output;
            }
            ENDCG
        }
    }
}