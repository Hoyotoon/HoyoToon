vertex_out vert_base(vertex_in v)
{
    vertex_out o = (vertex_out)0.f;
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float4 position =  mul(unity_MatrixMVP, v.vertex);
    float4 custom_position = mul(unity_MatrixVP, ws_pos);
    position = (0.5f < _EnableCustomCameraOverride) ? custom_position : position;
    position = showpart(v.color.y) ? position : float4(-99.0, -99.0, -99.0, 1.0);
    o.vertex = position;

    float4 uv = float4(_UVChannelFront.xx, _UVChannelBack.xx) < 0.5f ? v.uv.xyxy * _MainTex_ST.xyxy + _MainTex_ST.zwzw : v.uv1.xyxy;
    o.uv = uv;

    float4 tmp_color_a = (1 - _VertexColorSwitch) * float4(1.0, 1.0, 0.5, 0.5);
    float4 tmp_color_b = _VertexColorSwitch;
    tmp_color_a = v.color * tmp_color_b + tmp_color_a;
    o.color = tmp_color_a;
    float4 ss_tmp;
    ss_tmp.w = (position.y * _ProjectionParams.x) * 0.5f;
    ss_tmp.xz = position.xw * 0.5f;
    o.ss_pos.zw = position.zw;
    o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
    o.ws_pos = ws_pos;
    o.pos = position;

    o.view = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);

    float3 localLight = (_ES_CharacterDisableLocalMainLight + 1.0) - abs(_DisableCharacterLocalLight) > 0.5 
        ? _MainLightPosition.xyz 
        : _CharacterLocalMainLightPosition.xyz;
    
    float3 baseLightDir = _IsMonster > 0.5 ? _ES_MonsterLightDir.xyz : localLight;
    
    float3 customLightOffset = _CustomMainLightDir.xyz - baseLightDir;
    float3 lightdir = baseLightDir + _CustomMainLightDir.www * customLightOffset;
    lightdir = _IsMonster > 0.5 ? _ES_MonsterLightDir.xyz : lightdir;

    float3 object_up_axis = normalize(float3(unity_WorldToObject[0].y, unity_WorldToObject[1].y, unity_WorldToObject[2].y));
    float light_wrap = dot(object_up_axis.xyz, float3(-lightdir.z, -lightdir.x, lightdir.y));
    o.face_misc.z = saturate(light_wrap + 1.0);

    if(0.5<_UseUVChannel2){
        float3 main_face_uv;
        bool has_main_face_light;
        build_face_shadow_uv(lightdir.xyz, v.uv1.xy, main_face_uv, has_main_face_light);
        o.face_light.xyz = has_main_face_light ? main_face_uv : float3(0.0, 1.0, 0.0);
        o.face_misc.xy = v.uv1.xy * _MainTex_ST.xy + _MainTex_ST.zw;
        float3 local_face_uv;
        bool has_local_face_light;
        build_face_shadow_uv(_NewLocalLightDir.xyz, v.uv1.xy, local_face_uv, has_local_face_light);
        o.face_local.xyz = has_local_face_light ? local_face_uv : float3(0.0, 1.0, 0.0);
    } else {
        float3 main_face_uv;
        bool has_main_face_light;
        build_face_shadow_uv(lightdir.xyz, v.uv.xy, main_face_uv, has_main_face_light);
        o.face_light.xyz = has_main_face_light ? main_face_uv : float3(0.0, 1.0, 0.0);

        float3 local_face_uv;
        bool has_local_face_light;
        build_face_shadow_uv(_NewLocalLightDir.xyz, v.uv.xy, local_face_uv, has_local_face_light);
        o.face_local.xyz = has_local_face_light ? local_face_uv : float3(0.0, 1.0, 0.0);
        o.face_misc.xy = float2(0.0, 0.0);
    }

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_vertex_out(float2x2(v.uv.xy, v.uv1.xy), float4(o.ws_pos.xyz, 1.0f), v.vertex, o.diss_uv, o.diss_pos);
    #endif


    return o;
}

