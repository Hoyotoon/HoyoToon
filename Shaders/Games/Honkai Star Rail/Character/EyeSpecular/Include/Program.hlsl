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
    o.color = v.color;
    float4 ss_tmp;
    ss_tmp.w = (position.y * _ProjectionParams.x) * 0.5f;
    ss_tmp.xz = position.xw * 0.5f;
    o.ss_pos.zw = position.zw;
    o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
    o.ws_pos = ws_pos;
    o.pos = o.vertex;
    o.os_pos = v.vertex;
    o.view = _WorldSpaceCameraPos.xyz - ws_pos.xyz;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_vertex_out(float2x2(v.uv.xy, v.uv1.xy), float4(o.ws_pos.xyz, 1.0f), v.vertex, o.diss_uv, o.diss_pos);
    #endif

    #if defined(_USE_NORMAL_MAP)
        o.tangent = mul((float3x3)unity_ObjectToWorld, float3(v.uv7.xy, v.uv8.x));

        o.bitangent = cross(o.normal, o.tangent) * (v.uv8.y * unity_WorldTransformParams.w);
    #else
    o.tangent = v.tangent.xyz;
    #endif
    return o;
}

forward_mask_out frag_base(vertex_out i,  bool vface : SV_IsFrontFace)
{
    float2 screen_uv = i.ss_pos.xy / i.ss_pos.w;
    // initialize output buffer, this is to just guarantee no weird initialization issues    // change which dither to use depending on if the dissolve is active or not
    #if defined(_DIRECTIONALDISSOLVE)
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
        #if defined(_USE_DITHER)
            dither(i.ss_pos, i.diss_uv.z, i.diss_pos);
        #endif
    #else
        #if defined(_USE_DITHER)
            dither(i.ss_pos);
        #endif
    #endif   

  
    float4 color = _Color;
    color.w = 1.0f;
    float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv) ;

    float4 final_color = diffuse;

    float3 view = normalize(i.view);
    float3 normal = normalize(i.normal);

    float ndotv = dot(normal, view);
    float fresnel_area = saturate((1 - abs(ndotv.x) + (-_FresnelBSI.x)) * (float(1.0) / _FresnelBSI.y));
    float3 fresnel = fresnel_area * _FresnelColor.xyz;
    fresnel.xyz = max(fresnel.xyz * _FresnelColorStrength, 0.0f);


    if(_UseMatcap)
    {
        float3 vNormal = mul(normal, (float3x3)unity_MatrixV);
        vNormal.xy = (vNormal.xy * 0.5 + 0.5) * _MainTex_ST.xy + _MainTex_ST.zw;
        float4 matcap = _MatCapTex.Sample(sampler_linear_repeat, vNormal.xy);
        color = color * matcap;
    }
    else
    {
        color = 0;
    }

    final_color.xyz = diffuse.xyz * diffuse.w + fresnel.xyz;
    final_color.xyz = (color.xyz * color.w) * _MatCapStrength.xxx + final_color.xyz;

    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif



    #if defined(_DIRECTIONALDISSOLVE)
        dissolve_outline(final_color, dis_area, dis_map);
    #endif
    

    forward_mask_out forwardOut;
    forwardOut.color = final_color;
    forwardOut.alphaMask = float4(saturate(final_color.w), saturate(final_color.w), saturate(final_color.w), saturate(final_color.w));
    return forwardOut;
}  
