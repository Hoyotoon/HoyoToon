Shader "HoyoToon/Honkai Star Rail/Character/Stencil Clear"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [Sub(MainGroup)]_Color ("Color", Color) = (1,1,1,1)
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
            [SubGroup(Effects, Dither)] _DitherEffect("Dither Effect", Float) = 0
            [SubKeyword(Dither, _USINGDITHERALPAH)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
            [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
            [Sub(Dither)] _UsingDitherAlphaArt ("Dither Art Toggle", Float) = 0
            [Sub(Dither)] _DITHER_FADE_IN ("Enable Dither Fade In", Float) = 0
        [SubGroup(Effects, Dissolve)] _DissolveEffect("Dissolve Effect", Float) = 0
            [SubKeyword(Dissolve, _DIRECTIONALDISSOLVE)] _DissoveON ("Enable Dissolve", Float) = 0
            [SubToggle(Dissolve)] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
            [Sub(Dissolve)] _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
            [Tex(Dissolve, small, collapsed)] _DissolveMap ("Dissolve Map", 2D) = "white" { }
            [Sub(Dissolve)] _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
            [Sub(Dissolve)] _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
            [Sub(Dissolve)] _DissolveDistortionIntensity ("Dissolve Distortion Intensity", Float) = 0.01
            [Sub(Dissolve)] _DissolveOutlineSize1 ("Dissolve Outline Size 1", Float) = 0.05
            [Sub(Dissolve)] _DissolveOutlineSize2 ("Dissolve Outline Size 2", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineOffset ("Dissolve Outline Offset", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineColor1 ("Dissolve Outline Color 1", Color) = (1,1,1,1)
            [Sub(Dissolve)] _DissolveOutlineColor2 ("Dissolve Outline Color 2", Color) = (0,0,0,0)
            [Sub(Dissolve)] _DissoveDirecMask ("Dissolve Direction Mask", Float) = 2
            [Sub(Dissolve)] _DissolveMapAdd ("Dissolve Map Add", Float) = 0
            [Sub(Dissolve)] _DissolveOutlineSmoothStep ("Dissolve Outline Smooth Step", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolveUV ("Dissolve UV", Range(0, 1)) = 0
            [Sub(Dissolve)] _DissolveUVSpeed ("Dissolve UV Speed", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolveMask ("Dissolve Mask", 2D) = "white" { }
            [Sub(Dissolve)] _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
            [Sub(Dissolve)] _DissolvePosMaskPos ("Dissolve Pos Mask Pos", Vector) = (1,0,0,1)
            [SubToggle(Dissolve)] _DissolvePosMaskWorldON ("Dissolve Pos Mask World ON", Float) = 0
            [Sub(Dissolve)] _DissolvePosMaskRootOffset ("Dissolve Pos Mask Root Offset", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolvePosMaskFilpOn ("Dissolve Pos Mask Flip On", Float) = 0
            [SubToggle(Dissolve)] _DissolvePosMaskOn ("Dissolve Pos Mask On", Float) = 0
            [Sub(Dissolve)] _DissolveMaskUVSet ("Dissolve Mask UV Set", Range(0, 1)) = 0
            [SubToggle(Dissolve)] _DissolveUseDirection ("Dissolve Use Direction", Float) = 0
            [Sub(Dissolve)] _DissolveCenter ("Dissolve Center", Vector) = (0,0,0,0)
            [Sub(Dissolve)] _DissolveDiretcionXYZ ("Dissolve Direction XYZ", Vector) = (0,0,0,0)
            [SubToggle(Dissolve)] _DissolvePosMaskGlobalOn ("Dissolve Pos Mask Global On", Float) = 0
            
        [HideInInspector] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        [MHYHeaderBox(Stencil)] [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilOP ("Stencil Op", Float) = 2
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
        [HideInInspector] _CharaWorldSpaceOffset ("CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        [HideInInspector] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        [HideInInspector] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        [HideInInspector] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
    }
    SubShader
    {
        Tags { "QUEUE" = "Geometry+12" "RenderType" = "Opaque" }
        // Blend 0 DstColor Zero, DstColor Zero
        ColorMask RGB 0
        ColorMask 0 1
        ColorMask 0 2
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        ENDHLSL

        Pass 
        {
            Name "LightingGBuffer"
            Tags { "LightMode"="LightingGBufferEyeHair" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull [_CullMode]
            Blend One Zero, One OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRef]
                ReadMask 255
                WriteMask 255
                CompFront Equal
                CompBack Always
                PassFront Keep
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
            #pragma shader_feature_local _DISSOLVE
            #pragma shader_feature_local _USINGDITHERALPHA
            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 

        UsePass "Standard/ShadowCaster"
    }
     CustomEditor "LWGUI.LWGUI" 
     
    FallBack "Diffuse"
}