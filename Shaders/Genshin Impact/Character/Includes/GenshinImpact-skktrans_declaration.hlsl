#ifndef SKK_DEF
#define SKK_DEF


Texture2D _MainTex;
SamplerState sampler_linear_repeat;
float4 _MainTex_ST;

float _TessMask;
float _TessValue;
float _PhongWeight;
float _UsingDitherAlpha;
float _DitherAlpha;

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float _ES_AvatarRimWidthScale;
float _ES_AvatarRimWidth;
float4 _ES_AvatarFrontRimColor;
float _ES_AvatarFrontRimIntensity;
float4 _ES_AvatarBackRimColor;
float _ES_AvatarBackRimIntensity;

// from: https://github.com/cnlohr/shadertrixx/blob/main/README.md#best-practice-for-getting-depth-of-a-given-pixel-from-the-depth-texture
float GetLinearZFromZDepth_WorksWithMirrors(float zDepthFromMap, float2 screenUV)
{
    #if defined(UNITY_REVERSED_Z)
    zDepthFromMap = 1 - zDepthFromMap;
			
    // When using a mirror, the far plane is whack.  This just checks for it and aborts.
    if( zDepthFromMap >= 1.0 ) return _ProjectionParams.z;
    #endif

    float4 clipPos = float4(screenUV.xy, zDepthFromMap, 1.0);
    clipPos.xyz = 2.0f * clipPos.xyz - 1.0f;
    float4 camPos = mul(unity_CameraInvProjection, clipPos);
    return -camPos.z / camPos.w;
}

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

void rim_light(in float2 screen, in float3 normal, in float3 view, in float3 light, inout float4 output)
{
    float normal_offset = view.z * normal.x - (view.x * normal.z);
    normal_offset = 0.0f < normal_offset ? -1.0f : 1.0f;
    float rim_width = _ES_AvatarRimWidthScale * _ES_AvatarRimWidth;
    rim_width.x = normal_offset.x * rim_width.x;
    rim_width.x = rim_width.x * 0.0044;
    rim_width = UNITY_MATRIX_P[3][3] == 0 ? rim_width :  rim_width * 0.25;

    float org_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen.xy), screen);
    
    rim_width = rim_width / (org_depth );

    float2 depth_uv =  screen;
    depth_uv.x += rim_width;

    float rim_depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depth_uv.xy), depth_uv);
  
    rim_depth = pow(max(rim_depth - org_depth, 0.00001), 1);
    rim_depth = smoothstep(0.0, 0.5, rim_depth);

    float dark = saturate(dot(normal, light));

    float3 rim_color = saturate(output.xyz) * 2;

    float3 front_color = _ES_AvatarFrontRimColor * _ES_AvatarFrontRimIntensity;
    float3 back_color = _ES_AvatarBackRimColor * _ES_AvatarBackRimIntensity;

    rim_color =  (rim_color * front_color) * dark;

    output.xyz = rim_color * rim_depth + output.xyz;
}



float _TextureLineThickness;
float _TextureLineSmoothness;
float4 _TextureLineDistanceControl;
float4 mhy_AvatarLightDir;
float4 mhy_CharacterOverrideLightDir;
float4 _ClipPos;
float4 _ClipNormal;
float4 _mhyForwardConfig;
float _MainAlpha;
float4 _LightsColor;
float4 _ShadowsColor;
float4 _SpecularColors;
float _SpecularScales;
float _SpecularRanges;
float4 _MainColor;
float _MainColorScaler;
float _ShadowArea;
float _SpecularShadowScales;
float _DepthScaler;
float _UnderWaterDistanceDense;
float4 _OutLinesColor;
float _FresnelOutlineStep;
Texture2D _OutlineMaskTex;
#endif
