vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    float hidepart =  (v.color.y * 256);
    hidepart = (int)hidepart; 
    hidepart = (int)hidepart & asint(_ShowPartID);
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    if((hidepart > 0) && _HideCharaParts) o.vertex = float4(-99.0, -99.0, -99.0, 1.0);
    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    float4 color_switch = (-(_VertexColorSwitch) + (float4)1) * float4(1.0, 1.0, 0.5, 0.5);
    o.color = v.color * _VertexColorSwitch + color_switch;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
    o.view = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);

    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);
    o.opos = o.vertex;

    UNITY_TRANSFER_FOG(o,o.vertex);
    return o;
}

float4 base_pixel (vertex_output i, bool vface : SV_IsFrontFace) : SV_Target
{
    float4 fragCoord  = float4(i.opos.xyz, 1.0f / i.opos.w);
    // intialize inputs and output
    float4 color = vface ? _Color : _BackColor; 
    float4 vcol = i.color;
    float3 normal = vface ? i.normal : -1 * i.normal;
    float3 view = i.view; 
    float2 uv = vface ? i.uv : i.uv2;
    float4 output = (float4)1.0f;

    // create dot products
    float ndotl = dot(normal, _WorldSpaceLightPos0.xyz);
    float ndotv = dot(normal, view);
    float ndoth = dot(normal, normalize(view +  _WorldSpaceLightPos0.xyz));
    
    // sample main texture
    float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv);
    output = main_tex * color;
    // sample the lightmap
    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);

    // extract material regions
    float material_ID = floor(8.0f * lightmap.w);
    float ramp_ID     = ((material_ID * 2.0f + 1.0f) * 0.0625f);
    float ID = material_region(material_ID); // in order to avoid repeating code, use the funtion instead
    // setup the material luts
    float4 lut_speccol = _MaterialValuesPackLUT.Load(float4(material_ID, 0, 0, 0)); // xyz : color, w : customparam a
    float4 lut_specval = _MaterialValuesPackLUT.Load(float4(material_ID, 1, 0, 0)); // x: shininess, y : roughness, z : intensity,  w : customparamb
    float4 lut_edgecol = _MaterialValuesPackLUT.Load(float4(material_ID, 2, 0, 0)); // xyz : color
    float4 lut_rimcol  = _MaterialValuesPackLUT.Load(float4(material_ID, 3, 0, 0)); // xyz : color
    float4 lut_rimval  = _MaterialValuesPackLUT.Load(float4(material_ID, 4, 0, 0)); // x : rim type, y : softness , z : dark
    float4 lut_rimscol = _MaterialValuesPackLUT.Load(float4(material_ID, 5, 0, 0)); // xyz : color
    float4 lut_rimsval = _MaterialValuesPackLUT.Load(float4(material_ID, 6, 0, 0)); // x: rim shadow width, y: rim shadow feather 
    float4 lut_bloomval = _MaterialValuesPackLUT.Load(float4(material_ID, 7, 0, 0)); // xyz: rim color  
    
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // indented to make collapsing in vscode easier
    // stockings 
        float2 tile_uv = uv.xy * _StockRangeTex_ST.xy + _StockRangeTex_ST.zw;

        float stock_tile = _StockRangeTex.Sample(sampler_linear_repeat, tile_uv).z; 
        // blue channel is a tiled texture that when used adds the rough mesh textured feel
        stock_tile = stock_tile * 0.5f - 0.5f;
        stock_tile = _StockRoughness * stock_tile + 1.0f;
        // extract and remap 

        // sample untiled texture 
        float4 stocking_tex = _StockRangeTex.Sample(sampler_linear_repeat, uv.xy);
        // determine which areas area affected by the stocking
        float stock_area = (stocking_tex.x > 0.001f) ? 1.0f : 0.0f;

        float offset_ndotv = dot(normal, normalize(view - _RimOffset));
        // i dont remember where i got this from but its in my mmd shader so it must be right... right? 
        float stock_rim = max(0.001f, ndotv);

        float stock_power = max(0.039f, _Stockpower);

        stock_rim = smoothstep(stock_power, _StockDarkWidth * stock_power, stock_rim) * _StockSP;

        stocking_tex.x = stocking_tex.x * stock_area * stock_rim;
        float3 stock_dark_area = (float3)-1.0f * _StockDarkcolor;
        stock_dark_area = stocking_tex.x * stock_dark_area + (float3)1.0f;
        stock_dark_area = output.xyz * stock_dark_area + (float3)-1.0f;
        stock_dark_area = stocking_tex.x * stock_dark_area + (float3)1.0f;
        float3 stock_darkened = stock_dark_area * output.xyz;

        float stock_spec = (1.0f - _StockSP) * (stocking_tex.y * stock_tile);

        stock_rim = saturate(max(0.004f, pow(ndotv, _Stockpower1)) * stock_spec);

        float3 stocking = -output.xyz * stock_dark_area + _Stockcolor;
        stocking = stock_rim * stocking + stock_darkened;
        if(_EnableStocking) output.xyz = stocking;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // shadow
        float3 shadow_color;
        float shadow_area;
        shadow_area = shadow_rate((ndotl), (lightmap.y), vcol.x, _ShadowRamp) ;
        // RAMP UVS 
        float2 ramp_uv = {shadow_area, ramp_ID};

        // SAMPLE RAMP TEXTURES
        float3 warm_ramp = _DiffuseRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz; 
        float3 cool_ramp = _DiffuseCoolRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz;

        shadow_color = lerp(warm_ramp, cool_ramp, _ES_CharacterToonRampMode);

        if (_ES_LEVEL_ADJUST_ON)
        {
            // Determine if the material is skin, face, or hair
            float isSkin = (material_ID < 1) ? 0.0 : 1.0;
            // Initialize color adjustment variables
            float3 skinLightColorAdjustment = (float3)0.0;
            float3 highlightColorAdjustment = (float3)0.0;
            float3 skinShadowColorAdjustment = (float3)0.0;
            float3 shadowColorAdjustment = (float3)0.0;
            float3 isSkinVector = (float3)isSkin;
            float3 tempAdjustment = (float3)0.0;

            // Calculate skin light color adjustment
            skinLightColorAdjustment = _ES_LevelSkinLightColor.www * _ES_LevelSkinLightColor.xyz;
            skinLightColorAdjustment *= 2.0;

            // Calculate highlight color adjustment
            highlightColorAdjustment = _ES_LevelHighLightColor.www * _ES_LevelHighLightColor.xyz;
            highlightColorAdjustment = (highlightColorAdjustment * 2.0) - skinLightColorAdjustment;
            skinLightColorAdjustment = (isSkinVector * highlightColorAdjustment) + skinLightColorAdjustment;
            skinLightColorAdjustment = max(skinLightColorAdjustment, 0.01f);

            // Calculate skin shadow color adjustment
            skinShadowColorAdjustment = _ES_LevelSkinShadowColor.www * _ES_LevelSkinShadowColor.xyz;
            skinShadowColorAdjustment *= 2.0;

            // Calculate shadow color adjustment
            shadowColorAdjustment = _ES_LevelShadowColor.www * _ES_LevelShadowColor.xyz;
            shadowColorAdjustment = (shadowColorAdjustment * 2.0) - skinShadowColorAdjustment;
            skinShadowColorAdjustment = (isSkinVector * shadowColorAdjustment) + skinShadowColorAdjustment;
            skinShadowColorAdjustment = max(skinShadowColorAdjustment, 0.01f);

            // Adjust shadow color based on mid-level
            shadowColorAdjustment = shadow_color.xyz - (float3(_ES_LevelMid, _ES_LevelMid, _ES_LevelMid));
            tempAdjustment.xz = float2(_ES_LevelHighLight, _ES_LevelMid) - float2(_ES_LevelMid, _ES_LevelShadow);
            shadowColorAdjustment /= tempAdjustment.xxx;
            shadowColorAdjustment = (shadowColorAdjustment * 0.5) + 0.5;
            shadowColorAdjustment = clamp(shadowColorAdjustment, 0.0, 1.0);
            skinLightColorAdjustment *= shadowColorAdjustment;

            // Further adjust shadow color
            shadowColorAdjustment = -shadow_color.xyz + float3(_ES_LevelMid, _ES_LevelMid, _ES_LevelMid);
            shadowColorAdjustment /= tempAdjustment.zzz;
            shadowColorAdjustment = (-shadowColorAdjustment * 0.5) + 0.5;
            shadowColorAdjustment = clamp(shadowColorAdjustment, 0.0, 1.0);
            skinShadowColorAdjustment *= shadowColorAdjustment;

            // Apply final shadow color based on shadow area
            shadow_color.xyz = (shadow_area < 0.9f) ? skinShadowColorAdjustment : skinLightColorAdjustment;
        }
        if(_ShadowBoost)
        {
            float boost_range = smoothstep(0.8, 0.81, shadow_area);
            float boost = lerp(_ShadowBoostVal, 1.0f, boost_range);
            shadow_color.xyz = shadow_color * boost.xxx;
        }
    output.xyz = output.xyz * shadow_color.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // moon halo
        float check = _UseMoonHalo;
        float moon_area = 0.95f < vcol.y;

        // get both moon halo smoothsteps
        float range = frac(-_MoonHaloRange);
        float moon_x = smoothstep(_MoonAnim.x, 1.0f, range);
        float moon_y = smoothstep(_MoonAnim.y, 1.0f, range);

        float moon = moon_x * moon_y;
        moon =  moon * 0.5 - 0.5;
        float2 moon_dir = moon * _MoonDir.xy;
        float2 moon_uv =  -moon_dir + uv;

        float mlength = length(moon_uv);
        float uv_x = uv.x + (-_MoonDir.x);
        uv_x = uv_x * 2.0f + _MoonDir.x;
        float uv_y =  uv.y;
        moon_uv = -moon_uv + float2(uv_x, uv_y);
        moon_uv.x = length(moon_uv.xy);

        moon_uv.x = lerp(mlength, moon_uv.x, _MoonUVType);
        float real_moon = smoothstep(_MoonDir.w, _MoonDir.z, moon_uv.x);
        float3 moon_color =  real_moon.xxx * output.xyz - output.xyz;
        moon_color = moon_area * moon_color + output.xyz;
    output.xyz =  _UseMoonHalo ? moon_color : output.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // specular
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

        float3 specular_values[8] =
        {
            float3(_SpecularShininess0, _SpecularRoughness0, _SpecularIntensity0),
            float3(_SpecularShininess1, _SpecularRoughness1, _SpecularIntensity1),
            float3(_SpecularShininess2, _SpecularRoughness2, _SpecularIntensity2),
            float3(_SpecularShininess3, _SpecularRoughness3, _SpecularIntensity3),
            float3(_SpecularShininess4, _SpecularRoughness4, _SpecularIntensity4),
            float3(_SpecularShininess5, _SpecularRoughness5, _SpecularIntensity5),
            float3(_SpecularShininess6, _SpecularRoughness6, _SpecularIntensity6),
            float3(_SpecularShininess7, _SpecularRoughness7, _SpecularIntensity7),
        };
        
        if(_UseMaterialValuesLUT)
        {
            specular_color[ID] = lut_speccol;
            specular_values[ID] = lut_specval.xyz; // weird fix, not accurate to ingame code but whatever if it works it works
        }

        specular_values[ID].z = max(0.0f, specular_values[ID].z); // why would there ever be a reason for a negative specular intensity


        float3 specular = specular_base(shadow_area, ndoth, lightmap.z, specular_color[ID], specular_values[ID], _ES_SPColor, _ES_SPIntensity);
    output.xyz = output.xyz + specular;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // matcap
        float2 sphere_uv = mul(normal, (float3x3)UNITY_MATRIX_I_V ).xy;
        sphere_uv = sphere_uv * 0.5f + 0.5f;  


        float4 matcap = _MatCapTex.Sample(sampler_linear_repeat, sphere_uv);
        float matcap_mask = _MatCapMaskTex.Sample(sampler_linear_repeat, uv).x;

        float matcap_area = lightmap.z * matcap_mask;

        float matcap_strength = ((saturate(((shadow_area * 5.0) + -4.0)) * (((-_MatCapStrengthInShadow) * _MatCapStrength) + _MatCapStrength)) + (_MatCapStrength * _MatCapStrengthInShadow));
        float3 matcap_color = (matcap_strength * matcap.xyz); // mask * matcap * matcap_color
        matcap_color.xyz = (matcap_color.xyz * _MatCapColor.xyz);
        matcap_color.xyz = (matcap_area * matcap_color.xyz); // * spec color
        matcap_color.xyz = (specular_color[ID].xyz * matcap_color.xyz); // * spec intensity
        matcap_color.xyz = (specular_values[ID].z * matcap_color.xyz);

        float matcap_ceil = ((matcap_mask * lightmap.z) + -0.0099999998);
        matcap_ceil = clamp(matcap_ceil, 0.0, 1.0);
        matcap_ceil = ceil(matcap_ceil);

        matcap.xyz = (matcap_color.xyz * matcap_ceil);
    if(_UseMatcap)output.xyz = output * 1 /*this would be the rim shadow*/ + matcap;
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
        float3 fresnel = -abs(ndotv) + 1.0f;
        fresnel.x = fresnel.x + (-_FresnelBSI.x);
        fresnel.x = fresnel.x * (float(1.0) / _FresnelBSI.y);
        fresnel.x = saturate(fresnel.x);
        fresnel.xyz = (fresnel.xxx * _FresnelColor.xyz) * _FresnelColorStrength;
        fresnel = max(fresnel, 0.0f);
    output.xyz = output.xyz + fresnel;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // bloom
        float bloom_array[8] = 
        {
            _mBloomIntensity0,
            _mBloomIntensity1,
            _mBloomIntensity2,
            _mBloomIntensity3,
            _mBloomIntensity4,
            _mBloomIntensity5,
            _mBloomIntensity6,
            _mBloomIntensity7
        };

        float4 boom_color_array[8] =
        {
            _mBloomColor0,
            _mBloomColor1,
            _mBloomColor2,
            _mBloomColor3,
            _mBloomColor4,
            _mBloomColor5,
            _mBloomColor6,
            _mBloomColor7,
        };

        float4 bloom_color = boom_color_array[ID];
        float bloom_intensity = _mBloomIntensity0;
        if(_UseMaterialValuesLUT)
        {
            bloom_color = lut_bloomval;
            bloom_intensity = lut_rimsval.z;
        }
        else
        {
            bloom_intensity = bloom_array[ID];
        }  

    increase_bloom(bloom_color, bloom_intensity, output.xyz);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // rimlight 

        float4 rim_color[8] =
        {
            _RimColor0,
            _RimColor1,
            _RimColor2,
            _RimColor3, 
            _RimColor4,
            _RimColor5,
            _RimColor6,
            _RimColor7,   
        };

        float3 rim_values[8] = // x = width, y = softness, z = type, w = dark
        {
            float3(_RimEdgeSoftness0, _RimType0, saturate(_RimDark0)),
            float3(_RimEdgeSoftness1, _RimType0, saturate(_RimDark1)),
            float3(_RimEdgeSoftness2, _RimType0, saturate(_RimDark2)),
            float3(_RimEdgeSoftness3, _RimType0, saturate(_RimDark3)),
            float3(_RimEdgeSoftness4, _RimType0, saturate(_RimDark4)),
            float3(_RimEdgeSoftness5, _RimType0, saturate(_RimDark5)),
            float3(_RimEdgeSoftness6, _RimType0, saturate(_RimDark6)),
            float3(_RimEdgeSoftness7, _RimType0, saturate(_RimDark7)),
        }; // they have unused id specific rim widths but just in case they do end up using them in the future ill leave them be here

        if(_UseMaterialValuesLUT) 
        {    
            rim_values[ID].xyz = lut_rimval.yxz; 
        }

        float feather = rim_values[ID].x;
        float type = rim_values[ID].y;
        float dark = rim_values[ID].z;

        float darkening = (shadow_area * dark + (-dark)) + 1.0f;

        float mode =  lerp(1.0f, lightmap.x, _RimLightMode) * _RimWidth;
        float normal_offset = view.z * normal.x - (view.x * normal.z);
        normal_offset = 0.0f < normal_offset ? -1.0f : 1.0f;

        float rim_width = mode * _ES_RimLightWidth;
        rim_width = rim_width * normal_offset;
        rim_width = rim_width * 0.0055f;

        float2 screen_pos = i.screenpos.xy / i.screenpos.w;        

        float org_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen_pos.xy), screen_pos);
        rim_width = rim_width / org_depth;

        float2 screen;
        screen.x = (_ES_RimLightOffset.x * 0.01 + rim_width) + screen_pos.x;
        screen.y = (_ES_RimLightOffset.y * 0.01 + screen_pos.y);

        float norm_depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen.xy)); 

        float diff_depth = -org_depth + norm_depth; 
        diff_depth = max(diff_depth, 0.01);
        diff_depth = pow(diff_depth, _RimEdge * 10.f);

        diff_depth = smoothstep(0.82, 0.9, diff_depth);
        diff_depth = (feather < diff_depth) ? diff_depth : 0.0f;
        float3 rColor = (rim_color[ID].xyz * diff_depth) * _Rimintensity;
        rColor = (1.f - ndotv) * rColor;
        float shadow_dark = dot(rColor.xyz, float3(0.212670997, 0.715160012, 0.0721689984));

        shadow_dark =  saturate(darkening * shadow_dark);

        float rim_ndotv = pow(max(1.f - ndotv, 0.001f), dark) + 1.0f;

        float3 rim_add = (rim_color[ID].xyz * diff_depth) * _Rimintensity - output.xyz;
        rim_add = shadow_dark * rim_add + output;
        
        rColor = rColor * _ES_RimLightAddMode + rim_add;

        float3 diff_intensity = max(output.xyz, 0.001f);
        diff_intensity = pow(diff_intensity, rim_ndotv);

        float3 rimSomething = (rim_color[ID].xyz * diff_depth) * _Rimintensity + -diff_intensity;
        rimSomething = darkening * rimSomething + diff_intensity;
        
        float3 rim_light = (lerp(rimSomething, diff_intensity, type)) * diff_depth;

    output.xyz += rim_light;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    
    if(_UseHeightLerp) heightlightlerp(i.ws_pos, output);
    return output;
}