vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    // 
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    o.vertex = showpart(v.color.xy) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    float4 color_switch = (-(_VertexColorSwitch) + (float4)1) * float4(1.0, 1.0, 0.5, 0.5);
    o.color = v.color * _VertexColorSwitch + color_switch;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
    o.view.xyz = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);

    dissolve_vertex_out(float2x2(v.uv.xy, v.uv2.xy), o.ws_pos, v.vertex, o.diss_uv, o.diss_pos);

    // get view angle for hair fade
    float3 to_view = mul((float3x3)unity_WorldToObject, o.view.xyz) ;
    if (_IsYup)
    {
        o.view.w = dot(float2(0.276, 0.961), normalize(to_view).xz);
    }
    else
    {
        o.view.w = dot(float2(0.276, 0.961), normalize(to_view).xy);
    }


    TRANSFER_SHADOW(o)
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
    // float dither = 0;

    if(_UsingDitherAlpha)
    {
        dither(i.diss_pos.z, dis_out, dither_screen_pos);
        // ordered_dither(i.ws_pos.z * _DitherAlpha, dis_out, dither_screen_pos, dither);
    }
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // real start
    // intialize inputs and output
    float4 color = vface ? _Color : _BackColor; 
    float4 vcol = i.color;
    float3 normal = vface ? i.normal : -1 * i.normal;
    float3 view = i.view; 
    float2 uv = vface ? i.uv : i.uv2;
    float4 output = (float4)1.0f;
    float3 light = _WorldSpaceLightPos0;
    if(_UseFakeDirectionalLight) light = _FakeDirectionalLightRotation;

    // create dot products
    float ndotl = dot(normalize(normal), light.xyz);
    float ndotv = dot(normalize(normal), view);
    float ndoth = dot(normalize(normal), normalize(view +  light.xyz));
    
    // sample main texture
    float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv);
    output = main_tex * color;

    //  first alpha testing : 
    clip((main_tex.w - _AlphaTestThreshold) + (1.0 - _EnableAlphaCutoff));
    
    // sample the lightmap
    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);

    // lighting 
        float light_count = _FakePointLightNum;

    float3 lighting_color = 0;

    if(light_count!= 0)
    {
        if(_FakePointLight2)
        {
            POINTLIGHT_CALC(i.ws_pos, 2, normal, lighting_color)
        }
        if(_FakePointLight1)
        {
            POINTLIGHT_CALC(i.ws_pos, 1, normal, lighting_color)
        }
        if(_FakePointLight0)
        {
            POINTLIGHT_CALC(i.ws_pos, 0, normal, lighting_color)
        }
    }
    
    if(_IsVRC) pointlight_unity(i.ws_pos, normal, lighting_color);
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // shadow 
     // sample hair shadow :
        float3 shadow_color;
        float shadow_area;
        float selfshadow = SHADOW_ATTENUATION(i);
        shadow_area = shadow_rate(ndotl, lightmap.y, vcol.x, _ShadowRamp, 1);

        // RAMP UVS  
        float2 ramp_uv = {shadow_area, 0.0675};

        // SAMPLE RAMP TEXTURES
        float3 warm_ramp = _DiffuseRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz; 
        float3 cool_ramp = _DiffuseCoolRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz;

        shadow_color = lerp(warm_ramp, cool_ramp, _ES_CharacterToonRampMode);  

        if (_ES_LEVEL_ADJUST_ON)
        {
            cg_lighting(shadow_color, shadow_area, 0);
        }
        if(_ShadowBoost)
        {
            float boost_range = smoothstep(0.8, 0.81, shadow_area);
            float boost = lerp(_ShadowBoostVal, 1.0f, boost_range);
            shadow_color.xyz = shadow_color * boost.xxx;
        }
        
    output.xyz = output.xyz + (lighting_color * saturate(shadow_area));
    output.xyz = output.xyz * shadow_color.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // specular
        float4 specular_color = _SpecularColor0;
        float3 specular_values = float3(_SpecularShininess0, _SpecularRoughness0, _SpecularIntensity0);
        

        specular_values.z = max(0.0f, specular_values.z); // why would there ever be a reason for a negative specular intensity


        float3 specular = specular_base(shadow_area, ndoth, lightmap.z, specular_color, specular_values, _ES_SPColor, _ES_SPIntensity);
    output.xyz = output.xyz + specular;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // rim shadow
        float4 rim_shadow_color = _RimShadowColor0;

        float2 rim_shadow_values = float2(_RimShadowWidth0, _RimShadowFeather0);

        rim_shadow_color.xyz = rim_shadow_color * (_ES_RimShadowColor.www  * _ES_RimShadowColor.xyz);

        float rim_shadow = ndotv;
        rim_shadow = 1.0f - rim_shadow;
        rim_shadow =  max(rim_shadow, 0.001);
        rim_shadow = pow(rim_shadow, _RimShadowCt);
        rim_shadow = smoothstep(rim_shadow_values.x, rim_shadow_values.y, rim_shadow);
        rim_shadow = rim_shadow * (min(_RimShadowIntensity - 1.0f, 1));
        rim_shadow = rim_shadow * _ES_RimShadowIntensity;
        rim_shadow = rim_shadow * 0.25f;
        rim_shadow_color.xyz = rim_shadow_color.xyz * 2.0f - 1.0f;
        rim_shadow_color.xyz = rim_shadow * rim_shadow_color.xyz + 1.0f;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // emission
        
        float emis_area = (main_tex.w - _EmissionThreshold) / max(0.001f, 1.0f - _EmissionThreshold);
        emis_area = (_EmissionThreshold < main_tex.w) ? emis_area : 0.0f;
        emis_area = saturate(emis_area);

        float3 emission_color = _EmissionIntensity * (main_tex.xyz * _EmissionTintColor.xyz);
    output.xyz = emis_area * (output.xyz * emission_color) + output.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    //  fresnel
        float3 fresnel = 1.0f - abs(ndotv);
        fresnel.x = fresnel.x + (-_FresnelBSI.x);
        fresnel.x = fresnel.x * (float(1.0) / _FresnelBSI.y);
        fresnel.x = saturate(fresnel.x);
        fresnel.xyz = (fresnel.xxx * _FresnelColor.xyz) * _FresnelColorStrength;
        fresnel = max(fresnel, 0.0f);
    output.xyz = output.xyz + fresnel;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // rimlight 

        if(!_UsingDitherAlpha)
        {

            float rim_width_mask = lerp(1.0f, lightmap.x, _RimLightMode) * _RimWidth;
            float normal_offset = view.z * normal.x - (view.x * normal.z);
            normal_offset = 0.0f < normal_offset ? -1.0f : 1.0f;
            float rim_width = rim_width_mask.x * _ES_RimLightWidth;
            rim_width.x = normal_offset.x * rim_width.x;
            rim_width.x = rim_width.x * 0.0055;
            rim_width = UNITY_MATRIX_P[3][3] == 0 ? rim_width :  rim_width * 0.25;


            float2 screen_pos = (i.screenpos.xy / i.screenpos.w);

            float org_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen_pos.xy), screen_pos);
            
            rim_width = rim_width / (org_depth );

            float2 depth_uv;
            depth_uv.x = ((_ES_RimLightOffset.x + _RimOffset.x) * 0.001 + rim_width) + screen_pos.x;
            depth_uv.y = ((_ES_RimLightOffset.y + _RimOffset.y) * 0.001 + screen_pos.y);

            float rim_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depth_uv.xy), depth_uv);
            
            float4 rim_color = _RimColor0;
            float3 rim_values = float3(_RimEdgeSoftness0, _RimType0, saturate(_RimDark0));


            float feather = rim_values.x;
            float type = rim_values.y;
            float dark = rim_values.z;

            float3 darkening = (shadow_area * dark + (-dark)) + 1.0f;
        
            rim_depth = pow(max(rim_depth - org_depth, 9.99999997e-07), _RimEdge * 2.5);
            rim_depth = smoothstep(0.82, 1.0, rim_depth);
            rim_depth = (rim_depth > feather) ? rim_depth : 0;

            float3 rim = (rim_color.xyz * rim_depth) * _Rimintensity;

            darkening = saturate(output * dot(rim, float3(0.212670997, 0.715160012, 0.0721689984))) * darkening;

            float3 add_rim = lerp(darkening, rim, shadow_area*rim_depth);
            add_rim = rim + add_rim;

            float3 darkened = pow(max(1.0f-shadow_area, 0.001f), dark) + 1.0f; 

            float3 addened = pow(0, darkened);

            rim = lerp(addened, rim, rim_depth * darkening);
            rim = max(rim, 0.0f);
            rim = rim * _ES_RimLightAddMode;

            output.xyz = lerp(output+rim, output*rim+output, type);
        }   
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // dissolve outline
        if(_DissoveON) dissolve_outline(output, dis_area, dis_map);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    output.xyz = output.xyz*max(float3(0.1f, 0.1f,  0.1f), _LightColor0.xyz);
    if(_UseHeightLerp) heightlightlerp(i.ws_pos, output);
    // output.xyz = dither;
    #ifdef is_stencil
    float fade_angle = i.view.w + _HairBlendOffset;
    fade_angle = saturate(fade_angle * _HairBlendWeight);
    fade_angle = 1.f - fade_angle;
    output.w = fade_angle; // when its in the shade it should be lower
        
    // output.w = 0.5f;
    #endif
    return output;
}

