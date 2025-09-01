// ====================================================================
// VERTEX SHADERS 
vs_out vs_base(vs_in v)
{
    vs_out o = (vs_out)0.0f;
    UNITY_SETUP_INSTANCE_ID(v); 
    UNITY_INITIALIZE_OUTPUT(vs_out, o); 
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.ws_pos =  v.vertex;
    o.ss_pos = ComputeScreenPos(o.pos);
    // o.ss_pos = o.pos;

    o.uv = float4(v.uv_0, v.uv_1); // populate this with both uvs to save on texcoords 
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal) ; // WORLD SPACE NORMAL 
    o.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz); // WORLD SPACE TANGENT
    o.tangent.w = v.tangent.w * unity_WorldTransformParams.w; 
    // in case the data stored in the tangent slot is actually proper tangents and not a 2nd set of normals
    o.view = _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz;
    // its more efficient to do this in the vertex shader instead of trying to calculate the view Vector for every pixel 
    o.v_col = v.v_col;    

    o.uv_2 = float4(v.uv_2.xy, v.uv_3.xy);
    o.vertex = v.vertex;

    #if defined(can_dissolve)
        if(_DissoveON)
        {
            dissolve_vertex(v, o.dis_pos, o.dis_uv);
        }
    #endif

    if(_FaceMaterial) o.normal = 0.5f;

    if(_EnableParticleSwirl)
    {
        // float4 vertex_color = _VertexColor ? (v.v_col) : _VertexColorFallback;
        // o.v_col.w = lerp(1.0f, _Disappear, (vertex_color.w * _DisStep.x));
        // float lr_rand = (_LineRendererAniRandomSeed * 2234.324);
        // lr_rand = fract(lr_rand);
        // seed.x = ((lr_rand * 0.60000002) + -0.39999998);
        // seed.x = ((_LineRendererAniRandomSeed * seed.x) + 1.0);
        // seed.x = ((_EnableRandomSeed) ? (seed.x) : (1.0));
        // o.dis_uv = (vertex_color * seed.xxxx);
        // float new_vc = (vertex_color.xyz * (seed.xxx * _InsideColor.xyz));
        // o.v_col.xyz = new_vc;
        // o.uv.zw = ((bool(_MaskON)) ? v.uv_0.xy * _MaskTex_ST.xy + _MaskTex_ST.zw : min(abs(-v.uv_0.xy + _MaskTex_ST.zw), 1.0f);
        // u_xlat6.x = (seed.x + -1.0));
        // float2 main_uv = ((v.uv_0.xy * _MainTex_ST.xy) + _MainTex_ST.zw);
        // float timing = ((_EffectOverrideTimeEnable) ? (_EffectOverrideTime) : (_Time.y));
        // main_uv.xy = ((_MainSpeed.xy * timing) + main_uv.xy);
        // o.uv.xy = ((main_uv.xy * seed.xx) + (seed.x + -1.0));
        // main_uv.xy = (seed.xx * _DisRSpeed.xy);
        // float4 temp_uv;
        // temp_uv.xy = (u_xlat4.xx * main_uv.xy);
        // temp_uv.zw = (u_xlat4.xx * _DisGSpeed.xy);
        // float4 dis_uv;
        // dis_uv.xy = ((v.uv_0.xy * _DisTex_ST.xy) + _DisTex_ST.zw);
        // dis_uv.zw = (dis_uv.xy * _DisGSpeed.zw);
        // temp_uv = (temp_uv + dis_uv);
        // o.uv_2.xy = ((temp_uv.xy * seed.xx) + (seed.x + -1.0));
        // o.uv_2.zw = temp_uv.zw;
    }

    #if defined (_is_shadow)
        if(variant_selector == 4)
        {
            float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
            float3 vl = mul(_WorldSpaceLightPos0.xyz, UNITY_MATRIX_V) * (1.f / ws_pos.w);
            float3 offset_pos = ((vl * .001f) * float3(7,0,5)) + v.vertex.xyz;
            v.vertex.xyz = offset_pos;
            o.pos = UnityObjectToClipPos(v.vertex);
        }
        else if(_FaceMaterial)
        {
            o.pos = pos_wvp;
        }
        else
        {
            o.pos = float4(-90,-90,-90,1);
        }
        // o.vertex = 0.f;
    #endif
    if(hide_parts(v.v_col) && !_FaceMaterial && (_ShowPartID != 0)) o.pos = float4(-99.0, -99.0, -99.0, 1.0);
    TRANSFER_SHADOW(o)
    return o;
}

