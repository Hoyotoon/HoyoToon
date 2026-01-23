Shader "Hidden/HoyoToon/Utility/WhiteMask"
{
    Properties
    {
        _MaskColorValue ("Mask Color Value (0=Black, 1=White)", Float) = 0.0
        _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        _HideCharaParts ("Hide Chara Parts", Float) = 0
        _ShowPartID ("Show Part ID", Float) = 0
        _DissoveON ("Enable Dissolve", Float) = 0
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
        _DissolveMapAdd ("", Float) = 0
        _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0
        [HideInInspector] _DissolveShadowOff ("Disable Disolve Shadow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Hair" }
        LOD 100

        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            

            
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            SamplerState sampler_linear_repeat;

            
            float _HideCharaParts;
            int _ShowPartID;

            float _UsingDitherAlpha;
            float _UsingDitherAlphaArt;
            float _DitherAlpha;
            float _DITHER_FADE_IN;

            Texture2D _DissolveMap;
            Texture2D _DissolveMask;
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


            void dissolve_vertex_out(in float2x2 uv, in float4 ws, in float4 os, out float4 dis_uv, out float4 dis_pos)
            {
                // dissolve position:
                float3 dissolve_position = ws + (-_DissolvePosMaskPos.xyz);
                dissolve_position = lerp(os, dissolve_position, _DissolvePosMaskWorldON);

                float4 dissolve_uvs = lerp(uv[0], uv[1], _DissolveUV).xyxy;

                dis_uv = dissolve_uvs;
                dis_pos.x = dis_uv.x;

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

                dis_pos.y = dis_check ? dis_pos_mask.x : 1.0f;
                dis_pos.z = (_UsingDitherAlpha || _UsingDitherAlphaArt) ? _DitherAlpha : ws.z;
                dis_pos.w = 0.0f; 
            }

            void dissolve_clip_world(in float3 ws_pos, out float dissolve_area, out float dis_out)
            {
                float3 ws_dis = ws_pos + 0.000001f;
                ws_dis = ws_dis - _DissolveCenter.xyz;
                dissolve_area = dot(ws_dis, _DissolveDiretcionXYZ.xyz);
                int dis_clip = 0.0f < dissolve_area ? 2 : 0;
                if(dis_clip == 0) discard;
                dis_out = 0.000001f;
            }

            void dissolve_clip_uv(in float4 dissolve_uv, in float2 dissolve_pos, in float2 uv, out float dissolve_area, out float dis_out, out float map)
            {
                dis_out = 0.000003f;
                float diss_x = min(abs((-dissolve_pos.x) + _DissoveDirecMask), 1.0);
                float2 dis_uv = _DissolveUVSpeed.zw * _Time.yy + (dissolve_uv.zw + 0.000003f);
                float2 dis_map_a = _DissolveMap.Sample(sampler_linear_repeat, dis_uv);
                
                dis_uv = dis_map_a - 0.5f;
                dis_uv = _DissolveUVSpeed.xy * _Time.yy + (-dis_uv * _DissolveDistortionIntensity + dissolve_uv.xy);

                float dis_map_b = _DissolveMap.Sample(sampler_linear_repeat, dis_uv).z;
                map = dis_map_b;

                float2 mask_uv = lerp(uv, dissolve_uv.xy, _DissolveMaskUVSet);
                float3 mask = _DissolveMask.Sample(sampler_linear_repeat, mask_uv.xy);
                mask.xyz = dot(mask, _DissolveComponent);

                dissolve_area = (((mask.x * (diss_x.x * (dis_map_b.x + _DissolveMapAdd))) * dissolve_pos.y) * 1.01f + -0.01f);
                diss_x = (dissolve_area + (-_DissolveRate)) + 1.0f; 
                diss_x = max(floor(diss_x), 0.0f);
                if((int)diss_x == 0) discard;
            }

            void dissolve_outline(inout float4 color, in float dissolve_area, in float map)
            {
                float2 range = dissolve_area - ((_DissolveRate + _DissolveOutlineSize1) + (-_DissolveOutlineSize2));
                float2 smooth_inv = 1.0 / (_DissolveOutlineSmoothStep.xy + 0.001);
                float2 blend = saturate(range * smooth_inv);
                float3 base = color.xyz * map + _DissolveOutlineOffset;
                float3 color_a = base * _DissolveOutlineColor1.xyz;
                float3 color_diff = base * _DissolveOutlineColor2.xyz - color_a;
                float3 final = color_diff * blend.y + color_a;
                blend.x = blend.x + 1.0;
                blend.x = blend.x + (-_DissolveOutlineColor1.w);
                blend.x = saturate(blend.x);
                
                color.xyz = lerp(final, color.xyz, blend.x);
            }

            void dither(in float out_coord, in float dis_setting, in float2 screen_pos)
            {
                float4x4 identity;
                identity[0] = float4(1.0,0.0,0.0,0.0);
                identity[1] = float4(0.0,1.0,0.0,0.0);
                identity[2] = float4(0.0,0.0,1.0,0.0);
                identity[3] = float4(0.0,0.0,0.0,1.0);

                float4x4 bayer;
                bayer[0] = float4(1.0, 13.0, 4.0, 16.0);
                bayer[1] = float4(9.0, 5.0, 12.0, 8.0);
                bayer[2] = float4(3.0, 15.0, 2.0, 14.0);
                bayer[3] = float4(11.0, 7.0, 10.0, 6.0);
                
                if(dis_setting < _UsingDitherAlpha)
                {
                    if(out_coord < 0.94f)
                    {
                        uint2 screen = (uint2)screen_pos & (uint2)3;
                        
                        float4 lookup;
                        lookup.x = dot(bayer[0], identity[screen.y]);
                        lookup.y = dot(bayer[1], identity[screen.y]);
                        lookup.z = dot(bayer[2], identity[screen.y]);
                        lookup.w = dot(bayer[3], identity[screen.y]);

                        float dither_value = dot(lookup, identity[screen.x]);

                        dither_value = _DITHER_FADE_IN != 0 ? -dither_value + 16 : dither_value;
                        dither_value =  out_coord * 16.0 - dither_value;
                        dither_value = dither_value + 0.99f;
                        dither_value = max(floor(dither_value), 0.0);
                        if((int)dither_value == 0) discard;            
                    }
                }
            }

            bool showpart(in float2 vc)
            {
                int2 hidepart = int2(vc.yx * 256);
                hidepart = int2(uint(hidepart.x) & uint(_ShowPartID), uint(hidepart.y) & uint(_ShowPartID));
                int tmp = _HideCharaParts ? hidepart.x : 1;
                return (0 < tmp);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv1 : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 wspos : TEXCOORD1;
                float4 diss_uv   : TEXCOORD2;
                float4 diss_pos  : TEXCOORD3;
                float4 screenpos : TEXCOORD4;
            };

            float _MaskColorValue;
            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                float3 vl = mul(_WorldSpaceLightPos0.xyz, UNITY_MATRIX_V) * (1.f / ws_pos.w);
                float3 offset_pos = ((vl * .0015f) * float3(0,0,-3)) + v.vertex.xyz;
                o.uv = v.uv1;
                v.vertex.xyz = offset_pos;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.vertex = showpart(v.color.xy) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
                o.wspos = ws_pos;
                dissolve_vertex_out(float2x2(v.uv1.xy, v.uv2.xy), o.wspos, v.vertex, o.diss_uv, o.diss_pos);
                o.screenpos = ComputeScreenPos(o.vertex);
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
                        dissolve_clip_world(i.wspos, dis_area, dis_out);
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
                
                return float4(_MaskColorValue, 0, 1, 1.f);
            }
            ENDCG
        }
    }
}
