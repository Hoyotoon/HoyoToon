Shader "HoyoToon/Genshin Impact/Character/WeaponDissolve"
{
    Properties
    {
        [Header(Color)] _Color ("Tint Color", Color) = (1,1,1,1)
        [HideInInspector] _SingleColorOutputColor ("Single Color Output Color", Color) = (1,1,1,1)
        [Header(Element View)] _ElementViewEleID ("Element ID", Float) = 0
        [Header(Texture)] _MainTex ("Main Tex", 2D) = "white" { }
        [Enum(None, 0, AlphaTest, 1, Emission, 2, FaceBlush, 3)] _MainTexAlphaUse ("Main Tex Alpha Use", Float) = 0
        _MainTexAlphaCutoff ("Main Tex Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle(MATERIAL_MASK)] _UseMaterialMasksTex ("Use Material Masks", Float) = 0
        _MaterialMasksTex ("Material Masks", 2D) = "black" { }
        [Toggle(BUMP_TEXTURELINE_MAP)] _UseBumpMap ("Use Bump & Texture Line Map", Float) = 0
        [NoScaleOffset] _BumpMap ("Main Normal Map & SDF(B)", 2D) = "black" { }
        _BumpScale ("Main Normal Map scale", Range(0.0001, 1)) = 1
        _TextureLineThickness ("Texture Line Thickness", Range(0, 1)) = 0.6
        _TextureLineSmoothness ("Texture Line Smoothness", Range(0, 0.5)) = 0.1
        _TextureLineDistanceControl ("Thickness Inc/Max, smooth start Depth", Vector) = (0.1,0.6,1,1)
         _TextureLineMultiplier ("Texture Line Multiplier", Color) = (0,0,0,1)
        [Header(Coloring (For compatibility))] [Toggle(MAIN_TEX_COLORING_ON)] _MainTexColoring ("Use Main Tex Coloring", Float) = 0
        _MainTexTintColor ("Main Tex Tint Color", Color) = (1,1,1,1)
        [Header(Shadow)] [Toggle(TOON_LIGHTMAP_ON)] _UseToonLightMap ("Use Toon Light Map", Float) = 1
        [Toggle] _UseLightMapColorAO ("Use Light Map Color.g For AO", Float) = 1
        [Toggle] _UseVertexColorAO ("Use Vertex Color.r For AO", Float) = 1
        [Toggle] _UseCoolShadowColorOrTex ("Use Cool Shadow Color Or Tex", Float) = 0
        [NoScaleOffset] _LightMapTex ("Light Map Tex (RGB)", 2D) = "gray" { }
        _LightArea ("Light Area Threshold", Range(0, 1)) = 0.5
        _FirstShadowMultColor ("Warm Shadow Color", Color) = (0.9,0.7,0.75,1)
        _CoolShadowMultColor ("Cool Shadow Color", Color) = (0.9,0.7,0.75,1)
        [Header(Shadow Ramp)] [Toggle(SHADOW_RAMP_ON)] _UseShadowRamp ("Use Shadow Ramp", Float) = 0
        _PackedShadowRampTex ("Packed Shadow Ramp Tex", 2D) = "grey" { }
        // [MHYPackedGradient(_ShadowRampTex1 _ShadowRampTex2 _ShadowRampTex3 _ShadowRampTex4 _ShadowRampTex5 _CoolShadowRampTex1 _CoolShadowRampTex2 _CoolShadowRampTex3 _CoolShadowRampTex4 _CoolShadowRampTex5)] _PackedShadowRampTex ("Packed Shadow Ramp Tex", 2D) = "grey" { }
        _ShadowRampWidth ("Shadow Ramp Width", Range(0.01, 10)) = 1
        [Toggle] _UseVertexRampWidth ("Use Vertex Shadow Ramp Width", Float) = 0
        [Header(Shadow Transition)] [Toggle(SHADOW_TRANSITION_ON)] _UseShadowTransition ("Use Shadow Transition (only work when shadow ramp is off)", Float) = 0
        _ShadowTransitionRange ("Shadow Transition Range", Range(0.001, 0.2)) = 0.01
        _ShadowTransitionSoftness ("Shadow Transition Softness", Range(0, 2)) = 0.5
        [Header(Specular)] [Toggle(TOON_SPECULAR_ON)] _UseToonSpecular ("Use Toon Specular (unused, just for compatibility)", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _Shininess ("Specular Shininess", Range(0.1, 100)) = 10
        _SpecMulti ("Specular Multiply Factor", Range(0, 1)) = 0.1
        [Header(Face Blush)] _FaceBlushStrength ("Face Blush Strength", Range(0, 1)) = 0
        _FaceBlushColor ("Face Blush Color", Color) = (1,0.8,0.7,1)
        [Header(Face Map New)] [Toggle(FACE_MAP_NEW_ON)] _UseFaceMapNew ("Use Face Map", Float) = 0
        [NoScaleOffset] _FaceMapTex ("Face Map Tex (A Linear)", 2D) = "gray" { }
        _FaceMapRotateOffset ("Face Map Rotate Offset", Range(-1, 1)) = 0
        _FaceMapSoftness ("Face Map Softness", Range(0.0000001, 1)) = 0.0000001
        [Header(Emission(need use main tex alpha as mask))] _EmissionScaler ("Emission Scaler", Range(0, 100)) = 1
        _EmissionScaler1 ("Emission Scaler For Material 1", Range(0, 100)) = 1
        _EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
        [Header(Outline)] [Enum(None, 0, Normal, 1, Tangent, 2)] _OutlineType ("Outline Type", Float) = 2
        _Scale ("Outline Scale", Float) = 1
        _OutlineWidth ("Outline Width", Range(0, 100)) = 0.04
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
        [Header(Metal)] [Toggle(METAL_MAT)] _MetalMaterial ("Metal Material", Float) = 0
        _MTMap ("Metal Map", 2D) = "white" { }
        _MTMapBrightness ("Metal Map Brightness", Float) = 1
        _MTMapTileScale ("Metal Map Tile Scale", Float) = 1
        _MTMapLightColor ("Metal Map Light Color", Color) = (1,1,1,1)
        _MTMapDarkColor ("Metal Map Dark Color", Color) = (0,0,0,0)
        _MTShadowMultiColor ("Metal Shadow Multiply Color", Color) = (0.8,0.8,0.8,0.8)
        _MTShininess ("Metal Shininess", Float) = 11
        _MTSpecularScale ("Metal Specular Scale", Float) = 60
        _MTSpecularAttenInShadow ("Metal Specular Attenuation in Shadow", Range(0, 1)) = 0.2
        _MTSpecularColor ("Metal Specular Color", Color) = (1,1,1,1)
        [Toggle] _MTUseSpecularRamp ("Use Metal Specular Ramp", Float) = 0
        [MMDGradient] _MTSpecularRamp ("Specular Ramp", 2D) = "grey" { }
        _MTSharpLayerOffset ("Sharp Highlight Offset", Range(0.001, 1)) = 1
        _MTSharpLayerColor ("Sharp Highlight Color", Color) = (1,1,1,1)
        [Main(RimLight, _, off, off)] _dummyRimLight ("Character Rim Light", float) = 0
        [Sub(RimLight)] _ES_AvatarRimWidth ("Width", Float) = 1
        [Sub(RimLight)] _ES_AvatarRimWidthScale ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarFrontRimColor ("Front Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarFrontRimIntensity ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarBackRimColor ("Back Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarBackRimIntensity ("Back Intensity", Float) = 1
        
        [Header(State)] [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _OutlineMotionVectorsCull ("Outline Motion Vector Cull Mode", Float) = 1
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnit ("Polygon Offset Unit", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnit ("Outline Polygon Offset Unit", Float) = 0
        [Header(Blend Mode)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Src Blend Mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Dst Blend Mode", Float) = 0
        [Header(ASE Properties)] _WeaponDissolveTex ("WeaponDissolveTex", 2D) = "white" { }
        _WeaponDissolveValue ("WeaponDissolveValue", Range(0, 1)) = 0
        [Toggle] _DissolveDirection_Toggle ("DissolveDirection_Toggle", Float) = 0
        _WeaponPatternTex ("WeaponPatternTex", 2D) = "white" { }
        _WeaponPatternColor ("WeaponPatternColor", Color) = (1.682,1.568729,0.6554853,0)
        _Pattern_Speed ("Pattern_Speed", Float) = -0.033
        _SkillEmisssionPower ("SkillEmisssionPower", Float) = 0.6
        _SkillEmisssionColor ("SkillEmisssionColor", Color) = (0,0,0,0)
        _SkillEmissionScaler ("SkillEmissionScaler", Float) = 3.2
        _ScanPatternTex ("ScanPatternTex", 2D) = "black" { }
        _ScanColorScaler ("ScanColorScaler", Float) = 1
        _ScanColor ("ScanColor", Color) = (0.8970588,0.8970588,0.8970588,1)
        [Toggle] _ScanDirection_Switch ("ScanDirection_Switch", Float) = 0
        _ScanSpeed ("ScanSpeed", Float) = 0.8
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
        #define is_weapon_or_glass
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/GenshinImpact-header.hlsl"
        #include "includes/GenshinImpact-weapon_input.hlsl"
        #include "includes/GenshinImpact-common.hlsl"
        #include "includes/GenshinImpact-weapon_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Weapon"
            Blend [_SrcBlendMode] [_DstBlendMode]
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]

            HLSLPROGRAM
            #pragma shader_feature_local FACE_MAP_NEW_ON
            #pragma shader_feature_local METAL_MAT
            #pragma shader_feature_local MATERIAL_MASK
            #pragma shader_feature_local TOON_LIGHTMAP_ON
            #pragma shader_feature_local SHADOW_TRANSITION_ON
            #pragma shader_feature_local BACK_FACE_ON
            #pragma shader_feature_local BUMP_TEXTURELINE_MAP
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION

            #pragma vertex base_vertex
            #pragma fragment base_pixel

            ENDHLSL

        }


        UsePass "Hidden/HoyoToon/GenshinImpact/DepthCaster/DepthCaster"
    }
    CustomEditor "LWGUI.LWGUI" 
}