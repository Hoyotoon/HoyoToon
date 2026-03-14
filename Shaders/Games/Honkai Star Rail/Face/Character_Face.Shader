Shader "HoyoToon/Honkai Star Rail/Character/Face"
{
    Properties
    {
        [MHYHelpBox(Info, Vertex Color B (outline width))] [MHYHeaderBox(OPTIONS)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        _SelfShadowDepthOffset ("_SelfShadowDepthOffset", Float) = 0
        _SelfShadowSampleOffset ("_SelfShadowSampleOffset", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        _LipLineFixScale ("Lip Line Fix Threshold", Range(0.01, 4)) = 1
        _LipLinefixColor ("Lip Line Fix Color", Color) = (0,0,0,0)
        _LipLineFixThrd ("Lip Line Fix Threshold", Range(0, 0.95)) = 0.3
        _LipLineFixStart ("Lip Line Fix Start Distance", Range(0.04, 0.95)) = 0.25
        _LipLineFixMax ("Lip Line Fix Maximum Distance", Range(1, 2)) = 1.5
        _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        [Toggle]_UseSelfShadow ("Use Self Shadow", Float) = 1
        [MHYHeaderBox(MAPS)] [MHYHeader(Main Maps)] [MHYSingleLineTextureNoScaleOffset(_Color)] _MainTex ("Albedo", 2D) = "white" { }
        [MHYSingleLineTextureKeywordDrawer(_FACEMAP_ON)] _FaceMap ("Face Map |R(specular field) A (distance field)", 2D) = "white" { }
        _FaceExpression ("Face Expression Map |R (Cheek) G (Shy) B (Shadow)", 2D) = "black" { }
        [HideInInspector] _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [HideInInspector] _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        [HideInInspector] _UseUVChannel2 ("Use UV Channel2 For Facemap", Float) = 1
        [MHYHeaderBox(Expression)] _NoseLineColor ("Nose Line Color", Color) = (1,1,1,1)
        _NoseLinePower ("Nose Line Power", Range(0, 8)) = 1
        _EyeEffectColor ("Eye Effect Color", Color) = (1,1,1,1)
        _EyeEffectProcs ("Eye Effect Process", Range(0, 1)) = 0
        [MHYHeaderBox(SPECIALEYEEFFECT)] _UseSpecialEye ("Use Special Eye", Float) = 0
        [MHYSingleLineTextureNoScaleOffset(_Color)] _SpecialEyeShapeTexture ("Special Eye Shape Texture", 2D) = "black" { }
        _EyeCenter ("Special Eye Vector", Vector) = (0,0,0,0)
        _EyeSPColor1 ("Special Eye Color1", Color) = (1,1,1,1)
        _EyeSPColor2 ("Special Eye Color2", Color) = (1,1,1,1)
        _SpecialEyeIntensity ("Special Eye Intensity", Range(0, 5)) = 1
        [MHYHeaderBox(EMISSION)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        _EmissionIntensity ("Emission Intensity", Float) = 0
        [MHYHeaderBox(SPECULAR)] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularPow ("Specular Pow", Float) = 1
        _SpecularIntensity ("Specular Intensity", Float) = 1
        _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 1
        [MHYTextureScaleOffset] _MainMaps_ST ("Main Maps ST", Vector) = (1,1,0,0)
        [MHYHeaderBox(DIFFUSE)] _ShadowColor ("Dark Color", Color) = (0.5,0.5,0.5,1)
        _DarkColor ("Shadow Color", Color) = (0.85,0.85,0.85,1)
        _EyeShadowColor ("Eye Shadow Color", Color) = (1,1,1,1)
        _EyeBaseShadowColor ("EyeBase Shadow Color", Color) = (1,1,1,1)
        _EyeShadowAngleMin ("EyeBase Shadow Min Angle", Range(0.36, 1.36)) = 0.85
        _EyeShadowMaxAngle ("EyeBase Shadow Max Angle", Range(0, 1)) = 1
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowFeather ("Shadow Feather", Range(0.0001, 0.05)) = 0.0001
        _BackShadowRange ("Back Shadow Range", Range(0, 1)) = 0
        [MHYHeaderBox(ADDLIGHT)] _AddLightOffset ("Add Light Offset", Range(0, 1)) = 0.5
        _AddLightStrengthen ("Add Light Strengthen", Range(0, 3)) = 0.3
        _AddLightFeather ("Add Light Feather", Range(0, 0.1)) = 0.03
        [MHYHeaderBox(RIMSHADOW)] _RimShadowColor ("Rim Shadow Color", Color) = (1,1,1,1)
        _RimShadowWidth ("Rim Shadow Width", Float) = 1
        [HideInInspector] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        _RimShadowFeather ("Rim Shadow Feather", Range(0.01, 0.99)) = 0.01
        [HideInInspector] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        [MHYHeaderBox(RIMCOLOR)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [MHYHeaderBox(OUTLINE)] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [KeywordEnum(Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        _OutlineFixRange1 ("Lip Outline Show Start", Range(0, 1)) = 0.1
        _OutlineFixRange2 ("Lip Outline Show Max", Range(0, 1)) = 0.1
        _OutlineFixRange3 ("Lip Outline Show Start", Range(0, 1)) = 0.1
        _OutlineFixRange4 ("Lip Outline Show Max", Range(0, 1)) = 0.1
        _OutlineFixSide ("Outline Fix Star Side", Range(0, 1)) = 0.6
        _OutlineFixFront ("Outline Fix Star Front", Range(0, 1)) = 0.05
        _FixLipOutline ("TurnOn Temp Lip Outline", Range(0, 1)) = 0
        [HideInInspector] _Opaqueness ("Opaqueness", Range(0, 1)) = 1
        _HairBlendSilhouette ("Hair Blend Silhouette", Range(0, 1)) = 0.5
        [HideInInspector] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        [MHYHeaderBox(DITHER)] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [HideInInspector] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [MHYHeaderBox(BLOOM)] _mBloomIntensity0 ("Bloom Intensity", Float) = 0
        _mBloomIntensity1 ("Eye Bloom Intensity", Float) = 0
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
        _DissolvePosMaskPos ("", Vector) = (1,0,0,1)
        _DissolvePosMaskWorldON ("", Float) = 0
        _DissolvePosMaskRootOffset ("", Vector) = (0,0,0,0)
        _DissolvePosMaskFilpOn ("", Float) = 0
        _DissolvePosMaskOn ("", Float) = 0
        _DissolveMaskUVSet ("", Range(0, 1)) = 0
        _DissolveMapAdd ("", Float) = 0
        _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0
        [HideInInspector] _DissolveShadowOff ("Disable Disolve Shadow", Float) = 0
        [MHYHeaderBox(EXPRESSION)] _ExCheekColor ("Expression Cheek Color", Color) = (1,1,1,1)
        _ExMapThreshold ("Expression Map Threshold", Range(0, 1)) = 0.5
        _ExSpecularIntensity ("Expression Specular Intensity", Range(0, 7)) = 0
        _ExCheekIntensity ("Expression Cheek Intensity", Range(0, 1)) = 0
        [Space(10)] _ExShyColor ("Expression Shy Color", Color) = (1,1,1,1)
        _ExShyIntensity ("Expression Shy Intensity", Range(0, 1)) = 0
        [Space(10)] _ExShadowColor ("Expression Shadow Color", Color) = (1,1,1,1)
        _ExEyeColor ("Expression Eye Color", Color) = (1,1,1,1)
        _ExShadowIntensity ("Expression Shadow Intensity", Range(0, 1)) = 0
        [MHYHeaderBox(SHADOW)] [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        _StencilEye ("Stencil Eye", Float) = 17
        _StencilFace ("Stencil Face", Float) = 31
        _StencilMask ("Stencil Write Mask", Float) = 254
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOP ("Stencil Op", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _EyeMaskStencilComp ("Eye Mask Stencil Comparison", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _EyeMaskStencilOp ("Eye Mask Stencil Op", Float) = 2
         _ForwardStencilRef ("Forward Stencil Ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ForwardStencilComp ("Forward Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _ForwardStencilOp ("Forward Stencil Op", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _OutlineStencilComp ("Outline Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _OutlineStencilOp ("Outline Stencil Op", Float) = 2
        [HideInInspector] _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        _XorShadowPartID ("Xor Shadow Parts", Float) = 0
        [HideInInspector] _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        _LipLineFixSC ("_LipLineFixSC", Float) = 0
        _HideCharaParts ("Hide Chara Parts", Float) = 0
        _ShowPartID ("Show Part ID", Float) = 0
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