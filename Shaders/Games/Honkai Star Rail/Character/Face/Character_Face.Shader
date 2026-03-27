Shader "HoyoToon/Honkai Star Rail/Character/Face"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Options)] _Options ("Options", Float) = 0
        [SubGroup(Options, Rendering)] _RenderMode ("Rendering", Float) = 0
        [Sub(Rendering)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        [Sub(Rendering)] _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        [Sub(Rendering)] _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        [Sub(Rendering)] _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        [Sub(Rendering)] _UseUVChannel2 ("Use UV Channel2 For Facemap", Float) = 1
        [Sub(Rendering)] _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        [SubGroup(Rendering, StencilSettings)] _StencilSettings ("Stencil Settings", Float) = 0
            [Sub(StencilSettings)] _HairBlendSilhouette ("Hair Blend Silhouette", Range(0, 1)) = 0.5
            [Sub(StencilSettings)] _StencilEye ("Stencil Eye", Float) = 17
            [Sub(StencilSettings)] _StencilFace ("Stencil Face", Float) = 31
            [Sub(StencilSettings)] _StencilMask ("Stencil Write Mask", Float) = 254
            [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
            [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _StencilOP ("Stencil Op", Float) = 2
            [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _EyeMaskStencilComp ("Eye Mask Stencil Comparison", Float) = 8
            [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _EyeMaskStencilOp ("Eye Mask Stencil Op", Float) = 2
            [Sub(StencilSettings)] _ForwardStencilRef ("Forward Stencil Ref", Float) = 1
            [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _ForwardStencilComp ("Forward Stencil Comp", Float) = 8
            [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _ForwardStencilOp ("Forward Stencil Op", Float) = 0
            [SubEnum(StencilSettings, UnityEngine.Rendering.CompareFunction)] _OutlineStencilComp ("Outline Stencil Comp", Float) = 8
            [SubEnum(StencilSettings, UnityEngine.Rendering.StencilOp)] _OutlineStencilOp ("Outline Stencil Op", Float) = 2
        [SubGroup(Options, SelfShadow)] _SelfShadow ("Self Shadow", Float) = 0
            [Sub(SelfShadow)] _ReceiveShadows ("Receive Shadows", Float) = 1
            [Sub(SelfShadow)] _SelfShadowDepthOffset ("_SelfShadowDepthOffset", Float) = 0
            [Sub(SelfShadow)] _SelfShadowSampleOffset ("_SelfShadowSampleOffset", Float) = 0.001
            [SubToggle(SelfShadow)] _UseSelfShadow ("Use Self Shadow", Float) = 1
        [SubGroup(MainGroup, MainTextures)] _Maps ("Main Textures", Float) = 0
            [Tex(MainTextures, small, collapsed)] _MainTex ("Diffuse", 2D) = "white" { }
            [Tex(MainTextures, small, collapsed)] _FaceMap ("Face Map", 2D) = "white" { }
            [Tex(MainTextures, small, collapsed)] _FaceExpression ("Face Expression Map |R (Cheek) G (Shy) B (Shadow)", 2D) = "black" { }
        [HideInInspector] _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [HideInInspector] _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        [Main(FaceDetails, _, off, off)] _FaceDetails ("Face Details", Float) = 0
        [SubGroup(FaceDetails, FixLipLine)] _FixLipLine ("Fix Lip Line", Float) = 0
        [Sub(FixLipLine)] _LipLineFixScale ("Lip Line Fix Threshold", Range(0.01, 4)) = 1
        [Sub(FixLipLine)] _LipLinefixColor ("Lip Line Fix Color", Color) = (0,0,0,0)
        [Sub(FixLipLine)] _LipLineFixThrd ("Lip Line Fix Threshold", Range(0, 0.95)) = 0.3
        [Sub(FixLipLine)] _LipLineFixStart ("Lip Line Fix Start Distance", Range(0.04, 0.95)) = 0.25
        [Sub(FixLipLine)] _LipLineFixMax ("Lip Line Fix Maximum Distance", Range(1, 2)) = 1.5
        [Sub(FixLipLine)] _LipLineFixSC ("_LipLineFixSC", Float) = 0
        [SubGroup(FaceDetails, NoseLine)] _NoseLine ("Nose Line", Float) = 0
        [Sub(NoseLine)] _NoseLineColor ("Nose Line Color", Color) = (1,1,1,1)
        [Sub(NoseLine)] _NoseLinePower ("Nose Line Power", Range(0, 8)) = 1
        [SubGroup(FaceDetails, EyeEffect)] _EyeEffect ("Eye Effect", Float) = 0
        [Sub(EyeEffect)] _EyeEffectColor ("Eye Effect Color", Color) = (1,1,1,1)
        [Sub(EyeEffect)] _EyeEffectProcs ("Eye Effect Process", Range(0, 1)) = 0
        [SubGroup(FaceDetails, SpecialEyeEffect)] _SpecialEyeEffect ("Special Eye Effect", Float) = 0
        [Sub(SpecialEyeEffect)]  _UseSpecialEye ("Use Special Eye", Float) = 0
        [Tex(SpecialEyeEffect, _Color, small, collapsed)] _SpecialEyeShapeTexture ("Special Eye Shape Texture", 2D) = "black" { }
        [Sub(SpecialEyeEffect)] _EyeCenter ("Special Eye Vector", Vector) = (0,0,0,0)
        [Sub(SpecialEyeEffect)] _EyeSPColor1 ("Special Eye Color1", Color) = (1,1,1,1)
        [Sub(SpecialEyeEffect)] _EyeSPColor2 ("Special Eye Color2", Color) = (1,1,1,1)
        [Sub(SpecialEyeEffect)] _SpecialEyeIntensity ("Special Eye Intensity", Range(0, 5)) = 1
        [Main(LightingGroup, _, off, off)] _LightingGroup ("Lighting", Float) = 0
            [SubGroup(LightingGroup, EmissionGroup)] _EmissionGroup ("Emission", Float) = 0
        [Sub(EmissionGroup)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        [Sub(EmissionGroup)] _EmissionIntensity ("Emission Intensity", Float) = 0
        [SubGroup(LightingGroup, SpecularGroup)] _SpecularGroup ("Specular", Float) = 0
        [Sub(SpecularGroup)] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        [Sub(SpecularGroup)] _SpecularPow ("Specular Pow", Float) = 1
        [Sub(SpecularGroup)] _SpecularIntensity ("Specular Intensity", Float) = 1
        [Sub(SpecularGroup)] _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 1
        [SubGroup(LightingGroup, ShadowsGroup)] _ShadowGroup ("Shadow", Float) = 0  
        [Sub(ShadowsGroup)] _ShadowColor ("Dark Color", Color) = (0.5,0.5,0.5,1)
        [Sub(ShadowsGroup)] _DarkColor ("Shadow Color", Color) = (0.85,0.85,0.85,1)
        [Sub(ShadowsGroup)] _EyeShadowColor ("Eye Shadow Color", Color) = (1,1,1,1)
        [Sub(ShadowsGroup)] _EyeBaseShadowColor ("EyeBase Shadow Color", Color) = (1,1,1,1)
        [Sub(ShadowsGroup)] _EyeShadowAngleMin ("EyeBase Shadow Min Angle", Range(0.36, 1.36)) = 0.85
        [Sub(ShadowsGroup)] _EyeShadowMaxAngle ("EyeBase Shadow Max Angle", Range(0, 1)) = 1
        [Sub(ShadowsGroup)] _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        [Sub(ShadowsGroup)] _ShadowFeather ("Shadow Feather", Range(0.0001, 0.05)) = 0.0001
        [Sub(ShadowsGroup)] _BackShadowRange ("Back Shadow Range", Range(0, 1)) = 0
        [SubGroup(LightingGroup, RimShadow)] _RimShadow ("Rim Shadow", Float) = 0
        [Sub(RimShadow)] _RimShadowColor ("Rim Shadow Color", Color) = (1,1,1,1)
        [Sub(RimShadow)] _RimShadowWidth ("Rim Shadow Width", Float) = 1
        [Sub(RimShadow)]  _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [Sub(RimShadow)] _RimShadowFeather ("Rim Shadow Feather", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadow)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [Sub(RimShadow)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        [SubGroup(LightingGroup, Fresnel)] _RimColor ("Fresnel", Color) = (1,1,1,1)
        [Sub(Fresnel)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        [Sub(Fresnel)] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        [Sub(Fresnel)] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [Main(Outline, _, off, off)] _Outline ("Outline", Float) = 0
        [Sub(Outline)] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        [Sub(Outline)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [SubKeywordEnum(Outline, Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        [Sub(Outline)] _OutlineFixRange1 ("Lip Outline Min Range (Side A)", Range(0, 1)) = 0.1
        [Sub(Outline)] _OutlineFixRange2 ("Lip Outline Max Range (Side A)", Range(0, 1)) = 0.1
        [Sub(Outline)] _OutlineFixRange3 ("Lip Outline Min Range (Side B)", Range(0, 1)) = 0.1
        [Sub(Outline)] _OutlineFixRange4 ("Lip Outline Max Range (Side B)", Range(0, 1)) = 0.1
        [Sub(Outline)] _OutlineFixSide ("Outline Fix Star Side", Range(0, 1)) = 0.6
        [Sub(Outline)] _OutlineFixFront ("Outline Fix Star Front", Range(0, 1)) = 0.05
        [Sub(Outline)] _FixLipOutline ("TurnOn Temp Lip Outline", Range(0, 1)) = 0
        // [SubGroup(Outline, OutlineOffset)] _OutlineOffsets ("Outline Offset", Float) = 0
        // [Sub(OutlineOffset)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        // [Sub(OutlineOffset)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        // [Sub(OutlineOffset)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        // [Sub(OutlineOffset)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, Dither)] _Dither ("Dither", Float) = 0
        [Sub(Dither)] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        [Sub(Dither)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Sub(Dither)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [SubGroup(Effects, BLOOM)] _Bloom ("Bloom", Float) = 0
        [Sub(BLOOM)] _mBloomIntensity0 ("Bloom Intensity", Float) = 0
        [Sub(BLOOM)] _mBloomIntensity1 ("Eye Bloom Intensity", Float) = 0
        [SubGroup(Effects, Dissolve)] _Dissolve ("Dissolve", Float) = 0
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
        [SubGroup(Effects, Expression)] _Expression ("Expression", Float) = 0
            [Sub(Expression)] _ExCheekColor ("Expression Cheek Color", Color) = (1,1,1,1)
        [Sub(Expression)]_ExMapThreshold ("Expression Map Threshold", Range(0, 1)) = 0.5
        [Sub(Expression)]_ExSpecularIntensity ("Expression Specular Intensity", Range(0, 7)) = 0
        [Sub(Expression)]_ExCheekIntensity ("Expression Cheek Intensity", Range(0, 1)) = 0
        [Sub(Expression)][Space(10)] _ExShyColor ("Expression Shy Color", Color) = (1,1,1,1)
        [Sub(Expression)]_ExShyIntensity ("Expression Shy Intensity", Range(0, 1)) = 0
        [Sub(Expression)][Space(10)] _ExShadowColor ("Expression Shadow Color", Color) = (1,1,1,1)
        [Sub(Expression)]_ExEyeColor ("Expression Eye Color", Color) = (1,1,1,1)
        [Sub(Expression)]_ExShadowIntensity ("Expression Shadow Intensity", Range(0, 1)) = 0
        
        [HideInInspector] _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        
        [HideInInspector] _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)

        [HideInInspector] _HideCharaParts ("Hide Chara Parts", Float) = 0
        [HideInInspector] _ShowPartID ("Show Part ID", Float) = 0
        [HideInInspector] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [HideInInspector] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [HideInInspector] _IsMonster ("Is Monster", Float) = 0
        [HideInInspector] _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 0
        [HideInInspector] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
}
    SubShader
    {
        Tags { "QUEUE" = "Geometry+10" "RenderType" = "Opaque" }
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
            Cull Off
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER 
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
            Cull Off
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
            ColorMask 0
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma shader_feature_local _RECEIVE_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            
            #pragma vertex vert_base
            #pragma fragment frag_stencil

            ENDHLSL
        } 

        Pass 
        {
            Name "face forward"
            Tags { "LightMode"="ForwardEmission" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull Off
            
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER 
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
                Ref 16
                ReadMask 255
                WriteMask 254
                CompFront Always
                CompBack Always
                PassFront Keep
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
            #pragma multi_compile _OUTLINENORMALFROM_NORMAL _OUTLINENORMALFROM_TANGENT _OUTLINENORMALFROM_UV2
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER
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