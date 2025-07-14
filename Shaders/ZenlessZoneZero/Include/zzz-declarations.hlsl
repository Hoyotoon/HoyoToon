// textures : 
Texture2D _MainTex; // this is the diffuse color texture
Texture2D _LightTex;
Texture2D _OtherDataTex;
Texture2D _OtherDataTex2;

// matcaps
Texture2D _MatCapTex;
Texture2D _MatCapTex2;
Texture2D _MatCapTex3;
Texture2D _MatCapTex4;
Texture2D _MatCapTex5;
Texture2D _MatCapTexFallback;

Texture2D _EyeColorMap;
Texture2D _SecondaryEmissionTex;
Texture2D _SecondaryEmissionMaskTex;
Texture2D _ScreenTex;
Texture2D _ScreenMask;

Texture2D _Lut2DTex;

Texture2D _HueMaskTexture;

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float4 _SecondaryEmissionTex_ST;
float4 _SecondaryEmissionMaskTex_ST;
float4 _ScreenTex_ST;
float4 _ScreenMask_ST;

// samplers :  
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;

float _DoubleSided;
float _SymmetryUV;
float _LegacyOtherData;

uniform float _GI_Intensity;
uniform float4x4 _LightMatrix0;

// secondary emission properties: 
float4 _SecondaryEmissionTexSpeed;
float _SecondaryEmissionTexRotation;
float4 _SecondaryEmissionColor;
float _SecondaryEmission;
float _SecondaryEmissionUseUV2;
float _SecondaryEmissionChannel;
float _MultiplyAlbedo;
float _SecondaryEmissionMaskChannel;

// stencil
float _MinStencilAlpha;

// screen properties:
float _ScreenUV;
float _ScreenUVSource;
float _ScreenUVEnum;
float _ScreenImage;
float _ScreenScale;
float _MultiplySrcColor;
float4 _ScreenColor;
float _ScreenTexRotation;
float4 _ScreenTexRotationAxis;
float _ScreenMaskUV;
float4 _ScreenImageUvMove;
float _Blink;
float _BlinkFrequency;
float _BlinkOpacity;

// normal maps :
float _UseBumpMap;
float _BumpScale;

float _DoubleUV;
float _NormalUV;

// 
float _Debug;

// colors : 
float4 _Color;
float4 _Color2;
float4 _Color3;
float4 _Color4;
float4 _Color5;

float _HairMatId;
float _SkinMatId;

// stencil properties; 
float _EnableStencil;
float _HairBlendSilhouette;
float _UseHairSideFade;

// properties : 
float _MaterialType;
float _HardLightWidth;
float3 _headForwardVector;
float3 _headRightVector;
float3 _headUpVector;
float3 _MiddlePointPosition;

// light directions :
float _OverrideMainLight;
float3 _OverrideMainLightBody;
float3 _OverrideMainLightHair;

// shadow settings
float _UseSelfShadow; 

// albedo smoothness :
float _AlbedoSmoothness;
float _AlbedoSmoothness2;
float _AlbedoSmoothness3;
float _AlbedoSmoothness4;
float _AlbedoSmoothness5;

// shadow colors :
float4 _ShadowColor;
float4 _ShadowColor2;
float4 _ShadowColor3;
float4 _ShadowColor4;
float4 _ShadowColor5;
float4 _ShallowColor;
float4 _ShallowColor2;
float4 _ShallowColor3;
float4 _ShallowColor4;
float4 _ShallowColor5;

// post colors : 
float4 _PostShallowTint;
float4 _PostShallowFadeTint;
float4 _PostShadowTint;
float4 _PostShadowFadeTint;
float4 _PostFrontTint;
float4 _PostSssTint;

// head matrix : 
float4 _HeadMatrixWS2OS0;
float4 _HeadMatrixWS2OS1;
float4 _HeadMatrixWS2OS2;
float4 _HeadMatrixWS2OS3;

float _UseLegacyFace;
float _NoseLineLkDnDisp;
float _NoseLineHoriDisp;
float _NoseSmoothX;
float _NoseSmoothY;

