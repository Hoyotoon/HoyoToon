float2 offset_tiling(float2 uv, float4 st)
{
    return float2(uv.xy * st.xy + st.zw);
}
// texture sampling
//- 
    float4 sample_lr_texture(Texture2D tex, float2 uv)
    {
        return tex.Sample(sampler_linear_repeat, uv);
    }

    float4 sample_lc_texture(Texture2D tex, float2 uv)
    {
        return tex.Sample(sampler_linear_clamp, uv);
    }

    float4 sample_pr_texture(Texture2D tex, float2 uv)
    {
        return tex.Sample(sampler_point_repeat, uv);
    }

    float4 sample_pc_texture(Texture2D tex, float2 uv)
    {
        return tex.Sample(sampler_point_clamp, uv);
    }
//-
float packed_channel_picker(SamplerState texture_sampler, Texture2D texture_2D, float2 uv, float channel)
{
    float4 packed = texture_2D.Sample(texture_sampler, uv);

    float choice;
    if(channel == 0) {choice = packed.x;}
    else if(channel == 1) {choice = packed.y;}
    else if(channel == 2) {choice = packed.z;}
    else if(channel == 3) {choice = packed.w;}

    return choice;
}

// from: https://github.com/cnlohr/shadertrixx/blob/main/README.md#best-practice-for-getting-depth-of-a-given-pixel-from-the-depth-texture
float GetLinearZFromZDepth_WorksWithMirrors(float zDepthFromMap, float2 screenUV)
{
	#if defined(UNITY_REVERSED_Z)
	zDepthFromMap = 1 - zDepthFromMap;
			
	// When using a mirror, the far plane is whack.  This just checks for it and aborts.
	if( zDepthFromMap >= 1.0 ) return _ProjectionParams.z;
	#endif

	float4 clipPos = float4(screenUV.xy, zDepthFromMap, 1.0);
	clipPos.xyz = 2.0f * clipPos.xyz - 1.0f;
	float4 camPos = mul(unity_CameraInvProjection, clipPos);
	return -camPos.z / camPos.w;
}

// https://github.com/cnlohr/shadertrixx/blob/main/README.md#detecting-if-you-are-on-desktop-vr-camera-etc
bool isVR(){ 
    // USING_STEREO_MATRICES
    #if UNITY_SINGLE_PASS_STEREO
        return true;
    #else
        return false;
    #endif
}

float materialID(float alpha)
{
    int material = 0;
    
    bool4 checka = alpha.xxxx >= float4(0.8f, 0.4f, 0.2f, 0.6f);
    bool4 checkb = alpha.xxxx <= float4(0.6f, 0.4f, 0.8f, 0.0f);

    material = (_UseMaterial2 && checka.x) ? 2 : 1;
    material = (_UseMaterial3 && checka.y && checkb.x) ? 3 : material;
    material = (_UseMaterial4 && checka.z && checkb.y) ? 4 : material;
    material = (_UseMaterial5 && checka.w && checkb.z) ? 5 : material;


    return material;
}

void pointlight_unity(in float4 pos, in float3 normal, inout float3 color)
{
    float3 pl_matrix[4] =
    {
        float3(unity_4LightPosX0.x, unity_4LightPosY0.x, unity_4LightPosZ0.x),
        float3(unity_4LightPosX0.y, unity_4LightPosY0.y, unity_4LightPosZ0.y),
        float3(unity_4LightPosX0.z, unity_4LightPosY0.z, unity_4LightPosZ0.z),
        float3(unity_4LightPosX0.w, unity_4LightPosY0.w, unity_4LightPosZ0.w),
    };

    float pl_atten[4] =
    {
        unity_4LightAtten0.x, 
        unity_4LightAtten0.y,
        unity_4LightAtten0.z,
        unity_4LightAtten0.w
    };

    float3 ws_pl = mul((float3x3)unity_ObjectToWorld, pos.xyz);

    for(int i = 0; i <= 3; i++)
    {
        float3 toLight = normalize(pl_matrix[i].xyz - ws_pl.xyz);

        float lightLength = length(pl_matrix[i].xyz - ws_pl);
        lightLength = lightLength * max(pl_atten[i], 0.00001f);
        lightLength = pow(max(1.0f - lightLength, 0.0f), 2.0f);

        float lighting = max(dot(normal, toLight), 0.0f) * lightLength;

        color += lighting * unity_LightColor[i].xyz;
    }
}

