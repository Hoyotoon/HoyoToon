// textures 
Texture2D _FaceMap;
Texture2D _FaceExpression;
Texture2D _SpecialEyeShapeTexture;

float4 _SpecialEyeShapeTexture_ST;

float3 _NewLocalLightDir;

int _UVChannelFront;
int _UVChannelBack;
float _UseUVChannel2;
float _BackShadowRange;
float4 _VertexShadowColor;
float4 _VertexColorSwitch;
float4 _Color;
float4 _BackColor;
float4 _EnvColor;
float4 _AddColor;
float _UseMaterialValuesLUT;

// lines 
float _RimShadowOffset;
float _NoseLinePower;
float4 _NoseLineColor;
float _LipLineFixScale;
float4 _LipLinefixColor;
float _LipLineFixThrd;
float _LipLineFixStart;
float _LipLineFixMax;
float _LipLineFixSC;

float4 _EyeBaseShadowColor;

// face map
float4 _ExCheekColor;
float _ExMapThreshold;
float _ExSpecularIntensity;
float _ExCheekIntensity;
float4 _ExShyColor;
float _ExShyIntensity;
float4 _ExShadowColor;
float4 _ExEyeColor;
float _ExShadowIntensity;

float _ShadowFeather;
float _EyeShadowAngleMin;
float _EyeShadowMaxAngle;
float4 _ShadowColor;
float4 _EyeShadowColor;

float _EyeEffectProcs;
float4 _EyeEffectColor;

// special eye
float _UseSpecialEye;
float4 _EyeCenter;
float4 _EyeSPColor1;
float4 _EyeSPColor2;
float _SpecialEyeIntensity;

float _RimShadowCt;
float _RimShadowWidth;
float _RimShadowFeather;
float3 _RimShadowColor;
float _RimShadowIntensity;


// emission
float _EmissionThreshold;
float _EmissionIntensity;
float4 _EmissionTintColor;

// outline
float _OutlineFixRange1;
float _OutlineFixRange2;
float _OutlineFixRange3;
float _OutlineFixRange4;
float _FixLipOutline;
float _OutlineFixFront;
float _OutlineFixSide;
float _OutlineWidth;
float _OutlineScale;
float4 _OutlineColor;

