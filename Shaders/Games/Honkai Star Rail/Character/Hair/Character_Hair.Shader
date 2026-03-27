Shader "HoyoToon/Honkai Star Rail/Character/Hair"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Options)] _Options ("Options", Float) = 0
        [SubGroup(Options, Rendering)] _RenderMode ("Rendering", Float) = 0
        [SubEnum(Rendering, UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Sub(Rendering)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        [Sub(Rendering)] _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        [Sub(Rendering)] _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        [Sub(Rendering)] _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        [SubGroup(Rendering, StencilSettings)] _StencilSettings ("Stencil Settings", Float) = 0
        [Sub(StencilSettings)] _HairBlendWeight ("Hair Blend Weight", Range(0, 2)) = 1
        [Sub(StencilSettings)] _HairBlendOffset ("Hair Blend Offset", Range(-1, 0.1)) = 0
        [Sub(StencilSettings)]_StencilEye ("Stencil Eye", Float) = 17
        [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _HairStencilOp ("Hair Stencil Op", Float) = 2
        [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _HairStencilComp ("Hair Stencil Comp", Float) = 6
        [Sub(StencilSettings)]_HairStencilMask ("Hair Stencil Mask", Float) = 0
        [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _EyeMaskStencilOp ("Eye Mask Stencil Op", Float) = 2
        [Sub(StencilSettings)]_ForwardStencilRef ("Forward Stencil Ref", Range(0, 255)) = 1
        [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _ForwardStencilComp ("Forward Stencil Comp", Float) = 8
        [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _OutlineStencilComp ("Outline Stencil Comp", Float) = 8
        [SubToggle(Options)] _HideCharaParts ("Hide Chara Parts", Float) = 0
        [Sub(Options)] _ShowPartID ("Show Part ID", Float) = 0
        [Sub(Options)] _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        [SubGroup(MainGroup, MainTextures)] _Maps ("Main Textures", Float) = 0
            [Tex(MainTextures, small, collapsed)] _MainTex ("Diffuse", 2D) = "white" { }
            [Tex(MainTextures, small, collapsed)] _LightMap ("Light Map", 2D) = "grey" { }
            [SubGroup(MainGroup, MaterialLUT)] _MaterialLUTGroup ("Material LUT", Float) = 0
            [Sub(MaterialLUT)] _UseMaterialValuesLUT ("Use Mat Lut", Float) = 0
            [Tex(MaterialLUT)] _MaterialValuesPackLUT ("Mat Pack LUT", 2D) = "white" { }
        
        [SubGroup(MainGroup, MainColors)] _MainColors("Main Colors", Float) = 0
        [Sub(MainColors)] [HideInInspector] _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        [Sub(MainColors)] _Color ("Color", Color) = (1,1,1,1)
        [Sub(MainColors)] _BackColor ("BackColor", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _AddColor ("Add Color", Color) = (0,0,0,0)

        [Main(NormalMap, _, off, off)] _NormalGroup ("Normal Map", Float) = 0
        [SubKeyword(NormalMap, _USE_NORMAL_MAP)] _UseNormalMap ("UseNormalMap", Float) = 0
        [Tex(NormalMap, small, collapsed)] _NormalMap ("NormalMap", 2D) = "bump" { }
        [Sub(NormalMap)] _NormalScale ("NormalScale", Range(0, 4)) = 1
        
        [Main(LightingGroup, _, off, off)] _LightingGroup ("Lighting", Float) = 0
            [SubGroup(LightingGroup, ShadowsGroup)] _ShadowGroup ("Shadow", Float) = 0
                [Tex(ShadowsGroup, ramp)] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
                [Tex(ShadowsGroup, ramp)] _DiffuseRampMultiTex ("Shadow Multiple Ramp", 2D) = "white" { }
                [Sub(ShadowsGroup)] _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
                [SubToggle(ShadowsGroup)] _ShadowBoost ("Shadow Boost Enable", Float) = 0
                [Sub(ShadowsGroup)] _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
            [SubGroup(LightingGroup, SpecularGroup)] _SpecularGroup ("Specular Group", Float) = 0
        [Sub(SpecularGroup)] _SpecularColor0 ("Specular Color (ID = 0)", Color) = (1,1,1,1)
        [Sub(SpecularGroup)] _SpecularShininess0 ("Specular Shininess", Range(0.1, 500)) = 10
        [Sub(SpecularGroup)] _SpecularRoughness0 ("Specular Roughness", Range(0, 1)) = 0
        [Sub(SpecularGroup)] _SpecularIntensity0 ("Specular Intensity", Range(0, 50)) = 1
        [Sub(SpecularGroup)] _SpecularShadowOffset ("Specular Shadow Offset", Range(0, 1)) = 0.78
        [Sub(SpecularGroup)] _SpecularShadowIntensity ("Specular Shadow Intensity", Range(0, 1)) = 0
        [SubGroup(LightingGroup, RimLight)] _RimLightGroup ("Rim Light", Float) = 0
            [SubGroup(RimLight, RegularRim)] _RimLightGroupDummy("Screen Rim Light", Float) = 0
                [Sub(RegularRim)] _RimLightMode ("Use lightmap Red Channel for Mask", Range(0, 1)) = 1
                [Sub(RegularRim)] _Rimintensity ("Rim Intensity", Float) = 1
                [Sub(RegularRim)] _RimFeatherWidth ("Rim Feather Width", Float) = 0.01
                [Sub(RegularRim)] _RimWidth ("RimWidth", Float) = 1
                [Sub(RegularRim)] _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
                [Sub(RegularRim)] _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015 
                [SubGroup(RegularRim, RRParams)] _RimParams ("Rim Params", Float) = 0
                    [MaterialID(0)] [Sub(RRParams)] _RimColor0 ("Color 0", Color) = (1,1,1,1)
                    [MaterialID(0)] [Sub(RRParams)] _RimEdgeSoftness0 ("Softness 0", Range(0.01, 0.9)) = 0.1
                    [MaterialID(0)] [Sub(RRParams)] _RimType0 ("Blend Mode 0", Range(0, 1)) = 1
                    [MaterialID(0)] [Sub(RRParams)] _RimDark0 ("Darken 0", Range(0, 1)) = 0.5
            [SubGroup(RimLight, Fresnel)] _Fresnel("Fresnel", Float) = 0
                [Sub(Fresnel)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
                [Sub(Fresnel)] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
                [Sub(Fresnel)] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
            [SubKeyword(Fresnel, _RIM_BACKLIGHT_ON)] _EnableBackRimLight ("Enable Back Rim Light", Float) = 0
            [SubGroup(RimLight, RimShadow)] _RimShadowDummy ("Rim Shadow", Float) = 0
                [Sub(RimShadow)] _RimShadowCt ("Rim Shadow Ct", Float) = 1
                [Sub(RimShadow)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
                [Sub(RimShadow)] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
                [Sub(RimShadow)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
                [SubGroup(RimShadow, RimShadowParams)] _RimShadowParams ("Rim Shadow Params", Float) = 0
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowColor0 ("Color 0", Color) = (1,1,1,1)
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowWidth0 ("Width 0", Float) = 1
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowFeather0 ("Feather 0", Range(0.01, 0.99)) = 0.01

        [Main(Outline, _, off, off)] _Outline ("Outline", Range(0, 1)) = 0
        [Tex(Outline, small, collapsed)] _OutlineColorTex ("Outline Color", 2D) = "white" { }
            [Sub(Outline)] _OutlineColor0 ("Color 0", Color) = (0,0,0,1)
            [Sub(Outline)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
            [SubKeywordEnum(Outline, Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 1
            [Sub(Outline)] _OutlineColorIntensity ("Outline Color Intensity", Float) = 0
            [Sub(Outline)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
            [Sub(Outline)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
            [Sub(Outline)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
            [Sub(Outline)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
       
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, Emission)] _EmissionGroup ("Emission", Float) = 0
                [Sub(Emission)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
                [Sub(Emission)] _EmissionIntensity ("Emission Intensity", Float) = 0
        [SubGroup(Effects, Dissolve)] _DissolveEffect("Dissolve Effect", Float) = 0
            [SubKeyword(Dissolve, _DIRECTIONALDISSOLVE)] _DissoveON ("Enable Dissolve", Float) = 0
            [SubToggle(Dissolve)] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
            [Sub(Dissolve)] _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
            [Tex(Dissolve, small, collapsed)] _DissolveMap ("Dissolve Map", 2D) = "white" { }
            [Sub(Dissolve)] _DissolveST ("Dissolve Scale & Offset", Vector) = (1,1,0,0)
            [Sub(Dissolve)] _DistortionST ("Distortion Scale & Offset", Vector) = (1,1,0,0)
            [Sub(Dissolve)] _DissolveDistortionIntensity ("Dissolve Distortion Intensity", Float) = 0.01
            [Sub(Dissolve)] _DissolveOutlineSize1 ("Dissolve Outline Size 1", Float) = 0.05
            [Sub(Dissolve)] _DissolveOutlineSize2 ("Dissolve Outline Size 2", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineOffset ("Dissolve Outline Offset", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineColor1 ("Dissolve Outline Color 1", Color) = (1,1,1,1)
            [Sub(Dissolve)] _DissolveOutlineColor2 ("Dissolve Outline Color 2", Color) = (0,0,0,0)
            [Sub(Dissolve)] _DissoveDirecMask ("Dissolve Direction Mask", Float) = 2
            [Sub(Dissolve)] _DissolveMapAdd ("Dissolve Map Addition", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineSmoothStep ("Dissolve Outline Smoothstep", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolveUV ("Dissolve UV Mode", Range(0, 1)) = 0
            [Sub(Dissolve)] _DissolveUVSpeed ("Dissolve UV Speed", Vector) = (0,0,0,0)
            [Tex(Dissolve, small, collapsed)] _DissolveMask ("Dissolve Mask", 2D) = "white" { }
            [Sub(Dissolve)] _DissolveComponent ("Dissolve Mask Channel (RGBA=0/1/2/3)", Vector) = (1,0,0,0)
            [Sub(Dissolve)] _DissolvePosMaskPos ("Dissolve Position Mask Position", Vector) = (1,0,0,1)
            [SubToggle(Dissolve)] _DissolvePosMaskWorldON ("Dissolve Position Mask in World Space", Float) = 0
            [Sub(Dissolve)] _DissolvePosMaskRootOffset ("Dissolve Position Mask Root Offset", Vector) = (0,0,0,0)
            [SubToggle(Dissolve)] _DissolvePosMaskFilpOn ("Dissolve Position Mask Flip", Float) = 0
            [SubToggle(Dissolve)] _DissolvePosMaskOn ("Enable Dissolve Position Mask", Float) = 0 
            [Sub(Dissolve)] _DissolveMaskUVSet ("Dissolve Mask UV Set", Range(0, 1)) = 0
            [SubToggle(Dissolve)] _DissolveUseDirection ("Use Dissolve Direction", Float) = 0
            [Sub(Dissolve)] _DissolveCenter ("Dissolve Center Position", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolveDiretcionXYZ ("Dissolve Direction (XYZ)", Vector) = (0,0,0,0)
            [SubToggle(Dissolve)] _DissolvePosMaskGlobalOn ("Enable Global Dissolve Position Mask", Float) = 0
        [SubGroup(Effects, Bloom)] _BloomEffect("Bloom Effect", Float) = 0
            [SubGroup(Bloom, BloomParams)] _BloomParams ("Bloom Params", Float) = 0
            [Sub(BloomParams)]_BloomIntensity ("Bloom Intensity", Range(0, 1)) = 0
        [Sub(BloomParams)] _mBloomIntensity0 ("Bloom Intensity 0 (ID = 0)", Float) = 0
        [HideInInspector] _Opaqueness ("Opaqueness", Range(0, 1)) = 1
        [SubGroup(Effects, Dither)] _DitherEffect("Dither Effect", Float) = 0
        [SubKeyword(Dither, _USE_DITHER)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Sub(Dither)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [Sub(Dither)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [HideInInspector] _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        [HideInInspector] _XorShadowPartID ("Xor Shadow Parts", Float) = 0
        [HideInInspector] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [HideInInspector] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [HideInInspector] _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        [HideInInspector] _IsMonster ("Is Monster", Float) = 0
        [HideInInspector] _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 0
        [HideInInspector] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0}
    SubShader
    {
        Tags { "QUEUE" = "Geometry+20" "RenderType" = "Opaque" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"            
        ENDHLSL

        Pass 
        {
            Name "LightingGBuffer"
            Tags { "LightMode"="LightingGBuffer" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            // Cull Off
            Stencil
            {
                Ref [_StencilEye]
                ReadMask 255
                WriteMask 00
                CompFront NotEqual
                CompBack Always
                PassFront Replace
                PassBack Replace
                FailFront Keep
                FailBack Keep
                ZFailFront Keep
                ZFailBack Keep
            }
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _RIM_BACKLIGHT_ON
            #pragma shader_feature_local _USE_DITHER 
            #pragma shader_feature_local _USE_NORMAL_MAP 
            #pragma shader_feature_local _RECEIVE_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 

        Pass 
        {
            Name "LightingGBufferEyeHair"
            Tags { "LightMode"="LightingGBufferEyeHair" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Blend SrcAlpha OneMinusSrcAlpha
            // Cull [_CullMode]
            Stencil
            {
                Ref [_StencilEye]
                ReadMask 255
                WriteMask 254
                CompFront Equal
                CompBack Always
                PassFront Keep
                PassBack Keep
                FailFront Keep
                FailBack Keep
                ZFailFront Keep
                ZFailBack Keep
            }
            ColorMask RGB
            HLSLPROGRAM
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _RIM_BACKLIGHT_ON
            #pragma shader_feature_local _USE_DITHER
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma shader_feature_local _RECEIVE_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma vertex vert_base
            #pragma fragment frag_stencil

            ENDHLSL
        } 

        Pass 
        {
            Name "forward"
            Tags { "LightMode"="ForwardEmission" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull [_CullMode]
            ZWrite On
            // Stencil
            // {
            //     Ref [_ForwardStencilRef]
            //     ReadMask 255
            //     WriteMask 255
            //     CompFront Equal
            //     CompBack Always
            //     PassFront Keep
            //     PassBack Keep
            //     FailFront Keep
            //     FailBack Keep
            //     ZFailFront Keep
            //     ZFailBack Keep
            // }
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _RIM_BACKLIGHT_ON
            #pragma shader_feature_local _USE_DITHER
            #pragma shader_feature_local _RECEIVE_SHADOWS
            #pragma vertex vert_base
            #pragma fragment frab_forward

            ENDHLSL
        } 

        Pass 
        {
            Name "RPGOutline"
            Cull Front
            Offset [_OutlinePolygonOffsetFactor], [_OutlinePolygonOffsetUnits]
            Tags { "LightMode"="RPGOutline" }
            Stencil
            {
                Ref 17
                ReadMask 255
                WriteMask 254
                Comp Always
                Pass Keep
            }
            HLSLPROGRAM
            
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma multi_compile _OUTLINENORMALFROM_NORMAL _OUTLINENORMALFROM_TANGENT _OUTLINENORMALFROM_UV2
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER
            #pragma shader_feature_local _RAMP_OUTLINE
            #pragma vertex vert_edge
            #pragma fragment frag_edge

            ENDHLSL
        } 


        // Pass 
        // {
        //     Name "ShadowCaster"
        //     Tags { "LightMode"="SHADOWCASTER" }
            
        //     // Important for performance and depth precision
        //     ZWrite On 
        //     ZTest LEqual
        //     ColorMask 0
        //     Cull Off

        //     HLSLPROGRAM
        //     #include "Include/Struct.hlsl"
        //     #include "Include/Declarations.hlsl"
        //     #include "Include/Common.hlsl"
        //     #include "Include/Program.hlsl"
        //     // #pragma shader_feature_local _DIRECTIONALDISSOLVE
            
        //     #pragma vertex vert_shadow
        //     #pragma fragment frag_shadow
        //     ENDHLSL
        // }
        UsePass "Standard/ShadowCaster"
    }
     CustomEditor "LWGUI.LWGUI" 
     
    FallBack "Diffuse"
}