void dither( in float2 screen_pos)
{
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


    uint2 screen = (uint2)screen_pos & (uint2)3;
    
    float4 lookup;
    lookup.x = dot(bayer[0], identity[screen.y]);
    lookup.y = dot(bayer[1], identity[screen.y]);
    lookup.z = dot(bayer[2], identity[screen.y]);
    lookup.w = dot(bayer[3], identity[screen.y]);

    float dither_value = dot(lookup, identity[screen.x]);
    _DitherAlpha = 1 - _DitherAlpha;
    dither_value =  _DitherAlpha * 17.0 - dither_value;
    dither_value = 0.01 - dither_value;
    bool check = (_UsingDitherAlpha && _DitherAlpha) && dither_value < 0.0f;
    if(check) discard;            

}

void get_shadowtransition(in int material_id, out float2 shadow_transition)
{
    float2 trans[5] =
    {
        float2(_ShadowTransitionRange, _ShadowTransitionSoftness),
        float2(_ShadowTransitionRange2, _ShadowTransitionSoftness2),
        float2(_ShadowTransitionRange3, _ShadowTransitionSoftness3),
        float2(_ShadowTransitionRange4, _ShadowTransitionSoftness4),
        float2(_ShadowTransitionRange5, _ShadowTransitionSoftness5),
    };

    shadow_transition = trans[material_id];
}

void get_softshadow(in int material_id, out float2 shadow_soft)
{
    float2 tmp[5] =
    {
        float2(_useShadowSoft, _shadowSoftRange),
        float2(_useShadowSoft2, _shadowSoftRange2),
        float2(_useShadowSoft3, _shadowSoftRange3),
        float2(_useShadowSoft4, _shadowSoftRange4),
        float2(_useShadowSoft5, _shadowSoftRange5),
    };

    shadow_soft = tmp[material_id];
}

void get_emission(in int material_id, out float4 color, out float scalar)
{
    float4 tmp[5] = 
    {
        _EmissionColor_MHY,
        _EmissionColor_MHY2,
        _EmissionColor_MHY3,
        _EmissionColor_MHY4,
        _EmissionColor_MHY5,
    };
    float scal[5] =
    {
        _EmissionScaler,
        _EmissionScaler2,
        _EmissionScaler3,
        _EmissionScaler4,
        _EmissionScaler5,
    };

    color = tmp[material_id];
    scalar = scal[material_id];
}

void get_color(in int material_id, out float4 color)
{
    float4 tmp[5] = 
    {
        _Color, 
        _Color2, 
        _Color3, 
        _Color4, 
        _Color5
    };
    color = tmp[material_id];
}

void get_specular(in int material_id, out float4 color, out float shininess, out float multi)
{
    float4 tmp[5] = 
    {
        _SpecularColor, 
        _SpecularColor2, 
        _SpecularColor3, 
        _SpecularColor4, 
        _SpecularColor5
    };

    float2 value[5] =
    {
        float2(_Shininess, _SpecMulti),
        float2(_Shininess2, _SpecMulti2),
        float2(_Shininess3, _SpecMulti3),
        float2(_Shininess4, _SpecMulti4),
        float2(_Shininess5, _SpecMulti5),
    };

    color = tmp[material_id];
    shininess = value[material_id].x;
    multi = value[material_id].y;
}

void get_outline(in int material_id, out float4 color)
{
    float4 tmp[5] =
    {
        _OutlineColor,
        _OutlineColor2,
        _OutlineColor3,
        _OutlineColor4,
        _OutlineColor5,
    };
    color = tmp[material_id];
}

