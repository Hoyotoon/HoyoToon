vs_out vs_model(vs_in v)
{
    vs_out o;
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = pos_ws;
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    o.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent);
    o.tangent.w = v.tangent.w * unity_WorldTransformParams.w;
    o.view = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
    o.uv_a.xy = v.uv_0;
    o.uv_a.zw = v.uv_1;
    o.uv_b.xy = v.uv_2;
    o.uv_b.zw = v.uv_3;

    o.color = v.color;

    TRANSFER_SHADOW(o)

    return o;
}

float4 ps_model(vs_out i,  bool vface : SV_ISFRONTFACE) : SV_TARGET
{

    UNITY_LIGHT_ATTENUATION(atten, i, i.ws_pos.xyz);
    // initialize output color : 
    float4 color = (float4)1.0f;

    float4 face_color = vface ? _Color : _BackFaceColor;
    color = color * face_color;

    // initialize inputs : 
    float2 uv = (_UseVFaceSwitch2UV && !vface) ? i.uv_a.zw : i.uv_a.xy; // if enabled and back facing 
    float3 normal = normalize(i.normal);
    float3 tangent = normalize(i.tangent.xyz);
    float3 bitangent = cross(normal, tangent) * i.tangent.w;
    float3 view   = normalize(i.view);

    // lighting : 
    float3 light = _WorldSpaceLightPos0;
    #if defined(POINT) || defined(SPOT) 
        light = normalize(_WorldSpaceLightPos0.xyz - i.ws_pos.xyz);
        
    #endif

    // half vector 
    float3 half_vector = normalize(light + view);
    
    // sample main textures : 
    float4 diffuse = _MainTex.Sample(sampler_MainTex, uv * _MainTex_ST.xy + _MainTex_ST.zw);
    float4 bump    = _BumpMap.Sample(sampler_linear_repeat, uv);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv);
    float4 facemap = _FaceMapTex.Sample(sampler_linear_repeat, uv);
    float4 expmap = _FaceExpTex.Sample(sampler_linear_repeat, uv * _FaceExpTex_ST.xy + _FaceExpTex_ST.zw);

    // material region
    float region = material_region(lightmap.w);
    
    // bump mapping : 
    normal_online(bump, i.ws_pos, uv, normal, tangent, bitangent);
    bitangent = normalize(bitangent);

    // now the dot products : 
    float ndotl = dot(normal, light);
    float ndoth = saturate(dot(normal, half_vector));
    float ndotv = dot(normal, view);

    // shadow area and color bullshit
    float2 shaded_area = 1.0f; // initialize as 1 since the each variant has its own shadow area calculation
    float3 shadow_color = 1.0f;

    #if defined(use_shadow)
        if(variant_selector == 0)
        {
            shaded_area = (_EnableStocking && (lightmap.r <= 0.1)) ? shadow_area_body(1.0, ndotl) :shadow_area_body(lightmap.y, ndotl);
            shadow_color = (_EnableStocking && (lightmap.r <= 0.1)) ? shadow_base(dot(normal, light), lightmap.w,  lightmap.y) : shadow_base(shaded_area.x, lightmap.w, lightmap.y);
            shadow_color = saturate(shadow_color);
        }
        else if(variant_selector == 1 && _EnableFaceMap)
        {
            shaded_area = shadow_area_face(uv, light);
            shadow_color = shadow_base(shaded_area.x, lightmap.w, lightmap.y);
        }
        else if(variant_selector == 2)
        {
            shaded_area = shadow_area_hair(lightmap.y, ndotl);
            shadow_color = shadow_hair(shaded_area, lightmap.y);
        }
    #endif
    #ifdef _IS_PASS_BASE
        // color + alpha bullshit
        color.xyz = color.xyz * diffuse.xyz;
        if(!_UseMainTexAsEmission)
        {
            color.w = color.w * diffuse.w;
            clip(color.w - _CutOff);
        }
        color.w = color.w * _Opaqueness;

        float4 rim_colors = rg_color(lightmap.w);
        float3 rim = rim_glow(ndotv, rim_colors);

        float metal_area = ((lightmap.w + 0.1f) >= _MTMapThreshold);

        float3 specular = (float3)0.0f;
        float3 shadow   = shadow_color;

        float2 soft_range = _SpecSoftRange.xx + float2(0.0001f, 0.5f);
        float low_range = 0.5f - soft_range.x;
        
        if(_MTMapRampTexUsed && metal_area)
        {
            #if defined(use_metal)
                metal(normal, shaded_area.x, shadow_color, ndoth, lightmap.x, float2(low_range, soft_range.y), color, lightmap.w, specular, shadow);
            #endif
        }
        else
        {
            if(variant_selector == 2)
            {
                #if defined(use_specular) && defined(is_hair)
                    specular = hair_specular(normal, bitangent, light, view, uv, shaded_area.x);
                #endif
            }
            else
            {
                #if defined(use_specular)
                specular =  specular_regular(lightmap.w, shaded_area.xy, shadow_color, ndoth, lightmap.xz);
                #endif
            }
        }
        
        #if defined(use_shadow)
            shaded_area = shaded_area * -0.2f + 1.2f;
            shadow = shadow * shaded_area.x;
            color.xyz = color.xyz * shadow;
        #endif

        if(_EnableStocking && (!metal_area) && (lightmap.r <= 0.1))
        {
            color.xyz = tights(normal, light, view, bitangent, tangent, uv, lightmap, color.xyz);
        }

        color.xyz = color.xyz * float3(0.891f, 0.919f, 0.942f) + specular;

        #if defined(faceishadow)
            if(variant_selector == 1)
            {
                color.xyz = face_exp(uv, color);
            }
        #endif

        float emission_tex = _UseMainTexAsEmission ? diffuse.w : 1.0f;
        float3 emissive = (float3)0.0f;
        #if defined(use_emission)
            emissive = emission(lightmap.w, emission_tex, color.xyz);
        #endif
        #if defined(use_rimglow)
            if(_EnableRimGlow) emissive = rim + emissive;
        #endif
        color.xyz = color.xyz + emissive;

        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);
        color.xyz = light_color.xyz * color.xyz;

        float3 GI_color = DecodeLightProbe(normal);
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;

        color.xyz = color.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));

        if(_DebugMode) // debuuuuuug
        {
            if(_DebugDiffuse == 1) return float4(diffuse.xyz, 1.0f);
            if(_DebugDiffuse == 2) return float4(diffuse.www, 1.0f);
            if(_DebugLightMap == 1) return float4(lightmap.xxx, 1.0f);
            if(_DebugLightMap == 2) return float4(lightmap.yyy, 1.0f);
            if(_DebugLightMap == 3) return float4(lightmap.zzz, 1.0f);
            if(_DebugLightMap == 4) return float4(lightmap.www, 1.0f);
            #if defined(faceishadow)
            if(_DebugFaceMap == 1) return float4(facemap.xxx, 1.0f);
            if(_DebugFaceMap == 2) return float4(facemap.yyy, 1.0f);
            if(_DebugFaceMap == 3) return float4(facemap.zzz, 1.0f);
            if(_DebugFaceMap == 4) return float4(facemap.www, 1.0f);
            if(_DebugExpMap == 1) return float4(expmap.xxx, 1.0f);
            if(_DebugExpMap == 2) return float4(expmap.yyy, 1.0f);
            if(_DebugExpMap == 3) return float4(expmap.zzz, 1.0f);
            if(_DebugExpMap == 4) return float4(expmap.www, 1.0f);
            #endif
            if(_DebugNormalMap == 1) return float4(bump.xyz, 1.0f);
            if(_DebugVertexColor == 1) return float4(i.color.xxx, 1.0f);
            if(_DebugVertexColor == 2) return float4(i.color.yyy, 1.0f);
            if(_DebugVertexColor == 3) return float4(i.color.zzz, 1.0f);
            if(_DebugVertexColor == 4) return float4(i.color.www, 1.0f);
            if(_DebugVertexColor == 5) return float4(i.color.xyz, 1.0f);
            if(_DebugRimLight == 1) return float4(rim.xyz, 1.0f);
            if(_DebugNormalVector == 1) return float4(i.normal.xyz * 0.5f + 0.5f, 1.0f);
            if(_DebugNormalVector == 2) return float4(i.normal.xyz, 1.0f);
            if(_DebugNormalVector == 3) return float4(normal.xyz * 0.5f + 0.5f, 1.0f);
            if(_DebugNormalVector == 4) return float4(normal.xyz, 1.0f);
            if(_DebugTangent == 1) return float4(i.tangent.xyz, 1.0f);
            if(_DebugMetal == 1) return float4(color.xyz * ((lightmap.w + 0.1) >= _MTMapThreshold), 1.0f);
            if(_DebugSpecular == 1) return float4(specular.xyz, 1.0f);
            if(_DebugEmission == 1) return float4((emission_tex).xxx, 1.0f);
            if(_DebugEmission == 2) return float4(emissive.xyz, 1.0f);
            if(_DebugFaceVector == 1) return float4(UnityObjectToWorldDir(_headForwardVector.xyz), 1.0f);
            if(_DebugFaceVector == 2) return float4(UnityObjectToWorldDir(_headRightVector.xyz), 1.0f);
            if(_DebugFaceVector == 3) return float4(UnityObjectToWorldDir(_headUpVector.xyz), 1.0f);
    
            if((_DebugMaterialIDs > 0) && (_DebugMaterialIDs != 6))
            {
                if(_DebugMaterialIDs == region)
                {
                    return (float4)1.0f;
                }
                else 
                {
                    return float4((float3)0.0f, 1.0f);
                }
            }
            if(_DebugMaterialIDs == 6)
            {
                float4 debug_color = float4(0.0f, 0.0f, 0.0f, 1.0f);
                if(region == 1)
                {
                    debug_color.xyz = float3(1.0f, 0.0f, 0.0f);
                }
                else if(region == 2)
                {
                    debug_color.xyz = float3(0.0f, 1.0f, 0.0f);
                }
                else if(region == 3)
                {
                    debug_color.xyz = float3(0.0f, 0.0f, 1.0f);
                }
                else if(region == 4)
                {
                    debug_color.xyz = float3(1.0f, 0.0f, 1.0f);
                }
                else if(region == 5)
                {
                    debug_color.xyz = float3(0.0f, 1.0f, 1.0f);
                }
                return debug_color;
            }
            if(_DebugLights == 1) return float4((float3)0.0f, 1.0f);
        }
        
    #endif
        
    #ifdef _IS_PASS_LIGHT
        if(variant_selector == 1 && _EnableFaceMap)
        {
            shaded_area.x = dot(float3(0.5f, 0.5f, 1.0f), light);
        }
        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        shaded_area.x = smoothstep(0.0f, 0.5f, shaded_area.x) * 0.8f + 0.1f;
        float3 light_pass_color = ((diffuse.xyz * 5.0f) * _LightColor0.xyz) * atten * saturate(shaded_area.x) * 0.5f;
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), _FilterLight); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
            color.xyz = (light_color) * 0.5f;
        #elif defined(DIRECTIONAL)
            color.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
    #endif

    #ifdef is_xray
        if(_EnableStencil)
        {
            if(variant_selector == 2)
            {
                float3 up      = UnityObjectToWorldDir(_headUpVector.xyz);
                float3 forward = UnityObjectToWorldDir(_headForwardVector.xyz);
                float3 right   = UnityObjectToWorldDir(_headRightVector.xyz);

                float3 view_xz = normalize(view - dot(view, up) * up);
                float cosxz    = max(0.0f, dot(view_xz, forward));
                float alpha_a  = saturate((1.0f - cosxz) / 0.858f);

                float3 view_yz = normalize(view - dot(view, right) * right);
                float cosyz    = max(0.0f, dot(view_yz, forward));
                float alpha_b  = saturate((1.0f - cosyz) / 0.593f);

                float hair_alpha = max(alpha_a, alpha_b);


                color.w = max(hair_alpha, _HairBlendSilhouette);
            }
            else if(variant_selector == 1)
            {
                clip(lightmap.x - 0.45f);
            }
            else if(variant_selector != 3)
            {
                discard;
            }
        }
        else
        {
            discard;
        }
    #endif

    return color; 
}