buffer_out frag_base(vertex_out i,  bool vface : SV_IsFrontFace) : SV_Target
{
    // initialize output buffer, this is to just guarantee no weird initialization issues
    buffer_out output = (buffer_out)0.0f;
    // change which dither to use depending on if the dissolve is active or not
    #if defined(_DIRECTIONALDISSOLVE)
        float dis_out;
        float dis_area;
        float dis_map;
        // first thing to take care of is the first porti on of the dissolve:
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
                dissolve_clip_uv(i.diss_uv, i.diss_pos, i.uv.xy, dis_area, dis_out, dis_map);
            }
        }
        else
        { 
            dis_out = 0.0f;
            dis_area = 0.0f;
            dis_map = 1.f;
        }
        #if defined(_USE_DITHER)
            dither(i.ss_pos, i.diss_uv.z, i.diss_pos);
        #endif
    #else
        #if defined(_USE_DITHER)
            dither(i.ss_pos);
        #endif
    #endif   

    float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv) * _Color;
    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, i.uv);

    float4 final_color = 1.f;
    final_color.xyz = diffuse.xyz ;

    float2 facemap_zw = _FaceMap.Sample(sampler_linear_repeat, i.face_light.xy).zw;
    float sdf_range = (facemap_zw.y < _BackShadowRange) ? 1.0f - facemap_zw.y : facemap_zw.y; 
    float facemap_x = _FaceMap.Sample(sampler_linear_repeat, i.uv.xy).x;
    float3 exp = _FaceExpression.Sample(sampler_linear_repeat, i.uv.xy).xyz;


    // this isnt working at the moment and needs to be relooked at when more of the shader is implemented: 
    // float shadow = get_main_light_shadow(i.ws_pos);
    /*
    sdw_combine = 1.0f - u_xlat87;
    u_xlat16_60.x = (-_MainLightShadowParams.x) + 1.0;
    sdw_combine = sdw_combine * _MainLightShadowParams.x + u_xlat16_60.x;
    u_xlatb87 = 0.0>=sdw_factor.z;
    u_xlatb30 = sdw_factor.z>=1.0;
    u_xlatb87 = u_xlatb87 || u_xlatb30;
    sdw_combine = (u_xlatb87) ? 1.0 : sdw_combine;
    u_xlat87 = _ES_Indoor * _ES_IndoorCharShadowAsCookie;
    u_xlat16_60.x = lerp(1.0f, u_xlat16_31, u_xlat87);
    sdw_combine = lerp(sdw_combine, 1.f, u_xlat87);
    sdw_combine =  1.0 - sdw_combine;
    sdw_combine = (-sdw_combine) * 1.25 + 1.0;
    sdw_combine = clamp(sdw_combine, 0.0, 1.0);
    sdw_combine = lerp(sdw_combine, 1.f, _ES_CharacterShadowFactor);
    */
    float indoor_sdw = 1.f; // u_xlat16_60.x
    float sdw_factor = smoothstep(0.89f, 0.9f,SampleURPMainLightShadow(i.ws_pos)); // sdw_combine
    // the reason for the above is to basically accumulate the shadows into this one variable
    float hair_shadow = 1.0f;
    if(_UseSelfShadow)
    {
        float3 local_light = (_ES_CharacterDisableLocalMainLight + 1.0) - abs(_DisableCharacterLocalLight) > 0.5 
            ? _MainLightPosition.xyz 
            : _CharacterLocalMainLightPosition.xyz;
        
        bool is_monster = 0.5 < _IsMonster;
        float3 base_light_dir = is_monster ? _ES_MonsterLightDir.xyz : local_light;
        
        float3 custom_light_offset = _CustomMainLightDir.xyz - base_light_dir;
        float3 final_light_dir = _CustomMainLightDir.www * custom_light_offset + base_light_dir;
        final_light_dir = is_monster ? _ES_MonsterLightDir.xyz : final_light_dir;
        
        float4 shadow_sample_offset_pos;
        shadow_sample_offset_pos.xyz = _SelfShadowSampleOffset * i.normal.xyz +  i.ws_pos.xyz;
        shadow_sample_offset_pos.w = i.ws_pos.w;

        
        float3 light_dir_normalized = -final_light_dir;
        light_dir_normalized.x = max(light_dir_normalized.x, 0.2);
        
        float3 light_dir_norm = normalize(light_dir_normalized).zxy;
        
        float4 offset_pos_with_light;
        offset_pos_with_light.xyz = light_dir_norm * 0.008f + shadow_sample_offset_pos;
        offset_pos_with_light.w = i.ws_pos.w;
        
        float4 projected_offset_final = mul(unity_MatrixVP, offset_pos_with_light);
        float4 projected_base_final = mul(unity_MatrixVP, shadow_sample_offset_pos);
        float2 shadow_uv = projected_base_final.xy;
        shadow_uv.y = lerp(projected_offset_final.y, projected_base_final.y, _ES_SelfShadowLerpHair);
        shadow_uv = shadow_uv / projected_base_final.w;
        shadow_uv = shadow_uv * float2(0.5, -0.5) + float2(0.5, 0.5);

        // bool shadow_uv_valid = all(shadow_uv >= 0.0.xx) && all(shadow_uv <= 1.0.xx);
        float sampled_depth = _CharacterHairShadowMap.SampleLevel(sampler_linear_clamp, shadow_uv, 0.0).x;
        float reference_depth = projected_base_final.z / projected_base_final.w;

        float sampled_linear_depth = 1.0 / (_ZBufferParams.x * sampled_depth + _ZBufferParams.y);
        float reference_linear_depth = 1.0 / (_ZBufferParams.x * reference_depth + _ZBufferParams.y);
        float self_shadow_depth_bias = _SelfShadowDepthOffset;
        bool hair_in_front_of_face = sampled_linear_depth + self_shadow_depth_bias < reference_linear_depth;

        hair_shadow = (hair_in_front_of_face) ? 0.0f : 1.0f;
    }
    else
    {
        hair_shadow = 1.0;
    }

    

    // get the main shader driving vectors: light, view, normal.
    // these are the basis for almost the entire shading
    float3 light;
    float3 color;
    get_light(light, color);

    float3 view = normalize(i.view);
    float3 normal = normalize(i.normal);

    float nose_ndotv = dot(normal, float3(view.x, view.y * 0.5f, view.z));
    nose_ndotv = min(pow(max(nose_ndotv, 0.001f), max(0.1f, _NoseLinePower * 8.0f)), 1.0f);
    nose_ndotv = (facemap_zw.x * nose_ndotv) > 0.1f;
    final_color.xyz =  lerp(final_color.xyz, _NoseLineColor.xyz, nose_ndotv);

    float lipLineRange = max(_LipLineFixMax - _LipLineFixStart, 0.01f);
    float viewDistance = length(i.view);
    
    bool isNearCamera = viewDistance < 2.0;
    bool isFarCamera = viewDistance > 5.0;
    float distanceScale = isNearCamera ? 1.2f : (isFarCamera ? 0.8f : 1.0f);
    
    bool isBeforeLipLineStart = viewDistance < _LipLineFixStart;
    float distanceFromStart = viewDistance - _LipLineFixStart;
    float lipLineFade = saturate(distanceFromStart / lipLineRange);
    lipLineFade = isBeforeLipLineStart ? 0.0f : lipLineFade;
    
    float scaledThickness = distanceScale - 1.0f;
    scaledThickness = _LipLineFixSC * scaledThickness + 1.0f;
    scaledThickness = scaledThickness * _LipLineFixScale;
    scaledThickness = max(scaledThickness, 0.00999999978f);
    scaledThickness = saturate(scaledThickness * _LipLineFixThrd);
    
    float lipLineWidth = lerp(0.04f, scaledThickness, lipLineFade);
    
    float colorMask = (i.color.y < 0.95f) ? i.color.y : 0.0f;
    float fadeOut = 1.0f - lipLineFade;
    fadeOut = colorMask - fadeOut;
    float applyLipLine = (lipLineWidth < fadeOut) ? 0.0f : 1.0f;
    
    final_color.xyz = lerp(_LipLinefixColor, final_color.xyz, applyLipLine);

    final_color.xyz = final_color.xyz * lerp(_EyeBaseShadowColor, 1.0f, i.color.x);

    float emission_thresh = (_EmissionThreshold < diffuse.w) ? saturate(diffuse.w - _EmissionThreshold / max(1.0f - _EmissionThreshold, 0.001f)) : 0.0;   
    final_color.xyz = (final_color.xyz * emission_thresh) * _EmissionIntensity + final_color;

    float ndotv = dot(normal, view);
    float fresnel_area = saturate((1 - abs(ndotv.x) + (-_FresnelBSI.x)) * (float(1.0) / _FresnelBSI.y));
    float3 fresnel = fresnel_area * _FresnelColor.xyz;
    fresnel.xyz = max(fresnel.xyz * _FresnelColorStrength, 0.0f);


    bool has_eye = facemap_x > 0.05;
    bool has_cheek = facemap_x > 0.1;
    
    bool exceeds_threshold = _ExMapThreshold < exp.x;
    float above_threshold = exp.x - _ExMapThreshold;
    float threshold_range = max(1.0 - _ExMapThreshold, 0.001);
    float intensity_above = above_threshold / threshold_range;
    
    bool below_threshold = _ExMapThreshold >= exp.x;
    float safe_threshold = max(_ExMapThreshold, 0.001);
    float intensity_below = exp.x / safe_threshold;
    float intensity = below_threshold ? intensity_below : 1.0;
    
    float spec_intensity = intensity_above * _ExSpecularIntensity * _ExCheekIntensity;
    float cheek_spec = exceeds_threshold ? spec_intensity : 0.0;
    
    float3 eye_delta = _ExEyeColor.xyz - _ExShadowColor.xyz;
    float3 eye_tint = has_eye ? eye_delta : float3(0.0, 0.0, 0.0);
    float3 eye_color = eye_tint + _ExShadowColor.xyz;
    
    float cheek_intensity = intensity * _ExCheekIntensity;
    float3 cheek_color = lerp(float3(1.0, 1.0, 1.0), _ExCheekColor, cheek_intensity);
    
    float shy_intensity = exp.y * _ExShyIntensity;
    cheek_color = lerp(cheek_color, _ExShyColor, shy_intensity);
    
    float shadow_intensity = exp.z * _ExShadowIntensity;
    float3 expr_color = lerp(cheek_color, eye_color, shadow_intensity);
    
    float3 final_expression = cheek_spec + expr_color;
    final_color.xyz = final_color.xyz * final_expression;
    
    float2 region_mask = has_cheek ? float2(1.0, 0.0) : float2(0.0, 1.0);

    float shadow_blend = smoothstep(max(i.face_light.z - _ShadowFeather, 0.000001f), min(i.face_light.z + _ShadowFeather, 0.999f), sdf_range);
    float light_intensity = max(_CharacterLocalMainLightPosition.w, 0.00999999978);
    bool is_eye_valid = facemap_x < 0.800000012;
    bool apply_eye_highlight = has_eye && is_eye_valid;
    float3 eye_highlight_mod = apply_eye_highlight ? float3(1.0, -1.0, 0.5) : float3(0.0, -0.0, 0.0);

    float region_contribution = region_mask.x + eye_highlight_mod.y;
    region_contribution = region_contribution + eye_highlight_mod.z;

    float angle_fade = smoothstep(_EyeShadowAngleMin - 0.36f, _EyeShadowAngleMin, i.face_misc.z);
    float shadow_blend_scaled = region_contribution * angle_fade + -1.0;
    region_contribution = region_contribution * shadow_blend_scaled + 1.0;

    float3 final_shadow_color = lerp(_ShadowColor, _EyeShadowColor, region_mask.xxx);

    float3 lightdark1 = lerp(1.0f, _CharacterLocalMainLightDark1, _NewLocalLightStrength.zzz);
    final_shadow_color = lightdark1 * final_shadow_color.xyz;
    shadow_blend.x = (region_contribution.x * sdw_factor) * (hair_shadow.x * shadow_blend.x);
    final_shadow_color.xyz = lerp(final_shadow_color, 1.0f, shadow_blend.x);

    // Level-based skin shading with light and shadow color adjustments
    float3 light_color = _ES_LevelSkinLightColor.www * _ES_LevelSkinLightColor.xyz;
    float3 light_color_doubled = light_color + light_color;
    float3 shadow_color = _ES_LevelSkinShadowColor.www * _ES_LevelSkinShadowColor.xyz;
    float3 shadow_color_doubled = shadow_color + shadow_color;
    float3 mid_color = lerp(shadow_color * 2.0f, light_color * 2.0f, region_mask.xxx);
    mid_color = mid_color - 1.0f;
    mid_color = _ES_LevelEyeShadowIntensity * mid_color + 1.0f;
    
    // Determine if shadow_color is above threshold
    above_threshold = light_intensity >= shadow_blend.x;
    
    // Calculate normalized shadow color in level space
    float3 norm_shadow = final_shadow_color.xyz - _ES_LevelMid;
    float2 level_ranges = float2(_ES_LevelHighLight, _ES_LevelMid) - float2(_ES_LevelMid, _ES_LevelShadow);
    norm_shadow = norm_shadow / level_ranges.xxx;
    norm_shadow = saturate(norm_shadow * 0.5f + 0.5f);
    
    // Light color contribution
    float3 light_contrib = (-light_color) * 2.0f + mid_color;
    light_contrib = shadow_blend.xxx * light_contrib + light_color_doubled;
    float3 light_result = norm_shadow * light_contrib;
    
    // Shadow color contribution
    float3 norm_mid = (-final_shadow_color.xyz) + _ES_LevelMid;
    norm_mid = norm_mid / level_ranges.yyy;
    norm_mid = saturate((-norm_mid) * 0.5f + 0.5f);
    float3 shadow_contrib = (-shadow_color) * 2.0f + mid_color;
    shadow_contrib = shadow_blend.xxx * shadow_contrib + shadow_color_doubled;
    float3 shadow_result = norm_mid * shadow_contrib;
    
    // Select based on threshold and apply level adjustment
    float3 level_color = above_threshold ? light_result : shadow_result;
    level_color = (_ES_LEVEL_ADJUST_ON) ? level_color : final_shadow_color.xyz;
    level_color = final_color.xyz * level_color;

    // Eye effect processing
    float eye_effect_alpha = saturate(eye_highlight_mod.x * _EyeEffectProcs);
    float3 eye_effect_base = eye_highlight_mod.x * _EyeEffectColor.xyz;
    float3 eye_effect_blend = eye_highlight_mod.x * _EyeEffectColor.xyz + (-level_color.xyz);
    eye_effect_blend = eye_effect_alpha * eye_effect_blend + level_color.xyz;
    float3 eye_effect_combined = eye_effect_base * eye_highlight_mod.x + eye_effect_blend;
    float eye_mod = eye_highlight_mod.x * 0.5 + 1.0;
    level_color = saturate(lerp(level_color, eye_effect_combined * eye_mod, eye_effect_alpha));
    
    final_color.xyz = level_color;

    if(_UseSpecialEye){
        // Apply texture coordinate transformation and animation
        float2 eye_shape_uv = i.uv.xy * _SpecialEyeShapeTexture_ST.yx + _SpecialEyeShapeTexture_ST.wz;
        float2 rotation_time = _Time.yy * _EyeCenter.zw;
        float2 centered_uv = eye_shape_uv + (-_EyeCenter.yx);
        
        // First rotation (around eye center)
        float cos_angle_1 = cos(rotation_time.x);
        float sin_angle_1 = sin(rotation_time.x);
        float2 rotated_1 = float2(
            centered_uv.y * sin_angle_1,
            centered_uv.x * sin_angle_1
        );
        float2 final_rotated_1 = float2(
            centered_uv.y * cos_angle_1 - rotated_1.x,
            centered_uv.x * cos_angle_1 + rotated_1.y
        );
        float2 eye_shape_uv_1 = final_rotated_1 + _EyeCenter.xy;
        
        // Second rotation
        float cos_angle_2 = cos(rotation_time.y);
        float sin_angle_2 = sin(rotation_time.y);
        float2 rotated_2 = float2(
            centered_uv.x * sin_angle_2,
            centered_uv.y * sin_angle_2
        );
        float2 final_rotated_2 = float2(
            centered_uv.y * cos_angle_2 - rotated_2.x,
            centered_uv.x * cos_angle_2 + rotated_2.y
        );
        float2 eye_shape_uv_2 = final_rotated_2 + _EyeCenter.xy;
        
        // Sample and blend eye shape textures
        float eye_shape_sample_1 = _SpecialEyeShapeTexture.Sample(sampler_linear_repeat, eye_shape_uv_1).x;
        float eye_shape_sample_2 = _SpecialEyeShapeTexture.Sample(sampler_linear_repeat, eye_shape_uv_2).y;
        
        float eye_detail_blend = eye_highlight_mod.x * (eye_shape_sample_2 * _EyeSPColor2.w);
        float3 eye_color_2 = lerp(final_color.xyz, _EyeSPColor2, eye_detail_blend);
        
        float eye_intensity_1 = eye_highlight_mod.x * (eye_shape_sample_1 * _EyeSPColor1.w);
        float3 eye_color_1 = lerp(eye_color_2.xyz, _EyeSPColor1, eye_intensity_1);
        
        float special_eye_intensity = region_mask.x + eye_highlight_mod.x;
        float intensity_scale = _SpecialEyeIntensity - 1.0;
        special_eye_intensity = special_eye_intensity * intensity_scale + 1.0;
        
        final_color.xyz = eye_color_1 * special_eye_intensity;
    }

    
    float local_sdf = _FaceMap.Sample(sampler_linear_repeat, i.face_local.xy).w;
    float2 local_ranges = saturate(i.face_local.zz + float2(-0.1f, 0.1f));
    local_sdf = smoothstep(local_ranges.x, local_ranges.y, local_sdf);
    
    float3 char_center =  normalize(i.ws_pos.xyz - _NewLocalLightCharCenter.xyz);
    float cdotnl = dot(char_center, _NewLocalLightDir.xyz) * 0.5 + 0.5;

    float localw_inv = 1.0f - _CharacterLocalMainLightColor1.w;
    localw_inv = (local_sdf * localw_inv) * 0.95f;
    localw_inv = _CharacterLocalMainLightColor.w * cdotnl + localw_inv;
    localw_inv = (_ES_CharacterDisableLocalMainLight ? 0 : 1) * localw_inv;
    float sdwtmp = hair_shadow * 0.5 + 0.5;
    localw_inv *= sdwtmp;

    float2 local_strngth = localw_inv.xx * _NewLocalLightStrength.xy;
    float3 blend_low = final_color.xyz + final_color.xyz;
    blend_low.xyz = blend_low.xyz * _CharacterLocalMainLightColor1.xyz;
    float3 blend_high = (-final_color.xyz) + float3(1.0, 1.0, 1.0);
    blend_high.xyz = blend_high.xyz + blend_high.xyz;
    blend_high.xyz = lerp(1.0f, _CharacterLocalMainLightColor1.xyz, blend_high.xyz);
      // _ES_CharacterDisableLocalMainLight

    // Branchless-style selection for color blending
    {
        float3 movcTemp = blend_low;
        movcTemp.x = (final_color.x < 0.5f) ? blend_low.x : blend_high.x;
        movcTemp.y = (final_color.y < 0.5f) ? blend_low.y : blend_high.y;
        movcTemp.z = (final_color.z < 0.5f) ? blend_low.z : blend_high.z;
        blend_low = movcTemp;
    }
    
    final_color.xyz = lerp(final_color.xyz, blend_low, local_strngth.xxx);

    float rim_ndotv = saturate(dot(normal.xyz, normalize(view.xyz - _RimShadowOffset.xyz)));

    float local_light_intensity = sdwtmp.x * local_strngth.y;
    float3 local_light_color = local_light_intensity * _CharacterLocalMainLightColor2.xyz + final_color.xyz;
    
    bool is_color_x_above_half = 0.5 < i.uv.x;
    float inverted_color_x = (-i.uv.x) + 1.0;
    float face_sample_x = (is_color_x_above_half) ? inverted_color_x : i.uv.x;
    float face_sample_y = i.uv.y;
    float face_rim_sample = _FaceMap.Sample(sampler_linear_repeat, float2(face_sample_x, face_sample_y)).w;
    
    float rim_inverse = (-rim_ndotv) + 1.0;
    rim_inverse = max(rim_inverse, 0.00100000005);
    rim_inverse = pow(rim_inverse, _RimShadowCt);
    rim_inverse = saturate(rim_inverse * _RimShadowWidth);
    rim_inverse = smoothstep(_RimShadowFeather, 1.0, rim_inverse);
    
    float3 rim_color_inverse = (-_RimShadowColor.xyz) + 1;
    float3 rim_shadow = rim_color_inverse * rim_inverse ;
    rim_shadow = rim_shadow * _RimShadowIntensity;
    
    float face_rim_inverse = (-face_rim_sample) + 1.0;
    rim_shadow = face_rim_inverse * (-rim_shadow) + 1;
    // rim_shadow = lerp(1.0f, face_rim_sample, face_rim_inverse);
    final_color.xyz = rim_shadow * local_light_color.xyz;
    final_color.xyz = color * final_color;

    #if defined(_USE_OVERHEATED)
        
        float heated_range = 1.0f - smoothstep(0.f, _HeatedHeight, _CharaWorldSpaceOffset.y - i.ws_pos.y);
        float range_a = smoothstep(1.0f, _HeatedThreshould, heated_range.x);
        float tmp_a = 1.0f - _HeatedThreshould;
        float tmp_b = 1.0 - (_HeatedThreshould * 2.0f);
        float range_b =  smoothstep(tmp_a, tmp_b, heated_range);
        float3 overheated = lerp(_HeatColor0, _HeatColor1, range_a);
        overheated = lerp(overheated, _HeatColor2, range_b);
        overheated.xyz = heated_range * overheated.xyz;
        overheated.xyz = overheated.xyz * _HeatInst + final_color.xyz;
        final_color.xyz = (_UseOverHeated == 1) ? overheated.xyz : final_color.xyz;
    #endif

    #if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos.xyz, 1.f), final_color);
    #endif

    float avatar_intensity = 1.0 - (_GlobalOneMinusAvatarIntensityEnable * _GlobalOneMinusAvatarIntensity);
    final_color.xyz = avatar_intensity.xxx * final_color.xyz;

    #if defined(_ENABLE_FOG)
        // Calculate distance from camera to world position
        float3 camera_to_world = i.ws_pos.xyz - _WorldSpaceCameraPos.xyz;
        float distance_to_camera = length(camera_to_world);
        
        // Set up fog parameters
        float fog_near = _ES_FogNear;
        float fog_far = _ES_FogFar;
        float height_fog_near = _ES_HeightFogFogNear;
        float height_fog_far = _ES_HeightFogFogFar;
        
        // Calculate fog ranges
        float fog_range = fog_far - fog_near;
        float height_fog_range = height_fog_far - height_fog_near;
        float adjusted_fog_near = fog_near + fog_range * _ES_FogCharacterNearFactor;
        float adjusted_height_fog_near = height_fog_near + height_fog_range * _ES_FogCharacterNearFactor;
        
        // Calculate fog density factors
        float fog_density_factor = saturate((distance_to_camera - adjusted_fog_near) / (fog_far - adjusted_fog_near));
        float height_fog_density_factor = saturate((distance_to_camera - adjusted_height_fog_near) / (height_fog_far - adjusted_height_fog_near));
        
        // Apply density
        fog_density_factor *= _ES_FogDensity;
        height_fog_density_factor *= _ES_HeightFogDensity;
        
        // Smooth fog curves
        float fog_curve = fog_density_factor * (1.0 - fog_density_factor);
        float height_fog_curve = height_fog_density_factor * (1.0 - height_fog_density_factor);
        float final_fog_density = fog_density_factor + fog_curve;
        float final_height_fog_density = height_fog_density_factor + height_fog_curve;
        
        // Calculate height fog influence
        float height_offset = dot(i.ws_pos.xyz, _ES_GlobalRotMatrix[3].xyz) - _ES_GlobalRotMatrix[3].w;
        float height_diff = (0.0 < _ES_HeightFogRange) ? (height_offset - _ES_HeightFogBaseHeight) : (_ES_HeightFogBaseHeight - height_offset);
        height_diff = max(height_diff, 0.0) / (abs(_ES_HeightFogRange) + 1.0);
        height_diff = saturate(height_diff);
        float height_fog_influence = 1.0 - height_diff;
        float adjusted_height_fog = saturate(height_fog_influence * _ES_HeightFogDensity - 1.0);
        
        // Transition fade
        float transition_fade = (1.0 - _ES_DisableFogTransition) * _ES_TransitionRate;
        
        // Sample fog gradient
        float2 fog_gradient_uv = float2(final_fog_density, transition_fade * 0.125 + _ES_FogColor);
        float3 fog_color = _ES_GradientAtlas.SampleLevel(sampler_linear_clamp, fog_gradient_uv, 0.0).xyz;
        
        // Apply fog to final color
        float fog_blend = saturate(final_fog_density);
        float3 fog_applied = lerp(final_color.xyz, fog_color, fog_blend);
        
        float fog_intensity = saturate(_ES_FogDensity - 1.0);
        final_color.xyz = lerp(fog_applied, fog_color, fog_intensity);
        
        // Sample height fog gradient
        float2 height_fog_gradient_uv = float2(final_height_fog_density, transition_fade * 0.125 + _ES_HeightFogColor);
        float3 height_fog_color = _ES_GradientAtlas.SampleLevel(sampler_linear_clamp, height_fog_gradient_uv, 0.0).xyz;
        
        // Apply height fog
        float3 height_fog_applied = height_fog_influence * height_fog_color;
        float3 blended_height_fog = lerp(final_color.xyz, height_fog_applied, final_height_fog_density);
        float3 combined_height_fog = height_fog_applied + blended_height_fog;
        float3 final_height_fog = combined_height_fog * adjusted_height_fog + blended_height_fog;
        
        // Calculate brightness influence
        float max_brightness = max(max(height_fog_color.z, height_fog_color.y), height_fog_color.x);
        float height_brightness_adjust = _ES_HeightFogAddAjust * (-max_brightness) + max_brightness;
        
        // Final composite
        float3 height_fog_contribution = height_fog_influence * final_height_fog;
        float3 blended_final = lerp(final_color.xyz, height_fog_contribution, final_height_fog_density);
        final_color.xyz = height_fog_influence * (final_height_fog - final_color.xyz) + final_color.xyz;
        final_color.xyz = lerp(final_color, blended_final, height_brightness_adjust);
    #endif

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif
    // final_color.xyz = hair_shadow;

    //output this shit
    output.forward.xyz = final_color;
    output.forward.w = diffuse.r;    
    // octahedral normal encoding for small gbuffer 
    float d = dot((float3)1.f, abs(i.normal.xyz));
    float2 n_uv = i.normal.xy / d;
    float2 f_uv = (1.0 - abs(n_uv.yx)) * (float2(n_uv.x >= 0.0 ? 1.0 : -1.0, n_uv.y >= 0.0 ? 1.0 : -1.0));
    float2 enc = (i.normal.z <= 0.0) ? f_uv : n_uv;
    float2 n_final = enc * 0.5 + 0.5;
    float z_val = diffuse.z * 127.0 + 128.0;
    float z_tr = floor(z_val);
    output.normal = float4(n_final, z_tr * 1.52590219e-005, diffuse.y);
    output.ssrMask = float4(1,1,1,1);
    return output;
}  

