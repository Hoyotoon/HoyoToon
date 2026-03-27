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

float4 TransformWorldToCharacterSelfShadowCoord(float4x4 shadowMatrix, float3 positionWS)
{
    return mul(shadowMatrix, float4(positionWS, 1.0));
}

float SampleURPMainLightShadow(float3 positionWS)
{
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
        return MainLightRealtimeShadow(shadowCoord);
    #else
        return 1.0;
    #endif
}


int material_region(float lightmap_alpha)
{
    int material = 0;
    lightmap_alpha = floor(8.0f * lightmap_alpha);
    if(lightmap_alpha > 0.5 && lightmap_alpha < 1.5 )
    {
        material = 1;
    } 
    else if(lightmap_alpha > 1.5f && lightmap_alpha < 2.5f)
    {
        material = 2;
    } 
    else if(lightmap_alpha > 2.5f && lightmap_alpha < 3.5f)
    {
        material = 3;
    } 
    else
    {
        material = (lightmap_alpha > 6.5f && lightmap_alpha < 7.5f) ? 7 : 0;
        material = (lightmap_alpha > 5.5f && lightmap_alpha < 6.5f) ? 6 : material;
        material = (lightmap_alpha > 4.5f && lightmap_alpha < 5.5f) ? 5 : material;
        material = (lightmap_alpha > 3.5f && lightmap_alpha < 4.5f) ? 4 : material;
    }

    return material;
}

void get_light(out float3 light)
{
    float tmp = (_ES_CharacterDisableLocalMainLight + 1.0) - abs(_DisableCharacterLocalLight);
    light = 0.5f < tmp ? _MainLightPosition.xyz : _CharacterLocalMainLightPosition.xyz;
    light.xyz = 0.5f < _IsMonster ? _ES_MonsterLightDir.xyz : light.xyz;
    light = lerp(light, _CustomMainLightDir, _CustomMainLightDir.www);
    light = (bool(0.5f < _IsMonster)) ? _ES_MonsterLightDir.xyz : light.xyz;
}

void get_light(out float3 light, out float3 color)
{
    float tmp = (_ES_CharacterDisableLocalMainLight + 1.0) - abs(_DisableCharacterLocalLight);
    light = 0.5f < tmp ? _MainLightPosition.xyz : _CharacterLocalMainLightPosition.xyz;
    light.xyz = 0.5f < _IsMonster ? _ES_MonsterLightDir.xyz : light.xyz;
    light = lerp(light, _CustomMainLightDir, _CustomMainLightDir.www);
    light = (bool(0.5f < _IsMonster)) ? _ES_MonsterLightDir.xyz : light.xyz;
    color = (0.5f < tmp) ? _MainLightColor.xyz : _CharacterLocalMainLightColor.xyz;
}
float3 apply_light_dark(float3 ramp, float id, float area, float factor)
{
    factor.x = factor.x * area + -0.8f;
    factor.x = saturate(factor.x * 10.0000038);
    area = factor.x * -2.0 + 3.0;
    factor.x = factor.x * factor.x;
    float dark_area = (-area) * factor.x + 1.0f;
    dark_area = dark_area * _NewLocalLightStrength.z;
    float rounded_id = round(id);

    float3 darkend_b = lerp(1.0f, _CharacterLocalMainLightDark1.xyz, dark_area.xxx) * ramp.xyz;

    float3 darkend_a = ramp.xyz * lerp(1.0f, _CharacterLocalMainLightDark.xyz, dark_area.xxx);
    
    ramp.xyz = rounded_id == 0.0 ? darkend_b.xyz : darkend_a.xyz;
    return ramp;
}

