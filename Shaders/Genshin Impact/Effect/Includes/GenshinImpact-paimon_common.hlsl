void dither( in float2 screen_pos)
{
    float4x4 identity;
    identity[0] = float4(1.0,0.0,0.0,0.0);
    identity[1] = float4(0.0,1.0,0.0,0.0);
    identity[2] = float4(0.0,0.0,1.0,0.0);
    identity[3] = float4(0.0,0.0,0.0,1.0);

    float4x4 bayer;
    bayer[0] = float4(1.0, 13.0, 4.0, 16.0);
    bayer[1] = float4(9.0, 5.0, 12.0, 8.0);
    bayer[2] = float4(3.0, 15.0, 2.0, 14.0);
    bayer[3] = float4(11.0, 7.0, 10.0, 6.0);


    uint2 screen = (uint2)screen_pos & (uint2)3;
    
    float4 lookup;
    lookup.x = dot(bayer[0], identity[screen.y]);
    lookup.y = dot(bayer[1], identity[screen.y]);
    lookup.z = dot(bayer[2], identity[screen.y]);
    lookup.w = dot(bayer[3], identity[screen.y]);

    float dither_value = dot(lookup, identity[screen.x]);
    _DitherAlpha = 1 - _DitherAlpha;
    dither_value =  _DitherAlpha * 17.0 - dither_value;
    dither_value = 0.01 - dither_value;
    bool check = (_UsingDitherAlpha && _DitherAlpha) && dither_value < 0.0f;
    if(check) discard;            

}