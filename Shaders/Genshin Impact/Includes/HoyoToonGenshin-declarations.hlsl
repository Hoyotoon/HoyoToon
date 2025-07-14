// textures : 
Texture2D _MainTex; // this is the diffuse color texture
Texture2D _LightMapTex; // this is both the body/hair lightmap texture and the faceshadow texture
#if defined(faceishadow)
    Texture2D _FaceMapTex; // this is the facelightmap texture
#endif
#if defined(has_sramp)
    Texture2D _PackedShadowRampTex;
#endif
#if defined(use_shadow)
    Texture2D _CustomAO;
    float4 _CustomAO_ST;
#endif
#if defined(use_bump)
    Texture2D _BumpMap;
#endif
#if defined(use_metal)
    Texture2D _MTMap;
    Texture2D _MTSpecularRamp;
#endif
#if defined(has_mask)
    Texture2D _MaterialMasksTex;
#endif
Texture2D _CustomEmissionTex;
#if defined(is_cock)
    Texture2D _StarTex;
    #if defined(paimon_cock)
        Texture2D _Star02Tex;
        Texture2D _NoiseTex01;
        Texture2D _NoiseTex02;
        Texture2D _ColorPaletteTex;
        Texture2D _ConstellationTex;
        Texture2D _CloudTex;
    #endif
    #if defined(skirk_cock)
        Texture2D _StarMask;
        Texture2D _BlockHighlightMask;
        Texture2D _BrightLineMask;
    #endif
    #if defined(asmoday_cock)
        Texture2D _FlowMap;
        Texture2D _FlowMap02;
        Texture2D _NoiseMap;
        Texture2D _FlowMask;
    #endif
#endif
#if defined(asmogay_arm)
    Texture2D _Mask;
#endif
#if defined(use_leather)
    Texture2D _LeatherReflect;
    Texture2D _LeatherLaserRamp;
#endif
#if defined(use_stockings)
    Texture2D _StockingsDetailTex;
#endif
#if defined(parallax_glass)
    Texture2D _GlassSpecularTex;
#endif
#if defined(nyx_body) || defined(nyx_outline)
    Texture2D _TempNyxStatePaintMaskTex;
    Texture2D _NyxStateOutlineColorRamp;
    Texture2D _NyxStateOutlineNoise;
#endif
#if defined(use_rimlight)
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
#endif
Texture2D _ClipAlphaTex;
#if defined(can_shift)
    Texture2D _HueMaskTexture;
#endif
#if defined(weapon_mode)
    Texture2D _WeaponDissolveTex;
    Texture2D _WeaponPatternTex;
    Texture2D _ScanPatternTex;
#endif
#if defined(use_outline)
    Texture2D _OutlineTex;
#endif
#if defined(use_vat)
    Texture2D _LerpTexture;
    Texture2D _VertexTex;
    Texture2D _VertTex;
    Texture2D _PosTex_A;
    Texture2D _VerticalRampTex;
    Texture2D _VerticalRampTex2;
    Texture2D _HighlightMaskTex;
    Texture2D _HighlightMaskTex2;
    float4 _LerpTextureST;
    float4 _VertexTexST;
    float4 _VertTexST;
    float4 _PosTex_A_ST;
    float4 _VerticalRampTex_ST;
    float4 _VerticalRampTex2_ST;
    float4 _HighlightMaskTex_ST;
    float4 _HighlightMaskTex2_ST;
#endif


Texture2D _EyeMask;
Texture2D _EyeMaskCustom;


Texture2D _FakePointNoiseTex;
Texture2D _FakePointNoiseTex2;
Texture2D _FakePointNoiseTex3;

Texture2D _DissolveNoise;

#if defined(use_nbrbase)
    Texture2D _NbrRefTex; 
#endif

// apparently this was never defined 
float variant_selector;


