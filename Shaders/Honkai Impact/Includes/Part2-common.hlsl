// normal mapping : 
// offline uses a standard normal mapping implementation
// using the tangents that are calculated by unity or imported from blender
float3 normal_offline(float3 normal, float3 tangent, float3 bitangent, float3 bumpmap)
{
    float3x3 tbn = {tangent.xyz, bitangent, normal};
    float3 normal_map = bumpmap;

    bumpmap.xyz = bumpmap.xyz * 2.0f - 1.0f;
    bumpmap.xy = bumpmap.xy * (float2)_BumpScale;
    bumpmap.xyz = normalize(bumpmap);

    normal = mul(bumpmap.xyz, tbn);
    normal = normalize(normal);
    return normal;   
}

// online uses a derivative function to first calculate tangents given a uv and a positiong vector
// this is useful for when you're storing secondary normals where the tangent would normally be
// genshin uses an implementation similiar to this one
void normal_online(float3 bumpmap, float3 ws_pos, float2 uv, inout float3 normal, inout float3 tangent, inout float3 bitangent)
{
    // if(_UseBump) 
    // {
        // reencode normal map to the proper ranges and scale it by _BumpScale
        bumpmap.xyz = bumpmap.xyz * 2.0f - 1.0f;
        bumpmap.xy = bumpmap.xy * (float2)_BumpScale;
        bumpmap.x = -bumpmap.x;
        bumpmap.xyz = normalize(bumpmap);

        // world space position derivative
        float3 p_dx = ddx(ws_pos);
        float3 p_dy = ddy(ws_pos);

        // texture coord derivative
        float3 uv_dx;
        uv_dx.xy = ddx(uv);
        float3 uv_dy;
        uv_dy.xy = ddy(uv);

        uv_dy.z = -uv_dx.y;
        uv_dx.z = uv_dy.x;

        // this functions the same way as the w component of a traditional set of tangents.
        // determinent of the uv the direction of the bitangent
        float3 uv_det = dot(uv_dx.xz, uv_dy.yz);
        uv_det = -sign(uv_det);

        // normals are inverted in the case of a back-facing poly
        // useful for the two sided dresses and what not... 
        float3 corrected_normal = normal;

        float2 tangent_direction = uv_det.xy * uv_dy.yz;
        tangent = (tangent_direction.y * p_dy.xyz) + (p_dx * tangent_direction.x);
        tangent = normalize(tangent);

        bitangent = (corrected_normal.yzx * tangent.zxy) - (corrected_normal.zxy * tangent.yzx); 
        bitangent = bitangent * -uv_det;

        float3x3 tbn = {tangent, bitangent, corrected_normal};

        float3 mapped_normals = mul(bumpmap.xyz, tbn);
        mapped_normals = normalize(mapped_normals); // for some reason, this normalize messes things up in mmd

        if(_UseBump) normal = mapped_normals;
    // }
}

float material_region(float alpha)
{
    // i dont know why they do this but they do actually offset the alpha by 0.1f
    alpha = alpha + 0.1f;
    float region = 0;
    float4 greater_ranges = float4(alpha >= 0.8f, alpha >= 0.6f, alpha >= 0.4f, alpha >= 0.2f);
    float2 less_ranges = float2(alpha < 0.6f, alpha <= 0.4f);
    float2 combo_ranges;
    combo_ranges.x = greater_ranges.z && less_ranges.x;
    combo_ranges.y = greater_ranges.w && less_ranges.y;

    region = (combo_ranges.y) ? 1 : 0;
    region = (combo_ranges.x) ? 2 : region;
    region = (greater_ranges.y) ? 3 : region;
    region = (greater_ranges.x) ? 4 : region;

    return region; // i specifically do it like this so it makes indexing the colors array easier for me
}

float3 face_exp(float2 uv, float4 color)
{
    #if defined(faceishadow)
        float4 expression_map = _FaceExpTex.Sample(sampler_linear_repeat, uv * _FaceExpTex_ST.xy + _FaceExpTex_ST.zw);

        // --- blush 
        float blush = expression_map.x * _ExpBlushIntensityR * _ExpBlushColorR.w;

        // --- shadow G
        float shadow_g = saturate((expression_map.y * _ExpShadowIntensityG) * expression_map.y) * _ExpShadowColorG.w; 
        
        // --- shadow B
        float shadow_b = saturate((expression_map.z * _ExpShadowIntensityG) * expression_map.z) * _ExpShadowColorB.w; 
        
        // --- shadow A
        float shadow_a = -expression_map.w + 1.0f;
        shadow_a = saturate((shadow_a * shadow_a) * _ExpShadowIntensityA) * _ExpShadowColorA.w;

        // lerps

        color.xyz = lerp(color.xyz, _ExpBlushColorR, blush);
        color.xyz = lerp(color.xyz, _ExpShadowColorG, shadow_g);
        color.xyz = lerp(color.xyz, _ExpShadowColorB, shadow_b);
        color.xyz = lerp(color.xyz, _ExpShadowColorA, shadow_a);
    #endif
    // output
    return color.xyz;
}

