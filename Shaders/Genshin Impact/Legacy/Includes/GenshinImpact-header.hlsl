#ifndef GI_HEADER
#define GI_HEADER
// this is where we'll be putting the 100% common and shared things between all the hsr shaders
// starting with the generic samplers
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;
SamplerState sampler_point_repeat;
SamplerState sampler_point_clamp;

// common textures now
Texture2D _MainTex;
float4 _MainTex_ST;

float _TessMask;
float _TessValue;
float _PhongWeight;

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float _ES_CharacterColorTone;
float _IsYup;


#if defined(BUMP_TEXTURELINE_MAP)
Texture2D _BumpMap;
float _BumpScale;

#endif


#if defined(FACE_MAP_NEW_ON)
Texture2D _FaceMapTex;
#endif
#if defined(METAL_MAT)
    Texture2D _MTSpecularRamp;
    Texture2D _MTMap;
#endif
#if defined(SHADOW_RAMP_ON)
    Texture2D _PackedShadowRampTex;
#endif 
Texture2D _LightMapTex;
#if defined(MATERIAL_MASK)
    Texture2D _MaterialMasksTex;
    float4 _MaterialMasksTex_ST;
#endif

float _MainTexAlphaUse;
float _MainTexAlphaCutoff;
float _FaceBlushStrength;
float4 _FaceBlushColor;
float4 _Color;
float _UseEyeMask;
float _EnableEyeMaskDraw;
float _DrawAlphaClipEye;

float _ES_AvatarRimWidthScale;
float _ES_AvatarRimWidth;
float4 _ES_AvatarFrontRimColor;
float _ES_AvatarFrontRimIntensity;
float4 _ES_AvatarBackRimColor;
float _ES_AvatarBackRimIntensity;

float _UseSaturation;
float _UseMaterialMasksTex;
float _UseToonLightMap;
float _UseLightMapColorAO;
float4 _FirstShadowMultColor;
float4 _CoolShadowMultColor;
float _UseVertexColorAO;
float _UseCoolShadowColorOrTex;
float _LightArea;
float _UseShadowTransition;
float _ShadowTransitionRange;
float _ShadowTransitionSoftness;
#if defined(SHADOW_RAMP_ON)
    float _UseShadowRamp;
    float _ShadowRampWidth;
    float _UseVertexRampWidth;
#endif
float _useShadowSoft;
float _shadowSoftRange;
float _UseSpecular;
float _Shininess;
float _SpecMulti;
float _SpecualrInShaow;
float4 _SpecularColor;
float _SpecOpacity;
float _UseCharacterPlaneClips;
#if defined(METAL_MAT)
    float _MetalMaterial;
    float _MTMapBrightness;
    float _MTMapTileScale;
    float4 _MTMapLightColor;
    float4 _MTMapDarkColor;
    float4 _MTShadowMultiColor;
    float _MTShininess;
    float _MTSpecularScale;
    float _MTSpecularAttenInShadow;
    float4 _MTSpecularColor;
    float _MTUseSpecularRamp;
    float _MTSharpLayerOffset;
    float4 _MTSharpLayerColor;
    float _MTSpecularOffset;
    float _MTSpecularShadowScale;
    float _MTSpecularAO;
#endif
float _CharacterEmission;
float _EmissionScaler;
float4 _EmissionScaler_State;
float4 _EmissionScaler_Value1;
float4 _EmissionScaler_Value2;
float4 _EmissionScaler_LerpParam;
float4 _EmissionColor_MHY;
float4 _EmissionColor_MHY1;
float _EmissionScaler1;
float _EnableEmissionBloom;
float4 _EmissionBloomColor;
float _EmissionBloomScale;

