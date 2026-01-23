#if defined(is_weapon_or_glass) 
void weapon_outline_dissolve(in float2 uv1)
{
    float uv_y = (_DissolveDirection_Toggle) ? 1 - uv1.y : uv1.y;
    uv_y = _WeaponDissolveValue * 2.1f + uv_y;

    float2 dissolve_uv;
    dissolve_uv.y = uv_y - 1.0;
    dissolve_uv.x = uv1.x;
    float dissolve = _WeaponDissolveTex.Sample(sampler_linear_clamp, dissolve_uv.xy).x;

    bool checka = dissolve < 0.99f;
    dissolve -= 0.001f;
    bool checkb = dissolve < 0.0f;

    if(checka && checkb) discard;
} 
#endif