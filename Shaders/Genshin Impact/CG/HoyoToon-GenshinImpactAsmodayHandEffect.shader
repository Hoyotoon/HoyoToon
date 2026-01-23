Shader "HoyoToon/Genshin Impact/CG/Cg_Asmoday_HandEffect"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" { }
        [HDR] _LineColor ("LineColor", Color) = (1,1,1,0)
        _LightColor ("LightColor", Color) = (0.4117647,0.1665225,0.1665225,0)
        _ShadowColor ("ShadowColor", Color) = (0.2941176,0.1319204,0.1319204,0)
        _DownMaskRange ("DownMaskRange", Range(0, 1)) = 0.3058824
        _TopMaskRange ("TopMaskRange", Range(0, 1)) = 0.1147379
        _TopLineRange ("TopLineRange", Range(0, 1)) = 0.2101024
        [HDR] _FresnelColor ("FresnelColor", Color) = (1,0.7573529,0.7573529,0)
        _FresnelPower ("FresnelPower", Float) = 5
        _FresnelScale ("FresnelScale", Range(-1, 1)) = -0.4970588
        _ShadowWidth ("Shadow Width", Range(0, 1)) = 0.5764706
        _Tex01_UV ("Tex01_UV", Vector) = (1,1,0,0)
        _Tex01_Speed_U ("Tex01_Speed_U", Float) = 0.1
        _Tex01_Speed_V ("Tex01_Speed_V", Float) = 0
        _Tex02_UV ("Tex02_UV", Vector) = (1,1,0,0)
        _Tex02_Speed_U ("Tex02_Speed_U", Float) = -0.1
        _Tex02_Speed_V ("Tex02_Speed_V", Float) = 0
        _Tex03_UV ("Tex03_UV", Vector) = (1,1,0,0)
        _Tex03_Speed_U ("Tex03_Speed_U", Float) = 0
        _Tex03_Speed_V ("Tex03_Speed_V", Float) = -0.5
        _Tex04_UV ("Tex04_UV", Vector) = (1,1,0,-0.01)
        _Tex04_Speed_U ("Tex04_Speed_U", Float) = 0
        _Tex04_Speed_V ("Tex04_Speed_V", Float) = 0
        _Tex05_UV ("Tex05_UV", Vector) = (1,1,0,0)
        _Tex05_Speed_U ("Tex05_Speed_U", Float) = 0
        _Tex05_Speed_V ("Tex05_Speed_V", Float) = 0
        _Mask ("Mask", 2D) = "white" { }
        _Mask_Speed_U ("Mask_Speed_U", Float) = -0.1
        _GradientPower ("GradientPower", Float) = 1
        _GradientScale ("GradientScale", Float) = 1
        [Header(Cull Mode)] [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        _MHYZBias ("Z Bias", Float) = 0
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnit ("Polygon Offset Unit", Float) = 0
        [Header(Blend Mode)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Src Blend Mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Dst Blend Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOP ("BlendOp Mode", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "LightMode" = "ForwardBase"
        }
        
        HLSLINCLUDE
        
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "Include/GenshinImpact-hand_declaration.hlsl"
        #include "Include/GenshinImpact-hand_input.hlsl"
        #include "Include/GenshinImpact-hand_common.hlsl"
        #include "Include/GenshinImpact-hand_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Hand Effect"
            Cull [_Cull]
            Blend [_SrcBlendMode] [_DstBlendMode] 
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]
            
            HLSLPROGRAM
                #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
                #pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
                #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
                #pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
                #pragma skip_variants _SCREEN_SPACE_OCCLUSION
                
                #pragma multi_compile_fwdbase
                
                #pragma vertex base_vertex;
                #pragma fragment base_pixel;
            ENDHLSL
            
        }
    }
    CustomEditor "LWGUI.LWGUI" 
}