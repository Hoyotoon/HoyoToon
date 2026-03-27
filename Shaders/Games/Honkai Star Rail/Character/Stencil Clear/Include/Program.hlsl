inline void ApplyComputeSkinningVertex(inout vertex_in v, uint vertexID)
{
    if (_HSRComputeSkinningEnabled == 0)
        return;

    uint globalVertexIndex = vertexID + (uint)max(_HSRComputeSkinningVertexOffset, 0);
    HSRComputeSkinnedVertex skinnedVertex = _HSRComputeSkinnedVertices[globalVertexIndex];
    v.vertex = float4(skinnedVertex.pos, 1.0);
    v.normal = skinnedVertex.norm;
    v.tangent = skinnedVertex.tangent;
    v.uv7 = skinnedVertex.tangent1.xy;
    v.uv8 = skinnedVertex.tangent1.zw;
}

vertex_out vert_base(vertex_in v, uint vertexID : SV_VertexID)
{
    ApplyComputeSkinningVertex(v, vertexID);
    vertex_out o = (vertex_out)0.f;
    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    float4 position =  mul(unity_MatrixMVP, v.vertex);
    float4 custom_position = mul(unity_MatrixVP, ws_pos);
    position = (0.5f < _EnableCustomCameraOverride) ? custom_position : position;
    position = showpart(v.color.y) ? position : float4(-99.0, -99.0, -99.0, 1.0);
    o.vertex = position;

    float4 uv = float4(_UVChannelFront.xx, _UVChannelBack.xx) < 0.5f ? v.uv.xyxy * _MainTex_ST.xyxy + _MainTex_ST.zwzw : v.uv1.xyxy;
    o.uv = uv;

    float4 tmp_color_a = (1 - _VertexColorSwitch) * float4(1.0, 1.0, 0.5, 0.5);
    float4 tmp_color_b = _VertexColorSwitch;
    tmp_color_a = v.color * tmp_color_b + tmp_color_a;
    o.color = tmp_color_a;
    float4 ss_tmp;
    ss_tmp.w = (position.y * _ProjectionParams.x) * 0.5f;
    ss_tmp.xz = position.xw * 0.5f;
    o.ss_pos.zw = position.zw;
    o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
    o.ws_pos = ws_pos;
    o.pos = o.vertex;
    o.os_pos = v.vertex;
    o.view.xyz = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
    float2 tmp = normalize(mul((float3x3)unity_WorldToObject, o.view.xyz)).xy; 
    o.view.w = dot(float2(0.275999993, 0.961000025), tmp);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);

    #if defined(_DISSOLVE)
        dissolve_vertex_out(float2x2(v.uv.xy, v.uv1.xy), float4(o.ws_pos.xyz, 1.0f), v.vertex, o.diss_uv, o.diss_pos);
    #endif
    return o;
}

buffer_out frag_base(vertex_out i,  bool vface : SV_IsFrontFace)
{
    // initialize output buffer, this is to just guarantee no weird initialization issues
    buffer_out output = (buffer_out)0.0f;
    // change which dither to use depending on if the dissolve is active or not
    #if defined(_DISSOLVE)
        float dis_out;
        float dis_area;
        float dis_map;
        // first thing to take care of is the first porti on of the dissolve:
        if(_DissoveON)
        {
            if(_DissolveUseDirection) // use world space position to determine direction
            {
                dissolve_clip_world(i.ws_pos, dis_area, dis_out);
                dis_area = 0.f;
                dis_map = 1.f;
            }
            else // use the direction we calculated in the vertex shader
            {
                dissolve_clip_uv(i.diss_uv, i.diss_pos, i.uv.xy, dis_area, dis_out, dis_map);
            }
        }
        else
        { 
            dis_out = 0.0f;
            dis_area = 0.0f;
            dis_map = 1.f;
        }
        #if defined(_USINGDITHERALPAH)
            dither(i.ss_pos, i.diss_uv.z, i.diss_pos);
        #endif
    #else
        #if defined(_USINGDITHERALPAH)
            dither(i.ss_pos);
        #endif
    #endif   

    
    float4 final_color = (float4)0.0f;
    

    #if defined(_DISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif

    //output this shit
    output.forward= final_color;
    float4 diffuse =final_color;
    // output.forward.w = diffuse.r;    
    // octahedral normal encoding for small gbuffer 
    float d = dot((float3)1.f, abs(i.normal.xyz));
    float2 n_uv = i.normal.xy / d;
    float2 f_uv = (1.0 - abs(n_uv.yx)) * (float2(n_uv.x >= 0.0 ? 1.0 : -1.0, n_uv.y >= 0.0 ? 1.0 : -1.0));
    float2 enc = (i.normal.z <= 0.0) ? f_uv : n_uv;
    float2 n_final = enc * 0.5 + 0.5;
    float z_val = diffuse.z * 127.0 + 128.0;
    float z_tr = floor(z_val);
    output.normal = float4(n_final, z_tr * 1.52590219e-005, diffuse.y);
    output.ssrMask = float4(1,1,1,1);
    output.alphaMask = float4(1,1,1,1);
    return output;
}  

