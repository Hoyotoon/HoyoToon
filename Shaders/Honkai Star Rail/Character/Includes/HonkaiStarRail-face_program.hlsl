vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    // 
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.vertex = showpart(v.color.xy) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    float4 color_switch = (-(_VertexColorSwitch) + (float4)1) * float4(1.0, 1.0, 0.5, 0.5);
    o.color = v.color * _VertexColorSwitch + color_switch;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
    o.view = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
    float3 light = _WorldSpaceLightPos0;
    if(_UseFakeDirectionalLight) light = _FakeDirectionalLightRotation;
    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);

    float3 world_y = float3(unity_WorldToObject[0].y, unity_WorldToObject[1].y, unity_WorldToObject[2].y);
    world_y = normalize(world_y);

    o.face_veca.w    = saturate(dot(world_y, light) + 1.0f);
    o.face_veca.xyz  =  0.0f;

    if(_UseUVChannel2)
    {
        face_angle_extract(light, v.uv, o.face_veca.xyz);
    }
    else
    {
        face_angle_extract(light, v.uv2, o.face_veca.xyz);
    }

    dissolve_vertex_out(float2x2(v.uv.xy, v.uv2.xy), o.ws_pos, v.vertex, o.diss_uv, o.diss_pos);
    
    o.screenpos = ComputeScreenPos(o.vertex);
    o.hairpos;
    float3 vl = mul(light.xyz, UNITY_MATRIX_V) * (1.f / o.ws_pos.w);
    float3 offset_pos = ((vl * .001f) * float3(-15,10,0)) + v.vertex.xyz;
    v.vertex.xyz =  v.vertex.xyz;
    o.hairpos = ComputeScreenPos(UnityObjectToClipPos(v.vertex));

    return o;
}