float4 _MainTex_ST; // scale and translation offsets for main texture
float4 _Star02Tex_ST;
float4 _NoiseTex01_ST;
float4 _NoiseTex02_ST;
float4 _ColorPaletteTex_ST;
float4 _ConstellationTex_ST;
float4 _CloudTex_ST;
float4 _StarTex_ST;
float4 _FlowMap_ST;
float4 _FlowMap02_ST;
float4 _NoiseMap_ST;
float4 _FlowMask_ST;
float4 _Mask_ST;
float4 _GlassSpecularTex_ST;
float4 _WeaponDissolveTex_ST;
float4 _WeaponPatternTex_ST;
float4 _ScanPatternTex_ST;

SamplerState sampler_MainTex; 
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;
SamplerState sampler_point_repeat;
SamplerState sampler_point_clamp;
// reduce sampler count to just 3


float _gameVersion;

float _EnableShadow;
float _EnableHueShift;
float _EnableFresnel;

// main properties
float _UseBackFaceUV2;
float _MainTexAlphaUse;
float _MainTexAlphaCutoff;
float _UseMaterial2;
float _UseMaterial3;
float _UseMaterial4;
float _UseMaterial5;
float _EnableDithering;

// light: 
float _FilterLight;

// colors 
float _UseMaterialMasksTex;
float _MainTexColoring;
float4 _MainTexTintColor;
float _DisableColors;
float4 _Color;
float4 _Color2;
float4 _Color3;
float4 _Color4;
float4 _Color5;

// alpha clipping 
float _UseClipping;
float _ClipMethod;
float4 _ClipBoxPositionOffset;
float4 _ClipBoxScale;
float _ClipBoxHighLightScale;
float4 _ClipHighLightColor;
float _ClipAlphaUVSet;
float _ClipAlphaThreshold;
float _ClipDissolveDirection;
float _ClipDissolveValue;
float _ClipDissolveHightlightScale;
float _ClipAlphaHighLightScale;

// face propreties 
float _FaceBlushStrength;
float3 _FaceBlushColor;
float3 _headForwardVector;
float3 _headRightVector;
float3 _headUpVector;
float _FaceMapSoftness;
float _FaceMapRotateOffset;
float _UseFaceMapNew;
float _UseFaceBlueAsAO;

// weapon properties
float _UseWeapon;
float _WeaponDissolveValue;
float _DissolveDirection_Toggle;
float4 _WeaponPatternColor;
float _Pattern_Speed;
float _SkillEmisssionPower;
float4 _SkillEmisssionColor;
float _SkillEmissionScaler;
float _ScanColorScaler;
float4 _ScanColor;
float _ScanDirection_Switch;
float _ScanSpeed;

// glass properties
float4 _MainColor;
float4 _GlassThicknessColor;
float4 _GlassSpecularDetailColor;
float4 _GlassSpecularColor;
float _GlassTiling;
float _MainColorScaler;
float _UseGlassSpecularToggle;
float _GlassSpecularOffset;
float _GlasspecularLength;
float _GlasspecularLengthRange;
float _GlassThickness;
float _GlassThicknessScale;
float _GlassSpecularDetailOffset;
float _GlassSpecularDetailLength;
float _GlassSpecularDetailLengthRange;

// normal map properties
float _DummyFixedForNormal;
float _isNativeMainNormal;
float _UseBumpMap;
float _BumpScale;

// sdf detail line
float  _TextureLineUse;
float4 _TextureLineMultiplier;
float4 _TextureLineDistanceControl;
float  _TextureLineThickness;
float  _TextureLineSmoothness;

// shadow properties
float _EnableSelfShadow;
float _DayOrNight;
float _AutomaticNight;
float _MultiLight;
float _EnvironmentLightingStrength;
float _UseShadowRamp;
float _UseLightMapColorAO;
float _UseVertexColorAO;
float _LightArea;
float _ShadowRampWidth;
float _UseVertexRampWidth;
float _UseShadowTransition;
float _ShadowTransitionRange;
float _ShadowTransitionRange2;
float _ShadowTransitionRange3;
float _ShadowTransitionRange4;
float _ShadowTransitionRange5;
float _ShadowTransitionSoftness;
float _ShadowTransitionSoftness2;
float _ShadowTransitionSoftness3;
float _ShadowTransitionSoftness4;
float _ShadowTransitionSoftness5;
float4 _FirstShadowMultColor;
float4 _FirstShadowMultColor2;
float4 _FirstShadowMultColor3;
float4 _FirstShadowMultColor4;
float4 _FirstShadowMultColor5;
float4 _CoolShadowMultColor;
float4 _CoolShadowMultColor2;
float4 _CoolShadowMultColor3;
float4 _CoolShadowMultColor4;
float4 _CoolShadowMultColor5;
float _CustomAOEnable;
float _CustomAOUV;
float _AOSamplerType;