float4 frag_stencil(vertex_out i, bool vface : SV_IsFrontFace) : SV_TARGET
{
    float clip_mask;
    float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv);
    diffuse.w = 1.0f;
    if(_UseUVChannel2)
    {
        clip_mask = _FaceMap.Sample(sampler_linear_repeat, i.uv.zw).y;
    }
    else
    {
        clip_mask = _FaceMap.Sample(sampler_linear_repeat, i.uv.xy).y;
    }
    clip(clip_mask - _HairBlendSilhouette);
    return 0;
}

float4 frab_forward(vertex_out i, bool vface : SV_IsFrontFace) : SV_TARGET
{
    float2 screen_uv = i.ss_pos.xy / i.ss_pos.w;
    float4 gbuffer = _GBufferA.Sample(sampler_linear_clamp, screen_uv).xyzw;

    #if defined(_IS_DUALFACE)
        float2 uv = vface ? i.uv.xy : i.uv.zw;
        float4 color = vface ? _Color: _BackColor;
        float4 diffuse = _MainTex.Sample(sampler_linear_repeat, uv) * color;
        float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);
    #else
        float4 color = _Color;
        float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv) * color;
        float4 lightmap = _LightMap.Sample(sampler_linear_repeat, i.uv);
    #endif

    float id;
    get_id(lightmap.w, id);
    int array_index = material_region(lightmap.w);
    

    float3 light;
    float3 light_color;
    get_light(light, light_color);

    // temp shadow value for debugging: 
    float shadow_factor = 1.0f;

    float3 view = normalize(i.view);
    float3 normal = normalize(i.normal);
    float3 half_vec = normalize(view + light);

    #if defined(_USE_NORMAL_MAP)
        float3x3 tbn = float3x3(
            i.tangent.xyz,
            i.bitangent.xyz,
            i.normal
        );

        float3 normal_map;
        normal_map.xy = _NormalMap.Sample(sampler_linear_repeat, i.uv.xy).xy;
        normal_map.xy = (normal_map.xy * 2.0f - 1.0f) * _NormalScale;

        normal_map.z = sqrt(1.0f - min(dot(normal_map.xy, normal_map.xy), 1.0));

        normal = normalize(mul(tbn, normal_map));
    #endif

    float facing_reverse = vface ? 1 : -1;
    normal *= facing_reverse;

    // float3 vView = mul(view, (float3x3)unity_MatrixV);
    // float3 vNormal = mul(normal, (float3x3)unity_MatrixV);

    float ndotl = dot(normal, light);
    float ndotv = dot(normal, view);
    float ndoth = dot(normal, half_vec);

    
    float4 final_color = gbuffer;
