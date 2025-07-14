// ============================================
// common properties 
// -------------------------------------------
// TEXTURES AND SAMPLERS
Texture2D _MainTex; 
float4 _MainTex_ST;
#if defined(second_diffuse)
Texture2D _SecondaryDiff; 
#endif
Texture2D _LightMap;
#if defined(use_shadow)
Texture2D _DiffuseRampMultiTex;
Texture2D _DiffuseCoolRampMultiTex;
#endif
#if defined(use_stocking)
Texture2D _StockRangeTex;
#endif
#if defined(faceishadow)
Texture2D _FaceMap;
Texture2D _FaceExpression;
#endif
Texture2D _MaterialValuesPackLUT;
#if defined(use_emission)
Texture2D _EmissionTex; 
#endif
#if defined(use_caustic)
Texture2D _CausTexture;
#endif
#if defined(can_dissolve)
Texture2D _DissolveMap;
Texture2D _DissolveMask;
#endif
#if defined(can_shift)
Texture2D _HueMaskTexture;
#endif

float4 _CausTexture_ST;

Texture2D _AlphaTex;

#if defined(is_tonemapped)
    Texture2D _Lut2DTex;
#endif

Texture2D _DisTex;
Texture2D _MaskTex;
Texture2D _NoiseTex;
float4 _DisTex_ST;
float4 _MaskTex_ST;
float4 _NoiseTex_ST;

float _SecondaryUV;

float _OutlineZOff;

float _EnableParticleSwirl;
float _CL;
float4 _MainChannel;
float4 _MainChannelRGB;
float4 _MainSpeed;
float _Opacity;
float4 _DisStep;
float4 _InsideColor;
float4 _OutSideColor;
float _Mid;
float4 _MidColor;
float4 _SmoothStep;
float4 _DisRSpeed;
float _DisTexG;
float4 _DisGSpeed;
float _MaskON;
float4 _MaskChannel;
float _MaskChannelA;
float _MaskTransparency;
float4 _MaskTransparencyChannel;
float _NoiseSwitch;
float4 _NoiseSpeed;
float _Disappear;
float _SoftNear;
float _SoftFar;
float _RenderingMode;
float4 _ES_EP_EffectParticle;
float _ES_EP_EffectParticleBottom;
float _ES_EP_EffectParticleTop;
float _VertexColor;
float4 _VertexColorFallback;


SamplerState sampler_MainTex;
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;

#if defined(use_rimlight)
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
#endif

float _testA;

// man
int _ShowPartID;
int _HideCharaParts;
int _HideNPCParts;

// MATERIAL STATES
float variant_selector;
float _BaseMaterial;
float _FaceMaterial;
float _EyeShadowMat;
float _HairMaterial;
float _IsTransparent;

float _FilterLight;

float _EnableSpecular;
float _EnableShadow;
float _EnableRimLight;

// COLORS
float4 _Color;
float4 _BackColor;
float4 _EnvColor;
float4 _AddColor;
float4 _Color0;
float4 _Color1;
float4 _Color2;
float4 _Color3;
float4 _Color4;
float4 _Color5;
float4 _Color6;
float4 _Color7;
float _backfdceuv2;

// bloom shit
float4 _mBloomColor;
float _mBloomIntensity0;
float _mBloomIntensity1;
float _mBloomIntensity2;
float _mBloomIntensity3;
float _mBloomIntensity4;
float _mBloomIntensity5;
float _mBloomIntensity6;
float _mBloomIntensity7;

// secondary 
float _UseSecondaryTex;
float _SecondaryFade;

// alpha cutoff 
float _EnableAlphaCutoff;
float _AlphaTestThreshold;
float _AlphaCutoff;

