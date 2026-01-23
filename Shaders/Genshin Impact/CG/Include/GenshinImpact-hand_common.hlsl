float3 sample_cube_3(Texture2D tex, SamplerState smp, float4 ST, float speed_U, float speed_V, float2 uv)
{
    float2 tmp_uv = uv * ST.xy + ST.zw;
    tmp_uv.xy = _Time.yy * float2(speed_U, speed_V) + tmp_uv.xy;
    float3 tmp = tex.Sample(smp, tmp_uv.xy).xyw;
    
    return tmp;
}
float sample_cube_1(Texture2D tex, SamplerState smp, float4 ST, float speed_U, float speed_V, float2 uv)
{
    float2 tmp_uv = uv * ST.xy + ST.zw;
    tmp_uv.xy = _Time.yy * float2(speed_U, speed_V) + tmp_uv.xy;
    float3 tmp = tex.Sample(smp, tmp_uv.xy).z;
    
    return tmp;
}