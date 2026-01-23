vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    #if defined(ENABLE_OUTLINE_ON) 
        
            // outline tangents
            float3 norm_source = _OutlineType == 1 ? v.normal : v.tangent;
            float3 outline_norm = float3(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz).xyz);
            outline_norm = mul((float3x3)UNITY_MATRIX_V, outline_norm);
            outline_norm.z = 0.009999f;
            outline_norm.xy = normalize(outline_norm).xy;

            float blue = _OutlineOffsetBlockBChannel ? 1 :  v.color.z - 0.5f;

            float4 position = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0f));
            
            float3 outline_pos = normalize(position.xyz);

            outline_pos.xyz = (outline_pos.xyz * _MaxOutlineZOffset) * 0.00999999978;
            

            float3 offset_pos = outline_pos.xyz * blue + position.xyz;
            float fov = 2.41400003 / unity_CameraProjection[1].y;
            float foveated_depth = fov * (-position.z);
            float4 scales;
            scales.xy = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustZs.xy : _OutlineWidthAdjustZs.yz;
            scales.zw = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustScales.xy : _OutlineWidthAdjustScales.yz;
            fov = (-position.z) * fov + (-scales.x);
            scales.xy = float2((-scales.x) + scales.y, (-scales.z) + scales.w);
            foveated_depth = max(scales.x, 0.001);
            fov = saturate(fov / foveated_depth);


            fov = fov * scales.y + scales.z; 

            float outline_width = ((fov * (_OutlineWidth * _OutlineCorrectionWidth)) * v.color.w) * 0.414;
            #if defined(FACE_MAP_NEW_ON) 
            float tex = _FaceMapTex.SampleLevel(sampler_linear_repeat, v.uv.xy, 0).z;
            outline_width = outline_width * tex;
            #endif

            position.xy = outline_norm.xy * outline_width + offset_pos.xy;

            o.vertex = mul(UNITY_MATRIX_P, position);
            if (_OutlineType == 0) o.vertex = float4(-99, -99, -99, 1.0f);
            #if defined(is_facialuv)
            facialuv_vertex(o.vertex.xyz, v.uv3.xy);
            #endif

            o.screenpos = ComputeScreenPos(o.vertex);

            o.uv = float4(v.uv.xy, 0, 0);
            o.uv1 = float4(v.uv2.xy, 0, 0);
            o.uv2 = float4(v.uv3.xy, v.uv4.xy);
    #else
        // 
       
            o.vertex = UnityObjectToClipPos(v.vertex);
        o.screenpos = ComputeScreenPos(o.vertex);
        o.uv = float4(v.uv.xy, 0, 0);
        o.uv1 = float4(v.uv2.xy, 0, 0);
        o.uv2 = float4(v.uv3.xy, v.uv4.xy);
        float3 ws_pos =  mul(unity_ObjectToWorld, v.vertex).xyz;
        o.ws_pos.xyz = ws_pos;
        o.color = v.color;// * _VertexColorSwitch + color_switch;
        o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
        o.view = normalize(_WorldSpaceCameraPos.xyz - ws_pos);
        o.pos = o.vertex;
        o.n_view.xyz = _WorldSpaceCameraPos.xyz - ws_pos;
        float3 tangent  = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
        float3 binormal = cross(tangent, o.normal) * (v.tangent.w * unity_WorldTransformParams.w);

        float3 viewTangent = float3(
            dot(o.view, tangent),
            dot(o.view, binormal),
            dot(o.view, o.normal)
        );
    #if defined(is_paimon)
        o.n_view.xyz = viewTangent;
    #endif
        #if defined(FACE_MAP_NEW_ON)
            face_angle_extract(_WorldSpaceLightPos0.xyz, v.uv.xy, o.faceangle.xyz);
        #endif
        #if defined(is_facialuv)
        facialuv_vertex(o.vertex.xyz, o.uv2.xy);
        #endif
        o.os_pos.xyz = normalize(mul((float3x3)unity_WorldToObject, v.vertex.xyz));
        TRANSFER_SHADOW(o)
    #endif
    return o;
}