edge_out vs_edge(edge_in v)
{
    edge_out o;
    if(!_EnableOutline) return (edge_out)0.0f;
    float exp_fix = 1.0;
    #if defined(faceishadow)
        if(variant_selector == 1 && _ExpOutlineToggle)
        {
            float exp_map =_FaceExpTex.SampleLevel(sampler_linear_repeat, v.uv_0.xy, 0).w;
            exp_fix = saturate((1.0f - exp_map) * _ExpOutlineFix);
            exp_fix = exp_map + exp_fix;
        } 
    #endif
    float3 outline_normal = v.tangent;
    outline_normal = mul((float3x3)UNITY_MATRIX_MV, outline_normal);
    outline_normal.z = 0.01f;
    outline_normal.xy = normalize(outline_normal.xyz).xy;
    float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);

    float width = (_OutlineWidth * _Scale) * v.color.w;

    float z_offset = -(wv_pos.xyz/wv_pos.w).z / unity_CameraProjection._m11;
    z_offset = (_GlobalOutlineScale.w > 0.01f) ? z_offset * _GlobalOutlineScale.x : z_offset;
    z_offset = 1.0f / rsqrt(z_offset / _Scale);


    wv_pos.xy = wv_pos + (outline_normal * (z_offset * width) * exp_fix);

    o.vertex = mul(UNITY_MATRIX_P, wv_pos);

    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    o.color = v.color;
    o.uv_a.xy = v.uv_0.xy;
    return o;
}