void get_shadowcolor(in int material_id, out float4 warm, out float4 cool)
{
    float4 tmpa[5] =
    {
        _FirstShadowMultColor,
        _FirstShadowMultColor2,
        _FirstShadowMultColor3,
        _FirstShadowMultColor4,
        _FirstShadowMultColor5
    };
    warm = tmpa[material_id];
    float4 tmpb[5] = 
    {
        _CoolShadowMultColor,
        _CoolShadowMultColor2,
        _CoolShadowMultColor3,
        _CoolShadowMultColor4,
        _CoolShadowMultColor5
    };
    cool = tmpb[material_id];
}

#if defined(SHADOW_RAMP_ON)
    void get_shadowramp(in int material_id, in float shadowarea, out float4 warm, out float4 cool)
    {
        float3 coord = shadowarea;
        coord.x = saturate(coord.x);
        material_id = max(material_id, 0);
        coord.y = 1.0f - saturate(((float)material_id - 1) * 0.1 + 0.05);
        if(_UseCoolShadowColorOrTex)
        {
            coord.z = 1.0f - saturate(((float)material_id - 1) * 0.1 + 0.55);
            warm = _PackedShadowRampTex.Sample(sampler_linear_clamp, coord.xy);
            cool = _PackedShadowRampTex.Sample(sampler_linear_clamp, coord.xz);    
        } else {
            warm = _PackedShadowRampTex.Sample(sampler_linear_clamp, coord.xy);
        }
    }
#endif

#if defined(FACE_MAP_NEW_ON)
    void face_angle_extract(in float3 lightDir, in float2 uv, out float3 output)
    {
        float3 localUp = normalize(mul((float3x3)unity_WorldToObject, float3(0, 1, 0)));

        // Check which local axis is most aligned with world up
        float upY = abs(localUp.y);
        float upZ = abs(localUp.z);

        float rot_x, rot_z;
        bool is_corrected = true;
           
        if (_IsYup)
        {
            float3 light = mul((float3x3)unity_WorldToObject, lightDir.xyz);
            
            rot_x = -light.x;
            rot_z = light.z;
        }
        else
        {
            float3 light = mul((float3x3)unity_WorldToObject, -lightDir.xyz);
            
            rot_x = -light.z;
            rot_z = -light.y;
        }
        
        
        float angle = atan2(rot_x, rot_z);
        float facing = saturate(abs(angle) * (1.0f / UNITY_PI));
        
        float2 face_uv = (angle < 0.0f) ? uv * float2(-1.0, 1.0) + float2(1.0f, 0.0f) : uv;
        output = float3(face_uv, facing);
    }

    void face_shading(in float3 face_angle, out float area)
    {
        float angle = face_angle.z;
        angle = smoothstep(max(_FaceMapRotateOffset, 0.0), min(_FaceMapRotateOffset + 1.0f, 1.0f), angle);
        float2 faceuv = face_angle.xy;

        float facemap = _FaceMapTex.Sample(sampler_linear_repeat, faceuv).w;
        float softness = max(_FaceMapSoftness, 0.000000001f);
        float facerange = smoothstep(angle - (_FaceMapSoftness), angle + (_FaceMapSoftness), facemap);

        area = facerange;
    }
#endif

