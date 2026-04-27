Shader "HoyoToon/Honkai Star Rail/UI/Manikin/Floor_SoftEdged"
{
    Properties
    {
    _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
    _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
    [Space] [Header(Rim)] [HDR] _RimColor ("Rim Color", Color) = (1,1,1,0)
    _RimRange ("Rim Range", Range(0, 50)) = 4
    [Space] [Header(Other)] _ViewOffset ("View Offset", Range(0, 0.8)) = 0.05
    _ReflectWeight ("Reflect Weight", Range(0, 1)) = 0.5
    _ReflectionThs ("Reflect Alpha", Range(0, 1)) = 0.2
    [Header(SoftEdge)] _MainTex ("Main Texture", 2D) = "white" { }
    _Pattern1 ("Main Texture Noise Pattern", Vector) = (1,1,0,0)
    _Pattern1SpeedX ("Main Texture Noise Speed", Range(-1, 1)) = 0
    _Pattern1SpeedY ("Main Texture Noise Speed", Range(-1, 1)) = 0
    _MainDistortion ("Main Texture Distortion", Range(0, 0.5)) = 0
    _MainSpeedX ("Main Tex Speed X", Range(-5, 5)) = 0
    _MainSpeedY ("Main Tex Speed Y", Range(-5, 5)) = 0
    _Color ("Base Color", Color) = (1,1,1,1)
    _Color1 ("Second Color", Color) = (1,1,1,1)
    _Color2 ("Third Color", Color) = (1,1,1,1)
    _CenterPos ("Color Saturation Center", Vector) = (0,0,0,1)
    _SatRangeStar ("Color,Saturation Range", Range(0, 5)) = 0
    _SatRangeEnd ("Color,Saturation Range", Range(0, 5)) = 1
    _ColorRangeMin ("Color1 Range", Range(0, 1)) = 0
    _ColorRangeMax ("Color2 Range", Range(0, 1)) = 0
    _ColorRangeTop ("Color1 Range Top", Range(0, 1)) = 1
    _MaskTex ("Mask Texture", 2D) = "white" { }
    _MaskSpeedX ("Mask Tex Speed X", Range(-5, 5)) = 0
    _MaskSpeedY ("Mask Tex Speed Y", Range(-5, 5)) = 0
    _MaskRangeMin ("Mask Tex Bottom Range Min", Range(0, 1)) = 0
    _MaskRangeMax ("Mask Tex Bottom  Range Max", Range(0, 1)) = 0
    _MainRangeMin ("Alpha Top Range Min", Range(0, 1)) = 0
    _MainRangeMax ("Alpha Top Range Max", Range(0, 1)) = 0
    _MaskDistortion ("Alpha Range Distortion", Range(0, 0.5)) = 0
    _CenterPos1 ("Reflection Center 1&2", Vector) = (0,0,0,0)
    _CenterPos2 ("Reflection Center 3&4", Vector) = (0,0,0,0)
    _CenterPos3 ("Reflection Length 1&2&3&4", Vector) = (0.1,0.1,0.1,0.1)
    _Opacity ("Opacity", Range(0, 1)) = 1
    _BaseTex ("Dot Tex", 2D) = "black" { }
    _BaseTexInst ("Dot Texture Intensity", Range(0, 1)) = 0
    _BaseTexRange ("Dot Texture Visible Range", Range(0, 1)) = 0.01
    _BaseTexSharp ("Dot Texture Shapen Amount", Range(0, 1)) = 0.01
    _BaseTexMax ("Dot Texture Shape Size", Range(0, 1)) = 1
    _BaseTexSpeed ("Dot Texture Speed", Vector) = (0,0,0,0)
    [HDR] _Color3 ("Dot Color", Color) = (1,1,1,1)
    [Enum(Off,0, On,1)] _ZWriteMode ("ZWriteMode", Float) = 1
    [MHYHeaderBox(Stencil)] [IntRange] _StencilRefValue ("StencilRefValue", Range(0, 255)) = 0
    [IntRange] _StencilReadMask ("StencilReadMask", Range(0, 255)) = 255
    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("StencilComp", Float) = 8
    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("StencilPass", Float) = 2

    }
    SubShader
    {
        Name "CustomForward"
        Tags { "LIGHTMODE" = "CustomForwardOpaque" "QUEUE" = "Geometry-10" "RenderType" = "Opaque" }
        Offset 20, 20
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        ENDHLSL

        Pass
        {
            Name "CustomForward"
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry-10" "RenderType" = "Opaque" }
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            ZWrite [_ZWriteMode]
            HLSLPROGRAM
                #include "Common/Declarations.hlsl"
                #pragma vertex vert
                #pragma fragment frag

                CBUFFER_START(UnityPerMaterial)
                    float4 _MainTex_ST;
                    float4 _BaseTex_ST;
                    float4 _BottomTex_ST;
                    float4 _LightTex_ST;
                    float4 _BaseColor;
                    float4 _BottomColor;
                    float4 _LightColor;
                    float4 _RimColor;
                    float4 _Pattern1;
                    float4 _Color;
                    float4 _Color1;
                    float4 _Color2;
                    float4 _Color3;
                    float4 _MainSpeed;
                    float4 _BaseTexSpeed;
                    float _ReflectWeight;
                    float _LightIntensity;
                    float _LightPower;
                    float _LightOffset;
                    float _ViewOffset;
                    float _RimRange;
                    float _BottomCorrection;
                    float _AdditionalReflectionCubeMip;
                    float4 _AdditionalReflectionColor;
                    float _AddtionalReflactionAlpha;
                    float _Angle;
                    float _ReflectReturn;
                    float _ReflectAlpha;
                    float _ReflectClip;
                    float _Pattern1SpeedX;
                    float _Pattern1SpeedY;
                    float _MainDistortion;
                    float _MainSpeedX;
                    float _MainSpeedY;
                    float _SatRangeStar;
                    float _SatRangeEnd;
                    float _ColorRangeMin;
                    float _ColorRangeMax;
                    float _ColorRangeTop;
                    float _MaskRangeMin;
                    float _MaskRangeMax;
                    float _MainRangeMin;
                    float _MainRangeMax;
                    float _MaskDistortion;
                    float _Opacity;
                    float _BaseTexInst;
                    float _BaseTexRange;
                    float _BaseTexSharp;
                    float _BaseTexMax;
                CBUFFER_END

                TEXTURECUBE(_AdditionalReflectionCube);
                SAMPLER(sampler_AdditionalReflectionCube);
                TEXTURE2D(_ReflectionColor);
                TEXTURE2D(_MainTex);
                TEXTURE2D(_BaseTex);
                TEXTURE2D(_BottomTex);
                TEXTURE2D(_LightTex);
                SAMPLER(sampler_linear_repeat);
                SAMPLER(sampler_linear_clamp);
                // UNITY_LOCATION(0) uniform samplerCube _AdditionalReflectionCube;
                // UNITY_LOCATION(1) uniform sampler2D _ReflectionColor;
                // UNITY_LOCATION(2) uniform sampler2D _BaseTex;
                // UNITY_LOCATION(3) uniform sampler2D _BottomTex;
                // UNITY_LOCATION(4) uniform sampler2D _LightTex;


                struct vertex_input
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float2 uv1 : TEXCOORD1;
                    float2 uv2 : TEXCOORD2;
                    float4 color : COLOR;
                    float4 tangent : TANGENT;
                    float3 normal : NORMAL;
                };

                struct vertex_output 
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float2 uv1 : TEXCOORD1;
                    float2 uv2 : TEXCOORD2;
                    float4 ss_pos : TEXCOORD3;
                    float4 color : COLOR;
                    float3 view : TEXCOORD5;
                    float3 normal : TEXCOORD6;
                    float3 world_pos : TEXCOORD7;
                    float4 screen_pos : TEXCOORD8;
                };  

                float SmoothCubic(float x)
                {
                    return x * x * (3.0 - 2.0 * x);
                }

                float SafeRemap01(float value, float minValue, float maxValue)
                {
                    float denom = max(maxValue - minValue, 1e-5);
                    return saturate((value - minValue) / denom);
                }

                float Hash11(float2 p)
                {
                    return frac(sin(dot(p, float2(127.099998, 311.700012))) * 43758.545);
                }

                float GradientNoise(float2 p)
                {
                    float2 cell = floor(p);
                    float2 f = frac(p);

                    float g00 = Hash11(cell) * 2.0 - 1.0;
                    float g10 = Hash11(cell + float2(1.0, 0.0)) * 2.0 - 1.0;
                    float g01 = Hash11(cell + float2(0.0, 1.0)) * 2.0 - 1.0;
                    float g11 = Hash11(cell + float2(1.0, 1.0)) * 2.0 - 1.0;

                    float n00 = dot(float2(g00, g00), f);
                    float n10 = dot(float2(g10, g10), f - float2(1.0, 0.0));
                    float n01 = dot(float2(g01, g01), f - float2(0.0, 1.0));
                    float n11 = dot(float2(g11, g11), f - float2(1.0, 1.0));

                    float2 u = f * f * (3.0 - 2.0 * f);
                    return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
                }

                vertex_output vert(vertex_input v)
                {
                    vertex_output o;

                    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    float4 clipPos = TransformWorldToHClip(worldPos);

                    o.vertex = clipPos;
                    o.uv = v.uv;
                    o.uv1 = v.uv1;
                    o.uv2 = v.uv2;
                    o.color = v.color;
                    o.view = _WorldSpaceCameraPos.xyz - worldPos;
                    o.normal = TransformObjectToWorldNormal(v.normal);
                    o.world_pos = worldPos;
                    o.ss_pos = clipPos;
                    o.screen_pos = ComputeScreenPos(clipPos);

                    return o;
                }

                


                float4 frag(vertex_output i) : SV_Target
                {
                    float4 SV_Target0;
                    float4 u_xlat0;
                    float4 u_xlat1;
                    float u_xlat16_1;
                    float4 u_xlat2;
                    float4 u_xlat16_3;
                    float4 u_xlat16_4;
                    float4 u_xlat5;
                    float u_xlat16_5;
                    float2 u_xlat6;
                    float4 u_xlat16_8;
                    float4 u_xlat16_9;
                    float2 u_xlat10;
                    float2 u_xlat11;
                    float u_xlat15;
                    float u_xlat16_18;
                    u_xlat0.xy = i.world_pos.xy * _Pattern1.xy + _Pattern1.zw;
                    u_xlat0.xy = float2(_Pattern1SpeedX, _Pattern1SpeedY) * _Time.yy + u_xlat0.xy;
                    u_xlat10.xy = floor(u_xlat0.xy);
                    u_xlat0.xy = frac(u_xlat0.xy);
                    u_xlat1.xy = u_xlat10.xy + float2(1.0, 1.0);
                    u_xlat1.x = dot(u_xlat1.xy, float2(127.099998, 311.700012));
                    u_xlat1.x = sin(u_xlat1.x);
                    u_xlat1.x = u_xlat1.x * 4.37585449;
                    u_xlat1.x = frac(u_xlat1.x);
                    u_xlat1.x = u_xlat1.x * 2.0 + -1.0;
                    u_xlat6.xy = u_xlat0.xy + float2(-1.0, -1.0);
                    u_xlat1.x = dot(u_xlat1.xx, u_xlat6.xy);
                    u_xlat2 = u_xlat10.xyxy + float4(1.0, 0.0, 0.0, 1.0);
                    u_xlat10.x = dot(u_xlat10.xy, float2(127.099998, 311.700012));
                    u_xlat10.x = sin(u_xlat10.x);
                    u_xlat10.x = u_xlat10.x * 4.37585449;
                    u_xlat10.x = frac(u_xlat10.x);
                    u_xlat10.x = u_xlat10.x * 2.0 + -1.0;
                    u_xlat10.x = dot(u_xlat10.xx, u_xlat0.xy);
                    u_xlat15 = dot(u_xlat2.zw, float2(127.099998, 311.700012));
                    u_xlat6.x = dot(u_xlat2.xy, float2(127.099998, 311.700012));
                    u_xlat6.x = sin(u_xlat6.x);
                    u_xlat6.x = u_xlat6.x * 4.37585449;
                    u_xlat6.x = frac(u_xlat6.x);
                    u_xlat6.x = u_xlat6.x * 2.0 + -1.0;
                    u_xlat15 = sin(u_xlat15);
                    u_xlat15 = u_xlat15 * 4.37585449;
                    u_xlat15 = frac(u_xlat15);
                    u_xlat15 = u_xlat15 * 2.0 + -1.0;
                    u_xlat2 = u_xlat0.xyxy + float4(-1.0, -0.0, -0.0, -1.0);
                    u_xlat10.y = dot(float2(u_xlat15.xx), u_xlat2.zw);
                    u_xlat1.y = dot(u_xlat6.xx, u_xlat2.xy);
                    u_xlat1.xy = (-u_xlat10.yx) + u_xlat1.xy;
                    u_xlat11.xy = u_xlat0.xy * u_xlat0.xy;
                    u_xlat0.xy = (-u_xlat0.xy) * float2(2.0, 2.0) + float2(3.0, 3.0);
                    u_xlat0.xy = u_xlat0.xy * u_xlat11.xy;
                    u_xlat15 = u_xlat0.x * u_xlat1.x + u_xlat10.y;
                    u_xlat0.x = u_xlat0.x * u_xlat1.y + u_xlat10.x;
                    u_xlat10.x = (-u_xlat0.x) + u_xlat15;
                    u_xlat0.x = u_xlat0.y * u_xlat10.x + u_xlat0.x;
                    u_xlat0.xy = u_xlat0.xx * float2(_MainDistortion, _MaskDistortion) + i.uv1.yy;
                    u_xlat16_3.x = u_xlat0.y + (-_MaskRangeMin);
                    u_xlat16_8.x = (-_MaskRangeMin) + _MaskRangeMax;
                    u_xlat16_8.x = float(1.0) / u_xlat16_8.x;
                    u_xlat16_3.x = u_xlat16_8.x * u_xlat16_3.x;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat16_3.x = min(max(u_xlat16_3.x, 0.0), 1.0);
                #else
                    u_xlat16_3.x = clamp(u_xlat16_3.x, 0.0, 1.0);
                #endif
                    u_xlat16_8.x = u_xlat16_3.x * -2.0 + 3.0;
                    u_xlat16_3.x = u_xlat16_3.x * u_xlat16_3.x;
                    u_xlat16_3.x = u_xlat16_3.x * u_xlat16_8.x;
                    u_xlat16_3.x = u_xlat16_3.x * i.color.w;
                    u_xlat0.z = i.uv1.x;
                    u_xlat5.xy = u_xlat0.zx * _MainTex_ST.xy + _MainTex_ST.zw;
                    u_xlat5.xy = _Time.yy * float2(_MainSpeedX, _MainSpeedY) + u_xlat5.xy;
                    u_xlat16_5 = _MainTex.Sample(sampler_linear_repeat, u_xlat5.xy).x;
                    u_xlat16_8.x = u_xlat16_5 + -1.0;
                    u_xlat16_3.x = u_xlat16_3.x * u_xlat16_8.x + 1.0;
                    u_xlat5.x = u_xlat0.x + (-_MainRangeMin);
                    u_xlat0.xz = u_xlat0.xx + (-float2(_ColorRangeMax, _ColorRangeMin));
                    u_xlat15 = (-_MainRangeMin) + _MainRangeMax;
                    u_xlat15 = float(1.0) / u_xlat15;
                    u_xlat5.x = u_xlat15 * u_xlat5.x;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat5.x = min(max(u_xlat5.x, 0.0), 1.0);
                #else
                    u_xlat5.x = clamp(u_xlat5.x, 0.0, 1.0);
                #endif
                    u_xlat15 = u_xlat5.x * -2.0 + 3.0;
                    u_xlat5.x = u_xlat5.x * u_xlat5.x;
                    u_xlat5.x = u_xlat15 * u_xlat5.x + -1.0;
                    u_xlat15 = i.uv1.y + -0.0500000007;
                    u_xlat15 = u_xlat15 * 6.66666651;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat15 = min(max(u_xlat15, 0.0), 1.0);
                #else
                    u_xlat15 = clamp(u_xlat15, 0.0, 1.0);
                #endif
                    u_xlat1.x = u_xlat15 * -2.0 + 3.0;
                    u_xlat15 = u_xlat15 * u_xlat15;
                    u_xlat15 = u_xlat15 * u_xlat1.x;
                    u_xlat5.x = u_xlat15 * u_xlat5.x + 1.0;
                    u_xlat16_3.x = u_xlat16_3.x * u_xlat5.x;
                    SV_Target0.w = u_xlat16_3.x * _Opacity;
                    u_xlat5.xz = (-float2(_ColorRangeMax, _ColorRangeMin)) + float2(_ColorRangeTop, _ColorRangeMax);
                    u_xlat5.xz = float2(1.0, 1.0) / u_xlat5.xz;
                    u_xlat0.xy = u_xlat5.xz * u_xlat0.xz;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat0.xy = min(max(u_xlat0.xy, 0.0), 1.0);
                #else
                    u_xlat0.xy = clamp(u_xlat0.xy, 0.0, 1.0);
                #endif
                    u_xlat10.xy = u_xlat0.xy * float2(-2.0, -2.0) + float2(3.0, 3.0);
                    u_xlat0.xy = u_xlat0.xy * u_xlat0.xy;
                    u_xlat0.xy = u_xlat0.xy * u_xlat10.xy;
                    u_xlat16_3.x = u_xlat0.y * i.color.w;
                    u_xlat16_8.xyz = _Color.xyz + (-_Color1.xyz);
                    u_xlat16_8.xyz = u_xlat0.xxx * u_xlat16_8.xyz + _Color1.xyz;
                    u_xlat0.xy = i.screen_pos.xy / i.screen_pos.ww;
                    u_xlat0.xy = (-u_xlat0.xy) + float2(0.5, 0.5);
                    u_xlat0.xy = -abs(u_xlat0.xy) + float2(1.0, 1.0);
                    u_xlat16_4.x = u_xlat0.y + 0.100000001;
                    u_xlat16_4.x = u_xlat16_4.x * 1.08108103;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat16_4.x = min(max(u_xlat16_4.x, 0.0), 1.0);
                #else
                    u_xlat16_4.x = clamp(u_xlat16_4.x, 0.0, 1.0);
                #endif
                    u_xlat16_9.x = u_xlat16_4.x * -2.0 + 3.0;
                    u_xlat16_4.x = u_xlat16_4.x * u_xlat16_4.x;
                    u_xlat16_4.x = u_xlat16_4.x * u_xlat16_9.x;
                    u_xlat16_4.x = u_xlat0.x * u_xlat16_4.x + (-_SatRangeStar);
                    u_xlat16_9.x = (-_SatRangeStar) + _SatRangeEnd;
                    u_xlat16_9.x = float(1.0) / u_xlat16_9.x;
                    u_xlat16_4.x = u_xlat16_9.x * u_xlat16_4.x;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat16_4.x = min(max(u_xlat16_4.x, 0.0), 1.0);
                #else
                    u_xlat16_4.x = clamp(u_xlat16_4.x, 0.0, 1.0);
                #endif
                    u_xlat16_9.x = u_xlat16_4.x * -2.0 + 3.0;
                    u_xlat16_4.x = u_xlat16_4.x * u_xlat16_4.x;
                    u_xlat16_4.x = u_xlat16_4.x * u_xlat16_9.x;
                    u_xlat16_9.x = dot(float3(0.300000012, 0.589999974, 0.109999999), _Color2.xyz);
                    u_xlat16_9.xyz = u_xlat16_9.xxx + (-_Color2.xyz);
                    u_xlat16_4.xyz = u_xlat16_4.xxx * u_xlat16_9.xyz + _Color2.xyz;
                    u_xlat16_8.xyz = u_xlat16_8.xyz + (-u_xlat16_4.xyz);
                    u_xlat16_3.xyz = u_xlat16_3.xxx * u_xlat16_8.xyz + u_xlat16_4.xyz;
                    u_xlat0.xyz = (-u_xlat16_3.xyz) + _Color3.xyz;
                    u_xlat15 = dot(i.view.xyz, i.view.xyz);
                    u_xlat15 = rsqrt(u_xlat15);
                    u_xlat1.xyz =  u_xlat15  * i.view.xyz;
                    u_xlat15 = dot(i.normal.xyz, i.normal.xyz);
                    u_xlat15 = rsqrt(u_xlat15);
                    u_xlat2.xyz = u_xlat15 * i.normal.xyz;
                    u_xlat15 = dot(u_xlat2.xyz, u_xlat1.xyz);
                    u_xlat1.x = float(1.0) / _BaseTexRange.x;
                    u_xlat15 = u_xlat15 * u_xlat1.x;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat15 = min(max(u_xlat15, 0.0), 1.0);
                #else
                    u_xlat15 = clamp(u_xlat15, 0.0, 1.0);
                #endif
                    u_xlat1.x = u_xlat15 * -2.0 + 3.0;
                    u_xlat15 = u_xlat15 * u_xlat15;
                    u_xlat15 = u_xlat15 * u_xlat1.x;
                    u_xlat15 = u_xlat15 * _BaseTexInst;
                    u_xlat1.xy = i.uv.xy * _BaseTex_ST.xy + _BaseTex_ST.zw;
                    u_xlat1.xy = _BaseTexSpeed.xy * _Time.yy + u_xlat1.xy;
                    u_xlat16_1 = _BaseTex.Sample(sampler_linear_repeat, u_xlat1.xy).x;
                    u_xlat16_18 = u_xlat16_1 + (-_BaseTexSharp);
                    u_xlat16_4.x = (-_BaseTexSharp) + _BaseTexMax;
                    u_xlat16_4.x = float(1.0) / u_xlat16_4.x;
                    u_xlat16_18 = u_xlat16_18 * u_xlat16_4.x;
                #ifdef UNITY_ADRENO_ES3
                    u_xlat16_18 = min(max(u_xlat16_18, 0.0), 1.0);
                #else
                    u_xlat16_18 = clamp(u_xlat16_18, 0.0, 1.0);
                #endif
                    u_xlat16_4.x = u_xlat16_18 * -2.0 + 3.0;
                    u_xlat16_18 = u_xlat16_18 * u_xlat16_18;
                    u_xlat16_18 = u_xlat16_18 * u_xlat16_4.x;
                    u_xlat15 = u_xlat15 * u_xlat16_18;
                    u_xlat0.xyz = u_xlat15 * u_xlat0.xyz + u_xlat16_3.xyz;
                    SV_Target0.xyz = u_xlat0.xyz;
                    return SV_Target0;
                }   
            ENDHLSL

        }
    }

    CustomEditor "LWGUI.LWGUI" 
    FallBack "Diffuse"
}