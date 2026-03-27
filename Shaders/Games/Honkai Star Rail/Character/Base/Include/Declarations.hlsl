#ifndef HSR_BASE_DECLARATIONS
#define HSR_BASE_DECLARATIONS

CBUFFER_START(CRP_PerView)
    
	float4x4 _unity_MatrixInvP;
    float4x4 _NonJitteredProjMatrix;
    float4 _SplitUVTrans;
CBUFFER_END



#ifndef UNITY_MATRIX_MV
    #define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
#endif

#ifndef UNITY_MATRIX_MVP
    #define UNITY_MATRIX_MVP mul(unity_MatrixVP, unity_ObjectToWorld)
#endif

#define unity_MatrixMV  UNITY_MATRIX_MV
#define unity_MatrixMVP UNITY_MATRIX_MVP

// CBUFFER_START(UnityPerFrame)
//     float4 _MainLightShadowData;    // x: strength, y: cascade count/offset, z: normal bias
// CBUFFER_END

#define UNITY_MATRIX_M          unity_ObjectToWorld
#define UNITY_MATRIX_I_M        unity_WorldToObject
#define UNITY_PREV_MATRIX_M     unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M   unity_MatrixPreviousMI
#define UNITY_MATRIX_V          unity_MatrixV
#define UNITY_MATRIX_I_V        unity_MatrixInvV
// #define UNITY_MATRIX_P          glstate_matrix_projection
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

#define HSR_CHARACTER_SELF_SHADOW_SLOTS 16
float4x4 _CharacterSelfShadowWorldToShadowArr[HSR_CHARACTER_SELF_SHADOW_SLOTS];
float4 _CharacterSelfShadowAtlasRectArr[HSR_CHARACTER_SELF_SHADOW_SLOTS];
float _CharacterSelfShadowSlotCount;
float3 _PadCharacterSelfShadowSlotCount;
float4 _CharacterSelfShadowAtlasRect;
float _CharacterSelfShadowSliceIndex;
float _CharacterSelfShadowValid;
float2 _PadCharacterSelfShadow;
float4 _CharacterSelfShadowAtlasTexelSize;