vs_out vs_edge(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast to 0 to avoid intiailization warnings

    
    if(_EnableOutline)
    {
        if(_FaceMaterial) // sigh is this even going to work in vr? 
        {
            // Define meaningful variables for face outline calculation
            float4 outlineParams;
            float4 cameraDistanceFactors;
            float4 faceDirectionFactors;
            float4 outlineRangeParams;
            
            // Setup initial direction constants
            outlineParams.xy = float2(-0.206, 0.961); // Side direction constants
            outlineParams.z = _OutlineFixSide;
            
            // Calculate world position and camera direction
            faceDirectionFactors.xyz = mul(v.vertex.xyz, (float3x3)unity_ObjectToWorld).xyz;
            cameraDistanceFactors.xyz = _WorldSpaceCameraPos - faceDirectionFactors.xyz;
            faceDirectionFactors.xyz = mul(faceDirectionFactors.xyz, (float3x3)unity_ObjectToWorld).xyz;
            
            // Calculate distance and position factors
            float distanceToCamera = length(faceDirectionFactors.xyz);
            faceDirectionFactors.yzw = distanceToCamera * faceDirectionFactors.xyz;
            outlineParams.w = faceDirectionFactors.x * distanceToCamera + -0.1;
            
            // Calculate side direction factor
            float sideFactor = dot(outlineParams.xyz, faceDirectionFactors.xyz);
            
            // Setup front direction constants
            cameraDistanceFactors.yz = float2(-0.206, 0.961); // Front direction constants
            cameraDistanceFactors.xw = -float2(_OutlineFixSide.x, _OutlineFixFront.x);
            
            // Calculate front direction factor
            float frontFactor = dot(cameraDistanceFactors.xyz, faceDirectionFactors.xyz);
            
            // Calculate top direction factor
            float topFactor = dot(float2(0.076, 0.961), faceDirectionFactors.xy);
            
            // Determine maximum direction factor
            float maxDirectionFactor = max(frontFactor, sideFactor);
            float directionBlendFactor = 0.1 - maxDirectionFactor;
            directionBlendFactor = directionBlendFactor * 9.999998;
            directionBlendFactor = max(directionBlendFactor, 0.0);
            
            // Apply smoothstep to direction factor
            float smoothStepFactor = directionBlendFactor * -2.0 + 3.0;
            directionBlendFactor = directionBlendFactor * directionBlendFactor;
            directionBlendFactor = directionBlendFactor * smoothStepFactor;
            directionBlendFactor = min(directionBlendFactor, 1.0);
            
            // Calculate top direction blend
            float topFactorSaturated = saturate(topFactor);
            float invertedTopFactor = 1.0 - topFactor;
            float blendedTopFactor = cameraDistanceFactors.x + topFactorSaturated;
            
            // Apply saturation to factors
            float2 saturatedFactors = saturate(float2(blendedTopFactor, invertedTopFactor) * float2(20.0, 5.0));
            blendedTopFactor = saturatedFactors.x;
            invertedTopFactor = saturatedFactors.y;
            
            // Apply smoothstep to top factor
            float topSmoothStep = blendedTopFactor * -2.0 + 3.0;
            blendedTopFactor = blendedTopFactor * blendedTopFactor;
            blendedTopFactor = blendedTopFactor * topSmoothStep;
            
            // Combine direction factors
            directionBlendFactor = max(directionBlendFactor, blendedTopFactor);
            directionBlendFactor = min(directionBlendFactor, 1.0);
            directionBlendFactor = directionBlendFactor - 1.0;
            
            // Apply vertex color and outline width
            float vertexColorFactor = v.v_col.y * directionBlendFactor + 1.0;
            float outlineWidthFactor = vertexColorFactor * _OutlineWidth;
            outlineWidthFactor = outlineWidthFactor * _OutlineScale;
            
            // Calculate distance-based smoothing
            float distanceSmoothStep = distanceToCamera * -2.0 + 3.0;
            float squaredDistance = distanceToCamera * distanceToCamera;
            distanceSmoothStep = squaredDistance * distanceSmoothStep;
            
            // Calculate outline range interpolation
            float2 outlineRanges = -float2(_OutlineFixRange1.x, _OutlineFixRange2.x) + float2(_OutlineFixRange3.x, _OutlineFixRange4.x);
            float2 interpolatedRanges = distanceSmoothStep * outlineRanges + float2(_OutlineFixRange1.x, _OutlineFixRange2.x);
            
            // Apply smoothstep between ranges
            float rangeBlendFactor = smoothstep(interpolatedRanges.x, interpolatedRanges.y, invertedTopFactor);
            
            // Apply vertex color adjustments
            rangeBlendFactor = rangeBlendFactor * v.v_col.z;
            bool2 vertexColorMask = v.v_col.zy > float2(0.0, 0.0);
            rangeBlendFactor = vertexColorMask.x ? rangeBlendFactor : v.v_col.w;
            
            // Apply lip outline fix if needed
            bool needsLipFix = v.v_col.y < 1.0;
            bool applyLipFix = vertexColorMask.y ? needsLipFix : false;
            float lipFixFactor = applyLipFix ? 1.0 : 0.0;
            rangeBlendFactor = lipFixFactor * _FixLipOutline + rangeBlendFactor;
            
            // Final outline width calculation
            outlineWidthFactor = rangeBlendFactor * outlineWidthFactor;
            
            // Apply outline to vertex position
            float3 outlineNormal;
            outlineNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);
            if(!_OutlineZOff) outlineNormal.z = -1;
            outlineNormal.xyz = normalize(outlineNormal.xyz);
            
            float4 worldViewPos = mul(UNITY_MATRIX_MV, v.vertex);
            float fovWidthFactor = 1.0f / (rsqrt(abs(worldViewPos.z / unity_CameraProjection._m11)));
            if(!_EnableFOVWidth) fovWidthFactor = 1;
            
            worldViewPos.xyz = worldViewPos + (outlineNormal * fovWidthFactor * outlineWidthFactor);
            o.pos = mul(UNITY_MATRIX_P, worldViewPos);
        }
        else
        {
            float3 outline_normal;
            outline_normal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);
            if(!_OutlineZOff)outline_normal.z = -1;
            outline_normal.xyz = normalize(outline_normal.xyz);
            float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
            float fov_width = 1.0f / (rsqrt(abs(wv_pos.z / unity_CameraProjection._m11)));
            if(!_EnableFOVWidth)fov_width = 1;
            wv_pos.xyz = wv_pos + (outline_normal * fov_width * (v.v_col.w * _OutlineWidth * _OutlineScale));
            o.pos = mul(UNITY_MATRIX_P, wv_pos);
        }
    }
    o.uv = float4(v.uv_0, v.uv_1);
    o.v_col = v.v_col; 
    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);


    if(_DissoveON )
    {
        dissolve_vertex_sdw(v, o.dis_pos, o.dis_uv);
    }
    if(hide_parts(v.v_col) && !_FaceMaterial && (_ShowPartID != 0)) o.pos = float4(-99.0, -99.0, -99.0, 1.0);
    return o;
}

shadow_out vs_shadow(shadow_in v)
{
    shadow_out o = (shadow_out)0.0f; // initialize so no funny compile errors
    float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
    o.view = normalize(view);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    // if(_FaceMaterial) o.normal = 0.5f;
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = pos_ws;
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.uv_a = float4(v.uv_0.xy, v.uv_1.xy);

    if(_DissoveON )
    {
        dissolve_vertex(v, o.dis_pos, o.dis_uv);
    }
    if(hide_parts(v.v_col) && !_FaceMaterial && (_ShowPartID != 0)) o.pos = float4(-99.0, -99.0, -99.0, 1.0);
    o.hide = hide_parts(v.v_col) && !_FaceMaterial && (_ShowPartID != 0);
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    return o;
} 

// ====================================================================
// PIXEL SHADERS