float2 shadow_area_body(float lightmap_shadow, float ndotl)
{
    float2 shadow = 1.0f;
    if(!_EnableShadow) return shadow;
    #if defined(use_shadow)
        float shadow_base = (ndotl + 1.0f) + _DiffuseOffset;
        float shadow_area = lightmap_shadow;
        shadow_base = saturate(shadow_base * shadow_area);

        float soft_area = _ToneSoft + 0.00001f;
        float area = _LightArea - soft_area;
        soft_area = soft_area + _LightArea;

        shadow_base = smoothstep(area, soft_area, shadow_base);

        float scene_soft = _SceneShadowSoft + 0.00001f;
        area = _LightArea - scene_soft;
        scene_soft = scene_soft + _LightArea;

        float soft = smoothstep(area, scene_soft, 1.0f);
        soft = soft * shadow_base;
        shadow = min(soft, 1.0f);
        shadow.y = soft;
    #endif
    return shadow;
}

float3 shadow_area_hair(float lightmap_shadow, float ndotl)
{
    float shadow = 1.0f;
    if(!_EnableShadow) return shadow;
    #if defined(use_shadow) && defined(is_hair)
        float shadow_base = ndotl + _DiffuseOffset;
        float shadow_area = lightmap_shadow + 0.5f;
        shadow_base = saturate(shadow_base * shadow_area);

        float soft_area = _ToneSoft + 0.00001f;
        float area = _LightArea - soft_area;
        soft_area = soft_area + _LightArea;

        shadow_base = smoothstep(area, soft_area, shadow_base);

        float scene_soft = _SceneShadowSoft + 0.00001f;
        area = _LightArea - scene_soft;
        scene_soft = scene_soft + _LightArea;

        float soft = smoothstep(area, scene_soft, 1.0f);
        soft = soft * shadow_base;
        shadow = min(soft, 1.0f);
    #endif
    return shadow;
}

float shadow_area_face(float2 uv, float3 light)
{
    float shadow_step = 1.0f;
    if(!_EnableShadow) return shadow_step;
    #if defined(use_shadow) && defined(faceishadow)
        float3 head_forward = normalize(UnityObjectToWorldDir(_headForwardVector.xyz));
        float3 head_right   = normalize(UnityObjectToWorldDir(_headRightVector.xyz));
        light = normalize(light);
        float rdotl = dot((head_right.xz),  (light.xz));
        float fdotl = dot((head_forward.xz), (light.xz));

        float2 faceuv = 1.0f;
        if(rdotl > 0.0f )
        {
            faceuv = uv;
        }  
        else
        {
            faceuv = uv * float2(-1.0f, 1.0f) + float2(1.0f, 0.0f);
        }

        shadow_step = 1.0f - (fdotl * 0.5f + 0.5f);


        // use only the alpha channel of the texture 
        float facemap = _FaceMapTex.Sample(sampler_linear_repeat, faceuv).w;
        // interpolate between sharp and smooth face shading
        shadow_step = smoothstep(shadow_step - (0.001f), shadow_step + (0.001f), facemap);
    #endif
    return shadow_step;
}

float3 shadow_base(float shadow_area, float alpha, float lightmap_shadow)
{
    float3 color = (float3)1.0f;
    if(!_EnableShadow) return color;
    #if defined(use_shadow)
        float region = material_region(alpha);
        if(!_ShadowRampTexUsed) region = 0;

        float enable_contrast = (_EnableBlack < 0.5f) ? 1.0 : _ShadowContrast;

        float4 shadow_color[5] =
        {
            _ShadowMultColor,
            _ShadowMultColor2,
            _ShadowMultColor3,
            _ShadowMultColor4,
            _ShadowMultColor5
        };

        float3 first_color  = enable_contrast * shadow_color[region].xyz;

        // sample ramp texture 
        float2 ramp_uv = _RampTexV;
        ramp_uv.x = shadow_area.x;

        float3 ramp_shadow = _RampTex.Sample(sampler_linear_clamp, ramp_uv);

        color = lerp(first_color, ramp_shadow, shadow_area.x);
    #endif
    return color;
}

