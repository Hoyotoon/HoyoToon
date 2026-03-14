vertex_out vert_base(vertex_in v)
{
    vertex_out o = (vertex_out)0.f;
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float4 position =  mul(unity_MatrixMVP, v.vertex);
    float4 custom_position = mul(unity_MatrixVP, ws_pos);
    position = (_EnableCustomCameraOverride) ? custom_position : position;
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
    o.os_pos = v.vertex;
    o.view = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
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
    #else
    o.tangent = v.tangent.xyz;
    #endif
    return o;
}

buffer_out frag_base(vertex_out i,  bool vface : SV_IsFrontFace)
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

    #if defined(_FRAGMENT_CLIP)
        if(_EnableAlphaCutoff)
        {
            float test_val = diffuse.w + (-_AlphaTestThreshold);
            test_val = test_val + 1.0;
            test_val = floor(test_val);
            test_val = max(test_val, 0.0);
            
            int int_val = int(test_val);
            bool should_discard = int_val == 0;
            if (_EnableAlphaCutoff && should_discard)
            {
                discard;
            }
        }
    #endif

    // starting version of the starry sky, the actual sparkles happen in the specular pass
    #if defined(_STATTYSKY)
        float3 skytex = _SkyTex.Sample(sampler_linear_repeat, i.uv.xy * _SkyTex_ST.xy + _SkyTex_ST.zw);
        float skymask = _SkyMask.Sample(sampler_linear_repeat, i.uv.xy * _SkyMask_ST.xy + _SkyMask_ST.zw).x + _SkyRange;
        diffuse.xyz = lerp(diffuse, skytex, skymask);
    #endif

    float4 final_color = 1.f;
    final_color.xyz = diffuse.xyz ;

    float id;
    get_id(lightmap.w, id);
   int array_index = material_region(lightmap.w);

    // this isnt working at the moment and needs to be relooked at when more of the shader is implemented: 
    // float shadow = SampleURPMainLightShadow(i.ws_pos);
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
    float sdw_factor = max(SampleURPMainLightShadow(i.ws_pos), 0.5f); // sdw_combine
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
    float char_self_shadow = SampleCharacterSelfShadowAtlas(i.ws_pos.xyz, normal, light);
    sdw_factor *= char_self_shadow;
    sdw_factor = max(sdw_factor,  0.5f);

    float3 rsdw_view = normalize(vView - _RimShadowOffset.xyz);
    float rsdw_ndotv = saturate(dot(rsdw_view, vNormal));
    #if defined(_WITHSTOCKINGS)
            float pattern = _StockRangeTex.Sample(sampler_linear_repeat, i.uv.xy * _StockRangeTex_ST.xy + _StockRangeTex_ST.zw).z;
            float roughness = pattern * 0.5 - 0.5;
            roughness = _StockRoughness * roughness.x + 1.0;
            float2 stocking_masks = _StockRangeTex.Sample(sampler_linear_repeat, i.uv.xy).xy;
            float stock_pow = max(_Stockpower, 0.04f);
            float stock_rim = max(rsdw_ndotv.x, 0.001f);
            float stock_dark = stock_pow * _StockDarkWidth;
            stock_dark = max(stock_dark, 0.0);
            stock_pow = smoothstep(stock_pow, stock_dark, stock_rim);
            stock_pow = stock_pow * _StockSP;
            stock_pow = (0.001f < stocking_masks.x) ? stock_pow : 0.0;
            stock_pow = stocking_masks.x * stock_pow;
            float3 stock_color = lerp(1.0f, _StockDarkcolor, stock_pow);
            stock_color =  lerp(1.0f, diffuse.xyz * stock_color, stock_pow);
            stock_pow = roughness.x * stocking_masks.y;
            float thickness = (-_Stockthickness) + 1.0;
            stock_pow = stock_pow * thickness;
            stock_rim = max(pow(stock_rim, _Stockpower1), 0.004f);
            stock_pow = saturate(stock_pow * stock_rim);
            stock_pow = clamp(stock_pow, 0.0, 1.0);
            final_color.xyz  =lerp(diffuse * stock_color, _Stockcolor, stock_pow);
    #endif
    // shadow
    float ao_area = (lightmap.y + lightmap.y) * i.color.x;
    float sdw_area = saturate(ndotl * 0.5 + 0.5);
    sdw_area = dot(sdw_area.xx, ao_area.xx);

    sdw_factor =  0.5<_ES_CharacterDisableLocalMainLight ? sdw_factor : lerp(sdw_factor, 1.f , _CharacterLocalMainLightPosition.w);
    // sdw_factor = 1.f;
    float sdw_combine = (sdw_factor.x * sdw_area);
    sdw_combine = min(indoor_sdw.x, sdw_combine);
    sdw_combine.x = max(sdw_combine, 0.001f);
    sdw_combine.x = sdw_combine.x * 0.85f + 0.15f;
    sdw_combine = (_ShadowRamp<sdw_combine) ? 0.99f : sdw_combine.x;
    sdw_area = lightmap.y * i.color.x;
    sdw_area = min(sdw_area, 0.8f);
    sdw_area = (sdw_factor.x < 0.1f) ? sdw_area : 1.0;
    float2 ramp_uv;
    ramp_uv.x = (sdw_area * sdw_combine);
 ;
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

    #if defined(_CHARACTER_SPECIAL_FEATURE)
        float check = _UseMoonHalo;
        float moon_area = 0.95f < i.color.y;

        // get both moon halo smoothsteps
        float range = frac(-_MoonHaloRange);
        float moon_x = smoothstep(_MoonAnim.x, 1.0f, range);
        float moon_y = smoothstep(_MoonAnim.y, 1.0f, range);

        float moon = moon_x * moon_y;
        moon =  moon * 0.5 - 0.5;
        float2 moon_dir = moon * _MoonDir.xy;
        float2 moon_uv =  -moon_dir + i.uv;

        float mlength = length(moon_uv);
        float uv_x = i.uv.x + (-_MoonDir.x);
        uv_x = uv_x * 2.0f + _MoonDir.x;
        float uv_y =  i.uv.y;
        moon_uv = -moon_uv + float2(uv_x, uv_y);
        moon_uv.x = length(moon_uv.xy);

        moon_uv.x = lerp(mlength, moon_uv.x, _MoonUVType);
        float real_moon = smoothstep(_MoonDir.w, _MoonDir.z, moon_uv.x);
        float3 moon_color =  real_moon.xxx * final_color.xyz - final_color.xyz;
        moon_color = moon_area * moon_color + final_color.xyz;
        final_color.xyz =  _UseMoonHalo ? moon_color : final_color.xyz;
    #endif
    // after the moon was the rimshadow
    float3 rsdw_color;
    float2 rsdw_param;
    float3 rsdw =  1.0f;
        if(_UseMaterialValuesLUT)
        {
            float4 id_uv;
            id_uv.x = uint(int(id));
            id_uv.y = uint(5u);
            id_uv.z = uint(0u);
            id_uv.w = uint(6u);

            rsdw_color = _MaterialValuesPackLUT.Load(float4(id, 5u, 0u, 0u)).xyz;
            rsdw_param = _MaterialValuesPackLUT.Load(float4(id, 6u, 0u, 0u)).xy;
        }
        else
        {
            [forcecase]
            switch(array_index)
            {
            case 0:
                rsdw_color = _RimShadowColor0.xyz;
                rsdw_param = float2(_RimShadowWidth0, _RimShadowFeather0);
                break;
            case 1:
                rsdw_color = _RimShadowColor1.xyz;
                rsdw_param = float2(_RimShadowWidth1, _RimShadowFeather1);
                break;
            case 2:
                rsdw_color = _RimShadowColor2.xyz;
                rsdw_param = float2(_RimShadowWidth2, _RimShadowFeather2);
                break;
            case 3:
                rsdw_color = _RimShadowColor3.xyz;
                rsdw_param = float2(_RimShadowWidth3, _RimShadowFeather3);
                break;
            case 4:
                rsdw_color = _RimShadowColor4.xyz;
                rsdw_param = float2(_RimShadowWidth4, _RimShadowFeather4);
                break;
            case 5:
                rsdw_color = _RimShadowColor5.xyz;
                rsdw_param = float2(_RimShadowWidth5, _RimShadowFeather5);
                break;
            case 6:
                rsdw_color = _RimShadowColor6.xyz;
                rsdw_param = float2(_RimShadowWidth6, _RimShadowFeather6);
                break;
            default:
                rsdw_color = _RimShadowColor7.xyz;
                rsdw_param = float2(_RimShadowWidth7, _RimShadowFeather7);
                break;
            }
        }

        rsdw_color.xyz = rsdw_color.xyz * (_ES_RimShadowColor.www * _ES_RimShadowColor.xyz);
        rsdw_ndotv = pow(max( 1.0 - rsdw_ndotv.x, 0.001f), _RimShadowCt);
        rsdw_ndotv.x = saturate(rsdw_ndotv.x * rsdw_param.x);
        rsdw_ndotv.x = smoothstep(rsdw_param.y, 1.0f, rsdw_ndotv.x);
        rsdw_ndotv.x = rsdw_ndotv.x * _RimShadowIntensity;
        rsdw_ndotv.x = rsdw_ndotv.x * _ES_RimShadowIntensity;
        rsdw_ndotv.x = rsdw_ndotv.x * 0.25;
        rsdw = lerp(1.0f, rsdw_color.xyz * 2.0f, rsdw_ndotv.xxx);
    
    float3 spec_color;
    float spec_param;
    if(_UseMaterialValuesLUT)
    {
        float4 id_uv;
        id_uv.x = uint(int(id));
        id_uv.y = uint(1u);
        id_uv.z = uint(0u);
        id_uv.w = uint(6u);

        spec_color = _MaterialValuesPackLUT.Load(float4(id, 0, 0, 0)).xyz;
        spec_param = _MaterialValuesPackLUT.Load(float4(id, 1, 0, 0)).z;
    }
    else
    {
        float4 specular_color[8] =
        {
            _SpecularColor0,
            _SpecularColor1,
            _SpecularColor2,
            _SpecularColor3,
            _SpecularColor4,
            _SpecularColor5,
            _SpecularColor6,
            _SpecularColor7,
        };

        float specular_values[8] =
        {
            float(_SpecularIntensity0),
            float(_SpecularIntensity1),
            float(_SpecularIntensity2),
            float(_SpecularIntensity3),
            float(_SpecularIntensity4),
            float(_SpecularIntensity5),
            float(_SpecularIntensity6),
            float(_SpecularIntensity7),
        };

        spec_color = (specular_color[array_index] * lerp(1.0f, _ES_SPColor, _ES_SPColor.www)) * _ES_SPIntensity;
        spec_param = specular_values[array_index].x;
    }

    #if defined(_USE_MATCAP)
        
        vNormal.xy = vNormal.xy * 0.5 + 0.5;
        // vNormal.y = 1.0f - vNormal.y;
        float3 matcap = _MatCapTex.Sample(sampler_linear_repeat, vNormal.xy).xyz;
        float matcap_mask = _MatCapMaskTex.Sample(sampler_linear_repeat, i.uv.xy).x * lightmap.z;
        sdw_combine.x = saturate(sdw_combine.x * 5.0 - 4.0);
        float strength = lerp(_MatCapStrength * _MatCapStrengthInShadow, _MatCapStrength, sdw_combine);
        float3 tmp_color = strength * matcap.xyz;
        tmp_color.xyz = tmp_color.xyz * _MatCapColor.xyz;
        tmp_color.xyz = matcap_mask * tmp_color.xyz;
        spec_color.xyz = spec_color.xyz * tmp_color.xyz;
        spec_color.xyz = spec_param * spec_color.xyz;
        float ceil_mask = ceil(saturate(matcap_mask - 0.01f));
        spec_color.xyz = spec_color.xyz * ceil_mask;
        final_color.xyz = final_color.xyz * rsdw.xyz +  spec_color.xyz;
    #else
        final_color.xyz *= rsdw;
    #endif

    

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
        heightlightlerp(float4(i.ws_pos, 1.f), final_color);
    #endif

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif

    // final_color.xyz = shadow;

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
    float2 skymask = float2(1,1);
    #if defined(_STATTYSKY)
        float3 skytex = _SkyTex.Sample(sampler_linear_repeat, i.uv.xy * _SkyTex_ST.xy + _SkyTex_ST.zw);
        skymask = _SkyMask.Sample(sampler_linear_repeat, i.uv.xy * _SkyMask_ST.xy + _SkyMask_ST.zw).xy + _SkyRange;
        diffuse.xyz = lerp(diffuse, skytex, skymask.x);
    #endif

    float id;
    get_id(lightmap.w, id);
    int array_index = material_region(lightmap.w);
    

    float3 light;
    float3 light_color;
    get_light(light, light_color);

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

    shadow_factor = SampleCharacterSelfShadow(i.ws_pos.xyz, normal, light);

    // float3 vView = mul(view, (float3x3)unity_MatrixV);
    // float3 vNormal = mul(normal, (float3x3)unity_MatrixV);

    float ndotl = dot(normal, light);
    float ndotv = dot(normal, view);
    float ndoth = dot(normal, half_vec);

    float3 spec_color;
    float3 spec_param; // roughness intensity shininess
    float custom_param1;
    float custom_param2;
    if(_UseMaterialValuesLUT)
    {
        float4 matlut_tmpa = _MaterialValuesPackLUT.Load(float4(id.x, 0, 0, 0));
        float4 matlut_tmpb = _MaterialValuesPackLUT.Load(float4(id.x, 1, 0, 0));
        spec_color = matlut_tmpa.xyz;
        spec_param = matlut_tmpb.xyz;
        custom_param1 = matlut_tmpa.w;
        custom_param2 = matlut_tmpb.w;
    }
    else
    {   
        [forcecase]
        switch(array_index)
        {
        case 0:
            spec_color = _SpecularColor0.xyz;
            spec_param = float3(_SpecularShininess0, _SpecularRoughness0, _SpecularIntensity0);
            custom_param1 = _CustomParamA0;
            custom_param2 = _CustomParamB0;
            break;
        case 1:
            spec_color = _SpecularColor1.xyz;
            spec_param = float3(_SpecularShininess1, _SpecularRoughness1, _SpecularIntensity1);
            custom_param1 = _CustomParamA1;
            custom_param2 = _CustomParamB1;
            break;
        case 2:
            spec_color = _SpecularColor2.xyz;
            spec_param = float3(_SpecularShininess2, _SpecularRoughness2, _SpecularIntensity2);
            custom_param1 = _CustomParamA2;
            custom_param2 = _CustomParamB2;
            break;
        case 3:
            spec_color = _SpecularColor3.xyz;
            spec_param = float3(_SpecularShininess3, _SpecularRoughness3, _SpecularIntensity3);
            custom_param1 = _CustomParamA3;
            custom_param2 = _CustomParamB3;
            break;
        case 4:
            spec_color = _SpecularColor4.xyz;
            spec_param = float3(_SpecularShininess4, _SpecularRoughness4, _SpecularIntensity4);
            custom_param1 = _CustomParamA4;
            custom_param2 = _CustomParamB4;
            break;
        case 5:
            spec_color = _SpecularColor5.xyz;
            spec_param = float3(_SpecularShininess5, _SpecularRoughness5, _SpecularIntensity5);
            custom_param1 = _CustomParamA5;
            custom_param2 = _CustomParamB5;
            break;
        case 6:
            spec_color = _SpecularColor6.xyz;
            spec_param = float3(_SpecularShininess6, _SpecularRoughness6, _SpecularIntensity6);
            custom_param1 = _CustomParamA6;
            custom_param2 = _CustomParamB6;
            break;
        default:
            spec_color = _SpecularColor7.xyz;
            spec_param = float3(_SpecularShininess7, _SpecularRoughness7, _SpecularIntensity7);
            custom_param1 = _CustomParamA7;
            custom_param2 = _CustomParamB7;
            break;
        }
        
    }
    spec_color = (spec_color * lerp(1.0f, _ES_SPColor, _ES_SPColor.www)) * _ES_SPIntensity;

    float3 specular = ndoth;
    specular = pow(max(specular, 0.00f), spec_param.x) * shadow_factor;
    float store_specular = specular;
    spec_param.y = max(spec_param.y, 0.001f);
    

    float specular_thresh = 1.0f - lightmap.z;

    specular = smoothstep(specular_thresh - spec_param.y, specular_thresh + spec_param.y, specular) * spec_color * spec_param.z;
    
    #if defined(_USE_GLINT)
        float3 glintObjectToCamera = normalize(_WorldSpaceCameraPos.xyz + (-unity_ObjectToWorld[3].xyz));
        // float glintObjectToCameraLenInv = inversesqrt(dot(glintObjectToCamera, glintObjectToCamera));
        // glintObjectToCamera = glintObjectToCameraLenInv * glintObjectToCamera;
        float3 glint_uv;
        glint_uv.xy = (vface) ? i.uv.xy : i.uv.zw;
        glint_uv.z = glint_uv.y * _GlintUVTillingY;
        float2 glintAxisSource = normal.zx;
        float glintCellScaleFactor = custom_param1;
        float glintDensityFactor = custom_param2;
        float glintPointInput = specular;
        float3 glintBaseLit = specular * diffuse.xyz;
        bool useWorldPosGlintUV = 0.899999976 < _GlintWorldPosUV;
        float2 wsAxisSelector = abs(glintAxisSource) + float2(-0.5, -0.5);
        wsAxisSelector = ceil(clamp(wsAxisSelector, 0.0, 1.0));
        float4 wsMixA = (-i.ws_pos.xzxz) + i.ws_pos.xyxy;
        wsMixA = wsAxisSelector.xxxx * wsMixA + i.ws_pos.xzxz;
        float4 wsMixB = (-wsMixA.zwzw) + i.ws_pos.yzyz;
        float4 glintCoord4 = wsAxisSelector.yyyy * wsMixB + wsMixA;
        glintCoord4 = useWorldPosGlintUV ? glintCoord4 : glint_uv.xzxz;
        float glintFaceScale = (vface) ? 1.0 : _GlintScaleBackface;
        glintCoord4 = glintFaceScale.xxxx * glintCoord4;

        float4 glintMaskSample = _GlintMask.Sample(sampler_linear_clamp, glint_uv.xy);
        float glintMaskAlpha = glintMaskSample.w;
        float4 glintCell4 = glintCoord4.zwzw * _GlintScale;
        glintCell4 = glintCellScaleFactor.xxxx * glintCell4;
        float2 glintCellFrac = frac(glintCell4.zw);
        glintCell4 = floor(glintCell4);

        float glintConcentrationLerp = saturate(_GlintConcentration * 10.0);
        float glintPointScaleValue = glintPointInput * _GlintIntensity;
        glintPointScaleValue = pow(glintPointScaleValue, _GlintConcentration);
        glintPointScaleValue = max(glintPointScaleValue, 0.00999999978);
        glintPointScaleValue = glintPointScaleValue + (-_GlintPointScale);
        glintPointScaleValue = glintConcentrationLerp * glintPointScaleValue + _GlintPointScale;
        float glintPointBase = glintMaskAlpha * glintPointScaleValue;

        float hashA = glint_hash12(glintCell4.zw);
        float2 hashOffsetA = hashA + glintCell4.zw;
        float hashB = glint_hash12(hashOffsetA);
        float4 neighborhoodA = glintCell4.zwzw + float4(0.454869986, 0.454869986, 5.415452, 5.415452);
        float2 randA;
        randA.x = glint_hash12(neighborhoodA.xy);
        neighborhoodA.xy = neighborhoodA.xy + randA.xx;
        randA.y = glint_hash12(neighborhoodA.xy);
        randA = randA * 2 - 1;
        float densityA = glint_hash12(neighborhoodA.zw);
        float2 localOffsetA = glintCellFrac + float2(-0.5, -0.5);
        randA = randA * float2(0.400000006, 0.400000006);
        randA = localOffsetA - (randA * _GlintRandom);
        float distanceGateA = glint_distance_gate(randA, hashA, glintPointBase);
        densityA = densityA + -1.0;
        densityA = glint_density_gate(densityA, glintDensityFactor);
        densityA = densityA * distanceGateA;

        float glintSparkleHalf = _GlintSparkle * 0.5;
        float glintTimePhase = (_Time.x * 50.0) * _GlintSparkFreq;
        float sparkAccum = glint_spark_wave(hashB, glintTimePhase, hashA, glintSparkleHalf, 3.14);
        float4 randDirAccum = float4(glint_random_unit(hashA, hashB), 1.0);

        float4 neighborhoodB = glintCell4.zwzw + float4(1.0, 0.0, 1.45486999, 0.454869986);
        hashA = glint_hash12(neighborhoodB.xy);
        float2 hashOffsetB = hashA + neighborhoodB.xy;
        hashB = glint_hash12(hashOffsetB);
        randA.x = glint_hash12(neighborhoodB.zw);
        neighborhoodB.xy = randA.xx + neighborhoodB.zw;
        randA.y = glint_hash12(neighborhoodB.xy);
        randA = randA * 2 - 1;
        neighborhoodB = glintCell4.zwzw + float4(6.415452, 5.415452, -1.0, 0.0);
        float densityB = glint_hash12(neighborhoodB.xy);
        randA = randA * float2(0.400000006, 0.400000006);
        randA = (-randA) * _GlintRandom + glintCellFrac + float2(-1.5, -0.5);
        float distanceGateB = glint_distance_gate(randA, hashA, glintPointBase);
        densityB = glint_density_gate(densityB, glintDensityFactor);
        densityB = densityB * distanceGateB;
        float sparkB = glint_spark_wave(hashB, glintTimePhase, hashA, glintSparkleHalf, 3.14);
        sparkB = densityB * sparkB;
        sparkAccum = sparkAccum * densityA + sparkB;

        float4 randDirB = float4(glint_random_unit(hashA, hashB), 1.0);
        randDirB = densityB * randDirB;
        randDirAccum = randDirAccum * densityA + randDirB;

        hashA = glint_hash12(neighborhoodB.zw);
        hashOffsetB = hashA + neighborhoodB.zw;
        hashB = glint_hash12(hashOffsetB);
        float4 neighborhoodC = glintCell4.zwzw + float4(-0.545130014, 0.454869986, 4.415452, 5.415452);
        randA.x = glint_hash12(neighborhoodC.xy);
        neighborhoodC.xy = neighborhoodC.xy + randA.xx;
        randA.y = glint_hash12(neighborhoodC.xy);
        randA = randA * 2.0 - 1.0;
        float densityC = glint_hash12(neighborhoodC.zw);
        randA = randA * float2(0.4, 0.4);
        randA = (-randA) * _GlintRandom + glintCellFrac + float2(0.5, -0.5);
        float distanceGateC = glint_distance_gate(randA, hashA, glintPointBase);
        densityC = glint_density_gate(densityC, glintDensityFactor);
        densityC = densityC * distanceGateC;
        float sparkC = glint_spark_wave(hashB, glintTimePhase, hashA, glintSparkleHalf, 3.14);
        sparkAccum = sparkC * densityC + sparkAccum;
        randDirAccum = float4(glint_random_unit(hashA, hashB), 1.0) * densityC + randDirAccum;

        float4 neighborhoodD = glintCell4.zwzw + float4(0.0, 1.0, 0.454869986, 1.45486999);
        hashA = glint_hash12(neighborhoodD.xy);
        neighborhoodD.xy = hashA + neighborhoodD.xy;
        hashB = glint_hash12(neighborhoodD.xy);
        randA.x = glint_hash12(neighborhoodD.zw);
        float2 hashOffsetD = randA.xx + neighborhoodD.zw;
        randA.y = glint_hash12(hashOffsetD);
        randA = randA * 2 - 1;
        float4 neighborhoodE = glintCell4.zwzw + float4(5.415452, 6.415452, 0.0, -1.0);
        float densityD = glint_hash12(neighborhoodE.xy);
        randA = randA * float2(0.400000006, 0.400000006);
        randA = (-randA) * _GlintRandom + glintCellFrac + float2(-0.5, -1.5);
        float distanceGateD = glint_distance_gate(randA, hashA, glintPointBase);
        densityD = glint_density_gate(densityD, glintDensityFactor);
        densityD = densityD * distanceGateD;
        float sparkD = glint_spark_wave(hashB, glintTimePhase, hashA, glintSparkleHalf, 3.14);
        sparkAccum = sparkD * densityD + sparkAccum;
        randDirAccum = float4(glint_random_unit(hashA, hashB), 1.0) * densityD + randDirAccum;

        hashA = glint_hash12(neighborhoodE.zw);
        hashOffsetD = hashA + neighborhoodE.zw;
        hashB = glint_hash12(hashOffsetD);
        glintCell4 = glintCell4 + float4(0.454869986, -0.545130014, 5.415452, 4.415452);
        randA.x = glint_hash12(glintCell4.xy);
        glintCell4.xy = glintCell4.xy + randA.xx;
        randA.y = glint_hash12(glintCell4.xy);
        randA = randA * 2 - 1;
        float densityE = glint_hash12(glintCell4.zw);
        float2 localOffsetE = glintCellFrac + float2(-0.5, 0.5);
        randA = randA * float2(0.4, 0.4);
        localOffsetE = (-randA) * _GlintRandom + localOffsetE;
        float distanceGateE = glint_distance_gate(localOffsetE, hashA, glintPointBase);
        densityE = glint_density_gate(densityE, glintDensityFactor);
        distanceGateE = densityE * distanceGateE;
        float sparkE = glint_spark_wave(hashB, glintTimePhase, hashA, glintSparkleHalf, 3.14);
        float sparkFinal = sparkE * distanceGateE + sparkAccum;

        float4 randDirE = float4(glint_random_unit(hashA, hashB), 1.0);
        float4 randDirSum = randDirE * distanceGateE.xxxx + randDirAccum;
        bool hasRandDirWeight = 0.00999999978 < randDirSum.w;
        float3 randDirNormalized = randDirSum.xyz / randDirSum.www;
        randDirSum.xyz = hasRandDirWeight ? randDirNormalized : randDirSum.xyz;

        float glintMaskBias = glintPointScaleValue * glintMaskAlpha + -1.0;
        float glintViewMask = glintConcentrationLerp * glintMaskBias + 1.0;
        float glintViewPhase = dot(randDirSum.xyz, view.xyz);
        glintViewPhase = glintViewPhase * _GlintViewFreq;
        glintViewPhase = frac(glintViewPhase);
        glintViewPhase = glintViewMask * glintViewPhase;
        float sparkScale = sparkFinal + 0.8;
        sparkScale = max(sparkScale, 0.0);
        sparkScale = min(sparkScale, 3.0);
        float localGlintIntensity = glintViewPhase * sparkScale;

        float4 globalGlintCoord4 = glintCoord4 * _GlobalGlintScale;
        globalGlintCoord4 = glintCellScaleFactor.xxxx * globalGlintCoord4;
        float2 globalGlintFrac = frac(globalGlintCoord4.zw);
        globalGlintCoord4 = round(globalGlintCoord4);

        randA.x = glint_hash12(globalGlintCoord4.zw);
        float2 globalHashOffset = globalGlintCoord4.zw + randA.xx;
        randA.y = glint_hash12(globalHashOffset);
        randA = randA * 2 - 1;
        globalGlintFrac = randA * 0.5 + globalGlintFrac;

        float4 globalHashCoord4 = globalGlintCoord4 * float4(0.0386548117, 0.0386548117, 58.3610001, 58.3610001);
        hashB = glint_hash12(globalHashCoord4.xy);
        float2 globalHashCoord = globalGlintCoord4.xy * float2(0.0386548117, 0.0386548117) + hashB;
        float globalHashRnd = glint_hash12(globalHashCoord);

        float azimuth = hashB * 6.28318024;
        float elevationCosSeed = (-globalHashRnd) * 2.0 + 1.0;
        float elevationSin = sqrt(-abs(elevationCosSeed) + 1.0);
        float poly = abs(elevationCosSeed) * -0.0187292993 + 0.0742610022;
        poly = poly * abs(elevationCosSeed) + -0.212114394;
        poly = poly * abs(elevationCosSeed) + 1.57072878;
        float angleFix = elevationSin * poly;
        angleFix = angleFix * -2.0 + 3.14159274;
        bool useAngleFix = elevationCosSeed < (-elevationCosSeed);
        float elevationAngle = useAngleFix ? angleFix : float(0.0);
        elevationAngle = poly * elevationSin + elevationAngle;
        float sinElevation = sin(elevationAngle);
        float cosElevation = cos(elevationAngle);
        float sinAzimuth = sin(azimuth);
        float cosAzimuth = cos(azimuth);

        float3 globalRandDir = saturate(float3(sinElevation * cosAzimuth, sinElevation * sinAzimuth, cosElevation));

        hashB = glint_hash12(globalHashCoord4.zw);
        globalHashCoord = globalGlintCoord4.zw * float2(58.3610001, 58.3610001) + hashB;
        globalHashRnd = glint_hash12(globalHashCoord);
        float globalSparkEnvelope = globalRandDir.y * _GlobalGlintSparkFreq + hashB;
        globalSparkEnvelope = frac(globalSparkEnvelope);
        globalSparkEnvelope = globalSparkEnvelope - 0.5;
        globalSparkEnvelope = abs(globalSparkEnvelope) * _GlobalGlintSparkle + 0.300000012;

        float2 globalPointOffset = globalGlintFrac - 0.5;
        float globalPointDistance = length(globalPointOffset);
        float globalPointRadiusBase = _GlobalGlintPointScale * 0.399999976;
        float globalPointRadiusRand = frac(globalHashRnd);
        float globalPointRadius = globalPointRadiusBase * globalPointRadiusRand + 0.0199999996;
        bool globalPointMaskedOut = globalPointRadius < globalPointDistance;

        float globalViewPhase = dot(globalRandDir, glintObjectToCamera);
        globalViewPhase = globalViewPhase * _GlobalGlintViewFreq;
        globalViewPhase = frac(globalViewPhase);
        float globalDensityThreshold = (-_GlobalGlintDensity) * glintDensityFactor + 1.0;
        globalDensityThreshold = clamp(globalDensityThreshold, 0.0, 1.0);
        int globalDensityPassMask = int((globalDensityThreshold < globalViewPhase) ? 0xFFFFFFFFu : uint(0));
        int globalGlintPassMask = globalPointMaskedOut ? 0 : globalDensityPassMask;
        float globalSparkMasked = (globalGlintPassMask != 0) ? globalSparkEnvelope : 0.0;

        float globalIntensityDenom = _GlobalGlintPointScale * 5.0 + 0.5;
        float globalGlintScalar = globalSparkMasked / globalIntensityDenom; 
        float3 globalGlintBase = globalGlintScalar * _GlobalGlintColor.xyz;
        globalGlintBase = globalGlintBase * _GlobalGlintIntensity;

        float3 glintMaskColor = glintMaskSample.xyz * glintMaskAlpha;
        float3 glintCombinedColor = globalGlintBase * _GlobalGlintColor.xyz;
        glintCombinedColor = glintCombinedColor * _GlobalGlintIntensity;
        glintCombinedColor = localGlintIntensity * _GlintColor.xyz + glintCombinedColor;
        glintMaskColor = glintMaskColor * glintCombinedColor;
        specular = glintBaseLit + glintMaskColor;

    #endif 

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

        float3 rim_color;
        float3 rim_values;
        if(_UseMaterialValuesLUT)
        {

            rim_color = _MaterialValuesPackLUT.Load(float4(id.x, 3, 0, 0)).xyz;
            rim_values = _MaterialValuesPackLUT.Load(float4(id.x, 4, 0, 0)).yzx;
        }
        else
        {

            [forcecase]
            switch(array_index)
            {
                case 0:
                    rim_color = _RimColor0;
                    rim_values = float3(_RimEdgeSoftness0, _RimType0, _RimDark0);
                    break;
                case 1:
                    rim_color = _RimColor1;
                    rim_values = float3(_RimEdgeSoftness1, _RimType1, _RimDark1);
                    break;
                case 2:
                    rim_color = _RimColor2;
                    rim_values = float3(_RimEdgeSoftness2, _RimType2, _RimDark2);
                    break;
                case 3:
                    rim_color = _RimColor3;
                    rim_values = float3(_RimEdgeSoftness3, _RimType3, _RimDark3);
                    break;
                case 4:
                    rim_color = _RimColor4;
                    rim_values = float3(_RimEdgeSoftness4, _RimType4, _RimDark4);
                    break;
                case 5:
                    rim_color = _RimColor5;
                    rim_values = float3(_RimEdgeSoftness5, _RimType5, _RimDark5);
                    break;
                case 6:
                    rim_color = _RimColor6;
                    rim_values = float3(_RimEdgeSoftness6, _RimType6, _RimDark6);
                    break;
                default:
                    rim_color = _RimColor7;
                    rim_values = float3(_RimEdgeSoftness7, _RimType7, _RimDark7);
                    break;
            }
            

        }

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

        float3 ll_color = lerp(rim_color, _CharacterLocalMainLightColor1, cdotnl); // u_xlat3
        // u_xlat16_20.x = ndotl; 


        // float org_depth = 1.0f / (_ZBufferParams.x * i.pos.z + _ZBufferParams.y);
        float org_depth = Linear01Depth(_DepthBufferOrCopy.Sample(sampler_linear_clamp, screen_uv.xy).r, _ZBufferParams);
        rim_width = rim_width / (org_depth * _ProjectionParams.z + 3.0); 

        float2 depth_uv;
        depth_uv.x = ((_ES_RimLightOffset.x + _RimOffset.x) * 0.01f + rim_width) + screen_uv.x;
        depth_uv.y = ((_ES_RimLightOffset.y + _RimOffset.y) * 0.01f + screen_uv.y);
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


        // sampled_depth = (lut_rimval.y < sampled_depth) ? sampled_depth : 0.0f;
        // final_color.xyz = ndotl;
        // final_color.xyz = sampled_depth;  
    }
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

    #if defined(_FLAMECRYSTALEFFECT)

        float3 effect_color = _EffectColor0;
        [forcecase]
        switch(array_index)
        {
        case 0:
            effect_color = _EffectColor1.xyz;
            break;
        case 1:
            effect_color = _EffectColor2.xyz;
            break;
        case 2:
            effect_color = _EffectColor3.xyz;
            break;
        case 3:
            effect_color = _EffectColor4.xyz;
            break;
        case 4:
            effect_color = _EffectColor5.xyz;
            break;
        case 5:
            effect_color = _EffectColor6.xyz;
            break;
        case 6:
            effect_color = _EffectColor7.xyz;
            break;
        default:
            effect_color = _EffectColor0.xyz;
            break;
        }

        float2 crystal_sample = _CrystalTex.Sample(sampler_linear_repeat, i.uv.xy).xy;
        float crystal_alpha = crystal_sample.y;
        float crystal_inverted_alpha = 1.0 - crystal_alpha;
        
        float crystal_transparency_value = max(ndoth, 0.001);
        crystal_transparency_value = pow(crystal_transparency_value, 8.0);
        crystal_transparency_value = saturate(crystal_transparency_value * _CrystalTransparency + crystal_sample.x);
        crystal_transparency_value = 1.0 - crystal_transparency_value;
        
        float crystal_range_1 = saturate(_CrystalRange1 * 2.0 - crystal_alpha);
        float crystal_range_2_calc = 1.0 - crystal_inverted_alpha;
        float crystal_range_2_offset = _CrystalRange2 - 0.5;
        crystal_range_2_calc = saturate(crystal_range_2_offset * 2.0 + crystal_range_2_calc);
        
        float crystal_mask = min(crystal_range_1, crystal_range_2_calc);
        float crystal_intensity = crystal_transparency_value * crystal_mask;
        crystal_intensity = min(crystal_intensity, _ColorIntensity);
        
        float3 effect_color_darkened = effect_color - 1.0;
        effect_color_darkened = crystal_intensity * effect_color_darkened + 1.0;
        float3 effect_color_masked = crystal_mask * effect_color;
        effect_color_masked = (1.0 - crystal_transparency_value) * effect_color_masked;
        
        float3 crystal_final = final_color.xyz * effect_color_darkened + effect_color_masked;
        
        bool is_flame_id = (array_index == _FlameID);
        if(is_flame_id)
        {
            float4 weird_view = length(i.view.xyz) * i.view.zxyz;
            float3 flame_axis;
            flame_axis.xy = weird_view.zw * i.tangent.zx;
            flame_axis.xy = i.tangent.yz * weird_view.xy + (-flame_axis);
            flame_axis.xy = flame_axis + 1.0;
            flame_axis.xy = flame_axis * 0.5;
            flame_axis.xy = saturate(flame_axis);
            
            float flame_mask = smoothstep(_FlameWidth + 0.5, _FlameWidth, flame_axis.x);
            flame_axis.z = flame_mask;
            
            float2 flame_uv = _FlameSpeed * _Time.yy + flame_axis.yx;
            float flame_sample_1 = _FlameTex.Sample(sampler_linear_repeat, flame_uv).x;
            
            float2 flame_swirl_uv = i.uv.xy * _FlameSwirilTexScale + _Time.y * _FlameSwirilSpeed;
            float flame_sample_swirl = _FlameTex.Sample(sampler_linear_repeat, flame_swirl_uv).y;
            
            float flame_combined = flame_sample_swirl + flame_sample_1;
            float2 flame_final_uv = flame_combined * _FlameSwirilScale + flame_axis.yz;
            float flame_sample_final = _FlameTex.Sample(sampler_linear_repeat, flame_final_uv).z;
            
            float4 flame_height = smoothstep(_FlameHeight, _FlameHeight + 0.25, crystal_inverted_alpha * flame_sample_final);
            flame_height.x = crystal_transparency_value * flame_height.x;
            
            float3 flame_color = lerp(_FlameColorOut, _FlameColorIn, flame_height.xxx);
            final_color.xyz = lerp(final_color.xyz, flame_color, flame_height.xxx);
        }
    #endif

    #if defined(_HEIGHTLERP)
        heightlightlerp(float4(i.ws_pos, 1.f), final_color);
    #endif

    #if defined(_STATTYSKY)
        #ifndef _FAKEREFLECTION
            float2 screenCenterOffset = screen_uv.xy - 0.5f;
            float2 depthAdjustedOffset = length(view) * screenCenterOffset;
            float2 scaledDepthOffset = depthAdjustedOffset * _SkyStarDepthScale;
            float2 starTexUV = scaledDepthOffset * _SkyStarTex_ST.xy + _SkyStarTex_ST.zw;
            float2 animatedStarTexUV = _Time.yy * _SkyStarSpeed.xy + starTexUV;
            float starTexSample = _SkyStarTex.Sample(sampler_linear_repeat, animatedStarTexUV).x;
            float3 starColor = starTexSample * _SkyStarColor.xyz;
            starColor = starColor * _SkyStarTexScale;
            starColor = skymask.xxx * starColor;
            
            float2 maskTexUV = i.uv.xy * _SkyStarMaskTex_ST.xy + _SkyStarMaskTex_ST.zw;
            float2 forwardAnimMaskUV = _Time.yy * _SkyStarMaskTexSpeed + maskTexUV;
            float3 forwardMaskSample = _SkyStarMaskTex.Sample(sampler_linear_repeat, forwardAnimMaskUV).xyz;
            float2 backwardAnimMaskUV = (-_Time.yy) * _SkyStarMaskTexSpeed + maskTexUV;
            float3 backwardMaskSample = _SkyStarMaskTex.Sample(sampler_linear_repeat, backwardAnimMaskUV).xyz;
            float3 combinedMaskSamples = forwardMaskSample + backwardMaskSample;
            float3 scaledMask = combinedMaskSamples * _SkyStarMaskTexScale;
            
            float objectSpaceX = i.os_pos.x / _OSScale;
            float halfOSScale = _OSScale * 0.5;
            float2 objectSpaceYZ = i.os_pos.yz / halfOSScale;
            float3 smoothstepValues = float3(
                smoothstep(1.0f, -1.0f, objectSpaceYZ.x),
                smoothstep(1.0f, -1.0f, objectSpaceYZ.y),
                smoothstep(1.0f, -1.0f, objectSpaceX)
            );
            float2 adjustedSmoothstepYZ = smoothstepValues.yz * 2.0f;
            
            float starAlpha = _SkyStarTex.Sample(sampler_linear_repeat, adjustedSmoothstepYZ).w;
            float2 starTexSample2 = _SkyStarTex.Sample(sampler_linear_repeat, i.uv.xy).yz;
            float densityAdjustedAlpha = (-starTexSample2.x) * _StarDensity + starAlpha;
            float densityComplement = 1.0f - _StarDensity;
            float normalizedDensity = saturate(densityAdjustedAlpha / densityComplement);
            
            float4 starTexCoords = smoothstepValues.xzyz * _SkyStarTex_ST.xyxy + _SkyStarTex_ST.zwzw;
            float starTexX = _SkyStarTex.Sample(sampler_linear_repeat, starTexCoords.xy).x;
            float starTexZ = _SkyStarTex.Sample(sampler_linear_repeat, starTexCoords.zw).x;
            float lerpedStarTex = lerp(starTexSample2, starTexX, starTexSample2.y);
            float3 densityModulatedStarColor = normalizedDensity * _SkyStarColor.xyz;
            densityModulatedStarColor = lerpedStarTex * densityModulatedStarColor;
            densityModulatedStarColor = densityModulatedStarColor * _SkyStarTexScale;
            
            float3 tangentNormalized = normalize(i.tangent.xyz);
            float3 viewSpaceTangent;
            viewSpaceTangent.x = dot(float3(unity_MatrixV[0].x, unity_MatrixV[1].x, unity_MatrixV[2].x), tangentNormalized);
            viewSpaceTangent.y = dot(float3(unity_MatrixV[0].y, unity_MatrixV[1].y, unity_MatrixV[2].y), tangentNormalized);
            viewSpaceTangent.z = dot(float3(unity_MatrixV[0].z, unity_MatrixV[1].z, unity_MatrixV[2].z), tangentNormalized);
            
            float tangentDotView = dot(viewSpaceTangent, view);
            float fresnelBase = (-tangentDotView) + 1.0;
            float fresnelPower2 = fresnelBase * fresnelBase;
            float fresnelPower4 = fresnelPower2 * fresnelPower2;
            float fresnelPower5 = fresnelPower2 * fresnelPower4;
            
            float fresnelSmoothOffset = _SkyFresnelSmooth + 0.5;
            float2 fresnelParams = (-float2(_SkyFresnelSmooth, _SkyFresnelBaise)) + float2(0.5, 1.0);
            float fresnelModulated = fresnelParams.y * fresnelPower5 + _SkyFresnelBaise;
            float fresnelSmoothed = smoothstep(fresnelParams, fresnelSmoothOffset, fresnelModulated);
            float fresnelFinal = fresnelSmoothed * _SkyFresnelScale;
            float3 fresnelContribution = fresnelFinal * _SkyFresnelColor.xyz;
            
            float3 baseSkyColor = starColor * scaledMask.xxx;
            baseSkyColor = skymask.xxx * baseSkyColor;
            float3 starContribution = densityModulatedStarColor * scaledMask.xyz + (-baseSkyColor);
            starContribution = _StarMode * starContribution + baseSkyColor;
            starContribution = fresnelContribution * skymask.yyy + starContribution;
            
            final_color.xyz += starContribution;
        #else
            float3 ref_vec = normalize(reflect(-view, normal));
            float reflection_ndoth = dot(ref_vec.xyz, normal.xyz);
            float reflection_power = pow(max(reflection_ndoth, 0.001f), _ReflectionRoughness);
            float reflection_threshold_mask = smoothstep(_ReflectionThreshold, _ReflectionThreshold + _ReflectionSoftness, reflection_power);
            float reflection_blend_mask = smoothstep(_ReflectionBlendThreshold, _ReflectionBlendThreshold + 0.05f, reflection_ndoth);
            float reflection_reversed_mask = smoothstep(_ReflectionReversedThreshold + 0.05f, _ReflectionReversedThreshold, reflection_ndoth);
            float3 reflection_blend_reversed = reflection_reversed_mask * _ReflectionBlendColor.xyz;
            reflection_blend_reversed.xyz = reflection_blend_reversed.xyz * _FakeRefBlendIntensity;
            float3 reflection_blend_forward = reflection_blend_mask * _ReflectionBlendColor.xyz;
            reflection_blend_forward.xyz = reflection_blend_forward.xyz * _FakeRefBlendIntensity;
            float3 reflection_additive = reflection_threshold_mask * _ReflectionColor.xyz;
            reflection_additive.xyz = reflection_additive.xyz * _FakeRefAddIntensity;
            reflection_additive.xyz = skymask.xxx * reflection_additive.xyz;
            reflection_blend_forward.xyz = reflection_blend_forward.xyz * skymask.xxx + reflection_additive.xyz;
            float3 reflection_combined = reflection_blend_reversed.xyz * skymask.yyy + reflection_blend_forward.xyz;
            final_color.xyz = final_color + reflection_combined.xyz;
        #endif
    #endif
    float3 bloom_color = 1;
    float bloom_int = 0;
    if(_UseMaterialValuesLUT)
    {
        bloom_color = _MaterialValuesPackLUT.Load(float4(id.x, 7, 0, 0)).xyz;
        bloom_int   = _MaterialValuesPackLUT.Load(float4(id.x, 6, 0, 0)).z;
    }
    else
    {
        switch(array_index)
        {
            case 0:
                bloom_color = _mBloomColor1.xyz;
                bloom_int = _mBloomIntensity1;
                break;
            case 1:
                bloom_color = _mBloomColor2.xyz;
                bloom_int = _mBloomIntensity2;
                break;
            case 2:
                bloom_color = _mBloomColor3.xyz;
                bloom_int = _mBloomIntensity3;
                break;
            case 3:
                bloom_color = _mBloomColor4.xyz;
                bloom_int = _mBloomIntensity4;
                break;
            case 4:
                bloom_color = _mBloomColor5.xyz;
                bloom_int = _mBloomIntensity5;
                break;
            case 5:
                bloom_color = _mBloomColor6.xyz;
                bloom_int = _mBloomIntensity6;
                break;
            case 6:
                bloom_color = _mBloomColor7.xyz;
                bloom_int = _mBloomIntensity7;
                break;
            default:
                bloom_color = _mBloomColor0.xyz;
                bloom_int = _mBloomIntensity0;
                break;
        }
    }
    bloom_color = bloom_int * bloom_color + 1.0f;
    final_color.xyz *=  bloom_color.xyz;

    #if defined(_FRAGMENT_CLIP)
        if(_EnableAlphaCutoff)
        {
            float test_val = diffuse.w + (-_AlphaTestThreshold);
            test_val = test_val + 1.0;
            test_val = floor(test_val);
            test_val = max(test_val, 0.0);
            
            int int_val = int(test_val);
            bool should_discard = int_val == 0;
            if (_EnableAlphaCutoff && should_discard)
            {
                discard;
            }
        }
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

    
    return final_color;
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
    float2 uv = i.uv.xy;
    
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

    #if defined(_FRAGMENT_CLIP)
        //sample the diffuse tex now to make sure it can be got
        float alpha = _MainTex.Sample(sampler_linear_repeat, uv.xy).w;
        if(_EnableAlphaCutoff)
        {
            float test_val = alpha + (-_AlphaTestThreshold);
            test_val = test_val + 1.0;
            test_val = floor(test_val);
            test_val = max(test_val, 0.0);
            
            int int_val = int(test_val);
            bool should_discard = int_val == 0;
            if (_EnableAlphaCutoff && should_discard)
            {
                discard;
            }
        }
    #endif

    float4 outline_color;
    float lightmap = _LightMap.Sample(sampler_linear_repeat, i.uv.xy).w;
    lightmap = floor(lightmap * 8.f);
    float id_check = lightmap * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    lightmap = frac(lightmap * id_transform.y) * id_transform.x;
    float id_rounded = round(lightmap);
    float outline_ID = uint(id_rounded);
    outline_color.w = 1.0f;
    switch (outline_ID)
    {
    case 1: 
        outline_color.xyz = _OutlineColor1.xyz;
        break;
    case 2:
        outline_color.xyz = _OutlineColor2.xyz;
        break;
    case 3:
        outline_color.xyz = _OutlineColor3.xyz;
        break;
    case 4:
        outline_color.xyz = _OutlineColor4.xyz;
        break;
    case 5:
        outline_color.xyz = _OutlineColor5.xyz;
        break;
    case 6:
        outline_color.xyz = _OutlineColor6.xyz;
        break;
    case 7:
        outline_color.xyz = _OutlineColor7.xyz;
        break;
    default:
        outline_color.xyz = _OutlineColor0.xyz;
        break;
    }

    if(_UseMaterialValuesLUT) outline_color.xyz = _MaterialValuesPackLUT.Load(float4(lightmap, 2, 0, 0));
    
    float3 light;
    get_light(light);
    
    float ndotl = dot(i.normal, light);
    ndotl = smoothstep(0, 0.15f, ndotl); // u_xlat16_22
    float dark_val = 1.0f - _ES_OutLineDarkenVal;
    dark_val = lerp(dark_val, 1.0f, ndotl);
    ndotl = ndotl * _ES_OutLineLightedVal;
    
    outline_color.xyz = outline_color.xyz * dark_val + ndotl;
    outline_color.xyz = (1.0f - _OutlineColorIntensity) * outline_color.xyz; 
    outline_color.xyz = (1.0f - (_GlobalOneMinusAvatarIntensityEnable * _GlobalOneMinusAvatarIntensity)) * outline_color.xyz;
    float3 view = i.ws_pos.xyz - _WorldSpaceCameraPos;
    view.x = length(view);
    
    o.forward.xyz = outline_color;
    return o;
}