// metal properties : 
float _MetalMaterial;
float _MTUseSpecularRamp;
float _MTMapTileScale;
float _MTMapBrightness;
float _MTShininess;
float _MTSpecularScale;
float _MTSpecularAttenInShadow;
float _MTSharpLayerOffset;
float4 _MTMapDarkColor;
float4 _MTMapLightColor;
float4 _MTShadowMultiColor;
float4 _MTSpecularColor;
float4 _MTSharpLayerColor;

// specular properties :
float _SpecularHighlights;
float _UseToonSpecular;
float _SpecualrInShaow;
float _Shininess;
float _Shininess2;
float _Shininess3;
float _Shininess4;
float _Shininess5;
float _SpecMulti;
float _SpecMulti2;
float _SpecMulti3;
float _SpecMulti4;
float _SpecMulti5;
float _SpecOpacity;
float _SpecOpacity2;
float _SpecOpacity3;
float _SpecOpacity4;
float _SpecOpacity5;
float4 _SpecularColor;
float4 _SpecularColor2;
float4 _SpecularColor3;
float4 _SpecularColor4;
float4 _SpecularColor5;

// leather : 
float _UseCharacterLeather;
float _LeatherLaserTiling;
float _LeatherLaserOffset;
float _LeatherLaserScale;
float4 _LeatherSpecularColor;
float _LeatherReflectOffset;
float _LeatherReflectBlur;
float _LeatherReflectScale;
float _LeatherSpecularShift;
float _LeatherSpecularRange;
float _LeatherSpecularScale;
float _LeatherSpecularSharpe;
float4 _LeatherSpecularDetailColor;
float _LeatherSpecularDetailRange;
float _LeatherSpecularDetailScale;
float _LeatherSpecularDetailSharpe;

// stockings :
float _StockingUV;
float _UseCharacterStockings;
float _StockingCenterX;
float _StockingCenterY;
float4 _StockingDecalColor;
float _StockingDecalIntensity;
float _StockingDecalScale;
float4 _StockingShiningColor;
float _StockingShiningColorBlend;
float _StockingShiningDensity;
float _StockingShiningFrequencncy;
float _StockingShiningIntensity;
float _StockingShiningSize;
float _StockingShiningTiling;
float4 _StockingsDetailPattenColor;
float _StockingsDetailPattenScale;
float _StockingsDetailPattenTiling;
float _StockingsDetailScale;
float _StockingsDetailTilingFar;
float _StockingsDetailTilingNear;
float4 _StockingsLightColor;
float4 _StockingsShadowColor;
float _StockingsLightRange;
float _StockingsLightScale;
float _StockingsLightScaleInShadow;
float _StockingsShadowRange;
float4 _StockingsSpecularColor;
float4 _StockingsSpecularDetailColor;
float _StockingsSpecularDetailRange;
float _StockingsSpecularDetailScale;
float _StockingsSpecularDetailSharpe;
float _StockingsSpecularDistance;
float _StockingsSpecularFade;
float _StockingsSpecularRange;
float _StockingsSpecularScale;
float _StockingsSpecularSharpe;
float _StockingsSpecularShift;
float _StockingsTilingDistance;
float _StockingsWHite;