float4 base_pixel (vertex_output i, bool vface : SV_IsFrontFace) : SV_Target
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

    if(_UsingDitherAlpha)
    {
        dither(i.diss_pos.z, dis_out, dither_screen_pos);
    }

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // real start
    // intialize inputs and output
    float4 color = vface ? _Color : _BackColor; 
    float4 vcol = i.color;
    float3 normal = vface ? i.normal : -1 * i.normal;
    normal = normalize(normal);
    float3 view = normalize(i.view); 
    float2 uv = vface ? i.uv : i.uv2;
    float4 output = (float4)1.0f;
    float3 light = _WorldSpaceLightPos0;
    if(_UseFakeDirectionalLight) light = _FakeDirectionalLightRotation;

    float3 normal_vs = normalize(mul((float3x3)unity_MatrixV, i.normal));
    float3 view_vs = normalize(mul((float3x3)unity_MatrixV, view));
    float ndotv_sdw = dot(normal_vs, normalize(view_vs - _RimShadowOffset));
    
    // sample main texture
    float4 main_tex = sample_lr_texture(_MainTex, uv);
    // calculate this early
    float emissive = _EmissionThreshold > main_tex.w;
    emissive = emissive ? main_tex.w / _EmissionThreshold : 0.0f;

    float emis_tmp =  main_tex.w - _EmissionThreshold /max((-_EmissionThreshold) + 1.0, 0.00100000005);
    emis_tmp = (_EmissionThreshold < main_tex.w) ? emis_tmp : 0.0;
    
    
    main_tex.xyz = ((saturate(emis_tmp) * main_tex.xyz) * _EmissionIntensity) + main_tex.xyz;


    // sample this shit
    float4 face_map = sample_lr_texture(_FaceMap, i.face_veca.xy);
    face_map.x =  sample_lr_texture(_FaceMap, uv).x;
    float4 face_exp = sample_lr_texture(_FaceExpression, uv);
    // output = face_map.w;

    float2 area_check = float2(0.0f < face_map.x, 0.1f < face_map.x);
    float eye_area = face_map.x > 0.5f && face_map.x < 0.55f ;
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // nose line:
        float3 nose_view = float3(view.x, view.y * 0.5f, view.z);
        float ndotv_nose = dot(normal, nose_view);
        ndotv_nose = max(ndotv_nose, 0.001f);
        ndotv_nose = min(pow(ndotv_nose, _NoseLinePower * 8.0),1.0f) * face_map.z;
        float nose_thresh = 0.1f<ndotv_nose;
        main_tex.xyz = lerp(main_tex, _NoseLineColor.xyz, nose_thresh);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // lips 

        
        float max_range = max(_LipLineFixMax - _LipLineFixStart, 0.01f);

        // Calculate the view distance.
        float view_length = length(i.ws_pos - _WorldSpaceCameraPos); 

        float2 dist_factors;
        dist_factors.x = (view_length < 2.0f) ? 1.2f : 1.0f; // Scale up when close
        dist_factors.y = (5.0f < view_length) ? 0.8f : 1.0f; // Scale down when far

        float normalized_distance_beyond_start = (view_length - _LipLineFixStart) / max_range;
        normalized_distance_beyond_start = clamp(normalized_distance_beyond_start, 0.0f, 1.0f);

        float distance_alpha = (view_length < _LipLineFixStart) ? 0.0f : normalized_distance_beyond_start; 
        float combined_scale_factor = (dist_factors.x * dist_factors.y) - 1.0f;
        float weighted_scale = lerp(1, (dist_factors.x * dist_factors.y), _LipLineFixSC);
        float final_threshold = max(weighted_scale * _LipLineFixScale, 0.01f);
        final_threshold = saturate(final_threshold * _LipLineFixThrd);

        float mixed_threshold = lerp(0.04, final_threshold, distance_alpha);
        float lip_vcol = (vcol.y < 0.95f) ? vcol.y : 0.0f;
        float fix_mask = (mixed_threshold < (lip_vcol - (1.0f - distance_alpha))) ? 0.0f : 1.0f; 
        main_tex.xyz = lerp(_LipLinefixColor, main_tex, fix_mask);

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // eye base
        float3 eye_base = lerp(_EyeBaseShadowColor.xyz, 1.0f, vcol.x);

        main_tex.xyz = main_tex.xyz * eye_base;
        output.xyz = main_tex.xyz * _Color;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // expressions

        // face expression: 
        float thresh_a = face_exp.x + (-_ExMapThreshold);
        float thresh_b = (-_ExMapThreshold) + 1.0;
        thresh_a = thresh_a / max(thresh_b, 0.00100000005);
        thresh_b = face_exp.x / max(_ExMapThreshold, 0.00100000005);
        thresh_b = (_ExMapThreshold>=face_exp.x) ? thresh_b : 1.0;
        float cheek_tresh = thresh_a * _ExSpecularIntensity;
        cheek_tresh.x = cheek_tresh.x * _ExCheekIntensity;
        thresh_a = (_ExMapThreshold<face_exp.x) ? cheek_tresh.x : 0.0;
        float3 ex_eye = (-_ExShadowColor.xyz) + _ExEyeColor.xyz;
        ex_eye.xyz = (area_check.x) ? ex_eye.xyz : 0.0f;
        ex_eye.xyz = ex_eye.xyz + _ExShadowColor.xyz;

        // expression layers
        float3 ex_cheek = lerp(1.0f, _ExCheekColor.xyz, thresh_b * _ExCheekIntensity);
        ex_cheek = lerp(ex_cheek, _ExShyColor.xyz, face_exp.y * _ExShyIntensity);
        ex_eye.xyz = lerp(ex_cheek.xyz, ex_eye.xyz, face_exp.z * _ExShadowIntensity);
        //  final masking
        ex_eye.xyz = thresh_a + ex_eye.xyz;
        // apply expressions
        output.xyz = output.xyz * ex_eye.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // lighting 
    float light_count = _FakePointLightNum;
    float3 lighting_color = 0;
    float3 light_check = float3(light_count >= 3, light_count >= 2, light_count >= 1);

    if(light_count!= 0)
    {
        if(_FakePointLight2)
        {
            POINTLIGHT_CALC(i.ws_pos, 2, float3(i.ws_pos.xy, 0.5), lighting_color)
        }
        if(_FakePointLight1)
        {
            POINTLIGHT_CALC(i.ws_pos, 1, float3(i.ws_pos.xy, 0.5), lighting_color)
        }
        if(_FakePointLight0)
        {
            POINTLIGHT_CALC(i.ws_pos, 0, float3(i.ws_pos.xy, 0.5), lighting_color)
        }
    }
    
    if(_IsVRC) pointlight_unity(i.ws_pos, float3(i.ws_pos.xy, 0.5), lighting_color);

    

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // shadow
        // face stuff
        // sample hair shadow :
        float2 screenpos = (i.hairpos.xy / i.hairpos.w);
        float hair_shadow = _HairMaskRT.Sample(sampler_linear_repeat, screenpos).x;
        hair_shadow = lerp(hair_shadow, 0.0f, area_check.x);
        float shadow_range = (face_map.w<_BackShadowRange) ? -face_map.w + 1.0f : face_map.w;
        shadow_range = smoothstep(max(i.face_veca.z - _ShadowFeather, 0.0f), min(i.face_veca.z + _ShadowFeather, 1.0f),shadow_range);
        shadow_range = shadow_range * saturate(1.0f - hair_shadow);

        bool eye_check = (face_map.x < 0.800000012) && area_check.y;
        float3 eye_tmp = eye_check ? float3(1.0, -1.0, 0.5) : float3(0.0, -0.0, 0.0);

        float2 eye_vs_face = area_check.y ? float2(1.0, 0.0) : float2(0.0, 1.0);
        float offset = (eye_vs_face.x + eye_tmp.y) + eye_tmp.z;

        float3 face_sdw_color = lerp(_ShadowColor, _EyeShadowColor, eye_vs_face.x);
        face_sdw_color = lerp(face_sdw_color, 1.0f, shadow_range);
        
        if(_ES_LEVEL_ADJUST_ON)
        {
            // shadow_range = lerp(1.0f, shadow_range, _ES_LevelEyeShadowIntensity);
            cg_lighting(face_sdw_color, shadow_range, 1);
        }
        if(_ShadowBoost)
        {
            float boost_range = smoothstep(0.8, 0.81, shadow_range);
            float boost = lerp(_ShadowBoostVal, 1.0f, boost_range);
            face_sdw_color.xyz = face_sdw_color * boost.xxx;
        }
        
        output.xyz = output.xyz + (lighting_color * 0.85 * saturate(shadow_range * saturate(1 - (area_check.y * 0.975f))));
        output.xyz = output.xyz *  face_sdw_color; 
        
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // eye effect
        float eye_proc = saturate(eye_area * _EyeEffectProcs);
        float grey = dot(output.xyz, float3(0.300000012, 0.589999974, 0.109999999));
        
        // eye blending
        float3 eye_tmpa = grey * _EyeEffectColor.xyz;
        float3 eye_tmpb = grey * _EyeEffectColor.xyz - output.xyz;
        eye_tmpb = eye_proc * eye_tmpb + output.xyz;
        eye_tmpa = eye_tmpa * grey + eye_tmpb;
        eye_tmpa = eye_tmpa * (grey * 0.5f + 1.0f) - output.xyz; 
        output.xyz = eye_proc * eye_tmpa + output.xyz;    
    //- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    //  special eye 
        if(_UseSpecialEye)
        {
            float2 special_eye_uv = offset_tiling(i.uv, _SpecialEyeShapeTexture_ST).yx;
            special_eye_uv = special_eye_uv - _EyeCenter.yx;
            float2 scroll = _Time.yy * _EyeCenter.zw;
            
            float cos_rot = cos(scroll.x);
            float sin_rot = sin(scroll.x);

            float2 uv_tmpa = special_eye_uv * sin_rot.xx;
            float2 uv_tmpb;
            uv_tmpb.x = special_eye_uv.y * cos_rot - uv_tmpa.x;
            uv_tmpb.y = special_eye_uv.x * cos_rot + uv_tmpa.y;
            uv_tmpb.xy = uv_tmpb.xy + _EyeCenter.xy;
            float special_x = sample_lr_texture(_SpecialEyeShapeTexture, uv_tmpb).x;

            cos_rot = cos(scroll.y);
            sin_rot = sin(scroll.y);

            uv_tmpa = special_eye_uv * sin_rot.xx;
            uv_tmpb.x = special_eye_uv.y * cos_rot - uv_tmpa.x;
            uv_tmpb.y = special_eye_uv.x * cos_rot + uv_tmpa.y;
            uv_tmpb.xy = uv_tmpb.xy + _EyeCenter.xy;
            float special_y = sample_lr_texture(_SpecialEyeShapeTexture, uv_tmpb).y;
            special_y = (eye_area * special_y) * _EyeSPColor2.w;
            special_x = (eye_area * special_x) * _EyeSPColor1.w;

            float3 sp_color = lerp(output.xyz, _EyeSPColor2.xyz, special_y);
            sp_color = lerp(sp_color, _EyeSPColor1.xyz, special_x);


            output.xyz = lerp(output.xyz, sp_color.xyz, _SpecialEyeIntensity);



        }
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // secondary emission 
        float emis_area;
        emis_area = (main_tex.w - _EmissionThreshold) / max(0.001f, 1.0f - _EmissionThreshold);
        emis_area = (_EmissionThreshold < main_tex.w) ? emis_area : 0.0f;
        emis_area = saturate(emis_area);

        float3 emis_color;
        emis_color = (main_tex * emis_area) * _EmissionIntensity;

        output.xyz = emis_color + output;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    //dissolve outline
        if(_DissoveON) dissolve_outline(output, dis_area, dis_map);

    #if defined(is_eye_mask)
        uv = _UseUVChannel2 ? i.uv2 : i.uv;
        float stencil_mask = _FaceMap.Sample(sampler_linear_repeat, uv).y - _HairBlendSilhouette;
        stencil_mask = (max(floor(min(stencil_mask, 1.0f) + 1.0f), 0.0f));
        if(int(stencil_mask) == 0) discard;
    #endif


    output.xyz = output.xyz*max(float3(0.1f, 0.1f,  0.1f), _LightColor0.xyz);
    fake_fog(i.ws_pos, view, output);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    if(_UseHeightLerp) heightlightlerp(i.ws_pos, output);

    return output;
}

