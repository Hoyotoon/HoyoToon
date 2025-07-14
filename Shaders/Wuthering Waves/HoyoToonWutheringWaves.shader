Shader "HoyoToon/Wuthering Waves/Character"
{
    Properties 
  { 
        [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0
        // GUI THINGS
        [HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/wuwalogo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/wuwal", Float) = 0
        [HideInInspector] CharacterRight ("UI/wuwar", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
        [HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
        [HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0

        [HoyoToonShaderOptimizerLockButton] _ShaderOptimizerEnabled ("Lock Material", Float) = 0

        [Enum(Base, 0, Face, 1, Eye, 2, Bangs, 3, Hair, 4, Glass, 5, Tacet Mark, 6)] _MaterialType ("Material Type--{on_value_actions:[
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
            {value:0,actions:[{type:SET_PROPERTY,data:_sdwRef=0}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_sdwColorMask=16}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=1}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRefA=0}, {type:SET_PROPERTY,data:_StencilRefB=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_sdwRef=20}, {type:SET_PROPERTY,data:_sdwPass=2}, {type:SET_PROPERTY,data:_sdwComp=7}, {type:SET_PROPERTY,data:_sdwColorMask=0}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=0}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRefA=25}, {type:SET_PROPERTY,data:_StencilRefB=26}, {type:SET_PROPERTY,data:render_queue=2010}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:2,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_sdwRef=0}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_sdwColorMask=0}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=1}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRefA=25}, {type:SET_PROPERTY,data:_StencilRefB=26}, {type:SET_PROPERTY,data:render_queue=2012}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:3,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_sdwRef=20}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=3}, {type:SET_PROPERTY,data:_sdwColorMask=16}, {type:SET_PROPERTY,data:_sdwSrc=2}, {type:SET_PROPERTY,data:_sdwDst=3}, {type:SET_PROPERTY,data:_sdwZWrite=0}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRefA=25}, {type:SET_PROPERTY,data:_StencilRefB=27}, {type:SET_PROPERTY,data:render_queue=2020}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:4,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
            {value:4,actions:[{type:SET_PROPERTY,data:_sdwRef=0}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_sdwColorMask=16}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=1}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:4,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:5,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
            {value:5,actions:[{type:SET_PROPERTY,data:_sdwRef=0}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_sdwColorMask=16}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=1}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:5,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2060}, {type:SET_PROPERTY,data:render_type=Opaque}]}, 

            {value:6,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
            {value:6,actions:[{type:SET_PROPERTY,data:_sdwRef=0}, {type:SET_PROPERTY,data:_sdwPass=0}, {type:SET_PROPERTY,data:_sdwComp=8}, {type:SET_PROPERTY,data:_sdwColorMask=16}, {type:SET_PROPERTY,data:_sdwSrc=1}, {type:SET_PROPERTY,data:_sdwDst=1}, {type:SET_PROPERTY,data:_sdwZWrite=1}, {type:SET_PROPERTY,data:_sdwZTest=2}]},
            {value:6,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2060}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Float) = 0
        [HideInInspector] start_main ("Main", Float) = 0
            [Toggle] _MultiLight ("Enable Multi Light Source Mode", float) = 1
            [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1
            [HideInInspector] start_coretex ("Main Textures", Float) = 0
            [HideInInspector] start_diff ("Diffuse Texture", Float) = 0
                _MainTex ("Diffuse Texture", 2D) = "white" {}
                [Toggle] _UseMainTexA ("Alpha is Toon Mask", Float) = 0
                [Toggle] _UseTranslucent ("Alpha is Translucent", Float) = 0
            [HideInInspector]  end_diff ("", Float) = 0
            [HideInInspector] start_mask ("Type Mask", Float) = 0
                _MaskTex ("Mask", 2D) = "grey" {}
                [Toggle] _UseSDFShadow ("Use SDF Shadow", Float) = 0
            [HideInInspector] end_mask ("", Float) = 0
            [HideInInspector] start_type ("ID Mask", Float) = 0
                _TypeMask ("Type Mask (ID)", 2D) = "grey" {}
                [Toggle] _UseSkinMask ("Skin Mask Enable", Float) = 0
                [Toggle] _UseRampMask ("Ramp Mask Enable", Float) = 0
            [HideInInspector] end_type ("", Float) = 0
            [HideInInspector] start_nrm("Normal|Roughness|Metal", Float) = 0
                _Normal_Roughness_Metallic ("Normal Map(RG)|Roughness(B)|Metallic(G)", 2D) = "bump" {}
            [HideInInspector] end_nrm("", Float) = 0
            [HideInInspector] end_coretex ("", Float) = 0
            [HideInInspector] start_coloring ("Colors", Float) = 0
                _BaseColor ("Base Color", Color) = (1,1,1,1)
                _SkinColor ("Skin Color", Color) = (1,1,1,1)
                _SubsurfaceColor ("Subsurface Color", Color) = (0.5,0.5,0.5,1)
                _SkinSubsurfaceColor ("Skin Subsurface Color", Color) = (0.9387,0.6038,0.4072,1.0)
            [HideInInspector] end_coloring ("", Float) = 0
            [HideInInspector] start_facingdirection ("Facing Direction", Float) = 0
                _headUpVector ("Up Vector | XYZ", Vector) = (0, 1, 0, 0)
                _headForwardVector ("Forward Vector | XYZ", Vector) = (0, 0, 1, 0)
                _headRightVector ("Right Vector | XYZ ", Vector) = (-1, 0, 0, 0)
            [HideInInspector] end_facingdirection ("", Float) = 0
        [HideInInspector] end_main ("", Float) = 0

        [HideInInspector] start_bump("Normal|Roughness|Metal", Float) = 0
            [Toggle] _UseNormalMap ("Enable Bump Mapping", Float) = 0
            [Toggle] _NormalFlip ("Enable Bump Mapping", Float) = 0
            _NormalStrength ("Bump Strength", Float) = 1
        [HideInInspector] end_bump("", Float) = 0

        [HideInInspector] start_spec("Specular", Float) = 0
            _ToonMaxSpecular ("Specular Sharpness", Float) = 0.1
            _SpecularPower ("Specular Power", Float) = 1
            _SpecStrength ("Specular Strength", Float) = 0.2 
            [HideInInspector] start_matcap ("Metal MatCap", Float) = 0
                _MetalSpecularPower ("Metal Specular Power", Float) = 1
                _MatCapTex ("MatCap Texture", 2D) = "black" {}
                _MetalMatCapBack ("Metal MatCap Back Intensity", Float) = 1
                _MatCapInt ("MatCap Intensity", Float) = 1
                _MetalMatCapInt ("Metal MatCap Intensity", Float) = 1
            [HideInInspector] end_matcap ("", Float) = 0
        [HideInInspector] end_spec("", Float) = 0

        [HideInInspector] start_rim ("Rim Light", Float) = 0
            [Toggle] _EnableRimLight ("Use Rim Lighting", Float) = 1
            _RimWidth ("Rim Width", Float) = 1
            _RimHardness ("Rim Hardness", Float) = 1
            _RimColor ("Rim Color", Color) = (0.5,0.5,0.5,1)
        [HideInInspector] end_rim ("", Float) = 0

        [HideInInspector] start_stock ("Stocking", Float) = 0
            [Toggle] _UseStocking ("Enable Stocking", Float) = 0
            _StockingIntensity ("Stocking Intensity", Float) = 1.0
            [HideInInspector] start_anistropy ("Anisotropic", Float) = 0
                _AnistropyColor ("Anisotropic Highlight Color", Color) = (0.0139, 0.0139, 0.0139, 1.0)
                _AnistropyInt ("Anisotropic Intensity", Float) = 1
                _AnistropyNormalInt ("Anisotropic Normal Intensity", Float) = 1
            [HideInInspector] end_anistropy ("", Float) = 0
            [HideInInspector] start_scolor ("Colors", Float) = 0
                _StockingLightColor ("Stocking Light Color", Color) = (0.0139, 0.0139, 0.0139, 1.0)
                _StockingEdgeColor ("Stocking Edge Color", Color) = (0.609,0.542,0.596,1.0)
                _StockingColor ("Stocking Color", Color) = (0.731,0.689,0.739,1.0)
            [HideInInspector] end_scolor ("", Float) = 0
            [HideInInspector] start_knee ("Knee Settings", Float) = 0 
                _Stocking_KneeSkinIntensityOffset ("Knee Skin Intensity Offset", Float) = 0.1
                _Stocking_KneeSkinRangeOffset ("Knee Skin Range Offset", Float) = 2
            [HideInInspector] end_knee ("", Float) = 0
            [HideInInspector] start_ranges ("Ranges", float) = 0
                _StockingLightRangeMax ("Stocking Light Range Max", Float) = 1
                _StockingLightRangeMin ("Stocking Light Range Min", Float) = 0.4
                _StockingRangeMax ("Stocking Range Max", Float) = 1.0
                _StockingRangeMiddle ("Stocking Range Middle", Float) = 0.48
                _StockingRangeMin ("Stocking Range Min", Float) = 0.4
                _StockingSkinRange ("Stocking Skin Range", Float) = 6
            [HideInInspector] end_ranges ("", float) = 0
        [HideInInspector] end_stock ("Stocking", Float) = 0

        [HideInInspector] start_eye ("Eye", Float) = 0
            [Toggle] _UseHeightLightShape ("Use Highlight Shape", Float) = 0
            [Toggle] _UseEyeSDF ("Use Eye SDF", Float) = 0
            _RotateAngle ("Rotation Angle", Float) = 0.11
            _EyeScale ("Eye Scale", Float) = 0.8
            _HeightRatioInput ("Highlight Ratio", Float) = 0.4
            [HideInInspector] start_eyetex ("Eye", Float) = 0
                _HeightLightMap ("Highlight Map", 2D) = "black" {}
                _EM ("Highlight EM Map", 2D) = "black" {}
            [HideInInspector] end_eyetex ("", Float) = 0
            [HideInInspector] start_hlight ("Highlight", Float) = 0
                [HideInInspector] start_eyeshake ("Eye Shake", Float) = 0
                    _LightShakeScale ("Light Shake Scale", Float) = 0.01
                    _LightShakeSpeed ("Light Shake Speed", Float) = 10
                    _LightShakPositionX ("Light Shake X", Float) = 0.5
                    _LightShakPositionY ("Light Shake Y", Float) = 0.5
                [HideInInspector] end_eyeshake ("", Float) = 0
                [HideInInspector] start_lightpos ("Light Positions", Float) = 0
                    _SecondLight_PositionX ("Secont Light Position X", Float) = 0.5
                    _SecondLight_PositionY ("Secont Light Position Y", Float) = 0.5
                    _LightPositionX ("Light Position X", Float) = 0.5
                    _LightPositionY ("Light Position Y", Float) = 0.5
                [HideInInspector] end_lightpos ("", Float) = 0
                [HideInInspector] start_aight ("Height Light", Float) = 0
                    _HeightLight_PositionX ("Highlight Pos X", Float) = 0.54
                    _HeightLight_PositionY ("Highlight Pos Y", Float) = 0.58 
                    _HeightLight_WidthX ("Highlight Width X", Float) = 1.71
                    _HeightLight_WidthY ("HighLight Width Y", Float) = 1.03
                [HideInInspector] end_aight ("", Float) = 0
            [HideInInspector] end_hlight ("", Float) = 0
            [HideInInspector] start_parallax ("Parallax", Float) = 0
                _ParallaxSteps ("Step Count", Int) = 25 
                _ParallaxHeight ("Parallax Height", Float) = 0.2
            [HideInInspector] end_parallax ("", Float) = 0
        [HideInInspector] end_eye ("", Float) = 0

        // not deleting this as i need it for the next update for the shader
        // [HideInInspector] start_glass ("Glass", Float) = 0
        //     _HeightLightTex ("Highlight Tex", 2D) = "black" {}
        // [HideInInspector] end_glass ("", Float) = 0
        

        [HideInInspector] start_shadow ("Shadow", Float) = 0
            _ShadowProcess ("Shadow Process", Float) = 0.55
            _BackShadowProcessOffset ("Back Shadow Offset", Float) = -0.1
            _FrontShadowProcessOffset ("Front Shadow Offset", Float) = 0.4
            _ShadowWidth ("Shadow Width", Float) = 0.01
            _ShadowOffsetPower ("Shadow Offset Power", Float) = 0.56
            _MaskShadowOffsetStrength ("Mask Shadow Offset Strength", Float) = .42
            [Toggle] _UseSelfShadow ("Use Self Shadow", Float) = 1
            [HideInInspector] start_solidshadow("Solid Shadow Settings", Float) = 0
                _SolidShadowWidth ("Solid Shadow Width", Float) = 0.9
                _SolidShadowStrength ("Solid Shadow Strength", Float) = 1
                _SolidShadowProcess ("Solid Shadow Process", Float) = 0.1
            [HideInInspector] end_solidshadow("", Float) = 0
            [HideInInspector] start_ramp ("Shadow Ramp", Float) = 0
                [Toggle] _UseRampColor ("Use Shadow Ramp", Float) = 0
                _Ramp ("Shadow Ramp", 2D) = "white" {}
                _RampPosition ("Ramp Position", Float) = 0.5
                _RampProcess ("Ramp Process", Float) = 0.5 
                _RampWidth ("Ramp Width", Float) = 0.1
                _RampInt ("Ramp Intensity", Float) = 0.3
            [HideInInspector] end_ramp ("", Float) = 0
            [Helpbox] _HairShadowHelp ("The Hair Shadow is a separate feature from the Self Shadows, Self shadows will not be applied to the face but the Hair Shadow will be applied to it. You need to apply it to both the face and bangs materials for it to work.", float) = 0
            [HideInInspector] start_hair ("Hair Shadow", Float) = 0
                [Toggle] _EnableHairShadow ("Enable Hair Shadow", Float) = 0
                _HairShadowColor("Hair Subsurface Color", Color) = (0.938686,0.603828,0.40724,1.0)
                [HideInInspector] start_shadowstencil ("Stencil Settings", Float) = 0
                    [IntRange] _sdwRef ("Stenci l Reference Value", Range(0, 255)) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _sdwComp ("Stencil Compare Function", Float) = 8
                    [Enum(UnityEngine.Rendering.StencilOp)] _sdwPass ("Stencil Pass Op", Float) = 0
                    [IntRange] _sdwColorMask ("Stencil Color Mask", Range(0, 16)) = 16
                    [Enum(UnityEngine.Rendering.BlendMode)] _sdwSrc ("Source Blend", Int) = 1
                    [Enum(UnityEngine.Rendering.BlendMode)] _sdwDst ("Destination Blend", Int) = 1
                    [Enum(Off, 0, On, 1)] _sdwZWrite ("Zwrite", Int) = 1
                    [Enum(UnityEngine.Rendering.CompareFunction)] _sdwZTest ("ZTest", Float) = 2 // mihoyo uses this in their shader but set it to something that doesnt work 
                    [HideInInspector] end_shadowstenci ("", Float) = 0
            [HideInInspector] end_hair ("", Float) = 0
        [HideInInspector] end_shadow ("", Float) = 0
    
        //ifex _Outline == 0
        [HideInInspector] start_outline ("Outline", Float) = 0
            _OutlineTexture ("Outline Texture", 2D) = "white" {}
            [Enum(Off, 0, Tangent, 1, Normal, 2)] _Outline ("Outline Type", Float) = 1
            [Toggle] _UseMainTex ("Use Outline Texture", Float) = 1
            _OutlineWidth ("Outline Width", Float) = 0.11
            [Toggle] _UseVertexGreen_OutlineWidth ("Use Vertex Green as Width", Float) = 0
            [Toggle] _UseVertexColorB_InnerOutline ("Vertex B is Inner Range", Float) = 0
            _OutlineColor ("Outline Color", Color) = (0.765, 0.765, 0.765, 1.0)
        [HideInInspector] end_outline ("", Float) = 0
        //endex

        [HideInInspector] start_special ("Special Effects", Float) = 0
            [HideInInspector] start_emission ("Emission", Float) = 0
                [Toggle] _UseBreathLight ("Use Emission", Float) = 0
                _EmissionBreathThreshold ("Emission Texture Threshold", float) = 0.9
                _EmissionColor ("Emission Color", Color) = (1,1,1,1)
                _EmissionStrength ("Emission Strength", Float) = 1
            [HideInInspector] end_emission ("", Float) = 0
            //ifex _EnabelStencil == 0
            [HideInInspector] start_stencil ("Stencil", Float) = 0
                [Toggle] _EnabelStencil ("Enable Stencil", Float) = 1 
                [Toggle] _AlphaStencil ("Use Transparency in Stencil", Float) = 1
                _Mask ("Stencil Mask", 2D) = "white" {}
                [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassA ("Stencil Pass Op A", Float) = 0
                [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassB ("Stencil Pass Op B", Float) = 0
                [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
                [IntRange] _StencilRefA ("Stencil Reference Value", Range(0, 255)) = 0
                [IntRange] _StencilRefB ("Stencil Reference Value", Range(0, 255)) = 0
            [HideInInspector] end_stencil ("", Float) = 0
            //endex
            [HideInInspector] start_tacet ("Tacet Mark", Float) = 0
                [Toggle] _UseXingHen ("Enable Tacet Mark Decal", Float) = 0
                [Enum(Middle, 0, Left, 1, Right, 2)] _MaskingSide ("Tacet Mark Appears on Side", Float) = 0
                _XingHenControl ("Tacet Mark Control", Float) = 0
                _XingHenPosition ("Tacet Mark Position", Float) = 1
                _D ("Tacet Mark", 2D) = "white" {}
                // _SDF ("Tacet Mark SDF", 2D) = "white" {} // not sure if unused or if just not in this particular capture
                _Noise ("Tacet Mark Noise", 2D) = "white" {}
                _Noise02 ("Tacet Mark Noise 02", 2D) = "white" {}
                // _ShakeNoise ("Tacet Mark Shake Noise", 2D) = "white" {} // // not sure if unused or if just not in this particular capture
                _SDFStart ("Tacet Mark SDF Start", Range(-1, -0.01)) = -0.2
                _SDFColor ("Tacet Mark SDF Color", Color) = (0.0, 0.0, 0.0,1)
                [HideInInspector] start_tacetuv ("Tacet UV", Float) = 0
                    [Toggle] _UseRotate180UV ("Tacet Mark UV Rotation", Float) = 0
                    _RotationAngle ("Tacet Mark UV Rotation Angle", Float) = 0
                    _UVpositionX ("Tacet Mark UV Position X", Float) = 0
                    _UVpositionY ("Tacet Mark UV Position Y", Float) = 0
                    _UVScaleX ("Tacet Mark UV Scale X", Float) = 1
                    _UVScaleY ("Tacet Mark UV Scale Y", Float) = 1
                [HideInInspector] end_tacetuv ("", Float) = 0
                [HideInInspector] start_soundwave ("Sound Wave", Float) = 0
                    _SoundWaveSpeed01 ("Sound Wave Speed 01", Float) = -0.2
                    _SoundWaveTiling01 ("Sound Wave Tiling 01", Float) = 3.8
                    _SoundWaveSpeed02 ("Sound Wave Speed 02", Float) = -0.57
                    _SoundWaveTiling02 ("Sound Wave Tiling 02", Float) = 7.8
                [HideInInspector] end_soundwave ("", Float) = 0
             
            [HideInInspector] end_tacet ("", Float) = 0

            [HideInInspector] start_uvwave("Star Aurora", Float) = 0
                [Toggle] _UseStarrySky ("Enable Star Aurora", Float) = 0
                _AuroraAmount ("Aurora Amount", Float) = 3.0
                [HideInInspector] start_secondrgb ("Secondary Diffuse", float) = 0
                    _Second_RGB ("Secondary Diffuse", 2D) = "white" {}
                    _Second_NoiseStrength ("Secondary Noise Strength", Float) = 0.15
                    _Second_Height ("Secondary Height", Float) = -0.4
                    _Second_HeightRadio ("Secondary Height Ratio", Float) = 0.6
                [HideInInspector] end_secondrgb ("", float) = 0
                [HideInInspector] start_starrynoise ("Noise", Float) = 0
                    [Toggle] _UseNoise ("Enable Noise", Float) = 0
                    _CommonNoiseMap ("Common Noise Map", 2D) = "white" {}
                [HideInInspector] end_starrynoise ("", Float) = 0
                [HideInInspector] start_wave ("Wave", Float) = 0
                    _WaveAmount ("Wave Amount", Float) = 0.8
                    _WaveNormalAmount ("Wave Normal Amount", Float) = 1.2
                    _WaveSpeed  ("Wave Speed", Float) = 0.3
                    _WaveTiling ("Wave Tiling", Float) = 5.5
                [HideInInspector] end_wave ("", Float) = 0
                
            [HideInInspector] end_uvwave("", Float) = 0

            [HideInInspector] start_heightlight ("Height Light", Float) = 0
                [Toggle] _UseUVGradient_DYN ("Use Gradient", Float) = 0
                _UvGradientScale ("Gradient Scale", Float) = 1.0
                _UvGradientProcess ("Gradient Process", Float) = 0.5
                _UVGradientIn ("Gradient Intensity", Float) = 0
                _UVSaturationIntensity ("Gradient Saturation Intensity", Float) = 1.0
                _UVGradientColor ("Gradient Color", Color) = (1,1,1,1)
            [HideInInspector] end_heightlight ("", Float) = 0

            [HideInInspector] start_tonemapping("Tone Mapping", Float) = 0
                [Toggle] _UseToneMapping ("Enable Tone Mapping", Float) = 0
                _Lut2DTex ("LUT Texture", 2D) = "white" {}
            [HideInInspector] end_tonemapping ("", Float) = 0
        [HideInInspector] end_special ("", Float) = 0

        [HideInInspector] start_renderingOptions("Rendering Options", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
            [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Int) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Int) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Int) = 0
            //ifex _DebugMode == 0
            [HideInInspector] start_debugOptions("Debug--{reference_property:_DebugMode}", Float) = 0
                [Toggle] _DebugMode ("Enable Debug Mode", float) = 0
                [Enum(Off, 0, RGB, 1, A, 2)] _DebugDiffuse("Diffuse Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugMaskTex ("Mask Tex Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugTypeMask ("Type Mask Debug Mode", Float) = 0
                [Enum(Off, 0, Bump(RG), 1, Blue, 2, Alpha, 3)] _DebugNormalMap ("Normal Map Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugVertexColor ("Vertex Color Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugMask ("Stencil Mask Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugRimLight ("Rim Light Debug Mode", Float) = 0
                [Enum(Off, 0, Original (Encoded), 1, Original (Raw), 2, Bumped (Encoded), 3, Bumped (Raw), 4)] _DebugNormalVector ("Normals Debug Mode", Float) = 0 
                [Enum(Off, 0, On, 1)] _DebugTangent ("Tangents/Secondary Normal Debug Mode", Float) = 0
                [Enum(Off, 0, Original Form, 1, Metal Shading Form, 2)] _DebugMatcap ("Matcap Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugSpecular ("Specular Debug Mode", Float) = 0
                [Enum(Off, 0, Area, 1, Color, 2)] _DebugShadow ("Shadow Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugStocking ("Stocking Debug Mode", Float) = 0
                [Enum(Off, 0, Forward, 1, Right, 2)] _DebugFaceVector ("Facing Vector Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugLights ("Lights Debug Mode", Float) = 0
            [HideInInspector] end_debugOptions("Debug", Float) = 0
            //endex
        [HideInInspector] end_renderingOptions("", Float) = 0
        
    }
    SubShader
    {
        
        Tags { "RenderType"="Opaque" }
        HLSLINCLUDE

        //ifex _UseBreathLight == 0
            #define use_emission
        //endex
        
        //ifex _EnableRimLight == 0
            #define use_rim
        //endex
        //ifex _MaterialType != 1
            #define is_face
        //endex
        //ifex _MaterialType != 2
            #define is_eyes
        //endex
        //ifex _MaterialType != 3
            #define is_bangs
        //endex
        //ifex _MaterialType != 4
            #define is_hair
        //endex
        //ifex _MaterialType != 5
            #define is_glass
        //endex
        //ifex _MaterialType != 6
            #define is_tacet
        //endex
        //ifex _UseNormalMap == 0
            #define use_normal
        //endex
        //ifex _UseStocking == 0
            #define use_stocking
        //endex
        //ifex _DebugMode == 0
            #define can_debug
        //endex

        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityShaderVariables.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "UnityInstancing.cginc"
        #include "include/HoyoToonWutheringWaves-declaration.hlsl"
        #include "include/HoyoToonWutheringWaves-common.hlsl"
        #include "include/HoyoToonWutheringWaves-input.hlsl"
        ENDHLSL

        //ifex _EnableHairShadow == 0
        Pass
        {
            Name "Stencil Shadow"
            Tags { "LightMode" = "ForwardBase" }
            Blend [_sdwSrcd] [_sdwDst]
            ZWrite [_sdwZWrite]
            ZTest [_sdwZTest]
            ColorMask [_sdwColorMask]
            Stencil
            {
				ref [_sdwRef]
                Comp [_sdwComp]
				Pass [_sdwPass]
            }
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_SHADOW
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "include/HoyoToonWutheringWaves-program.hlsl"
            ENDHLSL
        }
        //endex

        Pass
        {
            Name "Character"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
                ref [_StencilRefA]
        		Comp [_StencilCompA]
        		Pass [_StencilPassA]
        	}
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "include/HoyoToonWutheringWaves-program.hlsl"
            ENDHLSL
        }

        //ifex _EnabelStencil == 0
        Pass
        {
            Name "Stencil"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Back  
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                ref [_StencilRefB]             
        		Comp [_StencilCompB]
        		Pass [_StencilPassB]
        	}
            HLSLPROGRAM
            #define is_stencil
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "include/HoyoToonWutheringWaves-program.hlsl"
            ENDHLSL
        }
        //endex
        
        //ifex _MultiLight == 0
        Pass // Character Light Pass
        {
            Name "Character Light Pass"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_Cull]
            ZWrite Off
            Blend One One     
               
            HLSLPROGRAM
            
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT

            #pragma vertex vs_model
            #pragma fragment ps_model 

            #include "include/HoyoToonWutheringWaves-program.hlsl"
            ENDHLSL
        }    
        //endex

        //ifex _Outline == 0 || _MaterialType >= 5 || _MaterialType == 2
        Pass
        {
            Name "Outline"
            Tags{ "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            Stencil
            {
                ref 255
                Comp Always
                Pass Keep
            }	
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex vs_edge
            #pragma fragment ps_edge
            #include "include/HoyoToonWutheringWaves-program.hlsl"
            ENDHLSL
        }
        //endex
        
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    CustomEditor "HoyoToon.ShaderEditor"
}
