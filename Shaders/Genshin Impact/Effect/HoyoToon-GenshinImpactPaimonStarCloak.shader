Shader "HoyoToon/Genshin Impact/Effect/Paimon_StarCloak"
{
    Properties
    {
        [HideInInspector] _Scale ("Scale Compared to Maya", Float) = 0.01
        [Toggle] _EnableAlphaTest ("Enable Alpha Test", Float) = 0
        _CutOff ("Mask Clip Value", Range(0, 1)) = 0
        [Enum(OFF, 0, ON, 1)] _OutlineOn ("Outline Type", Float) = 0
        _OutlineWidth ("Outline Width", Range(0, 100)) = 0.05
        _OutlineCorrectionWidth ("Outline Correction Width", Range(0, 100)) = 0.05
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _MaxOutlineZOffset ("Max Outline Z Offset", Range(0, 100)) = 1
        [Header(Dithering)] [Toggle(USINGDITHERALPAH)] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        _MainTex ("MainTex", 2D) = "white" { }
        [Header(Star Tex)] _StarTex ("Star Tex 01", 2D) = "black" { }
        _StarHeight ("Height 01", Float) = 14.89
        _Star02Tex ("Star Tex 02", 2D) = "black" { }
        _Star02Height ("Height 02", Float) = 0
        _StarBrightness ("Brightness", Float) = 60
        _Star01Speed ("Speed", Float) = 0
        [Header(Noise)] _NoiseTex01 ("Noise Tex 01", 2D) = "white" { }
        _Noise01Speed ("Speed 01", Float) = 0.1
        _NoiseTex02 ("Noise Tex 02", 2D) = "white" { }
        _Noise02Speed ("Speed 02", Float) = -0.1
        [Header(Color Palette)] _ColorPaletteTex ("Color Palette Tex", 2D) = "white" { }
        _ColorPalletteSpeed ("Speed", Float) = -0.1
        [Header(Constellation)] _ConstellationTex ("Constellation Tex", 2D) = "white" { }
        _ConstellationHeight ("Height", Float) = 1.2
        _ConstellationBrightness ("Brightness", Float) = 5
        [Header(Cloud)] _CloudTex ("Cloud Tex", 2D) = "white" { }
        _CloudBrightness ("Brightness", Float) = 1
        _CloudHeight ("Height", Float) = 1
        _Noise03Brightness ("Noise Strengh", Float) = 0.2
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
            #include "includes/GenshinImpact-paimon_declaration.hlsl"
            #include "includes/GenshinImpact-paimon_input.hlsl"
            #include "includes/GenshinImpact-paimon_common.hlsl"
            #include "includes/GenshinImpact-paimon_program.hlsl"
        ENDHLSL
        

        Pass
        {
            Name "Base Pass"    
            
            HLSLPROGRAM
           

            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fwdbase
            #pragma vertex base_vertex
            #pragma fragment base_pixel


            ENDHLSL
        }

        Pass
        {
            Name "Outline Pass"    
            Cull Front
            
            HLSLPROGRAM
           

            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fwdbase
            #pragma vertex edge_vertex
            #pragma fragment edge_pixel


            ENDHLSL
        }
    }
}