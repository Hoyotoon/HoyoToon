vs_out vs_model(vs_in v)
{
    // fix potential compiler errors
    vs_out o = (vs_out)0;

    float4 pos_ws = mul(unity_ObjectToWorld, v.vertex);
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    
    o.pos = pos_wvp;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    o.tangent = float4(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz), v.tangent.w);
    o.uv_a.xy = v.uv_0.xy;
    o.uv_a.zw = v.uv_1.xy;
    o.uv_b.xy = v.uv_2.xy;
    o.uv_b.zw = v.uv_3.xy;
    o.view = _WorldSpaceCameraPos.xyz - pos_ws.xyz;
    o.ws_pos =  mul(unity_ObjectToWorld, v.vertex);
    o.pos_wvp = pos_wvp;
    o.color = v.color;
    if(_MaterialType == 3)
    {
        float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
        float3 vl = mul(_WorldSpaceLightPos0.xyz, UNITY_MATRIX_V) * (1.f / ws_pos.w);
        float3 offset_pos = ((vl * .001f) * float3(7,-1,5)) + v.vertex.xyz;
        v.vertex.xyz = offset_pos;
        o.pos = UnityObjectToClipPos(v.vertex);
    }

    // the bit flag is a 4-component vector where:
    // y = 1 if the highest bit of the blue channel is set, otherwise 0
    // z = 1 if the 5th bit of the blue channel is set, otherwise 0
    // x = 1 if the alpha channel is negative, otherwise 0
    // w = 0 (unused)
    o.bit_flag.y = int(int(v.color.z * 255.0f) & 128) == 128 ? 0 : v.color.w; // mmd could never
    o.bit_flag.z = int(int(255 * v.color.z) >> 5) & 1;
    o.bit_flag.x = 0 * -v.color.w + 1.0f;
    o.bit_flag.w = 0;  
    if(_MaterialType == 1) o.bit_flag.z = -int((int)(v.color.z * 255) & 3) * 0.200000003 + 0.899999976;


    o.pos_model = v.vertex.xyzw;
    o.ss_model = ComputeGrabScreenPos(o.pos);
    o.pos_grab = ComputeGrabScreenPos(v.vertex);
    o.eye_shadow = seperate_eyeshadow(v.color.x);
    float2 face_uv = _UseLegacyFace  ? v.uv_1.xy : v.uv_0.xy;
    vertex_face(v.uv_0.xy, face_uv, v.color, o.ws_pos, o.test); 
    TRANSFER_SHADOW(o)
    return o;
}