float4 ps_base(vs_out i, bool vface : SV_IsFrontFace) : SV_Target
{
    float4 ws_pos = mul(unity_ObjectToWorld, i.ws_pos);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    // GET LIGHT ATTENUATION FOR BOTH PASSES : 
    UNITY_LIGHT_ATTENUATION(atten, i, ws_pos.xyz);
    // FIX POTENTIAL ISSUES WITH ALPHA CLIPPING 
    float transparnecy_check = _IsTransparent;
    float testTresh =_AlphaTestThreshold;
    if(_IsTransparent) testTresh = 0.0f;

    // INITIALIZE VERTEX SHADER INPUTS : 
    float3 normal    = normalize(i.normal);
    float3 vs_normal = normalize(mul((float3x3)UNITY_MATRIX_V, normal));
    float3 view      = normalize(i.view);
    float3 vs_view   = normalize(mul((float3x3)UNITY_MATRIX_V, view));
    float2 uv        = i.uv.xy;
    float4 vcol      = i.v_col;
    float3 light = _WorldSpaceLightPos0.xyz;
    float hair_alpha = 1.0f;
    float3 rim_light = (float3)0.0f;
    float3 specular = (float3)0.0f;
    float3 emission_color = (float3)0.0f;
    float emis_area = 0.0f;
    float3 test_normal = normal;
    float3 tangents = i.tangent.xyz;

    // MATERIAL COLOR :
    float4 color = (_HairMaterial) ? _Color0 * _Color : _Color;

    if(!vface && _backfdceuv2) // use uv2 if vface is false
    { // so basically if its a backfacing face
        uv.xy = i.uv.zw;
        color = _BackColor;
        normal.z = normal.z * -1.0f;
        tangents.z = tangents.z * -1.0f;
    }

    color.a = 1.0f; // this prevents issues with the alpha value of the material being less than 1
    // might remove later

    if(_DissoveON)
    {
        dissolve_clip(ws_pos, i.dis_pos, i.dis_uv, uv);
    }

    // INITIALIZE OUTPUT COLOR : 
    float4 out_color = color;

    // COMPUTE HALF floatTOR : 
    float3 half_vector = normalize(view + _WorldSpaceLightPos0);

    // DOT PRODUCTS : 
    float ndotl = dot(normal, light);
    float ndoth = dot(normal, half_vector);
    float ndotv = dot(normal, view);

    // SAMPLE TEXTURES : 
    float4 diffuse = _MainTex.Sample(sampler_MainTex, uv);
    float4 lightmap = _LightMap.Sample(sampler_linear_repeat, uv);
    float lightmap_alpha = _LightMap.Sample(sampler_linear_repeat, i.uv.xy).w;
    #if defined(faceishadow)
        float4 facemap = _FaceMap.Sample(sampler_linear_repeat, uv);
        float4 faceexp = _FaceExpression.Sample(sampler_linear_repeat, uv);
    #endif
    #if defined(use_emission)
        float4 emistex = _EmissionTex.Sample(sampler_linear_repeat, uv);
    #endif
    #if defined(second_diffuse)
        if(_UseSecondaryTex)
        {
            float2 secondary_uv[3] =
            {
                i.uv.xy, i.uv.zw, i.uv_2.xy
            };
            float4 secondary = _SecondaryDiff.Sample(sampler_linear_repeat, secondary_uv[_SecondaryUV % 3]);
            diffuse = lerp(diffuse, secondary, _SecondaryFade);
        }
    #endif

    // if(_StarrySky)
    // {
    //     diffuse = starry_sky(diffuse, uv);
    // }

    #if defined(can_shift)
        float diffuse_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _DiffuseMaskSource);
        float rim_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _RimMaskSource);
        float emission_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _EmissionMaskSource);
        if(!_UseHueMask) 
        {
            diffuse_mask = 1.0f;
            rim_mask = 1.0f;
            emission_mask = 1.0f;
        }
    #endif
    
    // EXTRACT MATERIAL REGIONS 
    float material_ID = floor(8.0f * lightmap.w);
    float ramp_ID     = ((material_ID * 2.0f + 1.0f) * 0.0625f);
    // when writing the shader for mmd i had to invert the ramp ID since the uvs are read differently  

    // I dont want to write a set of if else statements like this for the specular, rim, etc
    // so this is the next best thing i can do
    int curr_region = material_region(material_ID);

    // sample the various mluts
    float4 lut_speccol = _MaterialValuesPackLUT.Load(float4(material_ID, 0, 0, 0)); // xyz : color
    float4 lut_specval = _MaterialValuesPackLUT.Load(float4(material_ID, 1, 0, 0)); // x: shininess, y : roughness, z : intensity
    float4 lut_edgecol = _MaterialValuesPackLUT.Load(float4(material_ID, 2, 0, 0)); // xyz : color
    float4 lut_rimcol  = _MaterialValuesPackLUT.Load(float4(material_ID, 3, 0, 0)); // xyz : color
    float4 lut_rimval  = _MaterialValuesPackLUT.Load(float4(material_ID, 4, 0, 0)); // x : rim type, y : softness , z : dark
    float4 lut_rimscol = _MaterialValuesPackLUT.Load(float4(material_ID, 5, 0, 0)); // xyz : color
    float4 lut_rimsval = _MaterialValuesPackLUT.Load(float4(material_ID, 6, 0, 0)); // x: rim shadow width, y: rim shadow feather 
    float4 lut_bloomval = _MaterialValuesPackLUT.Load(float4(material_ID, 7, 0, 0)); // xyz: rim color
        

    #if defined (_is_shadow)
            if(_UseSelfShadow && (variant_selector == 4) && _HairMaterial)
            {
                float4 hair_shadow;
                hair_shadow.xyz = _ShadowColor;
                hair_shadow.w = 0.1;
                return hair_shadow;
            }
            else if(_UseSelfShadow && _FaceMaterial)
            {
                #if defined(faceishadow)
                    float face_shadow = shadow_rate_face(uv, light);
                    float stencil_mask = facemap.y;
                    face_shadow = face_shadow - stencil_mask;
                    out_color.xyzw = face_shadow;
                    clip(face_shadow - 0.01f);
                    return out_color;
                #else
                    clip(-1);
                #endif
            } 
            else
            {
                clip(-1);
            }   
    #endif

    #if defined (_IS_PASS_BASE)
        if(_EnableParticleSwirl)
        {
            swirl_dissolve(i, out_color);
        }
        // lighting
        float3 GI_color = DecodeLightProbe(normal);
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;
        
        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);
        // ================================================================================================ //
        #if defined(use_rimlight)
            // rim light : 
            float rimwidth = _RimWidth; 
            float2 rimoffset = _RimOffset;
            float2 esrimoffset = _ES_RimLightOffset;
            if(isVR())
            {
                rimwidth = 0.5f;
                rimoffset = 0.0f;
                esrimoffset = 0.0f;
            }
            // populate arrays with material values 
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

            float4 rim_values[8] = // x = width, y = softness, z = type, w = dark
            {
                float4(_RimWidth0, _RimEdgeSoftness0, _RimType0, saturate(_RimDark0)),
                float4(_RimWidth1, _RimEdgeSoftness1, _RimType0, saturate(_RimDark1)),
                float4(_RimWidth2, _RimEdgeSoftness2, _RimType0, saturate(_RimDark2)),
                float4(_RimWidth3, _RimEdgeSoftness3, _RimType0, saturate(_RimDark3)),
                float4(_RimWidth4, _RimEdgeSoftness4, _RimType0, saturate(_RimDark4)),
                float4(_RimWidth5, _RimEdgeSoftness5, _RimType0, saturate(_RimDark5)),
                float4(_RimWidth6, _RimEdgeSoftness6, _RimType0, saturate(_RimDark6)),
                float4(_RimWidth7, _RimEdgeSoftness7, _RimType0, saturate(_RimDark7)),
            }; // they have unused id specific rim widths but just in case they do end up using them in the future ill leave them be here

            if(_UseMaterialValuesLUT) 
            {    
                rim_values[curr_region].yzw = lut_rimval.yxz; 
            }

            float2 screen_pos = i.ss_pos.xy / i.ss_pos.w;
            float3 wvp_pos = mul(UNITY_MATRIX_VP, ws_pos);


            // in order to hide any weirdness at far distances, fade the rim by the distance from the camera
            float camera_dist = saturate(1.0f / distance(_WorldSpaceCameraPos.xyz, ws_pos));
            float fov = extract_fov();
            fov = clamp(fov, 0, 150);
            float range = fov_range(0, 180, fov);
            float width_depth = camera_dist / range;

            rimwidth = rimwidth * 0.25f;

            float rim_width = lerp(rimwidth * 0.5f, rimwidth * 0.45f, range) * width_depth;

            if(isVR())
            {
                rim_width = rim_width * 0.66f;
            }
            // sample depth texture, this will be the base
            float org_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen_pos.xy), screen_pos);

            float rim_side = (ws_pos.z * -vs_normal.x) - (ws_pos.x * -vs_normal.z);
            rim_side = (rim_side > 0.0f) ? 0.0f : 1.0f;
            

            // create offset screen uv using rim width value and view space normals for offset depth texture
            float2 offset_uv = esrimoffset.xy - rimoffset.xy;
            offset_uv.x = lerp(offset_uv.x, -offset_uv.x, rim_side);
            float2 offset = ((rim_width * vs_normal) * 0.0055f);
            offset_uv.x = screen_pos.x + ((offset_uv.x * 0.01f + offset.x));
            offset_uv.y = screen_pos.y + (offset_uv.y * 0.01f + offset.y);

            // sample depth texture using offset uv
            float offset_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, offset_uv.xy), offset_uv);

            float rim_depth = (offset_depth - org_depth);
            rim_depth = pow(rim_depth, rim_values[curr_region].w); 
            rim_depth = smoothstep(0.0f, rim_width, rim_depth);

            rim_depth = rim_depth * saturate(lerp(1.0f, lightmap.r, _RimLightMode));

            if(_EnableRimLight) rim_light = (rim_color[curr_region].xyz * rim_depth * _Rimintensity) * _ES_Rimintensity * max(0.5f, camera_dist) * saturate(vface);
            #if defined(can_shift)
                if(_EnableRimHue) rim_light.xyz = hue_shift(rim_light.xyz, curr_region, _RimHue, _RimHue2, _RimHue3, _RimHue4, _RimHue5, _RimHue6, _RimHue7, _RimHue8, _GlobalRimHue, _AutomaticRimShift, _ShiftRimSpeed, rim_mask);
                // rim_light.xyz = rim_light * saturate(diffuse.xyz * 5.0f);
            #endif
        #else
            rim_light = (float3)0.0f;
        #endif
        // ================================================================================================ //
        // self shadowing 
        float unity_shadow = 1.0f;
        #if defined(self_shading)
            unity_shadow = SHADOW_ATTENUATION(i);
            unity_shadow = smoothstep(0.f, 1.f, unity_shadow);

            if(!_UseSelfShadow) 
            {
                unity_shadow = 1.f;
            }

        #endif

        // ================================================================================================ //
        // get emissive area
        float4 emission = diffuse.xyzw;
        #if defined(use_emission)
            if( _EnableEmission == 2)
            {
                emission.w = emistex.x;
            }
            emis_area = (emission.w - _EmissionThreshold) / max(0.001f, 1.0f - _EmissionThreshold);
            emis_area = (_EmissionThreshold < emission.w * emistex.w) ? emis_area : 0.0f;
            emis_area = saturate(emis_area) * _EnableEmission;

            emission_color = _EmissionIntensity * (emission.xyz * _EmissionTintColor.xyz);
            #if defined(can_shift)
                if(_EnableEmissionHue) emission_color.xyz =  hue_shift(emission_color.xyz, curr_region, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _EmissionHue6, _EmissionHue7, _EmissionHue8, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, emission_mask);
            #endif
                if(_FaceMaterial)
            {
                #if defined(faceishadow)
                    float eye_emis = (facemap.x > 0.45f) && (facemap.x < 0.55f);
                    emis_area = emis_area + eye_emis;
                #endif
            }
        #endif
            
        // ================================================================================================ //
        // Material Coloring : 
        float4 mat_color[8] = 
        {
            _Color0, _Color1, _Color2, _Color3, _Color4, _Color5, _Color6, _Color7, 
        };

        if(!_HairMaterial)out_color = out_color * mat_color[material_ID];

        if(_UseCustomColors)
        {
            float alpha_tex = _AlphaTex.Sample(sampler_linear_repeat, uv).x;
            custom_coloring(diffuse, alpha_tex);
        }

        // // ================================================================================================ //
        // SHADOW AREA :
        float3 shadow_color = (float3)1.0f;
        float shadow_area = 1.0f;
        #if defined(use_shadow)
            if(_EnableShadow == 1)
            {
                shadow_area = shadow_rate((ndotl), (lightmap.y), vcol.x, _ShadowRamp, unity_shadow) ;
                // if(_BaseMaterial) shadow_area = lerp(_SelfShadowDarken, shadow_area, unity_shadow);

                // RAMP UVS 
                float2 ramp_uv = {shadow_area, ramp_ID};

                // SAMPLE RAMP TEXTURES
                float3 warm_ramp = _DiffuseRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz; 
                float3 cool_ramp = _DiffuseCoolRampMultiTex.Sample(sampler_linear_clamp, ramp_uv).xyz;

                shadow_color = lerp(warm_ramp, cool_ramp, 0.0f);
                #if defined(faceishadow)
                    if(_FaceMaterial)
                    {
                        float face_sdf_right = _FaceMap.Sample(sampler_linear_repeat, uv).w;
                        float face_sdf_left  = _FaceMap.Sample(sampler_linear_repeat, float2(1.0f - uv.x, uv.y)).w;

                        shadow_area = shadow_rate_face(uv, light);

                        shadow_color = lerp(_ShadowColor, 1.0f, shadow_area);
                    }
                #endif
                // using the ES Level adjuster 
                if (_ES_LEVEL_ADJUST_ON)
                {
                    // Determine if the material is skin, face, or hair
                    float isSkin = (material_ID < 1) ? 0.0 : 1.0;
                    isSkin = (_FaceMaterial) ? 0.0 : isSkin;
                    isSkin = (_HairMaterial) ? 1.0 : isSkin;

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
                    shadow_color.xyz = (shadow_area < 0.9f) ? skinLightColorAdjustment : skinShadowColorAdjustment;
                }

            }
        #endif

        // ================================================================================================ //
        // specular : 
        // initialize specular values because the fucking matcap uses it.. thank you 
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
            specular_color[curr_region] = lut_speccol;
            specular_values[curr_region] = lut_specval.xyz * float3(10.0f, 2.0f, 2.0f); // weird fix, not accurate to ingame code but whatever if it works it works
        }
        if(_FaceMaterial)
        {
            specular_color[curr_region] = (float4)0.0f;
        }

        specular_values[curr_region].z = max(0.0f, specular_values[curr_region].z); // why would there ever be a reason for a negative specular intensity

        #if defined(use_specular)
            if(_EnableSpecular)
            {
                specular = specular_base(shadow_area, ndoth, lightmap.z, specular_color[curr_region], specular_values[curr_region], _ES_SPColor, _ES_SPIntensity);
            }
        #endif
        // ================================================================================================ //
        #if defined(use_stocking)
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
            stock_dark_area = diffuse.xyz * stock_dark_area + (float3)-1.0f;
            stock_dark_area = stocking_tex.x * stock_dark_area + (float3)1.0f;
            float3 stock_darkened = stock_dark_area * diffuse.xyz;

            float stock_spec = (1.0f - _StockSP) * (stocking_tex.y * stock_tile);

            stock_rim = saturate(max(0.004f, pow(ndotv, _Stockpower1)) * stock_spec);

            float3 stocking = -diffuse.xyz * stock_dark_area + _Stockcolor;
            stocking = stock_rim * stocking + stock_darkened;
        #endif

        #if defined(use_matcap)
            if(_UseMatcap) matcap_color(normal, view, shadow_area, lightmap.z, uv, specular_values[curr_region].z, specular_color[curr_region], diffuse.xyz);
        #endif
        // ================================================================================================ //
        // rim shadow
        // this is distinct from the rim light, whatever it does
        // first things first, create and populate the color and value arrays 
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

        float4 rimsdw_color = (_UseMaterialValuesLUT) ? lut_rimscol : rim_shadow_color[curr_region];
        rimsdw_color.xyz = rimsdw_color * (_ES_RimShadowColor.www * _ES_RimShadowColor.xyz);

        float rim_shadow = ndotv;
        rim_shadow = 1.0f - rim_shadow;
        rim_shadow =  max(rim_shadow, 0.001);
        rim_shadow = pow(rim_shadow, _RimShadowCt);
        rim_shadow = smoothstep(rim_shadow_values[curr_region].x, rim_shadow_values[curr_region].y, rim_shadow);
        rim_shadow = rim_shadow * _RimShadowIntensity;
        rim_shadow = rim_shadow * _ES_RimShadowIntensity;
        rim_shadow = rim_shadow * 0.25f;
        rimsdw_color.xyz = rimsdw_color.xyz * 2.0f - 1.0f;
        rimsdw_color.xyz = rim_shadow * rimsdw_color.xyz + 1.0f;



        // ================================================================================================ //
        // FACE EXPRESSION MAP 
        // nose line doesnt come from the expression map but whatever, it goes here
        #if defined(faceishadow)
            float3 nose_view = view;
            // nose_view.y = nose_view.y * 0.5f;
            float nose_ndotv = max(dot(nose_view, normal), 0.0001f);
            float nose_power = max((_NoseLinePower * 0.01f) * 8.0f, 0.1f);
            nose_ndotv = pow(nose_ndotv, nose_power);

            float nose_area = facemap.z * nose_ndotv;
            nose_area = (nose_area > 0.1f) ? 1.0f : 0.0f;

            float3 expressions = 1.0f;
            
            // cheek blush
            float cheek_threshold = _ExMapThreshold < faceexp.x ? (faceexp.x - _ExMapThreshold) / (1.0f - _ExMapThreshold) : 0.0f;
            expressions = lerp((float3)1.0f, _ExCheekColor, cheek_threshold * _ExCheekIntensity);
            // shyness
            float exp_shy = faceexp.y * _ExShyIntensity;
            expressions = lerp(expressions, _ExShyColor, exp_shy);
            // shadow
            float3 exp_shadow = faceexp.z * _ExShadowIntensity;
            expressions = lerp(expressions, _ExShadowColor, exp_shadow);

            
            if(_FaceMaterial)
            {
                diffuse.xyz = lerp(diffuse.xyz, _NoseLineColor, nose_area); 
                diffuse.xyz = diffuse.xyz * expressions;
            } 
        #endif
        // ================================================================================================ //

        // bloom settings 
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

        float4 bloom_color = _mBloomColor;
        float bloom_intensity = _mBloomIntensity0;
        if(_UseMaterialValuesLUT)
        {
            bloom_color = lut_bloomval;
            bloom_intensity = lut_rimsval.z;
        }
        else
        {
            bloom_intensity = bloom_array[curr_region];
        }       
        
        
        // ================================================================================================ //
        #if defined(use_stocking)
            if(_EnableStocking) diffuse.xyz = stocking;
        #endif
        out_color = (_StarrySky && !_FaceMaterial && !_HairMaterial) ? starry_sky(diffuse, out_color, uv) : out_color * diffuse;
        if(_EnableAlphaCutoff) clip(diffuse.w - saturate(testTresh));
        #if defined(use_shadow)
            out_color.xyz = (_EnableShadow == 1) ? out_color * shadow_color : out_color; 
        #endif
        #if defined(use_specular)
            out_color.xyz = (_EnableSpecular == 1) ? out_color + specular : out_color; 
        #endif
        #if defined(can_shift)
            if(_EnableColorHue) out_color.xyz = hue_shift(out_color.xyz, curr_region, _ColorHue, _ColorHue2, _ColorHue3, _ColorHue4, _ColorHue5, _ColorHue6, _ColorHue7, _ColorHue8, _GlobalColorHue, _AutomaticColorShift, _ShiftColorSpeed, diffuse_mask);
        #endif
        #if defined(use_emission)
            if(_EnableEmission > 0) out_color.xyz = emis_area * (out_color.xyz * emission_color) + out_color.xyz;
        #endif
        #if defined(use_rimlight)
            if(!_FaceMaterial && _EnableRimLight) out_color.xyz = lerp(out_color.xyz.xyz - rim_light.xyz, out_color.xyz + rim_light.xyz, rim_values[curr_region].z);
        #endif
        if(!_FaceMaterial)
        {
            out_color.xyz = out_color.xyz * rimsdw_color.xyz;
        }

        float2 starry_mask = _SkyMask.Sample(sampler_linear_repeat, uv * _SkyMask_ST.xy + _SkyMask_ST.zw).xy + _SkyRange;
        if(_StarrySky) out_color = starry_cloak(i.ss_pos, i.view, uv, i.ws_pos, tangents, out_color);
        increase_bloom(bloom_color, bloom_intensity, out_color.xyz);
        if(!_IsTransparent && !_EnableAlphaCutoff) out_color.w = 1.0f;
        if(_EyeShadowMat) out_color = _Color;


        

        // intialize direction Vectors
        float3 up      = UnityObjectToWorldDir(_headUpVector.xyz);
        float3 forward = UnityObjectToWorldDir(_headForwardVector.xyz);
        float3 right   = UnityObjectToWorldDir(_headRightVector.xyz);

        float3 view_xz = normalize(view - dot(view, up) * up);
        float cosxz    = max(0.0f, dot(view_xz, forward));
        float alpha_a  = saturate((1.0f - cosxz) / 0.658f);

        float3 view_yz = normalize(view - dot(view, right) * right);
        float cosyz    = max(0.0f, dot(view_yz, forward));
        float alpha_b  = saturate((1.0f - cosyz) / 0.293f);

        #if defined(use_caustic)
            if(_CausToggle)
            {
                float2 caus_uv = ws_pos.xy;
                caus_uv.x = caus_uv.x + ws_pos.z; 
                if(_CausUV) caus_uv = uv;
                float2 caus_uv_a = _CausTexSTA.xy * caus_uv + _CausTexSTA.zw;
                float2 caus_uv_b = _CausTexSTB.xy * caus_uv + _CausTexSTB.zw;
                caus_uv_a = _CausSpeedA * _Time.yy + caus_uv_a;
                caus_uv_b = _CausSpeedB * _Time.yy + caus_uv_b;
                float3 caus_a = (float3)0.0f;
                float3 caus_b = (float3)0.0f;
                if(_EnableSplit)
                {
                    float caus_a_r = _CausTexture.Sample(sampler_linear_repeat, caus_uv_a + float2(_CausSplit, _CausSplit)).x;
                    float caus_a_g = _CausTexture.Sample(sampler_linear_repeat, caus_uv_a + float2(_CausSplit, -_CausSplit)).x;
                    float caus_a_b = _CausTexture.Sample(sampler_linear_repeat, caus_uv_a + float2(-_CausSplit, -_CausSplit)).x;
                    float caus_b_r = _CausTexture.Sample(sampler_linear_repeat, caus_uv_b + float2(_CausSplit, _CausSplit)).x;
                    float caus_b_g = _CausTexture.Sample(sampler_linear_repeat, caus_uv_b + float2(_CausSplit, -_CausSplit)).x;
                    float caus_b_b = _CausTexture.Sample(sampler_linear_repeat, caus_uv_b + float2(-_CausSplit, -_CausSplit)).x;
                    caus_a = float3(caus_a_r, caus_a_g, caus_a_b);
                    caus_b = float3(caus_b_r, caus_b_g, caus_b_b);
                }
                else
                {
                    caus_a = _CausTexture.Sample(sampler_linear_repeat, caus_uv_a).xxx;
                    caus_b = _CausTexture.Sample(sampler_linear_repeat, caus_uv_b).xxx;
                }

                float3 caus = min(caus_a, caus_b);  
                caus = pow(caus, _CausExp) * _CausColor * _CausInt;      
                out_color.xyz = out_color.xyz + caus;
            }   
        #endif 
        float filter = 1.0f;
        if(_StarrySky && !_StarAffectedByLight)
        {
            filter = saturate(1.0f - (_SkyMask.Sample(sampler_linear_repeat, uv * _SkyMask_ST.xy + _SkyMask_ST.zw).x + _SkyRange));
        }
        float3 light_applied_color = out_color.xyz * light_color;
        light_applied_color.xyz = light_applied_color.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
        out_color.xyz = lerp(out_color.xyz, light_applied_color, filter);
        
        out_color.xyz = out_color.xyz + fake_reflection(normal, view, starry_mask);

        #if defined(is_tonemapped)
            if(_EnableLUT)
            {
                LUT_2D(out_color.xyz, _Lut2DTexParam);
            }
        #endif

        if(_UseHeightLerp) heightlightlerp(ws_pos, out_color);

        


        if(_DebugMode && (_DebugLights == 1)) out_color.xyz = 0.0f;

    #endif
    #if defined (_IS_PASS_LIGHT)
        if(_FaceMaterial) normal = float3(0.5f, 0.5f, 1.0f);
        #if defined(POINT) || defined(SPOT)
            light = normalize(_WorldSpaceLightPos0.xyz - i.ws_pos.xyz);
        #endif
        
        // SHADOW
        // since this pass doesnt want the colors of the shadows, just use the shadow only functions: 
        ndotl = dot(normal, light);

        float3 shadow_area = (float3)1.0f;
        shadow_area = shadow_rate(ndotl, lightmap.y, i.v_col.x, 1.0f, 1.0f);
        // metalshadow = shadow_area_transition(lightmapao, vertexao, ndotl, material_id);
        #if defined(faceishadow)
            ndotl = dot(float3(0.5f, 0.5f, 1.0f), light);
            if(_FaceMaterial) shadow_area = ndotl;
        #endif

        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        float3 light_pass_color = ((diffuse.xyz * 1.0f) * _LightColor0.xyz) * atten * saturate(shadow_area);
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), _FilterLight); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
        out_color.xyz = (light_color);
        #elif defined(DIRECTIONAL)
        out_color.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
    #endif
        
    #if defined (is_stencil) // so the hair and eyes dont lose their shading
        if(_EnableStencil)
        {
            if(_FaceMaterial)
            {
                #if defined(faceishadow)
                float side_mask = 1.0f;
                if(_HairSideChoose == 1) side_mask = saturate(step(0, i.vertex.x));
                if(_HairSideChoose == 2) side_mask = saturate(step(i.vertex.x, 0));
                float stencil_mask = facemap.y;
                if(_UseDifAlphaStencil == 1) stencil_mask.x = diffuse.w;
                if(_UseDifAlphaStencil == 2) stencil_mask.x = stencil_mask.x + diffuse.w;       
                float hair_blend = max(0.02, _HairBlendSilhouette);
                clip(saturate(stencil_mask) * side_mask - hair_blend); // it is not accurate to use the diffuse alpha channel in this step
                // but it looks weird if the eye shines are specifically omitted from the stencil
                #endif
            } 
            else if(_HairMaterial)
            {
                // intialize direction Vectors
                float3 up      = UnityObjectToWorldDir(_headUpVector.xyz);
                float3 forward = UnityObjectToWorldDir(_headForwardVector.xyz);
                float3 right   = UnityObjectToWorldDir(_headRightVector.xyz);

                float3 view_xz = normalize(view - dot(view, up) * up);
                float cosxz    = max(0.0f, dot(view_xz, forward));
                float alpha_a  = saturate((1.0f - cosxz) / 0.658f);

                float3 view_yz = normalize(view - dot(view, right) * right);
                float cosyz    = max(0.0f, dot(view_yz, forward));
                float alpha_b  = saturate((1.0f - cosyz) / 0.293f);
                float hair_blend = max(0.0, _HairBlendSilhouette);
                hair_alpha = max(alpha_a, alpha_b);
                hair_alpha = (_UseHairSideFade) ? max(max(hair_alpha, hair_blend), 0.0f) : hair_blend;
                
                float side_mask = 1.0f;
                if(_HairSideChoose == 1) side_mask = saturate(step(0, i.vertex.x));
                if(_HairSideChoose == 2) side_mask = saturate(step(i.vertex.x, 0));
                hair_alpha = hair_alpha * saturate(side_mask);
                out_color.w = hair_alpha;
            }
            else
            {
                discard;
            }
        }
        else
        {
            discard;
        }
    #endif
    
    #if defined(debug_mode)
        if(_DebugMode)
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
            if(_DebugFaceExp == 1) return float4(faceexp.xxx, 1.0f);  
            if(_DebugFaceExp == 2) return float4(faceexp.yyy, 1.0f);  
            if(_DebugFaceExp == 3) return float4(faceexp.zzz, 1.0f);  
            if(_DebugFaceExp == 4) return float4(faceexp.www, 1.0f); 
            #endif
            if(_DebugMLut) // because of the nature of the mluts i had to expand the debugging like this 
            {
                float4 mlutdebug[8] =
                {
                    lut_speccol,
                    lut_specval, 
                    lut_edgecol,
                    lut_rimcol,
                    lut_rimval,
                    lut_rimscol,
                    lut_rimsval,
                    lut_bloomval
                };

                if(_DebugMLutChannel == 1) return float4(mlutdebug[_DebugMLut - 1].xxx, 1.0f);
                if(_DebugMLutChannel == 2) return float4(mlutdebug[_DebugMLut - 1].yyy, 1.0f);
                if(_DebugMLutChannel == 3) return float4(mlutdebug[_DebugMLut - 1].zzz, 1.0f);
                if(_DebugMLutChannel == 4) return float4(mlutdebug[_DebugMLut - 1].www, 1.0f);
                if(_DebugMLutChannel == 5) return float4(mlutdebug[_DebugMLut - 1].xyz, 1.0f);
                if(_DebugMLutChannel == 6) return float4(mlutdebug[_DebugMLut - 1]);
            }
            if(_DebugVertexColor == 1) return float4(i.v_col.xxx, 1.0f);
            if(_DebugVertexColor == 2) return float4(i.v_col.yyy, 1.0f);
            if(_DebugVertexColor == 3) return float4(i.v_col.zzz, 1.0f);
            if(_DebugVertexColor == 4) return float4(i.v_col.www, 1.0f);
            if(_DebugRimLight == 1) return float4(rim_light.xyz, 1.0f);
            if(_DebugNormalVector == 1) return float4(normal.xyz * 0.5f + 0.5f, 1.0f);
            if(_DebugNormalVector == 2) return float4(normal.xyz, 1.0f);
            if(_DebugTangent == 1) return float4(i.tangent.xyz, 1.0f);
            if(_DebugSpecular == 1) return float4(specular.xyz, 1.0f);
            if(_DebugEmission == 1) return float4(emis_area.xxx, 1.0f);
            if(_DebugEmission == 2) return float4(emission_color.xyz, 1.0f);
            if(_DebugEmission == 3) return float4(emission_color.xyz * emis_area, 1.0f);
            if((_DebugMaterialIDs > 0) && (_DebugMaterialIDs != 9))
            {
                curr_region = curr_region + 1.0f;
                if(_DebugMaterialIDs == curr_region)
                {
                    return (float4)1.0f;
                }
                else 
                {
                    return float4((float3)0.0f, 1.0f);
                }
            }
            if(_DebugMaterialIDs == 9)
            {
                float4 debug_color = float4(0.0f, 0.0f, 0.0f, 1.0f);
                if(curr_region == 0)
                {
                    debug_color.xyz = float3(1.0f, 0.0f, 0.0f);
                }
                else if(curr_region == 1)
                {
                    debug_color.xyz = float3(0.0f, 1.0f, 0.0f);
                }
                else if(curr_region == 2)
                {
                    debug_color.xyz = float3(0.0f, 0.0f, 1.0f);
                }
                else if(curr_region == 3)
                {
                    debug_color.xyz = float3(1.0f, 0.0f, 1.0f);
                }
                else if(curr_region == 4)
                {
                    debug_color.xyz = float3(0.0f, 1.0f, 1.0f);
                }
                else if(curr_region == 5)
                {
                    debug_color.xyz = float3(1.0f, 1.0f, 0.0f);
                }
                else if(curr_region == 6)
                {
                    debug_color.xyz = float3(1.0f, 1.0f, 1.0f);
                }
                else if(curr_region == 7)
                {
                    debug_color.xyz = float3(0.0f, 0.0f, 0.0f);
                }
                return debug_color;
            }
            if(_DebugFaceVector == 1) return float4(UnityObjectToWorldDir(_headForwardVector.xyz), 1.0f);
            if(_DebugFaceVector == 2) return float4(UnityObjectToWorldDir(_headRightVector.xyz), 1.0f);
            if(_DebugFaceVector == 3) return float4(UnityObjectToWorldDir(_headUpVector.xyz), 1.0f);
            if(_DebugHairFade == 1) return float4(hair_alpha.xxx, 1.0f); 
        } 
    #endif

    #if defined(can_dissolve)
        if(_DissoveON)
        {
            dissolve_color(ws_pos, i.dis_pos, i.dis_uv, uv, diffuse, out_color);
        }
    #endif
    return out_color;
}

