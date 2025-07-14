Shader "HoyoToon/Genshin/Character"
{
    Properties 
  { 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
        //Header
        //[HideInInspector] shader_master_label ("✧<b><i><color=#C69ECE>HoyoToon Genshin Impact</color></i></b>✧", Float) = 0
		[HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/gilogo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/gil", Float) = 0
        [HideInInspector] CharacterRight ("UI/gir", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
		[HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
		[HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        //Header End

        [HoyoToonShaderOptimizerLockButton] _ShaderOptimizerEnabled ("Lock Material", Float) = 0

        //Material Type
        [HoyoToonWideEnum(Base, 0, Face, 1, Weapon, 2, Glass, 3, Bangs, 4)]variant_selector("Material Type--{on_value_actions:[
            {value:0,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=1.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=1.0}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=1.0}]},
            {value:4,actions:[{type:SET_PROPERTY,data:_UseFaceMapNew=0.0}, {type:SET_PROPERTY,data:_UseWeapon=0.0}]}
            ]}", Int) = 0
        //Material Type End

        // Hidden Game Version Variable for switching certain logics
        [HideInInspector] [HoyoToonWideEnum(Pre Natlan, 0, Post Natlan, 1)] _gameVersion ("", Float) = 0
        [HideInInspector] [Toggle] _IsDevMode ("Dev Mode", Float) = 0
        
        //Main
        [HideInInspector] start_main ("Main", Float) = 0
            [SmallTexture]_MainTex("Diffuse Texture",2D)= "white" { }
            [SmallTexture]_LightMapTex("Light Map Texture", 2D) = "grey" {}
            [Toggle] _DrawBackFace ("Turn On Back Face", Float) = 0 // need to make this turn off backface culling
            [Enum(UV0, 0, UV1, 1)] _UseBackFaceUV2("Backface UV", int) = 1.0
            [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1 // because VRC world creators are fucking awful at lighting you need to do shit like this to not blow your models the fuck up
            // on by default >:(
            [Toggle] _MainTexColoring("Enable Material Tinting", Float) = 0
            [Toggle] _DisableColors("Disable Material Colors", Float) = 0    

            [HideInInspector] start_facingvector ("Facing Vectors", Float) = 0
                _headUpVector ("Up Vector | XYZ", Vector) = (0, 1, 0, 0)
                _headForwardVector ("Forward Vector | XYZ", Vector) = (0, 0, 1, 0)
                _headRightVector ("Right Vector | XYZ", Vector) = (-1, 0, 0, 0)
            [HideInInspector] end_facingvector("", Float) = 0
            // Main Color Tinting
            [HideInInspector] start_maincolor ("Color Options", Float) = 0
                // Color Mask
                //ifex _UseMaterialMasksTex == 0
                [HideInInspector] start_colormask ("Color Mask--{reference_property:_UseMaterialMasksTex}", Float) = 0
                    [Toggle] _UseMaterialMasksTex("Enable Material Color Mask", Int) = 0
                    [SmallTexture] _MaterialMasksTex ("Material Color Mask--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterialMasksTex==1.0}}", 2D) = "white"{ }
                [HideInInspector] end_colormask ("", Float) = 0
                //endex
                //ifex _MainTexColoring == 0
                // Tint and Colors
                [HDR]_MainTexTintColor ("Main Texture Tint Colors", Color) = (0.5, 0.5, 0.5, 1.0)
                //endex
                //ifex _DisableColors == 1
                _Color ("Tint Color 1", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color2 ("Tint Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color3 ("Tint Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color4 ("Tint Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                _Color5 ("Tint Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                //endex
            [HideInInspector] end_maincolor ("", Float) = 0
            // Main Alpha
            [HideInInspector] start_mainalpha ("Alpha Options", Float) = 0
                    [Helpbox] _MainTexAlphaUseHelp("Be careful: Changing these values will reset your render queue value as well both the Source and Destination Blend values.", float) = 0
                    [HoyoToonWideEnum(Off, 0, AlphaTest, 1, Glow, 2, FaceBlush, 3, Transparency, 4)] _MainTexAlphaUse("Diffuse Alpha Channel--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_SrcBlend=1},{type:SET_PROPERTY,data:_DstBlend=0},{type:SET_PROPERTY,data:render_queue=2000}]},
                    {value:4,actions:[{type:SET_PROPERTY,data:_SrcBlend=5},{type:SET_PROPERTY,data:_DstBlend=10},{type:SET_PROPERTY,data:render_queue=2225}]}]}", Int) = 0
                // _TestValue ("waa", Float) = 0
                _MainTexAlphaCutoff("Alpha Cuttoff--{condition_show:{type:PROPERTY_BOOL,data:_MainTexAlphaUse==1.0}}", Range(0, 1.0)) = 0.5
                [Toggle] _EnableDithering ("Alpha Dithering", Float) = 0
                [HideInInspector] end_mainalpha ("", Float) = 0
            // Detail Line
            //ifex _TextureLineUse == 0
            [HideInInspector] start_maindetail ("Details--{reference_property:_TextureLineUse}", Float) = 0
                [Toggle] _TextureLineUse ("Texture Line", Float) = 0.0
                _TextureLineSmoothness ("Texture Line Smoothness", Range(0.0, 1.0)) = 0.15
                _TextureLineThickness ("Texture Line Thickness", Range(0.0, 1.0)) = 0.55
                _TextureLineDistanceControl ("Texture Line Distance Control", Vector) = (0.1, 0.6, 1.0, 1.0)
                [HDR] _TextureLineMultiplier ("Texture Line Color", Color) = (0.6, 0.6, 0.6, 1.0)
            [HideInInspector] end_maindetail ("", Float) = 0
            //endex
            // Material ID
            [HideInInspector] start_matid ("Material IDs", Float) = 0
                [Toggle] _UseMaterial2 ("Enable Material 2", Float) = 1.0
                [Toggle] _UseMaterial3 ("Enable Material 3", Float) = 1.0
                [Toggle] _UseMaterial4 ("Enable Material 4", Float) = 1.0
                [Toggle] _UseMaterial5 ("Enable Material 5", Float) = 1.0
            [HideInInspector] end_matid ("", Float) = 0
        [HideInInspector] end_main ("", Float) = 0
        //Main End

        //Normal Map
        //ifex _UseBumpMap == 0
        [HideInInspector] start_normalmap ("Normal Map--{reference_property:_UseBumpMap}", Float) = 0
            [Toggle] _UseBumpMap("Normal Map", Float) = 0.0
            [HideInInspector] [Toggle] _DummyFixedForNormal ("kms", Float) = 0
            [HideInInspector] [Toggle] _isNativeMainNormal ("", Float) = 0
            [SmallTexture]_BumpMap("Normal Map",2D)= "bump" { } 
            _BumpScale ("Normal Map Scale", Range(0.0, 1.0)) = 0.0
        [HideInInspector] end_normalmap ("", Float) = 0
        //Normal Map End
        //endex

        //Face Shading
        //ifex _UseFaceMapNew == 0 && variant_selector != 1
        [HideInInspector] start_faceshading("Face--{condition_show:{type:PROPERTY_BOOL,data:_UseFaceMapNew==1.0}} ", Float) = 0
            _FaceMapRotateOffset ("Face Map Rotate Offset", Range(-1, 1)) = 0
            [SmallTexture] _FaceMapTex ("Face Shadow",2D)= "white"{ }
            [HideInInspector] _UseFaceMapNew ("Enable Face Shader", Float) = 0.0
            _FaceMapSoftness ("Face Lighting Softness", Range(0.0, 1.0)) = 0.001

            [Toggle] _UseFaceBlueAsAO ("Use LightMap Blue Channel as AO", Float) = 0.0            
            // Face Bloom
            [HideInInspector] start_faceblush ("Blush", Float) = 0
                _FaceBlushStrength ("Face Blush Strength", Range(0.0, 1.0)) = 0.0
                _FaceBlushColor ("Face Blush Color", Color) = (1.0, 0.8, 0.7, 1.0)
            [HideInInspector] end_faceblush ("", Float) = 0
        [HideInInspector] end_faceshading ("", Float) = 0
        //Face Shading End
        //endex

        //ifex _UseWeapon == 0
        //Weapon Shading
        [HideInInspector] start_weaponshading("Weapon--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0},reference_property:_UseWeapon}", Float) = 0
            [Toggle] [HideInInspector]  _UseWeapon ("Weapon Shader", Float) = 0.0
            [Toggle] _UsePattern ("Enable Weapon Pattern", Float) = 1.0
            [Toggle] _ProceduralUVs ("Disable UV1", Float) = 0.0
            [SmallTexture]_WeaponDissolveTex("Weapon Dissolve",2D)= "white"{ }
            [SmallTexture]_WeaponPatternTex("Weapon Pattern",2D)= "white"{ }
            [SmallTexture]_ScanPatternTex("Scan Pattern",2D)= "black"{ }
            _WeaponDissolveValue ("Weapon Dissolve Value", Range(0.0, 1.0)) = 1.0
            [Toggle] _DissolveDirection_Toggle ("Dissolve Direction Toggle", Float) = 0.0
            [HDR] _WeaponPatternColor ("Weapon Pattern Color", Color) = (1.682, 1.568729, 0.6554853, 1.0)
            _Pattern_Speed ("Pattern Speed", Float) = -0.033
            _SkillEmisssionPower ("Skill Emisssion Power", Float) = 0.6
            _SkillEmisssionColor ("Skill Emisssion Color", Color) = (0.0, 0.0, 0.0, 0.0)
            _SkillEmissionScaler ("Skill Emission Scaler", Float) = 3.2
            // Weapon Scan
            [HideInInspector] start_weaponscan ("Scan", Float) = 0
                _ScanColorScaler ("Scan Color Scaler", Float) = 0.0
                _ScanColor ("Scan Color", Color) = (0.8970588, 0.8970588, 0.8970588, 1.0)
                [Toggle] _ScanDirection_Switch ("Scan Direction Switch", Float) = 0.0
                _ScanSpeed ("Scan Speed", Float) = 0.8
            [HideInInspector] end_weaponscan ("", Float) = 0
        [HideInInspector] end_weaponshading ("", Float) = 0
        //Weapon Shading End
        //endex

        //ifex _UseGlassSpecularToggle == 0
        [HideInInspector] start_parallaxglass("Glass--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==3.0},reference_property:_UseGlassSpecularToggle}", Float) = 0
            [Toggle] _UseGlassSpecularToggle ("Use Glass Specular", Float) = 0
            _GlassSpecularTex ("Glass Specular Texture", 2D) = "black" {}
            _GlassTiling ("Tiling", Float) = 1.6
            _MainColor ("Color", Color) = (1,1,1,1)
            _MainColorScaler ("Scaler", Float) = 1
            [HideInInspector] start_glassspecular ("Specular", Float) = 0
                _GlassSpecularOffset ("Specular Offset", Range(-5, 5)) = 3
                _GlassSpecularColor ("Specular Color", Color) = (1,1,1,1)
                _GlasspecularLength ("Specular Length", Range(0, 1)) = 0.2
                _GlasspecularLengthRange ("Specular Length Range", Range(0, 1)) = 0.1
            [HideInInspector] end_glassspecular ("Specular", Float) = 0
            [HideInInspector] start_glassthickness ("Thickness (Fresnel)", Float) = 0
                _GlassThicknessColor ("Thickness Color", Color) = (0,0,0,0)
                _GlassThickness ("Thickness Power", Range(1, 10)) = 4
                _GlassThicknessScale ("Thickness Scale", Range(0, 5)) = 1.5
            [HideInInspector] end_glassthickness  ("Thickness", Float) = 0
            [HideInInspector] start_glassdetail ("Detail", Float) = 0
                _GlassSpecularDetailColor ("Detail Color", Color) = (0,0,0,0)
                _GlassSpecularDetailOffset ("Detail Offset", Range(-1, 1)) = 0
                _GlassSpecularDetailLength ("Detail Length", Range(0, 1)) = 0.2
                _GlassSpecularDetailLengthRange ("Detail Length Range", Range(0, 1)) = 0.1
            [HideInInspector] end_glassdetail  ("Detail", Float) = 0
        [HideInInspector] end_parallaxglass("", Float) = 0
        //endex

        //Lightning Options
        [HideInInspector] start_lighting("Lighting Options", Float) = 0
            //ifex _EnableShadow == 0 
            [HideInInspector] start_lightandshadow("Shadow--{reference_property:_EnableShadow}", Float) = 0
                [Toggle] _EnableShadow ("Enable Shadow", Float) = 1
                [Toggle] _EnableSelfShadow ("Enable Self Shadow", Float) = 1
                [Toggle] _AutomaticNight ("Enable Auto Night/Day", Float) = 1
                [Toggle] _DayOrNight ("Enable Nighttime", Range(0.0, 1.0)) = 0.0 // _ES_ColorTone       
                [SmallTexture]_PackedShadowRampTex("Shadow Ramp",2D)= "white"{ }
                [Toggle] _UseLightMapColorAO ("Enable Lightmap Ambient Occlusion", Float) = 1.0
                [Toggle] _UseShadowRamp ("Enable Shadow Ramp Texture", Float) = 1.0
                [Toggle] _UseVertexColorAO ("Enable Vertex Color Ambient Occlusion", Float) = 1.0
                [Toggle] _UseVertexRampWidth ("Use Vertex Shadow Ramp Width", Float) = 0
                [Toggle] _MultiLight ("Enable Multi Light Source Mode", float) = 1.0
                //_EnvironmentLightingStrength ("Environment Lighting Strength", Range(0.0, 1.0)) = 1.0
                _LightArea ("Shadow Position", Range(0.0, 2.0)) = 0.55
                _ShadowRampWidth ("Ramp Width", Range(0.2, 3.0)) = 1.0
                [Toggle] _CustomAOEnable ("Enable Custom AO", Float) = 0	
                [SmallTexture]_CustomAO ("Custom AO Texture--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}",2D)= "white"{ }
                [Enum(Repeat, 0, Clamp, 1)] _AOSamplerType ("Custom AO Sampler Type--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}", Int) = 0
                [Enum(UV0, 0, UV1, 1, ScreenSpaceUVs, 2)] _CustomAOUV ("Custom AO UV--{condition_show:{type:PROPERTY_BOOL,data:_CustomAOEnable==1.0}}", Int) = 0
                // Shadow Transition
                [HideInInspector] start_shadowtransitions("Shadow Transitions--{reference_property:_UseShadowTransition}", Float) = 0
                    [Toggle] _UseShadowTransition ("Use Shadow Transition (only work when shadow ramp is off)", Float) = 0
                    _ShadowTransitionRange ("Shadow Transition Range 1", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange2 ("Shadow Transition Range 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange3 ("Shadow Transition Range 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange4 ("Shadow Transition Range 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionRange5 ("Shadow Transition Range 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0.01
                    _ShadowTransitionSoftness ("Shadow Transition Softness 1", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness2 ("Shadow Transition Softness 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness3 ("Shadow Transition Softness 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness4 ("Shadow Transition Softness 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0.5
                    _ShadowTransitionSoftness5 ("Shadow Transition Softness 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0.5
                [HideInInspector] end_shadowtransitions ("", Float) = 0
                // Day Shadow Color
                [HideInInspector] start_shadowcolorsday("DayTime Colors", Float) = 0
                    _FirstShadowMultColor ("Daytime Shadow Color 1", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor2 ("Daytime Shadow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor3 ("Daytime Shadow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor4 ("Daytime Shadow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _FirstShadowMultColor5 ("Daytime Shadow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                [HideInInspector] end_shadowcolorsday ("", Float) = 0
                // Night Shadow Color
                [HideInInspector] start_shadowcolorsnight("NightTime Colors", Float) = 0
                    _CoolShadowMultColor ("Nighttime Shadow Color 1", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor2 ("Nighttime Shadow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor3 ("Nighttime Shadow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor4 ("Nighttime Shadow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                    _CoolShadowMultColor5 ("Nighttime Shadow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.9, 0.7, 0.75, 1)
                [HideInInspector] end_shadowcolorsnight ("", Float) = 0
            [HideInInspector] end_lightandshadow ("", Float) = 0
            //endex

            //ifex _UseHairShadow == 0
            [HideInInspector] start_hairshadow("Hair Shadow--{reference_property:_UseHairShadow}", Float) = 0
                [Toggle] _UseHairShadow ("Enable Hair Shadow", Float) = 0.0
                _HairShadowExtrusion ("Hair Shadow Extrusion", Float) = 0.95
                _HairShadowLightShift ("Hair Shadow Light Shift", Float) = 0.0
                _HairShadowStencilShift ("Hair Shadow Stencil Shift", Vector) = (0.0, -0.3, 0.0, 0.0)
                _HairShadowVerticalRemap ("Hair Shadow Vertical Remap", Vector) = (0.0, 1.0, 1.0, 0.0)
                _HairShadowColor ("HairShadow Day Color", Color) = (0, 0, 0, 0.0784313753)
                _CoolHairShadowColor ("HairShadow Night Color", Color) = (0, 0, 0, 0.0784313753)
                [HideInInspector] start_hairshadowstencil("Hair Shadow Stencil", Float) = 0
                    [Enum(Off, 0, On, 1)] _sdwZWrite ("Zwrite", Int) = 1
                    [Enum(UnityEngine.Rendering.CompareFunction)] _sdwZTest ("ZTest", Float) = 2 // mihoyo uses this in their shader but set it to something that doesnt work 
                    [Enum(UnityEngine.Rendering.BlendMode)] _sdwSrc ("Source Blend", Int) = 1
                    [Enum(UnityEngine.Rendering.BlendMode)] _sdwDst ("Destination Blend", Int) = 1
                    [IntRange] _sdwRef ("Stencil Reference Value", Range(0, 255)) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _sdwPass ("Stencil Pass Op", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _sdwComp ("Stencil Compare Function", Float) = 8
                    [IntRange] _sdwColorMask ("Stencil Color Mask", Range(0, 16)) = 16
                [HideInInspector] end_hairshadowstencil ("", Float) = 0
            [HideInInspector] end_hairshadow ("", Float) = 0
            //endex
            //ifex _UseRimLight == 0
            // Rim Light 
            [HideInInspector] start_rimlight("Rim Light--{reference_property:_UseRimLight}", Float) = 0
                [Enum(Off, 0, Legacy, 1, New, 2)] _RimLightType ("Rim Light Type--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_UseRimLight=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_UseRimLight=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_UseRimLight=1}]}
                    ]}", Int) = 2
                [HideInInspector][Toggle] _UseRimLight ("Enable Rim Light--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_RimLightType=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_RimLightType=2}]}
                    ]}", Float) = 1
                _ES_AvatarRimWidth  ("Rim Light Width--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Range(0.0, 10.0)) = 1.5
                _ES_AvatarRimWidthScale ("Rim Light Width Scale--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Range(0.0, 10.0)) = 1
                _RimThreshold ("Rim Threshold--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Range(0.0, 1.0)) = 0.5
                _RimLightIntensity ("Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Float) = 0.25
                _RimLightThickness ("Rim Light Thickness--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==1.0}}", Range(0.0, 10.0)) = 1.0
                [HideInInspector] start_rimfront ("Front Parameters--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 0
                    [HDR]_ES_AvatarFrontRimColor ("Front Rim Light Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Color) = (1, 1, 1, 1)
                    _ES_AvatarFrontRimIntensity ("Front Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 1
                [HideInInspector] end_rimfront ("", Float) = 0
                // rim back
                [HideInInspector] start_rimback ("Back Parameters--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 0
                    [HDR]_ES_AvatarBackRimColor ("Back Rim Light Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Color) = (1, 1, 1, 1)
                    _ES_AvatarBackRimIntensity ("Back Rim Light Intensity--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType==2.0}}", Float) = 1
                [HideInInspector] end_rimback ("", Float) = 0
                [HideInInspector] start_lightingrimcolor("Rimlight Color--{condition_show:{type:PROPERTY_BOOL,data:_RimLightType>=1.0}}", Float) = 0
                    _RimColor (" Rim Light Color", Color)   = (1, 1, 1, 1)
                    _RimColor0 (" Rim Light Color 1 | (RGB ID = 0)", Color)   = (1, 1, 1, 1)
                    _RimColor1 (" Rim Light Color 2 | (RGB ID = 31)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor2 (" Rim Light Color 3 | (RGB ID = 63)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor3 (" Rim Light Color 4 | (RGB ID = 95)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color)  = (1, 1, 1, 1)
                    _RimColor4 (" Rim Light Color 5 | (RGB ID = 127)--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1, 1, 1, 1)
                [HideInInspector] end_lightingrimcolor("", Float) = 0
            [HideInInspector] end_rimlight ("", Float) = 0
            //endex
        [HideInInspector] end_lightning ("", Float) = 0
        //Lightning Options End

        //Reflections, specular/metal/leather
        [HideInInspector] start_reflections("Reflections", Float) = 0
            // Specular
            //ifex _MetalMaterial == 0
                [HideInInspector] start_metallics("Metallics--{reference_property:_MetalMaterial}", Int) = 0
                    [Toggle] _MetalMaterial ("Enable Metallic", Float) = 1.0
                    [SmallTexture]_MTMap("Metallic Matcap--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}",2D)= "white"{ }
                    [Toggle] _MTUseSpecularRamp ("Enable Metal Specular Ramp--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 0.0
                    [SmallTexture] _MTSpecularRamp("Specular Ramp--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_MetalMaterial==1},{type:PROPERTY_BOOL,data:_MTUseSpecularRamp==1}]}}", 2D) = "white" { }
                    _MTMapBrightness ("Metallic Matcap Brightness--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 3.0
                    _MTShininess ("Metallic Specular Shininess--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 90.0
                    _MTSpecularScale ("Metallic Specular Scale--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Float) = 15.0 
                    _MTMapTileScale ("Metallic Matcap Tile Scale--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.0, 2.0)) = 1.0
                    _MTSpecularAttenInShadow ("Metallic Specular Power in Shadow--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.0, 1.0)) = 0.2
                    _MTSharpLayerOffset ("Metallic Sharp Layer Offset--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Range(0.001, 1.0)) = 1.0
                    // Metal Color
                    [HideInInspector] start_metallicscolor("Metallic Colors--{condition_show:{type:PROPERTY_BOOL,data:_MetalMaterial==1.0}}", Int) = 0
                        [HDR]_MTMapDarkColor ("Metallic Matcap Dark Color", Color) = (0.51, 0.3, 0.19, 1.0)
                        [HDR]_MTMapLightColor ("Metallic Matcap Light Color", Color) = (1.0, 1.0, 1.0, 1.0)
                        _MTShadowMultiColor ("Metallic Matcap Shadow Multiply Color", Color) = (0.78, 0.77, 0.82, 1.0)
                        [HDR]_MTSpecularColor ("Metallic Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
                        [HDR]_MTSharpLayerColor ("Metallic Sharp Layer Color", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HideInInspector] end_metallicscolor ("", Int) = 0
                [HideInInspector] end_metallics("", Int) = 0
            //endex
            //ifex _SpecularHighlights == 0
            // Metal 
                [HideInInspector] start_specular("Specular Reflections--{reference_property:_SpecularHighlights}", Int) = 0
                    [Toggle] _SpecularHighlights ("Enable Specular--{on_value_actions:[
                        {value:0,actions:[{type:SET_PROPERTY,data:_UseToonSpecular=0}]},
                        {value:1,actions:[{type:SET_PROPERTY,data:_UseToonSpecular=1}]}
                        ]}", Float) = 0.0
                    [Toggle] _UseToonSpecular ("Enable Specular--{condition_show:{type:PROPERTY_BOOL,data:_IsDevMode==1.0}}", Float) = 0.0
                    _Shininess ("Shininess 1--{condition_show:{type:PROPERTY_BOOL,data:_SpecularHighlights==1.0}}", Float) = 10
                    _Shininess2 ("Shininess 2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial2==1}]}}", Float) = 10
                    _Shininess3 ("Shininess 3--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial3==1}]}}", Float) = 10
                    _Shininess4 ("Shininess 4--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial4==1}]}}", Float) = 10
                    _Shininess5 ("Shininess 5--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial5==1}]}}", Float) = 10
                    _SpecMulti ("Specular Multiplier 1--{condition_show:{type:PROPERTY_BOOL,data:_SpecularHighlights==1.0}}", Float) = 0.1
                    _SpecMulti2 ("Specular Multiplier 2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial2==1}]}}", Float) = 0.1
                    _SpecMulti3 ("Specular Multiplier 3--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial3==1}]}}", Float) = 0.1
                    _SpecMulti4 ("Specular Multiplier 4--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial4==1}]}}", Float) = 0.1
                    _SpecMulti5 ("Specular Multiplier 5--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial5==1}]}}", Float) = 0.1
                    _SpecOpacity ("Specular Opacity 1--{condition_show:{type:PROPERTY_BOOL,data:_SpecularHighlights==1.0}}", Float) = 0.1
                    _SpecOpacity2 ("Specular Opacity 2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial2==1}]}}", Float) = 0.1
                    _SpecOpacity3 ("Specular Opacity 3--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial3==1}]}}", Float) = 0.1
                    _SpecOpacity4 ("Specular Opacity 4--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial4==1}]}}", Float) = 0.1
                    _SpecOpacity5 ("Specular Opacity 5--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial5==1}]}}", Float) = 0.1
                    [HDR]_SpecularColor ("Specular Color--{condition_show:{type:PROPERTY_BOOL,data:_SpecularHighlights==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HDR]_SpecularColor2 ("Specular Color2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial2==1}]}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HDR]_SpecularColor3 ("Specular Color3--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial3==1}]}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HDR]_SpecularColor4 ("Specular Color4--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial4==1}]}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    [HDR]_SpecularColor5 ("Specular Color5--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_SpecularHighlights==1},{type:PROPERTY_BOOL,data:_UseMaterial5==1}]}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    // [HDR] _SpecularColor ("Specular Color--{condition_show:{type:PROPERTY_BOOL,data:_SpecularHighlights==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                [HideInInspector] end_specular("", Int) = 0
            //endex
            // Leather 
            //ifex _UseCharacterLeather == 0
                [HideInInspector] start_leather("Leather--{reference_property:_UseCharacterLeather}", Float) = 0
                    [Toggle] _UseCharacterLeather("Enable Leather", Float) = 0
                    _LeatherSpecularColor ("Leather Specular Color", Color) = (1,1,1,1)
                    [HideInInspector] start_laser ("Holographic", Float) = 0
                        _LeatherLaserRamp ("Holographic Ramp", 2D) = "grey" { }
                        _LeatherLaserTiling ("Tiling", Range(1, 6)) = 1
                        _LeatherLaserOffset ("Offset", Range(0, 2)) = 0
                        _LeatherLaserScale ("Scale", Range(0, 1)) = 0.5
                    [HideInInspector] end_laser ("", Float) = 0
                    [HideInInspector] start_reflmap ("Leather Reflection Matcap", Float) = 0
                        _LeatherReflect ("Leather Reflection Matcap--{condition_show:{type:PROPERTY_BOOL,data:_UseCharacterLeather==1.0}}", 2D) = "black" {}
                        _LeatherReflectBlur ("Leather Reflection Blur", Float) = 1
                        _LeatherReflectOffset ("Leather Reflection Offset", Float) = 0 
                        _LeatherReflectScale ("Leather Reflection Scale", Float) = 1
                    [HideInInspector] end_reflmap ("", Float) = 0
                    [HideInInspector] start_leaspec ("Specular", Float) = 0
                        _LeatherSpecularShift ("Leather Specualr Shift", Range(-1, 1)) = -0.5
                        _LeatherSpecularRange ("Leather Specualr Range", Range(1, 200)) = 50
                        _LeatherSpecularScale ("Leather Specualr Scale", Range(0, 1)) = 0
                        _LeatherSpecularSharpe ("Leather Specualr Sharpe", Range(0.501, 1)) = 1
                    [HideInInspector] end_leaspec ("Specular", Float) = 0
                    [HideInInspector] start_leatherdetail ("Detail", Float) = 0
                        _LeatherSpecularDetailColor ("Leather DetailSpecular Color", Color) = (1,1,1,1)
                        _LeatherSpecularDetailRange ("Leather DetailSpecular Range", Range(1, 200)) = 50
                        _LeatherSpecularDetailScale ("Leather DetailSpecular Scale", Range(0, 1)) = 0
                        _LeatherSpecularDetailSharpe ("Leather Specualr Sharpe", Range(0.501, 1)) = 1
                    [HideInInspector] end_leatherdetail ("Detail", Float) = 0
                [HideInInspector] end_leather("", Float) = 0
            //endex

            // this can be worked on later, my marriage is more important
            //ifex _UseCharacterStockings == 0
                [HideInInspector] start_stocking("Stocking--{reference_property:_UseCharacterStockings}", Float) = 0
                    [Toggle] _UseCharacterStockings ("Enable Stocking", Float) = 0
                    [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _StockingUV ("Stocking UV", Int) = 0
                    _StockingsDetailTex ("Stocking Detail Texture", 2D) = "grey" {}
                    _StockingCenterX ("Stocking Center X", Float) = 0.5
                    _StockingCenterY ("Stocking Center Y", Float) = 0.5
                    [HideInInspector] start_stockdecal ("Decal Settings", Float) = 0
                        _StockingDecalColor ("Decal Color", Color) = (1,1,1,1)
                        _StockingDecalIntensity ("Decal Intensity", Float) = 1
                        _StockingDecalScale ("Decal Scale", Float) = 1
                    [HideInInspector] end_stockdecal ("Decal Settings", Float) = 0
                    [HideInInspector] start_stockshine ("Shine Settings", Float) = 0
                        [HDR]_StockingShiningColor ("Shining Color", Color) = (1,1,1,1)
                        _StockingShiningColorBlend ("Shining Color Blend", Float) = 0.5
                        _StockingShiningDensity ("Shining Density", Float) = 0.5
                        _StockingShiningFrequencncy ("Shining Frequency", Float) = 0.5
                        _StockingShiningIntensity ("Shining Intensity", Float) = 0.5
                        _StockingShiningSize ("Shining Size", Float) = 0.5
                        _StockingShiningTiling ("Shining Tiling", Float) = 0.5
                    [HideInInspector] end_stockshine ("Shine Settings", Float) = 0
                    [HideInInspector] start_stockdetail ("Detail Settings", Float) = 0
                        _StockingsDetailPattenColor ("Detail Patten Color", Color) = (1,1,1,1)
                        _StockingsDetailPattenScale ("Detail Patten Scale", Float) = 1
                        _StockingsDetailPattenTiling ("Detail Patten Tiling", Float) = 1
                        _StockingsDetailScale ("Detail Scale", Float) = 1
                        _StockingsDetailTilingFar ("Detail Tiling Far", Float) = 1
                        _StockingsDetailTilingNear ("Detail Tiling Near", Float) = 1
                    [HideInInspector] end_stockdetail ("Detail Settings", Float) = 0
                    [HideInInspector] start_stocklighting("Lighting Settings", Float) = 0
                        _StockingsLightColor ("Light Color", Color) = (1,1,1,1)
                        _StockingsShadowColor ("Shadow Color", Color) = (0,0,0,1)
                        _StockingsLightRange ("Light Range", Float) = 1
                        _StockingsLightScale ("Light Scale", Float) = 1
                        _StockingsLightScaleInShadow ("Light Scale In Shadow", Float) = 1
                        _StockingsShadowRange ("Shadow Range", Float) = 1
                    [HideInInspector] end_stocklighting("Lighting Settings", Float) = 0
                    [HideInInspector] start_stockspecular("Specular Settings", Float) = 0
                        _StockingsSpecularColor ("Specular Color", Color) = (1,1,1,1)
                        _StockingsSpecularDetailColor ("Specular Detail Color", Color) = (1,1,1,1)
                        _StockingsSpecularDetailRange ("Specular Detail Range", Float) = 1
                        _StockingsSpecularDetailScale ("Specular Detail Scale", Float) = 1
                        _StockingsSpecularDetailSharpe ("Specular Detail Sharpe", Float) = 1
                        _StockingsSpecularDistance ("Specular Distance", Float) = 1
                        _StockingsSpecularFade ("Specular Fade", Float) = 1
                        _StockingsSpecularRange ("Specular Range", Float) = 1
                        _StockingsSpecularScale ("Specular Scale", Float) = 1
                        _StockingsSpecularSharpe ("Specular Sharpe", Float) = 1
                        _StockingsSpecularShift ("Specular Shift", Float) = 1
                        _StockingsTilingDistance ("Tiling Distance", Float) = 1
                    [Toggle] _StockingsWHite ("White", Float) = 1
                    [HideInInspector] end_stockspecular("Specular Settings", Float) = 0
                [HideInInspector] end_stocking("", Float) = 0
            //endex
            //ifex _UseCharacterNbrBase == 0
                [HideInInspector] start_nbr ("Non-Body Reflections", Float) = 0
                    [Toggle] _UseCharacterNbrBase ("Enable Non-Body Reflections", Float) = 0
                    [SmallTexture] _NbrRefTex ("Non-Body Reflection Texture--{condition_show:{type:PROPERTY_BOOL,data:_UseCharacterNbrBase==1.0}}", 2D) = "black"{}
                    _NbrRefBlur ("Non-Body Reflection Blur", Range(0, 1)) = 0.814
                    _NbrRefScale ("Non-Body Reflection Scale", Float) = 0.377
                    _NbrRefTiling ("Non-Body Reflection Tiling", Float) = 2.23
                    _NbrRoughness ("Non-Body Reflection Roughness", Range(0, 1)) = 1.0
                    _NbrScale ("Non-Body Reflection Intensity", Float) = 0.188
                    _NbrBaseColor ("Non-Body Reflection Base Color", Color) = (0,0,0,1)
                [HideInInspector] end_nbr ("", Float) = 0
            //endex
        [HideInInspector] end_reflections ("", Float) = 0
        //Reflections End

        //Outlines
        //ifex _OutlineEnabled == 0
        [HideInInspector] start_outlines("Outlines--{reference_property:_OutlineEnabled}", Float) = 0
            [HideInInspector] [Toggle] _OutlineEnabled ("Hidden Outline Bool--{on_value_actions:[{value:0,actions:[{type:SET_PROPERTY,data:_OutlineType=0}]}, {value:1,actions:[{type:SET_PROPERTY,data:_OutlineType=2}]}]}", Float) = 1
            [Enum(None, 0, Normal, 1,  Tangent, 2)] _OutlineType ("Outline Type--{on_value_actions:[{value:0,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=0},{type:SET_PROPERTY,data:_saveoutlinevalue=0}]}, {value:1,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=1},{type:SET_PROPERTY,data:_saveoutlinevalue=1}]}, {value:2,actions:[{type:SET_PROPERTY,data:_OutlineEnabled=1},{type:SET_PROPERTY,data:_saveoutlinevalue=2}]}]}", Float) = 1.0
            [Toggle] _UseOutlineTex ("Use Outline Width Texture", Float) = 0
            [Toggle] _FallbackOutlines ("Enable Static Outlines", Float) = 0
            [Toggle] _EviroAffectOutline ("Outlines Affected by Lighting", Float) = 0
            [Toggle] _DisableFOVScalingOL("Disable FOV Scaling On Outline", Float) = 0
            [Toggle] _DisableZShift ("Disable Z Shift", Float) = 0
            _OutlineWidth ("Outline Width", Float) = 0.03
            _Scale ("Outline Scale", Float) = 0.01
            _OutlineOffsetBlockBChannel ("Use Vertex Color B Channel", Float) = 0
            [Toggle] [HideInInspector] _UseClipPlane ("Use Clip Plane?", Float) = 0.0
            [HideInInspector] start_outlinestex("Outline Texture--{condition_show:{type:PROPERTY_BOOL,data:_UseOutlineTex==1.0}}", Float) = 0
                [SmallTexture] _OutlineTex ("Outline Width Texture", 2D) = "black"{}
                [Enum(From Red, 0, From Green, 1, From Blue, 2, From Alpha, 3)] _OutlineWidthChannel ("Outline Width Channel", Float) = 0
                [Enum(From Texture, 0, From Vertex Color, 1, Combination, 2)] _OutlineWidthSource ("Outline Width Source", Float) = 0
            [HideInInspector] end_outlinestex ("", Float) = 0
            [HideInInspector] _ClipPlane ("Clip Plane", Vector) = (0.0, 0.0, 0.0, 0.0)
            // Outline Color
            [HideInInspector] start_outlinescolor("Outline Colors", Float) = 0
                _OutlineColor ("Outline Color 1", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor2 ("Outline Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor3 ("Outline Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor4 ("Outline Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
                _OutlineColor5 ("Outline Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (0.0, 0.0, 0.0, 1.0)
            [HideInInspector] end_outlinescolor ("", Float) = 0

            [HideInInspector] start_outlineint ("Diffuse Intensity", Float) = 0
                _OutLineIntensity ("Intensity 1", Range(0, 1)) = 0
                _OutLineIntensity2 ("Intensity 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity3 ("Intensity 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity4 ("Intensity 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0, 1)) = 0
                _OutLineIntensity5 ("Intensity 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0, 1)) = 0
            [HideInInspector] end_outlineint ("", Float) =0

            // Outline Offsets
            [HideInInspector] start_outlinesoffset("Outline Offset & Adjustments", Float) = 0
                [Toggle] _DisableDepthScaling ("Disable Width Depth Scaling", Float) = 0
                [Helpbox] _OutlineHelp("Each component (X Y Z) refers is a the Near, Middle, and Far distance at which the scales below will be applied. Max Z-Offset is the furthest the outlines can travel on the Z axis away. Setting it to zero will disable Z offsets.", float) = 0
                [Vector3] _OutlineWidthAdjustZs ("Width Adjustment At Distance", Vector) = (0.001, 2.0, 6.0, 0.0)
                [Vector3]  _OutlineWidthAdjustScales ("Scale at Distances", Vector) = (0.01, 0.245, 0.6, 0.0)
                _MaxOutlineZOffset ("Max Z-Offset", Float) = 1.0
            [HideInInspector] end_outlinesoffset ("", Float) = 0
        [HideInInspector] end_outlines ("", Float) = 0
        //endex
        //Outlines End

        //Special Effects
        [HideInInspector] start_specialeffects("Special Effects", Float) = 0
            [HideInInspector] start_emissionglow("Emission / Archon Glow", Float) = 0
                [Enum(From Diffuse Alpha, 0, From Custom Mask, 1)]  _EmissionType ("Emission Mask Source", Float) = 0
                _CustomEmissionTex ("Custom Emission Texture--{condition_show:{type:PROPERTY_BOOL,data:_EmissionType==1}}", 2D) = "black"{}
                // Emission Intensity
                [HideInInspector] start_glowscale("Emission Intensity", Float) = 0
                    _EmissionScaler ("Emission Intensity", Range(0, 100)) = 1
                    _EmissionScaler1 ("Emission Intensity For Material 1", Range(0, 100)) = 1
                    _EmissionScaler2 ("Emission Intensity For Material 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler3 ("Emission Intensity For Material 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler4 ("Emission Intensity For Material 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0, 100)) = 1
                    _EmissionScaler5 ("Emission Intensity For Material 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0, 100)) = 1
                [HideInInspector] end_glowscale("", Float) = 0
                // Emission Color
                [HideInInspector] start_glowcolor("Emission Color", Float) = 0
                    [HDR]_EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
                    [HDR]_EmissionColor1_MHY ("Emission Color For Material 1", Color) = (1,1,1,1)
                    [HDR]_EmissionColor2_MHY ("Emission Color For Material 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor3_MHY ("Emission Color For Material 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor4_MHY ("Emission Color For Material 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColor5_MHY ("Emission Color For Material 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1,1,1,1)
                    [HDR]_EmissionColorEye ("Emission Color For Eye--{condition_show:{type:PROPERTY_BOOL,data:_ToggleEyeGlow==1.0}}", Color) = (1,1,1,1)
                [HideInInspector] end_glowcolor("", Float) = 0
                // Force Eye Glow
                [HideInInspector] start_eyeemission("Eye Emission--{reference_property:_ToggleEyeGlow}", Float) = 0
                    [Toggle] _ToggleEyeGlow ("Enable Eye Glow", Float) = 0.0
                
                    _EyeGlowStrength ("Eye Glow Strength", Float) = 0.5
                    _EyeTimeOffset ("Eye Glow Timing Offset", Range(0.0, 1.0)) = 0.1
                [HideInInspector] end_eyeemission("", Float) = 0
                // Emission Pulse
                [HideInInspector] start_emissionpulse("Pulsing Emission--{reference_property:_TogglePulse}", Float) = 0
                    [Toggle] _TogglePulse ("Enable Pulse", Float) = 0.0 
                    [Toggle] _EyePulse ("Enable Pulse for Eyes", Float) = 0
                    _PulseSpeed ("Pulse Speed", Float) = 1.3
                    _PulseMinStrength ("Minimum Pulse Strength", Range(0.0, 1.0)) = 0.0
                    _PulseMaxStrength ("Maximum Pulse Strength", Range(0.0, 1.0)) = 1.0
                [HideInInspector] end_emissionpulse ("", Float) = 0
            [HideInInspector] end_emissionglow ("", Float) = 0
            // Outline Emission
            //ifex _EnableOutlineGlow == 0
            [HideInInspector] start_outlineemission("Outline Emission--{reference_property:_EnableOutlineGlow}", Float) = 0
                [Toggle] _EnableOutlineGlow("Enable Outline Emission", Float) = 0
                _OutlineGlowInt("Outline Emission Intesnity", Range(0.0000, 100.0000)) = 1.0
                [HideInInspector] start_outlineemissioncolors("Outline Emission Colors", Float) = 0
                    _OutlineGlowColor("Outline Emission Color 1", Color) = (1.0, 1.0, 1.0, 1.0)
                    _OutlineGlowColor2("Outline Emission Color 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    _OutlineGlowColor3("Outline Emission Color 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    _OutlineGlowColor4("Outline Emission Color 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                    _OutlineGlowColor5("Outline Emission Color 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Color) = (1.0, 1.0, 1.0, 1.0)
                [HideInInspector] end_outlineemissioncolors("", Float) = 0
            [HideInInspector] end_outlineemission ("", Float) = 0
            //endex 
            // Star Cock
            [HideInInspector] start_starcock("Star Cloak--{reference_property:_StarCloakEnable}", Float) = 0 //tribute to the starcock 
                //ifex _StarCloakEnable == 0
                [Toggle] _StarCloakEnable("Enable Star Cloak", Float) = 0.0
                [Enum(Paimon, 0, Skirk, 1, Asmoday, 2)] _StarCockType ("Star Cloak Type Override--{condition_show:{type:PROPERTY_BOOL,data:_StarCloakEnable==1.0}}", Float) = 0
                [Enum(Story Version, 0, Playable Version, 1)] _Skirktype ("Game Version--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 1
                [Toggle] _StarCockEmis ("Star Cloak As Emission--{condition_show:{type:PROPERTY_BOOL,data:_StarCloakEnable==1.0}}", Float) = 0
                [Enum(UV0, 0, UV1, 1, UV2, 2)] _StarUVSource ("UV Source--{condition_show:{type:PROPERTY_BOOL,data:_StarCloakEnable==1.0}}", Float) = 0.0
                [Toggle] _StarCloakOveride("Star Cloak Shading Only--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0.0
                _StarCloakBlendRate ("Star Cloak Blend Rate--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Range(0.0, 2.0)) = 1.0
                _StarTex ("Star Texture 1--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType<2}]}}", 2D) = "black" { } // cock 
                _Star02Tex ("Star Texture 2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", 2D) = "black" { }
                _Star01Speed ("Star 1 Scroll Speed--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0
                _StarBrightness ("Star Brightness--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 60
                _StarHeight ("Star Texture Height--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 14.89
                _Star02Height ("Star Texture 2 Height--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0
                // Noise
                [HideInInspector] start_starcocknoise("Noise--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0 //starcock: the phantom cock
                    _NoiseTex01 ("Noise Texture 1", 2D) = "white" { }
                    _NoiseTex02 ("Noise Texture 2", 2D) = "white" { }
                    _Noise01Speed ("Noise 1 Scroll Speed", Float) = 0.1
                    _Noise02Speed ("Noise 2 Scroll Speed", Float) = -0.1
                    _Noise03Brightness ("Noise 3 Brightness", Float) = 0.2
                [HideInInspector] end_starcocknoise("", Float) = 0 //starcock: attack of the cocks
                // Color Palette
                [HideInInspector] start_starcockcolorpallete("Color Pallete--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0 //starcock: revenge of the cock
                    _ColorPaletteTex ("Color Palette Texture", 2D) = "white" { }
                    _ColorPalletteSpeed ("Color Palette Scroll Speed", Float) = -0.1
                [HideInInspector] end_starcockcolorpallete("", Float) = 0 //starcock: the cock awakens
                // Constellation
                [HideInInspector] start_starcockconstellation("Constellation--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0 //starcock: the last cock
                    _ConstellationTex ("Constellation Texture", 2D) = "white" { }
                    _ConstellationHeight ("Constellation Texture Height", Float) = 1.2
                    _ConstellationBrightness ("Constellation Brightness", Float) = 5
                [HideInInspector] end_starcockconstellation("", Float) = 0 //starcock: a starcock story
                // Cloud
                [HideInInspector] start_starcockcloud("Cloud--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==0}]}}", Float) = 0 //starcock: the rise of cock
                    _CloudTex ("Cloud Texture", 2D) = "white" { }
                    _CloudBrightness ("Cloud Texture Brightness", Float) = 1
                    _CloudHeight ("Cloud Texture Height", Float) = 1
                [HideInInspector] end_starcockcloud("", Float) = 0 //starcock: the cock strikes back
                // Textures
                _FlowMap ("Star Texture--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", 2D) = "white" { }
                _FlowMap02 ("Star Texture 2--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", 2D) = "white" { }
                _NoiseMap ("Noise Map--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", 2D) = "white" { }
                _FlowMask ("Flow Mask--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", 2D) = "white" { }
                // Gradient
                [HideInInspector] start_starcockgrad ("Gradient--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", Float) = 0
                    _BottomColor01 ("Top Color", Color) = (0,0,0,0)
                    _BottomColor02 ("Bottom Color", Color) = (1,0,0,0)
                    _BottomScale ("Gradient Scale", Float) = 1
                    _BottomPower ("Gradient Power", Float) = 1
                [HideInInspector] end_starcockgrad ("", Float) = 0
                // Flow
                [HideInInspector] start_starcockflow ("Star Controls--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", Float) = 0
                    _FlowColor ("Star Color", Color) = (1,1,1,0)
                    _FlowMaskScale ("Star Texture Scale", Float) = 1 
                    _FlowMaskPower ("Star Texture Power", Float) = 1
                    _FlowScale ("Star Intensity", Float) = 1
                    _FlowMaskSpeed ("Star Texture Speed", Vector) = (0,0,0,0)
                    _FlowMask02Speed ("Star Texture 02 Speed", Vector) = (0,0,0,0)
                [HideInInspector] end_starcockflow ("", Float) = 0
                // Asmoday Noise
                [HideInInspector] start_starcockasmodaynoise ("Noise Controls--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==2}]}}", Float) = 0
                    _NoiseScale ("Noise Scale", Range(0, 1)) = 0
                    _NoiseSpeed ("Noise Speed", Vector) = (0,0,0,0)
                [HideInInspector] end_starcockasmodaynoise ("", Float) = 0
                // Skirk Options
                _StarMask ("Stars Mask--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", 2D) = "white" { }
                [Toggle] _UseScreenUV ("Enable Screen UV--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 0
                [Toggle] _ScreenIsWorld ("Screen is World Space--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 0
                _StarTiling ("Star Tiling--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 1
                _StarTexSpeed ("Star TexSpeed--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Vector) = (0,0,0,0)
                _StarColor ("Star Color--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Color) = (1,1,1,1)
                _StarFlickRange ("Star Flicker Range--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Range(0, 1)) = 0.2
                _StarFlickColor ("Star Flicker Color--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Color) = (1,1,1,1)
                _StarFlickerParameters ("Star Flicker Parameters--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Vector) = (1,20,0.5,0)
                // Skirk Block 
                [HideInInspector] start_skockblock ("Highlight Block Controls--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 0
                    _BlockHighlightMask ("Block Highlight Mask", 2D) = "black" { }
                    _BlockHighlightColor ("Block Highlight Color", Color) = (1,1,1,1)
                    _BlockHighlightViewWeight ("Block Highlight View Weight", Range(0, 1)) = 0.5
                    _CloakViewWeight ("Cloak View Weight (StarMaskG)", Range(0, 1)) = 0.5
                    _BlockHighlightRange ("Block Highlight Range", Range(0, 1)) = 0.9
                    _BlockHighlightSoftness ("Block Highlight Softness", Range(0, 1)) = 0
                [HideInInspector] end_skockblock ("", Float) = 0
                // Skirk Bright Light Mask
                [HideInInspector] start_skockbright ("Bright Line Controls--{condition_show:{type:AND,conditions:[{type:PROPERTY_BOOL,data:_StarCloakEnable==1},{type:PROPERTY_BOOL,data:_StarCockType==1}]}}", Float) = 0
                    _BrightLineMask ("Bright Line Mask", 2D) = "white" { }
                    _BrightLineMaskContrast ("Bright Line Mask Contrast", Range(0.01, 10)) = 1
                    _BrightLineColor ("Bright Line Color", Color) = (1,1,1,1)
                    _BrightLineMaskSpeed ("Bright Line Mask Speed", Vector) = (0,0,0,0)
                [HideInInspector] end_skockbright ("", Float) = 0
                //endex
                //ifex _HandEffectEnable == 0
                [HideInInspector] start_asmodayarm("Asmoday Arm Effect--{reference_property:_HandEffectEnable}", Float) = 0
                    [Toggle] _HandEffectEnable ("Enable Asmoday Arm Effect", Float) = 0
                    // _LightColor ("Light Color", Color) = (0.4117647,0.1665225,0.1665225,0)
                    _ShadowColor ("Shadow Color", Color) = (0.2941176,0.1319204,0.1319204,0)
                    _ShadowWidth ("Shadow Width", Range(0, 1)) = 0.5764706
                    _LineColor ("Line Color", Color) = (1,1,1,0)
                    _TopLineRange ("Line Range", Range(0, 1)) = 0.2101024
                    // Fresnel Controls
                    [HideInInspector] start_asmogayfresnel ("Fresnel", Float) = 0
                        _FresnelColor ("Fresnel Color", Color) = (1,0.7573529,0.7573529,0)
                        _FresnelPower ("Fresnel Power", Float) = 5
                        _FresnelScale ("Fresnel Scale", Range(-1, 1)) = -0.4970588
                    [HideInInspector] end_asmogayfresnel ("", Float) = 0
                    // Gradient Controls
                    [HideInInspector] start_asmodaygradient ("Alpha Gradients", Float) = 0
                        _GradientPower ("Gradient Power", Float) = 1
                        _GradientScale ("Gradient Scale", Float) = 1
                    [HideInInspector] end_asmodaygradient ("", Float) = 0
                    // Mask Controls
                    [HideInInspector] start_asmodaymask ("Mask Values", Float) = 0
                        _Mask ("Mask", 2D) = "white" { }
                        _DownMaskRange ("Down Mask Range", Range(0, 1)) = 0.3058824
                        _TopMaskRange ("Top Mask Range", Range(0, 1)) = 0.1147379
                        _Mask_Speed_U ("Mask X Scroll Speed", Float) = -0.1
                    [HideInInspector] end_asmodaymask ("", Float) = 0
                    // UV Scale and Offset for the multiple _MainTex samples
                    [HideInInspector] start_asmodayuv ("UV Scales & Offsets", Float) = 0
                        _Tex01_UV ("Mask 1 UV Scale and Offset", Vector) = (1,1,0,0)
                        _Tex02_UV ("Mask 2 UV Scale and Offset", Vector) = (1,1,0,0)
                        _Tex03_UV ("Mask 3 UV Scale and Offset", Vector) = (1,1,0,0)
                        _Tex04_UV ("Mask 4 UV Scale and Offset", Vector) = (1,1,0,-0.01)
                        _Tex05_UV ("Mask 5 UV Scale and Offset", Vector) = (1,1,0,0)
                    [HideInInspector] end_asmodayuv ("", Float) = 0
                    // Scrolling speed for the multple _MainTex samples
                    [HideInInspector] start_asmodayspeed ("UV Scrolling Speeds", Float) = 0
                        _Tex01_Speed_U ("Mask 1 X Scroll Speed", Float) = 0.1
                        _Tex01_Speed_V ("Mask 1 Y Scroll Speed", Float) = 0
                        _Tex02_Speed_U ("Mask 2 X Scroll Speed", Float) = -0.1
                        _Tex02_Speed_V ("Mask 2 Y Scroll Speed", Float) = 0
                        _Tex03_Speed_U ("Mask 3 X Scroll Speed", Float) = 0
                        _Tex03_Speed_V ("Mask 3 Y Scroll Speed", Float) = -0.5
                        _Tex04_Speed_U ("Mask 4 X Scroll Speed", Float) = 0
                        _Tex04_Speed_V ("Mask 4 Y Scroll Speed", Float) = 0
                        _Tex05_Speed_U ("Mask 5 X Scroll Speed", Float) = 0
                        _Tex05_Speed_V ("Mask 5 Y Scroll Speed", Float) = 0 
                    [HideInInspector] end_asmodayspeed ("", Float) = 0
                [HideInInspector] end_asmodayarm ("", Float) = 0 
                //endex
            [HideInInspector] end_starcock ("", Float) = 0   

            // Skill Animation Fresnel
            //ifex _EnableFresnel == 0
            [HideInInspector] start_fresnel("Fresnel--{reference_property:_EnableFresnel}", Float) = 0
                [Toggle] _EnableFresnel ("Enable Fresnel", Float) = 0
                _HitColor ("Hit Color", Color) = (0,0,0,0)
                _ElementRimColor ("Element Rim Color", Color) = (0,0,0,0)
                _HitColorScaler ("Hit Color Intensity", Range(0.00, 100.00)) = 6
                _HitColorFresnelPower ("Hit Fresnel Power", Range(0.00,100.00)) = 1.5
            [HideInInspector] end_fresnel ("", Float) = 0
            //endex
            //ifex _EnableHueShift == 0
            // Hue Controls
            [HideInInspector] start_hueshift("Hue Shifting--{reference_property:_EnableHueShift}", Float) = 0
                [Toggle] _EnableHueShift ("Enable Hue Shifting", Float) = 0
                [Toggle] _UseHueMask ("Enable Hue Mask", Float) = 0
                _HueMaskTexture ("Hue Mask--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", 2D) = "white" {}
                // Color Hue
                [HideInInspector] start_colorhue ("Diffuse", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _DiffuseMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableColorHue ("Enable Diffuse Hue Shift", Float) = 1
                    [Toggle] _AutomaticColorShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftColorSpeed ("Shift Speed", Float) = 0.0
                    _GlobalColorHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _ColorHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _ColorHue2 ("Hue Shift 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0
                    _ColorHue3 ("Hue Shift 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0
                    _ColorHue4 ("Hue Shift 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0
                    _ColorHue5 ("Hue Shift 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0
                [HideInInspector] end_colorhue ("", Float) = 0
                // Outline Hue
                [HideInInspector] start_outlinehue ("Outline", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _OutlineMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableOutlineHue ("Enable Outline Hue Shift", Float) = 1
                    [Toggle] _AutomaticOutlineShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftOutlineSpeed ("Shift Speed", Float) = 0.0
                    _GlobalOutlineHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _OutlineHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _OutlineHue2 ("Hue Shift 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0
                    _OutlineHue3 ("Hue Shift 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0
                    _OutlineHue4 ("Hue Shift 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0
                    _OutlineHue5 ("Hue Shift 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0
                [HideInInspector] end_outlinehue ("", Float) = 0
                // Glow Hue
                [HideInInspector] start_glowhue ("Emission", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _EmissionMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableEmissionHue ("Enable Emission Hue Shift", Float) = 1
                    [Toggle] _AutomaticEmissionShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftEmissionSpeed ("Shift Speed", Float) = 0.0
                    _GlobalEmissionHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _EmissionHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _EmissionHue2 ("Hue Shift 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0
                    _EmissionHue3 ("Hue Shift 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0
                    _EmissionHue4 ("Hue Shift 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0
                    _EmissionHue5 ("Hue Shift 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0
                [HideInInspector] end_glowhue ("", Float) = 0
                // Rim Hue
                [HideInInspector] start_rimhue ("Rim", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _RimMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableRimHue ("Enable Rim Hue Shift", Float) = 1
                    [Toggle] _AutomaticRimShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftRimSpeed ("Shift Speed", Float) = 0.0
                    _GlobalRimHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _RimHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _RimHue2 ("Hue Shift 2--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial2==1.0}}", Range(0.0, 1.0)) = 0
                    _RimHue3 ("Hue Shift 3--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial3==1.0}}", Range(0.0, 1.0)) = 0
                    _RimHue4 ("Hue Shift 4--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial4==1.0}}", Range(0.0, 1.0)) = 0
                    _RimHue5 ("Hue Shift 5--{condition_show:{type:PROPERTY_BOOL,data:_UseMaterial5==1.0}}", Range(0.0, 1.0)) = 0
                [HideInInspector] end_rimhue ("", Float) = 0
            [HideInInspector] end_hueshift ("", float) = 0
            //endex
            // Nyx State
            [HideInInspector] start_nyx("NightSoul", Float) = 0
                // [Toggle] _EnableNyxState ("Enable NightSoul--{on_value_actions:[{value:0,actions:[{type:SET_PROPERTY,data:_EnableNyxBody=0},{type:SET_PROPERTY,data:_EnableNyxOutline=0}]}, {value:1,actions:[{type:SET_PROPERTY,data:_EnableNyxBody=1},{type:SET_PROPERTY,data:_EnableNyxOutline=1}]}]}", Float) = 0
                [Enum(Premade, 0, Custom, 1)] _NyxStateRampType ("Ramp Type", Float) = 0
                [NoScaleOffset] _NyxStateOutlineColorRamp ("Color Ramp", 2D) = "gray" { }
                [HideInInspector] start_customramp("Custom Ramp Settings", Float) = 0
                    // [NoScaleOffset] _DefaultRamp ("GreyScale Ramp", 2D) = "gray" {}
                    _RampPoint0 ("Color Ramp Point 0", Color) = (0.00,0.00,0.00,0)
                    // _RampPointPos0 ("Color Ramp Point 0 Position", Range(0, 1)) = 0
                    _RampPoint1 ("Color Ramp Point 1", Color) = (0.25,0.25,0.25,1)
                    // _RampPointPos1 ("Color Ramp Point 1 Position", Range(0, 1)) = 0.25
                    _RampPoint2 ("Color Ramp Point 2", Color) = (0.50,0.50,0.50,1)
                    // _RampPointPos2 ("Color Ramp Point 2 Position", Range(0, 1)) = 0.50
                    // _RampPoint3 ("Color Ramp Point 3", Color) = (0.75,0.75,0.75,1)
                    // _RampPointPos3 ("Color Ramp Point 3 Position", Range(0, 1)) = 0.75
                    // _RampPoint4 ("Color Ramp Point 4", Color) = (1.00,1.00,1.00,1)
                    // _RampPointPos4 ("Color Ramp Point 4 Position", Range(0, 1)) = 1.00
                [HideInInspector] end_customramp ("", Float) = 0
                [NoScaleOffset] _NyxStateOutlineNoise ("Noise(RG)", 2D) = "gray" { }
                [Vector2] _NyxStateOutlineColorNoiseScale ("Noise Scale", Vector) = (2,2,0,0)
                _NyxStateOutlineColorNoiseAnim ("Noise Speed", Vector) = (0.05,0.05,0,0)
                _NyxStateOutlineColorNoiseTurbulence ("Noise Turbulence", Range(0, 1)) = 0.25
                [HideInInspector] start_bodygroup ("Body Markings", Float) = 0
                    [Toggle] _EnableNyxBody ("Enable Body Markings", Float) = 0
                    [Toggle] _BodyAffected ("Affected by Light", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _TempNyxStatePaintMaskChannel("Mask Channel", Float) = 1
                    [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _NyxBodyUVCoord ("UV Coord for Mask", Float) = 0
                    _TempNyxStatePaintMaskTex ("Body Mask Texture", 2D) = "black" {}
                    _NyxStateOutlineColorOnBodyMultiplier ("Color Multiplier", Color) = (1,1,1,1)
                    _NyxStateOutlineColorOnBodyOpacity ("Blend Rate", Float) = 0
                [HideInInspector] end_bodygroup ("", Float) = 0
                [HideInInspector] start_nyxoutline ("Outline", Float) = 0
                    [Helpbox] _NyxOutlineHelpBox("This effect is incompatible with the Eye Stencils as it introduces conflicting Stencil States.", Float) = 0
                    [Toggle(ENABLE_NYX)] _EnableNyxOutline ("Enable Outline--{on_value_actions:[
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassNyx=0}, {type:SET_PROPERTY,data:_StencilCompA=8}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompNyx=6}, {type:SET_PROPERTY,data:_StencilRef=10}, {type:SET_PROPERTY,data:_StencilRefNyx=10}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Float) = 0
                    [Toggle] _LineAffected ("Affected by Light", Float) = 0
                    _NyxStateOutlineColor ("Color", Color) =  (1,1,1,1)
                    _NyxStateOutlineColorScale ("Color Intensity", Float) = 1
                    _NyxStateOutlineWidthScale ("Width Scale", Float) = 5
                    [Toggle] _NyxStateEnableOutlineWidthScaleHeightLerp ("Enable Height Blending", Float) = 0
                    [Vector2] _NyxStateOutlineWidthScaleRange ("Width Scale Lerp Range", Vector) = (1,1,0,0)
                    _NyxStateOutlineWidthVarietyWithResolution ("Variety with Resolution", Vector) = (1080,0,0,0)
                    _NyxStateOutlineWidthScaleLerpHeightRange ("Lerp Height Range", Vector) = (0,1,1,0)
                    [HideInInspector] start_nyxvert ("Vertex Animation", Float) = 0
                        [Vector2] _NyxStateOutlineVertAnimNoiseScale ("Vertex Noise Scale", Vector) = (2,2,0,0)
                        [Vector2] _NyxStateOutlineVertAnimNoiseAnim ("Vertex Noise Speed", Vector) = (0.05,0.05,0,0)
                        _NyxStateOutlineVertAnimScale ("Vertex Scale", Float) = 30
                        [Toggle] _NyxStateEnableOutlineVertAnimScaleHeightLerp ("Enable Vertex Scale Height Lerp", Float) = 0
                        [Vector2] _NyxStateOutlineVertAnimScaleRange ("Vertex Scale Lerp Range", Vector) = (1,1,0,0)
                        _NyxStateOutlineVertAnimScaleLerpHeightRange ("Vertex Lerp Height Range", Vector) = (0,1,1,0)
                    [HideInInspector] end_nyxvert ("", Float) = 0
                    [HideInInspector] start_nyxstencilsetting ("Stencil Settings", Float) = 0
                        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassNyx ("Stencil Pass Op A", Float) = 0
                        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompNyx ("Stencil Compare Function A", Float) = 8
                        [IntRange] _StencilRefNyx ("Stencil Reference Value", Range(0, 255)) = 0
                    [HideInInspector] end_nyxstencilsetting ("", Float) = 0
                [HideInInspector] end_nyxoutline ("", Float) = 0
            [HideInInspector] end_nyx("NightSoul", Float) = 0
            // Mavuika VAT
            //ifex _VertexAnimType == 0
            [HideInInspector] start_vat("Vertex Animation", Float) = 0
                    [HoyoToonWideEnum(Off, 0, Hair A, 1, Hair B, 2)] _VertexAnimType ("Vertex Animation Type--{on_value_actions:[
                    {value:0,actions:[{type:SET_PROPERTY,data:_EnableHairVat=0},{type:SET_PROPERTY,data:_EnableHairVertexVat=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_EnableHairVat=1},{type:SET_PROPERTY,data:_EnableHairVertexVat=1}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_EnableHairVat=0},{type:SET_PROPERTY,data:_EnableHairVertexVat=0}]}
                    ]}", Int) = 0 
                    // {value:2,actions:[{type:SET_PROPERTY,data:_EnableHairVat=1}]}
                    [HideInInspector] start_vatb_textures ("Vertex Animation Textures--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==2.0}}", Float) = 0
                        _PosTex_A ("Position Texture A", 2D) = "white" { }
                    [HideInInspector] end_vatb_textures ("", Float) = 0
                    [HideInInspector] start_vat_frame ("Frame Settings", Float) = 0
                        _FrameCount ("Frame Count", Float) = 100
                        _Speed ("Speed", Float) = 1
                        [Toggle] _IfAutoPlayback ("Auto Playback", Float) = 1
                        _CurrentFrame ("Current Frame", Float) = 0
                        _HoudiniFPS ("Houdini FPS", Float) = 30
                        _TimeShift ("Time Shift", Float) = 1
                        [Toggle] _IfInterframeInterp ("Interframe Interpolation", Float) = 1
                        [HideInInspector] start_vat_bounds ("Bounds", Float) = 0
                            _BoundMaxX ("Bound Max X", Float) = 0
                            _BoundMaxY ("Bound Max Y", Float) = 0
                            _BoundMaxZ ("Bound Max Z", Float) = 0
                            _BoundMinX ("Bound Min X", Float) = 0
                            _BoundMinY ("Bound Min Y", Float) = 0
                            _BoundMinZ ("Bound Min Z", Float) = 0
                        [HideInInspector] end_vat_bounds ("", Float) = 0
                    [HideInInspector] end_vat_frame ("", Float) = 0
                    [HideInInspector] start_vat_textures ("Vertex Shader Settings--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}, reference_property:_EnableHairVat}", Float) = 0
                        [Toggle] _EnableHairVat ("Enable Vertex Animation (VS) --{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Float) = 0
                        _VertexTex ("Vertex Animation Texture (VS)", 2D) = "white" { }
                        _VertexTexST ("Vertex Animation Texture Scale/Transform", Vector) = (1,1,0,0)
                        [Enum(R,0,G,1,B,2,A,3)] _VertexTexSwitch ("VretexTexSwitch", Float) = 1
                        _VertexTexUS ("VretexTexUS", Float) = 0
                        _VertexTexVS ("VretexTexVS", Float) = 0
                        _VertexAdd ("VertexAdd", Float) = 0
                        _VertexPower ("VertexPower", Float) = 0
                        _VertexMask ("VertexMask", Range(0, 1.1)) = 1.1
                    [HideInInspector] end_vat_textures ("", Float) = 0
                    [HideInInspector] start_vatps_textures ("Pixel Shader Settings--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}, reference_property:_EnableHairVertexVat}", Float) = 0
                        [Toggle] _EnableHairVertexVat ("Enable Vertex Animation (PS) --{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Float) = 0
                        _VertTex ("Vertex Animation Texture (PS)", 2D) = "white" { }
                        _VertTexST ("Vertex Animation Texture Scale/Transform", Vector) = (1,1,0,0)
                        [Enum(R,0,G,1,B,2,A,3)] _VertTexSwitch ("VretexTexSwitch", Float) = 1
                        _VertTexUS ("VretexTexUS", Float) = 0
                        _VertTexVS ("VretexTexVS", Float) = 0
                        _VertAdd ("VertexAdd", Float) = 0
                        _VertPower ("VertexPower", Float) = 0
                        _VertMask ("VertexMask", Range(0, 1.1)) = 1.1
                    [HideInInspector] end_vatps_textures ("", Float) = 0
                    [HideInInspector] start_vat_shading("Shading", Float) = 0
                        // hair a things 
                            _LerpTexture ("Blend Tex--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", 2D) = "white" { }
                            _LerpTextureST ("Blend Texture Scale/Transform--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Vector) = (1,1,0,0)
                        // end of hair a things
                        [HDR] _AllColorBrightness ("Overall Brightness", Color) = (1,1,1,1)
                        _DayColor ("Day Color", Color) = (1,1,1,1)
                        // more hair a things
                            [HDR] _LightColor ("Light Color--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Color) = (1,1,1,1)
                            [HDR] _DarkColor ("Dark Color--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Color) = (1,1,1,1)
                            [HDR] _HighlightsColor ("Highlights Color--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Color) = (1,1,1,1)
                            [HDR] _AhomoColor ("Ahomo Color--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Color) = (1,1,1,1)
                            _NoisePowerForLerpTex ("Blend Power--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Float) = 0
                            _HighlightsBrightness ("Highlight Brightness--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Float) = 0
                            _HighlightsSpeed ("Highlight Flicker Speed--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==1.0}}", Float) = 0
                        // end of hair a things
                        // hair b things
                        [HideInInspector] start_vat_ramp ("Ramp Settings--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==2.0}}", Float) = 0
                            _VerticalRampTex ("Ramp A", 2D) = "white" { }
                            _VerticalRampTex2 ("Ramp B", 2D) = "white" { }
                            _VerticalRampLerp ("Ramp Lerp Factor", Range(0, 1)) = 0
                            _VerticalRampTint ("Ramp Tint", Color) = (1,1,1,1)
                        [HideInInspector] end_vat_ramp ("", Float) = 0
                        [HideInInspector] start_vat_highlight ("Highlights--{condition_show:{type:PROPERTY_BOOL,data:_VertexAnimType==2.0}}", Float) = 0
                            _HighlightMaskTex ("HighlightMaskTex", 2D) = "black" { }
                            [Enum(R,0,G,1,B,2,A,3)] _HighlightMaskTexChannelSwitch ("HighlightMaskTexSwitch", Float) = 0
                            _HighlightMaskTex2 ("HighlightMaskTex2", 2D) = "black" { }
                            [Enum(R,0,G,1,B,2,A,3)] _HighlightMaskTex2ChannelSwitch ("HighlightMaskTex2Switch", Float) = 0
                            [Toggle] _HighlightMaskTex2VOffsetByVerColA ("HighlightMaskTex2 V Offset By VerCol A", Float) = 0
                            [HDR] _HighlightColor ("HighlightColor", Color) = (1,1,1,0)
                            [HDR] _HighlightColor2 ("HighlightColor2", Color) = (1,1,1,0)
                        [HideInInspector] end_vat_highlight ("", Float) = 0
                        [HideInInspector] start_vatfade("Fade", Float) = 0
                            [Toggle] _DisappearByVerColAlpha ("Disappear By Vertex Color Alpha", Float) = 0
                            [Toggle] _VerticalFadeToggle ("Vertical Fade Toggle", Float) = 0
                            _VerticalFadeOffset ("Vertical Fade Offset", Range(0, 1)) = 0.5
                            _VerticalFadeRange ("Vertical Fade Range", Range(0, 1)) = 0.5
                        [HideInInspector] end_vatfade("", Float) = 0
                    [HideInInspector] end_vat_shading("", Float) = 0
            [HideInInspector] end_vat ("", float) = 0
            //endex 

            [HideInInspector] start_fakelight("Fake Point Light", float) = 0
                [HideInInspector] start_firstlight("Fake Light One", Float) = 1
                    [Toggle] _UseFakePoint ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange ("Light Range", Float) = 1
                    _FakePointIntensity ("Light Intensity", Float) = 1
                    _FakePointPosition ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection ("Light Reflection", Float) = 1
                    _FakePointFrequency ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_firstlight("", float) = 0
                [HideInInspector] start_secondlight("Fake Light Two", Float) = 1
                    [Toggle] _UseFakePoint2 ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex2 ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor2 ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange2 ("Light Range", Float) = 1
                    _FakePointIntensity2 ("Light Intensity", Float) = 1
                    _FakePointPosition2 ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection2 ("Light Reflection", Float) = 1
                    _FakePointFrequency2 ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin2 ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity2 ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate2 ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_secondlight("", float) = 0
                [HideInInspector] start_thirdlight("Fake Light Three", Float) = 1
                    [Toggle] _UseFakePoint3 ("Use FakePoint", Float) = 0
                    _FakePointNoiseTex3 ("Light Noise Tex", 2D) = "white" { }
                    _FakePointColor3 ("Light Color", Color) = (1,1,1,1)
                    _FakePointRange3 ("Light Range", Float) = 1
                    _FakePointIntensity3 ("Light Intensity", Float) = 1
                    _FakePointPosition3 ("Light Position", Vector) = (0,0,0,0)
                    _FakePointReflection3 ("Light Reflection", Float) = 1
                    _FakePointFrequency3 ("Light Frequency", Float) = 0
                    _FakePointFrequencyMin3 ("Light Frequency Min", Float) = 0
                    _FakePointSkinIntensity3 ("Light On Skin Intensity", Float) = 1
                    _FakePointSkinSaturate3 ("Light On Skin Saturation", Float) = 0
                [HideInInspector] end_thirdlight("", float) = 0
            [HideInInspector] end_fakelight("", float) = 0  

            [HideInInspector] start_eyestencil ("Eye Stencil", Float) = 0
                [Helpbox] _StencilHelp("Warning: This feature requires you to seperate the eyes from the hair material and make it it's own material. Depending on future game updates it may break.", float) = 0
                [Helpbox] _StencilHelp2("This effect is incompatible with the NightSoul outline as it introduces conflicting Stencil States--{condition_show:{type:PROPERTY_BOOL,data:_EnableNyxOutline==1}}", float) = 0
                [Toggle] _UseEyeStencil ("Use Stencil", Float) = 0
                [Enum(Face, 0, Eye, 1, Hair, 2, Off, 3)] _StencilType ("Stencil Type--{on_value_actions:[
                    {value:3,actions:[{type:SET_PROPERTY,data:_CullMode=0}, {type:SET_PROPERTY,data:_SrcBlend=5}, {type:SET_PROPERTY,data:_DstBlend=10}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_StencilPassA=2}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=0}]},
                    {value:3,actions:[{type:SET_PROPERTY,data:_StencilCompB=0}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2040}, {type:SET_PROPERTY,data:render_type=Opaque}]},

                    {value:0,actions:[{type:SET_PROPERTY,data:_CullMode=2}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:0,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:0,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2010}, {type:SET_PROPERTY,data:render_type=Opaque}]},

                    {value:1,actions:[{type:SET_PROPERTY,data:_CullMode=2}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompB=5}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2010}, {type:SET_PROPERTY,data:render_type=Opaque}]},

                    {value:2,actions:[{type:SET_PROPERTY,data:_CullMode=0}, {type:SET_PROPERTY,data:_SrcBlend=1}, {type:SET_PROPERTY,data:_DstBlend=0}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=5}]},
                    {value:2,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=100}, {type:SET_PROPERTY,data:render_queue=2020}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Float) = 3
                [Enum(Off, 0, Left, 1, Right, 2)] _StencilFilter ("Filter Stencil Side", Float) = 0
                [Enum(None, 3, Light Map, 1, Eye Mask, 0, Custom Mask, 2)] _StencilMaskSource ("Stencil Mask Source", Float) = 0
                [Helpbox] _StencilHelp3("Setting your Mask Source to none will make the entire material act as a stencil", float) = 0
                _EyeMask ("Eye Mask Stencil--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource==0}}", 2D) = "black" {}
                _EyeMaskCustom ("Custom Mask--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource==2}}", 2D) = "black" {}
                [Toggle] _InvertMask("Invert Mask", Float) = 0
                [Enum(One Channel, 0, Two Channel, 1, Three Channel, 2, All Channels, 3)] _StencilChannelCount ("Stencil Mask Channel Usage", Float) = 0
                [HideInInspector] start_stencil_mask_layer("Stencil Mask Creation--{condition_show:{type:PROPERTY_BOOL,data:_StencilMaskSource!=3}}", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer0 ("Channel 1", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer1 ("Channel 2--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>0}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer1Op ("Channel 2 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>0}}", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer2 ("Channel 3--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>1}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer2Op ("Channel 3 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>1}}", float) = 0 
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _StencilLayer3 ("Channel 4--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>2}}", float) = 0 
                    [Enum(Add, 0, Mul, 1, Sub, 2, Div, 3)] _StencilLayer3Op ("Channel 4 Operation--{condition_show:{type:PROPERTY_BOOL,data:_StencilChannelCount>2}}", float) = 0 
                [HideInInspector] end_stencil_mask_layer("Stencil Mask Creation", Float) = 0
                [HideInInspector] start_stencil_condition("Stencil Test Conditions", Float) = 0
                    [Helpbox] _StencilHelp4("This is the condition that the eye mask is checked as and the threshold is the value used in the condition.", Float) = 0
                    [Helpbox] _StencilHelp5("Example being: Stencil mask is greater than or equal to 1", Float) = 0
                    [Enum(less than, 0, greater than, 1, equal to, 2, less than or equal, 3, greater than or equal, 4)] _StencilConditional ("Conditional", Float) = 1
                    _StencilConditionThresh ("Threshold", Float) = 0
                    [HideInInspector] end_stencil_condition("Stencil Test Conditions", Float) = 0
                    [HideInInspector] start_stencil_fade("Fade Settings", Float) = 0
                    [Toggle] _HairBlendUse("View Angle Fade", Float) = 0
                    _HairTransparentValue ("Stencil Blend", Float) = 0.5
                    _HairZOffset ("Z Mask Offset", Float) = 0
                    
                    [Helpbox] _StencilHelp6("These two values below control the steepness of the view angle fade.--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", Float) = 0
                    _AlphaYZ ("Alpha Up Control--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", float) = 0.658 
                    _AlphaXZ ("Alpha Side Control--{condition_show:{type:PROPERTY_BOOL,data:_HairBlendUse==1}}", float) = 0.293
                [HideInInspector] end_stencil_fade ("", Float) = 0
                [HideInInspector] start_stencilsetting ("Stencil Settings", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassA ("Stencil Pass Op A", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassB ("Stencil Pass Op B", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
                    [IntRange] _StencilRefA ("Stencil Reference Value", Range(0, 255)) = 0
                    [IntRange] _StencilRefB ("Stencil Reference Value", Range(0, 255)) = 0
                [HideInInspector] end_stencilsetting ("", Float) = 0
            [HideInInspector] end_eyestencil ("", Float) = 0

            [HideInInspector] start_dissolve("Avatar Dissolve", Float) = 0
                [Helpbox] _AvatarDeathHelp ("This is used in game for Avatar Death dissolves", Float) = 0
                [Toggle] _EnableAvatarDie ("Enable Avatar Death", Float) = 0
                [Toggle] _ApplyOnlyNyx ("Apply Only to NightSoul Outline", float) = 0
                [NoScaleOffset] _DissolveNoise ("Dissolve Noise", 2D) = "white" { }
                _DissolveNoiseST ("Dissolve Noise Scale Tiling", Vector) = (1,1,0,0)
                _DissolveValue ("Dissolve Value", Range(0, 1)) = 0
                _DissolveEdgeWidth ("Dissolve Edge Width Value", Float) = 1.1
                _DissolveColorScaler ("Dissolve Color Scaler", Float) = 1
                [HDR] _DissolveColor ("Dissolve Color", Color) = (0.4338235,1,0.9297161,1)
                [HDR] _DeathTintColor ("Death Tint Color", Color) = (1,1,1,1)
            [HideInInspector] end_dissolve ("", Float) = 0
            
            [HideInInspector] start_tonemapping ("Built in Tonemapping", Float) = 0
                [Toggle] _EnableTonemapping ("Enable Tonemapping", float) = 0
            [HideInInspector] end_tonemapping ("", Float) = 0
        [HideInInspector] end_specialeffects ("", Float) = 0
        //Special Effects End

        //Rendering Options
        [HideInInspector] start_renderingOptions("Rendering Options", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
            [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Int) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Int) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Int) = 0
            [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Source Blend--{on_value_actions:[{value:any,actions:[{type:LINK_PROPERTY,data:_SrcBlendMode==_SrcBlend}]}]}", Int) = 1
            [HideInInspector] [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Destination Blend--{on_value_actions:[{value:any,actions:[{type:LINK_PROPERTY,data:_DstBlendMode==_DstBlend}]}]}", Int) = 1
            // Debug Options
            //ifex _DebugMode == 0
            [HideInInspector] start_debugOptions("Debug--{reference_property:_DebugMode}", Float) = 0
                [Toggle] _DebugMode ("Enable Debug Mode", float) = 0
                [Enum(Off, 0, NdotL, 1, NdotV, 2, NdotH, 3, Shadow Area, 4)] _DebugLighting ("Lighting Terms Debug Mode", Float) = 0
                [Enum(Off, 0, RGB, 1, A, 2)] _DebugDiffuse("Diffuse Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugLightMap ("Light Map Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugFaceMap ("Face Map Debug Mode", Float) = 0
                [Enum(Off, 0, Bump, 1, Line SDF, 2)] _DebugNormalMap ("Normal Map Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugVertexColor ("Vertex Color Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugRimLight ("Rim Light Debug Mode", Float) = 0
                [Enum(Off, 0, Original (Encoded), 1, Original (Raw), 2, Bumped (Encoded), 3, Bumped (Raw), 4)] _DebugNormalVector ("Normals Debug Mode", Float) = 0 
                [Enum(Off, 0, On, 1)] _DebugTangent ("Tangents/Secondary Normal Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugMetal ("Metal Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugSpecular ("Specular Debug Mode", Float) = 0
                [Enum(Off, 0, Factor, 1, Color, 2, Both, 3)] _DebugEmission ("Emission Debug Mode", Float) = 0 
                [Enum(Off, 0, Forward, 1, Right, 2)] _DebugFaceVector ("Facing Vector Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugLights ("Lights Debug Mode", Float) = 0
                [HoyoToonWideEnum(Off, 0, Materail ID 1, 1, Material ID 2, 2, Material ID 3, 3, Material ID 4, 4, Material ID 5, 5, All(Color Coded), 6)] _DebugMaterialIDs ("Material ID Debug Mode", Float) = 0
            [HideInInspector] end_debugOptions("Debug", Float) = 0
            //endex
        [HideInInspector] end_renderingOptions("Rendering Options", Float) = 0
        //Rendering Options End
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE

        //ifex _UseBumpMap == 0
            #define use_bump
        //endex
        //ifex _MainTexColoring == 0
            #define use_texTint
        //endex
        //ifex _DisableColors == 0
            #define disable_color
        //endex
        //ifex _UseMaterialMasksTex == 0
            #define has_mask
        //endex
        //ifex _TextureLineUse == 0
            #define sdf_line
        //endex
        //ifex _UseFaceMapNew == 0 && variant_selector != 1
            #define faceishadow
        //endex
        //ifex _UseWeapon == 0
            #define weapon_mode
        //endex
        //ifex _UseGlassSpecularToggle == 0
            #define parallax_glass
        //endex
        //ifex _EnableShadow == 0
            #define use_shadow
        //endex
        //ifex _UseShadowRamp == 0
            #define has_sramp
        //endex
        //ifex _UseRimLight == 0
            #define use_rimlight
        //endex
        //ifex _SpecularHighlights == 0
            #define use_specular
        //endex 
        //ifex _MetalMaterial == 0
            #define use_metal
        //endex
        //ifex _UseCharacterLeather == 0
            #define use_leather
        //endex
        //ifex _UseCharacterStockings == 0
            #define use_stockings    
        //endex
        //ifex _UseCharacterNbrBase == 0
            #define use_nbrbase
        //endex
        //ifex _OutlineEnabled == 0
            #define use_outline
        //endex
        //ifex _StarCloakEnable == 0
            #define is_cock
            #define paimon_cock
            #define skirk_cock
            #define asmoday_cock
        //endex 
        //ifex _HandEffectEnable == 0
            #define asmogay_arm
        //endex
        //ifex _EnableFresnel == 0
            #define has_fresnel
        //endex
        //ifex _EnableHueShift == 0
            #define can_shift
        //endex
        //ifex _EnableNyxBody == 0
            #define nyx_body
        //endex 
        //ifex _EnableNyxOutline == 0
            #define nyx_outline
        //endex
        //ifex _DebugMode == 0
            #define can_debug
        //endex 
        //ifex _VertexAnimType == 0
            #define use_vat
        //endex

        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityShaderVariables.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "UnityInstancing.cginc"
        #include "Includes/HoyoToonGenshin-declarations.hlsl"
        #include "Includes/HoyoToonGenshin-inputs.hlsl"
        #include "Includes/HoyoToonGenshin-common.hlsl"

        ENDHLSL

        Pass // Character Pass, the only REQUIRED pass
        {
            Name "HairShadow Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Back
            Blend [_sdwSrc] [_sdwDst]
            ColorMask [_sdwColorMask]
            Stencil
            {
				Ref [_sdwRef]
				Comp [_sdwComp]
                Pass [_sdwPass]  
			}
            ZWrite [_sdwZWrite]
            ZTest [_sdwZTest]
            HLSLPROGRAM
            
            
            #pragma multi_compile_fwdbase
            #pragma multi_compile _is_shadow
            
            #pragma vertex vs_model
            #pragma fragment ps_model

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      
        
        Pass // Character Pass, the only REQUIRED pass
        {
            Name "Character Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
				Ref [_StencilRefA]
				Comp [_StencilCompA]
                Pass [_StencilPassA]  
			}
            HLSLPROGRAM
            
            
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            
            #pragma vertex vs_model
            #pragma fragment ps_model

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      

        Pass // Eye Stencil Pass
        {
            Name "Character Stencil Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRefB]
                Comp [_StencilCompB]
        		Pass [_StencilPassB]  
            }
            HLSLPROGRAM            
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #define is_stencil
            
            #pragma vertex vs_model
            #pragma fragment ps_model

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }      
        //ifex _MultiLight == 0
        Pass // Character Light Pass
        {
            Name "Character Light Pass"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_Cull]
            ZWrite Off
            Blend One One     
            // Stencil
            // {
            //     Ref 10
			// 	Comp always
			// 	Pass replace
			// }     
            HLSLPROGRAM
            
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT

            #pragma vertex vs_model
            #pragma fragment ps_model 

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }    
        //endex
        //ifex _OutlineEnabled == 0
        Pass // Outline Pass
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
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

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }
        //endex
        Pass // Shadow Pass, this ensures the model shows up in CameraDepthTexture
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            
            #pragma multi_compile_fwdbase
            
            #pragma vertex vs_shadow
            #pragma fragment ps_shadow

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }   
        //ifex _EnableNyxOutline == 0
        Pass // Nyx Outline Pass, Rendered after everything so it appears behind everything thanks to the stencil settings
        {
            Name "Nyx Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Stencil
			{
				Ref [_StencilRefNyx]
				Comp [_StencilCompNyx]
				Pass [_StencilPassNyx]
				Fail [_StencilPassNyx]
			}
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma shader_feature ENABLE_NYX
            #pragma vertex vs_nyx
            #pragma fragment ps_nyx

            #include "Includes/HoyoToonGenshin-program.hlsl"
            ENDHLSL
        }
        //endex
    }
    CustomEditor "HoyoToon.ShaderEditor"
}