float4 ps_model(vs_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    // intialize inputs : 
    float3 normal = normalize(i.normal);
    float3 view = normalize(i.view);
    float2 uv_a = i.uv_a.xy;
    float2 uv_b = i.uv_b.xy;

    float2 main_uv = uv_a;
    float2 light_uv = uv_a;

    float2 selector_uv[4] = 
    {
        i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw
    };

    if(((_DoubleSided) && (!vface)) || ((_SymmetryUV) && (i.uv_a.z > 1))) main_uv = selector_uv[_DoubleUV % 4]; // flip uv if backface culling is enabled
    if((_DoubleSided) && (!vface)) light_uv = selector_uv[_DoubleUV % 4]; // flip uv if backface culling is enabled

    // get light attenuation:
    UNITY_LIGHT_ATTENUATION(atten, i, i.ws_pos.xyz);

    // sample textures
    float4 diffuse = _MainTex.Sample(sampler_linear_repeat, main_uv);
    float4 light = _LightTex.Sample(sampler_linear_repeat, light_uv); // rg normal, b diffuse bias
    float4 other_data = _OtherDataTex.Sample(sampler_linear_repeat, light_uv); // r mat id, g metallic, b specular mask
    float4 other_data2 = _OtherDataTex2.Sample(sampler_linear_repeat, light_uv); // r transparency, g smoothness, b emission mask 

    if(_LegacyOtherData) other_data2.xyz = float3(diffuse.w, light.w, other_data.w);
    if(_UseLegacyFace) i.test.z = 1.0f;

    float alpha = diffuse.w;
    // diffuse color shifting
    if(_EnableHueShift && _EnableColorHue)
    {
        float mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, main_uv, _DiffuseMaskSource);
        mask = _UseHueMask ? mask : 1.0f;
        if(_EnableColorHue)diffuse.xyz = hue_shift(diffuse.xyz, other_data.x, _ColorHue, _ColorHue2, _ColorHue3, _ColorHue4, _ColorHue5, _GlobalColorHue, _AutomaticColorShift, _ShiftColorSpeed, 1);
        diffuse.xyz = saturate(diffuse.xyz);
    }

    float4 final_color = diffuse;

    #if defined(_IS_PASS_BASE)

        // eye shadow, eye highlight, and eye
        if(_MaterialType == 5 || _MaterialType == 6)
        {  
            #if defined(_IS_STENCIL)
                clip(-1);
            #endif
            diffuse = diffuse * i.eye_shadow;
            if(_EnableLUT) 
            {
                diffuse.xyz = LUT_2D(diffuse.xyz, _Lut2DTexParam);
            }
            if(_ApplyLighting)CalcLighting(normal, diffuse.xyz);
            
            return diffuse;
        }
        else if( _MaterialType == 2)
        {
            #if defined(_IS_STENCIL)
                clip(-1);
            #endif
            if(_EnableLUT) 
            {
                diffuse.xyz = LUT_2D(diffuse.xyz, _Lut2DTexParam);
            }
            if(_ApplyLighting)CalcLighting(normal, diffuse.xyz);
            return diffuse;
        }
        if(_MaterialType == 3)
        {
            float4 hair_shadow = float4(1.0f, 0.9f, 0.9f, 1.0f);
            #if defined(_IS_STENCIL)
                clip(-1);
            #endif
            if(_EnableLUT) 
            {
                hair_shadow.xyz = LUT_2D(hair_shadow.xyz, _Lut2DTexParam);
            }
            // if(_ApplyLighting)CalcLighting(normal, hair_shadow.xyz);
            return hair_shadow;
        }
        

        // get material id : 
        float material_id = _MaterialType == 1 ? i.bit_flag.z : other_data.x;
        float skin_id = Material_Detect(other_data.x).x;

        // normal mapping : 
        float3 bumped = (_MaterialType != 1 && _UseBumpMap) ? normal_mapping(light.rgb, i.normal, i.tangent, vface) : i.normal;
        bumped = normalize(bumped);

        float ndotl = dot(bumped, _WorldSpaceLightPos0.xyz);

        if(_MaterialType == 1) ndotl = shadow_area_face(_LightTex, i.test, _WorldSpaceLightPos0.xyz).x * 2.0f - 1.0f;
        float4 shadow = float4((float3)1.0f, 0.0f);
        float3 highlight = 0.0f;
        if(_MaterialType != 1)
        {
            float unity_shadow = 1;
            unity_shadow = SHADOW_ATTENUATION(i);
            unity_shadow = smoothstep(0.0f, 1.f, unity_shadow);

            if(!_UseSelfShadow) 
            {
                unity_shadow = 1.f;
            }
            shadow = shadow_body(bumped, _WorldSpaceLightPos0.xyz, light, other_data.x, unity_shadow);
            
            // return float4((float3)unity_shadow, 1.0f);
        }
        else
        {
            shadow.xyz = shadow_face(bumped, _WorldSpaceLightPos0.xyz, light, 1.0f, ndotl, i.test.z);
            float3 nose = nose_line(normalize(i.view), bumped, material_id, diffuse.w);
            diffuse.xyz = diffuse.xyz * nose;
            highlight = face_high(normal, _WorldSpaceLightPos0, view, _LightTex, i.test);
            // return float4(highlight.xxx, 1.0f);
        }

        float4 material_color;
        float4 check = material_id.xxxx < float4(0.2f, 0.4f, 0.6f, 0.8f);
        material_color = check.wwww ? _Color2 : _Color;
        material_color = check.zzzz ? _Color3 : material_color;
        material_color = check.yyyy ? _Color4 : material_color; 
        material_color = check.xxxx ? _Color5 : material_color;

        if(_MaterialType != 2 )final_color.xyz = saturate(diffuse.xyz * shadow.xyz);
        float3 specular_color = specular(bumped, view, _WorldSpaceLightPos0.xyz, other_data, other_data2, final_color.xyz, shadow.w, i.pos_model);
        specular_color = skin_id ? specular_color * 0.1f : specular_color;
        final_color.xyz = matcap_body(bumped, other_data.x, other_data2.z, uv_a, final_color);
        if(_MaterialType != 1) final_color.xyz = final_color.xyz + specular_color;
        
        final_color = final_color * material_color;
        float3 rim = (float3)0.0f;
        if(_RimGlow)
        {
            rim = ndotv_rim(bumped, view, _WorldSpaceLightPos0, other_data, other_data2, diffuse, i.bit_flag);
            float rim_mask = rim_screen_mask(normal, _WorldSpaceLightPos0, i.ss_model, i.ws_pos);
            rim_mask = saturate(pow(rim_mask, 1)*2.5);
            if(_EnableHueShift && _EnableRimHue)
            {
                float mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, i.uv_a.xy, _RimMaskSource);
                mask = _UseHueMask ? mask : 1.0f;
                if(_EnableRimHue)rim.xyz = hue_shift(rim.xyz, other_data.x, _RimHue, _RimHue2, _RimHue3, _RimHue4, _RimHue5, _GlobalRimHue, _AutomaticRimShift, _ShiftRimSpeed, mask);
            }
            rim = ((rim * 1.5) * rim_mask);
            final_color.xyz = rim + final_color;
        }
        
        if(_EnableLUT) 
        {
            final_color.xyz = LUT_2D(final_color.xyz, _Lut2DTexParam);
        }

        if(_ApplyLighting) CalcLighting(bumped, (final_color.xyz)); // apply environment lighting
        final_color.xyz = emission(final_color.xyz, other_data2, other_data, uv_a); // layer 1 emission
        if(_MaterialType == 1) final_color.xyz = final_color.xyz + highlight;
        if(_SecondaryEmission) final_color.xyz = secondary_emission(final_color.xyz, uv_a, uv_b, material_id); // layer 2 emission
        
        float2 screen_uv_real[4] =
        {
            i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw
        };

        float4 screen_uv[3] =
        {
            i.ss_model, (i.ws_pos), float4(screen_uv_real[_ScreenUVEnum % 4], 0.0f, 1.0f)
        };

        if(_ScreenImage) final_color.xyz = screen_image(final_color.xyz, uv_a, uv_b, screen_uv[_ScreenUVSource % 3], material_id, view, i.ws_pos); // star cock thing
        
        if(_UseAlpha) apply_alpha(final_color, alpha);

        #if defined(_IS_STENCIL)
            if(_EnableStencil)
            {
                if(_MaterialType == 4)
                {
                    alpha = other_data2.x;
                    alpha = pow(alpha, 1.0);
                    final_color.w = max(alpha, _MinStencilAlpha);
                }
            }
            else
            {
                discard;
            }
        #endif


        if(_DebugMode != 0)
        {
            if(_DebugDiffuse != 0 && _DebugMode != 0)
            {
                if(_DebugDiffuse == 1) return float4(diffuse.xyz, 1.0);
                else return float4(alpha.xxx, 1.0);
            }
            if(_DebugLightMap != 0)
            {
                if(_DebugLightMap == 1) return float4(light.xy, 1.0f,  1.0f);
                if(_DebugLightMap == 2) return float4(light.zzz, 1.0f);
                if(_DebugLightMap == 3) return float4(light.www, 1.0f);
                // if(_DebugLightMap == 4) return float4(light.www, 1.0f);
            }
            if(_DebugOtherData != 0)
            {
                if(_DebugOtherData == 1) return float4(other_data.xxx, 1.0f);
                if(_DebugOtherData == 2) return float4(other_data.yyy, 1.0f);
                if(_DebugOtherData == 3) return float4(other_data.zzz, 1.0f);
                if(_DebugOtherData == 4) return float4(other_data.www, 1.0f);
            }
            if(_DebugOtherData2 != 0)
            {
                if(_DebugOtherData2 == 1) return float4(other_data2.xxx, 1.0f);
                if(_DebugOtherData2 == 2) return float4(other_data2.yyy, 1.0f);
                if(_DebugOtherData2 == 3) return float4(other_data2.zzz, 1.0f);
                if(_DebugOtherData2 == 4) return float4(other_data2.www, 1.0f);
            }
            if(_DebugVertexColor != 0)
            {
                if(_DebugVertexColor == 1) return float4(i.color.xxx, 1.0f);
                if(_DebugVertexColor == 2) return float4(i.color.yyy, 1.0f);
                if(_DebugVertexColor == 3) return float4(i.color.zzz, 1.0f);
                if(_DebugVertexColor == 4) return float4(i.color.www, 1.0f);
            }
            if(_DebugUV != 0)
            {
                if(_DebugUV == 1) return float4(i.uv_a.xy, 0.0f, 1.0f);
                if(_DebugUV == 2) return float4(i.uv_a.zw, 0.0f, 1.0f);
                if(_DebugUV == 3) return float4(i.uv_b.xy, 0.0f, 1.0f);
                if(_DebugUV == 4) return float4(i.uv_b.zw, 0.0f, 1.0f);
                if(_DebugUV == 5) return float4(i.uv_b.zw * 0.5f + 0.5f, 1.0f, 1.0f);
            }
            if(_DebugTangent != 0)
            {
                if(_DebugTangent == 1) return float4(i.tangent.xyz, 1.0f);
            }
            if(_DebugNormalVector != 0)
            {
                if(_DebugNormalVector == 1) return float4(i.normal.xyz * 0.5 + 0.5, 1.0f);
                if(_DebugNormalVector == 2) return float4(i.normal.xyz, 1.0f);
                if(_DebugNormalVector == 3) return float4(bumped * 0.5 + 0.5, 1.0f);
                if(_DebugNormalVector == 4) return float4(bumped, 1.0f);
            }
            if(_DebugMatcap != 0)
            {
                return float4(matcap_body(bumped, other_data.x, other_data2.z, uv_a, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz, 1.0f);
            }
            if(_DebugSpecular) return float4(specular_color.xyz, 1.0f);
            if(_DebugRimLight) return float4(rim.xyz, 1.0f);
            if(_DebugFaceVector == 1) return float4(normalize(UnityObjectToWorldDir(_headForwardVector.xyz)).xyz, 1.0f);
            if(_DebugFaceVector == 2) return float4(normalize(UnityObjectToWorldDir(_headRightVector.xyz)).xyz, 1.0f);
            if(_DebugFaceVector == 3) return float4(normalize(UnityObjectToWorldDir(_headUpVector.xyz)).xyz, 1.0f);
            if(_DebugEmission != 0)
            {
                if(_DebugEmission == 1) 
                {
                    float4 debug_emission = float4(0.0f, 0.0f, 0.0f, 1.0f);
                    debug_emission.xyz = emission(debug_emission.xyz, other_data2, other_data, uv_a);
                    return debug_emission;
                }
                if(_DebugEmission == 2)
                {
                    float4 debug_emission = float4(0.0f, 0.0f, 0.0f, 1.0f);
                    debug_emission.xyz = secondary_emission(debug_emission.xyz, uv_a, uv_b, material_id); // layer 2 emission;
                    return debug_emission;
                }
                if(_DebugEmission == 3) // both
                {
                    float4 debug_emission = float4(0.0f, 0.0f, 0.0f, 1.0f);
                    debug_emission.xyz = emission(debug_emission.xyz, other_data2, other_data, uv_a);
                    debug_emission.xyz = secondary_emission(debug_emission.xyz, uv_a, uv_b, material_id); // layer 2 emission;

                    return debug_emission;
                }
            }
            if(_DebugMaterialIDs != 0)
            {
                if(_DebugMaterialIDs < 6)
                {
                    float ID = 0;
                    int array_ID = _DebugMaterialIDs;

                    ID = material_id < 0.8f ? 2 : 1;
                    ID = material_id < 0.6f ? 3 : ID;
                    ID = material_id < 0.4f ? 4 : ID;
                    ID = material_id < 0.2f ? 5 : ID;
                    
                    if(array_ID == ID)
                    {
                        return float4((float3)1.0f, 1.0f);
                    }
                    else
                    {
                        return float4((float3)0.0f, 1.0f);
                    }
                }
                else
                {
                    float4 ID_Color = float4((float3)0.0f, 1.0f);
                    ID_Color.xyz = material_id < 0.8f ? float3(0,1,0): float3(1,0,0);
                    ID_Color.xyz = material_id < 0.6f ? float3(0,0,1) : ID_Color.xyz;
                    ID_Color.xyz = material_id < 0.4f ? float3(1,1,1) : ID_Color.xyz;
                    ID_Color.xyz = material_id < 0.2f ? float3(0,0,0) : ID_Color.xyz;

                    return ID_Color;
                }
            }
            if(_DebugLights == 1) return float4((float3)0.0f, 1.0f);
        }


    #endif
    #if defined(_IS_PASS_LIGHT)
        float3 light_direction = _WorldSpaceLightPos0,xyz;
        #if defined(POINT) || defined(SPOT)
            light_direction = normalize(_WorldSpaceLightPos0.xyz - i.ws_pos.xyz);
        #endif
        float shadow_area = dot(normal, light_direction);
        if(_MaterialType == 1) shadow_area = shadow_area_face(_LightTex, i.test, light_direction);

        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        float3 light_pass_color = ((diffuse.xyz * 5.0f) * _LightColor0.xyz) * atten * shadow_area * 0.5f;
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), _FilterLight); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
        final_color.xyz = (light_color) * 0.5f;
        #elif defined(DIRECTIONAL)
        final_color.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
        if((_MaterialType == 3 || _MaterialType == 5 || _MaterialType == 6)) clip(-1);   
        if(_DebugMode)
        {
            if(_DebugLights != 1) return float4((float3)0.0f, 1.0f);
        }     
    #endif
    return final_color;
}