float _OutlineType;
float _OutlineWidth;
float _Scale;
float _OutlineCorrectionWidth;
float4 _OutlineColor;
float _OutLineZOffset;
float _OutLineIntensity;
float _MaxOutlineZOffset;
float _OutlineOffsetBlockBChannel;
float4 _OutlineWidthAdjustZs;
float4 _OutlineWidthAdjustScales;
float _UseMaterial2;
float4 _Color2;
float _EmissionScaler2;
float4 _EmissionColor_MHY2;
float4 _FirstShadowMultColor2;
float4 _CoolShadowMultColor2;
float _Shininess2;
float _SpecMulti2;
float _SpecOpacity2;
float4 _SpecularColor2;
float4 _CharacterCubeColor2;
float _OutLineIntensity2;
float4 _OutlineColor2;
float _ShadowTransitionRange2;
float _ShadowTransitionSoftness2;
float _useShadowSoft2;
float _shadowSoftRange2;
float _UseMaterial3;
float4 _Color3;
float _EmissionScaler3;
float4 _EmissionColor_MHY3;
float4 _FirstShadowMultColor3;
float4 _CoolShadowMultColor3;
float _Shininess3;
float _SpecMulti3;
float _SpecOpacity3;
float4 _SpecularColor3;
float4 _CharacterCubeColor3;
float _OutLineIntensity3;
float4 _OutlineColor3;
float _ShadowTransitionRange3;
float _ShadowTransitionSoftness3;
float _useShadowSoft3;
float _shadowSoftRange3;
float _UseMaterial4;
float4 _Color4;
float _EmissionScaler4;
float4 _EmissionColor_MHY4;
float4 _FirstShadowMultColor4;
float4 _CoolShadowMultColor4;
float _Shininess4;
float _SpecMulti4;
float _SpecOpacity4;
float4 _SpecularColor4;
float4 _CharacterCubeColor4;
float _OutLineIntensity4;
float4 _OutlineColor4;
float _ShadowTransitionRange4;
float _ShadowTransitionSoftness4;
float _useShadowSoft4;
float _shadowSoftRange4;
float _UseMaterial5;
float4 _Color5;
float _EmissionScaler5;
float4 _EmissionColor_MHY5;
float4 _FirstShadowMultColor5;
float4 _CoolShadowMultColor5;
float _Shininess5;
float _SpecMulti5;
float _SpecOpacity5;
float4 _SpecularColor5;
float4 _CharacterCubeColor5;
float _OutLineIntensity5;
float4 _OutlineColor5;
float _ShadowTransitionRange5;
float _ShadowTransitionSoftness5;
float _useShadowSoft5;
float _shadowSoftRange5;
#if defined(FACE_MAP_NEW_ON)
float _UseFaceMapNew;
float _FaceMapRotateOffset;
float _FaceMapSoftness;
#endif

#if defined(BACK_FACE_ON)
    float _DrawBackFace;
    float _UseBackFaceUV2;
    float _BackFaceAlphaClipWithUV1;
    float _BackFaceLighting;
#endif
#if defined(MAIN_TEX_COLORING_ON)
    float _MainTexColoring;
    float4 _MainTexTintColor;
#endif
float4 _HitColor;
float4 _ElementRimColor;
float _HitColorScaler;
float _HitColorFresnelPower;
float _EmissionStrengthLerp;
float _UsingDitherAlpha;
float _DitherAlpha;

#if defined(is_paimon)
float4 _StarTex_ST;
float _StarHeight;
float4 _Star02Tex_ST;
float _Star02Height;
float4 _ColorPaletteTex_ST;
float4 _NoiseTex01_ST;
float4 _NoiseTex02_ST;
float4 _ConstellationTex_ST;
float _ConstellationHeight;
float4 _CloudTex_ST;
float _CloudHeight;

float _EnableAlphaTest;
float _CutOff;
float _Star01Speed;
float _ColorPalletteSpeed;
float _StarBrightness;
float _Noise01Speed;
float _Noise02Speed;
float _ConstellationBrightness;
float _Noise03Brightness;
float _CloudBrightness;
Texture2D _StarTex;  
Texture2D _Star02Tex;
Texture2D _ColorPaletteTex;
Texture2D _NoiseTex01;
Texture2D _NoiseTex02;
Texture2D _ConstellationTex;
Texture2D _CloudTex;
#endif
#if defined(is_asmoday)
float4 _FlowColor;
float _FlowScale;
float4 _NoiseMap_ST;
float2 _NoiseSpeed;
float _NoiseScale;
float4 _FlowMap_ST;
float2 _FlowMaskSpeed;
float4 _FlowMap02_ST;
float2 _FlowMask02Speed;
float _FlowMaskPower;
float _FlowMaskScale;
float4 _FlowMask_ST;
float4 _BottomColor01;
float4 _BottomColor02;
float _BottomPower;
float _BottomScale;
Texture2D _NoiseMap;
Texture2D _FlowMap;
Texture2D _FlowMap02;
Texture2D _FlowMask;
#endif
#endif