// face specific properties 
float _FaceSoftness;
float3 _headUpVector;
float3 _headForwardVector;
float3 _headRightVector;
float _HairBlendSilhouette;
float _UseHairSideFade;
int _HairSideChoose;
float _UseDifAlphaStencil;
float _EnableStencil;
float3 _ShadowColor;
float3 _NoseLineColor;
float _NoseLinePower;
float4 _ExCheekColor;
float _ExMapThreshold;
float _ExSpecularIntensity;
float _ExCheekIntensity;
float4 _ExShyColor;
float _ExShyIntensity;
float4 _ExShadowColor;
float4 _ExEyeColor;
float _ExShadowIntensity;

// stocking proprties
float _EnableStocking;
float4 _StockRangeTex_ST;
float4 _Stockcolor;
float4 _StockDarkcolor;
float _StockTransparency;
float _StockDarkWidth;
float _Stockpower;
float _Stockpower1;
float _StockSP;
float _StockRoughness;
float _Stockthickness;

// shadow properties
float _ShadowRamp;
float _ShadowSoftness;
float _ShadowBoost; // these two values are used on the shadow mapping to increase its brightness
float _UseSelfShadow;
float _SelfShadowDarken;
float _SelfShadowDepthOffset;
float _SelfShadowSampleOffset;
float _ShadowBoostVal;

float _ES_LEVEL_ADJUST_ON;
float4 _ES_LevelSkinLightColor;
float4 _ES_LevelSkinShadowColor;
float4 _ES_LevelHighLightColor;
float4 _ES_LevelShadowColor;
float _ES_LevelShadow;
float _ES_LevelMid;
float _ES_LevelHighLight;

float _EnvironmentLightingStrength;

float _UseMaterialValuesLUT;

// specular properties 
float4 _ES_SPColor;
float _ES_SPIntensity;
float4 _SpecularColor0; 
float4 _SpecularColor1; 
float4 _SpecularColor2; 
float4 _SpecularColor3; 
float4 _SpecularColor4; 
float4 _SpecularColor5; 
float4 _SpecularColor6; 
float4 _SpecularColor7;     
float  _SpecularShininess0; 
float  _SpecularShininess1; 
float  _SpecularShininess2; 
float  _SpecularShininess3; 
float  _SpecularShininess4; 
float  _SpecularShininess5; 
float  _SpecularShininess6; 
float  _SpecularShininess7; 
float  _SpecularRoughness0; 
float  _SpecularRoughness1; 
float  _SpecularRoughness2; 
float  _SpecularRoughness3; 
float  _SpecularRoughness4; 
float  _SpecularRoughness5; 
float  _SpecularRoughness6; 
float  _SpecularRoughness7; 
float  _SpecularIntensity0; 
float  _SpecularIntensity1; 
float  _SpecularIntensity2; 
float  _SpecularIntensity3; 
float  _SpecularIntensity4; 
float  _SpecularIntensity5; 
float  _SpecularIntensity6; 
float  _SpecularIntensity7; 

// rim light properties 
float _RimLightMode;
float _RimCt;
float _Rimintensity;
float _ES_Rimintensity;
float _RimWeight;
float _RimFeatherWidth;
float _RimIntensityTexIntensity;
float _RimWidth;
float4 _RimOffset;
float2 _ES_RimLightOffset; // only using the first two
float _RimEdge;
float4 _RimColor0;
float4 _RimColor1;
float4 _RimColor2;
float4 _RimColor3;
float4 _RimColor4;
float4 _RimColor5;
float4 _RimColor6;
float4 _RimColor7;
float _RimWidth0;
float _RimWidth1;
float _RimWidth2;
float _RimWidth3;
float _RimWidth4;
float _RimWidth5;
float _RimWidth6;
float _RimWidth7;
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