float4 base_pixel (vertex_output i, bool vface : SV_IsFrontFace ) : SV_Target
{
    #if defined(ENABLE_OUTLINE_ON) 
        float2 uv = i.uv;
        float lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv).w;
        int currentMat = materialID(lightmap);
        float4 color;
        float3 lightColor = _LightColor0;
        lightColor = max(lightColor, 0.1f);
        get_outline(max(currentMat - 1, 0), color);

        // discard what needs to be first

        float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
        dither(screen);
        

            

        float4 main_tex = _MainTex.Sample(sampler_linear_repeat,  uv);

        if(_MainTexAlphaUse == 1)
        {
            clip(main_tex.w - _MainTexAlphaCutoff);
        }

        color.xyz = color.xyz * lightColor;
        
        color.xyz = lerp(color.xyz, dot(color.xyz, float3(0.04, 0.45, 0.006)), _UseSaturation);

        return color;
    #else
        // initialize inputs:
        float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
        float3 normal = normalize(i.normal);
        bool isBackFace =  false;
        #if defined(BACK_FACE_ON)
            isBackFace = _UseBackFaceUV2 ? !vface : false;
        #endif
        normal = vface ? normal : -1 * normal;
        float2 uv = isBackFace ? i.uv1 : i.uv;
        float3 view = normalize(i.view);
        float3 light = _WorldSpaceLightPos0.xyz;
        float3 lightColor = _LightColor0;
        float3 color = i.color;

        lightColor = max(lightColor, 0.1f);
        
        // initialize output: +
        float4 output; 
        
        // discard what needs to be first
        dither(screen);
        
        // sample textures
        float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv);   
        float4 light_tex = _LightMapTex.Sample(sampler_linear_repeat, uv);


   
        bool isTransparent = _MainTexAlphaUse == 1 ? true : false;
        bool isEmission = _MainTexAlphaUse == 2 ? true : false;
        bool isBlush = _MainTexAlphaUse == 3 ? true : false;

        if(isBlush)
        {
            main_tex.xyz = lerp(main_tex.xyz, _FaceBlushColor.xyz, _FaceBlushStrength * main_tex.w);
            #ifndef is_transparent
                main_tex.w = 1;
            #endif
        }
        if(isTransparent)
        {
            clip(main_tex.w - _MainTexAlphaCutoff);
            #ifndef is_transparent
            main_tex.w = 1;
            #endif
        } 
        output.xyz = main_tex;
        output.w = 1;
        #if defined(MAIN_TEX_COLORING_ON)
            main_tint(output);
        #endif
        #ifndef MATERIAL_MASK
            output.xyz = output.xyz * _Color;
        #else
            material_mask(uv, output);
        #endif
        
        // material id
        int currentMat = materialID(light_tex.w);
    
        
        bool useMap = false;
        bool isMat = false; 
        float3x3 btn; 
        #if defined(BUMP_TEXTURELINE_MAP)
            // sample normal map:
            float4 normalmap = _BumpMap.Sample(sampler_linear_repeat, uv);
            float3 tmpmap = normalmap;
            // normal mapping :
            if(_UseBumpMap) normal_map(normal, i.n_view, uv, normalmap, vface);
        #endif
        // output.xyz = normal;
              

        float ndotl = dot(normal, light) * 0.4975 + 0.5f; 
        float ndoth = dot(normal, normalize(view + light));

        // shadows now
        float3 area;
        float2 transition;
        get_shadowtransition(currentMat, transition);
        #if defined(TOON_LIGHTMAP_ON)
            #if defined(FACE_MAP_NEW_ON)
                float shadow_range;
                face_shading(i.faceangle.xyz, shadow_range);
                shadow_area(shadow_range, color.x, light_tex.y, transition, vface, isMat, area);
            #else
                shadow_area(ndotl, color.xy, light_tex.y, transition, vface, isMat, area);
            #endif
        #else
            shadow_area(ndotl, color.x, transition, vface, area);
        #endif 
        #if defined(METAL_MAT) 
            bool metal_area = light_tex.x > 0.89f;

            if(metal_area)
            {
                metal_material(normal, ndoth, light_tex.xz, area, output);
            }
            else
            {
        #endif
            // actual shadow color
            float4 warm;
            float4 cool;
            
            #if defined(SHADOW_RAMP_ON)
                get_shadowramp(currentMat, area.y, warm, cool);
            #else
                get_shadowcolor(max(currentMat - 1, 0), warm, cool);
            #endif
                
            if (!_UseCoolShadowColorOrTex)
            {
                cool = warm;
            }
            
            float3 shadowcol = lerp(cool, warm, _ES_CharacterColorTone); // later ill add the daynight lerp
            #if defined(FACE_MAP_NEW_ON)
            output.xyz = lerp(output.xyz, shadowcol * output.xyz, area.x);
            #else
            output.xyz = area.x != 0 ? saturate(shadowcol) * saturate(output.xyz) : output.xyz;
            #endif
            // output.xyz = saturate(shadowcol);

            calc_specular(currentMat, ndoth, light_tex.xz, output);

        #if defined(METAL_MAT)
            }   
        #endif

        float emission_mask = isEmission && (main_tex.w > 0.02);
        if(emission_mask)
        {
            emission_setting(currentMat, main_tex.w, output.xyz);
        }
        else {output.xyz = output.xyz * lightColor;}
        get_hit(normal, view, emission_mask, output);
        screen = (i.screenpos.xy / i.screenpos.w);
        
        output.xyz = lerp(output.xyz, dot(output.xyz, float3(0.04, 0.45, 0.006)), _UseSaturation);
        #if defined(is_paimon)
            star_cloak(uv, i.n_view, output);
        #endif
    #if defined(is_asmoday)
        asm_cloak(uv, i.uv2.xy, emission_mask, main_tex.w, output);
    #endif
    
        #if defined(FACE_MAP_NEW_ON)
        normal = normalize(i.os_pos.xyz);
        #endif
    
        rim_light(screen, normal, view, light, output);
        // output.xyz = color.y;
        return output;
    #endif
}

