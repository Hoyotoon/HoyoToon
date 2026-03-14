Shader "HoyoToon/Honkai Star Rail/Character/EyeShadow"
{
    Properties
    {
        [MHYHelpBox(Info, Vertex Color B (outline width))] _EyeShadowColor ("Color", Color) = (1,1,1,1)
        [Toggle(_USINGDITHERALPAH)] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [HideInInspector] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [MHYHeaderBox(Dissolve)] _DissoveON ("Enable Dissolve", Float) = 0
        _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
        _DissolveMap ("Dissolve Map", 2D) = "white" { }
        _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
        _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
        _DissolveDistortionIntensity ("", Float) = 0.01
        _DissolveOutlineSize1 ("", Float) = 0.05
        _DissolveOutlineSize2 ("", Float) = 0
        _DissolveOutlineOffset ("", Float) = 0
        _DissolveOutlineColor1 ("", Color) = (1,1,1,1)
        _DissolveOutlineColor2 ("", Color) = (0,0,0,0)
        _DissoveDirecMask ("", Float) = 2
        _DissolveOutlineSmoothStep ("", Vector) = (0,0,0,0)
        _DissolveUV ("", Range(0, 1)) = 0
        _DissolveUVSpeed ("", Vector) = (0,0,0,0)
        _DissolveMask ("Dissolve Mask", 2D) = "white" { }
        _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
        _DissolveMaskUVSet ("", Range(0, 1)) = 0
        _DissolveMapAdd ("", Float) = 0
        _DissolvePosMaskRootOffset ("", Vector) = (0,0,0,0)
        _DissolvePosMaskPos ("", Vector) = (1,0,0,1)
        _DissolvePosMaskOn ("", Float) = 0
        _DissolvePosMaskFilpOn ("", Float) = 0
        _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0
        _DissolvePosMaskWorldON ("", Float) = 0
        _MainTex ("Albedo |RGB(base color) A (alpha)", 2D) = "white" { }
        _ShowPartID ("Show Part ID", Float) = 0
        _HideCharaParts ("Hide Chara Parts", Float) = 0
        _StencilRef ("Stencil Ref", Float) = 16
        _StencilComp ("Stencil Comp", Float) = 8
        _StencilEye ("Stencil Eye", Float) = 17
        _UVChannelFront ("UV Channel for Frontside", Float) = 0
        _UVChannelBack ("UV Channel for Backside", Float) = 1
    }
    SubShader
    {
        Tags { "QUEUE" = "Geometry+15" "RenderType" = "Opaque" }
        Blend 0 DstColor Zero, DstColor Zero
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
            Stencil
            {
                Ref [_StencilEye]
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
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER
            #pragma vertex vert_base
            #pragma fragment frag_eye

            ENDHLSL
        } 

        Pass 
        {
            Name "LightingGBuffer"
            Tags { "LightMode"="LightingGBuffer" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull [_CullMode]
            // Stencil
            // {
            //     Ref [_StencilRef]
            //     ReadMask 255
            //     WriteMask 255
            //     CompFront Always
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
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER
            #pragma vertex vert_base
            #pragma fragment frag_base

            ENDHLSL
        } 


        UsePass "Standard/ShadowCaster"
    }
     CustomEditor "LWGUI.LWGUI" 
     
    FallBack "Diffuse"
}