float4 base_mask (vertex_output i, bool vface : SV_IsFrontFace) : SV_Target
{
    // float2 uv = _UseUVChannel2 ? i.uv2 : i.uv;
    // float stencil_mask = _FaceMap.Sample(sampler_linear_repeat, uv).y - _HairBlendSilhouette;
    // stencil_mask = (max(floor(min(stencil_mask, 1.0f) + 1.0f), 0.0f));
    // if(int(stencil_mask) == 0) discard;
    // return (float4)1.0f;    
}

vertex_output outline_vertex(vertex_input v)
{  
    vertex_output o = (vertex_output)0;

    // anime outline lip function : 
    o.view = normalize(_WorldSpaceCameraPos.xyz - mul(unity_WorldToObject, v.vertex).xyz);
    float3 os_view = mul((float3x3)unity_WorldToObject, o.view);
    float3 os_norm = normalize(os_view);

    float2 ov_rot = dot(float2(0.075, 1), os_norm.xy);
    float2 inv_rot = saturate(ov_rot + -1.0f);
    
    float outline_range = smoothstep(0.1f, 0.19f, os_norm.y);

    float range24 = lerp(_OutlineFixRange2, _OutlineFixRange4, outline_range);
    float range13 = lerp(_OutlineFixRange1, _OutlineFixRange3, outline_range);

    float outline_blue = smoothstep(range13, range24, inv_rot);

    float2 vc_check = v.color.yz < float2(0.99f, 1.0f);
    float blue = vc_check.x ? v.color.z : 0.0f;

    outline_blue = outline_blue * blue;

    float outline_width = blue > 0.0f ? outline_blue : v.color.w;

    float fix_lip = ((0.0f < v.color.y) && vc_check.y) * _FixLipOutline + outline_width;

    // start rotating and applying more and more smoothsteps
    float3 fix_side_vec = float3(-0.206, 0.961, _OutlineFixSide);
    float4 fix_front_vec = float4(-_OutlineFixFront, -0.206, 0.961, -_OutlineFixSide);
    float side_angle = dot(fix_side_vec.xyz, os_norm);
    float front_angle = dot(fix_front_vec.yzw, os_norm);
    float fix_angle = saturate(max(front_angle, side_angle));
    fix_angle = min(smoothstep(0.1f, 0.099, fix_angle), 1.0f);

    float tmp_angle = smoothstep(fix_front_vec.x, fix_front_vec.x + 0.5f, inv_rot);

    fix_angle = min(max(fix_angle, tmp_angle), 1.0);
    fix_angle = (lerp(1.0f, v.color.y, fix_angle) * _OutlineWidth) * _OutlineScale;

    outline_width = outline_width * fix_angle;

    float4 outline_pos = mul(UNITY_MATRIX_MV, v.vertex);
    float3 outline_normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);
    outline_normal.z = -0.1f;
    outline_normal = normalize(outline_normal);


    float outline_scale = outline_width;

    // float outline_offset = (v.color.z < 0.99f) ? v.color.z : 0.0f;
    // outline_offset = outline_offset * 1;
    // float pos_w = (-outline_offset) + 0.0099f + outline_pos.z;
    float outline_fov =  outline_pos.z / unity_CameraProjection._43;
    outline_fov = 1.0f / (rsqrt(abs(outline_fov) /  _OutlineScale));
    outline_scale = outline_scale * outline_fov;

    outline_scale = UNITY_MATRIX_P[3][3] == 0 ? outline_scale :  ((outline_width)) * (_OutlineScale * 2500);


    outline_pos.xyz = outline_normal * outline_scale + outline_pos.xyz;

    o.vertex = mul(UNITY_MATRIX_P, outline_pos);


    // o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    o.vertex = showpart(v.color.xy) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);

    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 

    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);
    dissolve_vertex_out(float2x2(v.uv.xy, v.uv2.xy), o.ws_pos, v.vertex, o.diss_uv, o.diss_pos);

    UNITY_TRANSFER_FOG(o,o.vertex);
    TRANSFER_SHADOW(o);
    return o; 
}

float4 outline_pixel (vertex_output i, bool vface : SV_IsFrontFace) : SV_Target
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
    
    float2 dither_screen_pos = floor((i.screenpos.xy / i.screenpos.w) * _ScreenParams);

    dither(i.diss_pos.z, dis_out, dither_screen_pos);


    // initalize inputs: 
    float2 uv = vface ? i.uv : i.uv2;
    float3 normal = normalize(i.normal);

    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);
    float material_ID = floor(8.0f * lightmap.w);
    float id_check = material_ID * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    material_ID = frac(material_ID * id_transform.y) * id_transform.x;
    float id_rounded = round(material_ID);

    float alpha = sample_lr_texture(_MainTex, uv);
    clip((alpha - _AlphaTestThreshold) + (1.0 - _EnableAlphaCutoff));


    float outline_ID = uint(id_rounded);
    float4 outline_color = _OutlineColor * max(float4(0.1f, 0.1f,  0.1f, 0.1), _LightColor0);
    

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // dissolve outline
    if(_DissoveON) dissolve_outline(outline_color, dis_area, dis_map);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    return outline_color;
}