#if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos.xyz, 1.f), final_color);
    #endif
    final_color.w = 1.0f;
    return final_color;
}

vertex_out vert_edge(vertex_in v)
{
    vertex_out o = (vertex_out)0.f;
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float3 camera_to_vertex_ws = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
    float3 camera_to_vertex_os = mul((float3x3)unity_WorldToObject, camera_to_vertex_ws);
    float3 camera_to_vertex_os_dir = normalize(camera_to_vertex_os);

    float range_lerp = smoothstep(0.1f, 0.3f, camera_to_vertex_os_dir.x);
    float outline_range_max = lerp(_OutlineFixRange2, _OutlineFixRange4, range_lerp);
    float outline_range_min = lerp(_OutlineFixRange1, _OutlineFixRange3, range_lerp);
    float outline_range_inv = 1.0f / (outline_range_max - outline_range_min);

    float front_side_mask = saturate(1.0f - dot(float2(0.0759999976f, 0.961000025f), camera_to_vertex_os_dir.xy));
    float range_weight = saturate((front_side_mask - outline_range_min) * outline_range_inv);
    range_weight = range_weight * range_weight * (range_weight * -2.0f + 3.0f);

    bool has_lip_z = v.color.z < 0.99f;
    float lip_z = has_lip_z ? v.color.z : 0.0f;
    float lip_weight = range_weight * lip_z;
    float outline_weight = (0.0f < lip_z) ? lip_weight : v.color.w;

    bool has_lip_y = (0.0f < v.color.y) && (v.color.y < 1.0f);
    outline_weight += has_lip_y ? _FixLipOutline : 0.0f;

    float side_fix_a = dot(float3(-0.206f, 0.961000025f, _OutlineFixSide), camera_to_vertex_os_dir);
    float side_fix_b = dot(float3(-0.206f, 0.961000025f, -_OutlineFixSide), camera_to_vertex_os_dir);
    float front_fix = front_side_mask - _OutlineFixFront;

    float lip_outline_mask = saturate(max(side_fix_a, side_fix_b));
    lip_outline_mask = smoothstep(0.1f, 0.2f, lip_outline_mask);
    lip_outline_mask = max(lip_outline_mask, smoothstep(0.0f, 0.05f, front_fix));

    float lip_outline_scale = v.color.y * (lip_outline_mask - 1.0f) + 1.0f;
    float outline_width = lip_outline_scale * _OutlineWidth;
    float outline_scale = outline_width * _OutlineScale;
    outline_weight *= outline_scale;

    float4 view_pos_default = mul(unity_MatrixMV, v.vertex);
    float4 view_pos_override = mul(unity_MatrixV, ws_pos);
    float4 view_pos = (0.5f < _EnableCustomCameraOverride) ? view_pos_override : view_pos_default;

    float distance_scale = sqrt(abs(view_pos.z / unity_CameraProjection[1].y) / _OutlineScale);
    distance_scale = (0.0f != _ES_OutlineDisableDistanceScale) ? _ES_OutlineFallbackScale : distance_scale;

    outline_weight *= distance_scale;
    outline_weight *= (1.0f - _OneMinusCharacterOutlineWidthScale);

    float3x3 inv_mv = (float3x3)mul(unity_MatrixInvV, unity_ObjectToWorld);
    float3 outline_norm = 0.f;
    #if defined(_OUTLINENORMALFROM_TANGENT)
        outline_norm = v.tangent.xyz;
    #elif defined(_OUTLINENORMALFROM_NORMAL)
        outline_norm = v.normal;
    #elif defined(_OUTLINENORMALFROM_UV2)
        outline_norm = float3(v.uv1.xy, 1.f);
    #endif
    outline_norm.xy = mul(inv_mv, outline_norm.xyz).xy;
    outline_norm.z = -0.1f;
    outline_norm = normalize(outline_norm);

    float4 outline_view_pos = view_pos;
    outline_view_pos.xyz = outline_norm * outline_weight + view_pos.xyz;
    o.vertex = mul(glstate_matrix_projection, outline_view_pos);
    
    o.vertex = showpart(v.color.y) ? o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
    
    float4 ss_tmp;
    ss_tmp.w = (o.vertex.y * _ProjectionParams.x) * 0.5f;
    ss_tmp.xz = o.vertex.xw * 0.5f;
    o.ss_pos.zw = o.vertex.zw;
    o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
    o.color = v.color;
    o.ws_pos = ws_pos;
    
    o.uv.xy = v.uv.xy;
    o.uv.zw = float2(0.0f, 0.0f);
    o.normal = normalize( mul((float3x3)unity_ObjectToWorld, v.normal));
    return o;
}