vertex_out vert_shadow(vertex_in v)
{
    vertex_out o = (vertex_out)0.0;
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float4 position =  mul(unity_MatrixMVP, v.vertex);
    float4 custom_position = mul(unity_MatrixVP, ws_pos);
    position = (_EnableCustomCameraOverride) ? custom_position : position;
    position = showpart(v.color.y) ? position : float4(-99.0, -99.0, -99.0, 1.0);
    o.vertex = position;

   #if defined(_DIRECTIONALDISSOLVE)
        dissolve_vertex_out(float2x2(v.uv.xy, v.uv1.xy), float4(o.ws_pos.xyz, 1.0f), v.vertex, o.diss_uv, o.diss_pos);
    #endif

    return o;
}

half4 frag_shadow(vertex_out i, bool vface : SV_IsFrontFace) : SV_TARGET
{

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
    #endif
    #if defined(_IS_DUALFACE)
        float2 uv = vface ? i.uv.xy : i.uv.zw;
        float4 color = vface ? _Color: _BackColor;
        float4 diffuse = _MainTex.Sample(sampler_linear_repeat, uv) * color;
    #else
        float4 color = _Color;
        float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv) * color;
    #endif

    #if defined(_FRAGMENT_CLIP)
        if(_EnableAlphaCutoff)
        {
            float test_val = diffuse.w + (-_AlphaTestThreshold);
            test_val = test_val + 1.0;
            test_val = floor(test_val);
            test_val = max(test_val, 0.0);
            
            int int_val = int(test_val);
            bool should_discard = int_val == 0;
            if (_EnableAlphaCutoff && should_discard)
            {
                discard;
            }
        }
    #endif
    return 0;
}