float4 ps_edge(edge_out i,  bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    float3 GI_color = DecodeLightProbe(normalize(i.normal));
    GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
    float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
    GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;  
    GI_color = (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
    float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
    float3 light_color = max(ambient_color, _LightColor0.rgb);
    // initialize output color : 
    float4 color = (float4)1.0f;   
    color.xyz = (float3)0.0f;

    clip(i.color.w - 0.01f);

    float lightmap_alpha = _LightMapTex.Sample(sampler_linear_repeat, i.uv_a.xy).w;
    float region = material_region(lightmap_alpha);

    float4 outline_color[5] =
    {
        _OutlineColor,
        _OutlineColor2,
        _OutlineColor3,
        _OutlineColor4,
        _OutlineColor5
    };

    region = _More_Outline_Color ? region : 0.0f;
    color = outline_color[region];

    color.xyz = color.xyz * light_color.xyz + GI_color.xyz;
    // if(_IsVFX) clip(-1);
    return color; 
}

shadow_out vs_shadow(shadow_in v)
{
    shadow_out o = (shadow_out)0.0f; // initialize so no funny compile errors
    float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
    o.view = normalize(view);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = pos_ws;
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.uv_a = float4(v.uv_0.xy, v.uv_1.xy);
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    return o;
}

float4 ps_shadow(shadow_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    // initialize uv 
    float2 uv = (_UseVFaceSwitch2UV && !vface) ? i.uv_a.zw : i.uv_a.xy;

    float alpha = _MainTex.Sample(sampler_MainTex, uv).w;

    float4 out_color = (float4)0.0f;

    return 0.0f;
}