// rim shadow properties 
float4 _ES_RimShadowColor;
float _ES_RimShadowIntensity;
float _EnableBackRimLight;
float _RimShadowCt;
float _RimShadowIntensity;
float3 _RimShadowOffset;
float4 _RimShadowColor0;
float4 _RimShadowColor1;
float4 _RimShadowColor2;
float4 _RimShadowColor3;
float4 _RimShadowColor4;
float4 _RimShadowColor5;
float4 _RimShadowColor6;
float4 _RimShadowColor7;
float _RimShadowWidth0;
float _RimShadowWidth1;
float _RimShadowWidth2;
float _RimShadowWidth3;
float _RimShadowWidth4;
float _RimShadowWidth5;
float _RimShadowWidth6;
float _RimShadowWidth7;
float _RimShadowFeather0;
float _RimShadowFeather1;
float _RimShadowFeather2;
float _RimShadowFeather3;
float _RimShadowFeather4;
float _RimShadowFeather5;
float _RimShadowFeather6;
float _RimShadowFeather7;



// emission properties
int _EnableEmission;
float4 _EmissionTintColor;
float _EmissionThreshold;
float _EmissionIntensity;

// caustic properties
float _CausToggle;
float _CausUV;
float4 _CausTexSTA;
float4 _CausTexSTB;
float _CausSpeedA;
float _CausSpeedB;
float4 _CausColor;
float _CausInt;
float _CausExp;
float _EnableSplit;
float _CausSplit;

// liquid properties
float _UseGlass;
float _FillAmount1;
float _FillAmount2;
float _WobbleX;
float _WobbleZ;
float _PosY0;
float _PosY1;
float _PosY2;
float _MainTexSpeed;
float4 _BrightColor;
float4 _DarkColor;
float4 _FoamColor;
float _FoamWidth;
float4 _SurfaceColor;
float _SurfaceLighted;
float _RimColor;
float _RimPower;
float _LiquidOpaqueness;
float4 _GlassColorA;
float _GlassFrsnIn;
float _Opaqueness;
float4 _GlassColorU;
float _SpecularShininess;
float _SpecularThreshold;
float _SpecularIntensity;
float4 _SPDir;
float _EdgeWidth;

// outline properties 
float _EnableOutline;
float _EnableFOVWidth;
float _OutlineWidth;
float _OutlineScale;
float _OutlineFixFront;
float _OutlineFixSide;
float _OutlineFixRange1;
float _OutlineFixRange2;
float _OutlineFixRange3;
float _OutlineFixRange4;
float _FixLipOutline;
float4 _OutlineColor;
float4 _OutlineColor0;
float4 _OutlineColor1;
float4 _OutlineColor2;
float4 _OutlineColor3;
float4 _OutlineColor4;
float4 _OutlineColor5;
float4 _OutlineColor6;
float4 _OutlineColor7;

// starry sky
float _StarAffectedByLight;
float _StarsAreDiffuse;
float _StarrySky;
Texture2D _SkyTex;
float4 _SkyTex_ST;
Texture2D _SkyMask;
float4 _SkyMask_ST;
float _SkyRange;
float4 _SkyStarColor;
Texture2D _SkyStarTex;
float4 _SkyStarTex_ST;
float _SkyStarTexScale;
float4 _SkyStarSpeed;
float _SkyStarDepthScale;
Texture2D _SkyStarMaskTex;
float4 _SkyStarMaskTex_ST;
float _SkyStarMaskTexScale;
float4 _SkyStarMaskTexSpeed;
float4 _SkyFresnelColor;
float _SkyFresnelBaise;
float _SkyFresnelScale;
float _SkyFresnelSmooth;
float _OSScale;
float _StarDensity;
float _StarMode;

// new dissolve 
float _UseWorldPosDissolve;
float _InvertRate;
float _DissolveRate;
float4 _DissolveUVSpeed;
float4 _DissolveOutlineColor1;
float4 _DissolveOutlineColor2;
float _DissolveDistortionIntensity;
float _DissolveOutlineSize1;
float _DissolveOutlineSize2;
float _DissolveOutlineOffset;
float _DissoveDirecMask;
float _DissolveMapAdd;
float _DissolveUV;
float4 _DissolveOutlineSmoothStep;
float4 _DissolveST;
float4 _DistortionST;
float4 _DissolveComponent;
float4 _DissolveDiretcionXYZ;
float4 _DissolveCenter;
float4 _DissolvePosMaskPos;
float4 _DissolvePosMaskRootOffset;
float _DissolvePosMaskWorldON;
float _DissolveUseDirection;
float _DissolvePosMaskFilpOn;
float _DissolvePosMaskOn;
float _DissolvePosMaskGlobalOn;
float _DissoveON;
float _DissolveShadowOff;
float4 _ES_EffCustomLightPosition;

