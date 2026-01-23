Shader "HoyoToon/Genshin Impact/Character/SkirkTrans"
{
    Properties
    {
        [Main(MainSettings, _, off, off)] _DummyMainSettings ("MainSettings", Float) = 0
        [Sub(MainSettings)] _TessMask ("Tessellation Mask", Range(0, 20)) = 0
        [Sub(MainSettings)] _TessValue ("Tessellation Density", Range(1, 32)) = 1
        [Sub(MainSettings)] _PhongWeight ("Phong Weight", Range(0, 3)) = 0
        [Header(MainTex)] _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Tex", 2D) = "white" { }
        _MainColorScaler ("MainColor Scaler", Range(0, 2)) = 1
        _MainAlpha ("Main Alpha", Range(0, 1)) = 1
        [Space(10)] [Header(Normal)] [Toggle] _UseNormal ("Use Normal?", Float) = 0
        _BumpMap ("Bump Map", 2D) = "gray" { }
        _BumpScale ("Bump Scale", Range(0, 2)) = 1
        [Space(10)] [Header(Specualr)] _SpecularColors ("Specular Colors", Color) = (1,1,1,1)
        _SpecularRanges ("Specular Ranges", Range(0, 1)) = 0.1
        _SpecularScales ("Specular Scales", Range(0, 5)) = 1
        _SpecularShadowScales ("SpecularShadow Scales", Range(0, 1)) = 0.5
        [Space(10)] [Header(Ibl)] [Toggle] _UseIblTex ("Use IBL?", Float) = 0
        _IblTex ("Ibl Tex", 2D) = "Black" { }
        _IblColors ("Specular Colors", Color) = (1,1,1,1)
        _IblScales ("Specular Scales", Range(0, 5)) = 0
        _IblShadowScales ("IblShadow Scales", Range(0, 1)) = 0.5
        [Space(10)] [Header(Shadow)] _LightsColor ("Light Color", Color) = (1,1,1,1)
        _ShadowsColor ("Shadow Color", Color) = (0.5,0.5,0.5,1)
        _ShadowArea ("Shadow Area", Range(-1, 1)) = 0.55
        [Space(10)] [Header(OutLine)] [HDR] _OutLinesColor ("OutLines Color", Color) = (1,1,1,0)
        _OutlineMaskTex ("Outline Mask R:TextureLine G:FresnelLineMask", 2D) = "black" { }
        [Toggle(ENABLE_TEXTURE_LINE_ON)] _DummyTextureLine ("Texture Line", Float) = 0
        _TextureLineThickness ("Texture Line Thickness", Range(0, 1)) = 0.6
        _TextureLineSmoothness ("Texture Line Smoothness", Range(0, 0.5)) = 0.1
        _TextureLineDistanceControl ("Thickness Inc/Max, smooth start Depth", Vector) = (0.1,0.6,1,1)
        [HDR] _TextureLineMultiplier ("Texture Line Multiplier", Color) = (1,1,1,1)
        [Toggle(ENABLE_FRESNEL_OUTLINE)] _UseFresnelOutline ("Fresnel Outline", Float) = 0
        _FresnelOutlineStep ("Fresnel Outline Step", Range(0, 1)) = 0.3
        [Header(Dithering)] [MHYToggle] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("DitherAlpha", Range(0, 1)) = 1
        [Header(UnderWaterFade)] _UnderWaterDistanceDense ("Distance Dense", Range(0, 50)) = 0.15
        [Space(10)] [Header(Depth Mode)] [Enum(Off, 0, On, 1)] _Zwrite ("ZWrite Mode", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _Ztest ("ZTest Mode", Float) = 4
        [Enum(Off, 0, On, 1)] _PreStencilZwrite ("PreStencil Zwrite", Float) = 0
        [Header(Cull Mode)] [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
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
			"Queue" = "Geometry" 
            "LightMode" = "ForwardBase"
		}
        HLSLINCLUDE 
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/GenshinImpact-skktrans_declaration.hlsl"
        #include "includes/GenshinImpact-skktrans_input.hlsl"
        #include "includes/GenshinImpact-skktrans_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"    
            Cull [_CullMode]
            Blend [_SrcBlendMode] [_DstBlendMode] 
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]
            ZWrite [_Zwrite]
            ZTest [_Ztest]

            HLSLPROGRAM
            #pragma shader_feature_local ENABLE_OUTLINE_ON
            #pragma shader_feature_local ENABLE_TEXTURE_LINE_ON
            #pragma shader_feature_local ENABLE_FRESNEL_OUTLINE

            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fwdbase
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment base_pixel


            ENDHLSL
        }

        UsePass "Hidden/HoyoToon/GenshinImpact/DepthCaster/DepthCaster"
    }
    CustomEditor "LWGUI.LWGUI" 
}