vs_out vs_outline(vs_in v)
{
    vs_out o = (vs_out)0;
    float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
    o.view = normalize(view);

    o.uv_a.xy = v.uv_0.xy;
    o.uv_a.zw = v.uv_1.xy;
    o.uv_b.xy = v.uv_2.xy;
    o.uv_b.zw = v.uv_3.xy;

    float2 selector_uv[4] = 
    {
        v.uv_0.xy, v.uv_1.xy, v.uv_2.xy, v.uv_3.xy
    };

    float2 normal_uv = v.uv_3.xy;
    if(_NormalUV == 0){normal_uv = v.uv_0.xy;}
    if(_NormalUV == 1){normal_uv = v.uv_1.xy;}
    if(_NormalUV == 2){normal_uv = v.uv_2.xy;}
    if(_NormalUV == 3){normal_uv = v.uv_3.xy;}

    // get bitangent, should probably output it but meh
    float3 bitangent = cross(v.normal, v.tangent.xyz) * (v.tangent.w * unity_WorldTransformParams.w);

    // intiialize the outline normals
    float4 outline_normal;
    
    // outline_normal.xyz = mul(v.normal, (float3x3)unity_ObjectToWorld);
    outline_normal.w = -0.5f + v.color.y;

    float z_norm = dot(normal_uv.xy, normal_uv.xy);
    z_norm = min(1, z_norm);
    z_norm = 1 + -z_norm;
    z_norm = sqrt(z_norm);

    // normal mapping 
    outline_normal.yzx = v.tangent.yzx * v.normal.zxy;
    outline_normal.yzx = v.normal.yzx * v.tangent.zxy + -outline_normal.yzx;
    outline_normal.yzx = v.tangent.www * outline_normal.yzx;
    outline_normal.yzx = normal_uv.yyy * outline_normal.yzx;
    outline_normal.yzx = normal_uv.xxx * v.tangent.xyz + outline_normal.yzx;
    outline_normal.xyz = z_norm * v.normal.xyz + outline_normal.yzx;
    
    // transform mapped normals to world space
    outline_normal.xyz = normalize(outline_normal.xyz);
    o.normal = normalize(mul(v.normal, (float3x3)(unity_ObjectToWorld))).xyz;

    // transform world space normals to view space
    outline_normal.xyz = mul((float3x3)UNITY_MATRIX_MV, outline_normal.xyz);
    if(!_OutlineZOff)outline_normal.z = 0.0001f; 
    outline_normal.xy = normalize(outline_normal.xyz).xy;


    float fov = _DisableFOVScalingOL || (unity_CameraProjection[3].w == 1) ? 1.0f : 2.414f / unity_CameraProjection[1].y; // may need to come back in and change this back to 1.0f
            // can't remember in what vrchat mode this was messing up 

    float max_z = (unity_CameraProjection[3].w == 1) ? 0.5f : 0.01f;

    float depth = -wv_pos.z * fov;
    float offset_depth = saturate(1.0f - depth);
    float max_offset = lerp(max_z * 0.1, max_z, offset_depth);

    float3 z_offset = (wv_pos.xyz) * (float3)max_offset * (float3)0.01f;

    float outline_tex = 1.0f;
    if(_UseLightMapOL) outline_tex = _LightTex.SampleLevel(sampler_linear_repeat, v.uv_0.xy, 0.0f).z;
    float outline_scale = (_OutlineWidth * v.color.x);
    outline_scale = outline_scale * 0.0015f;
    outline_scale = outline_scale * outline_tex;
    
    float3 outline_position = wv_pos + z_offset;
    outline_position = outline_scale * outline_normal.xyz + outline_position;

 
    o.pos = wv_pos;
    o.pos.xyz = outline_position;

    o.pos = mul(UNITY_MATRIX_P, o.pos); 
    if(!_Outline) o.pos = float4(-90,-90,-90, 1.0);
    
    vertex_face(v.uv_0.xy, v.uv_1.xy, v.color, o.test); 
    if(_MaterialType == 1) o.bit_flag.z = -int((int)(v.color.z * 255) & 3) * 0.200000003 + 0.899999976; // get skin material IDs

    if(_MaterialType == 2 || _MaterialType == 3 || _MaterialType == 6 || _MaterialType == 5) o.pos = float4(-99.0, -99.0, -99.0, 1.0);

    return o;
}