control_struct vert (vertex_input v)
{
    control_struct o;
    o.vertex = v.vertex;
    o.uv = v.uv;
    o.uv2 = v.uv2;
    o.uv3 = v.uv3;
    o.uv4 = v.uv4;
    o.color = v.color;
    o.normal = v.normal;
    o.tangent = v.tangent;
    return o;
}

tess_struct patchConstantFunc (InputPatch<control_struct, 3> patch) 
{
    tess_struct f;
    // Strictly use a uniform value or a symmetric calculation to prevent edge T-junctions
    float tess = _TessValue; 
    #if !defined(TESSELLATION_ON)
    tess = 1;
    #endif
    f.edge[0] = f.edge[1] = f.edge[2] = tess;
    f.inside = tess;
    return f;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[patchconstantfunc("patchConstantFunc")]
[outputcontrolpoints(3)]
control_struct hull (InputPatch<control_struct, 3> patch, uint id : SV_OutputControlPointID) 
{
    return patch[id];
}

PN_Control_Patch pn_constant_func(InputPatch<control_struct, 3> patch) {
    PN_Control_Patch o;

    // 1. Position Corners
    o.p300 = patch[0].vertex.xyz;
    o.p030 = patch[1].vertex.xyz;
    o.p003 = patch[2].vertex.xyz;

   
    float3 n0 = normalize(patch[0].tangent.xyz);
    float3 n1 = normalize(patch[1].tangent.xyz);
    float3 n2 = normalize(patch[2].tangent.xyz);

    float3 edge210 = o.p030 - o.p300;
    float3 edge021 = o.p003 - o.p030;
    float3 edge102 = o.p300 - o.p003;

    o.p210 = (2.0 * o.p300 + o.p030 - dot(edge210, n0) * n0) / 3.0;
    o.p120 = (2.0 * o.p030 + o.p300 - dot(-edge210, n1) * n1) / 3.0;
    o.p021 = (2.0 * o.p030 + o.p003 - dot(edge021, n1) * n1) / 3.0;
    o.p012 = (2.0 * o.p003 + o.p030 - dot(-edge021, n2) * n2) / 3.0;
    o.p102 = (2.0 * o.p003 + o.p300 - dot(edge102, n2) * n2) / 3.0;
    o.p201 = (2.0 * o.p300 + o.p003 - dot(-edge102, n0) * n0) / 3.0;

    float3 ee = (o.p210 + o.p120 + o.p021 + o.p012 + o.p102 + o.p201) / 6.0;
    float3 vv = (o.p300 + o.p030 + o.p003) / 3.0;
    o.p111 = ee + (ee - vv) * 0.5;

    o.n200.w = saturate(pow(dot(n0, normalize(patch[0].normal.xyz)), _TessMask));
    o.n020.w = saturate(pow(dot(n1, normalize(patch[1].normal.xyz)), _TessMask));
    o.n002.w = saturate(pow(dot(n2, normalize(patch[2].normal.xyz)), _TessMask));

    return o;
}

[domain("tri")]
vertex_output domain(tess_struct factors, OutputPatch<control_struct, 3> patch, float3 bary : SV_DomainLocation)
{
    float u = bary.x; float v = bary.y; float w = bary.z;
    PN_Control_Patch cp = pn_constant_func(patch);
    
    float3 posLinear = patch[0].vertex.xyz * u + patch[1].vertex.xyz * v + patch[2].vertex.xyz * w;
    float3 posPN = cp.p300*(u*u*u) + cp.p030*(v*v*v) + cp.p003*(w*w*w)
                 + cp.p210*3.0*(u*u)*v + cp.p120*3.0*u*(v*v)
                 + cp.p021*3.0*(v*v)*w + cp.p012*3.0*v*(w*w)
                 + cp.p102*3.0*u*(w*w) + cp.p201*3.0*(u*u)*w
                 + cp.p111*6.0*u*v*w;
    
    float weight = _PhongWeight;
    #ifndef TESSELLATION_ON
    weight = 0.f    ;
    #endif
    float sharpnessMask = cp.n200.w * u + cp.n020.w * v + cp.n002.w * w;
    float seal = saturate((u*v + v*w + w*u) * 100.0);
    float finalWeight = _PhongWeight * sharpnessMask * seal;

    vertex_input v_in;
    v_in.vertex.xyz = lerp(posLinear, posPN, finalWeight);
    v_in.vertex.w = 1.0;

    float3 s0 = normalize(patch[0].tangent.xyz);
    float3 s1 = normalize(patch[1].tangent.xyz);
    float3 s2 = normalize(patch[2].tangent.xyz);
    float3 smoothN = normalize(s0 * u + s1 * v + s2 * w);

    float3 n0 = normalize(patch[0].normal.xyz);
    float3 n1 = normalize(patch[1].normal.xyz);
    float3 n2 = normalize(patch[2].normal.xyz);
    float3 sharpN = normalize(n0 * u + n1 * v + n2 * w);

    v_in.tangent.xyz = smoothN;
    v_in.tangent.w = patch[0].tangent.w;

    v_in.normal = normalize(lerp(sharpN, smoothN, finalWeight));

    v_in.uv = patch[0].uv * u + patch[1].uv * v + patch[2].uv * w;
    v_in.uv2 = patch[0].uv2 * u + patch[1].uv2 * v + patch[2].uv2 * w;
    v_in.uv3 = patch[0].uv3 * u + patch[1].uv3 * v + patch[2].uv3 * w;
    v_in.uv4 = patch[0].uv4 * u + patch[1].uv4 * v + patch[2].uv4 * w;
    v_in.color = patch[0].color * u + patch[1].color * v + patch[2].color * w;

    return base_vertex(v_in); 
}
//
// vertex_output edge_vertex (vertex_input v)
// {
//     vertex_output o = (vertex_output)0;
//     #if defined(ENABLE_OUTLINE_ON)
//     // outline tangents
//     float3 outline_norm = float3(mul((float2x2)unity_ObjectToWorld, v.tangent.xy), 0.01f);
//     outline_norm.xy = normalize(outline_norm.xy);
//     outline_norm = mul((float3x3)unity_MatrixV, outline_norm);
//
//     float blue = _OutlineOffsetBlockBChannel ? 1 :  v.color.z - 0.5f;
//
//     float4 position = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0f));
//     
//     float3 outline_pos = normalize(position.xyz);
//
//     outline_pos.xyz = (outline_pos.xyz *_MaxOutlineZOffset) * 0.00999999978;
//     
//
//     float3 offset_pos = outline_pos.xyz * blue + position.xyz;
//     float fov = 2.41400003 / unity_CameraProjection[1].y;
//     float foveated_depth = fov * (-position.z);
//     float4 scales;
//     scales.xy = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustZs.xy : _OutlineWidthAdjustZs.yz;
//     scales.zw = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustScales.xy : _OutlineWidthAdjustScales.yz;
//     fov = (-position.z) * fov + (-scales.x);
//     scales.xy = float2((-scales.x) + scales.y, (-scales.z) + scales.w);
//     foveated_depth = max(scales.x, 0.001);
//     fov = saturate(fov / foveated_depth);
//
//
//     fov = fov * scales.y + scales.z; 
//
//     float outline_width = ((fov * (_OutlineWidth * _OutlineCorrectionWidth)) * v.color.w) * 0.414;
//     #if defined(FACE_MAP_NEW_ON) 
//     float tex = _FaceMapTex.SampleLevel(sampler_linear_repeat, v.uv.xy, 0).z;
//     outline_width = outline_width * tex;
//     #endif
//
//     position.xy = outline_norm.xy * outline_width + offset_pos.xy;
//
//     o.vertex = mul(UNITY_MATRIX_P, position);
//     #if defined(is_facialuv)
//     facialuv_vertex(o.vertex.xyz, v.uv3.xy);
//     #endif
//
//     o.screenpos = ComputeScreenPos(o.vertex);
//
//     o.uv = float4(v.uv.xy, 0, 0);
//     o.uv1 = float4(v.uv2.xy, 0, 0);
//     o.uv2 = float4(v.uv3.xy, v.uv4.xy);
//     #else
//     o.vertex = 0;
//     #endif
//
//     return o;
// }
//
// float4 edge_pixel (vertex_output i) : SV_Target
// {
//     float2 uv = i.uv;
//     float lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv).w;
//     int currentMat = materialID(lightmap);
//     float4 color;
//     float intensity;
//     float3 lightColor = _LightColor0;
//     lightColor = max(lightColor, 0.1f);
//     get_outline(max(currentMat - 1, 0), color, intensity);
//
//     // discard what needs to be first
//
//     float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
//     dither(screen);
//     #ifndef is_facialuv
//         #if defined(FACE_MAP_NEW_ON)
//             npc_eye(i.uv2.xy);
//         #endif
//     #endif
//
//     float4 main_tex = _MainTex.Sample(sampler_linear_repeat,  uv);
//
//     if(_MainTexAlphaUse == 1)
//     {
//         clip(main_tex.w - _MainTexAlphaCutoff);
//     }
//
//     if((_EnableEyeMaskDraw && _DrawAlphaClipEye) && _UseEyeMask)
//     {
//         float eye_mask = _EyeMask.Sample(sampler_linear_repeat, uv).x;
//         eye_mask = _DrawAlphaClipEye ? eye_mask : 1.0f - eye_mask;
//         clip(eye_mask - 0.5f);
//     }
//     color.xyz = lerp(color, main_tex.xyz * 0.2f, intensity);
//     color.xyz = color.xyz * lightColor;
//     
//     color.xyz = lerp(color.xyz, dot(color.xyz, float3(0.04, 0.45, 0.006)), _DesaturateScale);
//
//     return color;
// }