float3 shadow_hair(float2 shadow_area, float lightmap_shadow)
{
    float3 color = (float3) 1.0f;
    if(!_EnableShadow) return color;
    #if defined(use_shadow) && defined(is_hair)
        float enable_contrast = (_EnableBlack < 0.5f) ? 1.0 : _ShadowContrast;

        float3 first_color  = enable_contrast * _FirstShadowMultColor.xyz;
        float3 second_color = enable_contrast * _SecondShadowMultColor.xyz;

        // sample ramp texture 
        float2 ramp_uv = _RampTexV;
        ramp_uv.x = shadow_area.x * 0.5 + 0.5;

        float3 ramp_shadow = _RampTex.Sample(sampler_linear_clamp, ramp_uv);

        color = -_FirstShadowMultColor.xyz * enable_contrast + ramp_shadow;
        color = shadow_area.x * color + first_color;

        float second_area = (lightmap_shadow >= 0.2) ? 1.0 : 0.0;

        color = -_SecondShadowMultColor.xyz * enable_contrast + color;
        color = second_area * color + second_color;
    #endif

    return color;
}

void metal(float3 normal, float shadow_area, float3 shadow_color, float ndoth, float lightmap_red, float2 soft_ranges, float3 main_color, float alpha, out float3 out_specular, out float3 out_shadow)
{
    #if defined(use_metal)
        float2 sphere_uv = mul(normal, (float3x3)UNITY_MATRIX_I_V).xy;
        sphere_uv.x = sphere_uv.x * _MTMapTileScale; 
        sphere_uv = sphere_uv * 0.5f + 0.5f; 

        float sphere = _MTMap.Sample(sampler_linear_repeat, sphere_uv);

        float sphere_base = sphere * shadow_area;

        float3 sphere_color = lerp(_MTMapDarkColor, _MTMapLightColor, sphere_base);

        float3 metal_shadow = shadow_color * _MTShadowMultiColor;
        metal_shadow = shadow_area * (-(_MTShadowMultiColor) * shadow_color + sphere_color) + metal_shadow;
        
        float3 metal_specular = min(pow(ndoth, _MTShininess), 1.0f);
        metal_specular = (metal_specular * _MTSpecularColor) * lightmap_red;
        metal_specular = smoothstep(soft_ranges.x, soft_ranges.y, metal_specular * _SpecMulti);

        float metal_atten = lerp(_MTSpecularAttenInShadow, 1.0f, sphere_base);
        metal_specular = (metal_specular * metal_atten) * _MTMapBrightness;

        // outputs : 
        out_shadow = metal_shadow;
        out_specular = metal_specular;
    #endif
}

float3 specular_regular(float lightmap_alpha, float2 shadow_area, float3 shadow_color, float ndoth, float2 specular_tex)
{

    float3 specular = (float3)0.0f;
    if(!_EnableSpecular) return specular;
    #if defined(use_specular)
        float region = _SpecularRampTexUsed ? material_region(lightmap_alpha) : 0;

        float4 specular_colors[5] = 
        {
            _LightSpecColor,
            _LightSpecColor2,
            _LightSpecColor3,
            _LightSpecColor4,
            _LightSpecColor5,
        };

        float specular_power = specular_colors[region].w * _Shininess;

        float2 specular_soft = _SpecSoftRange.xx + float2(0.00001f, 0.5f);
        float specular_soft_low = 0.5f - specular_soft.x;

        float specular_area = lerp(1.0f, specular_tex.y, _UseSoftSpecular);
        specular_power = specular_power * specular_area;

        specular = pow(ndoth, specular_power);
        float spec_hard = specular * specular_tex.x;
        
        specular = smoothstep(specular_soft_low.x, specular_soft.y, specular * specular_tex.x);
        specular = specular * specular_colors[region];

        specular_area = 1.0f - specular_tex.y;
        specular_area = (spec_hard >= specular_area) ? 1.0f : 0.0;

        specular_area = saturate((1.0f / specular_soft.x) * specular_area);
        specular_area = (specular_area * -2.0f + 3.0f) * (specular_area * specular_area);

        specular = _UseSoftSpecular ? specular : (specular_area * specular_colors[region].xyz);

        specular = specular * _SpecMulti;
    #endif
    return specular;
}