CBUFFER_START(UnityPerMaterial)
    float3 _CharaWorldSpaceOffset;
    float _IsMonster;
    float _UVChannelFront;
    float _UVChannelBack;
    float _EnableAlphaCutoff;
    float _AlphaCutoff;
    float _AlphaTestThreshold;
    int _HideCharaParts;
    int _ShowPartID;
    int4 _VertexColorSwitch;
    float4 _MainTex_ST;
    float4 _CustomMainLightDir;
    float4 _Color;
    float4 _BackColor;
    float4 _LightMap_TexelSize;
    float _NormalScale;
    float _EmissionThreshold;
    float _EmissionIntensity;
    float _ShadowRamp;
    float _ShadowBoost;
    float _ShadowBoostVal;
    float4 _SpecularColor0;
    float _SpecularShininess0;
    float _SpecularRoughness0;
    float _SpecularIntensity0;
    float4 _SpecularColor1;
    float _SpecularShininess1;
    float _SpecularRoughness1;
    float _SpecularIntensity1;
    float4 _SpecularColor2;
    float _SpecularShininess2;
    float _SpecularRoughness2;
    float _SpecularIntensity2;
    float4 _SpecularColor3;
    float _SpecularShininess3;
    float _SpecularRoughness3;
    float _SpecularIntensity3;
    float4 _SpecularColor4;
    float _SpecularShininess4;
    float _SpecularRoughness4;
    float _SpecularIntensity4;
    float4 _SpecularColor5;
    float _SpecularShininess5;
    float _SpecularRoughness5;
    float _SpecularIntensity5;
    float4 _SpecularColor6;
    float _SpecularShininess6;
    float _SpecularRoughness6;
    float _SpecularIntensity6;
    float4 _SpecularColor7;
    float _SpecularShininess7;
    float _SpecularRoughness7;
    float _SpecularIntensity7;
    float4 _OutlineColor0;
    float4 _OutlineColor1;
    float4 _OutlineColor2;
    float4 _OutlineColor3;
    float4 _OutlineColor4;
    float4 _OutlineColor5;
    float4 _OutlineColor6;
    float4 _OutlineColor7;
    float _OutlineWidth;
    float _OutlineColorIntensity;
    float _OutlineExtdStart;
    float _OutlineExtdMax;
    float _OutlineOffset;
    float _RimLightMode;
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
    float _RimShadowWidth0;
    float3 _RimShadowColor0;
    float _RimShadowFeather0;
    float _RimShadowWidth1;
    float3 _RimShadowColor1;
    float _RimShadowFeather1;
    float _RimShadowWidth2;
    float3 _RimShadowColor2;
    float _RimShadowFeather2;
    float _RimShadowWidth3;
    float3 _RimShadowColor3;
    float _RimShadowFeather3;
    float _RimShadowWidth4;
    float3 _RimShadowColor4;
    float _RimShadowFeather4;
    float _RimShadowWidth5;
    float3 _RimShadowColor5;
    float _RimShadowFeather5;
    float _RimShadowWidth6;
    float3 _RimShadowColor6;
    float _RimShadowFeather6;
    float _RimShadowWidth7;
    float3 _RimShadowColor7;
    float _RimShadowFeather7;
    float _Rimintensity;
    float _RimWidth;
    float4 _RimOffset;
    float _RimEdge;
    float4 _FresnelColor;
    float4 _FresnelBSI;
    float _FresnelColorStrength;
    float _RimShadowCt;
    float _RimShadowIntensity;
    float4 _RimShadowOffset;
    float4 _StockRangeTex_ST;
    float4 _Stockcolor;
    float4 _StockDarkcolor;
    float _StockDarkWidth;
    float _Stockpower;
    float _Stockpower1;
    float _StockSP;
    float _StockRoughness;
    float _Stockthickness;
    float4 _SkyTex_ST;
    float4 _SkyMask_ST;
    float _SkyRange;
    float4 _SkyStarColor;
    float4 _SkyStarTex_ST;
    float _SkyStarTexScale;
    float4 _SkyStarSpeed;
    float _SkyStarDepthScale;
    float4 _SkyStarMaskTex_ST;
    float _SkyStarMaskTexScale;
    float _SkyStarMaskTexSpeed;
    float4 _SkyFresnelColor;
    float _SkyFresnelBaise;
    float _SkyFresnelScale;
    float _SkyFresnelSmooth;
    float _OSScale;
    float _StarDensity;
    float _StarMode;
    int _FlameID;
    float4 _FlameColorOut;
    float4 _FlameColorIn;
    float _FlameHeight;
    float _FlameWidth;
    float _FlameSpeed;
    float _FlameSwirilTexScale;
    float _FlameSwirilSpeed;
    float _FlameSwirilScale;
    float _CrystalTransparency;
    float _CrystalRange1;
    float _CrystalRange2;
    float _ColorIntensity;
    float4 _EffectColor0;
    float4 _EffectColor1;
    float4 _EffectColor2;
    float4 _EffectColor3;
    float4 _EffectColor4;
    float4 _EffectColor5;
    float4 _EffectColor6;
    float4 _EffectColor7;
    float _GlobalOneMinusAvatarIntensityEnable;
    float _OneMinusCharacterOutlineWidthScale;
    float _UseMoonHalo;
    float _MoonHaloRange;
    float4 _MoonDir;
    float4 _MoonAnim;
    float _MoonUVType;
    float _UseOverHeated;
    float _HeatInst;
    float _HeatedHeight;
    float _HeatedThreshould;
    float3 _Pad15;
    float4 _HeatColor0;
    float4 _HeatColor1;
    float4 _HeatColor2;
    float4 _MatCapColor;
    float _MatCapStrength;
    float _MatCapStrengthInShadow;
    float _GlobalGlintScale;
    float _GlobalGlintIntensity;
    float4 _GlobalGlintColor;
    float _GlobalGlintDensity;
    float _GlobalGlintSparkle;
    float _GlintWorldPosUV;
    float _GlintScale;
    float _GlintConcentration;
    float _GlintIntensity;
    float4 _GlintColor;
    float _GlintRandom;
    float _GlintUVTillingY;
    float _GlintScaleBackface;
    float _GlobalGlintPointScale;
    float _GlintPointScale;
    float _GlintDensity;
    float _GlobalGlintSparkFreq;
    float _GlintSparkle;
    float _GlintSparkFreq;
    float _GlobalGlintViewFreq;
    float _GlintViewFreq;
    float _ReflectionRoughness;
    float _ReflectionThreshold;
    float _ReflectionSoftness;
    float _ReflectionBlendThreshold;
    float _ReflectionReversedThreshold;
    float _FakeRefBlendIntensity;
    float _FakeRefAddIntensity;
    float4 _ReflectionColor;
    float4 _ReflectionBlendColor;
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
    float _mBloomIntensity0;
    float _mBloomIntensity1;
    float _mBloomIntensity2;
    float _mBloomIntensity3;
    float _mBloomIntensity4;
    float _mBloomIntensity5;
    float _mBloomIntensity6;
    float _mBloomIntensity7;
    float4 _mBloomColor0;
    float4 _mBloomColor1;
    float4 _mBloomColor2;
    float4 _mBloomColor3;
    float4 _mBloomColor4;
    float4 _mBloomColor5;
    float4 _mBloomColor6;
    float4 _mBloomColor7;
    float _CustomParamA0;
    float _CustomParamB0;
    float _CustomParamA1;
    float _CustomParamB1;
    float _CustomParamA2;
    float _CustomParamB2;
    float _CustomParamA3;
    float _CustomParamB3;
    float _CustomParamA4;
    float _CustomParamB4;
    float _CustomParamA5;
    float _CustomParamB5;
    float _CustomParamA6;
    float _CustomParamB6;
    float _CustomParamA7;
    float _CustomParamB7;
    int _UsingDitherAlpha;
    int _UsingDitherAlphaArt;
    float _DitherAlpha;
    int _DITHER_FADE_IN;
    int _UseMaterialValuesLUT;
    int _XorShadowPartID;
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
TEXTURE2D_SHADOW(_CharacterSelfShadowTexture);
SAMPLER(sampler_linear_repeat);
SAMPLER(sampler_linear_clamp);
SAMPLER_CMP(sampler_CharacterSelfShadowTexture);
// #if defined(is_forwardemission)
TEXTURE2D(_GBufferA);
TEXTURE2D(_DepthBufferOrCopy);
// #endif

TEXTURE2D(_ES_GradientAtlas);

#if defined(_USE_NORMAL_MAP)
TEXTURE2D(_NormalMap);
#endif

#if defined(_DIRECTIONALDISSOLVE)
TEXTURE2D(_DissolveMap);
TEXTURE2D(_DissolveMask);
#endif

#if defined(_WITHSTOCKINGS)
    TEXTURE2D(_StockRangeTex);
#endif

#if defined(_STATTYSKY)
    TEXTURE2D(_SkyTex);
    TEXTURE2D(_SkyMask);
    TEXTURE2D(_SkyStarTex);
    TEXTURE2D(_SkyStarMaskTex);
#endif

#if defined(_FLAMECRYSTALEFFECT)
    TEXTURE2D(_TangentDirTex);
    TEXTURE2D(_FlameTex);
    TEXTURE2D(_CrystalTex);
#endif

#if defined(_USE_MATCAP)
    TEXTURE2D(_MatCapTex);
    TEXTURE2D(_MatCapMaskTex);
#endif

#if defined(_USE_GLINT)
    TEXTURE2D(_GlintMask);
#endif

#endif