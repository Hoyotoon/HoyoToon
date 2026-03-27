#ifndef HSR_BASE_DECLARATIONS
#define HSR_BASE_DECLARATIONS

CBUFFER_START(CRP_PerView)
    
	float4x4 _unity_MatrixInvP;
    float4x4 _NonJitteredProjMatrix;
    float4 _SplitUVTrans;
CBUFFER_END

struct HSRComputeSkinnedVertex
{
	float3 pos;
	float _Pad0;
	float3 norm;
	float _Pad1;
	float4 tangent;
	float4 tangent1;
};

StructuredBuffer<HSRComputeSkinnedVertex> _HSRComputeSkinnedVertices;
int _HSRComputeSkinningEnabled;
int _HSRComputeSkinningVertexOffset;



#ifndef UNITY_MATRIX_MV
    #define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
#endif

#ifndef UNITY_MATRIX_MVP
    #define UNITY_MATRIX_MVP mul(unity_MatrixVP, unity_ObjectToWorld)
#endif

#define unity_MatrixMV  UNITY_MATRIX_MV
#define unity_MatrixMVP UNITY_MATRIX_MVP

CBUFFER_START(UnityPerFrame)
    float4 _MainLightShadowData;    // x: strength, y: cascade count/offset, z: normal bias
CBUFFER_END

#define UNITY_MATRIX_M          unity_ObjectToWorld
#define UNITY_MATRIX_I_M        unity_WorldToObject
#define UNITY_PREV_MATRIX_M     unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M   unity_MatrixPreviousMI
#define UNITY_MATRIX_V          unity_MatrixV
#define UNITY_MATRIX_I_V        unity_MatrixInvV
#define UNITY_MATRIX_P          glstate_matrix_projection
#define UNITY_MATRIX_VP         unity_MatrixVP




CBUFFER_START (CRP_PerDrawEx) 
	float4                _CharacterLocalMainLightPosition;
    float4                _CharacterLocalMainLightColor;
	float4                _CharacterLocalMainLightColor1;
	float4                _CharacterLocalMainLightColor2;
	float4                _CharacterLocalMainLightDark;
	float4                _CharacterLocalMainLightDark1;
	float4                _NewLocalLightDir;
	float4                _NewLocalLightCharCenter;
	float4                _NewLocalLightStrength;
	float                _DisableCharacterLocalLight;
	float                _EnableCustomCameraOverride;
	float4                _CharacterSelfShadowAtlasRect;
	float                _CharacterSelfShadowSliceIndex;
	float                _CharacterSelfShadowValid;
	float2               _PadCharacterSelfShadow;
CBUFFER_END

CBUFFER_START(CRP_PassMisc_CRPBuildin)
    float4 _CascadeShadowSplitSpheres0;
    float4 _CascadeShadowSplitSpheres1;
    float4 _CascadeShadowSplitSpheres2;
    float4 _CascadeShadowSplitSpheres3;
    float4 _CascadeShadowSplitSphereRadii;
    float4 _MainLightShadowParams; // x: strength, y: bias
    float4 _MainLightShadowmapSize; // x: 1/w, y: 1/h, z: w, w: h
    float _MainLightShadowCascadeCount;
    float3 _PadShadowCB;
    float4x4 _MainLightWorldToShadowArr[5];
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
    float _UVChannelFront;
    float _UVChannelBack;
    int _HideCharaParts;
    int _ShowPartID;
    int4 _VertexColorSwitch;
    float4 _MainTex_ST;
    float _DissoveON;
    float _DissolveRate;
    float4 _DissolveST;
    float4 _DistortionST;
    float _DissolveDistortionIntensity;
    float _DissoveDirecMask;
    float _DissolveMapAdd;
    float _DissolveUV;
    float4 _DissolveUVSpeed;
    float4 _DissolveComponent;
    float4 _DissolvePosMaskPos;
    float _DissolvePosMaskWorldON;
    float4 _DissolvePosMaskRootOffset;
    float _DissolvePosMaskFilpOn;
    float _DissolvePosMaskOn;
    float _DissolveMaskUVSet;
    float _DissolvePosMaskGlobalOn;
    int _UsingDitherAlpha;
    int _UsingDitherAlphaArt;
    float _DitherAlpha;
    int _DITHER_FADE_IN;
    float4 _EyeShadowColor;