// height light
float _UseHeightLerp;
float _ES_HeightLerpBottom;
float _ES_HeightLerpTop;
float4 _ES_HeightLerpBottomColor;
float4 _ES_HeightLerpMiddleColor;
float4 _ES_HeightLerpTopColor;
float _CharaWorldSpaceOffset;

// hue shift
float _UseHueMask;
float _DiffuseMaskSource;
float _OutlineMaskSource;
float _EmissionMaskSource;
float _RimMaskSource;
float _EnableColorHue;
float _AutomaticColorShift;
float _ShiftColorSpeed;
float _GlobalColorHue;
float _ColorHue;
float _ColorHue2;
float _ColorHue3;
float _ColorHue4;
float _ColorHue5;
float _ColorHue6;
float _ColorHue7;
float _ColorHue8;
float _EnableOutlineHue;
float _AutomaticOutlineShift;
float _ShiftOutlineSpeed;
float _GlobalOutlineHue;
float _OutlineHue;
float _OutlineHue2;
float _OutlineHue3;
float _OutlineHue4;
float _OutlineHue5;
float _OutlineHue6;
float _OutlineHue7;
float _OutlineHue8;
float _EnableEmissionHue;
float _AutomaticEmissionShift;
float _ShiftEmissionSpeed;
float _GlobalEmissionHue;
float _EmissionHue;
float _EmissionHue2;
float _EmissionHue3;
float _EmissionHue4;
float _EmissionHue5;
float _EmissionHue6;
float _EmissionHue7;
float _EmissionHue8;
float _EnableRimHue;
float _AutomaticRimShift;
float _ShiftRimSpeed;
float _GlobalRimHue;
float _RimHue;
float _RimHue2;
float _RimHue3;
float _RimHue4;
float _RimHue5;
float _RimHue6;
float _RimHue7;
float _RimHue8;

// custom colors
float _UseCustomColors;
float4 _CustomSkinColor;
float4 _CustomSkinColor1;
float4 _CustomColor0;
float4 _CustomColor1;
float4 _CustomColor2;
float4 _CustomColor3;
float4 _CustomColor4;
float4 _CustomColor5;
float4 _CustomColor6;
float4 _CustomColor7;
float4 _CustomColor8;
float4 _CustomColor9;
float4 _CustomColor10;
float4 _CustomColor11;
float4 _CustomColor12;
float4 _CustomColor13;

// matcaps
#if defined(use_matcap)
    Texture2D _MatCapMaskTex;
    Texture2D _MatCapTex;
    TextureCube _CubeMap;
#endif
float _OnlyMask;
float _UseCubeMap;
float _ReplaceColor;
float _UseMatcap;
float4 _MatCapColor;
float _MatCapStrength;
float _MatCapStrengthInShadow;

float _EnableLUT;
float4 _Lut2DTexParam;


float _DebugMode;
float _DebugDiffuse;
float _DebugLightMap;
float _DebugFaceMap;
float _DebugFaceExp;
float _DebugMLut;
float _DebugMLutChannel;
float _DebugVertexColor;
float _DebugRimLight;
float _DebugNormalVector;
float _DebugTangent;
float _DebugSpecular;
float _DebugEmission;
float _DebugFaceVector;
float _DebugHairFade;
float _DebugMaterialIDs;
float _DebugLights;

uniform float _GI_Intensity;
uniform float4x4 _LightMatrix0;