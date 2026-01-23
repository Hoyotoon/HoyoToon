vertex_output base_vertex(vertex_input v)
{
    vertex_output o = (vertex_output) 0.f;

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.screenpos = ComputeScreenPos(o.vertex);

    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

    float3 ws_pos   = mul(unity_ObjectToWorld, v.vertex).xyz;
    float3 view     = normalize(_WorldSpaceCameraPos.xyz - ws_pos);
    float3 tangent  = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
    float3 binormal = cross(tangent, o.normal) * (v.tangent.w * unity_WorldTransformParams.w);

    float3 viewTangent = float3(
        dot(view, tangent),
        dot(view, binormal),
        dot(view, o.normal)
    );
    
    o.parallax.xyz = viewTangent;
    
    o.uv_set_A.xy = v.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
    o.uv_set_A.zw = v.uv.xy * _ColorPaletteTex_ST.xy + _ColorPaletteTex_ST.zw;
    o.uv_set_B.xy = v.uv.xy * _StarTex_ST.xy + _StarTex_ST.zw;
    o.uv_set_B.zw = v.uv.xy * _Star02Tex_ST.xy + _Star02Tex_ST.zw;
    o.uv_set_C.xy = v.uv.xy * _NoiseTex01_ST.xy + _NoiseTex01_ST.zw;
    o.uv_set_C.zw = v.uv.xy * _NoiseTex02_ST.xy + _NoiseTex02_ST.zw;
    o.uv_set_D.xy = v.uv.xy * _ConstellationTex_ST.xy + _ConstellationTex_ST.zw;
    o.uv_set_D.zw = v.uv.xy * _CloudTex_ST.xy + _CloudTex_ST.zw;
    o.heights.x = _StarHeight - 1.0;
    o.heights.y = _Star02Height - 1.0;
    o.heights.z = _ConstellationHeight - 1.0;
    o.heights.w = _CloudHeight - 1.0;
    return o;
}

float4 base_pixel(vertex_output i) : SV_Target
{
    float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
    dither(screen);

    // Normalize parallax for depth offset calculations
    float4 parallax_norm = normalize(i.parallax).xyxy;

    // Sample main texture
    float4 main_tex = _MainTex.Sample(sampler_linear_repeat, i.uv_set_A.xy);

    // Star 01 sampling with parallax offset
    float time_offset_1 = _Time.y * _Star01Speed;
    float2 star01_uv = parallax_norm.xy * (i.heights.x * -0.1) + float2(i.uv_set_B.x, time_offset_1 + i.uv_set_B.y);
    float star01_val = _StarTex.Sample(sampler_linear_repeat, star01_uv).x;

    // Star 02 sampling with parallax offset
    float2 star02_uv = parallax_norm.zw * (i.heights.y * -0.1) + float2(i.uv_set_B.z, time_offset_1 * 0.5 + i.uv_set_B.w);
    float star02_val = _Star02Tex.Sample(sampler_linear_repeat, star02_uv).y;
    float star_combined = (star01_val + star02_val) * _StarBrightness;

    // Color palette sampling
    float2 palette_uv = float2(_Time.y * _ColorPalletteSpeed + i.uv_set_A.z, i.uv_set_A.w);
    float3 palette_color = _ColorPaletteTex.Sample(sampler_linear_repeat, palette_uv).xyz;

    // Noise sampling
    float2 noise01_uv = _Time.yy * _Noise01Speed + i.uv_set_C.xy;
    float2 noise02_uv = _Time.yy * _Noise02Speed + i.uv_set_C.zw;
    float noise_val = _NoiseTex01.Sample(sampler_linear_repeat, noise01_uv).x * _NoiseTex02.Sample(sampler_linear_repeat, noise02_uv).x;

    // Constellation sampling with noise offset
    float2 const_uv = parallax_norm.xy * (i.heights.z * -0.1) + i.uv_set_D.xy;
    float3 const_color = _ConstellationTex.Sample(sampler_linear_repeat, const_uv).xyz;

    // Cloud sampling with noise offset
    float2 cloud_uv = noise_val * _Noise03Brightness + i.uv_set_D.zw;
    cloud_uv = parallax_norm.zw * (i.heights.w * -0.1) + cloud_uv;
    float cloud_val = _CloudTex.Sample(sampler_linear_repeat, cloud_uv).x * _CloudBrightness;

    // Composite colors
    float3 result = palette_color * star_combined;
    result = noise_val * result;
    result = result * main_tex.w + main_tex.xyz;
    result = const_color * _ConstellationBrightness + result;
    result = palette_color * cloud_val * main_tex.w + result;

    return float4(result, 1.0);
 
}

vertex_output edge_vertex(vertex_input v)
{
    vertex_output o = (vertex_output) 0.f;

    if(_OutlineOn)
    {
        float4 position = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0f));
        float3 outline_pos = normalize(position.xyz);

        float3 outline_norm = float3(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz).xyz);
        outline_norm = mul((float3x3)UNITY_MATRIX_V, outline_norm);
        outline_norm.z = 0.009999f;
        outline_norm.xyz = normalize(outline_norm).xyz;

        float fov = 2.414f / unity_CameraProjection[1].y;
        float foveated_depth = fov * (-position.z);
        float4 scales;
        scales.xy = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustZs.xy : _OutlineWidthAdjustZs.yz;
        scales.zw = (bool(foveated_depth<_OutlineWidthAdjustZs.y)) ? _OutlineWidthAdjustScales.xy : _OutlineWidthAdjustScales.yz;
        fov = (-position.z) * fov + (-scales.x);
        scales.xy = float2((-scales.x) + scales.y, (-scales.z) + scales.w);
        foveated_depth = max(scales.x, 0.001);
        fov = saturate(fov / foveated_depth);

        fov = fov * scales.y + scales.z; 

        float width = (((fov * (_OutlineWidth * _OutlineCorrectionWidth)) * 100) * _Scale) * 0.414250195f;

        float3 pos_offset = (outline_pos * _MaxOutlineZOffset) * _Scale;
        position.xyz = pos_offset * (v.color.z - 0.5f) + position;
        position.xy = outline_norm.xy * width + position.xy;

        o.screenpos = ComputeScreenPos(o.vertex);
        o.color = _OutlineColor; 

        o.vertex = mul(UNITY_MATRIX_P, position);
    }

    return o;
}

float4 edge_pixel(vertex_output i) : SV_Target
{
    return i.color;
}