CBUFFER_END

CBUFFER_START(RPGEnv_PerMainCamera)
	float _GlobalOneMinusAvatarIntensity;
	float3 _XPad0;
	float3 _ES_MonsterLightDir;
	float _ES_Indoor;
	float _ES_TransitionRate;
	float _ES_SelfShadowLerpHair;
	float _ES_LEVEL_ADJUST_ON;
	float _XPad1;
	float4 _ES_GlobalRotMatrix[4];
	float _ES_CharacterToonRampMode;
	float _ES_CharacterDisableLocalMainLight;
	float2 _XPad2;
	float4 _ES_AddColor;
	float4 _ES_SPColor;
	float _ES_SPIntensity;
	float3 _XPad3;
	float4 _ES_RimShadowColor;
	float _ES_RimShadowIntensity;
	float _ES_CharacterShadowFactor;
	float _ES_OutLineDarkenVal;
	float _ES_OutLineLightedVal;
	float _ES_OutlineDisableDistanceScale;
	float _ES_OutlineFallbackScale;
	float _ES_HeightLerpTop;
	float _ES_HeightLerpBottom;
	float4 _ES_HeightLerpTopColor;
	float4 _ES_HeightLerpMiddleColor;
	float4 _ES_HeightLerpBottomColor;
	float2 _ES_RimLightOffset;
	float _ES_RimLightWidth;
	float _ES_RimLightIntensity;
	float _ES_RimLightAddMode;
    float _ES_RimLightMode;
	float2 _XPad4;
	float4 _ES_RimLightColor;
	float4 _ES_LevelSkinLightColor;
	float4 _ES_LevelSkinShadowColor;
	float4 _ES_LevelHighLightColor;
	float4 _ES_LevelShadowColor;
	float _ES_LevelShadow;
	float _ES_LevelMid;
	float _ES_LevelHighLight;
	float _ES_LevelEyeShadowIntensity;
	float _ES_IndoorCharShadowAsCookie;
	float _ES_FogColor;
	float _ES_FogDensity;
	float _ES_FogNear;
	float _ES_FogFar;
	float _ES_HeightFogColor;
	float _ES_HeightFogBaseHeight;
	float _ES_HeightFogRange;
	float _ES_HeightFogDensity;
	float _ES_HeightFogFogNear;
	float _ES_HeightFogFogFar;
	float _ES_FogCharacterNearFactor;
	float _ES_HeightFogAddAjust;
	float _ES_DisableFogTransition;
	float2 _XPad5;
	float4 _ES_EffCustomLightPosition;
	float _OutlineScale;
CBUFFER_END


TEXTURE2D(_MainTex);
TEXTURE2D(_LightMap);
TEXTURE2D(_DiffuseCoolRampMultiTex);
TEXTURE2D(_DiffuseRampMultiTex);
TEXTURE2D(_MaterialValuesPackLUT);
SAMPLER(sampler_linear_repeat);
SAMPLER(sampler_linear_clamp);
// The raw shadow map texture (for manual sampling/debug)
TEXTURE2D_ARRAY(_MainLightShadowmapTexture);
SAMPLER(sampler_MainLightShadowmapTexture);

// The hardware comparison sampler (for ShadowCoord/PCF)
// This maps to hlslcc_zcmp_MainLightShadowmapTexture
TEXTURE2D_ARRAY_SHADOW(hlslcc_zcmp_MainLightShadowmapTexture);
SAMPLER_CMP(sampler_hlslcc_zcmp_MainLightShadowmapTexture);

// #if defined(is_forwardemission)
TEXTURE2D(_GBufferA);
TEXTURE2D(_DepthBufferOrCopy);
// #endif

TEXTURE2D(_ES_GradientAtlas);

#if defined(_DISSOLVE)
TEXTURE2D(_DissolveMap);
TEXTURE2D(_DissolveMask);
#endif

#endif