// Textures
Texture2D _MainTex;
Texture2D _OutlineTexture;
Texture2D _Normal_Roughness_Metallic;
Texture2D _MaskTex;
Texture2D _TypeMask;
Texture2D _Mask;
Texture2D _Ramp;
Texture2D _MatCapTex;
Texture2D _HeightLightMap;
Texture2D _HeightLightTex;
Texture2D _EM;
Texture2D _D;
Texture2D _SDF;
Texture2D _Noise;
Texture2D _Noise02;
Texture2D _ShakeNoise;
Texture2D _Second_RGB;
Texture2D _CommonNoiseMap;
Texture2D _Lut2DTex;
SamplerState sampler_MainTex;
SamplerState sampler_linear_repeat;
SamplerState sampler_linear_clamp;

float4 _CommonNoiseMap_ST;


float4 _EM_TexelSize; // x 1/height y 1/width z height w width


UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

// material shit
float _MaterialType;

// alpha shit
float _UseTranslucent;
float _AlphaMode;
float _AlphaClipRate;

// lighting shit
float _MultiLight;
float _FilterLight;
uniform float _GI_Intensity;
uniform float4x4 _LightMatrix0;

// masking shit
float _UseToonMask;
float _UseSDFShadow;
float _UseSkinMask;
float _UseMainTexA;
float _UseRampMask;

// facing directions for face shadow and stencil fading 
float4 _headUpVector;
float4 _headForwardVector;
float4 _headRightVector;

// Colors 
float4 _BaseColor;
float4 _SkinColor;
float4 _SubsurfaceColor;
float4 _SkinSubsurfaceColor;

// specular
float _UseToonSpecular;
float _ToonMaxSpecular;
float _SpecularPower;
float _SpecStrength;
float _MetalSpecularPower;
float _MetalMatCapBack;
float _MatCapInt;
float _MetalMatCapInt;

// general shadow 
float _ForceShadowView;
float _ShadowProcess;
float _ShadowWidth;
float _BackShadowProcessOffset;
float _FrontShadowProcessOffset;
float _ShadowOffsetPower;
float _DecodeShadowTreshold;
float _SolidShadowWidth;
float _SolidShadowProcess;
float _SolidShadowStrength;
float _MaskShadowOffsetStrength;

float4 _ShadowColor;
float4 _SkinShadowColor;

// shadow ramp shit
float _RampProcess;
float _RampWidth;
float _UseFaceRamp;
float _RampAdd; 
float _UseRampColor;
float _RampPosition;
float _RampInt;
float _HairUseShadowRamp;
float _HairRampPosition;
float _HairRampIntensity;
float _UseSelfShadow; 

// stocking
float _UseStocking;
float4 _AnistropyColor;
float4 _StockingLightColor;
float4 _StockingEdgeColor;
float4 _StockingColor;
float _AnistropyInt;
float _AnistropyNormalInt;
float _Stocking_KneeSkinIntensityOffset;
float _Stocking_KneeSkinRangeOffset;
float _StockingIntensity;
float _StockingLightRangeMax;
float _StockingLightRangeMin;
float _StockingRangeMax;
float _StockingRangeMiddle;
float _StockingRangeMin;
float _StockingSkinRange;

// hair shadow
float _EnableHairShadow;
float4 _HairShadowColor;
float _WriteToMask;
float3 _HairDistance;
float _HairDepthOffset;

// face shadow 
float _FaceMapRotateOffset;
float _SDFChannel;
float _SDFSmoothness;

// normal mapping
float _UseNormalMap;
float _NormalStrength;
float _NormalFlip;

// outline
float _Outline;
float _UseMainTex;
float _CurvatureStrength;
float _OutlineWidth;
float4 _OutlineColor;
float4 _OutlineColorTint;
float _UseVertexGreen_OutlineWidth;
float _UseVertexColorB_InnerOutline;

// high lights 
float _EyeScale;
float _HeightRatioInput;
float _RotateAngle;
float _LightShakeSpeed;
float _LightShakeScale;
float _SecondLight_PositionX;
float _SecondLight_PositionY;
float _LightPositionX;
float _LightPositionY;
float _UseHeightLightShape;
float _UseEyeSDF;
float _HeightLight_PositionX;
float _HeightLight_PositionY;
float _HeightLight_WidthX;
float _HeightLight_WidthY;
float _LightShakPositionX;
float _LightShakPositionY;

// parallax 
int _ParallaxSteps;
float _ParallaxHeight;

// emission
float _UseBreathLight;
float4 _EmissionColor;
float _EmissionStrength;
float _EmissionBreathThreshold;
float _UseEyeEmission;
float _EyeBreathThreshold;
float4 _EyeEmissionColor;
float _EyeEmissionStrength;

// tacet mark
float4 _SDFColor;
float _SDFStart;
float _ShakeNoisex_Speed;
float _ShakeNoise_YSpeed;
float _ShakeNoiseIntensity;
float _ShakeNoiseTilingX;
float _ShakeNoiseTilingY;
float _UseRotate180UV;
float _RotationAngle;
float _SoundWaveSpeed01;
float _SoundWaveInt01;
float _SoundWaveTiling01;
float _SoundWaveSpeed02;
float _SoundWaveInt02;
float _SoundWaveTiling02;
float _MaskingSide; // 0 middle, 1 left, 2 right
float _XingHenControl;
float _XingHenControlMax;
float _XingHenPosition;
float _XHTimeSpeed;
float _XHIntensity;
float _XHPower;
float _XHMax;
float _UseXingHen;
float _UseRotation;
float _UVpositionX;
float _UVpositionY;
float _UVScaleX;
float _UVScaleY;

float _UseToneMapping;

// gradient 
float _UseUVGradient_DYN;
float _UvGradientScale;
float _UvGradientProcess;
float _UVGradientIn;
float _UVSaturationIntensity;
float4 _UVGradientColor;

// stencil
float _EnabelStencil;
float _AlphaStencil;
float _StencilTrans;

// rim 
float _EnableRimLight;
float _RimWidth;
float4 _RimColor;
float _RimHardness;

// pbr
float _Metalllic;
float _UsePBR;

// aurora
float _UseStarrySky;
float _AuroraAmount;
float _Second_NoiseStrength;
float _Second_Height;
float _Second_HeightRadio;
float _UseNoise;
float _WaveAmount;
float _WaveNormalAmount;
float _WaveSpeed;
float _WaveTiling;

// debug
float _DebugMode;
float _DebugDiffuse;
float _DebugMaskTex;
float _DebugTypeMask;
float _DebugNormalMap;
float _DebugVertexColor;
float _DebugMask;
float _DebugRimLight;
float _DebugNormalVector;
float _DebugTangent;
float _DebugMatcap;
float _DebugSpecular;
float _DebugShadow;
float _DebugStocking;
float _DebugFaceVector;
float _DebugLights;
