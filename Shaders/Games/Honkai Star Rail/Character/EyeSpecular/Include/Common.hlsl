bool showpart(in float vc)
{
    int hidepart = int(vc * 256);
    hidepart.x = int(uint(hidepart.x) & uint(_ShowPartID));
    int tmp = _HideCharaParts ? hidepart.x : 1;
    return (0 < tmp);
}

void get_id(in float light_y, out float id)
{

    float quantized = floor(light_y * 8.0);
    float scaled_value = quantized * 8.0;
    bool is_positive = scaled_value >= (-scaled_value);
    float2 sign_factors = (bool(is_positive)) ? float2(8.0, 0.125) : float2(-8.0, -0.125);
    float normalized = frac(sign_factors.y * quantized);
    id = normalized * sign_factors.x;
}


#if defined(_USE_DITHER)
void dither(float4 screen_pos)
{
    float2 dither_screen_pos = floor((screen_pos.xy / screen_pos.w) * _ScaledScreenParams);
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
        
        if( _UsingDitherAlpha)
        {
            // if(out_coord < 0.94f)
            // {
                uint2 screen = (uint2)dither_screen_pos & (uint2)3;
                
                float4 lookup;
                lookup.x = dot(bayer[0], identity[screen.y]);
                lookup.y = dot(bayer[1], identity[screen.y]);
                lookup.z = dot(bayer[2], identity[screen.y]);
                lookup.w = dot(bayer[3], identity[screen.y]);

                float dither_value = dot(lookup, identity[screen.x]);

                dither_value = _DITHER_FADE_IN != 0 ? 17-dither_value : dither_value;
                dither_value =  _DitherAlpha  * 17.0 - dither_value;
                dither_value = dither_value + 0.99f;
                dither_value = max(floor(dither_value), 0.0);
                if((int)dither_value == 0) discard;            
            // }
        }
}
void dither(float4 screen_pos, float out_coord, float dis_setting)
{   
    float2 dither_screen_pos = floor((screen_pos.xy / screen_pos.w) * _ScaledScreenParams);
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
                uint2 screen = (uint2)dither_screen_pos & (uint2)3;
                
                float4 lookup;
                lookup.x = dot(bayer[0], identity[screen.y]);
                lookup.y = dot(bayer[1], identity[screen.y]);
                lookup.z = dot(bayer[2], identity[screen.y]);
                lookup.w = dot(bayer[3], identity[screen.y]);

                float dither_value = dot(lookup, identity[screen.x]);

                dither_value = _DITHER_FADE_IN != 0 ? 17-dither_value : dither_value;
                dither_value =  _DitherAlpha  * 17.0 - dither_value;
                dither_value = dither_value + 0.99f;
                dither_value = max(floor(dither_value), 0.0);
                if((int)dither_value == 0) discard;            
            }
        }
}
#endif

#if defined(_DIRECTIONALDISSOLVE)
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
#endif