float4 ps_edge(vs_out i, bool vface : SV_IsFrontFace) : SV_Target
{
    float4 ws_pos = mul(unity_ObjectToWorld, i.ws_pos);
    float2 uv  = i.uv.xy;

    float4 color_tex = _OutlineColorTex.Sample(sampler_linear_repeat, uv);

    #if defined(can_dissolve)
    if(_DissoveON)
    {
        dissolve_clip(ws_pos, i.dis_pos, i.dis_uv, uv);
    }
    #endif
    float lightmap = _LightMap.Sample(sampler_linear_repeat, uv).w;
    if(_UseCustomColors) lightmap = _AlphaTex.Sample(sampler_linear_repeat, uv).x;
    float alpha = _MainTex.Sample(sampler_MainTex, uv).w;

    // lighting
    float3 GI_color = DecodeLightProbe(normalize(i.normal));
    GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
    float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
    GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;  
    GI_color = (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
    float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
    float3 light_color = max(ambient_color, _LightColor0.rgb);

    
    int material_ID = floor(lightmap * 8.0f);

    int material = material_region(material_ID);

    float4 outline_color[8] =
    {
        _OutlineColor0,
        _OutlineColor1,
        _OutlineColor2,
        _OutlineColor3,
        _OutlineColor4,
        _OutlineColor5,
        _OutlineColor6,
        _OutlineColor7,
    };

    if(_UseMaterialValuesLUT) outline_color[material] = _MaterialValuesPackLUT.Load(float4(material_ID, 2, 0, 0));

    float4 out_color = outline_color[material];
    out_color.xyz = out_color.xyz * color_tex;
    if(_FaceMaterial) out_color = _OutlineColor;
    out_color.a = 1.0f;
    #if defined(can_shift)
        float outline_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _OutlineMaskSource);
        if(!_UseHueMask) outline_mask = 1.0f;
        if(_EnableOutlineHue) out_color.xyz = hue_shift(out_color.xyz, material, _OutlineHue, _OutlineHue2, _OutlineHue3, _OutlineHue4, _OutlineHue5, _OutlineHue6, _OutlineHue7, _OutlineHue8, _GlobalOutlineHue, _AutomaticOutlineShift, _ShiftOutlineSpeed, outline_mask);
    #endif
    out_color.xyz = out_color.xyz * light_color + GI_color;
    #if defined(can_dissolve)
        if(_DissoveON)
        {
            dissolve_color(ws_pos, i.dis_pos, i.dis_uv, uv, out_color, out_color); 
        }
    #endif
    if(i.v_col.w < 0.05f) clip(-1); // discard all pixels with the a vertex color alpha value of less than 0.05f
    // this fixes double sided meshes for hsr having bad outlines
    if(_EnableAlphaCutoff) clip(alpha - _AlphaCutoff);
    
    return out_color;
}

float4 ps_shadow(shadow_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    float4 ws_pos = mul(unity_ObjectToWorld, i.ws_pos);

    // initialize uv 
    float2 uv = (!vface) ? i.uv_a.zw : i.uv_a.xy;
    float testTresh = _AlphaTestThreshold;
    if(_IsTransparent) testTresh = 0.0f;
    float alpha = _MainTex.Sample(sampler_MainTex, uv).w;

    float4 out_color = (float4)0.0f;

    #if defined(can_dissolve)
    if(_DissoveON)
    {
        dissolve_clip(ws_pos, i.dis_pos, i.dis_uv, uv);
    }        
    #endif
    if(_EnableAlphaCutoff) clip(alpha - saturate(testTresh));
    if(i.hide) clip(-1);
    return 0.0f;
}