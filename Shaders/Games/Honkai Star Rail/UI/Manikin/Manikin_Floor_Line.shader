Shader "HoyoToon/Honkai Star Rail/UI/Manikin/FloorLine"
{
    Properties
    {
    _MainColor ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" { }
    _MainTexScale ("Texture Scale", Float) = 1
    _MaskTex ("Mask Tex", 2D) = "white" { }
    _MaskStrength ("Mask Strength", Range(0, 2)) = 1
    _MaskSpeed ("MaskSpeed", Vector) = (0,0,0,0)
    _MaskScale ("MaskScale", Float) = 1
    _MaskOffset ("MaskOffset", Float) = 0
    _InflateScale ("InflateScale", Float) = 0
    _RimAlphaPow ("_RimAlphaPow", Range(0, 10)) = 1
    _ZOffsetX ("Offset", Float) = 0
    _ZOffsetY ("Offset", Float) = 0
    [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
    [Toggle] _FlipOnBackface ("Flip On Backface?", Float) = 0
    [Toggle(_LINEARTOGAMMA)] _LinearToGamma ("Linear To Gamma?", Float) = 0
    [Enum(Off, 0, On, 1)] _ZWriteMode ("ZWriteMode", Float) = 1
    _Width ("Width", Float) = 1

    }
    SubShader
    {
        Name "CustomForward"
        Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry" "RenderType" = "Opaque" }
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
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry-40" "RenderType" = "Opaque" }
            ZWrite [_ZWriteMode]
            HLSLPROGRAM
                #include "Common/Declarations.hlsl"
                #pragma vertex vert
                #pragma fragment frag

                uniform 	float _InflateScale;
                uniform 	float _Width;
                uniform 	float _MainTexScale;
                uniform 	float4 _MainColor;
                uniform 	float4 _MaskTex_ST;
                uniform 	float2 _MaskSpeed;
                uniform 	float _MaskScale;
                uniform 	float _MaskOffset;

                TEXTURE2D(_MainTex);
                TEXTURE2D(_MaskTex);
                SAMPLER(sampler_linear_repeat);
                


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
                    float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                    float3 viewDir = (-worldPos) + _WorldSpaceCameraPos.xyz;
                    float viewDistance = dot(viewDir, viewDir);
                    viewDistance = sqrt(viewDistance);
                    float inflatedHeight = v.color.x * _InflateScale;
                    float3 inflatedObjectPos = v.vertex.xyz;
                    inflatedObjectPos.y = inflatedHeight * viewDistance + v.vertex.y;
                    inflatedObjectPos.xz += v.normal.xz * _Width;
                    float3 inflatedPos = TransformObjectToWorld(inflatedObjectPos);
                    o.vertex = mul(UNITY_MATRIX_VP, float4(inflatedPos, 1.0));
                    o.uv = v.uv;
                    o.uv1 = v.uv1;
                    o.uv2 = v.uv2;
                    o.ss_pos = float4(inflatedPos, 1.0);
                    o.color = v.color;
                    o.normal = TransformObjectToWorldNormal(v.normal);
                    o.view = _WorldSpaceCameraPos - inflatedPos;
                    return o;
                }

                


                float4 frag(vertex_output i) : SV_Target
                {
                    // Sample mask texture with UV scrolling
                    float2 maskUV = i.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
                    maskUV += _Time.yy * _MaskSpeed.xy;

                    // Sample mask at two different time offsets
                    float2 maskUV1 = maskUV;
                    maskUV1.y += _Time.y * 0.02;
                    float mask1 = _MaskTex.Sample(sampler_linear_repeat, maskUV1).x;

                    float2 maskUV2 = maskUV;
                    maskUV2.y -= _Time.y * 0.02;
                    float mask2 = _MaskTex.Sample(sampler_linear_repeat, maskUV2).x;

                    // Combine masks and apply scale/offset
                    float combinedMask = saturate((mask1 + mask2) * _MaskScale + _MaskOffset);

                    // Sample and colorize main texture
                    float4 mainTexture = _MainTex.Sample(sampler_linear_repeat, i.uv.xy);
                    float4 coloredTex = mainTexture * _MainColor;

                    // Output final result
                    float4 result;
                    result.xyz = coloredTex.xyz * _MainTexScale;
                    result.w = combinedMask * coloredTex.w;
                    return result;
                }   
            ENDHLSL

        }
    }

    CustomEditor "LWGUI.LWGUI" 
    FallBack "Diffuse"
}