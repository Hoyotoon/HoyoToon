Shader "HoyoToon/Honkai Star Rail/UI/Manikin/Floor"
{
    Properties
    {
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
    _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
    [Header(Base)] _BaseColor ("Base Color", Color) = (1,1,1,1)
    _BaseTex ("Base Tex", 2D) = "white" { }
    [Space] [Header(Bottom)] _BottomColor ("Bottom Color", Color) = (1,1,1,0)
    _BottomTex ("Bottom Tex", 2D) = "white" { }
    [Space] [Header(Light)] _LightColor ("Light Color", Color) = (1,1,1,0)
    _LightIntensity ("Light Intensity", Range(0, 2)) = 1
    _LightPower ("Light Power", Range(0, 20)) = 1
    _LightOffset ("LightOffset", Range(0, 1)) = 0
    _LightTex ("Light Tex", 2D) = "white" { }
    [Space] [Header(Rim)] [HDR] _RimColor ("Rim Color", Color) = (1,1,1,0)
    _RimRange ("Rim Range", Range(0, 50)) = 4
    [Space] [Header(Other)] _ViewOffset ("View Offset", Range(0, 0.8)) = 0.05
    _ReflectWeight ("Reflect Weight", Range(0, 10)) = 0.5
    _AdditionalReflectionCube ("Additional ReflectionCube", Cube) = "gray" { }
    _AdditionalReflectionCubeMip ("Additional ReflectionCubeMip", Range(0, 9)) = 2
    [HDR] _AdditionalReflectionColor ("Additional Reflection Color", Color) = (1,1,1,1)
    _AddtionalReflactionAlpha ("Addtional Reflaction Alpha", Range(0, 1)) = 0.5
    _Angle ("Angle", Float) = 0
    [Header(Direct Reflect)] [Toggle] _ReflectReturn ("Reflect Return", Float) = 0
    _ReflectAlpha ("Reflect Alpha", Range(0, 1)) = 1
    [HideInInspector] _ReflectClip ("reflect Clip", Range(0, 1)) = 0
    [MHYHeaderBox(Stencil)] [IntRange] _StencilRefValue ("StencilRefValue", Range(0, 255)) = 0
    [IntRange] _StencilReadMask ("StencilReadMask", Range(0, 255)) = 255
    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("StencilComp", Float) = 8
    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("StencilPass", Float) = 2

    }
    SubShader
    {
        Name "CustomForward"
        Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry+31" "RenderType" = "Opaque" }
        Offset 20, 20
        Blend SrcAlpha OneMinusSrcAlpha, Zero One
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        ENDHLSL

        Pass
        {
            Name "CustomForward"
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry+31" "RenderType" = "Opaque" }
            HLSLPROGRAM
            
                #include "Common/Declarations.hlsl"
                #pragma vertex vert
                #pragma fragment frag

                CBUFFER_START(UnityPerMaterial)
                    float4 _BaseTex_ST;
                    float4 _BottomTex_ST;
                    float4 _LightTex_ST;
                    float4 _BaseColor;
                    float4 _BottomColor;
                    float4 _LightColor;
                    float4 _RimColor;
                    float4 _MainSpeed;
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
                CBUFFER_END

                TEXTURECUBE(_AdditionalReflectionCube);
                SAMPLER(sampler_AdditionalReflectionCube);
                TEXTURE2D(_ReflectionColor);
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
                };  

                vertex_output vert(vertex_input v)
                {
                    vertex_output o = (vertex_output)0;
                    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                    float4 position =  mul(unity_MatrixMVP, v.vertex);
                    o.vertex = position;
                    o.uv = v.uv;
                    o.uv1 = v.uv1;
                    o.uv2 = v.uv2;
                    // o.ss_pos = ComputeScreenPos(position);

                    position.y = position.y * _ProjectionParams.x;
                    float3 tmp = position.xwy * float3(-0.5, 0.5, 0.5);
                    o.ss_pos.zw = position.zw;
                    o.ss_pos.xy = tmp.yy + tmp.xz;
                    o.color = v.color;
                    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
                    o.view = _WorldSpaceCameraPos - ws_pos.xyz;
                    return o;
                }

                


                float4 frag(vertex_output i) : SV_Target
                {
                    float4 SV_Target0;
                    float3 u_xlat0;
                    float3 u_xlat16_0;
                    float4 u_xlat1;
                    float3 u_xlat2;
                    bool u_xlatb2;
                    float3 u_xlat16_3;
                    float2 u_xlat4;
                    float3 u_xlat16_5;
                    float3 u_xlat16_6;
                    float u_xlat16_7;
                    float3 u_xlat16_8;
                    float u_xlat9;
                    int u_xlati11;
                    bool u_xlatb11;
                    float2 u_xlat16_16;
                    float2 u_xlat22;
                    float u_xlat16_24;
                    float u_xlat27;
                    float u_xlat16_30;
                    u_xlat0 = normalize(i.view);
                    u_xlat1.xy = i.ss_pos.xy / i.ss_pos.ww;
                    u_xlat2.xyz = normalize(i.normal);
                    u_xlat27 = dot(u_xlat2.xyz, u_xlat0.xyz);
                    u_xlat27 = (-u_xlat27) + 1.0;
                    u_xlat27 = max(u_xlat27, 9.99999975e-05);
                    u_xlat27 = pow(u_xlat27, _RimRange);
                    u_xlat1.z = (-u_xlat1.x) + 1.0;
                    u_xlat1 = _ReflectionColor.Sample(sampler_linear_repeat, u_xlat1.zy);
                    u_xlatb2 = 0.5<_ReflectReturn;
                    if(u_xlatb2){
                        u_xlatb2 = 0.5<_ReflectClip;
                        u_xlat16_3.x = u_xlat1.x + 0.99000001;
                        u_xlat16_3.x = floor(u_xlat16_3.x);
                        u_xlat16_3.x = max(u_xlat16_3.x, 0.0);
                        u_xlati11 = int(u_xlat16_3.x);
                        u_xlatb11 = u_xlati11==0;
                        u_xlatb2 = u_xlatb11 && u_xlatb2;
                        if(u_xlatb2){discard;}
                        u_xlat2.x = u_xlat27 * _ReflectAlpha;
                        u_xlat1.w = u_xlat1.w * u_xlat2.x;
                        SV_Target0 = u_xlat1;
                        return SV_Target0;
                    }
                    u_xlat2.xyz = u_xlat0.yyy * unity_WorldToObject[1].xyz;
                    u_xlat2.xyz = unity_WorldToObject[0].xyz * u_xlat0.xxx + u_xlat2.xyz;
                    u_xlat2.xyz = unity_WorldToObject[2].xyz * u_xlat0.zzz + u_xlat2.xyz;
                    u_xlat9 = dot(u_xlat2.xyz, u_xlat2.xyz);
                    u_xlat9 = rsqrt(u_xlat9);
                    u_xlat2.xyz = u_xlat9 * u_xlat2.xyz;
                    u_xlat4.xy = i.uv.xy * _BottomTex_ST.xy + _BottomTex_ST.zw;
                    u_xlat22.xy = _Time.yy * _MainSpeed.xy;
                    u_xlat4.xy = u_xlat22.xy * float2(float2(_BottomCorrection, _BottomCorrection)) + u_xlat4.xy;
                    u_xlat22.xy = i.uv.xy * _BaseTex_ST.xy + _BaseTex_ST.zw;
                    u_xlat16_5.xyz = _BaseTex.Sample(sampler_linear_repeat, u_xlat22.xy).xzw;
                    u_xlat0.xy = u_xlat0.xz * _ViewOffset + u_xlat4.xy;
                    u_xlat16_0.xyz = _BottomTex.Sample(sampler_linear_repeat, u_xlat0.xy).xyz;
                    u_xlat0.xyz = u_xlat16_5.yyy * u_xlat16_0.xyz;
                    u_xlat16_3.xyz = u_xlat0.xyz * _BottomColor.xyz;
                    u_xlat0.xyz = (-u_xlat0.xyz) * _BottomColor.xyz + _RimColor.xyz;
                    u_xlat0.xyz = u_xlat27 * u_xlat0.xyz + u_xlat16_3.xyz;
                    u_xlat4.xy = i.uv.xy + float2(-0.5, -0.5);
                    u_xlat27 = dot((-u_xlat4.xy), (-u_xlat4.xy));
                    u_xlat27 = sqrt(u_xlat27);
                    u_xlat27 = u_xlat27 * 2.0 + 0.300000012;
                    u_xlat27 = min(u_xlat27, 1.0);
                    u_xlat27 = (-u_xlat27) + 1.0;
                    u_xlat16_3.x = u_xlat27 * _ReflectWeight;
                    u_xlat16_3.xyz = u_xlat16_3.xxx * u_xlat1.xyz + u_xlat0.xyz;
                    u_xlat16_30 = u_xlat16_5.z * _BaseColor.w;
                    u_xlat16_3.xyz = u_xlat16_5.xxx * _BaseColor.xyz + u_xlat16_3.xyz;
                    SV_Target0.w = u_xlat16_30 * i.color.w;
                    u_xlat0.xy = i.uv.xy * _LightTex_ST.xy + _LightTex_ST.zw;
                    u_xlat16_0.x = _LightTex.Sample(sampler_linear_clamp, u_xlat0.xy).x;
                    u_xlat16_30 = pow(u_xlat16_0.x, _LightPower);
                    u_xlat16_30 = u_xlat16_30 * _LightColor.x;
                    u_xlat16_30 = u_xlat16_30 * _LightIntensity + _LightOffset;
                    u_xlat16_30 = clamp(u_xlat16_30, 0.0, 1.0);
                    u_xlat16_6.xyz = u_xlat2.yyy * float3(-0.0, -2.0, -0.0) + u_xlat2.xyz;
                    u_xlat16_7 = sin(_Angle);
                    u_xlat16_8.x = cos(_Angle);
                    u_xlat16_16.xy = u_xlat16_6.xz * u_xlat16_8.xx;
                    u_xlat16_8.x = u_xlat16_7 * u_xlat16_6.z + u_xlat16_16.x;
                    u_xlat16_24 = sin((-_Angle));
                    u_xlat16_8.z = u_xlat16_24 * u_xlat16_6.x + u_xlat16_16.y;
                    u_xlat16_8.y = u_xlat16_6.y;
                    u_xlat16_0.xyz = _AdditionalReflectionCube.SampleLevel(sampler_AdditionalReflectionCube, u_xlat16_8.xyz, _AdditionalReflectionCubeMip).xyz;
                    u_xlat16_6.xyz = u_xlat16_0.xyz * _AdditionalReflectionColor.xyz;
                    u_xlat16_3.xyz = lerp(u_xlat16_3, u_xlat16_3 * u_xlat16_6, _AddtionalReflactionAlpha);
                    SV_Target0.xyz = u_xlat16_30 * u_xlat16_3.xyz;
                    return SV_Target0;
                }   
            ENDHLSL

        }
    }

    CustomEditor "LWGUI.LWGUI" 
    FallBack "Diffuse"
}