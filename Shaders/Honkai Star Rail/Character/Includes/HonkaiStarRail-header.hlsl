#ifndef HSR_HEADER
#define HSR_HEADER
// this is where we'll be putting the 100% common and shared things between all the hsr shaders
// starting with the generic samplers
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;
SamplerState sampler_point_repeat;
SamplerState sampler_point_clamp;

// common textures now
Texture2D _MainTex;
float4 _MainTex_ST;

Texture2D _LightMap;
Texture2D _DiffuseRampMultiTex;
Texture2D _DiffuseCoolRampMultiTex;

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float _HideCharaParts;
int _ShowPartID;

float _ES_CharacterToonRampMode;
float _ES_RimLightWidth;
float2 _ES_RimLightOffset;
float _ES_RimLightAddMode;

float _ES_LEVEL_ADJUST_ON;
float4 _ES_LevelSkinLightColor;
float4 _ES_LevelSkinShadowColor;
float4 _ES_LevelHighLightColor;
float4 _ES_LevelShadowColor;
float _ES_LevelShadow;
float _ES_LevelMid;
float _ES_LevelHighLight;
float4 _ES_SPColor;
float _ES_SPIntensity;
float _UseHeightLerp;
float4 _CharaWorldSpaceOffset;
float _ES_HeightLerpBottom;
float _ES_HeightLerpTop;
float4 _ES_HeightLerpBottomColor;
float4 _ES_HeightLerpMiddleColor;
float4 _ES_HeightLerpTopColor;
#endif