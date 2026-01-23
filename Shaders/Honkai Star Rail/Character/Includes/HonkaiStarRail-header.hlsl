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
Texture2D _HairMaskRT;

float _IsYup;

// alpha
float _EnableAlphaCutoff;
float _AlphaCutoff;
float _AlphaTestThreshold;

float _ShadowBoost;
float _ShadowBoostVal;

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float _HairBlendSilhouette;

Texture2D _DissolveMap;
Texture2D _DissolveMask;
float _dissolvegroup;
float _DissoveON;
float _DissolveShadowOff;
float _DissolveRate;
float4 _DissolveST;
float4 _DistortionST;
float _DissolveDistortionIntensity;
float _DissolveOutlineSize1;
float _DissolveOutlineSize2;
float _DissolveOutlineOffset;
float4 _DissolveOutlineColor1;
float4 _DissolveOutlineColor2;
float _DissoveDirecMask;
float _DissolveMapAdd;
float4 _DissolveOutlineSmoothStep;
float _DissolveUV;
float4 _DissolveUVSpeed;
float4 _DissolveComponent;
float4 _DissolvePosMaskPos;
float _DissolvePosMaskWorldON;
float4 _DissolvePosMaskRootOffset;
float _DissolvePosMaskFilpOn;
float _DissolvePosMaskOn;
float _DissolveMaskUVSet;
float _DissolveUseDirection;
float4 _DissolveCenter;
float4 _DissolveDiretcionXYZ;
float _DissolvePosMaskGlobalOn;
float4 _ES_EffCustomLightPosition;


// lighting
float4 _FakeFogColor;
float _FakeFogDensity;
float _FakeFogHeightFalloff;
float _FakeFogStartHeight;
float _FakePointLightNum;
float4 _FakePointLight0Color;
float4 _FakePointLight0Pos;
float _FakePointLight0Intensity;
float _FakePointLight0AttenuationRadius;
float4 _FakePointLight1Color;
float4 _FakePointLight1Pos;
float _FakePointLight1Intensity;
float _FakePointLight1AttenuationRadius;
float4 _FakePointLight2Color;
float4 _FakePointLight2Pos;
float _FakePointLight2Intensity;
float _FakePointLight2AttenuationRadius;
float _UseFakeDirectionalLight;
float4 _FakeDirectionalLightRotation;
float _FakePointLight0;
float _FakePointLight1;
float _FakePointLight2;

// speculalr
// specular color
float4 _SpecularColor0;
float4 _SpecularColor1;
float4 _SpecularColor2;
float4 _SpecularColor3;
float4 _SpecularColor4;
float4 _SpecularColor5;
float4 _SpecularColor6;
float4 _SpecularColor7;
// specular shininess
float _SpecularShininess0;
float _SpecularShininess1;
float _SpecularShininess2;
float _SpecularShininess3;
float _SpecularShininess4;
float _SpecularShininess5;
float _SpecularShininess6;
float _SpecularShininess7;
// specular roughness
float _SpecularRoughness0;
float _SpecularRoughness1;
float _SpecularRoughness2;
float _SpecularRoughness3;
float _SpecularRoughness4;
float _SpecularRoughness5;
float _SpecularRoughness6;
float _SpecularRoughness7;
//  specular intensity
float _SpecularIntensity0;
float _SpecularIntensity1;
float _SpecularIntensity2;
float _SpecularIntensity3;
float _SpecularIntensity4;
float _SpecularIntensity5;
float _SpecularIntensity6;
float _SpecularIntensity7;

float _HideCharaParts;
int _ShowPartID;

float _ES_CharacterToonRampMode;
float _ES_RimLightWidth;
float4 _ES_RimLightOffset;
float _ES_RimLightAddMode;
float4 _ES_RimShadowColor;
float _ES_RimShadowIntensity;;
// rim
float _RimLightMode;
float _RimLight;
float _RimWidth0;
// rim colors
float4 _RimColor0;
float4 _RimColor1;
float4 _RimColor2;
float4 _RimColor3;
float4 _RimColor4;
float4 _RimColor5;
float4 _RimColor6;
float4 _RimColor7;
float _RimEdgeSoftness0;
float _RimEdgeSoftness1;
float _RimEdgeSoftness2;
float _RimEdgeSoftness3;
float _RimEdgeSoftness4;
float _RimEdgeSoftness5;
float _RimEdgeSoftness6;
float _RimEdgeSoftness7;
float _RimType0;
float _RimType1;
float _RimType2;
float _RimType3;
float _RimType4;
float _RimType5;
float _RimType6;
float _RimType7;
float _RimDark0;
float _RimDark1;
float _RimDark2;
float _RimDark3;
float _RimDark4;
float _RimDark5;
float _RimDark6;
float _RimDark7;
float _Rimintensity;
float _RimIntensity;
float _RimFeatherWidth;
float _RimWidth;
float _RimEdge;
float2 _RimOffset;
float4 _FresnelColor;
float4 _FresnelBSI;
float _FresnelColorStrength;


float _ES_LEVEL_ADJUST_ON;
float4 _ES_LevelSkinLightColor;
float4 _ES_LevelSkinShadowColor;
float4 _ES_LevelHighLightColor;
float4 _ES_LevelShadowColor;
float _ES_LevelShadow;
float _ES_LevelMid;
float _ES_LevelHighLight;

float _ES_LevelEyeShadowIntensity;

float4 _ES_SPColor;
float _ES_SPIntensity;
float _UseHeightLerp;
float4 _CharaWorldSpaceOffset;
float _ES_HeightLerpBottom;
float _ES_HeightLerpTop;
float4 _ES_HeightLerpBottomColor;
float4 _ES_HeightLerpMiddleColor;
float4 _ES_HeightLerpTopColor;

float _UsingDitherAlpha;
float _UsingDitherAlphaArt;
float _DitherAlpha;
float _DITHER_FADE_IN;


float _IsVRC;
#endif