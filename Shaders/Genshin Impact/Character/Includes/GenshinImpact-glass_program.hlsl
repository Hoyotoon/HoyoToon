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

float4 base_pixel ( vertex_output i) : SV_Target
{

    float2 screen = (i.screenpos.xy / i.screenpos.w) * _ScreenParams.xy;
    dither(screen);

    float4 output;
    
    float4 main = _MainTex.Sample(sampler_linear_repeat, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);
    output = main * _Color;

    weapon_dissolve_nonstandard(i.uv1, i.view, output);

    #if defined(ENABLE_CHARACTER_GLASSSPECULAR_ON)
        glass_specular(i.uv1, i.normal, i.view, output);
    #endif
    return output;
}