void normal_map(inout float3 normal, in float3 view, in float2 uv, float2 normalmap, in bool front_facing)
{
    // this is the old form of the bump mapping, the uber one accounts for the newer bump textures
    #if defined(BUMP_TEXTURELINE_MAP)
    float3 bump;
    bump.xy = normalmap * 2 - 1;
    bump.z = 1.0f - _BumpScale;
    bump = normalize(bump);
    
    float3 dPdx = ddx(view.yzx); 
    float3 dPdy = ddy(view.zxy); 
    float2 dUVdx = ddx(uv); 
    float2 dUVdy = ddy(uv); 
    
    float3 bump_normal = front_facing ? normal : -normal;
    
    float3 u_dir = cross(dPdx, normal);
    float3 v_dir = cross(dPdy, normal);
    
    
    float3 t_norm = v_dir * dUVdx.x + (dUVdy.x * u_dir);
    float3 b_norm = v_dir * dUVdx.y + (dUVdy.y * v_dir);
    
    float max_sq_len = max(dot(b_norm, b_norm), dot(t_norm, t_norm));
    float inv_len = 1.0 / sqrt(max_sq_len);
    
    float3 bitangent = b_norm * inv_len;
    float3 tangent = t_norm * inv_len;
        
    float t_len_inv = rsqrt(max(dot(tangent, tangent), 0.001));
    tangent *= t_len_inv;
    
    float3 bumped = (bump.x * tangent) + (bump.y * bitangent);
    bumped = (bump.z * normal) + bumped;
    
    bool use_bump = (0.99f >= bump.z);
    
    normal = use_bump ? normalize(bumped) : normal;
    
    #endif
}

void rim_light(in float2 screen, in float3 normal, in float3 view, in float3 light, inout float4 output)
{
    float normal_offset = view.z * normal.x - (view.x * normal.z);
    normal_offset = 0.0f < normal_offset ? -1.0f : 1.0f;
    float rim_width = _ES_AvatarRimWidthScale * _ES_AvatarRimWidth;
    rim_width.x = normal_offset.x * rim_width.x;
    rim_width.x = rim_width.x * 0.0044;
    rim_width = UNITY_MATRIX_P[3][3] == 0 ? rim_width :  rim_width * 0.25;

    float org_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen.xy), screen);
    
    rim_width = rim_width / (org_depth );

    float2 depth_uv =  screen;
    depth_uv.x += rim_width;

    float rim_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depth_uv.xy), depth_uv);
  
    rim_depth = pow(max(rim_depth - org_depth, 0.00001), 1);
    rim_depth = smoothstep(0.0, 0.5, rim_depth);

    float dark = saturate(dot(normal, light));

    float3 rim_color = saturate(output.xyz) * 2;

    float3 front_color = _ES_AvatarFrontRimColor * _ES_AvatarFrontRimIntensity;
    float3 back_color = _ES_AvatarBackRimColor * _ES_AvatarBackRimIntensity;

    rim_color =  (rim_color * front_color) * dark;

    output.xyz = rim_color * rim_depth + output.xyz;
}

void shadow_area(
    in float ndotl, 
    in float2 vcol, 
    #if defined(TOON_LIGHTMAP_ON)
    in float2 lightmap, 
    in bool isHairAOMat, 
    #endif
    in float2 shadow_transitions, 
    in bool vface, 
    out float3 shadowarea)
{
    
    #if defined(TOON_LIGHTMAP_ON)
        float lm_ao = _UseLightMapColorAO ? (lightmap.y - 0.5f) : 0.0f;
        lm_ao = dot(((float2)lm_ao), abs((float2)lm_ao)) + 0.5f;
        float vc_ao = _UseVertexColorAO ? lm_ao * vcol.x : lm_ao;
    #else
        float vc_ao = (_UseVertexColorAO) ? vcol * 0.5 : 0.5;
    #endif

    float2 ao_check;
    ao_check.x = vc_ao < 0.05f;
    ao_check.y = vc_ao > 0.95f;

    vc_ao = (ndotl + vc_ao) * 0.5f;
    vc_ao = ao_check.y ? 1.0f : vc_ao;
    vc_ao = ao_check.x ? 0.0f : vc_ao;

    #ifndef FACE_MAP_NEW_ON
        float ao = vc_ao;

        if(vc_ao<_LightArea)
        {
            #if defined(SHADOW_RAMP_ON)
                ao.x = (_LightArea - ao) / _LightArea;
                shadowarea.x = ao;

                float ramp_width = _UseVertexRampWidth ? max(vcol.y + vcol.y, 0.00999f) * _ShadowRampWidth : _ShadowRampWidth;
                shadowarea.y = 1.0f - min(ao.x / ramp_width.x, 1.0);
            #else
                ao.x = (_LightArea - ao.x) / shadow_transitions.x;
                float tmp = ao.x>=1.0;
                ao = pow(ao.x = ao.x + 0.01, shadow_transitions.y);
                ao.x = (tmp) ? 1.0 : min(ao.x, 1.0);
                shadowarea.x = (_UseShadowTransition) ? ao.x : 1.0;
                shadowarea.y = 0.0;
                shadowarea.z = 0;
            #endif
        }
        else
        {
            shadowarea.x = 0;
            shadowarea.y = 1;
            shadowarea.z = 1;
        }
    #else
        vc_ao = ndotl;
        float ao = vc_ao;
        if(vc_ao<_LightArea)
        {
            ao.x = (_LightArea - ao) / _LightArea;
            shadowarea.x = ao;
            shadowarea.y = .0;
            shadowarea.z = ndotl;
        }
        else
        {
            shadowarea.x = 0;
            shadowarea.y = 1;
            shadowarea.z = 1;
        }
    #endif
    #if defined(FACE_MAP_NEW_ON)
        bool2 checka = float2(0.5f, 0.89f) > lightmap.x;
    #endif
    #if defined(BACK_FACE_ON)
        if(_BackFaceLighting && !vface) shadowarea.xyz = float3(1, shadowarea.y, 0);
    #endif
} 