float4 ps_outline(vs_out i) : SV_TARGET
{
    // initialize uv
    float2 uv_a = i.uv_a.xy;
    float2 uv_b = i.uv_b.xy;

    // sample textures
    float4 diffuse = _MainTex.Sample(sampler_linear_clamp, uv_a.xy);
    float other_data = _OtherDataTex.Sample(sampler_linear_clamp, uv_a.xy).x; 
    if(_MaterialType == 1) other_data = i.bit_flag.z;

    float4 final_color = (float4)1.0f;
    final_color.xyz = outline_color(diffuse, other_data, normalize(i.normal), _WorldSpaceLightPos0);

    if(_ApplyLighting) CalcLighting( normalize(i.normal), final_color.xyz); // apply environment lighting

    if(_EnableHueShift && _EnableOutlineHue)
    {
        float outline_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, i.uv_a.xy, _OutlineMaskSource);
        outline_mask = _UseHueMask ? outline_mask : 1.0f;
        if(_EnableOutlineHue)final_color.xyz = hue_shift(final_color.xyz, other_data.x, _OutlineHue, _OutlineHue2, _OutlineHue3, _OutlineHue4, _OutlineHue5, _GlobalOutlineHue, _AutomaticOutlineShift, _ShiftOutlineSpeed, outline_mask);
    }
    if(_EnableLUT) 
    {
        final_color.xyz = LUT_2D(final_color.xyz, _Lut2DTexParam);
    }


    if(_MaterialType == 3) clip(-1);
    return final_color;
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
    if(_MaterialType == 3) clip(-1);
    return 0.0f;
}
