vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    int2 hidepart = int2(v.color.yx * 256);
    hidepart = int2(uint(hidepart.x) & uint(_ShowPartID), uint(hidepart.y) & uint(_ShowPartID));
    int tmp = _HideCharaParts ? hidepart.x : 1;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    o.vertex = (0 < tmp) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);
    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    float4 color_switch = (-(_VertexColorSwitch) + (float4)1) * float4(1.0, 1.0, 0.5, 0.5);
    o.color = v.color * _VertexColorSwitch + color_switch;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
    o.view = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
    o.tangent.xyz = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
    o.tangent.w = v.tangent.w;
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


    o.opos = o.vertex;

    UNITY_TRANSFER_FOG(o,o.vertex);
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


    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // real start
    float4 fragCoord  = float4(i.opos.xyz, 1.0f / i.opos.w);
    // intialize inputs and output
    float4 color = vface ? _Color : _BackColor; 
    float4 vcol = i.color;
    float3 normal = vface ? i.normal : -1 * i.normal;
    float3 tangents = vface ? i.tangent.xyz : -1 * i.tangent.xyz;
    float3 view = i.view; 
    float2 uv = vface ? i.uv : i.uv2;
    float4 output = (float4)1.0f;

    // create dot products
    float ndotl = dot(normalize(normal), _WorldSpaceLightPos0.xyz);
    float ndotv = dot(normalize(normal), view);
    float ndoth = dot(normalize(normal), normalize(view +  _WorldSpaceLightPos0.xyz));
    
    // sample main texture
    float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv);
    output = main_tex * color;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Starry Sky Tex
        if(_StarrySky)
        {
            float2 sky_uv = uv * _SkyTex_ST.xy + _SkyTex_ST.zw;
            float2 mask_uv = uv * _SkyMask_ST.xy + _SkyMask_ST.zw;
            float3 sky_tex = _SkyTex.Sample(sampler_linear_repeat, sky_uv);
            float sky_mask = _SkyMask.Sample(sampler_linear_repeat, mask_uv).x;

            float3 colored = main_tex * color;
            output.xyz = -main_tex * color + sky_tex;
            output.xyz = (sky_mask + _SkyRange) * output + colored;
        }
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
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

    // alternative material value getting, things like the flame crystal used this method
    float id_check = material_ID * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    material_ID = frac(material_ID * id_transform.y) * id_transform.x;
    float id_rounded = round(material_ID);
    
        
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
    // rim shadow
        float4 rim_shadow_color[8] = 
        {
            _RimShadowColor0,
            _RimShadowColor1,
            _RimShadowColor2,
            _RimShadowColor3,
            _RimShadowColor4,
            _RimShadowColor5,
            _RimShadowColor6,
            _RimShadowColor7
        };

        float2 rim_shadow_values[8] = 
        {
            float2(_RimShadowWidth0, _RimShadowFeather0),
            float2(_RimShadowWidth1, _RimShadowFeather1),
            float2(_RimShadowWidth2, _RimShadowFeather2),
            float2(_RimShadowWidth3, _RimShadowFeather3),
            float2(_RimShadowWidth4, _RimShadowFeather4),
            float2(_RimShadowWidth5, _RimShadowFeather5),
            float2(_RimShadowWidth6, _RimShadowFeather6),
            float2(_RimShadowWidth7, _RimShadowFeather7)
        };

        float4 rimsdw_color = (_UseMaterialValuesLUT) ? lut_rimscol : rim_shadow_color[ID];
        rimsdw_color.xyz = rimsdw_color * (_ES_RimShadowColor.www  * _ES_RimShadowColor.xyz);

        float rim_shadow = ndotv;
        rim_shadow = 1.0f - rim_shadow;
        rim_shadow =  max(rim_shadow, 0.001);
        rim_shadow = pow(rim_shadow, _RimShadowCt);
        rim_shadow = smoothstep(rim_shadow_values[ID].x, rim_shadow_values[ID].y, rim_shadow);
        rim_shadow = rim_shadow * _RimShadowIntensity;
        rim_shadow = rim_shadow * _ES_RimShadowIntensity;
        rim_shadow = rim_shadow * 0.25f;
        rimsdw_color.xyz = rimsdw_color.xyz * 2.0f - 1.0f;
        rimsdw_color.xyz = rim_shadow * rimsdw_color.xyz + 1.0f;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // glint 
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        float customparam_a = lut_speccol;
        float customparam_b = lut_specval;
        if(!_UseMaterialValuesLUT)
        {
            switch (uint(id_rounded))
            {
                case 1: 
                    customparam_a = _CustomParamA1;
                    customparam_b = _CustomParamB1;
                    break;
                case 2:
                    customparam_a = _CustomParamA2;
                    customparam_b = _CustomParamB2;
                    break;
                case 3:
                    customparam_a = _CustomParamA3;
                    customparam_b = _CustomParamB3;
                    break;
                case 4:
                    customparam_a = _CustomParamA4;
                    customparam_b = _CustomParamB4;
                    break;
                case 5:
                    customparam_a = _CustomParamA5;
                    customparam_b = _CustomParamB5;
                    break;
                case 6:
                    customparam_a = _CustomParamA6;
                    customparam_b = _CustomParamB6;
                    break;
                case 7:
                    customparam_a = _CustomParamA7;
                    customparam_b = _CustomParamB7;
                    break;
                default:
                    customparam_a = _CustomParamA0;
                    customparam_b = _CustomParamB0;
                    break;
            }
        }

        if(_UseGlint)
        {
            // something with the normals
            float2 glint_uv = uv.xy * float2(1.0f, _GlintUVTillingY);
            float2 glint_check = ceil(saturate(abs(normal.zx) + float2(-0.5, -0.5)));
            float4 glint_pos =  lerp(i.ws_pos.xz, i.ws_pos.xy, glint_check.x).xyxy;
            glint_pos = lerp(glint_pos.zw, i.ws_pos.yz, glint_check.y).xyxy;

            float4 glint_coord =  0.899999976<_GlintWorldPosUV ? glint_pos : glint_uv.xyxy;
            glint_coord = vface ? glint_coord : glint_coord * _GlintScaleBackface;


            
            // Glint mask sampling
            float4 glint_mask = _GlintMask.Sample(sampler_linear_repeat, uv);

            // Scale glint coordinates
            float4 scaled_glint_coord = glint_coord.zwzw * float4(_GlintScale, _GlintScale, _GlintScale, _GlintScale);
            scaled_glint_coord = customparam_a * scaled_glint_coord;
            float2 glint_fract = frac(scaled_glint_coord.zw);
            float4 glint_floor = floor(scaled_glint_coord);

            // Glint concentration and intensity calculations
            float glint_concentration_clamped = clamp(_GlintConcentration * 10.0, 0.0, 1.0);
            float glint_specular = pow((pow(max(ndoth, 0.01f), specular_values[ID].x) * _GlintIntensity), _GlintConcentration);
            glint_specular = max(glint_specular, 0.009f);
            glint_specular = lerp(_GlintPointScale, glint_specular, saturate(_GlintConcentration * 10.0f)) * glint_mask.w;

            float base_glint_threshold = glint_mask.w * glint_specular;

            // Time-based animation
            float4 time_scaled = _Time.xxxx * float4(50.0, 10.0, 0.0, 0.0);
            float time_anim = time_scaled.x * _GlintSparkFreq;
            float sparkle_half = _GlintSparkle * 0.5;

            float total_sparkle = 0.0;
            float4 accumulated_direction = float4(0.0, 0.0, 0.0, 0.0);

            // Loop through 6 glint cells (unrolled for clarity)
            // Cell 1: offset (0, 0)
            float2 cell_coord_1 = glint_floor.zw;
            float rand_1a = frac(sin(dot(cell_coord_1, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_1b = frac(sin(dot(cell_coord_1 + rand_1a, float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 cell_coord_1b = glint_floor.zw + float2(0.454869986, 5.415452);
            float rand_1c = frac(sin(dot(cell_coord_1b, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_1d = frac(sin(dot(cell_coord_1b + rand_1c, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 random_offset_1 = float2(rand_1c, rand_1d) * 2.0 - 1.0;

            float rand_density_1 = frac(sin(dot(glint_floor.zw + float2(6.415452, 5.415452), float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 offset_pos_1 = glint_fract.xy + float2(-0.5, -0.5);
            float2 randomized_pos_1 = (-random_offset_1) * float2(_GlintRandom, _GlintRandom) * 0.400000006 + offset_pos_1;
            float distance_1 = sqrt(dot(randomized_pos_1, randomized_pos_1));

            float threshold_1 = rand_1a * 2.0 - 1.0;
            threshold_1 = threshold_1 * 0.150000006 + base_glint_threshold;
            float is_glint_1 = (distance_1 < threshold_1) ? 1.0 : 0.0;

            float density_factor_1 = (-_GlintDensity) * customparam_b.x + rand_density_1 - 1.0;
            density_factor_1 = ceil(clamp(density_factor_1, 0.0, 1.0));
            float glint_presence_1 = density_factor_1 * is_glint_1;

            float sparkle_1 = sin(time_anim * rand_1a + rand_1b * 3.1400001);
            sparkle_1 = sparkle_half * sparkle_1 + 0.5;

            // Convert random values to spherical coordinates for direction
            float phi_1 = rand_1a * 6.28318024;
            float theta_input_1 = (-rand_1b) * 2.0 + 1.0;
            float theta_sqrt_1 = sqrt(-abs(theta_input_1) + 1.0);
            float theta_approx_1 = abs(theta_input_1) * (abs(theta_input_1) * (abs(theta_input_1) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float theta_1 = theta_sqrt_1 * theta_approx_1;
            theta_1 = theta_1 + ((theta_input_1 < -theta_input_1) ? (theta_1 * -2.0 + 3.14159274) : 0.0);

            float4 direction_1 = float4(
                sin(theta_1) * cos(phi_1),
                sin(theta_1) * sin(phi_1),
                cos(theta_1),
                1.0
            );

            // Cell 2: offset (1, 0)
            float2 cell_coord_2 = glint_floor.zw + float2(1.0, 0.0);
            float rand_2a = frac(sin(dot(cell_coord_2, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_2b = frac(sin(dot(cell_coord_2 + rand_2a, float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 cell_coord_2b = glint_floor.zw + float2(1.45486999, 0.454869986);
            float rand_2c = frac(sin(dot(cell_coord_2b, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_2d = frac(sin(dot(cell_coord_2b + rand_2c, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 random_offset_2 = float2(rand_2c, rand_2d) * 2.0 - 1.0;

            float rand_density_2 = frac(sin(dot(glint_floor.zw + float2(6.415452, 5.415452), float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 offset_pos_2 = glint_fract.xy + float2(-1.5, -0.5);
            float2 randomized_pos_2 = (-random_offset_2) * float2(_GlintRandom, _GlintRandom) * 0.400000006 + offset_pos_2;
            float distance_2 = sqrt(dot(randomized_pos_2, randomized_pos_2));

            float threshold_2 = rand_2a * 2.0 - 1.0;
            threshold_2 = threshold_2 * 0.150000006 + base_glint_threshold;
            float is_glint_2 = (distance_2 < threshold_2) ? 1.0 : 0.0;

            float density_factor_2 = (-_GlintDensity) * customparam_b.x + rand_density_2 - 1.0;
            density_factor_2 = ceil(clamp(density_factor_2, 0.0, 1.0));
            float glint_presence_2 = density_factor_2 * is_glint_2;

            float sparkle_2 = sin(time_anim * rand_2a + rand_2b * 3.1400001);
            sparkle_2 = sparkle_half * sparkle_2 + 0.5;
            total_sparkle = sparkle_1 * glint_presence_1 + sparkle_2 * glint_presence_2;

            float phi_2 = rand_2a * 6.28318024;
            float theta_input_2 = (-rand_2b) * 2.0 + 1.0;
            float theta_sqrt_2 = sqrt(-abs(theta_input_2) + 1.0);
            float theta_approx_2 = abs(theta_input_2) * (abs(theta_input_2) * (abs(theta_input_2) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float theta_2 = theta_sqrt_2 * theta_approx_2;
            theta_2 = theta_2 + ((theta_input_2 < -theta_input_2) ? (theta_2 * -2.0 + 3.14159274) : 0.0);

            float4 direction_2 = float4(
                sin(theta_2) * cos(phi_2),
                sin(theta_2) * sin(phi_2),
                cos(theta_2),
                1.0
            );
            accumulated_direction = direction_1 * float4(glint_presence_1, glint_presence_1, glint_presence_1, glint_presence_1) + direction_2 * float4(glint_presence_2, glint_presence_2, glint_presence_2, glint_presence_2);

            // Cell 3: offset (-1, 0)
            float2 cell_coord_3 = glint_floor.zw + float2(-1.0, 0.0);
            float rand_3a = frac(sin(dot(cell_coord_3, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_3b = frac(sin(dot(cell_coord_3 + rand_3a, float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 cell_coord_3b = glint_floor.zw + float2(-0.545130014, 0.454869986);
            float rand_3c = frac(sin(dot(cell_coord_3b, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_3d = frac(sin(dot(cell_coord_3b + rand_3c, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 random_offset_3 = float2(rand_3c, rand_3d) * 2.0 - 1.0;

            float rand_density_3 = frac(sin(dot(glint_floor.zw + float2(4.415452, 5.415452), float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 offset_pos_3 = glint_fract.xy + float2(0.5, -0.5);
            float2 randomized_pos_3 = (-random_offset_3) * float2(_GlintRandom, _GlintRandom) * 0.400000006 + offset_pos_3;
            float distance_3 = sqrt(dot(randomized_pos_3, randomized_pos_3));

            float threshold_3 = rand_3a * 2.0 - 1.0;
            threshold_3 = threshold_3 * 0.150000006 + base_glint_threshold;
            float is_glint_3 = (distance_3 < threshold_3) ? 1.0 : 0.0;

            float density_factor_3 = (-_GlintDensity) * customparam_b.x + rand_density_3 - 1.0;
            density_factor_3 = ceil(clamp(density_factor_3, 0.0, 1.0));
            float glint_presence_3 = density_factor_3 * is_glint_3;

            float sparkle_3 = sin(time_anim * rand_3a + rand_3b * 3.1400001);
            sparkle_3 = sparkle_half * sparkle_3 + 0.5;
            total_sparkle = sparkle_3 * glint_presence_3 + total_sparkle;

            float phi_3 = rand_3a * 6.28318024;
            float theta_input_3 = (-rand_3b) * 2.0 + 1.0;
            float theta_sqrt_3 = sqrt(-abs(theta_input_3) + 1.0);
            float theta_approx_3 = abs(theta_input_3) * (abs(theta_input_3) * (abs(theta_input_3) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float theta_3 = theta_sqrt_3 * theta_approx_3;
            theta_3 = theta_3 + ((theta_input_3 < -theta_input_3) ? (theta_3 * -2.0 + 3.14159274) : 0.0);

            float4 direction_3 = float4(
                sin(theta_3) * cos(phi_3),
                sin(theta_3) * sin(phi_3),
                cos(theta_3),
                1.0
            );
            accumulated_direction = direction_3 * float4(glint_presence_3, glint_presence_3, glint_presence_3, glint_presence_3) + accumulated_direction;

            // Cell 4: offset (0, 1)
            float2 cell_coord_4 = glint_floor.zw + float2(0.0, 1.0);
            float rand_4a = frac(sin(dot(cell_coord_4, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_4b = frac(sin(dot(cell_coord_4 + rand_4a, float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 cell_coord_4b = glint_floor.zw + float2(0.454869986, 1.45486999);
            float rand_4c = frac(sin(dot(cell_coord_4b, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_4d = frac(sin(dot(cell_coord_4b + rand_4c, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 random_offset_4 = float2(rand_4c, rand_4d) * 2.0 - 1.0;

            float rand_density_4 = frac(sin(dot(glint_floor.zw + float2(5.415452, 6.415452), float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 offset_pos_4 = glint_fract.xy + float2(-0.5, -1.5);
            float2 randomized_pos_4 = (-random_offset_4) * float2(_GlintRandom, _GlintRandom) * 0.400000006 + offset_pos_4;
            float distance_4 = sqrt(dot(randomized_pos_4, randomized_pos_4));

            float threshold_4 = rand_4a * 2.0 - 1.0;
            threshold_4 = threshold_4 * 0.150000006 + base_glint_threshold;
            float is_glint_4 = (distance_4 < threshold_4) ? 1.0 : 0.0;

            float density_factor_4 = (-_GlintDensity) * customparam_b.x + rand_density_4 - 1.0;
            density_factor_4 = ceil(clamp(density_factor_4, 0.0, 1.0));
            float glint_presence_4 = density_factor_4 * is_glint_4;

            float sparkle_4 = sin(time_anim * rand_4a + rand_4b * 3.1400001);
            sparkle_4 = sparkle_half * sparkle_4 + 0.5;
            total_sparkle = sparkle_4 * glint_presence_4 + total_sparkle;

            float phi_4 = rand_4a * 6.28318024;
            float theta_input_4 = (-rand_4b) * 2.0 + 1.0;
            float theta_sqrt_4 = sqrt(-abs(theta_input_4) + 1.0);
            float theta_approx_4 = abs(theta_input_4) * (abs(theta_input_4) * (abs(theta_input_4) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float theta_4 = theta_sqrt_4 * theta_approx_4;
            theta_4 = theta_4 + ((theta_input_4 < -theta_input_4) ? (theta_4 * -2.0 + 3.14159274) : 0.0);

            float4 direction_4 = float4(
                sin(theta_4) * cos(phi_4),
                sin(theta_4) * sin(phi_4),
                cos(theta_4),
                1.0
            );
            accumulated_direction = direction_4 * float4(glint_presence_4, glint_presence_4, glint_presence_4, glint_presence_4) + accumulated_direction;

            // Cell 5: offset (0, -1)
            float2 cell_coord_5 = glint_floor.zw + float2(0.0, -1.0);
            float rand_5a = frac(sin(dot(cell_coord_5, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_5b = frac(sin(dot(cell_coord_5 + rand_5a, float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 cell_coord_5b = glint_floor.zw + float2(0.454869986, -0.545130014);
            float rand_5c = frac(sin(dot(cell_coord_5b, float2(12.9898005, 78.2330017))) * 43758.5469);
            float rand_5d = frac(sin(dot(cell_coord_5b + rand_5c, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 random_offset_5 = float2(rand_5c, rand_5d) * 2.0 - 1.0;

            float rand_density_5 = frac(sin(dot(glint_floor.zw + float2(5.415452, 4.415452), float2(12.9898005, 78.2330017))) * 43758.5469);

            float2 offset_pos_5 = glint_fract.xy + float2(-0.5, 0.5);
            float2 randomized_pos_5 = (-random_offset_5) * float2(_GlintRandom, _GlintRandom) * 0.400000006 + offset_pos_5;
            float distance_5 = sqrt(dot(randomized_pos_5, randomized_pos_5));

            float threshold_5 = rand_5a * 2.0 - 1.0;
            threshold_5 = threshold_5 * 0.150000006 + base_glint_threshold;
            float is_glint_5 = (distance_5 < threshold_5) ? 1.0 : 0.0;

            float density_factor_5 = (-_GlintDensity) * customparam_b.x + rand_density_5 - 1.0;
            density_factor_5 = ceil(clamp(density_factor_5, 0.0, 1.0));
            float glint_presence_5 = density_factor_5 * is_glint_5;

            float sparkle_5 = sin(time_anim * rand_5a + rand_5b * 3.1400001);
            sparkle_5 = sparkle_half * sparkle_5 + 0.5;
            total_sparkle = sparkle_5 * glint_presence_5 + total_sparkle;

            float phi_5 = rand_5a * 6.28318024;
            float theta_input_5 = (-rand_5b) * 2.0 + 1.0;
            float theta_sqrt_5 = sqrt(-abs(theta_input_5) + 1.0);
            float theta_approx_5 = abs(theta_input_5) * (abs(theta_input_5) * (abs(theta_input_5) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float theta_5 = theta_sqrt_5 * theta_approx_5;
            theta_5 = theta_5 + ((theta_input_5 < -theta_input_5) ? (theta_5 * -2.0 + 3.14159274) : 0.0);

            float4 direction_5 = float4(
                sin(theta_5) * cos(phi_5),
                sin(theta_5) * sin(phi_5),
                cos(theta_5),
                1.0
            );

            
            accumulated_direction = direction_5 * float4(glint_presence_5, glint_presence_5, glint_presence_5, glint_presence_5) + accumulated_direction;
            // Normalize accumulated direction
            if (accumulated_direction.w > 0.00999999978)
                accumulated_direction.xyz = accumulated_direction.xyz / accumulated_direction.w;
            
            
            // Calculate final glint parameters
            float glint_intensity_adjusted = glint_specular * glint_mask.w - 1.0;
            float glint_intensity_final = glint_concentration_clamped * glint_intensity_adjusted + 1.0;

            float view_dot = dot(accumulated_direction.xyz, view.xyz);
            float view_frequency_factor = frac(view_dot * _GlintViewFreq);
            view_frequency_factor = glint_intensity_final * view_frequency_factor;

            float sparkle_clamped = clamp(max(total_sparkle + 0.800000012, 0.0), 0.0, 3.0);
            float local_glint = view_frequency_factor * sparkle_clamped;

            // Global glint calculation
            float4 global_glint_coord = glint_coord * float4(_GlobalGlintScale, _GlobalGlintScale, _GlobalGlintScale, _GlobalGlintScale);
            global_glint_coord = customparam_b * global_glint_coord;
            float2 global_glint_fract = frac(global_glint_coord.zw);
            float4 global_glint_floor = round(global_glint_coord);

            // Global glint random offset
            float global_rand_a = frac(sin(dot(global_glint_floor.zw, float2(12.9898005, 78.2330017))) * 43758.5469);
            float global_rand_b = frac(sin(dot(global_glint_floor.zw + global_rand_a, float2(12.9898005, 78.2330017))) * 43758.5469);
            float2 global_random_offset = float2(global_rand_a, global_rand_b) * 2.0 - 1.0;
            float2 global_glint_pos = global_random_offset * 0.5 + global_glint_fract;

            // Global glint direction calculation
            float4 global_scaled_floor = global_glint_floor * float4(0.0386548117, 0.0386548117, 58.3610001, 58.3610001);
            float global_rand_c = frac(sin(dot(global_scaled_floor.xy, float2(12.9898005, 78.2330017))) * 43758.5469);
            float global_rand_d = frac(sin(dot(global_glint_floor.xy * 0.0386548117 + global_rand_c, float2(12.9898005, 78.2330017))) * 43758.5469);

            float global_phi = global_rand_c * 6.28318024;
            float global_theta_input = (-global_rand_d) * 2.0 + 1.0;
            float global_theta_sqrt = sqrt(-abs(global_theta_input) + 1.0);
            float global_theta_approx = abs(global_theta_input) * (abs(global_theta_input) * (abs(global_theta_input) * -0.0187292993 + 0.0742610022) - 0.212114394) + 1.57072878;
            float global_theta = global_theta_sqrt * global_theta_approx;
            global_theta = global_theta + ((global_theta_input < -global_theta_input) ? (global_theta * -2.0 + 3.14159274) : 0.0);

            float3 global_direction = float3(
                sin(global_theta) * cos(global_phi),
                sin(global_theta) * sin(global_phi),
                cos(global_theta)
            );

            // Global glint sparkle
            float global_rand_e = frac(sin(dot(global_scaled_floor.zw, float2(12.9898005, 78.2330017))) * 43758.5469);
            float global_rand_f = frac(sin(dot(global_glint_floor.zw * 58.3610001 + global_rand_e, float2(12.9898005, 78.2330017))) * 43758.5469);

            float global_time_factor = frac(time_scaled.y * _GlobalGlintSparkFreq + global_rand_e);
            float global_sparkle = abs(global_time_factor - 0.5) * _GlobalGlintSparkle + 0.300000012;

            // Global glint visibility test
            float2 global_glint_center = global_glint_pos - float2(0.5, 0.5);
            float global_distance = sqrt(dot(global_glint_center, global_glint_center));
            float global_point_threshold = _GlobalGlintPointScale * 0.399999976 * frac(global_rand_f) + 0.0199999996;
            bool is_global_point = global_distance < global_point_threshold;

            float global_view_dot = dot(global_direction, view.xyz);
            float global_view_freq = frac(global_view_dot * _GlobalGlintViewFreq);
            float global_density_threshold = clamp((-_GlobalGlintDensity) * customparam_b.x + 1.0, 0.0, 1.0);
            bool is_global_visible = global_density_threshold < global_view_freq;

            float global_glint_intensity = (is_global_point && is_global_visible) ? global_sparkle : 0.0;
            float global_point_scale_adjusted = _GlobalGlintPointScale * 5.0 + 0.5;
            float global_glint_normalized = global_glint_intensity / global_point_scale_adjusted;
            float3 global_glint_color = float3(global_glint_normalized, global_glint_normalized, global_glint_normalized) * _GlobalGlintColor.xyz * float3(_GlobalGlintIntensity, _GlobalGlintIntensity, _GlobalGlintIntensity);

            // Combine glint effects
            float3 glint_base_color = glint_mask.xyz * glint_mask.w;
            float3 local_glint_color = float3(local_glint, local_glint, local_glint) * _GlintColor.xyz;
            float3 global_glint_combined = global_glint_color * _GlobalGlintColor.xyz * float3(_GlobalGlintIntensity, _GlobalGlintIntensity, _GlobalGlintIntensity);
            float3 total_glint_color = local_glint_color + global_glint_combined;
            float3 final_glint = glint_base_color * total_glint_color;

            // Final output
            output.xyz = output.xyz + final_glint;
        }

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
    if(_UseMatcap)output.xyz = output * rimsdw_color + matcap;
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
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // Heated
        float heated_check = _UseOverHeated;

        float heat_height = smoothstep(0.f, max(_HeatedHeight, 0.01), i.ws_pos.y + (-_CharaWorldSpaceOffset.y));  
        float heat_thresh = smoothstep(1.0f, _HeatedThreshould, heat_height);
        float heat_inv = smoothstep(((-_HeatedThreshould) + 1.0), ((-_HeatedThreshould) * 2.0 + 1.0), heat_height);

        float3 heat_color = lerp(_HeatColor0.xyz, _HeatColor1.xyz, heat_thresh); 
        heat_color = lerp(heat_color, _HeatColor2.xyz, heat_inv);

        heat_color = heat_height * heat_color; 

        heat_color = heat_color * _HeatInst + output.xyz;

    output.xyz = (heated_check > 0.5) ? heat_color : output.xyz;
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // flame crystal
        if(_FlameCrystal)
        {
            float3 effect_color; // u_xlat16_6
            float flame_ID = uint(id_rounded);
            switch (flame_ID)
            {
                case 1: 
                    effect_color = _EffectColor1.xyz;;
                    break;
                case 2:
                    effect_color = _EffectColor2.xyz;
                    break;
                case 3:
                    effect_color = _EffectColor3.xyz;
                    break;
                case 4:
                    effect_color = _EffectColor4.xyz;
                    break;
                case 5:
                    effect_color = _EffectColor5.xyz;
                    break;
                case 6:
                    effect_color = _EffectColor6.xyz;
                    break;
                case 7:
                    effect_color = _EffectColor7.xyz;
                    break;
                default:
                    effect_color = _EffectColor0.xyz;
                    break;
            }

            float2 crystal_tex = _CrystalTex.Sample(sampler_linear_repeat, uv.xy).xy;
            float crystal_y = crystal_tex.y - 1.0f;

            float crystal_power = pow(ndotv.x, 8.0f);
            crystal_power = crystal_power * _CrystalTransparency + crystal_tex.x;
            crystal_power = clamp(crystal_power, 0.0, 1.0);
            crystal_power = (-crystal_power) + 1.0;  //  u_xlat16_44

            float crystal_range = saturate(_CrystalRange1 * 2.0 + (-crystal_tex.y)); // u_xlat16_46
            float crystal_intensity = (-crystal_y) + 1.0f;
            float crystal_offset = _CrystalRange2 - 0.5f;
            crystal_intensity = crystal_offset * 2.0f + crystal_intensity;
            crystal_intensity = saturate(crystal_intensity);
            crystal_range = min(crystal_range, crystal_intensity);

            float final_crystal = crystal_power * crystal_range;
            final_crystal = min(final_crystal, _ColorIntensity);

            float3 crystal_effect = lerp(1.0f, effect_color.xyz, final_crystal);

            effect_color = (-crystal_power + 1.0f) * (crystal_range * effect_color.xyz);
            
            output.xyz = output.xyz * crystal_effect + effect_color.xyz;

            if(int(_FlameID) ==  flame_ID)
            {
                float2 parallax = i.tangent.yz * view.zx + (-(view.yz * i.tangent.zx));
                parallax.xy = parallax.xy + float2(1.0, 1.0);
                parallax.xy = parallax.xy * float2(0.5, 0.5);
                parallax.xy = clamp(parallax.xy, 0.0, 1.0);

                float2 flame_uv = smoothstep(_FlameWidth, _FlameWidth + (-0.5), parallax.xy);
                flame_uv = float2(float2(_FlameSpeed, _FlameSpeed)) * _Time.yy + flame_uv.yx;
                float flame_tex = _FlameTex.Sample(sampler_linear_repeat, flame_uv.xy).x;

                float2 swirl_uv = _Time.y * _FlameSwirilSpeed;
                swirl_uv.xy = uv.xy * float2(float2(_FlameSwirilTexScale, _FlameSwirilTexScale)) + swirl_uv.xx;
                float swirl_tex = _FlameTex.Sample(sampler_linear_repeat, swirl_uv.xy).y;

                float swirl_amount = flame_tex + swirl_tex;
                swirl_uv = swirl_amount * _FlameSwirilScale + parallax;
                swirl_tex = _FlameTex.Sample(sampler_linear_repeat, swirl_uv.xy).z;

                swirl_tex = smoothstep(_FlameHeight, _FlameHeight + (-0.25f), crystal_y * swirl_tex) * crystal_power;

                output.xyz = lerp(output.xyz, lerp(_FlameColorOut.xyz, _FlameColorIn.xyz, swirl_tex), swirl_tex);
            }
        }
        // return crystal_range.xxxx;
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
    // Starry Sky Cloak
        if(_StarrySky) output.xyz = starry_cloak(i.screenpos, i.view, uv, i.ws_pos, tangents, output);     
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // fake reflection:
        if(_StarrySky)
        {
            // it is kinda redundant to sample this texture again but whatever
            float2 sky_mask = _SkyMask.Sample(sampler_linear_repeat, uv * _SkyMask_ST.xy + _SkyMask_ST.zw).xy + _SkyRange;
            float refl_ndoth = max(ndoth, 0.001f);
            refl_ndoth = pow(refl_ndoth, _ReflectionRoughness); 
            refl_ndoth  = smoothstep(_ReflectionThreshold,  _ReflectionSoftness, refl_ndoth);

            float refl_blend = smoothstep(_ReflectionBlendThreshold, 20.f, ndoth);

            float rev_refl = smoothstep(_ReflectionReversedThreshold+0.05f, _ReflectionReversedThreshold, ndoth);

            // blending time
            float3 refl_rev_color = (rev_refl *  _ReflectionBlendColor) * _FakeRefBlendIntensity;
            float3 refl_blnd_color = (refl_blend *  _ReflectionBlendColor) * _FakeRefBlendIntensity;
            float3 refl_color = ((refl_ndoth *  _ReflectionColor) * _FakeRefAddIntensity) * sky_mask.x;


            refl_blnd_color = refl_blnd_color * sky_mask.x + refl_color;
            refl_rev_color = refl_rev_color * sky_mask.y + refl_color;
            output.xyz =  1.0f  * output.xyz + refl_rev_color;
        }
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // dissolve outline
        if(_DissoveON) dissolve_outline(output, dis_area, dis_map);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    if(_UseHeightLerp) heightlightlerp(i.ws_pos, output);
    return output;
}

vertex_output outline_vertex(vertex_input v)
{
    vertex_output o = (vertex_output)0;
    int2 hidepart = int2(v.color.yx * 256);
    hidepart = int2(uint(hidepart.x) & uint(_ShowPartID), uint(hidepart.y) & uint(_ShowPartID));
    int tmp =  _HideCharaParts ? hidepart.x : 1;


    float4 outline_pos = mul(UNITY_MATRIX_MV, v.vertex);

    float3 outline_normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);

    outline_normal.z = -0.1f;
    outline_normal = normalize(outline_normal);

    float outline_offset = (v.color.z < 0.99f) ? v.color.z : 0.0f;
    outline_offset = outline_offset * _OutlineOffset;
    float pos_w = (-outline_offset) + 0.0099f + outline_pos.z;

    float outline_fov = pos_w / unity_CameraProjection._43;
    outline_fov = 1.0f / (rsqrt(abs(outline_fov) /  _OutlineScale));

    float outline_scale =  _OutlineWidth * _OutlineScale;
    outline_scale = outline_scale * v.color.w;
    outline_scale = outline_scale * outline_fov;

    o.view = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);

    float view_length = length(o.view);
    view_length = smoothstep(_OutlineExtdStart, _OutlineExtdMax, view_length);
    view_length = min(view_length, 0.5f) + 1.0f;
    outline_scale = outline_scale * view_length;

    outline_pos.xyz = outline_normal * outline_scale + outline_pos.xyz;

    o.vertex = mul(UNITY_MATRIX_P, outline_pos);



    // o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    o.vertex = ((0 < tmp)) ?  o.vertex : float4(-99.0, -99.0, -99.0, 1.0);

    float2 front_uv = _UVChannelFront ? v.uv2.xy : v.uv.xy;
    float2 back_uv = _UVChannelBack ? v.uv2.xy : v.uv.xy;
    o.uv = offset_tiling(front_uv, _MainTex_ST);
    o.uv2 = back_uv; // back is not scaled
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 

    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);
    o.opos = o.vertex;

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

    UNITY_TRANSFER_FOG(o,o.vertex);
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


    // initalize inputs: 
    float2 uv = i.uv;
    float3 normal = normalize(i.normal);
    float ndotl = dot(normal, _WorldSpaceLightPos0.xyz);

    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);
    float material_ID = floor(8.0f * lightmap.w);
    float id_check = material_ID * 8.0f;
    float2 id_transform = (id_check) >= (-id_check) ? float2(8.0f, 0.125f) : float2(-8.0f, -0.125f);
    material_ID = frac(material_ID * id_transform.y) * id_transform.x;
    float id_rounded = round(material_ID);

    float outline_ID = uint(id_rounded);
    float4 outline_color;
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

    if(_UseMaterialValuesLUT) outline_color = _MaterialValuesPackLUT.Load(float4(material_ID, 2, 0, 0));

    float outline_shadow = saturate(smoothstep(0.3, 0.4, ndotl * 0.5 + 0.5));

    float outline_darkness = (-_ES_OutLineDarkenVal) + 1.0;
    outline_darkness = lerp(outline_darkness, 1.0, outline_shadow);

    outline_shadow = outline_shadow * _ES_OutLineLightedVal;
    outline_color = outline_color * outline_darkness + outline_shadow;

    float intensity = (-_OutlineColorIntensity) + 1.0f;
    outline_color = outline_color * intensity;

    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
    // dissolve outline
    if(_DissoveON) dissolve_outline(outline_color, dis_area, dis_map);
    // - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

    return outline_color;
}
