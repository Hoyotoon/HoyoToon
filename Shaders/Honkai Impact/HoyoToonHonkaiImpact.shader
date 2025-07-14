Shader "HoyoToon/Honkai Impact/Character Part 1"
{
    Properties 
  { 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
        //Header
        //[HideInInspector] shader_master_label ("✧<b><i><color=#C69ECE>HoyoToon Honkai Impact</color></i></b>✧", Float) = 0
        [HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/hi3p1logo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/hi3p1l", Float) = 0
        [HideInInspector] CharacterRight ("UI/hi3p1r", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
        [HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
        [HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        //Header End

        [HoyoToonShaderOptimizerLockButton] _ShaderOptimizerEnabled ("Lock Material", Float) = 0

        //Material Type
        [HoyoToonWideEnum(Base, 0, Face, 1, Hair, 2, Eye, 3, Mouth, 4, EyeL, 5, EyeR, 6)]variant_selector("Material Type--{on_value_actions:[
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=8}]},
            {value:0,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]}]},
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:1,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:4,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:4,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}]}]},
            {value:4,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2001}, {type:SET_PROPERTY,data:render_type=Opaque}]},
            
            {value:5,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:5,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}]}]},
            {value:5,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2001}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:6,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:6,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}]}]},
            {value:6,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2001}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:2,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:2,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2002}, {type:SET_PROPERTY,data:render_type=Opaque}]},
            
            {value:3,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:3,actions:[{value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2001}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Int) = 0
        //Material Type End
            
        // Main
        [HideInInspector] start_main ("Main", Float) = 0
            [SmallTexture]_MainTex ("Texture", 2D) = "white" {}
            [SmallTexture]_LightMapTex ("LightMap Texture", 2D) = "white" {}
            _Color ("Main Color", Color) = (1,1,1,1)
            _EnvColor ("Tint Color", Color) = (1,1,1,1)
            [Toggle] _BackFaceUseUV2 ("BackFace Use UV2", Float) = 0
            _BackFaceColor ("Back Face Color", Color) = (1,1,1,1)
            [HideInInspector] _BackColor ("Back Face Color", Color) = (1,1,1,1)
            // Main Alpha
            [HideInInspector] start_mainalpha ("Alpha Options--{reference_property:_alphatoggler}", Float) = 0
                [HideInInspector] [Toggle] _alphatoggler ("idk how you see me", Float) = 0
                [HoyoToonWideEnum(None, 0, Alpha, 1, Emission, 2)]_AlphaType("Transparency Type--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}, {type:SET_PROPERTY,data:_alphatoggler=0}]},

                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}, {type:SET_PROPERTY,data:render_queue=2003}, {type:SET_PROPERTY,data:render_type=Opaque}, {type:SET_PROPERTY,data:_alphatoggler=1}]},

                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}, {type:SET_PROPERTY,data:_alphatoggler=1}]}]}", Int) = 0
                [Toggle] _AlphaClip ("Use Alpha Clip", Float) = 0
                _Opaqueness ("Opaqueness", Range(0, 1)) = 1

            [HideInInspector] end_mainalpha ("", Float) = 0
            [HideInInspector] start_directions("Facing Directions", Float) = 0
                _headForward ("Forward Vector || XYZ", Vector) = (0, 0, 1, 0)
                _headUp ("Up Vector || XYZ", Vector) = (0, 1, 0, 0)
                _headRight ("Right Vector || XYZ", Vector) = (-1, 0, 0, 0)
            [HideInInspector] end_directions ("", Float) = 0
        [HideInInspector] end_main ("", Float) = 0
        //Main End

        //ifex variant_selector == 0 || variant_selector == 2
            //Face 
            [HideInInspector] start_faceshading("Face--{condition_show:{type:OR,conditions:[{type:PROPERTY_BOOL,data:variant_selector==1},{type:PROPERTY_BOOL,data:variant_selector==3}]}}", Float) = 0.0
                _FaceMapTex ("FaceMap Texture", 2D) = "grey" {}
                _FacExpTex ("Face Expression Texture", 2D) = "black" { }
                _ShadowFeather ("Shadow Feather", Range(0.0001, 1)) = 0.001 // for face
                [HideInInspector] start_eyes("Eyes", Float) = 0
                    [Toggle] _EyeEffectPupil ("Eye Effect Pupil", Float) = 0
                    _EyeEffectTex ("Eye Effect Texture", 2D) = "white" { }
                    _EyeEffectCenterPos ("Eye Effect Center Position", Vector) = (0.5,0.5,0,0)
                    _EyeEffectLocalScale ("Eye Effect Local Scale", Vector) = (1,1,0,0)
                [HideInInspector] end_eyes(" ", Float) = 0

                [HideInInspector] start_faceexpression("Facial Expressions", Float) = 0
                    _ExpBlushColor ("Expression Blush Color", Color) = (1,0,0,1)
                    _ExpBlushIntensity ("Expression Blush Intensity", Range(0, 1)) = 0
                    _ExpShadowColor ("Expression Shadow Color", Color) = (0.5,0.5,0.5,1)
                    _ExpShadowIntensity ("Expression Shadow Intensity", Range(0, 1)) = 0
                    _ExpShadowColor2 ("Expression Shadow Color", Color) = (0.5,0.5,0.5,1)
                    _ExpShadowIntensity2 ("Expression Shadow Intensity", Range(0, 1)) = 0
                    _ExpShadowColor3 ("Expression Shadow Color", Color) = (0.5,0.5,0.5,1)
                    _ExpShadowIntensity3 ("Expression Shadow Intensity", Range(0, 1)) = 0
                [HideInInspector] end_faceexpression(" ", Float) = 0
            [HideInInspector] end_faceshading(" ", Float) = 0
            //Face End
        //endex

        //Lighting 
        //ifex _EnableShadow == 0
            [HideInInspector] start_lighting("Shadows--{reference_property:_EnableShadow}", Float) = 0
                [Toggle] _EnableShadow ("Enable Shadow", Float) = 1
                [Toggle] _MultiLight ("Enable Multi Light Support", Float) = 1
                [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1
                _LightArea ("Light Area Threshold", Range(0, 1)) = 0.51
                [HideInInspector] start_shadowcolor("Shadow Colors", Float) = 0
                    _ShadowColor ("Shadow Color", Color) = (0.9,0.7,0.75,1)
                    _FirstShadowMultColor ("Shadow Color 1", Color) = (0.9,0.7,0.75,1)
                    _FirstShadowMultColor2 ("Shadow Color 2", Color) = (0.9,0.7,0.75,1)
                    _FirstShadowMultColor3 ("Shadow Color 3", Color) = (0.9,0.7,0.75,1)
                    _FirstShadowMultColor4 ("Shadow Color 4", Color) = (0.9,0.7,0.75,1)
                    _FirstShadowMultColor5 ("Shadow Color 5", Color) = (0.9,0.7,0.75,1)
                [HideInInspector] end_shadowcolor(" ", Float) = 0
            [HideInInspector] end_lighting("", Float) = 0
            //Lighting End
        //endex

        //ifex _EnableSpecular == 0
            //Specular
            [HideInInspector] start_specular("Specular Reflections--{reference_property:_EnableSpecular}", Int) = 0
                [Toggle] _EnableSpecular ("Enable Specular", Float) = 1
                _Shininess ("Specular Shininess", Range(0.1, 100)) = 10
                _SpecMulti ("Specular Multiply Factor", Range(0, 1)) = 0.1
                _LightSpecColor ("Light Specular Color", Color) = (1,1,1,1)
            [HideInInspector] end_specular("", Float) = 0
            //Specular End
        //endex

        //ifex _EnableOutline == 0
            //Outlines
            [HideInInspector] start_outlines("Outlines--{reference_property:_EnableOutline}", Float) = 0
                [Toggle] _EnableOutline ("Enable Outline", Float) = 1
                [Toggle] _DisableFOVScalingOL ("Disable FOV Scaling", Float) = 1
                _OutlineWidth ("Outline Width", Range(0, 100)) = 0.0
                _Scale ("Outline Scale", Float) = 0.01 // I don't think there's a noticable difference between hoyo2vrc and maya scales
                _GlobalOutlineScale ("X: Scale W:Distance Toggle", Vector) = (1, 1, 1, 1) //X: Scale Y:Distance Toggle (w > 0.09: manual, w < 0.09: camera)
                _MaxOutlineZOffset ("Max Outline Z Offset", Range(0, 100)) = 1
                _OutlineCamStart ("Outline Camera Adjustment Start Distance", Range(0, 10000)) = 1000
                [Toggle(TRANSPARENTOUTLINE)] _TrasOutline ("Outline is Transparent By MainTex", Float) = 0
                [Toggle] _OutlineTrans ("Outline Tranparent", Float) = 0
                [HideInInspector] start_outlineAlpha("Outline Alpha Options", Float) = 0
                    [Toggle] _UseAlphaClip ("Use Alpha Clipping", Float) = 0
                    _AlphaClipThreshold ("Clip Value", Range(0, 1)) = 1 
                [HideInInspector] end_outlineAlpha(" ", Float) = 0
                [HideInInspector] start_outlinescolor("Outline Colors", Float) = 0
                    _OutlineColor ("Outline Color 1", Color) = (0,0,0,1)
                    _OutlineColor2 ("Outline Color 2", Color) = (0,0,0,1)
                    _OutlineColor3 ("Outline Color 3", Color) = (0,0,0,1)
                    _OutlineColor4 ("Outline Color 4", Color) = (0,0,0,1)
                    _OutlineColor5 ("Outline Color 5", Color) = (0,0,0,1)
                [HideInInspector] end_outlinescolor(" ", Float) = 0
                [HideInInspector] start_outlinefade("Outline Fade", Float) = 0
                [Toggle] _UseCameraFade ("Use Camera Fade", Float) = 0
                _FadeOffset ("Fade Offset", Float) = 0
                _FadeDistance ("Fade Distance", Float) = 0
                [HideInInspector] end_outlinefade("Outline Fade", Float) = 0
            [HideInInspector] end_outlines(" ", Float) = 0
            //Outlines End
        //endex

        //ifex _RimGlow == 0
            //Rimlights
            [HideInInspector] start_rimlight("Rim Light--{reference_property:_RimGlow}", Float) = 0
                [Toggle] _RimGlow ("Enable Rim", Float) = 0
                _RGColor ("Rim Glow Color 1", Color) = (1,1,1,1)
                _RGShininess ("Rim Glow Shininess", Float) = 1
                _RGScale ("Rim Glow Scale", Float) = 1
                _RGBias ("Rim Glow Bias", Float) = 0
                _RGRatio ("Rim Glow Ratio", Range(-1, 1)) = 0.5
                _RGBloomFactor ("Rim Glow Bloom Factor", Float) = 1
                [HideInInspector] start_hardrimlight("Hard Rim Light", Float) = 0
                    _HRRimIntensity ("Hard Rim Mask", Range(0, 1)) = 0
                    _HRRimPower ("Hard Rim Ratio", Range(0, 3)) = 0.1
                    _Hardness ("Rim Hardless", Range(0, 5)) = 0.1
                    [Toggle(Hardrim)] _MoreHardRimColor ("More HardRim Color", Range(0, 1)) = 0
                    [HideInInspector] start_hardrimlightcolors("Hard Rimlight Colors", Float) = 0
                        _HRRimColor2 ("Hard Rim Color 2", Color) = (1,1,1,1)
                        _HRRimColor3 ("Hard Rim Color 3", Color) = (1,1,1,1)
                        _HRRimColor4 ("Hard Rim Color 4", Color) = (1,1,1,1)
                        _HRRimColor5 ("Hard Rim Color 5", Color) = (1,1,1,1)
                    [HideInInspector] end_hardrimlightcolors(" ", Float) = 0
                [HideInInspector] end_hardrimlight(" ", Float) = 0
            [HideInInspector] end_rimlight(" ", Float) = 0
            //Rimlights End
        //endex

        //Special Effects
        [HideInInspector] start_specialeffects("Special Effects", Float) = 0
            //ifex _EnableEmission == 0
                [HideInInspector] start_emission("Emission--{reference_property:_EnableEmission}", Float) = 0
                [Toggle] _EnableEmission ("Enable Emission", Float) = 1
                    _EmissionStr ("Emission Strength", Float) = 0
                    [Toggle] _EmissionColorToggle ("Emission Colors", Float) = 0
                    [Toggle] _usepulse ("Emission Pulsing", Float) = 0

                    [HideInInspector] start_emissionColor("Emission Colors", Float) = 0
                        _EmissionColor ("Emission Color 1", Color) = (1,1,1,1)
                        _EmissionColor2 ("Emission Color 2", Color) = (1,1,1,1)
                        _EmissionColor3 ("Emission Color 3", Color) = (1,1,1,1)
                        _EmissionColor4 ("Emission Color 4", Color) = (1,1,1,1)
                        _EmissionColor5 ("Emission Color 5", Color) = (1,1,1,1)
                    [HideInInspector] end_emissionColor(" ", Float) = 0
                    [HideInInspector] start_Pulse("Emission Pulsing--{condition_show:{type:PROPERTY_BOOL,data:_usepulse==1.0}}", Float) = 0
                        _PulseRate ("Pulse Speed", Range(0, 30)) = 0
                        _MinPulse ("Min Pulse Strength", Range(0, 1)) = 0
                        _MaxPulse ("Max Pulse Strength", Range(0, 1)) = 1
                    [HideInInspector] end_Pulse(" ", Float) = 0
                [HideInInspector] end_emission(" ", Float) = 0
            //endex
            //ifex _EnableStencil == 0
                [HideInInspector] start_stencil("Stencil--{reference_property:_EnableStencil}", Float) = 0
                    [Toggle] _EnableStencil ("Enable Stencil", Float) = 1
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassA ("Stencil Pass Op A", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassB ("Stencil Pass Op B", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
                    [IntRange] _StencilRef ("Stencil Reference Value", Range(0, 255)) = 0
                [HideInInspector] end_stencil("", Float) = 0
            //endex 
            //ifex _LengthWaysDis == 0 
                [HideInInspector] start_dissolve("Dissolve--{reference_property:_LengthWaysDis}", Float) = 0
                    [Toggle(LENGTHWAYSDIS)] _LengthWaysDis ("Enable", Float) = 0
                    _MaskDisTex ("Dissolve Mask", 2D) = "White" {}
                    _NoiseTex ("Noise Texture", 2D) = "black" { }
                    _MainTex2 ("Secondary Diffuse", 2D) = "white" { }
                    _AlphaPosition ("AlphaPosition", Float) = 0
                    _DisAngle ("DisAngle", Range(0, 360)) = 0
                    _DisColor ("DisColor", Color) = (1,1,1,1)
                    _DisColorScale ("DisColorScale", Float) = 1
                    _Edge ("DisEdge", Range(0, 1)) = 0.2
                    _TintColorEdge ("TintColorEdge", Range(0, 1)) = 0
                    _AddLightColor ("Dissolve Add Color", Color) = (1, 1, 1, 1)
                    _Soft ("SoftEdge", Range(0, 1)) = 0
                    _TintColorPower ("TintColorPower", Range(0, 3)) = 1
                    _MaskTillingOffset ("MaskDisTexture Tilling(XY) Offset(ZW)", Vector) = (1,1,0,0)
                    _MaskOffsetSpeed ("MaskDisTexture Offset Speed", Float) = 0
                    _MaskDisTexScale ("Dissolve Mask Scale", Float) = 1
                            
                    _NoiseIntensity ("Noise Intensity", Range(0, 10)) = 5
                    _NoiseTillingOffset ("NoiseTexture Tilling(XY) Offset(ZW)", Vector) = (1,1,0,0)
                    [Toggle] _LengthWaysDisBlend ("UseMainTex2 Blend", Float) = 0
                    [Toggle] _DissolveUseUV2 ("Dissolve Use UV2 ", Float) = 1
                    [Toggle] _OnlyUseMaskDis ("Only Use MaskDisTexture", Float) = 0
                [HideInInspector] end_dissolve ("", Float) = 0
            //endex
        [HideInInspector] end_specialeffects(" ", Float) = 0

        //Rendering Options
        [HideInInspector] start_renderingOptions("Rendering Options", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Int) = 0	
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Int) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Int) = 0
        [HideInInspector] end_renderingOptions(" ", Float) = 0
        //Rendering Options End
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" 
            "LightMode" = "ForwardBase"
            // "PassFlags" = "OnlyDirectional" 
            // the above line actually was fucking with the point lights
        }
        Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
        HLSLINCLUDE

        // material macros
        //ifex _EnableShadow == 0
            #define use_shadow
        //endex
        //ifex _EnableSpecular == 0
            #define use_specular
        //endex
        //ifex _EnableEmission == 0
            #define use_emission
        //endex
        //ifex _RimGlow == 0
            #define use_rimlight
        //endex
        //ifex variant_selector == 0 || variant_selector == 2
            #define faceishadow
        //endex
        //ifex _LengthWaysDis == 0
            #define can_dissolve
        //endex

        #include "Includes/HoyoToonHonkaiImpact-Declarations.hlsl"
        #include "Includes/HoyoToonHonkaiImpact-Colors.hlsl"

        #include "UnityCG.cginc"
        #include "UnityPBSLighting.cginc"
        #include "UnityShaderVariables.cginc"
        #include "AutoLight.cginc"
        #include "UnityLightingCommon.cginc"
        #include "Lighting.cginc"

        ENDHLSL

        // UsePass "Hidden/HoyoToon/HI3Depth/ShadowCast" // shadow casting

        Pass
        {
            Name "Character Shading"
            Tags{ "LightMode" = "ForwardBase"}    
            Cull [_CullMode]
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
                ref [_StencilRef]  
                Comp [_StencilCompA]
                Pass [_StencilPassA] // this doesn't even fucking matter like what?
                Fail Keep
                ZFail Keep
            }
            HLSLPROGRAM

            #pragma vertex vs_model
            #pragma fragment ps_model


            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
                    

            //imports
            #include "Includes/HoyoToonHonkaiImpact-Import.hlsl"

            //shader
            #include "Includes/HoyoToonHonkaiImpact-Common.hlsl"
            #include "Includes/HoyoToonHonkaiImpact-Program.hlsl"

            ENDHLSL
        }

        //ifex _EnableStencil == 0
        Pass
        {
            Name "Character Stencil"
            Tags{  "LightMode" = "ForwardBase" } 
            Cull [_CullMode]
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                ref [_StencilRef]              
                Comp [_StencilCompB]
                Pass [_StencilPassB]  
                Fail Keep
                ZFail Keep
            }
            HLSLPROGRAM

            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #define stencil


            #pragma vertex vs_model
            #pragma fragment ps_model


            //imports
            #include "Includes/HoyoToonHonkaiImpact-Import.hlsl"

            //shader
            #include "Includes/HoyoToonHonkaiImpact-Common.hlsl"
            #include "Includes/HoyoToonHonkaiImpact-Program.hlsl"
            // 
            ENDHLSL 
        }
        //endex
        //ifex _MultiLight == 0
        Pass
        {
            Name "Character Lighting"
            Tags { "LightMode" = "ForwardAdd" }
            Cull [_CullMode]
            Blend One One
            // ZWrite Off
            HLSLPROGRAM
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT
            #pragma vertex vs_model
            #pragma fragment ps_model

            //imports
            #include "Includes/HoyoToonHonkaiImpact-Import.hlsl"

            //shader
            #include "Includes/HoyoToonHonkaiImpact-Common.hlsl"
            #include "Includes/HoyoToonHonkaiImpact-Program.hlsl"

            ENDHLSL
        }
        //endex
        //ifex _EnableOutline == 0
        Pass
        {
            Name "Character Outline"
            Tags{  "LightMode" = "ForwardBase" } 
            Cull front
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
				ref 255
                Comp Always
                Pass Keep
            }
            HLSLPROGRAM
                    
            #pragma vertex edge_model
            #pragma fragment ps_edge
                    
                    
            //imports
            #include "Includes/HoyoToonHonkaiImpact-Import.hlsl"
                    
            //shader
            #include "Includes/HoyoToonHonkaiImpact-Common.hlsl"
            #include "Includes/HoyoToonHonkaiImpact-Program.hlsl"
                    
            ENDHLSL
        }
        //endex
    } 
    CustomEditor "HoyoToon.ShaderEditor"
}
