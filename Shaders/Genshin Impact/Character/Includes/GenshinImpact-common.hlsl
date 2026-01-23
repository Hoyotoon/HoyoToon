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

void npc_eye(in float2 uv)
{
    float index = uv.y;
    int count = -(_NPCMultiEyeSize * 0.1);
    float V = saturate(floor(1.0f -index) - floor(count + 0.5));
    if(_UseNPCMultiEye)
    {
        
        clip(V - 0.1);
    }
    else
    {
        clip((1 - V) - 0.1);
    }
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

void get_specular(in int material_id, out float4 color, out float shininess, out float multi, out float opacity)
{
    float4 tmp[5] = 
    {
        _SpecularColor, 
        _SpecularColor2, 
        _SpecularColor3, 
        _SpecularColor4, 
        _SpecularColor5
    };

    float3 value[5] =
    {
        float3(_Shininess, _SpecMulti, _SpecOpacity),
        float3(_Shininess2, _SpecMulti2, _SpecOpacity2),
        float3(_Shininess3, _SpecMulti3, _SpecOpacity3),
        float3(_Shininess4, _SpecMulti4, _SpecOpacity4),
        float3(_Shininess5, _SpecMulti5, _SpecOpacity5),
    };

    color = tmp[material_id];
    shininess = value[material_id].x;
    multi = value[material_id].y;
    opacity = value[material_id].z;
}

void get_outline(in int material_id, out float4 color, out float intensity)
{
    float4 tmp[5] =
    {
        _OutlineColor,
        _OutlineColor2,
        _OutlineColor3,
        _OutlineColor4,
        _OutlineColor5,
    };

    float inten[5] = 
    {
        _OutLineIntensity,
        _OutLineIntensity2,
        _OutLineIntensity3,
        _OutLineIntensity4,
        _OutLineIntensity5,
    };

    color = tmp[material_id];
    intensity = inten[material_id];
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
    void get_shadowramp(in int material_id, in float shadowarea, out float4 warm, inout float4 cool)
    {
        float3 coord = shadowarea;
        if(_UseCoolShadowColorOrTex){
            coord.y = 1.0f - saturate(((float)material_id - 1) * 0.1 + 0.05) ;
            coord.z = 1.0f - saturate(((float)material_id - 1) * 0.1 + 0.55) ;
            warm = _PackedShadowRampTex.Sample(sampler_linear_clamp, coord.xy);
            cool = _PackedShadowRampTex.Sample(sampler_linear_clamp, coord.xz);    
        } else {
            coord.y = 1.f - (1.0f - material_id) * 0.1 + 0.05;
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

void normal_map(inout float3 normal, in float3 view, in float2 uv, float2 normalmap , out float3x3 tbn )
{

#if defined(BUMP_TEXTURELINE_MAP)
    float2 bump;
    float bump_z;
    #if defined(ENABLE_PACK_NORMAL_ON)
        if(_isNativeMainNormal)
        {
            float2 unpack_normal = normalmap.xy * 2 - 1;

            float z_recalculated = sqrt(1.0 - min(dot(normalmap.xy, normalmap.xy), 1.0));
            float base_z = max(1.0 - _BumpScale, 0.00100000005);

            // Create the pre-normalized vector (X, Y are unpacked, Z is scale-based)
            float3 pre_normalized_vector;
            pre_normalized_vector.z = base_z;
            pre_normalized_vector.xy = unpack_normal;

            // Normalize the entire vector
            float inv_length = rsqrt(dot(pre_normalized_vector.xyz, pre_normalized_vector.xyz));
            float2 normalized_n_xy = inv_length * pre_normalized_vector.xy;
            float normalized_n_z = inv_length * pre_normalized_vector.z;

            bump.xy = (_DummyFixedForNormal) ? unpack_normal * _BumpScale : normalized_n_xy;
            bump_z = (_DummyFixedForNormal) ? z_recalculated : normalized_n_z;
        }
        else
        {
            float2 normal_uv = uv + _NormalMapOffset;
            float3 normal_a = _NormalPackageMap.Sample(sampler_linear_repeat, normal_uv);
            float3 normal_b = _NormalPackageMap.Sample(sampler_linear_repeat, normal_uv);
            float3 normal_c = _NormalPackageMap.Sample(sampler_linear_repeat, normal_uv);

            float3 combo_a = normal_c - normal_a;
            float3 combo_b = normal_b - normal_a;

            bool3 check = _NormalMapLayerEnum.xxx == float3(2,1,2);
            float3 map; 
            map.x = (check.y) ? combo_a.y : combo_a.x;
            map.x = (check.x) ? combo_a.z : map.x;
            map.y = (check.y) ? combo_b.y : combo_b.x;
            map.y = (check.x) ? combo_b.z : map.y;
            map.xy = map.xy * _NormalMapScale;
            map.z = 0;
            map.xyz = normalize(float3(0,0,1) - map);
            bump = map.xy;
            bump_z = map.z;
        }
    #else
        float2 T_minus_half = normalmap - 0.5;
        float2 T_squared_abs = abs(T_minus_half) * T_minus_half; 

        float2 smooth = T_squared_abs * 2.0 + 0.5;
        smooth = smooth - normalmap;

        float factor = _UseMobileBumpCompressSmooth ? 0.5 : 0.0; 
        float2 bumpA = smooth * factor + normalmap;
        
        float2 normalA = bumpA * 2.0 - 1.0;
        normalA *= _BumpScale;;
        
        float normalA_z = sqrt(1.0 - min(dot(bumpA, bumpA), 1.0)); 

        float2 norm_unscaled = normalmap * 2.0 - 1.0; 
        float min_bump = max(1.0 - _BumpScale, 0.001); // u_xlat3.z

        float3 norm_vec = float3(norm_unscaled, min_bump);
        float InvMag = rsqrt(dot(norm_vec, norm_vec)); 

        float2 normalB = norm_unscaled * InvMag;
        float normalB_z = InvMag * min_bump; 
        
        bump = _DummyFixedForNormal ? normalA : normalB; 
        bump_z = _DummyFixedForNormal ? normalA_z : normalB_z;

    #endif
#endif

    float3 dPdx = ddx(view.yzx); // spec_uv.xyz
    float3 dPdy = ddy(view.zxy); // output.xyz
    float2 dUVdx = ddx(uv);          // color_tmpa.xy
    float2 dUVdy = ddy(uv);          // u_xlat33.xy


    float3 normal_yzx = normal.yzx;
    float3 normal_zxy = normal.zxy;

    float3 Temp8_DyP = normal_yzx * dPdy; 
    float3 FinalDyP = dPdy.zxy * normal_zxy - Temp8_DyP;

    float3 FinalDxP = normal_yzx * dPdx.yzx - (normal_zxy * dPdx);
    float3 tangent = FinalDyP * dUVdx.x + FinalDxP * dUVdy.x;

    float3 bitangent = FinalDyP * dUVdx.y + FinalDxP * dUVdy.y;

    float tangent_length = dot(tangent, tangent);
    float bitangent_length = dot(bitangent, bitangent);

    float max_length = 1.0 / (sqrt(max(tangent_length, bitangent_length)));
    float3 tmp_tangent = tangent * max_length;
    float final_mag = rsqrt(max(dot(tmp_tangent, tmp_tangent), 0.001)); 

    tangent = tmp_tangent * final_mag; 
    float3 tmp_bitangent = bitangent * max_length;

    bitangent = tmp_bitangent * rsqrt(max(dot(tmp_bitangent, tmp_bitangent), 0.001));
    tbn = float3x3(tangent, bitangent, normal);
    
    #if defined(BUMP_TEXTURELINE_MAP)
        float4 tmp;
        tmp.xyw = bump.yyy * bitangent.xyz;
        tmp.xyw = bump.xxx * tangent.xyz + tmp.xyw;
        tmp.xyz = bump_z.xxx * normal.xyz + tmp.xyw;
        tmp.xyz = normalize(tmp);
        normal.xyz = (bool(bump_z <= 0.99f)) ? tmp.xyz : normal.xyz;
    #endif
}

#if defined(HAIR_MAP_ON)
    void determine_hairmap(in int material_id, out bool useMap, out int isMat)
    {
        isMat = ~(int(_SelectMatID > 0) ? 0xffffffffu : 0);
        isMat = int(uint(isMat) | (material_id == _SelectMatID)) * 0xffffffffu;

        useMap = _UseBumpAsAOMask ? isMat : 0; 
    }
#endif

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
    float scale = 1;
   
    #if defined(TOON_LIGHTMAP_ON)
    
    #if defined(HAIR_MAP_ON)
    if(isHairAOMat) scale = _AOShadowWarpScale; 
    #endif  
    float lightmap_ao =lerp(lightmap.y, 0.5f, 0.001f>=_UseLightMapColorAO) - 0.5f;
    // float lightmap_ao = _UseLightMapColorAO ? lightmap.y - 0.5f : 0.0f;
    lightmap_ao = (dot(lightmap_ao.xx, abs(lightmap_ao.xx))) + 0.5f;
    float vc_ao = (_UseVertexColorAO) ? lightmap_ao * vcol : lightmap_ao;
    #else
    float vc_ao = (_UseVertexColorAO) ? vcol * 0.5 : 0.5;
    #endif

    float2 ao_check;
    ao_check.x = vc_ao < 0.05f;
    ao_check.y = vc_ao > 0.95f;

    vc_ao = (vc_ao + ndotl) * 0.5f;
    vc_ao = ao_check.y ? 1.0f : vc_ao;
    vc_ao = ao_check.x ? 0.0f : vc_ao;

    #ifndef FACE_MAP_NEW_ON
        float ao = vc_ao;

        if(vc_ao<_LightArea)
        {
            #if defined(SHADOW_RAMP_ON)
                ao.x = (_LightArea - ao) / _LightArea;
                shadowarea.x = ao;

                float ramp_width = _UseVertexRampWidth ? max(vcol.y + vcol.y, 0.01f) * _ShadowRampWidth : _ShadowRampWidth;
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
        float opacity;
        //  get values
        get_specular(material_id, s_color, shininess, multi, opacity);
        s_color = (s_color * multi) * lightmap.x;
        opacity = min(opacity, 1);
        float3 specular = (pow(max(ndoth, 0.0000001f), shininess));
        bool check = specular > (1.-lightmap.y);
        specular = lerp(1, color *specular , opacity); 
        specular = (specular) * s_color;
        specular = lerp(0, specular, check);


        color.xyz = color.xyz + specular.xyz;
}

#if defined(ENABLE_CHARACTER_NBRBASE_ON)
    void nbr_base_material(in float3 normal, in float3 light, in float3 view, in float ao, in float lightz, in float4 diffuse,  inout float4 output)
    {
        // get ndoth 
        float3 hv = normalize(light + view);
        float ndoth = saturate(dot(normal, hv));

        float roughness = pow(lightz * _NbrRoughness, 2.0f);

        ndoth = pow((ndoth * roughness - ndoth) * ndoth + 1.0f, 2.0f);
        ndoth = (roughness / max(ndoth, 0.00001f)) * 0.318f; // specular with energy conservation
        
        float3 color = lerp(diffuse, _NbrBaseColor.xyz, length(_NbrBaseColor.xyz));

        color = (color * ndoth) * _NbrScale;

        // matcap time:
        // reuse naming from metal matcap calc:
        float3 coord = mul((float3x3)unity_WorldToCamera, normal);
        coord.x = coord.x * _NbrRefTiling;
        coord.xy = coord.xy * 0.5 + 0.5;
        float blur = (-_NbrRefBlur * 0.7f + 1.7f) * _NbrRefBlur;

        float map = _NbrRefTex.SampleLevel(sampler_linear_repeat, coord.xy, blur).x * _NbrRefScale;

        float shad = saturate(dot(normal, light));

        color = color * shad + map;

        if(_NbrBaseAOByLightmapG) color = color * ao;

        output.xyz += color;
    }
#endif

#if defined(ENABLE_CHARACTER_NBRSPECULAR_ON)
    void nbr_specular_material(in float4 lightmap, in float3 normal, in float3 view, in float3 light, in float2 uv2, in bool facing, in float2 screen, in float shadow,  in float4 diffuse, inout float4 output)
    {
        // first things first, the specular portion:
        // this is essentially the same as the base version except greatly expanded
        bool something  = lightmap.x >= 0.5f;
        float scale = something ? _NbrSpecularDirScale : _NbrSpecularIntensity;

        float range = _NbrSpecularRange * 20.0;

        float roughness = lightmap.z * _NbrSpecularRoughness;
        float metal = lightmap.x * _NbrSpecularMetal;

        // get ndoth 
        float3 hv = normalize(light + view);
        float ndoth = (dot(normal, hv));

        range = pow(max(ndoth, 0.00001f), range);
        range = range >= 0.5f ? 1.0f : 0.0f;
        range = range * scale;
        float3 range_color = range * _NbrSpecularColor;

        float3 color = lerp(diffuse, _NbrMainSpecularColor.xyz, length(_NbrMainSpecularColor.xyz));

        ndoth = saturate(ndoth);

        ndoth = pow(((ndoth) * roughness - ndoth) * ndoth + 1.0f, 2.0f);
        ndoth = (roughness / max(ndoth, 0.00001f)) * 0.318f; // specular with energy conservation

        color = ((color * ndoth) * _NbrSpecularScale) * 3.14159274f;


        float ndotv = dot(normal, view);

        float3 matcap_color = 0.f;
        float3 ref_color    = 0.f;
        float3 final_plane  = 0.f;

        float isNbrS = 0;

        // this code is techincally unreachable unless forced on with a script:
        // if(_UseMatCapReflection)
        // {
        //     uint tmp = (uint(something) * 0xffffffff) | (facing ? 0xffffffff : 0);
        //     float  isNbrS = asfloat(tmp & 1065353216);
        //     // use _NbrParallaxTex for some weird parallax thing
        //     bool area = lightmap.w >= 0.8f;

        //     float ref_plane = pow(ndotv, _NbrReflectionPlaneRange);

        //     float2 plane = length(uv2 * 5.0f) * 2 - 1;

        //     float3 coord = mul((float3x3)unity_WorldToCamera, normal);
        //     coord.x = coord.x * _MatCapSpecularRefTiling;
        //     coord.xy = coord.xy * 0.5 + 0.5;

        //     float heightmap = _NbrParallaxTex.Sample(sampler_linear_repeat, coord.xy).w;
        //     float3 matcap = heightmap * _MatCapSpecularRefScale * _MatCapSpecularRefColor.xyz;
            
        //     float2 spec_scale = (tmp != 0) ? float2(_NbrSpecularRefScale, _NbrSpecularParallaxScale) : float2(1.0, 1.0);
        //     matcap *= spec_scale.x;
            
        //     float depth = screen.x / screen.y; // Depth value (normalized)
        //     float2 parallax_coord = depth * _NbrParallaxDepth + coord.xy;
            
        //     float2 parallax_time = float2(_Time.y * _NbrParallaxTilingOffset.z, _Time.y * _NbrParallaxTilingOffset.w);
        //     parallax_time = frac(parallax_time);
        //     float2 parallax_uv = parallax_coord * _NbrParallaxTilingOffset.xy + parallax_time;
            
        //     float parallax_detail = _NbrParallaxTex.Sample(sampler_linear_repeat, parallax_uv).x;
        //     float3 parallax_color = parallax_detail * _NbrParallaxColor.xyz;
            
        //     float2 noise_offset = float2(_Time.y * _NbrNoiseTilingOffset.z, _Time.y * _NbrNoiseTilingOffset.w);
        //     noise_offset = frac(noise_offset);
        //     float2 noise_uv = parallax_coord * _NbrNoiseTilingOffset.xy + noise_offset;
            
        //     float3 noise = _NbrParallaxTex.Sample(sampler_linear_repeat, noise_uv).y * _NbrNoiseColor.xyz;
            
        //     float curvatureValue = (_NbrParallaxTex.Sample(sampler_linear_repeat, coord.xy).z) * _NbrCurvatureScale;
            
        //     float3 combined_parallax = parallax_color * _NbrParallaxScale + curvatureValue;
        //     combined_parallax += noise * _NbrNoiseScale;
        //     float3 effect = combined_parallax * spec_scale.y;
            
        //     float3 reflectionContribution = effect * lightmap.w;
            
        //     float2 plane_uv = plane * uv2;
        //     float cameraDist = length(_WorldSpaceCameraPos.xyz * 0.03);
        //     plane_uv = plane_uv * _NbrNoiseTilingOffset.xy + cameraDist.x;
            
        //     float reflection_plane = _NbrParallaxTex.Sample(sampler_linear_repeat, plane_uv).y;
            
        //     float3 contribution = (reflection_plane * _NbrReflectionPlaneScale) * ((ref_plane * plane.x) * _NbrReflectionPlaneColor.xyz);
            
        //     // Combine all contributions
        //     matcap_color = matcap;
        //     ref_color    = effect;
        //     final_plane  = contribution;
        // }
        // else
        // {
            isNbrS = something ? 1 : 0;
            // use the cubemap: 
            if(_NbrSpecularIblScale != 0.0f)
            {
                // assuming for now its a custom reflection vector calc: 
                float3 ref_vec = view * (-(dot(-normal, -view) * 2)) + normal;

                float level = (((-_NbrSpecularIblBlur) * 0.7f + 1.7f) * _NbrSpecularIblBlur) * 6.0f;

                float3 cube = _NbrSpecularIbl.SampleLevel(sampler_linear_repeat, ref_vec, level);

                float3 diff = (cube * saturate(shadow)) * 0.45f;

                cube = cube * 0.550000012 + diff.xyz;
                cube = cube * _NbrSpecularIblScale;
                cube = cube * _NbrSpecularIblColor.xyz;
                cube = cube * ((-lightmap.z) * _NbrSpecularRoughness + 1.0) * metal;

                matcap_color = cube;
            
            }
            else
            {
                matcap_color = 0.0f;
            }
        // }


        isNbrS = isNbrS != 0 ? 1 : 0;

        float3 spec = isNbrS * color;
        spec = spec * shadow + range_color;
        spec = spec + matcap_color;
        spec = spec + ref_color;
        spec = spec + (facing * final_plane);

        spec = (lightmap.w < 0.6f) && (lightmap.w >= 0.4f) ? spec : 0.0f;

        output.xyz += spec;
    }

#endif

#if defined(METAL_MAT)
    void metal_material(in float3 normal, in float ndoth, in float indoth, in float2 lightmap, in float3 shadowarea, inout float4 output)
    {
        // get sphere coords: 
        float3 coord = mul((float3x3)unity_WorldToCamera, normal);
        coord.y = coord.y + _MTSpecularOffset;
        coord = normalize(coord);
        coord.x = coord.x * _MTMapTileScale;
        coord.xy = coord.xy * 0.5 + 0.5;

        float map = saturate(_MTMap.Sample(sampler_linear_repeat, coord.xy).x * _MTMapBrightness);
        float3 metal = lerp(_MTMapDarkColor, _MTMapLightColor, map);
        float3 colored = metal * output.xyz;

        // metal specular 
        float mt_spec = saturate(pow(max(ndoth, 0.001), _MTShininess) * _MTSpecularScale);
        // specular in shadow
        float inv_spec = saturate(pow(max(indoth, 0.001), _MTShininess * 10)) * _MTSpecularShadowScale;

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

        spec_color = spec_color + (inv_spec * lightmap.y); 

        float3 spec_shadow = lerp(1, _MTShadowMultiColor, shadowarea.x);

        if(_MTSpecularAO){ spec_shadow = spec_shadow * shadowarea.z; }

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
    #if defined(is_weapon_or_glass)
        emis_color = (scalar) * (_EmissionColor_MHY * _EmissionScaler);
        color = lerp(color, emis_color * color, mask);
    #else
        emis_color = (emis_color * scalar) * (_EmissionColor_MHY * _EmissionScaler);
        color = lerp(color, emis_color * color, mask);
    #endif
}

void get_hit(in float3 normal, in float3 view, in float emission_mask, inout float4 output)
{
    float ndotv = pow(max(0.000001f, 1 - dot(normal, view)), _HitColorFresnelPower);
    float3 hit = ((max(_ElementRimColor.xyz, _HitColor.xyz) * ndotv) * _HitColorScaler) + output.xyz;
    float strength = lerp(emission_mask, 1, _EmissionStrengthLerp);

    output.xyz = (strength > 0.01f) ? lerp(output.xyz, hit, strength) : output;
}

#if defined(is_facialuv)
    void facialuv_vertex(inout float3 position, in float2 uv)
    {
        float3 tmp = lerp(0, position.xyz, 0.0500000007 < uv.x);
        float3 tmp2 = (0.0500000007 < uv.x) ? 0 : position.xyz;
        tmp2.xyz = (_FacialExpEnable) ? tmp.xyz : tmp2.xyz;
        position.xyz = (_FacialUVExpressionEnable) ? tmp2.xyz : position.xyz;
    }

    void facialuv_pixel(inout float3 color, in float2 uv)
    {
        if(_FacialExpEnable && _FacialUVExpressionEnable)
        {
            bool isLeftHalf = uv.x < 0.5;


            bool useRightParamsForLeftHalf = (_FacialExpMirror > 0.0);
            int2 mirrorFlags = int2(useRightParamsForLeftHalf, _FacialExpSplitIndex > 0.0);
            bool isMirrorEnabled = (mirrorFlags.x != 0);

            float horiFlip = (isLeftHalf) ? _FacialExpLeftHoriFlip : _FacialExpRightHoriFlip;

            horiFlip = (isMirrorEnabled) ? _FacialExpRightHoriFlip : horiFlip;
            bool shouldHoriFlip = (horiFlip != 0.0);

            float atlasIndex;
            if (isLeftHalf) {
                atlasIndex = (mirrorFlags.y != 0) ? _FacialExpLeftIndex : _FacialExpIndex;
            } else {
                atlasIndex = _FacialExpIndex;
            }
            uint expIndexU = uint(atlasIndex);
            
            float scale;
            float2 offset;
            float rotateAngle;
            float rotateSpeed;


            bool useLeftParameters = isLeftHalf && (!isMirrorEnabled);

            if (useLeftParameters) {
                scale = _FacialExpLeftScale;
                offset = float2(-_FacialExpLeftOffsetX, _FacialExpLeftOffsetY);
                rotateAngle = _FacialExpLeftRotateAngle;
                rotateSpeed = _FacialExpLeftRotateSpeed;
            } else {
                scale = _FacialExpRightScale;
                offset = float2(_FacialExpRightOffsetX, _FacialExpRightOffsetY);
                rotateAngle = _FacialExpRightRotateAngle;
                rotateSpeed = _FacialExpRightRotateSpeed;
            }



            uint atlasRows = uint(_FacialExpAtlasRows);
            float cell_size = 1.0 / float(atlasRows);

            // Calculate (Row, Col) of the expression index
            uint rowIndex = expIndexU / atlasRows; 
            uint colIndex = expIndexU % atlasRows; 


            uint flippedRowIndex = (atlasRows - 1) - rowIndex;
            float2 tileOffsetUV = float2(float(colIndex), float(flippedRowIndex)) * cell_size;


            float rotationRad = _Time.y * rotateSpeed + rotateAngle;
            float sinR = sin(rotationRad);
            float cosR = cos(rotationRad);


            float2 centeredUV = uv.xy - 0.5;
            centeredUV.y += offset.y; 

            if (isMirrorEnabled) {

                centeredUV.x = abs(centeredUV.x);
            }

            float2 rotatedUV_pre = centeredUV + float2(-0.5, -0.5);


            float2 rotatedUV;
            rotatedUV.x = dot(float2(cosR, sinR), rotatedUV_pre); 
            rotatedUV.y = dot(float2(-sinR, cosR), rotatedUV_pre); 

            // e) Apply Scaling
            float2 scaledUV = rotatedUV * scale;

            if (shouldHoriFlip) {
                scaledUV.x = -scaledUV.x;
            }

            float2 finalTileUV = scaledUV + float2(offset.x, 0.0);
            finalTileUV += 0.5;

            finalTileUV = clamp(finalTileUV, 0.0, 1.0);

            float2 finalAtlasUV = finalTileUV * cell_size + tileOffsetUV;

            float4 expressionTexel = _FacialExpAtlasTex.Sample(sampler_linear_repeat, finalAtlasUV);

            float3 expColor = expressionTexel.xyz;
            float alpha = expressionTexel.w;
            color.xyz = lerp(color.xyz, expColor, alpha);
        }
    }
#endif

#if defined(ENABLE_CHARACTER_STOCKINGS_ON)
    void stocking(in float3 normal, in float3 light, in float3 view, in float2 uv, in float lightmapz, in float lightmapx, inout float4 output)
    {
        // stocking specular
        // shift specular if needed
        float3 shifted_view = normalize((view + float3(0, _StockingsSpecularShift, 0))).xyz;
        float3 shifted_hvec = normalize(shifted_view + light);

        float ndoth = max(dot(normal, shifted_hvec), 0.000001f);
        float ndotv = max(dot(normal, shifted_view), 0.0001);
        float ndotl = saturate((dot(normal, shifted_view) * 0.5 + 0.5)* 5);

        float speca = min(pow(ndoth, _StockingsSpecularRange), 1.0f);
        speca = smoothstep(0.5f, _StockingsSpecularSharpe, speca) * _StockingsSpecularScale;
        float specb = min(pow(ndoth, _StockingsSpecularDetailRange), 1.0);
        specb = smoothstep(0.5f, _StockingsSpecularDetailSharpe, specb) * _StockingsSpecularDetailScale;

        float3 specular  = (speca * _StockingsSpecularColor + (specb * _StockingsSpecularDetailColor)) * lightmapx;

        float view_length = _StockingsSpecularDistance - length(view);
        view_length = saturate(pow(view_length, 2));
        float specular_fade = lerp(1, _StockingsSpecularFade, view_length);

        specular = specular * specular_fade;

        // // stocking shadow
        float shadow = 1 - min(pow(max(ndotv, 0.0001), _StockingsShadowRange), 1);

        float2 shadowranges = min(pow(ndotv.xx, float2(_StockingsSecondShadowRange, _StockingsLightRange)), 1);
        shadowranges.x = 1 - shadowranges.x;

        float secondshadow = lerp(shadowranges.x >= 0.5 ? _StockingsSecondShadowSoft : float(0.0), shadowranges.x, _StockingsSecondShadowSoft);

        shadow = _UseSecondFresnel ? secondshadow + shadow : shadow;

        float3 shadow_color = _UseSecondFresnel ? lerp(_StockingsShadowColor, _StockingsSecondShadowColor, secondshadow) : _StockingsShadowColor;
        
        float2 pattern_uv = uv * _StockingsDetailPattenTiling;
        float pattern_tex = _StockingsDetailTex.Sample(sampler_linear_repeat, pattern_uv).z;  
        float blend_tex = _StockingsDetailTex.Sample(sampler_linear_repeat, uv).w;


        float pattern = lerp(1.0f, pattern_tex, _StockingsDetailPattenScale);
        pattern = -blend_tex + pattern;
        pattern = saturate(pattern + 1.0f); 

        float3 pattern_color = lerp(_StockingsDetailPattenColor, 1.0f, pattern); 
        float s_light = (1-shadowranges.y) * (1.0 - lightmapz); 


        float3 stock_light = lerp(output.xyz, saturate((output.xyz * lerp(_StockingsLightDarkColor, _StockingsLightColor.xyz, shadow)) * (_StockingsLightScaleInShadow,_StockingsLightScale, shadow)), s_light * (1 - lightmapz)); // u_xlat40
        // stock_light = lerp(stock_light * _StockingsLightScaleInShadow)

        float3 something11 = lerp(pattern_color, (pattern_color * _StockingsLightDarkColor)* _StockingsLightScaleInShadow, 1 - lightmapz);
        float3 something44 = pattern_color * stock_light;

        // output.xyz = shadow_color; 
        if(_UseGradientStocking)
        {
            float2 gradientuv = saturate((0.5f - ( 1 - uv.xy)) * _StockingsGradientLength + 0.5);
            float2 uvtmpa = 1 - (gradientuv.xy);
            float2 uvtmpb = 1 - (gradientuv.xy * uvtmpa.xy);

            float gradientarea = (_StockingsGradientTypeChoose == 4) ? uvtmpa.y : 1.0;
            gradientarea       = (_StockingsGradientTypeChoose == 3) ? uvtmpb.y : gradientarea;
            gradientarea       = (_StockingsGradientTypeChoose == 2) ? uvtmpb.x : gradientarea;
            gradientarea       = (_StockingsGradientTypeChoose == 1) ? uvtmpa.x : gradientarea;
            gradientarea       = (_StockingsGradientTypeChoose == 0) ? gradientuv.x : gradientarea;

            float3 shadowramp = _StockingsShadowRamp.Sample(sampler_linear_repeat, uv.xy).xyz;
            float3 grad_color = lerp(_StockingsGradientLightColor, _StockingsGradientDarkColor, gradientarea) ;


            shadowramp = lerp(grad_color, shadowramp, _UseGradientStockingRampColor);
            something44 = lerp(something44, shadowramp, _StockingsGradientStrength);
            stock_light = lerp(something11, shadowramp, _StockingsGradientStrength);
        }
        else
        {
            stock_light = stock_light;
        }

        something44 = lerp(something44, something44 * shadow_color, shadow); 
        something44 = saturate(something44);
        shadow_color = lerp(stock_light, stock_light * shadow_color, shadow);
        shadow_color = saturate(shadow_color);

        // something11  = lerp(shadow_color, shadow_color * _ES_CharacterAmbientLightColor, _StockingAmbientColorStrength);
        
        output.xyz = saturate(shadow_color * output + something44) + specular;

    }   
#endif

#if defined(ENABLE_CHARACTER_SHINING_ON) 
    void shining(in float2 uv, in float3 view, in float mask, inout float4 output)
    {
        float vLength =  length(view);
        float dist = saturate(vLength / _ShiningMaxDistance);
        dist = saturate(dist / _ShiningFarNearBlend);

        float tiling = lerp((2.0 - _ShiningSizeNear) * _ShiningTiling, (2.0 - _ShiningSizeNear) * _ShiningTiling, dist);

        float radius = lerp(_ShiningSize * _ShiningSizeNear, _ShiningSizeFar * _ShiningSize, dist);
        float2 tile_a = tiling * uv.xy;
        float4 grid_index_a  = floor(tile_a.xyxy);
        tile_a = frac(tile_a);

        // --- Random Number Generation for Cell A (Per-Cell Random Offset) ---
        // This sequence generates a pseudo-random value (rand_offset_a.xy) based on the grid_index_a.
        float4 index_a_shifted = grid_index_a.zwzw + 0.1;
        index_a_shifted = index_a_shifted * float4(0.103100002, 0.103, 0.0973000005, 0.0973000005);
        float4 seed_a = frac(index_a_shifted);
        float4 seed_a_shifted = seed_a.wzxy + float4(33.3300018, 33.3300018, 33.3300018, 33.3300018);
        seed_a.xyz = (dot(seed_a, seed_a_shifted)) + seed_a.xyz;
        float2 rand_offset_a = seed_a.xx * seed_a.yy + float2(seed_a.z * 0.618309915, seed_a.y * 0.618309915);
        rand_offset_a = frac(rand_offset_a);
        rand_offset_a = rand_offset_a * 2.0 - 1.0; 
        
        float4 index_b_shifted = float4(grid_index_a.z * 0.0973000005, grid_index_a.w * 0.103, grid_index_a.z * 0.103100002, grid_index_a.w * 0.0973000005);
        float4 seed_b = frac(index_b_shifted);
        float4 seed_b_shifted = seed_b.wxzy + float4(33.3300018, 33.3300018, 33.3300018, 33.3300018);
        seed_b = dot(seed_b.zyxw, seed_b_shifted) + seed_b;
        float4 rand_b = seed_b.zzxx * seed_b.yyww + (seed_b * float4(0.618309915, 0.618309915, 0.618309915, 0.524999976));
        rand_b = frac(rand_b); 

        float2 toCenter = tile_a + (-rand_b.xy);
        float cell_a_dist = dot(toCenter, toCenter);

        float center_brightness = rand_b.z + 0.5; // (0.5 to 1.5)
        float cell_a_maxdist = radius * center_brightness;

        float cell_a_rawshine = cell_a_maxdist + (-cell_a_dist);
        cell_a_maxdist += 0.000001; // Epsilon for division
        cell_a_rawshine = saturate(cell_a_rawshine / cell_a_maxdist);

        float cell_a_density = (-rand_b.w) + 1.0; // (0.0 to 1.0)
        float cell_a_phase = cell_a_density * (1.0 / _ShiningDensity);
        float cLength = length(_WorldSpaceCameraPos);
        cell_a_phase += cLength;
        float time = _Time.y * _ShiningFrequencncy;
        float cell_a_input = cell_a_phase * 6.28318548 + time;
        float cell_a_factor = sin(cell_a_input);

        float cellA_ShineValue = cell_a_rawshine * cell_a_factor;
        float densityThreshold_Final = 1.0 - _ShiningDensity; 
        bool isCellA_Visible = rand_b.w >= densityThreshold_Final;
        float cellA_DensityCull = isCellA_Visible ? 1.0 : 0.0;
        cellA_ShineValue = cellA_ShineValue * cellA_DensityCull;
        cellA_ShineValue = max(cellA_ShineValue, 0.0);

        float4 grid_base_c = grid_index_a.xyxy + rand_offset_a.xyxy;
        float4 seed_c = frac(grid_base_c * float4(0.0973000005, 0.103, 0.103100002, 0.0973000005));
        float4 seed_c_shifted = seed_c.wxzy + float4(33.3300018, 33.3300018, 33.3300018, 33.3300018);
        seed_c = seed_c + dot(seed_c.zyxw, seed_c_shifted);
        float4 randPropC_temp = seed_c * float4(0.618309915, 0.618309915, 0.618309915, 0.524999976);
        float4 rand_c = seed_c.zzxx * seed_c.yyww + randPropC_temp;
        rand_c = frac(rand_c);

        float2 cellC_CenterOffset = rand_offset_a.xy + rand_c.xy; // Center pos is an accumulation of cell offset + random offset
        float2 cellC_VectorToCenter = tile_a + (-cellC_CenterOffset);
        float cellC_DistanceSq = dot(cellC_VectorToCenter, cellC_VectorToCenter);

        // Cell C: Determine max distance for falloff.
        float brightness_c = rand_c.z + 0.5; // (0.5 to 1.5)
        float max_distance_c = radius * brightness_c;

        float cellC_RawShineValue = max_distance_c + (-cellC_DistanceSq);
        max_distance_c += 0.000001; // Epsilon for division
        float shine_value_c = saturate(cellC_RawShineValue / max_distance_c);

        // Cell C: Apply time-based pulsation/animation factor.
        float cellC_DensityThreshold = 1.0 - rand_c.w; // (0.0 to 1.0)
        float cellC_AnimationPhaseOffset = cellC_DensityThreshold * (1.0 / _ShiningDensity);
        cellC_AnimationPhaseOffset += cLength;
        float cellC_AnimationInput = cellC_AnimationPhaseOffset * 6.28318548 + time;
        float cellC_AnimationFactor = sin(cellC_AnimationInput);
        shine_value_c = shine_value_c * cellC_AnimationFactor;

        shine_value_c = shine_value_c * (rand_c.w >= densityThreshold_Final);
        shine_value_c = max(shine_value_c, 0.0);

        float3 combined = cellA_ShineValue * rand_b.xyz + (rand_c.xyz * shine_value_c);

        combined = combined * (_ShiningIntensity * _ShiningColor.xyz);

        // Apply Color Blend (Desaturating towards the base _ShiningColor)
        float3 luminance = (dot(combined, float3(0.212500006, 0.715399981, 0.0720999986))) * _ShiningColor.xyz;
        float3 blendedShineColor = lerp(combined, luminance, _ShiningColorBlend);

        // --- Mask Application ---
        bool useMask = _ShiningNotUseMask < 1.0; 

        float3 final_color = _ShiningNotUseMask ?  mask * blendedShineColor : blendedShineColor ;
        output.xyz = output.xyz + final_color;
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

#if defined(ENABLE_CHARACTER_LEATHER_ON)
    void leather_mat(in float3 normal, in float3 view, in float3 light, in float4 lightmap, inout float4 output)
    {
        if(_UseMaterial5)
        {
            bool leather_area = (lightmap.w < 0.8f) && (lightmap.w > 0.6f);

            float3 shifted_view = normalize(view + float3(0,_LeatherSpecularShift,0));
            float3 shifted_hvec = normalize(view + light);

            float ndoth = max(dot(normal, shifted_hvec), 0.00000001f);

            float leather_spec = min(pow(ndoth, _LeatherSpecularRange), 1.0f);
            leather_spec = smoothstep(0.5f, _LeatherSpecularSharpe, leather_spec) * _LeatherSpecularScale;

            float detail_spec = min(pow(ndoth, _LeatherSpecularDetailRange), 1.0f);
            detail_spec = smoothstep(0.5f, _LeatherSpecularDetailSharpe, detail_spec) * _LeatherSpecularDetailScale;
            float3 detail_specular = detail_spec * _LeatherSpecularDetailColor;

            float ramp_coord = saturate(dot(normal, shifted_view) * -0.5 + 0.5);
            ramp_coord = ramp_coord * _LeatherLaserTiling + _LeatherLaserOffset;

            float3 leather_lazer = _LeatherLaserRamp.Sample(sampler_linear_repeat, ramp_coord).xyz * _LeatherLaserScale;

            float3 coord = mul((float3x3)unity_WorldToCamera, normal);
            coord.y = coord.y + _LeatherReflectOffset;
            coord = normalize(coord);
            coord.x = coord.x * _MTMapTileScale;
            coord.xy = coord.xy * 0.5 + 0.5;

            float3 leather = (_LeatherReflect.Sample(sampler_linear_repeat, coord.xy) * _LeatherReflectScale) * _FakePointReflection;
            float3 specular = leather_spec * _LeatherSpecularColor + detail_specular;

            leather =  max(specular, leather) * lightmap.z;
            output.xyz = leather_area ? leather + output.xyz : output;
        }   
    }
#endif

#if defined(MAIN_TEX_COLORING_ON)
    void main_tint(inout float4 output)
    {
        float3 color_tmpa = output.xyz * _MainTexTintColor.xyz;
        float3 color_tmpb = color_tmpa.xyz + color_tmpa.xyz;
        float3 color_tmpc = output.xyz + _MainTexTintColor.xyz;
        color_tmpc.xyz = color_tmpc.xyz + color_tmpc.xyz;
        color_tmpa.xyz = color_tmpa.xyz * -4 + color_tmpc.xyz;
        bool3 check = (float3)0.5 < color_tmpc.xyz;

        color_tmpa.xyz = color_tmpa.xyz - 1;
        output.xyz = check.xyz * color_tmpa.xyz + color_tmpb.xyz;
    }
#endif

#if defined(is_transparent)
    #if defined(ENABLE_FRESNEL_ON)
        void alpha_fresnel(in float3 normal, in float3 view, inout float4 output)
        {
            float ndotv = dot(normal, view);
            ndotv = max(1 - min(ndotv, 1), 0.0001);
            ndotv = pow(ndotv, _FresnelPower) * _FresnelScale;
            
            ndotv = _FresnelInvert ? 1 - ndotv : ndotv;

            float3 fresnel_color = _FresnelColorAdditive ? _FresnelColor + output.xyz : _FresnelColor * output.xyz;
            output.xyz =  lerp(output.xyz, fresnel_color, ndotv);

            // this super unused
            // if(_FresnelAlpha == 0)
            // {
            //     output.w = ndotv;
            // }
            // else if(_FresnelAlpha == 1)
            // {
            //     output.w += ndotv;
            // }
            // else if(_FresnelAlpha == 2)
            // {
            //     output.w *= ndotv;  
            // }
        }
    #endif

    void distance_fade(inout float4 color)
    {
        float camera_fade = 1.f;
        float3 head_view = normalize(_WorldSpaceCameraPos.xyz - _CharacterHeadCenterWorldPosition.xyz);
        float head_length = length(_WorldSpaceCameraPos.xyz - _CharacterHeadCenterWorldPosition.xyz);
        float3 center = cross(_CharacterFaceWorldDirection.xyz, _CharacterHeadCenterXDirWS.xyz);
        
        float3 head;
        head.x = dot(_CharacterFaceWorldDirection, head_view);
        head.y = dot(_CharacterHeadCenterXDirWS, head_view);
        head.z = dot(center, head_view);

        float head_tmp = normalize(head).y;

        float fade = smoothstep(_CameraDirFadeHorizontalMin, _CameraDirFadeHorizontalMax, head_tmp);
        float fade2 = smoothstep(0.0f, 0.3f, head_tmp);

        fade = lerp(_CameraDirFadeAlphaMin, _CameraDirFadeAlphaMax, fade);
        fade = lerp(fade, 1.0f, fade2);

        camera_fade = (_EnableCameraDirFade) ? fade : camera_fade;
        float trans_fade = _TransparentAlpha;

        float fade_tmp = saturate((head_length - _TransparentAlphaDistanceMin) / (_TransparentAlphaDistanceMax - _TransparentAlphaDistanceMin));
        fade_tmp = lerp(_TransparentAlphaDistanceAlphaMin, _TransparentAlphaDistanceAlphaMax, fade_tmp);

        float eye_tmp = saturate((head_length - _EyeTransDistanceMin) / (_EyeTransDistanceMax - _EyeTransDistanceMin));
        eye_tmp = lerp(_EyeTransDistanceAlphaMin, _EyeTransDistanceAlphaMax, eye_tmp);

        trans_fade = _EnableEyeTransDistFade ? eye_tmp : trans_fade;

        trans_fade = _EnableTransAlphaDistFade ? fade_tmp : trans_fade;

        camera_fade = trans_fade * camera_fade;
        color.w *= camera_fade;
    }
#endif

#if defined(is_facedecal)
    void face_decal(in float4 ws_pos, in float2 uv, inout float4 output)
    {
        float2 obj_space_xz = mul(ws_pos.xyz, (float3x3)unity_WorldToObject).xz;

        float2 mask_uv_s12 = (_CharacterFaceDecalMaskUVSwitch == 1) ? uv.xy : lerp(0, uv, _CharacterFaceDecalMaskUVSwitch == 2);

        obj_space_xz.x = (-obj_space_xz.x);

        float2 mask_uv_input = (_CharacterFaceDecalMaskUVSwitch == 0) ? obj_space_xz : mask_uv_s12;

        float2 mask_uv_signed = mask_uv_input.xy * 2.0 - 1.0;

        float mask_u = (_CharacterFaceDecalMask_MirrorU) ? abs(mask_uv_signed.x) : mask_uv_input.x;
        float mask_v = (_CharacterFaceDecalMask_MirrorV) ? abs(mask_uv_signed.y) : mask_uv_input.y;
        float2 final_mask_uv = float2(mask_u, mask_v);

        final_mask_uv = final_mask_uv * _CharacterFaceDecalMaskST.xy + _CharacterFaceDecalMaskST.zw;

        float4 decal_mask = _CharacterFaceDecalMask.Sample(sampler_linear_repeat, final_mask_uv);

        float channel_val = 0.0;
        channel_val = (_CharacterFaceDecalMaskChannelSwitch == 3) ? decal_mask.w : channel_val;
        channel_val = (_CharacterFaceDecalMaskChannelSwitch == 2) ? decal_mask.z : channel_val;
        channel_val = (_CharacterFaceDecalMaskChannelSwitch == 1) ? decal_mask.y : channel_val;
        channel_val = (_CharacterFaceDecalMaskChannelSwitch == 0) ? decal_mask.x : channel_val;

        float gradient_mask = channel_val + _CharacterFaceDecalMaskGradientOffset;
        gradient_mask = saturate(gradient_mask / _CharacterFaceDecalMaskGradientSoftness);

        float decal_opacity = gradient_mask * _CharacterFaceDecalOpacity;

        float skip_factor = (_CharacterFaceDecalSkipByLightmapG) ? 0.5 : 1.0;
        decal_opacity = decal_opacity * skip_factor;

        float3 base_col = output.xyz;
        float3 decal_col = _CharacterFaceDecalColor.xyz;

        float3 additive_blend = lerp(base_col, decal_col, decal_opacity);

        float3 multiply_blend = lerp(base_col, base_col * decal_col, decal_opacity);

        output.xyz = (_CharacterFaceDecalBlendMode) ? additive_blend : multiply_blend;
    }
#endif

#if defined(is_weapon_or_glass)
    void weapon_dissolve_nonstandard(in float2 uv1, in float3 view, inout float4 output)
    {
        float3 something; 
        something.x = dot(unity_ObjectToWorld[2].xyz, unity_ObjectToWorld[2].xyz);
        something.x = rsqrt(something.x);
        something.xyz = something.xxx * unity_ObjectToWorld[2].xyz;
        something.x = dot(view.xyz, something.xyz);

        float2 diss_uv = uv1 * _WeaponDissolveTex_ST.xy + _WeaponDissolveTex_ST.zw;
        float dissolve_rate;
        dissolve_rate = (_DissolveDirection_Toggle) ? 1 - diss_uv.y : diss_uv.y;
        dissolve_rate = _WeaponDissolveValue * 2.1f + dissolve_rate;
        diss_uv.y  = 1 - dissolve_rate;

        float2 dissolve_tex = _WeaponDissolveTex.Sample(sampler_linear_repeat, diss_uv);

        dissolve_rate = (dissolve_tex.x * output.w);
        dissolve_rate = _UsingDitherAlpha ? dissolve_rate * 0.15f : dissolve_rate;
        dissolve_rate = dissolve_rate * something.x;
        // output.w = dissolve_rate;

        float2 diss_tmp;    
        diss_tmp.y = dissolve_tex.y;
        diss_tmp.x = _WeaponDissolveValue + -0.25;
        diss_tmp.xy = diss_tmp.xy * float2(6.28f, 3.0);
        diss_tmp.x = sin(diss_tmp.x);
        diss_tmp.x = diss_tmp.x + 1.0;
        float2 pattern_uv = _Time.yy * _Pattern_Speed + (uv1.xy * _WeaponPatternTex_ST.xy + _WeaponPatternTex_ST.zw);
        float pattern = _WeaponPatternTex.Sample(sampler_linear_repeat, pattern_uv.xy).x;
        diss_tmp.x = pattern * diss_tmp.x;
        diss_tmp.x = diss_tmp.x * 0.5 + diss_tmp.y;
        output.xyz = output.xyz * _MainColorScaler + (diss_tmp.xxx * _WeaponPatternColor.xyz);
    }

    void weapon_dissolve(in float2 uv0, in float2 uv1, in float3 view, in float3 normal, in float emission_mask, inout float4 output)
    {

        float4 uv_set = float4(uv0, uv1);

        float dissolve_highlight = pow(max(1 - dot(normal, view), 0.000001f), 3.f); 
        float2 offset_uv = uv_set.zw * _WeaponPatternTex_ST.xy + _WeaponPatternTex_ST.zw;

        float uv_y = (_DissolveDirection_Toggle) ? 1 - uv_set.w : uv_set.w;
        uv_y = _WeaponDissolveValue * 2.1f + uv_y;

        float2 dissolve_uv;
        dissolve_uv.y = uv_y - 1.0;
        dissolve_uv.x = uv_set.z;
        float2 dissolve = _WeaponDissolveTex.Sample(sampler_linear_clamp, dissolve_uv.xy).xy; 

        offset_uv.xy = _Time.yy * _Pattern_Speed + offset_uv.xy;
        float pattern = _WeaponPatternTex.Sample(sampler_linear_repeat, offset_uv.xy).x; 
        dissolve_highlight.x = dissolve_highlight.x * 1.1f + pattern;
        dissolve_highlight.x = dissolve_highlight.x * (sin((_WeaponDissolveValue - 0.25f) * 6.28f) + 1.0f);

        dissolve_highlight.x = dissolve_highlight.x * 0.5f + (dissolve.y * 3.0f);

        float3 skill_emission = pow(max(1 - dot(view, normal), 0.000001f), _SkillEmisssionPower) * _SkillEmisssionColor;


        float2 scan_uv = uv_set.zw * _ScanPatternTex_ST.xy + _ScanPatternTex_ST.zw;
        scan_uv.y = _ScanDirection_Switch ? 1 - scan_uv.y : scan_uv.y;
        scan_uv.y = (_Time.y * _ScanSpeed) * 0.5f + scan_uv.y;
        float scan =  _ScanPatternTex.Sample(sampler_linear_repeat, scan_uv).x * _ScanColorScaler;

        float3 weapon_color = dissolve_highlight * _WeaponPatternColor.xyz + output.xyz; 
        weapon_color = skill_emission * _SkillEmissionScaler + weapon_color;
        weapon_color = scan * _ScanColor.xyz + weapon_color;

        float emissive_area = emission_mask + dissolve_highlight.x;
        emissive_area = skill_emission.x * _SkillEmissionScaler + emissive_area.x;
        emissive_area = scan.x * _ScanColor.x + emissive_area.x;
        emissive_area = saturate(emissive_area);


        output.xyz = (0.01f < emissive_area) ? lerp(output.xyz, weapon_color, emissive_area) : output.xyz;
        clip(dissolve.x - 0.001f);
    }

    void weapon_outline_dissolve(in float2 uv1)
    {
        float uv_y = (_DissolveDirection_Toggle) ? 1 - uv1.y : uv1.y;
        uv_y = _WeaponDissolveValue * 2.1f + uv_y;

        float2 dissolve_uv;
        dissolve_uv.y = uv_y - 1.0;
        dissolve_uv.x = uv1.x;
        float dissolve = _WeaponDissolveTex.Sample(sampler_linear_clamp, dissolve_uv.xy).x;

        bool checka = dissolve < 0.99f;
        dissolve -= 0.001f;
        bool checkb = dissolve < 0.0f;

        if(checka && checkb) discard;
    } 
#endif

#if defined(ENABLE_CHARACTER_GLASSSPECULAR_ON)
    void glass_specular(in float2 uv1, in float3 normal, in float3 view, inout float4 output)
    {
        float detail_length = saturate((uv1.y - _GlassSpecularDetailLength) / (max(_GlassSpecularDetailLengthRange, 0.000001)));

        float2 spec_uv = uv1.xy * _GlassSpecularTex_ST.xy;
        spec_uv.xy = spec_uv.xy * _GlassTiling + _GlassSpecularTex_ST.zw;
        spec_uv.xy = (_GlassSpecularOffset - 1.0) * view.xy + spec_uv.xy;
        float2 detail_uv = _GlassSpecularDetailOffset * float2(1.0, 0.0) + spec_uv.xy;
        float3 specular = _GlassSpecularTex.Sample(sampler_linear_repeat, spec_uv.xy).x;
        float3 specular_detail = _GlassSpecularTex.Sample(sampler_linear_repeat, detail_uv.xy).y;
        detail_length.x = detail_length.x * specular_detail;
        float spec_length = uv1.y - _GlasspecularLength;
        spec_length = saturate(spec_length / max(_GlasspecularLengthRange, 0.000001));

        specular = specular * spec_length;
        specular = specular * _GlassSpecularColor.xyz + (detail_length * _GlassSpecularDetailColor);
        float ndotv = dot(normal.xyz, view.xyz);
        ndotv = pow(1 - ndotv, _GlassThickness) * _GlassThicknessScale;
        float3 thickness = saturate(ndotv * _GlassThicknessColor.xyz);

        output.xyz += specular + thickness;
    }
#endif

// this some weird shit
void hair_transparency(inout float4 output, in float alpha_mask)
{
    float mask = 1.f;
    if(_UseHairAlphaLimitation)
    {
        float3 head_view = normalize(_WorldSpaceCameraPos.xyz - _CharacterHeadCenterWorldPosition.xyz);
        float3 center = cross(_CharacterFaceWorldDirection.xyz, _CharacterHeadCenterXDirWS.yzx);
        float cdotv = dot(center, head_view.xyz); // 1
        float fdotv = dot(_CharacterFaceWorldDirection.xyz, head_view); // 44
        float rdotv = dot(_CharacterHeadCenterXDirWS.yzx, head_view); // 3
        float tmpa = saturate(rdotv.x * _HairTransRemapVert.z + (-_HairTransRemapVert.w));

        float tmpb = saturate(abs(cdotv.x) * _HairTransRemapHori.z + (-_HairTransRemapHori.w));

        tmpb.x = tmpb.x * (1 - tmpa) + tmpa;

        tmpb.x = (0.0<fdotv) ? 1 - tmpb.x : 1.0;
        tmpa = (-_HairTransparentValue) + 1.0;
        tmpb.x = tmpb.x * tmpa + _HairTransparentValue;
        mask = tmpb.x;
    }
    else
    {
        mask = _HairTransparentValue;
    }

    output.w = _UseHairAlphaMask ? lerp(1, mask, alpha_mask) : mask;
    
}

void hair_shadow(inout float4 position)
{
    float4 pos = mul(unity_ObjectToWorld, float4(position.xyz, 1.0f));

    // weird hair shadow math: 
    float vs_light = normalize(mul(UNITY_MATRIX_V, float4(normalize(_WorldSpaceLightPos0.xyz), 1.0f)).xyz).y;
    float3 vs_head = _WorldSpaceCameraPos.xyz - _CharacterHeadCenterWorldPosition.xyz;
    float head_angle = 1 - saturate(dot(vs_head, _CharacterHeadCenterXDirWS.xyz) * _HairShadowVerticalRemap.z - _HairShadowVerticalRemap.w);

    float3 up = cross(_CharacterHeadCenterXDirWS.xyz, _CharacterFaceWorldDirection.xyz);

    float3 center = pos.xyz - _CharacterHeadCenterWorldPosition.xyz;

    float fdotc = dot(_CharacterFaceWorldDirection.xyz, center);
    float hdotc = dot(-_CharacterHeadCenterXDirWS.xyz, center);
    float udotc = dot(up, center) * _HairShadowExtrusion; 

    float3 head_dir = (_CharacterFaceWorldDirection.xyz * fdotc) + (hdotc * (-_CharacterHeadCenterXDirWS.xyz));

    center.xyz = up.xyz * udotc.xxx + head_dir;
    center.xyz = center.xyz + _CharacterHeadCenterWorldPosition.xyz;
    pos.xyz = center.xyz;


    pos = mul(UNITY_MATRIX_V, pos);

    vs_light.x = vs_light.x * _HairShadowLightShift + _HairShadowStencilShift.x;
    vs_light.x = (-vs_light.x) * 0.0045 + pos.x;
    pos.x = vs_light.x;
    head_angle.x = (_HairShadowStencilShift.y + 1.0) * head_angle.x;
    head_angle.x = (-head_angle.x) * 0.0075 + pos.y;
    pos.y = head_angle.x;
    pos = mul(UNITY_MATRIX_P, pos);

    position = pos;

}

#if defined(is_nyx)
void body_markings(in float2 screen, in float2 uv, inout float4 output)
{
    float3 tmp_uv;
    float4 screen_uv = float4(screen, screen.yx);

    float4 noise_scale = frac(_Time.yyyy * _NyxStateOutlineColorNoiseAnim.zwxy);
    screen_uv = screen_uv * _NyxStateOutlineColorNoiseScale.xyxy + noise_scale;
    float noise_tmp = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.zw).x;
    tmp_uv.xy = noise_tmp.xx * _NyxStateOutlineColorNoiseTurbulence + screen_uv.zw;
    tmp_uv.x = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, tmp_uv.xy);
    tmp_uv.y = float(0.75);
    tmp_uv.z = float(0.25);
    float3 color_ramp = _NyxStateOutlineColorRamp.SampleLevel(sampler_linear_clamp, tmp_uv.xy, 0.f).xyz * _NyxStateOutlineColorOnBodyMultiplier;
    tmp_uv.x = _CurTimeOfDay24 * 0.0416660011; // the full number is very important because it will make 24 = 1
    float3 time_color = _NyxStateOutlineColorRamp.SampleLevel(sampler_linear_clamp, tmp_uv.xz, 0.f).xyz;
    color_ramp = color_ramp * time_color;
    color_ramp = color_ramp * _NyxStateOutlineColorScale;

    //paint time
    float4 paint_mask = _TempNyxStatePaintMaskTex.Sample(sampler_linear_repeat, uv).xyzw;
    float mask = _TempNyxStatePaintMaskChannel == 3 ? paint_mask.w : 0.f;
    mask = _TempNyxStatePaintMaskChannel == 2 ? paint_mask.z : mask;
    mask = _TempNyxStatePaintMaskChannel == 1 ? paint_mask.y : mask;
    mask = _TempNyxStatePaintMaskChannel == 0 ? paint_mask.x : mask;

    mask = mask * _NyxStateOutlineColorOnBodyOpacity;

    output.xyz = lerp(output.xyz, color_ramp, mask);

}

void ns_outline_color(in float2 screen, inout float4 output)
{
    float3 tmp_uv;
    float4 screen_uv = float4(screen, screen.yx);

    float4 noise_scale = frac(_Time.yyyy * _NyxStateOutlineColorNoiseAnim.zwxy);
    screen_uv = screen_uv * _NyxStateOutlineColorNoiseScale.xyxy + noise_scale;
    float noise_tmp = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.zw).x;
    tmp_uv.xy = noise_tmp.xx * _NyxStateOutlineColorNoiseTurbulence + screen_uv.zw;
    tmp_uv.x = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, tmp_uv.xy);
    tmp_uv.y = float(0.75);
    tmp_uv.z = float(0.25);
    float3 color_ramp = _NyxStateOutlineColorRamp.SampleLevel(sampler_linear_clamp, tmp_uv.xy, 0.f).xyz * _NyxStateOutlineColorOnBodyMultiplier;
    tmp_uv.x = _CurTimeOfDay24 * 0.0416660011; // the full number is very important because it will make 24 = 1
    float3 time_color = _NyxStateOutlineColorRamp.SampleLevel(sampler_linear_clamp, tmp_uv.xz, 0.f).xyz;
    color_ramp = color_ramp * time_color;
    color_ramp = color_ramp * _NyxStateOutlineColorScale;

    output.xyz = color_ramp;
}
#endif

float select_channel(float4 tex_sample, int channel_index) {
    return tex_sample[clamp(channel_index, 0, 3)];
}

#if defined(ENABLE_CHARACTER_SKIRK_STAR_ON)
void skk_stars(in float2 screen, in float3 view, in float2 uv, inout float4 output)
{
    // 1. Setup Screen UVs for Star Projection
    float aspect_ratio = _ScreenParams.x / _ScreenParams.y;
    float2 centered_uv = screen.xy * 2.0 - 1.0; 
    float3 star_uv_base;
    star_uv_base.y = centered_uv.y;
    star_uv_base.x = centered_uv.x * aspect_ratio;

    // Calculate distance from camera to object to scale the stars
    float3 world_offset = _WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23;
    float2 final_star_uv = (length(world_offset) * star_uv_base.xy) * _StarTexST.xy + _StarTexST.zw;

    float2 scrolling_uv = frac(_Time.y * _StarTexSpeed.xy) + final_star_uv;
    float4 star_sample_1 = _StarTex.SampleLevel(sampler_linear_repeat, scrolling_uv, 0.0);
    float4 star_sample_2 = _StarTex.SampleLevel(sampler_linear_repeat, final_star_uv * 0.95 + 0.05, 0.0);

    float star_val_1 = select_channel(star_sample_1, (int)_StarTexChannelSwitch);
    float star_val_2 = select_channel(star_sample_2, (int)_StarTexChannelSwitch);

    
    bool2 star_flicker_mask = bool2(star_val_1 >= _StarFlickRange, star_val_2 >= _StarFlickRange);
    float star_flicker_final = (float)star_flicker_mask.x;

    if (_StarFlickToggle) {
        star_flicker_final = (float)star_flicker_mask.y * (float)star_flicker_mask.x;
    }
    
    float star_mask = 1.0 - _StarMask.Sample(sampler_linear_repeat, uv.xy).r;
    float3 star_base_color = star_val_1 * _StarColor.rgb;
    float3 star_final_color = (star_flicker_final * _StarFlickColor.rgb + star_base_color) * star_mask;

    float highlight_wave_input = (_WorldSpaceLightPos0.z + view.z) * _BlockHighlightViewWeight;
    float4 highlight_wave = frac(highlight_wave_input + float4(0.0, 0.2, 0.5, 0.8));
    highlight_wave = 1.0 - abs(highlight_wave * 2.0 - 1.0); // Triangle wave logic

    float4 highlight_factor = saturate((highlight_wave - _BlockHighlightRange + _BlockHighlightSoftness) / _BlockHighlightSoftness);
    float4 highlight_mask = _BlockHighlightMask.Sample(sampler_linear_repeat, uv.xy);

    // Sum up the highlight channels
    float total_highlight = dot(highlight_mask, highlight_factor); 
    float3 highlight_final_color = saturate(total_highlight) * _BlockHighlightColor.rgb;

    // 4. Bright Lines (Energy scrolling effect)
    float4 line_sample = _BrightLineTex.Sample(sampler_linear_repeat, uv.xy);
    float line_val = select_channel(line_sample, (int)_BrightLineTexChannelSwitch);

    float2 line_mask_uv = frac(_Time.y * _BrightLineMaskSpeed.xy) + uv.xy;
    float4 line_mask_sample = _BrightLineMask.Sample(sampler_linear_repeat, line_mask_uv);
    float line_mask_val = select_channel(line_mask_sample, (int)_BrightLineMaskChannelSwitch);

    // Apply contrast and color
    float line_contrast = pow(max(line_mask_val, 0.000001), _BrightLineMaskContrast);
    float3 line_final_color = line_val * (line_contrast * _BrightLineColor.rgb);

    // 5. Final Composition
    // Combine stars (masked), highlights, and scrolling lines
    float3 combined_layers = star_final_color + highlight_final_color;
    float3 final_effect = line_final_color + combined_layers;

    output.rgb += final_effect;
    
}

#endif