// rim light properties :
float _RimLightType;
float _UseRimLight; 
float _RimLightThickness;
float _RimLightIntensity;
float _RimThreshold;
float _ES_AvatarRimWidth;
float _ES_AvatarRimWidthScale;
float4 _ES_AvatarFrontRimColor;
float4 _ES_AvatarBackRimColor; 
float _ES_AvatarFrontRimIntensity;
float _ES_AvatarBackRimIntensity;
float4 _RimColor;
float4 _RimColor1;
float4 _RimColor2;
float4 _RimColor3;
float4 _RimColor4;
float4 _RimColor5;

// emission properties : 
float _EmissionType;
float _EmissionScaler;
float _EmissionScaler1;
float _EmissionScaler2;
float _EmissionScaler3;
float _EmissionScaler4;
float _EmissionScaler5;
float4 _EmissionColor_MHY;
float4 _EmissionColor1_MHY;
float4 _EmissionColor2_MHY;
float4 _EmissionColor3_MHY;
float4 _EmissionColor4_MHY;
float4 _EmissionColor5_MHY;
float4 _EmissionColorEye;
float _TogglePulse;
float _EyePulse;
float _PulseSpeed;
float _PulseMinStrength;
float _PulseMaxStrength;
float _ToggleEyeGlow;
float _EyeGlowStrength;
float _EyeTimeOffset;

// fresnel properties
float4 _HitColor;
float4 _ElementRimColor;
float _HitColorScaler;
float _HitColorFresnelPower;

// nyx state properties
float _NyxStateRampType;
float _EnableNyxState;
float _EnableNyxBody;
float _EnableNyxOutline;
float _NyxBodyUVCoord;
float _BodyAffected;
float _LineAffected;
float _TempNyxStatePaintMaskChannel;
float4 _NyxStateOutlineWidthVarietyWithResolution;
float4 _NyxStateOutlineColorOnBodyMultiplier;
float _NyxStateOutlineColorOnBodyOpacity;
float _NyxStateOutlineColorScale;
float4 _NyxStateOutlineColor;
float2 _NyxStateOutlineColorNoiseScale;
float4 _NyxStateOutlineColorNoiseAnim;
float _NyxStateOutlineColorNoiseTurbulence;
float _NyxStateEnableOutlineWidthScaleHeightLerp;
float _NyxStateOutlineWidthScale;
float2 _NyxStateOutlineWidthScaleRange;
float4 _NyxStateOutlineWidthScaleLerpHeightRange;
float2 _NyxStateOutlineVertAnimNoiseScale;
float2 _NyxStateOutlineVertAnimNoiseAnim;
float _NyxStateOutlineVertAnimScale;
float _NyxStateEnableOutlineVertAnimScaleHeightLerp;
float2 _NyxStateOutlineVertAnimScaleRange;
float4 _NyxStateOutlineVertAnimScaleLerpHeightRange;
// custom nyx ramp settings
float4 _RampPoint0;
float _RampPointPos0;
float4 _RampPoint1;
float _RampPointPos1;
float4 _RampPoint2;
float _RampPointPos2;
// float4 _RampPoint3;
// float _RampPointPos3;
// float4 _RampPoint4;
// float _RampPointPos4;

// outline properties 
float  _OutlineType;
float _DisableZShift;
float _OutlineOffsetBlockBChannel;
float _UseOutlineTex;
float _OutlineWidthChannel;
float _OutlineWidthSource;
float _FallbackOutlines;
float _OutlineWidth;
float _OutlineCorrectionWidth;
float _Scale;
float _DisableDepthScaling;
float _DisableFOVScalingOL;

float _OutLineIntensity;
float _OutLineIntensity2;
float _OutLineIntensity3;
float _OutLineIntensity4;
float _OutLineIntensity5;

float4 _OutlineColor;
float4 _OutlineColor2;
float4 _OutlineColor3;
float4 _OutlineColor4;
float4 _OutlineColor5;
float4 _OutlineWidthAdjustScales;
float4 _OutlineWidthAdjustZs;
float  _MaxOutlineZOffset;

// special fx
float _StarUVSource;
float _StarCockEmis;
float _StarCloakEnable;
int _StarCockType;

