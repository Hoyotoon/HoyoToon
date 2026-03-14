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
    o.pos = o.vertex;

    o.view.xyz = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
    float2 tmp = normalize(mul((float3x3)unity_WorldToObject, o.view.xyz)).xy; 
    o.view.w = dot(float2(0.275999993, 0.961000025), tmp);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_vertex_out(float2x2(v.uv.xy, v.uv1.xy), float4(o.ws_pos.xyz, 1.0f), v.vertex, o.diss_uv, o.diss_pos);
    #endif

    #if defined(_USE_NORMAL_MAP)
        // one of the few things that the devs left out before but it randomly appeared thanks
        // thanks to someone leaving it toggled on 
        float x_sgn = (v.uv1.x > 0.0) ? 1.0 : ((v.uv1.x < 0.0) ? -1.0 : 0.0);
        float x_off = abs(v.uv1.x) - 1.01;
        float radial_sq = 1.0 - (x_off * x_off);
        float combined_sq = radial_sq - (v.uv1.y * v.uv1.y);

        float thickness = sqrt(max(combined_sq, 0.0001));
        float final_x = thickness * x_sgn;

        float3 world_pos;
        world_pos.xyz = v.uv1.yyy * unity_ObjectToWorld[1].yzx;
        world_pos.xyz = unity_ObjectToWorld[0].yzx * x_off + world_pos.xyz;
        world_pos.xyz = unity_ObjectToWorld[2].yzx * final_x + world_pos.xyz;

        o.tangent = normalize(world_pos);

        o.bitangent = cross(o.normal, o.tangent) * (v.tangent.w * unity_WorldTransformParams.w);
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

    float4 final_color = 1.f;
    final_color.xyz = diffuse.xyz ;

    float id;
    get_id(lightmap.w, id);
   int array_index = material_region(lightmap.w);

    // this isnt working at the moment and needs to be relooked at when more of the shader is implemented: 
    // float shadow = get_main_light_shadow(i.ws_pos);
    /*
    sdw_combine = 1.0f - u_xlat87;
    u_xlat16_60.x = (-_MainLightShadowParams.x) + 1.0;
    sdw_combine = sdw_combine * _MainLightShadowParams.x + u_xlat16_60.x;
    sdw_combine = lerp(-_MainLightShadowParams.x, 1.0f, sdw_combine);
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
    float sdw_factor = max(SampleURPMainLightShadow(i.ws_pos), 0.5f); // sdw_combine
    // the reason for the above is to basically accumulate the shadows into this one variable

    // get the main shader driving vectors: light, view, normal.
    // these are the basis for almost the entire shading
    float3 light;
    get_light(light);

    float3 view = normalize(i.view);
    float3 normal = normalize(i.normal);

    float facing_reverse = vface ? 1 : -1;
    normal *= facing_reverse;

    float3 vView = mul(view, (float3x3)unity_MatrixV);
    float3 vNormal = mul(normal, (float3x3)unity_MatrixV);

    float ndotl = dot(normal, light);

    float3 rsdw_view = normalize(vView - _RimShadowOffset.xyz);
    float rsdw_ndotv = saturate(dot(rsdw_view, vNormal));

    // shadow
    float ao_area = (lightmap.y + lightmap.y) ;
    float sdw_area = saturate(ndotl * 0.5 + 0.5);
    sdw_area = dot(sdw_area.xx, ao_area.xx);

    sdw_factor =  0.5<_ES_CharacterDisableLocalMainLight ? sdw_factor : lerp(sdw_factor, 1.f , _CharacterLocalMainLightPosition.w);
    // sdw_factor = 1.f;
    float sdw_combine =  sdw_area;
    sdw_combine = min(indoor_sdw.x, sdw_combine) * sdw_factor;
    sdw_combine.x = max(sdw_combine, 0.001f);
    sdw_combine.x = sdw_combine.x * 0.85f + 0.15f;
    sdw_combine = (_ShadowRamp<sdw_combine) ? 0.99f : sdw_combine.x;
    sdw_area = lightmap.y * i.color.x;
    sdw_area = min(sdw_area, 0.8f);
    sdw_area = (sdw_factor.x < 0.1f) ? sdw_area : 1.0;
    float2 ramp_uv;
    ramp_uv.x = (sdw_area * sdw_combine);
    ramp_uv.y = 0.0625;

    // sample both ramps: 
    float3 multi_ramp = _DiffuseRampMultiTex.Sample(sampler_linear_clamp, ramp_uv);
    float3 cool_ramp = _DiffuseCoolRampMultiTex.Sample(sampler_linear_clamp, ramp_uv);

    float3 ramp = lerp(multi_ramp, cool_ramp, saturate(_ES_CharacterToonRampMode));

    
    ramp.xyz = apply_light_dark(ramp, id, sdw_area, sdw_combine);
    ramp.xyz = apply_level_adjust(ramp, 0);
    // ramp.xyz = apply_shadow_boost(ramp, sdw_combine);

    final_color.xyz *= ramp;

    float3 char_center =  normalize(i.ws_pos.xyz - _NewLocalLightCharCenter.xyz);
    float cdotnl = dot(char_center, _NewLocalLightDir.xyz);

    float ndotnl = (dot(normal,_NewLocalLightDir));
    float tmp = smoothstep(-0.5, 1.0f, ndotnl);
    float half_lambert;
    float3 light_str;
    float light_mask;
    float4 shaded_sdw;
    float3 blend_low;
    float3 blend_high;
    float3 inv_light;

    half_lambert = cdotnl * 0.5 + 0.5;

    // Calculate base intensity based on light alpha and NdotL
    light_str.x = 1.0 - _CharacterLocalMainLightColor1.w;
    light_str.x = tmp * light_str.x;
    light_str.x = _CharacterLocalMainLightColor1.w * half_lambert + light_str.x;

    // Apply toggle check
    light_mask = (0.5 < _ES_CharacterDisableLocalMainLight) ? 0.0 : 1.0;
    light_mask = light_str.x * light_mask;

    // Split light strength into channels for the two local lights
    light_str.xy = light_mask.xx * _NewLocalLightStrength.xy;

    // Shadowing factor
    shaded_sdw = indoor_sdw * light_str.x;

    // Calculate Multiply/Overlay-style blend components
    blend_low.xyz = final_color.xyz + final_color.xyz;
    blend_low.xyz = blend_low.xyz * _CharacterLocalMainLightColor1.xyz;

    blend_high.xyz = (-ramp.xyz) * diffuse.xyz + 1;
    blend_high.xyz = blend_high.xyz + blend_high.xyz;

    inv_light.xyz = (-_CharacterLocalMainLightColor1.xyz) + 1;
    blend_high.xyz = (-blend_high.xyz) * inv_light.xyz + 1;

    // Branchless-style selection for color blending
    {
        float3 movcTemp = blend_low;
        movcTemp.x = (final_color.x < 0.5f) ? blend_low.x : blend_high.x;
        movcTemp.y = (final_color.y < 0.5f) ? blend_low.y : blend_high.y;
        movcTemp.z = (final_color.z < 0.5f) ? blend_low.z : blend_high.z;
        blend_low = movcTemp;
    }

    // Apply blended light result to the ramp
    ramp.xyz = (-ramp.xyz) * diffuse.xyz + blend_low.xyz;
    ramp.xyz = shaded_sdw * ramp.xyz + final_color.xyz;

    // Add second local light contribution
    final_color.xyz = light_str.yyy * _CharacterLocalMainLightColor2.xyz;
    final_color.xyz = final_color.xyz * indoor_sdw + ramp.xyz;

    // after the moon was the rimshadow
    float3 rsdw_color;
    float2 rsdw_param;
    rsdw_color = _RimShadowColor0; 
    rsdw_param = float2(_RimShadowWidth0, _RimShadowFeather0);
    

    rsdw_color.xyz = rsdw_color.xyz * (_ES_RimShadowColor.www * _ES_RimShadowColor.xyz);
    rsdw_ndotv = pow(max( 1.0 - rsdw_ndotv.x, 0.001f), _RimShadowCt);
    rsdw_ndotv.x = saturate(rsdw_ndotv.x * rsdw_param.x);
    rsdw_ndotv.x = smoothstep(rsdw_param.y, 1.0f, rsdw_ndotv.x);
    rsdw_ndotv.x = rsdw_ndotv.x * _RimShadowIntensity;
    rsdw_ndotv.x = rsdw_ndotv.x * _ES_RimShadowIntensity;
    rsdw_ndotv.x = rsdw_ndotv.x * 0.25;
    float3 rsdw = lerp(1.0f, rsdw_color.xyz * 2.0f, rsdw_ndotv.xxx);
    final_color.xyz = final_color.xyz * rsdw;

    #if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos.xyz, 1.f), final_color);
    #endif

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif
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

float4 frab_forward(vertex_out i, bool vface : SV_IsFrontFace) : SV_TARGET
{
    float2 screen_uv = i.ss_pos.xy / i.ss_pos.w;
    float4 gbuffer = _GBufferA.Sample(sampler_linear_clamp, screen_uv).xyzw;

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


    // float3 vView = mul(view, (float3x3)unity_MatrixV);
    // float3 vNormal = mul(normal, (float3x3)unity_MatrixV);

    float ndotl = dot(normal, light);
    float ndotv = dot(normal, view);
    float ndoth = dot(normal, half_vec);

    float3 spec_color = (_SpecularColor0 * lerp(1.0f, _ES_SPColor, _ES_SPColor.www)) * _ES_SPIntensity;
    float3 spec_param = float3(_SpecularShininess0, _SpecularRoughness0, _SpecularIntensity0); // roughness intensity shininess
    
    float3 specular = ndoth;
    specular = pow(max(specular, 0.00f), spec_param.x) * shadow_factor;
    spec_param.y = max(spec_param.y, 0.001f);
    

    float specular_thresh = 1.0f - lightmap.z;

    specular = smoothstep(specular_thresh - spec_param.y, specular_thresh + spec_param.y, specular) * spec_color * spec_param.z;
    


    // emission
    float emission_thresh = (_EmissionThreshold < diffuse.w) ? saturate(diffuse.w - _EmissionThreshold / max(1.0f - _EmissionThreshold, 0.001f)) : 0.0;   

    // fresnel 
    float fresnel_area = saturate((1 - abs(ndotv.x) + (-_FresnelBSI.x)) * (float(1.0) / _FresnelBSI.y));
    float3 fresnel = fresnel_area * _FresnelColor.xyz;
    fresnel.xyz = max(fresnel.xyz * _FresnelColorStrength, 0.0f);
    
    float4 final_color = gbuffer;
    final_color.xyz = specular * gbuffer + lerp(gbuffer, diffuse * _EmissionIntensity, 
    emission_thresh);
    final_color.xyz = final_color.xyz * light_color + fresnel;
    final_color.xyz = _ES_AddColor.xyz * gbuffer + final_color;

    if(!_UsingDitherAlpha)
    {

        float3 rim_color  = _RimColor0;
        float3 rim_values = float3(_RimEdgeSoftness0, _RimType0, _RimDark0);
        

        rim_color = (rim_color * lerp(1.0f, _ES_RimLightColor, _ES_RimLightColor.www)) * _ES_RimLightIntensity;
        rim_color = rim_color * 0.5f;
        float rim_mask = lerp(1.0f, lightmap.x, _RimLightMode) * _RimWidth;
        float normal_offset = normal.z * view.x - (normal.x * view.z);    
        normal_offset = sign(normal_offset);

        float rim_width = rim_mask.x * _ES_RimLightWidth;
        rim_width.x = normal_offset.x * rim_width.x;
        rim_width.x = rim_width.x * 0.0055;

        ndotl = dot(saturate(ndotl * 0.5 + 0.5f).xx, lightmap.yy) * shadow_factor;

        float3 char_center =  normalize(i.ws_pos.xyz - _NewLocalLightCharCenter.xyz);
        float cdotnl = dot(char_center, _NewLocalLightDir.xyz);

        float ndotnl = (dot(normal,_NewLocalLightDir));
        float tmp = smoothstep(-0.5, 1.0f, ndotnl);
        float half_cdtl = cdotnl * 0.5f + 0.5f;


        cdotnl = (-_CharacterLocalMainLightColor1.w) + 1.0;
        cdotnl = tmp * cdotnl;
        cdotnl = _CharacterLocalMainLightColor1.w * half_cdtl.x + cdotnl;
        cdotnl = 0.5<_ES_CharacterDisableLocalMainLight ? 0.0f : cdotnl;
        cdotnl = saturate(cdotnl * _NewLocalLightStrength.x) * 0.3f;

        float3 ll_color = lerp(rim_color, _CharacterLocalMainLightColor1, cdotnl);

        float org_depth = Linear01Depth(_DepthBufferOrCopy.Sample(sampler_linear_clamp, screen_uv.xy).r, _ZBufferParams);
        rim_width = rim_width / (org_depth * _ProjectionParams.z + 3.0); 

        float2 depth_uv;
        depth_uv.x = ((_ES_RimLightOffset.x) * 0.01f + rim_width) + screen_uv.x;
        depth_uv.y = ((_ES_RimLightOffset.y) * 0.01f + screen_uv.y);
        float sampled_depth = (_DepthBufferOrCopy.Sample(sampler_linear_clamp, depth_uv.xy).r);
        
        sampled_depth = Linear01Depth(sampled_depth, _ZBufferParams);
        sampled_depth = sampled_depth - org_depth;
        sampled_depth = max(sampled_depth, 9.99999997e-07);
        sampled_depth = pow(sampled_depth, _RimEdge);
        sampled_depth = smoothstep(0.82f, 0.9f, sampled_depth);
        sampled_depth = (rim_values.x < sampled_depth) ? sampled_depth : 0.0f;

        ndotl = (ndotl * rim_values.z - rim_values.z) + 1.0f;
        ndotv = 1.0f - ndotv; 

        float3 tmp_color = (ll_color * sampled_depth) * _Rimintensity;

        float grey = dot(tmp_color, float3(0.212670997, 0.715160012, 0.0721689984)) * ndotv;

        ndotl = saturate(ndotl * grey);

        rim_color = rim_color * _ES_RimLightAddMode + lerp(final_color, tmp_color, ndotl);

        float tmp_34 = pow(max(ndotv, 0.001f), rim_values.z) + 1.0f;

        float3 maxed_final = pow(max(final_color.xyz, 0.001f), tmp_34);
        maxed_final = lerp(maxed_final, tmp_color, ndotl);
        final_color.xyz = lerp(maxed_final, rim_color, rim_values.y);
        // final_color.xyz = sampled_depth;
    }


    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif

    #if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos, 1.f), final_color);
    #endif
    
    final_color.w = 1.0f;

    return final_color;
}


buffer_out frag_stencil(vertex_out i, bool vface : SV_IsFrontFace)
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

    float4 final_color = 1.f;
    final_color.xyz = diffuse.xyz ;

    float id;
    get_id(lightmap.w, id);
   int array_index = material_region(lightmap.w);

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
    float sdw_factor = 1.f; // sdw_combine
    // the reason for the above is to basically accumulate the shadows into this one variable

    // get the main shader driving vectors: light, view, normal.
    // these are the basis for almost the entire shading
    float3 light;
    get_light(light);

    float3 view = normalize(i.view);
    float3 normal = normalize(i.normal);

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

    float3 vView = mul(view, (float3x3)unity_MatrixV);
    float3 vNormal = mul(normal, (float3x3)unity_MatrixV);

    float ndotl = dot(normal, light);

    float3 rsdw_view = normalize(vView - _RimShadowOffset.xyz);
    float rsdw_ndotv = saturate(dot(rsdw_view, vNormal));

    // shadow
    float ao_area = (lightmap.y + lightmap.y) * i.color.x;
    float sdw_area = saturate(ndotl * 0.5 + 0.5);
    sdw_area = dot(sdw_area.xx, ao_area.xx);

    sdw_factor =  0.5<_ES_CharacterDisableLocalMainLight ? sdw_factor : lerp(sdw_factor, 1.f , _CharacterLocalMainLightPosition.w);
    // sdw_factor = 1.f;
    float sdw_combine = sdw_factor.x * sdw_area;
    sdw_combine = min(indoor_sdw.x, sdw_combine);
    sdw_combine.x = max(sdw_combine, 0.001f);
    sdw_combine.x = sdw_combine.x * 0.85f + 0.15f;
    sdw_combine = (_ShadowRamp<sdw_combine) ? 0.99f : sdw_combine.x;
    sdw_area = lightmap.y * i.color.x;
    sdw_area = min(sdw_area, 0.8f);
    sdw_area = (sdw_factor.x < 0.1f) ? sdw_area : 1.0;
    float2 ramp_uv;
    ramp_uv.x = (sdw_area * sdw_combine);
    ramp_uv.y = (id * 2.0f + 1.0f) * 0.0625f;

    // sample both ramps: 
    float3 multi_ramp = _DiffuseRampMultiTex.Sample(sampler_linear_clamp, ramp_uv);
    float3 cool_ramp = _DiffuseCoolRampMultiTex.Sample(sampler_linear_clamp, ramp_uv);

    float3 ramp = lerp(multi_ramp, cool_ramp, saturate(_ES_CharacterToonRampMode));

    
    ramp.xyz = apply_light_dark(ramp, id, sdw_area, sdw_combine);
    ramp.xyz = apply_level_adjust(ramp, id);
    ramp.xyz = apply_shadow_boost(ramp, sdw_combine);

    final_color.xyz *= ramp;

    float3 char_center =  normalize(i.ws_pos.xyz - _NewLocalLightCharCenter.xyz);
    float cdotnl = dot(char_center, _NewLocalLightDir.xyz);

    float ndotnl = (dot(normal,_NewLocalLightDir));
    float tmp = smoothstep(-0.5, 1.0f, ndotnl);
    float half_lambert;
    float3 light_str;
    float light_mask;
    float4 shaded_sdw;
    float3 blend_low;
    float3 blend_high;
    float3 inv_light;

    half_lambert = cdotnl * 0.5 + 0.5;

    // Calculate base intensity based on light alpha and NdotL
    light_str.x = 1.0 - _CharacterLocalMainLightColor1.w;
    light_str.x = tmp * light_str.x;
    light_str.x = _CharacterLocalMainLightColor1.w * half_lambert + light_str.x;

    // Apply toggle check
    light_mask = (0.5 < _ES_CharacterDisableLocalMainLight) ? 0.0 : 1.0;
    light_mask = light_str.x * light_mask;

    // Split light strength into channels for the two local lights
    light_str.xy = light_mask.xx * _NewLocalLightStrength.xy;

    // Shadowing factor
    shaded_sdw = indoor_sdw * light_str.x;

    // Calculate Multiply/Overlay-style blend components
    blend_low.xyz = final_color.xyz + final_color.xyz;
    blend_low.xyz = blend_low.xyz * _CharacterLocalMainLightColor1.xyz;

    blend_high.xyz = (-ramp.xyz) * diffuse.xyz + 1;
    blend_high.xyz = blend_high.xyz + blend_high.xyz;

    inv_light.xyz = (-_CharacterLocalMainLightColor1.xyz) + 1;
    blend_high.xyz = (-blend_high.xyz) * inv_light.xyz + 1;

    // Branchless-style selection for color blending
    {
        float3 movcTemp = blend_low;
        movcTemp.x = (final_color.x < 0.5f) ? blend_low.x : blend_high.x;
        movcTemp.y = (final_color.y < 0.5f) ? blend_low.y : blend_high.y;
        movcTemp.z = (final_color.z < 0.5f) ? blend_low.z : blend_high.z;
        blend_low = movcTemp;
    }

    // Apply blended light result to the ramp
    ramp.xyz = (-ramp.xyz) * diffuse.xyz + blend_low.xyz;
    ramp.xyz = shaded_sdw * ramp.xyz + final_color.xyz;

    // Add second local light contribution
    final_color.xyz = light_str.yyy * _CharacterLocalMainLightColor2.xyz;
    final_color.xyz = final_color.xyz * indoor_sdw + ramp.xyz;

    // after the moon was the rimshadow
    float3 rsdw_color;
    float2 rsdw_param;
    rsdw_color = _RimShadowColor0; 
    rsdw_param = float2(_RimShadowWidth0, _RimShadowFeather0);
    

    rsdw_color.xyz = rsdw_color.xyz * (_ES_RimShadowColor.www * _ES_RimShadowColor.xyz);
    rsdw_ndotv = pow(max( 1.0 - rsdw_ndotv.x, 0.001f), _RimShadowCt);
    rsdw_ndotv.x = saturate(rsdw_ndotv.x * rsdw_param.x);
    rsdw_ndotv.x = smoothstep(rsdw_param.y, 1.0f, rsdw_ndotv.x);
    rsdw_ndotv.x = rsdw_ndotv.x * _RimShadowIntensity;
    rsdw_ndotv.x = rsdw_ndotv.x * _ES_RimShadowIntensity;
    rsdw_ndotv.x = rsdw_ndotv.x * 0.25;
    float3 rsdw = lerp(1.0f, rsdw_color.xyz * 2.0f, rsdw_ndotv.xxx);

    #if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos.xyz, 1.f), final_color);
    #endif

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif

    //output this shit
    output.forward.xyz = final_color;

    float hair_blend = i.view.w + _HairBlendOffset;
    hair_blend = saturate(hair_blend * _HairBlendWeight);
    hair_blend = 1.0 - hair_blend;
    float hair_blend_remap = hair_blend * 0.5 + 0.5;
    output.forward.w = lerp(hair_blend_remap, hair_blend, diffuse.w);
    // output.forward.w = 0.5;    
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

vertex_out vert_edge(vertex_in v)
{
    vertex_out o = (vertex_out)0.f;
    float4x4 InvMV = mul(unity_MatrixInvV, unity_ObjectToWorld);
    float3 outline_norm = 0.f;
    #if defined(_OUTLINENORMALFROM_TANGENT)
        outline_norm = v.tangent; 
    #elif defined(_OUTLINENORMALFROM_NORMAL)
        outline_norm = v.normal;
    #elif defined(_OUTLINENORMALFROM_UV2)
        outline_norm = float3(v.uv1.xy, 1.f);
    #endif
    outline_norm.xy = mul((float3x3)InvMV, outline_norm.xyz);
    outline_norm.z = -0.1f;
    outline_norm = normalize(outline_norm);
    
    // Corrected to Matrix * Vector
    float4 wv_pos = mul(unity_MatrixMV, v.vertex);
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);

    // Use the matrix as the first argument
    wv_pos = _EnableCustomCameraOverride ? mul(unity_MatrixV, ws_pos) : wv_pos;
    
    float offset = v.color.z < 0.99f ? v.color.z : 0.0f;
    offset = offset * _OutlineOffset;
    offset = -offset * 0.0099f + wv_pos.z;
    
    float fov_offset = offset / unity_CameraProjection[1].y;
    fov_offset = 1.0f / rsqrt(abs(fov_offset) / _OutlineScale);
    fov_offset = _ES_OutlineDisableDistanceScale ? _ES_OutlineFallbackScale : fov_offset;
    
    float width = _OutlineWidth * _OutlineScale;
    width = width * v.color.w;
    float scale = fov_offset * width;
    scale = scale * (1.0f - _OneMinusCharacterOutlineWidthScale);
    float3 view = ws_pos.xyz - _WorldSpaceCameraPos;
    o.ws_pos.xyz = ws_pos.xyz;
    float view_length = length(view);
    
    view_length = smoothstep(_OutlineExtdStart, _OutlineExtdMax, view_length);
    view_length = min(view_length, 0.5);
    view_length = view_length + 1.0;
    scale = scale * view_length;
    wv_pos.xyz = outline_norm.xyz * scale + wv_pos.xyz;
    

    // Final Clip Space: Projection * ViewSpacePosition
    o.vertex = mul(glstate_matrix_projection, wv_pos);
    
    o.vertex = showpart(v.color.y) ? o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
    
    float4 ss_tmp;
    ss_tmp.w = (o.vertex.y * _ProjectionParams.x) * 0.5f;
    ss_tmp.xz = o.vertex.xw * 0.5f;
    o.ss_pos.zw = o.vertex.zw;
    o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
    o.color = v.color;
    
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
    float4 outline_color = _OutlineColor0;
    #if defined(_RAMP_OUTLINE)
        outline_color = outline_color * _OutlineColorTex.Sample(sampler_linear_repeat, i.uv.xy);
    #endif
    float lightmap = _LightMap.Sample(sampler_linear_repeat, i.uv.xy).w;
    lightmap = floor(lightmap * 8.f);
    float id_check = lightmap * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    lightmap = frac(lightmap * id_transform.y) * id_transform.x;
    float id_rounded = round(lightmap);
    float outline_ID = uint(id_rounded);
    outline_color.w = 1.0f;
    
    float3 light;
    get_light(light);
    
    float ndotl = dot(i.normal, light);
    ndotl = smoothstep(0, 0.15f, ndotl); // u_xlat16_22
    float dark_val = 1.0f - _ES_OutLineDarkenVal;
    dark_val = lerp(dark_val, 1.0f, ndotl);
    ndotl = ndotl * _ES_OutLineLightedVal;
    
    outline_color.xyz = outline_color.xyz * dark_val + ndotl;
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

    #if UNITY_REVERSED_Z
        o.vertex.z = min(o.vertex.z, o.vertex.w * UNITY_NEAR_CLIP_VALUE);
    #else
        o.vertex.z = max(o.vertex.z, o.vertex.w * UNITY_NEAR_CLIP_VALUE);
    #endif

    return o;
}

half4 frag_shadow(vertex_out i) : SV_TARGET
{
    return 0;
}