float3 apply_level_adjust(float3 ramp, float id)
{
    float rounded_id = round(id);
    bool ramp_check = 2.9f < (dot(ramp.xyz, float3(1.0, 1.0, 1.0)));
    bool isSkin = (rounded_id == 0) ? 0.0 : 1.0;

    float3 skin_light = _ES_LevelSkinLightColor.www * _ES_LevelSkinLightColor.xyz;
    float3 high_light = _ES_LevelHighLightColor.www * _ES_LevelHighLightColor.xyz;
    skin_light.xyz = lerp(skin_light * 2.f, high_light.xyz * 2.f, isSkin.xxx);

    float3 skin_shadow = _ES_LevelSkinShadowColor.www * _ES_LevelSkinShadowColor.xyz;
    float3 high_shadow = _ES_LevelShadowColor.www * _ES_LevelShadowColor.xyz;

    skin_shadow.xyz = lerp(skin_shadow * 2.0f, high_shadow.xyz * 2.0f, isSkin.xxx);
    float3 level_tmp = ramp.xyz - _ES_LevelMid;
    float2 level = float2(_ES_LevelHighLight, _ES_LevelMid) - (float2(_ES_LevelMid, _ES_LevelShadow));
    level_tmp.xyz = level_tmp.xyz / level.xxx;
    level_tmp.xyz = saturate(level_tmp.xyz * 0.5f + 0.5f);
    skin_light.xyz = skin_light.xyz * level_tmp.xyz;

    level_tmp.xyz = _ES_LevelMid - ramp.xyz;
    level_tmp.xyz = level_tmp.xyz / level.yyy;
    level_tmp.xyz = saturate((-level_tmp.xyz) * 0.5 + 0.5f);
    skin_shadow.xyz = skin_shadow.xyz * level_tmp.xyz;
    skin_light.xyz = (bool(ramp_check)) ? skin_light.xyz : skin_shadow.xyz;
    ramp.xyz = (0.5 < _ES_LEVEL_ADJUST_ON) ? skin_light.xyz : ramp.xyz;
    return ramp;
}

float3 apply_shadow_boost(float3 ramp, float factor)
{
    if (_ShadowBoost)
    {
        float range = smoothstep(0.0f, 0.15f, factor);
        range = lerp(_ShadowBoostVal + 1.0f, 1.0f, range);
        ramp = ramp * range;
    }
    return ramp;
}

// --- Shadow Helpers ---
float3 ApplyShadowNormalBias(float3 pos_ws, float3 normal_ws, float bias) {
    float inv_ndotl = 1.0 - saturate(dot(_MainLightPosition.xyz, normal_ws));
    float scale = inv_ndotl * bias;
    return pos_ws - (normal_ws * scale);
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

#if defined(_HEIGHTLERP)
    void heightlightlerp(float4 pos, inout float4 color)
    {
        float height = pos.y + (-_CharaWorldSpaceOffset.y);

        // Use the world space height
        float wsHeight = height;

        // Bottom region calculation
        float bottomThreshold = max(_ES_HeightLerpBottom, 0.001);
        float bottomFactor = wsHeight / bottomThreshold;
        bottomFactor = clamp(bottomFactor, 0.0, 1.0);
        
        // Smooth step for bottom region
        float bottomSmoothStep = (bottomFactor * -2.0) + 3.0;
        bottomFactor = bottomFactor * bottomFactor;
        float bottomBlend = 1.0 - (bottomSmoothStep * bottomFactor);
        
        // Top region calculation
        float topFactor = (wsHeight - _ES_HeightLerpTop) * 2.0;
        topFactor = clamp(topFactor, 0.0, 1.0);
        
        // Smooth step for top region
        float topSmoothStep = (topFactor * -2.0) + 3.0;
        topFactor = topFactor * topFactor;
        float topBlend = topFactor * topSmoothStep;
        
        // Middle region calculation
        float middleBlend = 1.0 - bottomBlend;
        middleBlend = middleBlend - (topSmoothStep * topFactor);
        middleBlend = clamp(middleBlend, 0.0, 1.0);
        
        // Blend the three colors based on height regions
        float3 bottomColor = bottomBlend * _ES_HeightLerpBottomColor.xyz * _ES_HeightLerpBottomColor.www;
        float3 middleColor = middleBlend * _ES_HeightLerpMiddleColor.xyz * _ES_HeightLerpMiddleColor.www;
        float3 topColor = topBlend * _ES_HeightLerpTopColor.xyz * _ES_HeightLerpTopColor.www;
        
        // Combine all three color regions
        float3 finalColor = bottomColor + middleColor + topColor;
        finalColor = clamp(finalColor, 0.0, 1.0);
        
        // Apply to the input color
        color.xyz = finalColor * color.xyz;
    }
#endif