void calc_specular(
    in int material_id,
    in float3 ndoth,
    in float2 lightmap,
    inout float4 color
    )
    {
        material_id = max(material_id - 1, 0);
        float4 s_color;
        float shininess; 
        float multi;
        get_specular(material_id, s_color, shininess, multi);
        s_color = (s_color * multi) * lightmap.x;
        float3 specular = (pow(max(ndoth, 0.0000001f), shininess));
        bool spec_area = specular > 1.0f - lightmap.y;
    
        specular = lerp(0, s_color, spec_area);
    


        color.xyz +=  specular.xyz;
}

#if defined(METAL_MAT)
    void metal_material(in float3 normal, in float ndoth, in float2 lightmap, in float3 shadowarea, inout float4 output)
    {
        // get sphere coords: 
        float3 coord = mul((float3x3)unity_WorldToCamera, normal);
        coord.x = coord.x * _MTMapTileScale;
        coord.xy = coord.xy * 0.5 + 0.5;

        float map = saturate(_MTMap.Sample(sampler_linear_repeat, coord.xy).x * _MTMapBrightness);
        float3 metal = lerp(_MTMapDarkColor, _MTMapLightColor, map);
        float3 colored = metal * output.xyz;

        // metal specular 
        float mt_spec = saturate(pow(max(ndoth, 0.001), _MTShininess) * _MTSpecularScale);
      
        bool isSharp = mt_spec > _MTSharpLayerOffset;
        float3 spec_color;
        if(_MTUseSpecularRamp)
        {
            float2 ramp_coord = float2(mt_spec, 0.5f);
            float3 ramp       = (_MTSpecularRamp.Sample(sampler_linear_repeat, ramp_coord) * lightmap.y);
            spec_color        = ramp;
        }
        else
        {
            spec_color = lightmap.y * (mt_spec * _MTSpecularColor);
        }
        spec_color = isSharp ?  _MTSharpLayerColor.xyz  : spec_color;


        float3 spec_shadow = lerp(1, _MTShadowMultiColor, shadowarea.x);


        output.xyz = colored * spec_shadow + spec_color;
    }
#endif

// color.w contains the emission mask 
void emission_setting(in int material_id, in float mask, inout float3 color)
{
    material_id = max(material_id - 1, 0);
    float4 emis_color;
    float scalar;
    get_emission(material_id, emis_color, scalar);
    emis_color = (emis_color * scalar) * (_EmissionColor_MHY * _EmissionScaler);
    color = lerp(color, emis_color * color, mask);
}

