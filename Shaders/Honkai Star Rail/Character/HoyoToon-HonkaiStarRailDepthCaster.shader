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
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
            Texture2D _DissolveMap;
            Texture2D _DissolveMask;
            SamplerState sampler_linear_repeat;
            SamplerState sampler_linear_clamp;

            float _dissolvegroup;
            float _DissoveON;
            float _DissolveShadowOff;
            float _DissolveRate;
            float4 _DissolveST;
            float4 _DistortionST;
            float _DissolveDistortionIntensity;
            float _DissolveOutlineSize1;
            float _DissolveOutlineSize2;
            float _DissolveOutlineOffset;
            float4 _DissolveOutlineColor1;
            float4 _DissolveOutlineColor2;
            float _DissoveDirecMask;
            float _DissolveMapAdd;
            float4 _DissolveOutlineSmoothStep;
            float _DissolveUV;
            float4 _DissolveUVSpeed;
            float4 _DissolveComponent;
            float4 _DissolvePosMaskPos;
            float _DissolvePosMaskWorldON;
            float4 _DissolvePosMaskRootOffset;
            float _DissolvePosMaskFilpOn;
            float _DissolvePosMaskOn;
            float _DissolveMaskUVSet;
            float _DissolveUseDirection;
            float4 _DissolveCenter;
            float4 _DissolveDiretcionXYZ;
            float _DissolvePosMaskGlobalOn;
            float4 _ES_EffCustomLightPosition;

            float _HideCharaParts;
            float _ShowPartID;
            
            #define DEPTH_SHADER

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "UnityShaderVariables.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityInstancing.cginc"
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

            Texture2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                int2 hidepart = int2(v.color.yx * 256);
                hidepart = int2(uint(hidepart.x) & uint(_ShowPartID), uint(hidepart.y) & uint(_ShowPartID));
                int tmp =  _HideCharaParts ? hidepart.x : 1;
                o.hidden = tmp;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.pos = ((0 < tmp)) ?  o.pos : float4(-99.0, -99.0, -99.0, 1.0);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.ws_pos = mul(unity_ObjectToWorld, v.vertex);

                // dissolve position:
                float3 dissolve_position = o.ws_pos + (-_DissolvePosMaskPos.xyz);
                dissolve_position = lerp(v.vertex, dissolve_position, _DissolvePosMaskWorldON);

                float4 dissolve_uvs = lerp(v.uv.xy, v.uv2.xy, _DissolveUV).xyxy;

                o.diss_uv = dissolve_uvs;
                o.diss_pos.x = o.diss_uv.x;

                float3 dis_weird = lerp(dissolve_position, _ES_EffCustomLightPosition, _DissolvePosMaskGlobalOn) - _DissolvePosMaskRootOffset;

                float3 dis_camera = -unity_ObjectToWorld[3].xyz + _ES_EffCustomLightPosition.xyz;
                float3 dis_camtwo = (float3)(_DissolvePosMaskWorldON) * (-unity_ObjectToWorld[3].xyz) + _DissolvePosMaskPos.xyz;

                float3 dissolve_global = lerp(dis_camtwo, dis_camera, _DissolvePosMaskGlobalOn);

                float3 dissolve_norm = normalize(dissolve_global);

                float dis_check = dot(abs(dissolve_global), (float3)1.0f) >= 0.001f;

                float idk = dot(dissolve_norm, dis_weird);

                float dis_pos_mask = max(_DissolvePosMaskPos.w, 0.00999999978);
                float dissolve_abs = abs(idk) + dis_pos_mask;
                dis_pos_mask = dis_pos_mask + dis_pos_mask;
                dis_pos_mask = dissolve_abs / dis_pos_mask;
                dissolve_abs = dissolve_abs.x * -2.0 + 1.0;
                dis_pos_mask.x = _DissolvePosMaskFilpOn * dis_pos_mask + dis_pos_mask.x;
                dis_pos_mask.x = dis_pos_mask.x + (-_DissolvePosMaskOn);
                dis_pos_mask.x = dis_pos_mask.x + 1.0;
                dis_pos_mask.x = clamp(dis_pos_mask.x, 0.0, 1.0);

                o.diss_pos.y = dis_check ? dis_pos_mask.x : 1.0f;
                o.diss_pos.zw = 0.0f; 



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
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                if(i.hidden == 0)
                {
                    discard;
                }
                return col;
            }
            ENDHLSL
        }
    }
}
