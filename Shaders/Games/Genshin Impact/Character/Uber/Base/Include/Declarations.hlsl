TEXTURE2D(_MainTex);

float _DummyFixedForNormal;
float _MHYZBias;
float4 _MainTex_ST;
float _CharacterAmbientSensorShadowOn;
float _CharacterAmbientSensorForceShadowOn;
float4 _AmbientSensorUVs;
float _UsingDitherAlpha;
float _DitherAlpha;
float4 mhy_AvatarLightDir;
float4 mhy_CharacterOverrideLightDir;
float4 mhy_CharacterOverrideLightDirInShadow;
float4 _mhyJittered;

TEXTURE2D(_CharacterAmbientSensorTex);
SAMPLER(sampler_linear_repeat);
SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);
SAMPLER(sampler_point_repeat);