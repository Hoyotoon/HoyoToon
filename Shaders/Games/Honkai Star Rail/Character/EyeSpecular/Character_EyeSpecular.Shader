Shader "HoyoToon/Honkai Star Rail/Character/EyeSpecular"
{
    Properties
    {
        [MaterialIDCount(8)] _MateirialIDCount ("Material ID Count", Float) = 8
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Options)] _Options ("Options", Float) = 0
        [SubGroup(Options, Rendering)] _RenderMode ("Rendering", Float) = 0
            [MaterialID(0)] [SubKeyword(Rendering, _IS_DUALFACE)] _EnableDualFace ("Enable Dual Face Rendering", Float) = 0
                [SubEnum(Rendering, UV1, 0, UV2, 1)] _UVChannelFront ("UV Channel for Front", Float) = 0
                [SubEnum(Rendering, UV1, 0, UV2, 1)] _UVChannelBack ("UV Channel for Back", Float) = 1
                [Sub(Rendering)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
                [Sub(Rendering)] _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
                [Sub(Rendering)] _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
                [Sub(Rendering)] _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
                [Sub(Rendering)] _StencilEye ("Stencil Ref", Float) = 16
                [Sub(Rendering)] _StencilFace ("Stencil Ref", Float) = 16
                [Sub(Rendering)] _StencilOP ("Stencil Op", Float) = 2
                [Sub(Rendering)] _StencilComp ("Stencil Comp", Float) = 8
                [Sub(Rendering)] _StencilMask ("Stencil Read Mask", Float) = 255
                [Sub(Rendering)] _RenderingMode ("Rendering Mode", Float) = 0
                [Sub(Rendering)] _ZWrite ("ZWrite", Float) = 1
                [Sub(Rendering)] _SrcBlend ("Src Blend", Float) = 1
                [Sub(Rendering)] _DstBlend ("Dst Blend", Float) = 0
                [Sub(Rendering)] _CullMode ("Cull Mode", Float) = 2
                [Sub(Rendering)] _ZClip ("ZClip", Float) = 0
        [SubGroup(MainGroup, MainTextures)] _Maps ("Main Textures", Float) = 0
            [Tex(MainTextures, small, collapsed)]  _MainTex ("Albedo |RGB(base color) A (alpha)", 2D) = "white" { }
            [Sub(MainTextures)] _Color ("Color", Color) = (1,1,1,1)
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, Matcap)] _MatCapEffect("Matcap",Float) = 0
            [SubToggle(Matcap)] _UseMatcap ("use matcap", Float) = 0
            [Tex(Matcap, small, collapsed)] _MatCapTex ("MatCap", 2D) = "black" { }
            [Sub(Matcap)] _MatCapStrength ("_MatCapStrength", Range(0, 5)) = 1
        [SubGroup(Effects, RimLight)] _RimLightEffect("Rim Light Effect", Float) = 0
            [Sub(RimLight)][HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
            [Sub(RimLight)]_FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
            [Sub(RimLight)]_FresnelColorStrength ("FresnelColorStrength", Float) = 1
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

        [SubGroup(Effects, Dither)] _DitherEffect("Dither Effect", Float) = 0
        [SubKeyword(Dither, _USE_DITHER)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Sub(Dither)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [Sub(Dither)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0

}
    SubShader
    {
        Tags { "QUEUE" = "Geometry+490" "RenderType" = "Transparent"}
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
            Cull [_CullMode]
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            Stencil
            {
                Ref [_StencilFace]
                ReadMask 255
                WriteMask 254
                CompFront Always
                CompBack Always
                PassFront Replace
                PassBack Keep
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
            #pragma shader_feature_local _USE_DITHER
            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 

        Pass 
        {
            Name "LightingGBuffer"
            Tags { "LightMode"="LightingGBufferEyeHair" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull [_CullMode]
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            Stencil
            {
                Ref [_StencilEye]
                ReadMask 255
                WriteMask 255
                CompFront Always
                CompBack Always
                PassFront Replace
                PassBack Keep
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
            #pragma shader_feature_local _USE_DITHER
            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 

    }
     CustomEditor "LWGUI.LWGUI" 
     
    FallBack "Diffuse"
}