vertex_output outline_vertex(vertex_input v)
{
    vertex_output o = (vertex_output)0;


    float4 outline_pos = mul(UNITY_MATRIX_MV, v.vertex);

    float3 outline_normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);

    outline_normal.z = -0.1f;
    outline_normal = normalize(outline_normal);

    float outline_offset = (v.color.z < 0.99f) ? v.color.z : 0.0f;
    outline_offset = outline_offset * _OutlineOffset;
    float pos_w = (-outline_offset) + 0.0099f + outline_pos.z;

    float outline_fov = pos_w / unity_CameraProjection._43;
    outline_fov = 1.0f / (rsqrt(abs(outline_fov) /  _OutlineScale));
    
    // float check_ortho =  UNITY_MATRIX_P[3][3]  == 0 ? 0 : ;

    float outline_scale =  _OutlineWidth * _OutlineScale;
    outline_scale = outline_scale * v.color.w;
    outline_scale = outline_scale * outline_fov;
    outline_scale = UNITY_MATRIX_P[3][3] == 0 ? outline_scale :  (((_OutlineWidth * _OutlineScale) * v.color.w))*(_OutlineScale * 2500);

    o.view.xyz = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);

    float view_length = length(o.view);
    view_length = smoothstep(_OutlineExtdStart, _OutlineExtdMax, view_length);
    view_length = min(view_length, 0.5f) + 1.0f;
    outline_scale = outline_scale * view_length;

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
    // float ndotl = dot(normal, light.xyz);

    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);
    float material_ID = floor(8.0f * lightmap.w);
    float id_check = material_ID * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    material_ID = frac(material_ID * id_transform.y) * id_transform.x;
    float id_rounded = round(material_ID);

    float alpha = sample_lr_texture(_MainTex, uv);
    clip((alpha - _AlphaTestThreshold) + (1.0 - _EnableAlphaCutoff));


    float outline_ID = uint(id_rounded);
    float4 outline_color = _OutlineColor0;

    outline_color = outline_color * _OutlineColorTex.Sample(sampler_linear_repeat, uv);
    outline_color.xyz = outline_color * max(float3(0.1f, 0.1f,  0.1f), _LightColor0.xyz);
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // dissolve outline
    if(_DissoveON) dissolve_outline(outline_color, dis_area, dis_map);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    return outline_color;
}
