Shader "HoyoToon/Genshin Impact/Character/Glass"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Tex", 2D) = "white" { }
        _MainColorScaler ("Emission Scaler", Float) = 1
        _WeaponPatternTex ("WeaponPatternTex", 2D) = "white" { }
        [HDR] _WeaponPatternColor ("WeaponPatternColor", Color) = (1.682,1.568729,0.6554853,0)
        _Pattern_Speed ("Pattern_Speed", Float) = -0.033
        [Header(Dissolve Direction)] [Toggle] _DissolveDirection_Toggle ("DissolveDirection_Toggle", Float) = 0
        _WeaponDissolveTex ("WeaponDissolveTex", 2D) = "white" { }
        _WeaponDissolveValue ("WeaponDissolveValue", Range(0, 1)) = 0
        [Header(Glass Specular)] [Toggle(ENABLE_CHARACTER_GLASSSPECULAR_ON)] _UseCharacterGlassSpecular ("Use Character Glass Specular", Float) = 0
        _GlassSpecularColor ("GlassSpecularColor", Color) = (0,0,0,0)
        _GlassSpecularTex ("GlsaaSpecularTex", 2D) = "black" { }
        _GlassTiling ("GlassTiling", Float) = 40
        _GlassSpecularOffset ("GlassSpecularOffset", Range(-5, 5)) = 3
        _GlasspecularLength ("GlasspecularLength", Range(0, 1)) = 0.2
        _GlasspecularLengthRange ("GlasspecularLengthRange", Range(0, 1)) = 0.1
        _GlassThicknessColor ("GlassThicknessColor", Color) = (0,0,0,0)
        _GlassThickness ("GlassThickness", Range(1, 10)) = 4
        _GlassThicknessScale ("GlassThicknessScale", Range(0, 5)) = 1.5
        _GlassSpecularDetailColor ("GlassSpecularDetailColor", Color) = (0,0,0,0)
        _GlassSpecularDetailOffset ("GlassSpecularDetailOffset", Range(-1, 1)) = 0
        _GlassSpecularDetailLength ("GlassSpecularDetailLength", Range(0, 1)) = 0.2
        _GlassSpecularDetailLengthRange ("GlassSpecularDetailLengthRange", Range(0, 1)) = 0.1
        [Header(OtherSettings)] _ElementViewEleID ("Element ID", Float) = 0
        [Header(Dithering)] [Toggle] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("DitherAlpha", Range(0, 1)) = 1
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
		    "RenderType"="NonHair" 
			"Queue" = "Geometry+1" 
            "LightMode" = "ForwardBase"
		}

        HLSLINCLUDE 
        #define is_weapon_or_glass
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/GenshinImpact-header.hlsl"
        #include "includes/GenshinImpact-glass_input.hlsl"
        #include "includes/GenshinImpact-glass_declaration.hlsl"
        #include "includes/GenshinImpact-common.hlsl"
        #include "includes/GenshinImpact-glass_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Glass"
            Blend [_SrcBlendMode] [_DstBlendMode]
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]

            HLSLPROGRAM
            #pragma shader_feature_local ENABLE_CHARACTER_GLASSSPECULAR_ON
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION

            #pragma vertex base_vertex
            #pragma fragment base_pixel

            ENDHLSL

        }


    }
    CustomEditor "LWGUI.LWGUI" 
}