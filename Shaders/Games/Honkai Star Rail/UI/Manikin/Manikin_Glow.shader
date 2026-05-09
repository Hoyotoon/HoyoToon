Shader "HoyoToon/Honkai Star Rail/UI/Manikin/Glow"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" { }
        _MainTexScale ("Texture Scale", Float) = 1
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionTex ("Emission Tex", 2D) = "black" { }
        _EmissionStrength ("Emission Strength", Range(0, 20)) = 0
        [Toggle(_USE_EMISSIONFLOW)] _UseEmissionFlow ("Use Emission Flow?", Float) = 0
        [Toggle(_EMISSIONFLOW_ADD)] _UseEmissionFlowAdd ("Emission Flow Add?", Float) = 0
        _EmissionFlowStrength ("EmissionFlow Strength", Range(0, 2)) = 1
        [Toggle(_EMISSIONFLOWINVERSE)] _EmissionFlowInverse ("EmissionFlow Inverse", Float) = 0
        _Progress ("Progress", Range(0, 1)) = 0
        _LerpColor ("LerpColor", Color) = (1,1,1,1)
        _LerpValue ("LerpValue", Range(0, 1)) = 0
        _ZOffsetX ("Offset", Float) = 0
        _ZOffsetY ("Offset", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Toggle] _FlipOnBackface ("Flip On Backface?", Float) = 0
        [Toggle(_LINEARTOGAMMA)] _LinearToGamma ("Linear To Gamma?", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWriteMode ("ZWriteMode", Float) = 1


    }
    SubShader
    {
        Name "CustomForward"
        Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry-40" "RenderType" = "Opaque" }
        Offset 20, 20
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        ENDHLSL

        Pass
        {
            Name "CustomForward" 
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry+30" "RenderType" = "Opaque" }
            ZWrite [_ZWriteMode]
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
                #include "Common/Declarations.hlsl"
                #pragma vertex vert
                #pragma fragment frag

                uniform 	float _MainTexScale;
                uniform 	float4 _MainColor;
                uniform 	float4 _MainTex_ST;
                uniform 	float _LinearToGamma;
                uniform 	float _FlipOnBackface;
                uniform 	float _EmissionStrength;
                uniform 	float4 _EmissionTex_ST;
                uniform 	float4 _EmissionColor;
                uniform 	float _UseEmissionFlow;
                uniform 	float _UseEmissionFlowAdd;
                uniform 	float _EmissionFlowStrength;
                uniform 	float _EmissionFlowInverse;
                uniform 	float _Progress;
                uniform 	float4 _LerpColor;
                uniform 	float _LerpValue; 

                TEXTURE2D(_MainTex);
                TEXTURE2D(_EmissionTex);
                SAMPLER(sampler_linear_repeat);
                SAMPLER(sampler_linear_clamp);
                


                struct vertex_input
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct vertex_output 
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };  

                vertex_output vert(vertex_input v)
                {
                    vertex_output o = (vertex_output)0;
                    float4 position =  mul(unity_MatrixMVP, v.vertex);
                    o.vertex = position;
                    o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                    return o;
                }

                


                float4 frag(vertex_output i,  bool vface : SV_IsFrontFace) : SV_Target
                {
                    float2 emissionUV = i.uv * _EmissionTex_ST.xy + _EmissionTex_ST.zw;
                    float4 emissionSample = _EmissionTex.Sample(sampler_linear_repeat, emissionUV);
                    float emissionAlpha = -emissionSample.w + _Progress + 1.0;
                    float3 emissionColor = emissionSample.xyz * _EmissionColor.xyz * _EmissionStrength;

                    float flowMask = (emissionAlpha > 1.0) ? (1.0 - _EmissionFlowInverse) : _EmissionFlowInverse;
                    float3 flowEmission = lerp(emissionColor, emissionColor * flowMask, _UseEmissionFlow);
                    flowEmission = lerp(flowEmission, flowEmission * _EmissionFlowStrength, _UseEmissionFlowAdd);

                    float2 mainUV = vface ? i.uv : float2(1.0 - i.uv.x, i.uv.y);
                    mainUV = lerp(i.uv, mainUV, _FlipOnBackface);

                    float4 mainSample = _MainTex.Sample(sampler_linear_clamp, mainUV);
                    float4 mainColor = mainSample * _MainColor;
                    mainColor.xyz *= _MainTexScale;

                    float3 gammaCorrection = mainColor.xyz * 0.305306017 + 0.682171106;
                    gammaCorrection = mainColor.xyz * gammaCorrection + 0.0125228781;
                    mainColor.xyz = lerp(mainColor.xyz, mainColor.xyz * gammaCorrection, _LinearToGamma);

                    float3 finalColor = flowEmission + mainColor.xyz;
                    float3 lerpDiff = _LerpColor.xyz - finalColor;
                    float lerpAlpha = _LerpColor.w - mainColor.w;
                    
                    float4 final;
                    final.xyz = finalColor + _LerpValue * lerpDiff;
                    final.w = mainColor.w + _LerpValue * lerpAlpha;
                    return final;
                }   
            ENDHLSL

        }
    }

    CustomEditor "LWGUI.LWGUI" 
    FallBack "Diffuse"
}