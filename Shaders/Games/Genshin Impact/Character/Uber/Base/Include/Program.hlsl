vertex_out vert_base (vertex_in v)
{
    vertex_out o = (vertex_out)0; // cast to zero so no weird initialization errors
    // ambient sensor, still dont know how this texture is created 
    float isAmbient = 0.0f;
    if(_CharacterAmbientSensorShadowOn)
    {
        float ambientTex = _CharacterAmbientSensorTex.SampleLevel(sampler_point_clamp,  _AmbientSensorUVs.xy, 0.0f).x;
        isAmbient = 0.5f < ambientTex.x ? 1.0f : 0.0f;
    }
    else
    {
        isAmbient = _CharacterAmbientSensorForceShadowOn ? 1.0 : 0.0;
    }
    
    isAmbient = mhy_CharacterOverrideLightDirInShadow ? 1.0 : isAmbient;

    o.packed.w = 0.5f <  mhy_CharacterOverrideLightDir.w ? 0.0f : isAmbient;

    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = ws_pos;

    o.view = normalize(_WorldSpaceCameraPos.xyz - ws_pos.xyz);
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

    o.uv = float4(v.uv, 0, 0);
    o.uv1 = float4(v.uv1, 0, 0);
    o.uv2 = float4(v.uv2, v.uv3);

    float4 custom_position = mul(unity_MatrixVP, ws_pos);
    o.vertex = custom_position;


    return o;
}


float4 frag_base (vertex_out i, bool vface : SV_IsFrontFace) : SV_TARGET
{
    float4 diffuse = _MainTex.Sample(sampler_linear_repeat, i.uv);
    float4 output = 1.0f;
    output.xyz = diffuse;

    return output;
}