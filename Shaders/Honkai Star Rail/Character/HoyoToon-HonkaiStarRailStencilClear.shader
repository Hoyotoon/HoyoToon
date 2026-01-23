Shader "HoyoToon/Honkai Star Rail/Character/Stencil Clear"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" 
               "Queue" = "Geometry+0"
             }
        LOD 100

        Pass
        {
            Name "Face Mask Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Stencil
            {
                // Ref 0
                // ReadMask 255
                // WriteMask 255
                // CompFront Always
                // CompBack Always
                // FailFront Keep
                // FailBack Keep
                // PassFront Replace
                // PassBack Keep
                // ZFailFront Keep
                // ZFailBack Keep
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _EyeShadowColor;
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(0.0, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
