Shader "HoyoToon/Honkai Star Rail/Character/Depth Caster"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _dissolvegroup ("Dissolve", Float) = 0
        _DissoveON ("Enable Dissolve", Float) = 0
        _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
        _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
        _DissolveMap ("Dissolve Map", 2D) = "white" { }
        _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
        _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
        _DissolveDistortionIntensity ("", Float) = 0.01
        _DissolveOutlineSize1 ("", Float) = 0.05
        _DissolveOutlineSize2 ("", Float) = 0
        _DissolveOutlineOffset ("", Float) = 0
        _DissolveOutlineColor1 ("", Color) = (1,1,1,1)
        _DissolveOutlineColor2 ("", Color) = (0,0,0,0)
        _DissoveDirecMask ("", Float) = 2
        _DissolveMapAdd ("", Float) = 0
        _DissolveOutlineSmoothStep ("", Vector) = (0,0,0,0)
        _DissolveUV ("", Range(0, 1)) = 0
        _DissolveUVSpeed ("", Vector) = (0,0,0,0)
        _DissolveMask ("Dissolve Mask", 2D) = "white" { }
        _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
        _DissolvePosMaskPos ("", Vector) = (1,0,0,1)
        _DissolvePosMaskWorldON ("", Float) = 0
        _DissolvePosMaskRootOffset ("", Vector) = (0,0,0,0)
        _DissolvePosMaskFilpOn ("", Float) = 0
        _DissolvePosMaskOn ("", Float) = 0
        _DissolveMaskUVSet ("", Range(0, 1)) = 0
        _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0 
        _ES_EffCustomLightPosition ("", Vector) = (0,0,0,0)

        _HideCharaParts ("Toggle Hide", Float) = 0
        [IntRange] _ShowPartID ("Show Part ID", Range(0, 256)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull Off
            // Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

            
            #define DEPTH_SHADER

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "UnityShaderVariables.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityInstancing.cginc"
            #include "includes/HonkaiStarRail-header.hlsl"
            #include "includes/HonkaiStarRail-common.hlsl"

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
                o.pos = show ?  o.pos : float4(-99.0, -99.0, -99.0, 1.0);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.ws_pos = mul(unity_ObjectToWorld, v.vertex);

                dissolve_vertex_out(float2x2(v.uv.xy, v.uv2.xy), o.ws_pos, v.vertex, o.diss_uv, o.diss_pos);




                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
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

                // sample the texture
                fixed4 col = _MainTex.Sample(sampler_linear_repeat, i.uv);
                clip((col.w - _AlphaTestThreshold) + (1.0 - _EnableAlphaCutoff));
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                if(i.hidden == 0)
                {
                    discard;
                }
                return 0;
            }
            ENDHLSL
        }
    }
}