void get_hit(in float3 normal, in float3 view, in float emission_mask, inout float4 output)
{
    float ndotv = pow(max(0.000001f, 1 - dot(normal, view)), _HitColorFresnelPower);
    float3 hit = ((max(_ElementRimColor.xyz, _HitColor.xyz) * ndotv) * _HitColorScaler) + output.xyz;
    float strength = lerp(emission_mask, 1, _EmissionStrengthLerp);

    output.xyz = (strength > 0.01f) ? lerp(output.xyz, hit, strength) : output;
}

#if defined(MATERIAL_MASK)
void material_mask(in float2 uv, inout float4 output)
{
    // sample mask tex
    float4 tex =_MaterialMasksTex.Sample(sampler_linear_repeat, uv * _MaterialMasksTex_ST.xy + _MaterialMasksTex_ST.zw);
    tex *= float4(_UseMaterial2, _UseMaterial3, _UseMaterial4, _UseMaterial5);

    float4 color = lerp(_Color, _Color2, tex.x);
    color = lerp(color, _Color3, tex.y);
    color = lerp(color, _Color4, tex.z);
    color = lerp(color, _Color5, tex.w);

    output.xyz = output.xyz * color;
}
#endif

// after the 6.0 update this was removed, im leaving the code in here in case they ever use it again
// that way we can quickly reenable it if the logic has stayed the same
// #if defined(ENABLE_TEXTURE_LINE_ON)
//     void detail_line(float2 sspos, float sdf, inout float3 diffuse)
//     {
//         float3 line_color = (_TextureLineMultiplier.xyz * diffuse.xyz - diffuse.xyz) * _TextureLineMultiplier.www;
//         float line_dist = LinearEyeDepth(sspos.x / sspos); // this may need to be replaced with the version that works for mirrors, will wait for feedback    
//         float line_thick = _TextureLineDistanceControl.x * line_dist + _TextureLineThickness;
//         line_thick = 1.0f - min(line_thick, min(_TextureLineDistanceControl.y, 0.99f)); 
//         line_dist = (line_dist > _TextureLineDistanceControl.z) ? 1.0f : 0.0f;
//         line_thick = 1.0f - line_thick;

//         float line_smooth = -_TextureLineSmoothness * line_dist + line_thick;
//         line_dist = _TextureLineSmoothness * line_dist + line_thick;
//         lines = smoothstep(line_smooth, line_dist, sdf);
//         diffuse.xyz = lines * line_color + diffuse.xyz;
//     }
// #endif

