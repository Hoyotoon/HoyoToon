Shader "HoyoToon/Honkai Star Rail/Character/EyeShadow"
{
    Properties
    {
        _EyeShadowColor ("Color", Color) = (1,1,1,1)
        [HideInInspector] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        [HideInInspector] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [HideInInspector] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _dissolvegroup ("Dissolve", Float) = 0
        [HideInInspector] _DissoveON ("Enable Dissolve", Float) = 0
        [HideInInspector] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
        [HideInInspector] _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
        [HideInInspector] _DissolveMap ("Dissolve Map", 2D) = "white" { }
        [HideInInspector] _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
        [HideInInspector] _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
        [HideInInspector] _DissolveDistortionIntensity ("", Float) = 0.01
        [HideInInspector] _DissolveOutlineSize1 ("", Float) = 0.05
        [HideInInspector] _DissolveOutlineSize2 ("", Float) = 0
        [HideInInspector] _DissolveOutlineOffset ("", Float) = 0
        [HideInInspector] _DissolveOutlineColor1 ("", Color) = (1,1,1,1)
        [HideInInspector] _DissolveOutlineColor2 ("", Color) = (0,0,0,0)
        [HideInInspector] _DissoveDirecMask ("", Float) = 2
        [HideInInspector] _DissolveMapAdd ("", Float) = 0
        [HideInInspector] _DissolveOutlineSmoothStep ("", Vector) = (0,0,0,0)
        [HideInInspector] _DissolveUV ("", Range(0, 1)) = 0
        [HideInInspector] _DissolveUVSpeed ("", Vector) = (0,0,0,0)
        [HideInInspector] _DissolveMask ("Dissolve Mask", 2D) = "white" { }
        [HideInInspector] _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
        [HideInInspector] _DissolvePosMaskPos ("", Vector) = (1,0,0,1)
        [HideInInspector] _DissolvePosMaskWorldON ("", Float) = 0
        [HideInInspector] _DissolvePosMaskRootOffset ("", Vector) = (0,0,0,0)
        [HideInInspector] _DissolvePosMaskFilpOn ("", Float) = 0
        [HideInInspector] _DissolvePosMaskOn ("", Float) = 0
        [HideInInspector] _DissolveMaskUVSet ("", Range(0, 1)) = 0
        [HideInInspector] _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        [HideInInspector] _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        [HideInInspector] _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        [HideInInspector] _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0 
        [HideInInspector] _ES_EffCustomLightPosition ("", Vector) = (0,0,0,0)

        [HideInInspector] _HideCharaParts ("Toggle Hide", Float) = 0
        [HideInInspector] [IntRange] _ShowPartID ("Show Part ID", Range(0, 256)) = 0
        _StencilRefA ("Stencil Ref", Float) = 26
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue" = "Geometry+2"
        }
        LOD 100

        Pass
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Blend DstColor Zero 
            Stencil
            {
                Ref [_StencilRefA]
            }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

            


            #include "UnityCG.cginc"
            #include "includes/HonkaiStarRail-header.hlsl"
            #include "includes/HonkaiStarRail-common.hlsl"

            float4 _EyeShadowColor;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
                float4 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 pos : SV_POSITION;
                float4 screenpos : TEXCOORD6;
                int hidden : TEXCOORD2; 
                float4 diss_uv   : TEXCOORD3;
                float4 diss_pos  : TEXCOORD4;
                float4 ws_pos    : TEXCOORD5;
            };

            v2f vert (appdata v)
            {
                v2f o;
                bool show = showpart(v.color.xy);
                o.hidden = show;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenpos = ComputeScreenPos(o.pos);
                o.pos = show ?  o.pos : float4(-99.0, -99.0, -99.0, 1.0);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.ws_pos = mul(unity_ObjectToWorld, v.vertex);

                dissolve_vertex_out(float2x2(v.uv.xy, v.uv2.xy), o.ws_pos, v.vertex, o.diss_uv, o.diss_pos);

                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {

                float dis_out;
                float dis_area;
                float dis_map;
                // first thing to take care of is the first portion of the dissolve:
                if(_DissoveON)
                {
                    if(_DissolveUseDirection) // use world space position to determine direction
                    {
                        dissolve_clip_world(i.ws_pos, dis_area, dis_out);
                        dis_area = 0.f;
                        dis_map = 1.f;
                    }
                    else // use the direction we calculated in the vertex shader
                    {
                        dissolve_clip_uv(i.diss_uv, i.diss_pos, i.uv, dis_area, dis_out, dis_map);
                    }
                }
                else
                { 
                    dis_out = 0.0f;
                    dis_area = 0.0f;
                    dis_map = 1.f;
                }

                // then the dither right after:
                float2 dither_screen_pos = floor((i.screenpos.xy / i.screenpos.w) * _ScreenParams);
                // float dither = 0;

                if(_UsingDitherAlpha)
                {
                    dither(i.diss_pos.z, dis_out, dither_screen_pos);
                    // ordered_dither(i.ws_pos.z * _DitherAlpha, dis_out, dither_screen_pos, dither);
                }

                // sample the texture
                fixed4 col = _MainTex.Sample(sampler_linear_repeat, i.uv);
                clip((col.w - _AlphaTestThreshold) + (1.0 - _EnableAlphaCutoff));
                
                return _EyeShadowColor;
            }
            ENDHLSL
        }
    }
}
