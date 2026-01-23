vertex_output base_vertex(vertex_input v)
{
    vertex_output o = (vertex_output)0.f; // cast to 0 in case of no initialization
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = v.color;
    
    o.normal = normalize(mul((float3x3)unity_WorldToObject, v.normal));
    o.ws_pos = mul((float3x3)unity_ObjectToWorld, v.vertex);
    o.view = normalize(o.ws_pos - _WorldSpaceCameraPos);
    
    o.uv = v.uv;
    o.uv1 = v.uv1;
    o.uv2 = v.uv2;
    return o;   
}


float4 base_pixel(vertex_output i) : SV_Target
{
    // initalize inputs: 
    float2 uv = i.uv;
    float2 uv1 = i.uv1;
    float2 uv2 = i.uv2;
    float3 normal = normalize(i.normal);
    float3 view = normalize(i.view);
    float4 color = i.color;
    float3 ws_pos = i.ws_pos; 
    
    // initialize output: 
    float4 output = 1.0f;
    
    float2 mask_uv = uv2 * _Mask_ST.xy + _Mask_ST.zw;
    mask_uv.x = _Time.y * _Mask_Speed_U + mask_uv.x;
    float3 mask = _Mask.Sample(sampler_linear_repeat, mask_uv);
    
    
    float3 tex1 = sample_cube_3(_MainTex, sampler_linear_repeat, _Tex01_UV, _Tex01_Speed_U, _Tex01_Speed_V, uv2);
    float3 tex2 = sample_cube_3(_MainTex, sampler_linear_repeat, _Tex02_UV, _Tex02_Speed_U, _Tex02_Speed_V, uv2);
    float3 tex3 = sample_cube_3(_MainTex, sampler_linear_repeat, _Tex03_UV, _Tex03_Speed_U, _Tex03_Speed_V, uv2);
    float cube = max(tex1.y, tex2.y);
    cube = max(cube, tex3.y);
    float2 tmp = mask.xz * tex3.zx;
    cube = max(cube, mask.y);
    tmp = tex1.zx * tex2.zx + tmp;
    cube = cube - tmp.y;
    
    tmp.x = (uv.x >= _DownMaskRange) * tmp.x;
    
    float tex4 = sample_cube_1(_MainTex, sampler_linear_repeat, _Tex04_UV, _Tex04_Speed_U, _Tex04_Speed_V, uv2);
    float tex5 = sample_cube_1(_MainTex, sampler_linear_repeat, _Tex05_UV, _Tex05_Speed_U, _Tex05_Speed_V, uv2);
    
    float top = tex4 * tex5;
    float2 top_ranges = float2(top >= _TopMaskRange, top >= _TopLineRange);
    top_ranges.x = top_ranges.x ? -1.0f : -0.0f;
    top_ranges.y = top_ranges.y ? 1.0f : 0.0f;
    
    tmp.x = tmp.x * top_ranges.y;
    tmp.y = top_ranges.x + top_ranges.y;
    tmp.y = tmp.x * tmp.y;
    cube = saturate(max(cube, tmp.y));
    
    
    float3 light = lerp(_LightColor, _LineColor, cube);
    float3 shadow = lerp(_ShadowColor, _LineColor, cube);
    
    float3 pl = (-i.ws_pos.xyz) * _WorldSpaceLightPos0.www + _WorldSpaceLightPos0.xyz;
    float ndotl = 1.0f - (dot(normal, pl) * 0.5 + 0.5);
    ndotl = _ShadowWidth >= ndotl ? 1.0f : 0.0f;
    
    light = lerp(light, shadow, ndotl);
    
    float ndotv = 1.0f - dot(normal, view);
    ndotv = saturate(pow(max(ndotv, 0.00001f), _FresnelPower) * _FresnelScale);
        
    output.xyz = _FresnelColor * ndotv + light;
    
    float grad = (pow(max(uv2.y, 0.00001f), _GradientPower) * _GradientScale) * tmp.x;
    
    output.w = saturate(grad);
    
    return output;
    
}