#if defined(is_paimon)
    void star_cloak(in float2 uv, in float3 parallax, inout float4 color)
    {
        float mask = _MainTex.Sample(sampler_linear_repeat, uv).w;
        float4 uv_set_A;
        float4 uv_set_B;
        float4 uv_set_C;
        float4 uv_set_D;
        float4 heights;
    
        uv_set_A.xy = uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
        uv_set_A.zw = uv.xy * _ColorPaletteTex_ST.xy + _ColorPaletteTex_ST.zw;
        uv_set_B.xy = uv.xy * _StarTex_ST.xy + _StarTex_ST.zw;
        uv_set_B.zw = uv.xy * _Star02Tex_ST.xy + _Star02Tex_ST.zw;
        uv_set_C.xy = uv.xy * _NoiseTex01_ST.xy + _NoiseTex01_ST.zw;
        uv_set_C.zw = uv.xy * _NoiseTex02_ST.xy + _NoiseTex02_ST.zw;
        uv_set_D.xy = uv.xy * _ConstellationTex_ST.xy + _ConstellationTex_ST.zw;
        uv_set_D.zw = uv.xy * _CloudTex_ST.xy + _CloudTex_ST.zw;
        heights.x = _StarHeight - 1.0;
        heights.y = _Star02Height - 1.0;
        heights.z = _ConstellationHeight - 1.0;
        heights.w = _CloudHeight - 1.0;
    
        float4 parallax_norm = normalize(parallax).xyxy;

        // Sample main texture
        float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv_set_A.xy);

        // Color palette sampling
        float2 palette_uv = float2(_Time.y * _ColorPalletteSpeed + uv_set_A.z, uv_set_A.w);
        float3 palette_color = _ColorPaletteTex.Sample(sampler_linear_repeat, palette_uv).xyz;
    
    
        // Star 01 sampling with parallax offset
        float time_offset_1 = _Time.y * _Star01Speed;
        float2 star01_uv = parallax_norm.xy * (heights.x * -0.1) + float2(uv_set_B.x, time_offset_1 + uv_set_B.y);
        float star01_val = _StarTex.Sample(sampler_linear_repeat, star01_uv).x;

        // Star 02 sampling with parallax offset
        float2 star02_uv = parallax_norm.zw * (heights.y * -0.1) + float2(uv_set_B.z, time_offset_1 * 0.5 + uv_set_B.w);
        float star02_val = _Star02Tex.Sample(sampler_linear_repeat, star02_uv).y;
        float star_combined = ((star01_val + star02_val) * mask) * _StarBrightness;
    
        float3 stars = star_combined * palette_color;


        // Noise sampling
        float2 noise01_uv = _Time.yy * _Noise01Speed + uv_set_C.xy;
        float2 noise02_uv = _Time.yy * _Noise02Speed + uv_set_C.zw;
        float noise_val = _NoiseTex01.Sample(sampler_linear_repeat, noise01_uv).x * _NoiseTex02.Sample(sampler_linear_repeat, noise02_uv).x;

        // Constellation sampling with noise offset
        float2 const_uv = parallax_norm.xy * (heights.z * -0.1) + uv_set_D.xy;
        float3 const_color = _ConstellationTex.Sample(sampler_linear_repeat, const_uv).xyz;

        // Cloud sampling with noise offset
        float2 cloud_uv = noise_val * _Noise03Brightness + uv_set_D.zw;
        cloud_uv = parallax_norm.zw * (heights.w * -0.1) + cloud_uv;
        float cloud_val = (_CloudTex.Sample(sampler_linear_repeat, cloud_uv).x * mask) * _CloudBrightness;

        // Composite colors
        float3 result = palette_color * star_combined;
        result = noise_val * result;
        result = cloud_val * palette_color + (color.xyz + result);
    
        color.xyz = result;
    }
#endif

#if defined(is_asmoday)
    void asm_cloak(in float2 uv, in float2 uv2, float is_emis, float emis_mask, inout float4 color)
    {
        float2 noise_uv = uv2 * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
        noise_uv = _Time.yy * _NoiseSpeed.xy + noise_uv;
        
        float noise = _NoiseMap.Sample(sampler_linear_repeat, noise_uv).x;
    
    
        float2 flow_uv = _Time.yy * _FlowMaskSpeed.xy + (noise * _NoiseScale + (uv2.xy * _FlowMap_ST.xy + _FlowMap_ST.zw));
        float2 flow02_uv = _Time.yy * _FlowMask02Speed.xy + (uv2.xy * _FlowMap02_ST.xy + _FlowMap02_ST.zw);
        float2 mask_uv = uv.xy * _FlowMask_ST.xy + _FlowMask_ST.zw;
    
        float3 bottom = saturate(pow(max(uv2.y, 0.00001f),_BottomPower) * _BottomScale);
        float3 bottom_color = lerp(_BottomColor01, _BottomColor02, bottom.xxx);
    
        float3 flow_color = _FlowColor.xyz * _FlowScale;
        float flow = _FlowMap.Sample(sampler_linear_repeat, flow_uv).x;
        float flow02 = _FlowMap02.Sample(sampler_linear_repeat, flow02_uv).x;
        float flows = flow + flow02;
        flow_color = flow_color * flows;
    
        float flow_mask = saturate(pow(max(uv2.y, 0.00001f),_FlowMaskPower) * _FlowMaskScale);
        flow_color = flow_color * flow_mask;
    
        float mask = _FlowMask.Sample(sampler_linear_repeat, mask_uv).x;
        float3 tmp = flow_color * mask + bottom_color;
        if (is_emis)
        {
            color.xyz = lerp(color, tmp, emis_mask);
        }
    }
#endif
