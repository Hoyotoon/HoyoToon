vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    // 
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);
    o.uv = float4(v.uv.xy, 0, 0);
    o.uv1 = float4(v.uv2.xy, 0, 0);
    o.uv2 = float4(v.uv3.xy, v.uv4.xy);
    o.color = v.color;// * _VertexColorSwitch + color_switch;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
    
    float3 ws_pos =  mul(unity_ObjectToWorld, v.vertex).xyz;

    o.view.xyz = normalize(_WorldSpaceCameraPos.xyz - ws_pos);

    return o;
}

float4 base_pixel ( vertex_output i, bool vface : SV_IsFrontFace ) : SV_Target
{
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
    lightColor = max(lightColor, 0.1f);
    float3 color = i.color;

    float4 output;

    dither(screen);

    bool useMap = false;
    bool isMat = false; 
    
    float4 main_tex = _MainTex.Sample(sampler_linear_repeat, uv);   
    float4 light_tex = _LightMapTex.Sample(sampler_linear_repeat, uv);
    int currentMat = materialID(light_tex.w);


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
        clip(_MainTexAlphaCutoff - main_tex.w);
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
    #if defined(BUMP_TEXTURELINE_MAP)
        // sample normal map:
        float4 normalmap = _BumpMap.Sample(sampler_linear_repeat, uv);
        // normal mapping :
        float3x3 tbn;
        if(_UseBumpMap) normal_map(normal, i.view, uv, normalmap, tbn);
    #endif

    float ndotl = dot(normal, _WorldSpaceLightPos0) * 0.5 + 0.5f; 
    float ndoth = dot(normal, normalize(view + light));
    float indoth = dot(normal, normalize(light * -1 + view));

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
        shadow_area(ndotl, color.x, light_tex.y, transition, vface, isMat, area);
    #endif
    #else
        shadow_area(ndotl, color.x, transition, vface, area);
    #endif 

    #if defined(METAL_MAT)
        bool metal_area = light_tex.x > 0.89f;

        if(metal_area)
        {
            metal_material(normal, ndoth, indoth, light_tex.xz, area, output);
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
        
        float3 shadowcol = warm; // later ill add the daynight lerp
        #if defined(FACE_MAP_NEW_ON)
        output.xyz = lerp(output.xyz, shadowcol * output.xyz, area.x);
        #else
        output.xyz = area.x != 0 ? shadowcol * output.xyz : output.xyz;
        #endif

        calc_specular(currentMat, ndoth, light_tex.xz, output);

    #if defined(METAL_MAT)
        }   
    #endif
    if(isEmission && (main_tex.w > 0.020)) {emission_setting(currentMat, main_tex.w, output.xyz);}
    else {output.xyz = output.xyz * lightColor;}

    screen = (i.screenpos.xy / i.screenpos.w);
    rim_light(screen, normal, view, light, output);
    float emis_mask = isEmission && (main_tex.w > 0.020);
    weapon_dissolve(uv, i.uv1, normal, view,emis_mask,  output);
    return output;
}

vertex_output edge_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
    // outline tangents
    float3 outline_norm = float3(mul((float2x2)unity_ObjectToWorld, v.tangent.xy), 0.01f);
    outline_norm.xy = normalize(outline_norm.xy);
    outline_norm = mul((float3x3)unity_MatrixV, outline_norm);

    float blue =  v.color.z - 0.5f;

    float4 position = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0f));
    
    float3 outline_pos = normalize(position.xyz);

    outline_pos.xyz = (outline_pos.xyz *_MaxOutlineZOffset) * 0.00999999978;
    

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

    float outline_width = ((fov * (_OutlineWidth * _Scale)) * v.color.w) * 0.414;
    // #if defined(FACE_MAP_NEW_ON) 
    //     float tex = _FaceMapTex.SampleLevel(sampler_linear_repeat, v.uv.xy, 0).z;
    //     outline_width = outline_width * tex;
    // #endif

    position.xy = outline_norm.xy * outline_width + offset_pos.xy;

    o.vertex = mul(UNITY_MATRIX_P, position);


    o.screenpos = ComputeScreenPos(o.vertex);

    o.uv = float4(v.uv.xy, 0, 0);
    o.uv1 = float4(v.uv2.xy, 0, 0);
    o.uv2 = float4(v.uv3.xy, v.uv4.xy);

    return o;
}

float4 edge_pixel (vertex_output i) : SV_Target
{
    float2 uv = i.uv;
    float lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv).w;
    int currentMat = materialID(lightmap);
    float4 color;
    float intensity;
    float3 lightColor = _LightColor0;
    lightColor = max(lightColor, 0.1f);
    get_outline(max(currentMat - 1, 0), color, intensity);

    // discard what needs to be first

    float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
    dither(screen);

    float4 main_tex = _MainTex.Sample(sampler_linear_repeat,  uv);

    if(_MainTexAlphaUse == 1)
    {
        clip(main_tex.w - _MainTexAlphaCutoff);
    }

    
    color.xyz = lerp(color, main_tex.xyz * 0.2f, intensity);
    color.xyz = color.xyz * lightColor;
    
    weapon_outline_dissolve(i.uv1);

    return color;
}
