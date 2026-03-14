Shader "HoyoToon/Honkai Star Rail/Character/Hair"
{
    Properties
    {
        [MHYHelpBox(Info, Vertex Color B (outline width))] 
        [MHYHeaderBox(OPTIONS)] 
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        _SelfShadowDepthOffset ("_SelfShadowDepthOffset", Float) = 0
        _HideCharaParts ("Hide Chara Parts", Float) = 0
        _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        [MHYHeaderBox(MAPS)] [MHYHeader(Main Maps)] [MHYSingleLineTextureNoScaleOffset(_Color)] _MainTex ("Albedo |RGB(base color) A (alpha)", 2D) = "white" { }
        _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        [Toggle(_USE_NORMAL_MAP)] _UseNormalMap ("UseNormalMap", Float) = 0
        [HideInInspector] _NormalMap ("NormalMap", 2D) = "bump" { }
        [HideInInspector] _NormalScale ("NormalScale", Range(0, 4)) = 1
        [MHYSingleLineTextureNoScaleOffset] _LightMap ("Light Map |R (sepcular intensity) G (diffuse threshold) B (specular threshold) A (material id)", 2D) = "grey" { }
        [MHYTextureScaleOffset] _MainMaps_ST ("Main Maps ST", Vector) = (1,1,0,0)
        [HideInInspector] _Color ("Color", Color) = (1,1,1,1)
        _BackColor ("Back Color", Color) = (1,1,1,1)
        [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [MHYHeaderBox(Emission)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        _EmissionIntensity ("Emission Intensity", Range(0, 32)) = 0
        [MHYHeaderBox(DIFFUSE)] [MHYColorGradient] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
        [MHYColorGradient] _DiffuseRampMultiTex ("Diffuse Multiple Ramp", 2D) = "white" { }
        _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
        _HairBlendWeight ("Hair Blend Weight", Range(0, 2)) = 1
        _HairBlendOffset ("Hair Blend Offset", Range(-1, 0.1)) = 0
        [MHYHeaderBox(SPECULAR)] [MHYMaterialIDFold] _SpecularColor ("Specular Color", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor0 ("Specular Color (ID = 0)", Color) = (1,1,1,1)
        [Toggle(_ANISOTROPIC_ON)] _UsingAniso ("Enable Anisptropic", Float) = 0
        [MHYMaterialIDFold] _AnisoT ("Anisptropic by Sphere Tangent", Range(0, 1)) = 0
        [MHYMaterialIDFold] _AnisoN ("Anisptropic by Texture", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_AnisoN)] _TangentShifterTex ("Tangent Shifter Texture", 2D) = "black" { }
        _SpecularShininess ("Specular Shininess", Range(0.1, 500)) = 10
        _SpecularShininess0 ("Specular Shininess", Range(0.1, 500)) = 10
        _SpecularRoughness ("Specular Roughness", Range(0, 1)) = 0
        _SpecularRoughness0 ("Specular Roughness", Range(0, 1)) = 0
        _SpecularIntensity ("Specular Intensity", Range(0, 50)) = 1
        _SpecularIntensity0 ("Specular Intensity", Range(0, 50)) = 1
        _SpecularShadowOffset ("Specular Shadow Offset", Range(0, 1)) = 0.78
        _SpecularShadowIntensity ("Specular Shadow Intensity", Range(0, 1)) = 0
        [MHYHeaderBox(OUTLINE)] [MHYMaterialIDFold] _Outline ("Outline", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_Outline)] 
        _OutlineColorTex ("Outline Color", 2D) = "white" { }
        [HideInInspector] _OutlineColor0 ("Outline Color 0 (ID = 0)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor1 ("Outline Color 1 (ID = 1)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor2 ("Outline Color 2 (ID = 2)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor3 ("Outline Color 3 (ID = 3)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor4 ("Outline Color 4 (ID = 4)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor5 ("Outline Color 5 (ID = 5)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor6 ("Outline Color 6 (ID = 6)", Color) = (0,0,0,1)
        [HideInInspector] _OutlineColor7 ("Outline Color 7 (ID = 7)", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        _OutlineBlendWeight ("Outline Blend Weight", Range(0, 4)) = 1
        _OutlineBlendOffset ("Outline Blend Offset", Range(-1, 0.1)) = 0
        _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        [KeywordEnum(Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        _OutlineEnhanceAmt ("Outline Enhance By Distance Amount", Range(0, 0.5)) = 0.5
        [MHYHeaderBox(RIMLIGHT)] _RimLightMode ("0:don't use lightmap.r, 1:use", Range(0, 1)) = 1
        [MHYMaterialIDFold] _RimLight ("Rim Light", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_RimLight)] _RimWidth0 ("RimWidth 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimColor0 ("RimColor 0 (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness0 ("RimSoftness 0 (ID = 0)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType0 ("RimType 0 (ID = 0)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark0 ("RimType 0 (ID = 0)", Range(0, 1)) = 0.5
        _Rimintensity ("Rim Intensity", Float) = 3
        _RimWidth ("RimWidth", Float) = 1
        _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
        _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015
        [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [HideInInspector] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [HideInInspector] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [MHYHeaderBox(RIMSHADOW)] [MHYMaterialIDFold] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor0 ("Rim Shadow Color 0 (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth0 ("Rim Shadow Width 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather0 ("Rim Shadow Feather 0 (ID = 0)", Range(0.01, 0.99)) = 0.01
        _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
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
        [MHYHeaderBox(ADDLIGHT)] _AddLightOffset ("Add Light Offset", Range(0, 1)) = 0.5
        _AddLightStrengthen ("Add Light Strengthen", Range(0, 5)) = 0.3
        _AddLightFeather ("Add Light Feather", Range(0, 0.1)) = 0.03
        [MHYHeaderBox(BLOOM INSTENSITY)] [MHYMaterialIDFold] _BloomIntensity ("Bloom Intensity", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity0 ("Bloom Intensity 0 (ID = 0)", Float) = 0
        [MHYHeaderBox(SHOWPARTSBYID)] [MHYMaterialIDFold] _ShowPartID ("Show Parts", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID0 ("Show Part 1 (ID = 0)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID1 ("Show Part 2 (ID = 1)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID2 ("Show Part 3 (ID = 2)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID3 ("Show Part 4 (ID = 3)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID4 ("Show Part 5 (ID = 4)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID5 ("Show Part 6 (ID = 5)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID6 ("Show Part 7 (ID = 6)", Float) = 0
        [MHYMaterialIDProperty(_ShowPartID)] _ShowID7 ("Show Part 6 (ID = 7)", Float) = 0
        [MHYHeaderBox(DITHER)] _UsingDitherAlpha ("UsingDitherAlpha", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [HideInInspector] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 0
        _StencilEye ("Stencil Eye", Float) = 17
        [Enum(UnityEngine.Rendering.StencilOp)] _HairStencilOp ("Hair Stencil Op", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _HairStencilComp ("Hair Stencil Comp", Float) = 6
        _HairStencilMask ("Hair Stencil Mask", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _EyeMaskStencilOp ("Eye Mask Stencil Op", Float) = 2
        _ForwardStencilRef ("Forward Stencil Ref", Range(0, 255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ForwardStencilComp ("Forward Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.CompareFunction)] _OutlineStencilComp ("Outline Stencil Comp", Float) = 8
        [HideInInspector] _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        _XorShadowPartID ("Xor Shadow Parts", Float) = 0
        _ShadowBoost ("Shadow Boost Enable", Float) = 0
        _ShadowBoostVal ("Shadow Boost Intensity", Range(0, 0.5)) = 0
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