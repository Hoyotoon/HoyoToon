Shader "HoyoToon/Genshin/Particles"
{
    Properties
    {
        [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
        // ui header shit
        [HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/gilogo", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
		[HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/Melioli/HoyoToon},hover:Github}", Float) = 0
		[HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        
        [HoyoToonWideEnum(One Channel, 0, UV Move, 1, Liquid Common, 2, Lightning Bolt, 3, Line Renderer, 4)] _ParticleType ("Particle Type", Float) = 0

        // --{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0}}
        // --{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==1}}
        // --{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}
        [HideInInspector] start_main ("Main", float) = 0
            [Toggle] _AlphaClipping("Clip?", Float) = 0
            _BaseTex ("Base Tex--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType<=1}}", 2D) = "white" { }
            [Enum(R, 0, G, 1, B, 2, A, 3, White, 4)] _BaseTexAlphaChannelSwitchONE ("Base Alpha Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0},on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=2}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=3}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=4}]}]}", Float) = 0
            [Enum(R, 0, G, 1, B, 2, A, 3, White, 4)] _BaseTexColorChannelSwitchONE ("Base Color Channel Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0},on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=2}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=3}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=4}]}]}", Float) = 1
            [Enum(RGB, 0, R, 1, G, 2, B, 3, A, 4)] _BaseTexAlphaChannelSwitchUV ("Base Alpha Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==1},on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=2}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=3}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:_BaseTexAlphaChannelSwitch=4}]}]}", Float) = 0
            [Enum(A, 0, R, 1, G, 2, B, 3)] _BaseTexColorChannelSwitchUV ("Base Color Channel Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==1},on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=2}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=3}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:_BaseTexColorChannelSwitch=4}]}]}", Float) = 1        
            [HideInInspector][Enum(R, 0, G, 1, B, 2, A, 3, White, 4)] _BaseTexAlphaChannelSwitch ("Base Alpha Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0}}", Float) = 0
            [HideInInspector][Enum(R, 0, G, 1, B, 2, A, 3, White, 4)] _BaseTexColorChannelSwitch ("Base Color Channel Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0}}", Float) = 1
            [Toggle] _UseCustom2ColorToggle ("Use Custom Color 2--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0}}", Float) = 0
            _Color ("Color--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Color) = (1,1,1,1)
            _MainColor ("Main Color--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==0}}", Color) = (0.5019608,0.5019608,0.5019608,0.5019608)
            _DayColor ("Day Color", Color) = (1,1,1,1)
            _ColorBrightnessMax ("Color Brightness Max", Float) = 1.05
            [HDR] _AllColorBrightness ("Color Brightness--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Color) = (1,1,1,1)
            _ColorBrightness ("Color Brightness", Range(0, 50)) = 1
            _AlphaBrightness ("Alpha Brightness", Range(0, 50)) = 1
            _Alpha ("Alpha", Range(0, 1)) = 1
            [HideInInspector] start_basetex("Base Control--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==1}}", Float) = 0
                _BaseTexAlphaBrightness ("Base Alpha Brightness", Float) = 1
                [Toggle] _BaseTexURandomToggle ("Texture Random X Toggle", Float) = 0
                [Toggle] _BaseTexVRandomToggle ("Texture Random Y Toggle", Float) = 0
                _BaseTex_Uspeed ("Texture X Speed", Float) = 1
                _BaseTex_Vspeed ("Texture Y Speed", Float) = 1
            [HideInInspector] end_basetex("", Float) = 0
            // [HideInInspector] start_alpha ("Alpha--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==1}}", Float) = 0
            //     [Toggle] _AlphaSoftedgeToggle ("Soft Alpha Edge", Float) = 0
            //     [Toggle] _AlphaSoftedgeTwoSideToggle ("Two Sided Soft Edge[Toggle]", Float) = 0
            //     _AlphaSoftedgeScale ("AlphaSoftedgeScale", Float) = 1
            //     _AlphaSoftedgePower ("AlphaSoftedgePower", Float) = 1
            //     [Toggle] _AlphaFadeByDistanceToggle ("AlphaFadeByDistance[Toggle]", Float) = 0
            //     _AlphaFadeDistance ("AlphaFadeDistance", Float) = 2
            //     _AlphaFadeOffset ("AlphaFadeOffset", Float) = 0
            //     [Toggle] _AlphaFadeDistanceInvertToggle ("AlphaFadeDistanceInvertToggle", Float) = 0
            //     [Toggle] _AlphaFadeDistanceTwoWayToggle ("AlphaFadeDistanceTwoWayToggle", Float) = 0
            //     _AlphaFadeDistanceTwoWay ("AlphaFadeDistance(Two Way)", Float) = 1000
            //     _AlphaFadeOffsetTwoWay ("AlphaFadeOffset(Two Way)", Float) = 0
            // [HideInInspector] end_alpha ("", Float) = 0
            // AND,conditions:[{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<=3}]},{type:PROPERTY_BOOL,data:_ParticleType<=3}]
            [HideInInspector] start_lqdcolor ("Liquid Color--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Float) = 0
                _LiquidTex ("Liquid Texture", 2D) = "white" { }
                [HDR] _LiquidColor ("Liquid Color", Color) = (0.5019608,0.5019608,0.5019608,0.5019608)
                _LiquidColorBrightness ("Color Brightness", Range(0, 50)) = 1
            [HideInInspector] end_lqdcolor ("", Float) = 0
            [HideInInspector] start_lqdmove ("Scrolling--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Float) = 0
                [Toggle] _UspeedToggle ("Enable X Scrolling", Float) = 0
                _Uspeed ("X Speed", Float) = 0
            [HideInInspector] end_lqdmove ("", Float) = 0
            [HideInInspector] start_lqdnormal ("Normal Mapping--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Float) = 0
                _Normalmap ("Normal map", 2D) = "bump" { }
                _NormalIntensity ("Normal Intensity", Range(-1, 4)) = 1
            [HideInInspector] end_lqdnormal ("", Float) = 0
            [HideInInspector] start_lqdmatcap("Matcap--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Float) = 0
                _Matcap ("Matcap", 2D) = "white" { }
                [Toggle] _MatcapAlphaToggle ("Use Matcap Alpha", Float) = 0
                _MatcapSize ("Matcap Size", Range(0, 2)) = 1
            [HideInInspector] end_lqdmatcap("", Float) = 0
            [HideInInspector] start_lqdmask("Masking--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<=4}]}}", Float) = 0
                _TextureMask ("Texture Mask--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", 2D) = "white" { }
                [Toggle] _MaskTexToggle ("Enable Mask Texture--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                _NoiseIntensityOnMask ("Mask Noise Intensity--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                [Enum(Multiply, 0, Add, 1)] _MaskTexBlendModeToggle ("Mask Blending Mode--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                _MaskTex ("Mask Texture", 2D) = "white" { }
                _MaskTex1TillingAdd2Offset ("Mask Texture Tiling Offset--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 1
                [Enum(R, 0, G, 1, B, 2, A, 3)] _MaskTexSwitch ("Mask Texture Switch--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 2
                [Enum(R, 0, G, 1, B, 2, A, 3)] _MaskTexChannelSwitch ("Mask Texture Switch--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 2
                _MaskTex_Uspeed ("Mask Texture X Speed", Float) = 0
                _MaskTex_Vspeed ("Mask Texture Y Speed", Float) = 0
                _MaskTexBrightness ("Mask Texture Brightness--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 1
            [HideInInspector] end_lqdmask("", Float) = 0
            [HideInInspector] start_noise("Noise--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<=4}]}}", Float) = 0
                [Enum(R, 0, G, 1, B, 2, A, 3)] _NoiseTexChannelSwitch ("Noise Texture Channel--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<=3}]}}", Float) = 0
                [Enum(R, 0, G, 1, B, 2, A, 3)] _NoiseTexSwitch ("Noise Texture Channel--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 2
                // [Toggle] _NoiseToggle ("Enable Noise--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                [Toggle] _NoiseTexToggle ("Enable Noise Tex--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                [Toggle] _NoiseRandomToggle ("Enable Random Noise--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                [Toggle] _NoiseTexUVRandomToggle ("Enable Random UV --{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0
                _Noise_Tex ("Noise Texture--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", 2D) = "white" { } 
                _NoiseTex ("Noise Texture", 2D) = "white" { }
                _Noise_Uspeed ("Noise X Speed", Float) = 0
                _Noise_Vspeed ("Noise Y Speed", Float) = 0
                _NoiseInt ("Noise Intensity--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _NoiseOffset ("Noise Offset--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _NoiseTex1TillingAdd2Offset ("Noise Texture Tiling Offset--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0

                _Noise_Offset ("Noise Offset--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0.5
                _Noise_Brightness ("Noise Brightness--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_ParticleType>=1},{type:PROPERTY_BOOL,data:_ParticleType<4}]}}", Float) = 0.1
            [HideInInspector] end_noise("", Float) = 0
            [HideInInspector] start_lqdvc ("Vertex Color Toggle--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==2}}", Float) = 0
                [Toggle] _VertexColorForLiquidColorToggle ("Use Vertex Color", Float) = 0
                [Toggle] _VertexRForLiquidOpacityToggle ("Red is Opacity", Float) = 0
            [HideInInspector] end_lqdvc ("", Float) = 0
            [HideInInspector] start_masktwo("Secondary Mask--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _Mask2 ("Mask 2", 2D) = "white" { }
                [Vector2] _Mask2Speed ("Mask 2 Scroll Speed", Vector) = (0,0,0,0)
                [Enum(R, 0, G, 1, B, 2, A, 3)] _Mask2Switch ("Mask 2 Switch", Float) = 1
                _Mask2NoiseInt ("Noise Intensity", Float) = 0
            [HideInInspector] end_masktwo("", Float) = 0
            [HideInInspector] start_highlight("Highlight--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _HighlightTex ("Highlight Texture", 2D) = "white" { }
                _HighlightTex1TillingAdd2Offset ("Texture Tiling Offset", Float) = 1
                [Enum(R, 0, G, 1, B, 2, A, 3)] _HighlightTex_Switch ("HighlightTex_Switch", Float) = 1
                _HighlightSoft ("Softness", Float) = 5
                _HighlightRange ("Range", Float) = 0
                _HighlightTex_Uspeed ("X Speed", Float) = 1
                _HighlightTex_Vspeed ("Y Speed", Float) = 1
                _HighlightTex_LightColor ("Light Color", Float) = 0
                _HighlightTex_DarkColor ("Dark Color", Float) = 0
                [Vector2] _HighlightTex_Mask ("Highlight Texture Mask", Vector) = (0.8,0.5,0,0)
            [HideInInspector] end_highlight("", Float) = 0
            [HideInInspector] start_fire("Fire--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _FireNoiseTex ("Fire Noise Texture", 2D) = "white" { }
                [Enum(R, 0, G, 1, B, 2, A, 3)] _FireNoiseTex_Switch ("Fire Noise Texture Channel", Float) = 0
                [Vector2] _FireNoiseTex_UVspeed ("Fire Noise XY Speed", Vector) = (0,0,0,0)
                _FireNoiseInt ("Noise Intensity", Float) = 10
                _FireTex ("Fire Texture", 2D) = "white" { }
                [HDR] _FireColor ("Fire Color", Color) = (1,0.75,0.75,0)
                [Enum(R, 0, G, 1, B, 2, A, 3)] _FireTex_Switch ("Fire Texture Channel", Float) = 0
                _Fire02_Tex ("Fire 02 Texture", 2D) = "white" { }
                [Vector2] _Fire02Tex_UVspeed ("Fire 02 XY Speed", Vector) = (0,0,0,0)
                [HDR] _Fire02_Color ("Fire 02 Color", Color) = (0,0,0,0)
                [Enum(R, 0, G, 1, B, 2, A, 3)] _Fire02Tex_Switch ("Fire 02 Texture Channel", Float) = 0
                _Fire02_TexMask ("Fire 02 Mask ", Vector) = (0,0.2,0.45,1.1)
                _FireSpeedTime ("Fire Speed Time", Float) = 5
                _FireSpeedInt ("Fire Speed Intensity", Float) = 0
                _EdgeBlurPower ("Edge Blur Power", Float) = 2
                _EdgeBlur ("Edge Blur", Float) = 0.02
                _Switch ("Fire State Switch", Float) = -0.5
            [HideInInspector] end_fire("", Float) = 0
            [HideInInspector] start_ramp("Ramps--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _RampTexWeak ("Weak Ramp", 2D) = "white" { }
                _RampTexStrong ("Strong Ramp", 2D) = "white" { }
                _RampTexWeakStrongLerp ("Ramp Blend Rate", Range(0, 1)) = 0
                _RampTexScale ("Ramp Scale", Float) = 1
                _RampTexBrightness ("Ramp Brightness", Float) = 1
                _RampTexNoiseBrightness ("Noise Brightness", Float) = 0
            [HideInInspector] end_ramp ("", float) = 0
            [HideInInspector] start_dissolve("Dissolve--{condition_show:{type:PROPERTY_BOOL,data:_ParticleType==4}}", Float) = 0
                _DissolveTex ("Dissolve Texture", 2D) = "white" { }
                _DissovleTex1TillingAdd2Offset ("Dissolve Texture Channel", Float) = 1
                [Enum(R, 0, G, 1, B, 2, A, 3)] _DissolveTex_Switch ("Dissolve Texture Channel", Float) = 0
                _DissolveSoft1 ("Dissolve Softness", Float) = 50
                _DissolveRange1 ("Dissolve Range", Float) = -0.05
                [HDR] _OutlineColor ("Edge Color", Color) = (1,1,1,0)
                _OutlineWidth ("Edge Width", Float) = 0.05
                _DissolveTex_Uspeed ("Dissolve X Speed", Float) = 1
                _DissolveTex_Vspeed ("Dissolve Y Speed", Float) = 1
                _DissolveTex_Uoffset ("Dissolve X Offset", Float) = 1
                _DissolveTex_Voffset ("Dissolve Y Offset", Float) = 1
            [HideInInspector] end_dissolve ("", float) = 0
        [HideInInspector] end_main ("Main", float) = 0
        [HideInInspector] start_softparticles ("Soft Particles", float) = 0
            [Toggle] _SOFTPARTICLES ("Enable Soft Particles", Float) = 0
            _DepthThresh ("Thresh", Range(0.001, 20)) = 1
            _DepthFade ("Fade", Range(0.001, 20)) = 1
        [HideInInspector] end_softparticles ("", Float) = 0
        [HideInInspector] start_rendering("Rendering Options", Float) = 0
            [Enum(Normal, 0, Curve, 1)] Emission_Type ("Alpha Control Type", Float) = 0
            // _AlphaCurve ("Alpha Curve", 2D) = "white" { }
            // [Toggle] _CameraFade ("Camera Fade", Float) = 0
            // [Toggle] _EnableCorrectMV ("Correct MV", Float) = 0
            // _MotionVectorsAlphaCutoff ("Motion Vectors Alpha Cutoff", Range(0, 1)) = 0.1
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Src Blend Mode", Float) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Dst Blend Mode", Float) = 0
            [Enum(UnityEngine.Rendering.BlendOp)] _BlendOP ("BlendOp Mode", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
            [Enum(Off, 0, On, 1)] _Zwrite ("ZWrite Mode", Float) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _Ztest ("ZTest Mode", Float) = 4
        [HideInInspector] end_rendering("Rendering Options", Float) = 0

    }
    SubShader
    {
        Tags { "AllowDistortionVectors" = "False" "IGNOREPROJECTOR" = "true" "QUEUE" = "Transparent" "RenderType" = "Transparent" }
        HLSLINCLUDE
            #include "UnityCG.cginc"
            #include "Include/HoyoToonGI-ParticlesImport.hlsl"
            #include "Include/HoyoToonGI-ParticlesDeclaration.hlsl"
            #include "Include/HoyoToonGI-ParticlesCommon.hlsl"
        ENDHLSL
        Pass
        {
            Name "MAIN"
            Tags { "AllowHDRMode" = "true" "CanUseSpriteAtlas" = "true" "HDRMode" = "False" "IGNOREPROJECTOR" = "true" "LIGHTMODE" = "FORWARDBASE" "PreviewType" = "Plane" "QUEUE" = "Transparent" "RenderType" = "Transparent" }
            Blend [_SrcBlendMode] [_DstBlendMode]
            ZTest [_Ztest]
            ZWrite [_Zwrite]
            Cull [_Cull]
            // BlendOp [_BlendOP]
            
            HLSLPROGRAM
            #pragma multi_compile_particles 
            #pragma vertex vert
            #pragma fragment frag
            #include "Include/HoyoToonGI-ParticlesProgram.hlsl"
            ENDHLSL
        }
    }
    
    CustomEditor "HoyoToon.ShaderEditor"
}
