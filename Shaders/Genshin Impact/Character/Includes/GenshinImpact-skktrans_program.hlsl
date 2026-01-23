vertex_output base_vertex (vertex_input v)
{
    vertex_output o = (vertex_output)0;
   
        o.vertex = UnityObjectToClipPos(v.vertex) ;
        o.screenpos = ComputeScreenPos(o.vertex);
        o.uv = float4(v.uv.xy, 0, 0);
        o.uv1 = float4(v.uv2.xy, 0, 0);
        o.uv2 = float4(v.uv3.xy, v.uv4.xy);
        float3 ws_pos =  mul(unity_ObjectToWorld, v.vertex).xyz;
        o.ws_pos.xyz = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent));
        o.color = v.color;// * _VertexColorSwitch + color_switch;
        o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal)) ; // WORLD SPACE NORMAL 
        o.view = normalize(_WorldSpaceCameraPos.xyz - ws_pos);
        o.pos = o.vertex;
        o.n_view.xyz =_WorldSpaceCameraPos.xyz - ws_pos;

        o.os_pos.xyz = normalize(mul((float3x3)unity_WorldToObject, v.vertex.xyz));
        TRANSFER_SHADOW(o)
    return o;
}

float4 base_pixel (vertex_output i, bool vface : SV_IsFrontFace ) : SV_Target
{

    float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
    dither(screen);
    float3 normal = i.normal;
    float3 view = i.view;
    float3 light = _WorldSpaceLightPos0.xyz;
    float4 output = 1;
    
    float3 lightColor = _LightColor0;
    lightColor = max(lightColor, 0.1f);
    
    screen = (i.screenpos.xy / i.screenpos.w);
    float2 outline_mask = _OutlineMaskTex.Sample(sampler_linear_repeat, i.uv).xy;
    
    #if defined(ENABLE_FRESNEL_OUTLINE)
        float3 vs_tangent = normalize(mul(normalize(i.ws_pos), (float3x3)unity_MatrixV));
        float3 vs_normal = normalize(mul(i.view, (float3x3)unity_MatrixV));
        float ndotv = 1.0f - saturate(dot(vs_normal, vs_tangent));
        ndotv = ((pow(ndotv, 5.0f) >= _FresnelOutlineStep) * outline_mask.y) * vface;        
    #endif
    
    float line_distance = 0.f;
    #if defined(ENABLE_TEXTURE_LINE_ON)
        line_distance = GetLinearZFromZDepth_WorksWithMirrors(i.pos.z/i.pos.w, screen);;
        float control = _TextureLineDistanceControl.x * line_distance + _TextureLineThickness;
        line_distance = line_distance >= _TextureLineDistanceControl.z ? 1 : 0;
        float min_distance = min(_TextureLineDistanceControl.y, 0.99f);
        control = min(control, min_distance);
        control = 1 - control;
        control = outline_mask.x - control;
        control = _TextureLineSmoothness *  line_distance + control;
        line_distance = line_distance * _TextureLineSmoothness;
        line_distance = line_distance + line_distance;
        line_distance = saturate(control / line_distance);
    #endif
    #if defined(ENABLE_FRESNEL_OUTLINE)
        line_distance = line_distance + ndotv;
    #endif
    
    
    float ndotl = dot(normal, light);
    float ndoth = dot(normal, normalize(light + view));
    
    bool shaded = ndotl >= _ShadowArea;
    float3 spec = ((saturate(ndoth) >= (1 - _SpecularRanges)) * _SpecularScales) * _SpecularColors;
    spec = shaded ? spec * _SpecularShadowScales : spec;    
    
    float4 main = _MainTex.Sample(sampler_linear_repeat, i.uv) * float4(lightColor, 1.f);
    
    float alpha = saturate(spec.x + main.w);
    #if defined(ENABLE_TEXTURE_LINE_ON)
        alpha = alpha + line_distance;
    #endif
    alpha = alpha * _MainAlpha;
    #if defined(ENABLE_TEXTURE_LINE_ON)
        float3 outline = lerp(_OutLinesColor.xyz, _OutLinesColor * main, _OutLinesColor.www);
    #endif
    
    main.xyz = (main * _MainColor) * _MainColorScaler;
    main.xyz = shaded ? main.xyz * _LightsColor.xyz : main.xyz * _ShadowsColor; 
    #if defined(ENABLE_TEXTURE_LINE_ON)
    main.xyz = lerp(main.xyz, outline, line_distance);
    #endif
    
    output.xyz = main;
    output.w = alpha;
    
    rim_light(screen, normal, view, light, output);
    
    return output;
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
// 2. THIS MUST BE ABOVE THE HULL SHADER
tess_struct patchConstantFunc (InputPatch<control_struct, 3> patch) 
{
    tess_struct f;
    // Strictly use a uniform value or a symmetric calculation to prevent edge T-junctions
    float tess = _TessValue; 
    f.edge[0] = f.edge[1] = f.edge[2] = tess;
    f.inside = tess;
    return f;
}
// 4. THE HULL SHADER
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

    // 2. THE FIX: Force consistent normal orientation
    // PN-Triangles project along the normal. If this is a backface,
    // we must ensure the normal direction is consistent with the frontface
    // so the control points (p210, etc.) are pushed in the same direction.
    
    // We use the TANGENT (the smooth welded normal) as the guide.
    float3 n0 = normalize(patch[0].tangent.xyz);
    float3 n1 = normalize(patch[1].tangent.xyz);
    float3 n2 = normalize(patch[2].tangent.xyz);

    float3 edge210 = o.p030 - o.p300;
    float3 edge021 = o.p003 - o.p030;
    float3 edge102 = o.p300 - o.p003;

    // 3. Project control points
    o.p210 = (2.0 * o.p300 + o.p030 - dot(edge210, n0) * n0) / 3.0;
    o.p120 = (2.0 * o.p030 + o.p300 - dot(-edge210, n1) * n1) / 3.0;
    o.p021 = (2.0 * o.p030 + o.p003 - dot(edge021, n1) * n1) / 3.0;
    o.p012 = (2.0 * o.p003 + o.p030 - dot(-edge021, n2) * n2) / 3.0;
    o.p102 = (2.0 * o.p003 + o.p300 - dot(edge102, n2) * n2) / 3.0;
    o.p201 = (2.0 * o.p300 + o.p003 - dot(-edge102, n0) * n0) / 3.0;

    float3 ee = (o.p210 + o.p120 + o.p021 + o.p012 + o.p102 + o.p201) / 6.0;
    float3 vv = (o.p300 + o.p030 + o.p003) / 3.0;
    o.p111 = ee + (ee - vv) * 0.5;

    // 4. Sharpness masking
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

    // 1. POSITION CALCULATION
    float3 posLinear = patch[0].vertex.xyz * u + patch[1].vertex.xyz * v + patch[2].vertex.xyz * w;
    float3 posPN = cp.p300*(u*u*u) + cp.p030*(v*v*v) + cp.p003*(w*w*w)
                 + cp.p210*3.0*(u*u)*v + cp.p120*3.0*u*(v*v)
                 + cp.p021*3.0*(v*v)*w + cp.p012*3.0*v*(w*w)
                 + cp.p102*3.0*u*(w*w) + cp.p201*3.0*(u*u)*w
                 + cp.p111*6.0*u*v*w;

    // Weighting
    float sharpnessMask = cp.n200.w * u + cp.n020.w * v + cp.n002.w * w;
    float seal = saturate((u*v + v*w + w*u) * 100.0);
    float finalWeight = _PhongWeight * sharpnessMask * seal;

    vertex_input v_in;
    v_in.vertex.xyz = lerp(posLinear, posPN, finalWeight);
    v_in.vertex.w = 1.0;

    // 2. TANGENT & NORMAL RECONSTRUCTION
    // We interpolate the SMOOTH normals (stored in tangent)
    float3 s0 = normalize(patch[0].tangent.xyz);
    float3 s1 = normalize(patch[1].tangent.xyz);
    float3 s2 = normalize(patch[2].tangent.xyz);
    float3 smoothN = normalize(s0 * u + s1 * v + s2 * w);

    // Interpolate original sharp normals
    float3 n0 = normalize(patch[0].normal.xyz);
    float3 n1 = normalize(patch[1].normal.xyz);
    float3 n2 = normalize(patch[2].normal.xyz);
    float3 sharpN = normalize(n0 * u + n1 * v + n2 * w);

    // CRITICAL: Update the tangent so base_vertex knows WHICH WAY to push the outline
    // Without this, base_vertex uses the low-poly tangent, leaving the outline behind.
    v_in.tangent.xyz = smoothN;
    v_in.tangent.w = patch[0].tangent.w;

    // Lighting normal
    v_in.normal = sharpN;

    // 3. ATTRIBUTE INTERPOLATION
    v_in.uv = patch[0].uv * u + patch[1].uv * v + patch[2].uv * w;
    v_in.uv2 = patch[0].uv2 * u + patch[1].uv2 * v + patch[2].uv2 * w;
    v_in.uv3 = patch[0].uv3 * u + patch[1].uv3 * v + patch[2].uv3 * w;
    v_in.uv4 = patch[0].uv4 * u + patch[1].uv4 * v + patch[2].uv4 * w;
    v_in.color = patch[0].color * u + patch[1].color * v + patch[2].color * w;

    return base_vertex(v_in); 
}