// skirk specific
float _Skirktype;
float _UseScreenUV;
float _ScreenIsWorld;
float _StarTiling;
float4 _StarTexSpeed;
float4 _StarColor;
float _StarFlickRange;
float4 _StarFlickColor;
float4 _StarFlickerParameters;
float4 _BlockHighlightColor;
float4 _BlockHighlightViewWeight;
float _CloakViewWeight;
float _BlockHighlightRange;
float _BlockHighlightSoftness;
float _BrightLineMaskContrast;
float4 _BrightLineColor;
float4 _BrightLineMaskSpeed;

// paimon/dainsleif
float _StarBrightness;
float _StarHeight;
float _Star02Height;
float _Noise01Speed;
float _Noise02Speed;
float _ColorPalletteSpeed;
float _ConstellationHeight;
float _ConstellationBrightness;
float _Star01Speed;
float _Noise03Brightness;
float _CloudBrightness;
float _CloudHeight;

// asmoday cloak
float4 _BottomColor01;
float4 _BottomColor02;
float _BottomScale;
float _BottomPower;
float _FlowMaskScale;
float _FlowMaskPower;
float4 _FlowColor;
float _FlowScale;
float4 _FlowMaskSpeed;
float4 _FlowMask02Speed;
float _NoiseScale;
float4 _NoiseSpeed;

// asmoday arm
float _HandEffectEnable;
float4 _LineColor;
// float4 _LightColor;
float4 _ShadowColor;
float _DownMaskRange;
float _TopMaskRange;
float _TopLineRange;
float4 _FresnelColor;
float _FresnelPower;
float _FresnelScale;
float _ShadowWidth;
float4 _Tex01_UV;
float _Tex01_Speed_U;
float _Tex01_Speed_V;
float4 _Tex02_UV;
float _Tex02_Speed_U;
float _Tex02_Speed_V;
float4 _Tex03_UV;
float _Tex03_Speed_U;
float _Tex03_Speed_V;
float4 _Tex04_UV;
float _Tex04_Speed_U;
float _Tex04_Speed_V;
float4 _Tex05_UV;
float _Tex05_Speed_U;
float _Tex05_Speed_V;
float _Mask_Speed_U;
float _GradientPower;
float _GradientScale;

// outline emission
float _EnableOutlineGlow;
float _OutlineGlowInt;
float4 _OutlineGlowColor;
float4 _OutlineGlowColor2;
float4 _OutlineGlowColor3;
float4 _OutlineGlowColor4;
float4 _OutlineGlowColor5;

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
float _EnableOutlineHue;
float _AutomaticOutlineShift;
float _ShiftOutlineSpeed;
float _GlobalOutlineHue;
float _OutlineHue;
float _OutlineHue2;
float _OutlineHue3;
float _OutlineHue4;
float _OutlineHue5;
float _EnableEmissionHue;
float _AutomaticEmissionShift;
float _ShiftEmissionSpeed;
float _GlobalEmissionHue;
float _EmissionHue;
float _EmissionHue2;
float _EmissionHue3;
float _EmissionHue4;
float _EmissionHue5;
float _EnableRimHue;
float _AutomaticRimShift;
float _ShiftRimSpeed;
float _GlobalRimHue;
float _RimHue;
float _RimHue2;
float _RimHue3;
float _RimHue4;
float _RimHue5;

// debug
float _DebugMode;
float _DebugLighting;
float _DebugDiffuse;
float _DebugLightMap;
float _DebugFaceMap;
float _DebugNormalMap;
float _DebugVertexColor;
float _DebugRimLight;
float _DebugNormalVector;
float _DebugTangent;
float _DebugMetal;
float _DebugSpecular;
float _DebugEmission;
float _DebugFaceVector;
float _DebugMaterialIDs;
float _DebugLights;

