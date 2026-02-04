Shader "HoyoToon/Genshin Impact/Legacy/Character/Base"
{
    Properties
    {
        [HideInInspector] _Scale ("Scale Compared to _MAYA", Float) = 0.01
        [Header(Utility Display)] [Enum(None, 0, vertex.r, 1, vertex.g, 2, vertex.b, 3, vertex.a, 4, diffuse, 5)] _UtilityDisplay1 ("Utility Display 1", Float) = 0
        [Enum(None, 0, shadow strength, 1, shadow ramp uv, 2, normal, 3, point light, 4)] _UtilityDisplay2 ("Utility Display 2", Float) = 0
        [Header(Tessellation)] [Toggle(TESSELLATION_ON)] _TessellationOn ("Tessellation on", Float) = 0
    	 _TessMask ("Tessellation Mask", Range(0, 20)) = 0
		 _TessValue ("Tessellation Density", Range(1, 32)) = 1
		 _PhongWeight ("Phong Weight", Range(0, 3)) = 0
        [Header(Color)] _Color ("Tint Color", Color) = (1,1,1,1)
        [HideInInspector] _SingleColorOutputColor ("Single Color Output Color", Color) = (1,1,1,1)
        [Header(Element View)] _ElementViewEleID ("Element ID", Float) = 0
        [Header(Texture)] _MainTex ("Main Tex", 2D) = "white" { }
        [Enum(None, 0, AlphaTest, 1, Emission, 2, FaceBlush, 3)] _MainTexAlphaUse ("Main Tex Alpha Use", Float) = 0
        _MainTexAlphaCutoff ("Main Tex Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(MATERIAL_MASK)] _UseMaterialMasksTex ("Use Material Masks", Float) = 0
        _MaterialMasksTex ("Material Masks", 2D) = "black" { }
        _UseSaturation ("Use Saturation?", Range(0, 1)) = 0
        [Toggle(BUMP_TEXTURELINE_MAP)] _UseBumpMap ("Use Bump & Texture Line Map", Float) = 0
        [NoScaleOffset] _BumpMap ("Main Normal Map & SDF(B)", 2D) = "black" { }
        _BumpScale ("Main Normal Map scale", Range(0.0001, 1)) = 1
        _TextureLineThickness ("Texture Line Thickness", Range(0, 1)) = 0.6
        _TextureLineSmoothness ("Texture Line Smoothness", Range(0, 0.5)) = 0.1
        _TextureLineDistanceControl ("Thickness Inc/Max, smooth start Depth", Vector) = (0.1,0.6,1,1)
        [HDR] _TextureLineMultiplier ("Texture Line Multiplier", Color) = (0,0,0,1)
        [Header(Coloring (For compatibility))] [Toggle(MAIN_TEX_COLORING_ON)] _MainTexColoring ("Use Main Tex Coloring", Float) = 0
        _MainTexTintColor ("Main Tex Tint Color", Color) = (1,1,1,1)
        [Header(Shadow)] _ES_CharacterColorTone ("Shadow Tone Cool -> Warm", Range(0, 1)) = 1
    	[Toggle(TOON_LIGHTMAP_ON)] _UseToonLightMap ("Use Toon Light Map", Float) = 1
        [Toggle] _UseLightMapColorAO ("Use Light Map Color.g For AO", Float) = 1
        [Toggle] _UseVertexColorAO ("Use Vertex Color.r For AO", Float) = 1
        [Toggle] _UseCoolShadowColorOrTex ("Use Cool Shadow Color Or Tex", Float) = 0
        [NoScaleOffset] _LightMapTex ("Light Map Tex (RGB)", 2D) = "gray" { }
        _LightArea ("Light Area Threshold", Range(0, 1)) = 0.5
        _FirstShadowMultColor ("Warm Shadow Color", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor ("Cool Shadow Color", Color) = (0.9,0.7,0.75,1)
        [Header(Shadow Ramp)] [Toggle(SHADOW_RAMP_ON)] _UseShadowRamp ("Use Shadow Ramp", Float) = 0
        [MHYPackedGradient(_ShadowRampTex1 _ShadowRampTex2 _ShadowRampTex3 _ShadowRampTex4 _ShadowRampTex5 _CoolShadowRampTex1 _CoolShadowRampTex2 _CoolShadowRampTex3 _CoolShadowRampTex4 _CoolShadowRampTex5)] _PackedShadowRampTex ("Packed Shadow Ramp Tex", 2D) = "grey" { }
        _ShadowRampWidth ("Shadow Ramp Width", Range(0.01, 10)) = 1
        [Toggle] _UseVertexRampWidth ("Use Vertex Shadow Ramp Width", Float) = 0
        [Header(Shadow Transition)] [Toggle(SHADOW_TRANSITION_ON)] _UseShadowTransition ("Use Shadow Transition (only work when shadow ramp is off)", Float) = 0
        _ShadowTransitionRange ("Shadow Transition Range", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness ("Shadow Transition Softness", Range(0, 2)) = 0.5
        [Header(Specular)] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _Shininess ("Specular Shininess", Range(0.1, 100)) = 10
        _SpecMulti ("Specular Multiply Factor", Range(0, 1)) = 0.1
        [Header(Face Blush)] _FaceBlushStrength ("Face Blush Strength", Range(0, 1)) = 0
        _FaceBlushColor ("Face Blush Color", Color) = (1,0.8,0.7,1)
        [Header(Face Map New)] [Toggle(FACE_MAP_NEW_ON)] _UseFaceMapNew ("Use Face Map",  Float) = 0
        [NoScaleOffset] _FaceMapTex ("Face Map Tex (A Linear)", 2D) = "gray" { }
        _FaceMapRotateOffset ("Face Map Rotate Offset", Range(-1, 1)) = 0
        _FaceMapSoftness ("Face Map Softness", Range(0.000001, 1)) = 0.000001
        [Header(Emission(need use main tex alpha as mask))] _EmissionScaler ("Emission Scaler", Range(0, 100)) = 1
        _EmissionScaler1 ("Emission Scaler For Material 1", Range(0, 100)) = 1
        _EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
    	[Toggle(ENABLE_OUTLINE_ON)] _ToggleOutline("Toggle Outline", float) = 0
        [Header(Outline)] [Enum(None, 0, Normal, 1, Tangent, 2)] _OutlineType ("Outline Type", Float) = 2
        _OutlineWidth ("Outline Width", Range(0, 100)) = 0.04
        _OutlineCorrectionWidth ("Outline Width", Range(0, 100)) = 0.04
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _MaxOutlineZOffset ("Max Outline Z Offset", Range(0, 100)) = 1
        _OutlineWidthAdjustZs ("Outline Width Adjust Dist Start (near, middle, far)", Vector) = (0.01,2,6,0)
        _OutlineWidthAdjustScales ("Outline Width Adjust Scale (near, middle, far)", Vector) = (0.105,0.245,0.6,0)
        [Header(Material 2)] [Toggle] _UseMaterial2 ("Use Material 2", Float) = 0
        _Color2 ("Tint Color 2", Color) = (1,1,1,1)
        _EmissionScaler2 ("Emission Scaler 2", Range(0, 100)) = 1
        _FirstShadowMultColor2 ("Warm Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor2 ("Cool Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        _Shininess2 ("Specular Shininess 2", Range(0.1, 100)) = 10
        _SpecMulti2 ("Specular Multiply Factor 2", Range(0, 1)) = 0.1
        _OutlineColor2 ("Outline Color 2", Color) = (0,0,0,1)
        [Header(Shadow Transition 2)] _ShadowTransitionRange2 ("Shadow Transition Range 2", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness2 ("Shadow Transition Softness 2", Range(0, 2)) = 0.5
        [Header(Material 3)] [Toggle] _UseMaterial3 ("Use Material 3", Float) = 0
        _Color3 ("Tint Color 3", Color) = (1,1,1,1)
        _EmissionScaler3 ("Emission Scaler 3", Range(0, 100)) = 1
        _FirstShadowMultColor3 ("Warm Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor3 ("Cool Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        _Shininess3 ("Specular Shininess 3", Range(0.1, 100)) = 10
        _SpecMulti3 ("Specular Multiply Factor 3", Range(0, 1)) = 0.1
        _OutlineColor3 ("Outline Color 3", Color) = (0,0,0,1)
        [Header(Shadow Transition 3)] _ShadowTransitionRange3 ("Shadow Transition Range 3", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness3 ("Shadow Transition Softness 3", Range(0, 2)) = 0.5
        [Header(Material 4)] [Toggle] _UseMaterial4 ("Use Material 4", Float) = 0
        _Color4 ("Tint Color 4", Color) = (1,1,1,1)
        _EmissionScaler4 ("Emission Scaler 4", Range(0, 100)) = 1
        _FirstShadowMultColor4 ("Warm Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor4 ("Cool Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        _Shininess4 ("Specular Shininess 4", Range(0.1, 100)) = 10
        _SpecMulti4 ("Specular Multiply Factor 4", Range(0, 1)) = 0.1
        _OutlineColor4 ("Outline Color 4", Color) = (0,0,0,1)
        [Header(Shadow Transition 4)] _ShadowTransitionRange4 ("Shadow Transition Range 4", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness4 ("Shadow Transition Softness 4", Range(0, 2)) = 0.5
        [Header(Material 5)] [Toggle] _UseMaterial5 ("Use Material 5", Float) = 0
        _Color5 ("Tint Color 5", Color) = (1,1,1,1)
        _EmissionScaler5 ("Emission Scaler 5", Range(0, 100)) = 1
        _FirstShadowMultColor5 ("Warm Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor5 ("Cool Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        _Shininess5 ("Specular Shininess 5", Range(0.1, 100)) = 10
        _SpecMulti5 ("Specular Multiply Factor 5", Range(0, 1)) = 0.1
        _OutlineColor5 ("Outline Color 5", Color) = (0,0,0,1)
        [Header(Shadow Transition 5)] _ShadowTransitionRange5 ("Shadow Transition Range 5", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness5 ("Shadow Transition Softness 5", Range(0, 2)) = 0.5
        [Header(Back Face)] [Toggle(BACK_FACE_ON)] _DrawBackFace ("Draw Back Face", Float) = 0
        [Toggle] _UseBackFaceUV2 ("Use Back Face UV 2", Float) = 1
        [Header(Dithering)] [Toggle] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [HideInInspector] _TextureBiasWhenDithering ("Texture Bias When Dithering", Float) = -1
        [Header(Plane Clipping)] [Toggle] _UseClipPlane ("Use Clip Plane", Float) = 0
        [Toggle] _ClipPlaneWorld ("Clip Plane In World Space", Float) = 1
        _ClipPlane ("Clip Plane", Vector) = (0,0,0,0)
        [Header(Effect)] [Toggle] _FlareType ("Flare Type (When causing flare)", Float) = 0
        [Header(Metal)] [Toggle(METAL_MAT)] _MetalMaterial ("Metal Material", Float) = 0
        _MTMap ("Metal Map", 2D) = "white" { }
        _MTMapBrightness ("Metal Map Brightness", Float) = 1
        _MTMapTileScale ("Metal Map Tile Scale", Float) = 1
        [HDR] _MTMapLightColor ("Metal Map Light Color", Color) = (1,1,1,1)
        [HDR] _MTMapDarkColor ("Metal Map Dark Color", Color) = (0,0,0,0)
        _MTShadowMultiColor ("Metal Shadow Multiply Color", Color) = (0.8,0.8,0.8,0.8)
        _MTShininess ("Metal Shininess", Float) = 11
        _MTSpecularScale ("Metal Specular Scale", Float) = 60
        _MTSpecularAttenInShadow ("Metal Specular Attenuation in Shadow", Range(0, 1)) = 0.2
        [HDR] _MTSpecularColor ("Metal Specular Color", Color) = (1,1,1,1)
        [Toggle] _MTUseSpecularRamp ("Use Metal Specular Ramp", Float) = 0
        [MMDGradient] _MTSpecularRamp ("Specular Ramp", 2D) = "grey" { }
        _MTSharpLayerOffset ("Sharp Highlight Offset", Range(0.001, 1)) = 1
        [HDR] _MTSharpLayerColor ("Sharp Highlight Color", Color) = (1,1,1,1)
        [Header(Clipping)] [Toggle(CLIPPING_ON)] _UseClipping ("Enable Clipping", Float) = 0
        [Enum(WorldSpace, 0, UVDissolveTex, 1, AlphaTex(R), 2)] _ClipMethod ("Clip Method", Float) = 0
        _ClipBoxPositionOffset ("Clip Box Position Offset", Vector) = (0,0,0,0)
        _ClipBoxScale ("Clip Box (half)Scale", Vector) = (0.5,0.5,0.5,1)
        _ClipBoxHighLightScale ("Clip High Light Scale", Range(0, 5)) = 0.2
        [HDR] _ClipHighLightColor ("Clip High Light Color", Color) = (1,1,1,1)
        _ClipAlphaTex ("Alpha Clipping Texture", 2D) = "white" { }
        [Enum(UV1, 0, UV2, 1)] _ClipAlphaUVSet ("Clipping UV Set", Float) = 0
        _ClipAlphaThreshold ("Alpha Clipping Threshold", Range(0, 1)) = 0
        [Toggle] _ClipDissolveDirection ("Invert Dissolve Direction", Float) = 0
        _ClipDissolveValue ("Dissolve Value", Range(0, 1)) = 1
        _ClipDissolveHightlightScale ("Dissolve Highlight Power", Float) = 1
        _ClipAlphaHighLightScale ("Clip Alpha Tex High Light Scale", Range(0, 1)) = 0
        [Header(State)] [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _OutlineMotionVectorsCull ("Outline Motion Vector Cull Mode", Float) = 1
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnit ("Polygon Offset Unit", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnit ("Outline Polygon Offset Unit", Float) = 0
        [Header(ASE Properties)] _HitColor ("HitColor", Color) = (0,0,0,0)
        _ElementRimColor ("ElementRimColor", Color) = (0,0,0,0)
        _HitColorScaler ("HitColorScaler", Float) = 6
        _HitColorFresnelPower ("HitColorFresnelPower", Float) = 1.5
        _EmissionStrengthLerp ("EmissionStrengthLerp", Range(0, 1)) = 0
        [HideInInspector] _ASEHeader ("", Float) = 0
    	[Main(RimLight, _, off, off)] _dummyRimLight ("Character Rim Light", float) = 0
        [Sub(RimLight)] _ES_AvatarRimWidth ("Width", Float) = 1
        [Sub(RimLight)] _ES_AvatarRimWidthScale ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarFrontRimColor ("Front Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarFrontRimIntensity ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarBackRimColor ("Back Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarBackRimIntensity ("Back Intensity", Float) = 1

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
        #include "includes/GenshinImpact-header.hlsl"
        #include "includes/GenshinImpact-base_input.hlsl"

        #include "includes/GenshinImpact-common.hlsl"
        #include "includes/GenshinImpact-base_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"    
            Cull [_CullMode]
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]
            
            HLSLPROGRAM
            #pragma shader_feature_local MATERIAL_MASK
            #pragma shader_feature_local TOON_LIGHTMAP_ON 
            
            #pragma shader_feature_local BUMP_TEXTURELINE_MAP 
            #pragma shader_feature_local SHADOW_RAMP_ON
            #pragma shader_feature_local BACK_FACE_ON
            #pragma shader_feature_local ENABLE_OUTLINE_ON
            // // #pragma shader_feature_local ENABLE_TEXTURE_LINE_ON
            #pragma shader_feature_local FACE_MAP_NEW_ON 
            #pragma shader_feature_local MAIN_TEX_COLORING_ON
            #pragma shader_feature_local METAL_MAT
            #pragma shader_feature_local CLIPPING_ON
            #pragma shader_feature_local TESSELLATION_ON
            
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fwdbase
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain_base
            #pragma fragment base_pixel


            ENDHLSL
        }
        UsePass "Hidden/HoyoToon/GenshinImpact/DepthCaster/DepthCaster"
    }
    CustomEditor "LWGUI.LWGUI" 
}
