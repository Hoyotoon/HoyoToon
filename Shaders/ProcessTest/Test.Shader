Shader "Custom/VRCTestShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        /*#VRC_VRChatEmission ("VRChat Emission", Color) = (0,1,0,1)
        _VRChatIntensity ("VRChat Intensity", Range(0,10)) = 1#VRC_END*/
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "VRCTestInclude.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            
            /*#VRCfloat4 _VRChatEmission;
            float _VRChatIntensity;#VRC_END*/

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                /*#VRC// VRChat specific emission calculation
                col.rgb += _VRChatEmission.rgb * _VRChatIntensity;
                col.rgb *= GetVRChatMultiplier();#VRC_END*/
                
                return col;
            }
            ENDCG
        }
    }
}