float3 hair_specular(float3 normal, float3 bitangent, float3 light, float3 view, float2 uv, float shadow)
{
    float3 shine_color = (float3)0.0f;
    if(!_EnableSpecular) return shine_color;
    #if defined(use_specular) && defined(is_hair)
        // sample textures first
        float2 jitter_uv = uv * _JitterMap_ST.xy + _JitterMap_ST.zw;

        float jitter  = _JitterMap.Sample(sampler_linear_repeat, jitter_uv).x;
        float4 mask   = _SpecularMaskMap.Sample(sampler_linear_repeat, uv);
        float pattern = _HairStripPatternsTex.SampleLevel(sampler_linear_repeat, float2(uv.x, 0.5f) * _HairStripPatternsTex_ST.xy + _HairStripPatternsTex_ST.zw, 0.0f) * 2.0f - 1.0f;
        
        float3 specular_offset = _SpecularOffset * float3(-1.0f, 1.0f, 1.0f);
        specular_offset = normalize(specular_offset);

        float pJitter = lerp(_SpecularLowJitterRangeMin, _SpecularLowJitterRangeMax, pattern);
        pJitter = lerp(-pJitter * 0.5f + 0.5f, pJitter * 0.5f + 0.5f, jitter);

        float specular_shininess = _SpecularLowShininessRangeMax - _SpecularLowShininessRangeMin;
        specular_shininess = specular_shininess * 0.5f + _SpecularLowShininessRangeMin;

        float ldotv = dot(light, view);

        float low_shift = lerp((_SpecularLowShift - _SpecularShiftRange), _SpecularLowShift, abs(ldotv));
        float high_shift = lerp((_SpecularHighShift - _SpecularShiftRange), _SpecularHighShift, abs(ldotv));

        float jitter_shift = pJitter + low_shift;

        float3 aniso = normalize(jitter_shift * normal + bitangent);
        aniso = mul(aniso, (float3x3)unity_WorldToObject);

        float shine = dot(aniso, specular_offset);

        shine = -shine * shine + 1.0f;
        shine = max(sqrt(max(shine, 0.0f)), 0.0f);
        shine = pow(shine, specular_shininess);

        float mask_lerp = lerp(1.0f, mask.x, _SpecularMaskLerp);
        shine = shine * lerp(1.0f, mask.x, _SpecularMaskLerp);

        float2 high_jitter = uv.xx * float2(_SpecularHighJitterRangeMin, _SpecularHighJitterRangeMax);
        high_jitter = high_jitter * float2(-2.0f, -2.0f) + float2(_SpecularHighJitterRangeMin.x, _SpecularHighJitterRangeMax.x);
        
        low_shift = 0.5f - high_jitter.x;
        high_jitter.x = high_jitter.y + 0.5f;

        float hJitter = lerp(low_shift, high_jitter.x, mask.x);

        specular_shininess = lerp(_SpecularHighShininessRangeMax, _SpecularHighShininessRangeMin, mask.y);

        float high_offset = jitter.x * hJitter + high_shift;
        aniso = normalize(high_offset * normal + bitangent);
        aniso = mul(aniso, (float3x3)unity_WorldToObject);

        float high_shine = dot(aniso, specular_offset);
        high_shine = -high_shine * high_shine + 1.0f;
        high_shine = max(sqrt(max(high_shine, 0.0f)), 0.001f);
        high_shine = pow(high_shine, specular_shininess);

        float specular_ramp = _RampMap.Sample(sampler_linear_clamp, float2(high_shine, 0.5f)).w;
        high_shine = specular_ramp * mask_lerp;

        float mask_region = (mask.z >= 0.5) ? 1.0 : 0.0;
        high_shine = high_shine * mask_region;

        shine_color = shine * _SpecularLowColor.xyz;
        float3 shine_color_b = (high_shine * _SpecularHighColor.xyz) * _SpecularHighIntensity.xxx;
        shine_color = shine_color * _SpecularLowIntensity.xxx + shine_color_b;

        float3 final_shine = (shadow * 0.5f + 0.5f) * shine_color;
        #endif
    return shine_color;
}