// matcap parameters :
float _Emission; 
float _MatCap;
float _CharacterMatCapEnable;
float _UseMatCapMask;
float4 _MatCapColorTint;
float4 _MatCapColorTint2;
float4 _MatCapColorTint3;
float4 _MatCapColorTint4;
float4 _MatCapColorTint5;
float _MatCapAlphaBurst;
float _MatCapAlphaBurst2;
float _MatCapAlphaBurst3;
float _MatCapAlphaBurst4;
float _MatCapAlphaBurst5;
float _MatCapAlphaBurstFallback;
float _MatCapAlphaBurstFx;
float _MatCapBlendMode;
float _MatCapBlendMode2;
float _MatCapBlendMode3;
float _MatCapBlendMode4;
float _MatCapBlendMode5;
float _MatCapBlendModeFallback;
float _MatCapBlendModeFx;
float _MatCapBumpScaleFx;
float _MatCapColorBurst;
float _MatCapColorBurst2;
float _MatCapColorBurst3;
float _MatCapColorBurst4;
float _MatCapColorBurst5;
float _MatCapColorBurstFallback;
float _MatCapColorBurstFx;
float _MatCapId;
float _MatCapNormalUSpeedFx;
float _MatCapNormalVSpeedFx;
float _MatCapRefract;
float _MatCapRefract2;
float _MatCapRefract3;
float _MatCapRefract4;
float _MatCapRefract5;
float _MatCapRefractFallback;
float _MatCapTexID;
float _MatCapTexID2;
float _MatCapTexID3;
float _MatCapTexID4;
float _MatCapTexID5;
float _MatCapUSpeed;
float _MatCapUSpeed2;
float _MatCapUSpeed3;
float _MatCapUSpeed4;
float _MatCapUSpeed5;
float _MatCapUSpeedFallback;
float _MatCapUSpeedFx;
float _MatCapVSpeed;
float _MatCapVSpeed2;
float _MatCapVSpeed3;
float _MatCapVSpeed4;
float _MatCapVSpeed5;
float _MatCapVSpeedFallback;
float _MatCapVSpeedFx;
float _RefractDepth;
float _RefractDepth2;
float _RefractDepth3;
float _RefractDepth4;
float _RefractDepth5;
float _RefractDepthFallback;
float4 _RefractParam;
float4 _RefractParam2;
float4 _RefractParam3;
float4 _RefractParam4;
float4 _RefractParam5;
float4 _RefractParamFallback;

// specular : 
float _Metallic;
float _Glossiness;
float _SpecIntensity;
float4 _HeadSphereNormalCenter;
float _HighlightShape;   
float _HighlightShape2;  
float _HighlightShape3;  
float _HighlightShape4;  
float _HighlightShape5;  
float _ToonSpecular;
float _ToonSpecular2;
float _ToonSpecular3;
float _ToonSpecular4;
float _ToonSpecular5;
float _SpecularRange;
float _SpecularRange2;
float _SpecularRange3;
float _SpecularRange4;
float _SpecularRange5;
float _ShapeSoftness;
float _ShapeSoftness2;
float _ShapeSoftness3;
float _ShapeSoftness4;
float _ShapeSoftness5;
float4 _SpecularColor;
float4 _SpecularColor2;
float4 _SpecularColor3;
float4 _SpecularColor4;
float4 _SpecularColor5;
float _ModelSize;
float _ModelSize2;
float _ModelSize3;
float _ModelSize4;
float _ModelSize5;

// emission : 
float4 _EmissionColor;
float4 _EmissionColor2;
float4 _EmissionColor3;
float4 _EmissionColor4;
float4 _EmissionColor5;

// outline : 
float _Outline;
float _UseLightMapOL;
float _OutlineZOff;
float _MaxOutlineZOffset;
float _DisableFOVScalingOL;
float _OutlineWidth;
float _OutlineWidthUIAdjustment;
float4 _OutlineColor;
float4 _OutlineColor2;
float4 _OutlineColor3;
float4 _OutlineColor4;
float4 _OutlineColor5;

// rim glow
float _RimGlow;
float _RimWidth;
float4 _RimGlowLightColor;
float4 _RimGlowLightColor2;
float4 _RimGlowLightColor3;
float4 _RimGlowLightColor4;
float4 _RimGlowLightColor5;
float4 _UISunColor;
float4 _UISunColor2;
float4 _UISunColor3;
float4 _UISunColor4;
float4 _UISunColor5;

// color hue
float _EnableHueShift;
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

float4 _Lut2DTexParam;
float _EnableLUT;

float _MultiLight;
float _FilterLight;
float _ApplyLighting;

// alpha
float _UseAlpha;
float _AlphaCutoff;

// debug : 
float _DebugMode;
float _DebugDiffuse;
float _DebugLightMap;
float _DebugOtherData;
float _DebugOtherData2;
float _DebugVertexColor;
float _DebugUV;
float _DebugTangent;
float _DebugNormalVector;
float _DebugMatcap;
float _DebugSpecular;
float _DebugRimLight;
float _DebugFaceVector;
float _DebugEmission;
float _DebugLights;
float _DebugMaterialIDs;