buffer_out frag_edge(vertex_out i,  bool vface : SV_IsFrontFace) : SV_Target
{
    buffer_out o = (buffer_out)1.f;   
    #if defined(_USE_DITHER)
        float2 dither_screen_pos = floor((i.ss_pos.xy / i.ss_pos.w) * _ScaledScreenParams);
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
    #endif 
    float4 outline_color = _OutlineColor;
    

    
    float3 light;
    get_light(light);
    
    float ndotl = dot(i.normal, light);
    ndotl = smoothstep(0, 0.15f, ndotl); // blend_high
    float dark_val = 1.0f - _ES_OutLineDarkenVal;
    dark_val = lerp(dark_val, 1.0f, ndotl);
    ndotl = ndotl * _ES_OutLineLightedVal;
    
    outline_color.xyz = outline_color.xyz * dark_val + ndotl;
    // outline_color.xyz = outline_color.xyz; 
    outline_color.xyz = (1.0f - (_GlobalOneMinusAvatarIntensityEnable * _GlobalOneMinusAvatarIntensity)) * outline_color.xyz;
    float3 view = i.ws_pos.xyz - _WorldSpaceCameraPos;
    view.x = length(view);
    
    o.forward.xyz = outline_color;
    return o;
}

vertex_out vert_shadow(vertex_in v)
{
    vertex_out o = (vertex_out)0.0;
    float3 pos_ws = mul(unity_ObjectToWorld, v.vertex).xyz;
    float4 worldPos = float4(pos_ws, 1.0);
    o.vertex = mul(unity_MatrixVP, worldPos);

    return o;
}

half4 frag_shadow(vertex_out i) : SV_TARGET
{
    return 0;
}