// mavuika vat shit
float _DisappearByVerColAlpha;
float _VertexAnimType;
float _EnableHairVat;
float _EnableHairVertexVat;
float _UseVAT;
float4 _LightColor;
float4 _DarkColor;
float4 _HighlightsColor;
float4 _AhomoColor;
float _VertTexSwitch;
float _VertTexUS;
float _VertTexVS;
float _VertAdd;
float _VertPower;
float _VertMask;
float _VertexTexSwitch;
float _VertexTexUS;
float _VertexTexVS;
float _VertexAdd;
float _VertexPower;
float _VertexMask;
float _NoisePowerForLerpTex;
float _HighlightsBrightness;
float _HighlightsSpeed;
float4 _AllColorBrightness;
float4 _DayColor;
float _Speed;
float _IfAutoPlayback;
float _CurrentFrame;
float _FrameCount;
float _HoudiniFPS;
float _IfInterframeInterp;
float _BoundMaxX;
float _BoundMaxY;
float _BoundMaxZ;
float _BoundMinX;
float _BoundMinY;
float _BoundMinZ;
float4 _FRESC;
float _FresR;
float _FresS;
float _RempLerp;
float _MR;
float _MS;
float _MainSwitch;
float2 _AlphaNoiseSpeed;
float _AlphaNoiseScale;
float2 _MainUVSpeed;
float4 _MCOLOR;
float4 _DC;
float4 _BC;
float _R;
float _S;
float4 _RampBrightColor;
float4 _RampDarkColor;
float _RampR;
float _RampS;
float4 _HighlightColor;
float4 _HighlightColor2;
float _HighlightMaskTexChannelSwitch;
float _HighlightMaskTex2ChannelSwitch;
float _HighlightMaskTex2VOffsetByVerColA;

float _VerticalRampLerp;
float4 _VerticalRampTint;
float _VerticalFadeToggle;
float _VerticalFadeOffset;
float _VerticalFadeRange;


float _UseHairShadow;
float _HairShadowExtrusion;
float _HairShadowLightShift;
float4 _HairShadowStencilShift;
float4 _HairShadowVerticalRemap;
float4 _HairShadowColor;
float4 _CoolHairShadowColor;


float4 _FakePointColor;
float4 _FakePointPosition;
float _UseFakePoint;
float _FakePointRange;
float _FakePointIntensity;
float _FakePointReflection;
float _FakePointFrequency;
float _FakePointFrequencyMin;
float _FakePointSkinIntensity;
float _FakePointSkinSaturate;

float4 _FakePointColor2;
float4 _FakePointPosition2;
float _UseFakePoint2;
float _FakePointRange2;
float _FakePointIntensity2;
float _FakePointReflection2;
float _FakePointFrequency2;
float _FakePointFrequencyMin2;
float _FakePointSkinIntensity2;
float _FakePointSkinSaturate2;

float4 _FakePointColor3;
float4 _FakePointPosition3;
float _UseFakePoint3;
float _FakePointRange3;
float _FakePointIntensity3;
float _FakePointReflection3;
float _FakePointFrequency3;
float _FakePointFrequencyMin3;
float _FakePointSkinIntensity3;
float _FakePointSkinSaturate3;

float _UseCharacterNbrBase;
float _NbrRefBlur;
float _NbrRefScale;
float _NbrRefTiling;
float _NbrRoughness;
float _NbrScale;
float4 _NbrBaseColor;

float _UseEyeStencil;
float _HairZOffset;
float _StencilType;
float _StencilFilter;
float _HairBlendUse;
float _HairTransparentValue;
float _StencilMaskSource;
float _StencilChannelCount;
float _StencilLayer0;
float _StencilLayer1;
float _StencilLayer2;
float _StencilLayer3;
float _StencilLayer1Op;
float _StencilLayer2Op;
float _StencilLayer3Op;
float _StencilConditional;
float _StencilConditionThresh;
float _AlphaYZ;
float _AlphaXZ;
float _InvertMask;

// avatar death
float _EnableAvatarDie;
float _ApplyOnlyNyx;
float4 _DissolveNoiseST;
float _DissolveValue;
float _DissolveEdgeWidth;
float4 _DissolveColor;
float _DissolveColorScaler;
float4 _DeathTintColor;

uniform float _GI_Intensity;
uniform float4x4 _LightMatrix0;

float _EnableTonemapping;