float3 tights(float3 normal, float3 light, float3 view, float3 tangent, float3 bitangent, float2 uv, float4 lightmap, float3 color)
{
    if(_EnableStocking)
    {
        float ndotl = dot(normal, light) * 0.5f + 0.5f;
        float shadow = smoothstep((-_StockingDiffSoftRange) + _StockingLightArea, _StockingDiffSoftRange + _StockingLightArea, ndotl);
        
        float ndotv = 1.0f - dot(normal, view);

        float stock_dark_area = min(pow(ndotv, _StockingDarkenRange), 1.0f);
        float3 stocking_dark = ndotv * _StockingDarkenColor.xyz + (1.0f - ndotv);

        float stock_add_area = saturate((min(pow(ndotv, 2.5f), 1.0f) + -0.6f) * -2.0f);
        float something_else = -(stock_add_area * -2.0f + 3.0f) * (stock_add_area * stock_add_area) + 1.f;
        stock_add_area = (stock_add_area * stock_add_area) * (stock_add_area * -2.0f + 3.0f);
        
        float3 stocking_add = something_else * _StockingDarkenColorAdd.xyz + stock_add_area;
        
        float3 stocking_color = stocking_dark * stocking_add;


        float3 stocking_ramp = _StockingRampTex.Sample(sampler_linear_clamp, float2(shadow, 0.4f));

        stocking_ramp = min(stocking_add, stocking_ramp);

        // aniso line 
        float3 aniso = normal - tangent;
        aniso = aniso * _StockingSpecNormalInt + tangent;
        aniso = saturate(dot(aniso, view));
        aniso = (pow(aniso + 0.01f, _StockingSpecRange * 255.0f));

        float3 aniso_ramp = _StockingRampTex.Sample(sampler_linear_clamp, float2(aniso.x, 0.8f));

        aniso_ramp = (aniso_ramp * lightmap.y) * _StockingSpecInt;

        float stocking_edge = 1.0f - min(pow(ndotv, _StockingEdgeRange), 1.0f);

        float stocking_shadow = saturate(1.0f - dot(normal, light));

        stocking_shadow = smoothstep((-_StockingEdgeSoft) + 0.800000012, ((_StockingEdgeSoft) + 0.800000012), stocking_edge * stocking_shadow);
        
        float3 stocking = color * stocking_ramp + aniso_ramp;
        stocking = _StockingEdgeColor * stocking_shadow + stocking;

        return stocking;
    }
    return 0;
}

float4 rg_color(float alpha)
{
    float4 rim_glow = float4((float3)0.0f, 1.0f);
    #if defined(use_rimglow)
        float region = material_region(alpha);
        region = _RGRampTexUsed ? region : 0;

        float4 color[5] = // xyz = color data, w = power
        {
            _RGColor,
            _RGColor2,
            _RGColor3,
            _RGColor4,
            _RGColor5,
        };
        
        rim_glow = float4(color[region].xyz * (float3)_RimGlowStrength, color[region].w * _RGPower);
    #endif
    return rim_glow;
}
float3 rim_glow(float ndotv, float4 colors)
{
    float3 rim = (float3)0.0f;
    #if defined(use_rimglow)
        ndotv = saturate(ndotv);
        ndotv = (1.0f - ndotv) + 9.99999975e-05;

        rim = pow(ndotv, colors.w);
        rim = min(rim, 1.0f);

        float soft_rim = _RGSoftRange + 0.009f;
        
        rim = smoothstep(soft_rim + 0.5f, -soft_rim + 0.5f, -rim + 1.0);

        rim = _EnableRimGlow ? rim : 0;
        rim = rim * colors.xyz;
    #endif
    return rim;
}

float3 emission(float alpha, float emission_tex, float3 in_color)
{
    float3 emission = (float3)0.0f;
    #if defined(use_emission)
        float region = material_region(alpha);
        region = _EmissionRampTexUsed ? region : 0.0f;
        float4 emission_colors[5] = 
        {
            _EmissionColor,
            _EmissionColor2,
            _EmissionColor3,
            _EmissionColor4,
            _EmissionColor5,
        };

        float emissiveness = emission_tex * _EmissionStrength;
        emission = emissiveness * emission_colors[region].xyz;
        
        emissiveness = floor(_MulAlbedo);
        float3 albedo = emissiveness * in_color - emissiveness;
        albedo = albedo + 1.0f;
        emission = emission * albedo;
        emission = _Emission_Type ? emission : 0.0f;
    #endif
    return emission;
}

float3 DecodeLightProbe( float3 N )
{
    return ShadeSH9(float4(N,1));
}