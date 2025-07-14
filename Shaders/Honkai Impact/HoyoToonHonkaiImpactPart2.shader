Shader "HoyoToon/Honkai Impact/Character Part 2"
{
    Properties 
  { 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
      [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0 
        //Header
        //[HideInInspector] shader_master_label ("✧<b><i><color=#C69ECE>HoyoToon Honkai Impact Part 2</color></i></b>✧", Float) = 0
        [HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/hi3p2logo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/hi3p2l", Float) = 0
        [HideInInspector] CharacterRight ("UI/hi3p2r", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
        [HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
        [HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        //Header End

        [HoyoToonShaderOptimizerLockButton] _ShaderOptimizerEnabled ("Lock Material", Float) = 0

        [HoyoToonWideEnum(Base, 0, Face, 1, Hair, 2, Eye, 3)]variant_selector("Material Type--{on_value_actions:[
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=0}, {type:SET_PROPERTY,data:_StencilCompA=8}]},
            {value:0,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=0}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:1,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:1,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2000}, {type:SET_PROPERTY,data:render_type=Opaque}]},

            {value:2,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:2,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2002}, {type:SET_PROPERTY,data:render_type=Opaque}]},
            
            {value:3,actions:[{type:SET_PROPERTY,data:_StencilPassA=0}, {type:SET_PROPERTY,data:_StencilPassB=2}, {type:SET_PROPERTY,data:_StencilCompA=6}]},
            {value:3,actions:[{type:SET_PROPERTY,data:_StencilCompB=8}, {type:SET_PROPERTY,data:_StencilRef=16}, {type:SET_PROPERTY,data:render_queue=2001}, {type:SET_PROPERTY,data:render_type=Opaque}]}]}", Int) = 0
            //Material Type End
            // face and eyes should use Base Stencil, Hair includes the stencil by default

        [HideInInspector] start_main ("Main", Float) = 0
            _MainTex ("Diffuse Texture", 2D) = "white" {}
            _LightMapTex ("Light Map Tex", 2D) = "gray" { } // (R X-ray Mask, G Shadow Threshold, B Specular Shininess, A NoUsed)
            [Toggle] _UseVFaceSwitch2UV ("Back Face Uses UV2", Float) = 0
            _Color ("Front Face Color", Color) = (1,1,1,1) 
            _BackFaceColor ("Back Face Color", Color) = (1,1,1,1)
            [HideInInspector] start_alpha("Alpha", Float) = 0
                _Opaqueness ("Transparency", Range(0,1)) = 1
                _VertexAlphaFactor ("Alpha From Vertex Factor", Range(0,1)) = 0 // (0: off)
                _CutOff ("Alpha Test Factor", Range(0,1)) = 0.5
            [HideInInspector] end_alpha ("", Float) = 0
            [HideInInspector] start_directions("Facing Directions", Float) = 0
                _headForwardVector ("Forward Vector | XYZ", Vector) = (0, 0, 1, 0)
                _headRightVector ("Right Vector | XYZ", Vector) = (-1, 0, 0, 0)
                _headUpVector ("Up Vector || XYZ", Vector) = (0, 1, 0, 0)
            [HideInInspector] end_directions ("", Float) = 0
        [HideInInspector] end_main ("", Float) = 0
        
        //ifex _UseBump ==  0
        [HideInInspector] start_bump("Normal Map--{reference_property:_UseBump}", Float) = 0
            [Toggle] _UseBump ("Enable Normal Mapping", Float) = 0
            _BumpMap ("Normal Map", 2D) = "bump" { } // (RGB - Normal)
            _BumpScale ("Normal Scale", Range(0, 5)) = 1
        [HideInInspector] end_bump("Normal Map", Float) = 0
        //endex

        //ifex variant_selector!=1
        [HideInInspector] start_faceshading("Face Shading--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==1.0}}", Float) = 0.0
            [HideInInspector] start_faceshadow ("Face Shadow--{reference_property:_EnableFaceMap}", Float) = 0
                [Toggle] _EnableFaceMap ("Use Face Map", Float) = 0
                _FaceMapTex ("Face Map Texture", 2D) = "gray" { } // (A)
            [HideInInspector] end_faceshadow("", Float) = 0
            [HideInInspector] start_faceexp("Face Expression", Float) = 0
                _FaceExpTex ("Face Expression Texture", 2D) = "white" { }
                [Toggle] _ExpOutlineToggle ("Expression Controls Outline Width", Float) = 0
                _ExpOutlineFix ("Expression Outline Fix", Range(0, 1)) = 0
                [HideInInspector] start_expcolor("Color", Float) = 0
                    _ExpBlushColorR ("Expression Blush Color(R)", Color) = (1,0,0,1)
                    _ExpShadowColorG ("Expression Shadow Color(G)", Color) = (0.5,0.5,0.5,1)
                    _ExpShadowColorB ("Expression Shadow Color(B)", Color) = (0.5,0.5,0.5,1)
                    _ExpShadowColorA ("Expression Shadow Color(A)", Color) = (0.5,0.5,0.5,1)
                [HideInInspector] end_expcolor("", Float) = 0
                [HideInInspector] start_expint("Intensity", Float) = 0
                    _ExpBlushIntensityR ("Expression Blush Intensity(R)", Range(0, 1)) = 0
                    _ExpShadowIntensityG ("Expression Shadow Intensity(G)", Range(0, 1)) = 0
                    _ExpShadowIntensityB ("Expression Shadow Intensity(B)", Range(0, 1)) = 0
                    _ExpShadowIntensityA ("Expression Shadow Intensity(A)", Range(0, 1)) = 0
                [HideInInspector] end_expint("", Float) = 0
            [HideInInspector] end_faceexp("", Float) = 0 
            
        [HideInInspector] end_faceshading(" ", Float) = 0
        //endex

        [HideInInspector] start_stocking("Stockings", Float) = 0
            [Toggle] _EnableStocking ("Stocking", Float) = 0
            _StockingMap ("Stocking Map", 2D) = "black" { }
            _StockingRampTex ("Ramp Map", 2D) = "white" { }
            _StockingDiffSoftRange ("Diffuse Soft Range", Range(0.01, 1)) = 0.8
            _StockingDarkenRange ("Darken Range", Range(0.01, 10)) = 3
            _StockingDarkenColor ("Darken Color", Color) = (0.5,0.5,0.5,0.5)
            _StockingDarkenColorAdd ("Darken Color Add", Color) = (0.5,0.5,0.5,0.5)
            _StockingSpecNormalInt ("Specular Normal Int", Range(0, 1)) = 1
            _StockingSpecRange ("Specular Range", Range(0, 1)) = 1
            _StockingSpecInt ("Specular Int", Range(0, 0.4)) = 0.2
            _StockingEdgeRange ("Edge Range", Range(0, 5)) = 1
            _StockingEdgeSoft ("Edge Soft", Range(0, 1)) = 0.4
            _StockingEdgeColor ("Edge Color", Color) = (0.5,0.5,0.5,0.5)
            // _StockingStretchColor ("Stretch Color", Color) = (0.5,0.5,0.5,0.5)
            // _EnableDynamicStretch ("Enable Dynamic Stretch", Float) = 0
            _StockingLightArea ("Stocking Light Area", Float) = 0.6
        [HideInInspector] end_stocking("", Float) = 0

        [HideInInspector] start_lighting("Lighting Options", Float) = 0
            [Toggle] _MultiLight ("Enable Multiple Lights Support", Float) =0
            [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1    
            //ifex _EnableShadow == 0
            [HideInInspector] start_shadow ("Shadow--{reference_property:_EnableShadow}", Float) = 0.0   
                [Toggle] _EnableShadow ("Enable Shadow", Float) = 1
                _RampTex ("Diffuse Ramp Texture", 2D) = "white" { }
                _RampTexV ("Diffuse Ramp Y Coordinate", Range(0, 1)) = 1
                _DiffuseOffset ("Shadow Offset", Range(-1, 1)) = 0
                _ToneSoft ("Tone Shading Soft", Range(0, 0.5)) = 0.1
                _SceneShadowSoft ("Scene Shadow Soft", Range(0, 0.5)) = 0.05
                _LightArea ("Light Area Threshold", Range(0, 1)) = 0.51
                // [HideInInspector] start_hair_shading("Hair Shading--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Float) = 0
                //     _HairShadowWidthX ("Hair Shadow Width :X", Float) = 0 // these are things used for a hair shadow texture thats created at runtime
                //     _HairShadowWidthY ("Hair Shadow Width :Y", Float) = 0
                // [HideInInspector] end_hair_shading("", Float) = 0
                [Toggle] _EnableBlack ("Enable Contrast Adjustment", Float) = 0
                _ShadowContrast ("Shadow Color Contrast", Float) = 1
                [HideInInspector] start_shadow_color("Shadow Colors--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Float) = 0
                    [Toggle]_ShadowRampTexUsed ("Use Shadow Colors 2 through 5", Float) = 0
                    _ShadowMultColor ("Shadow Colors", Color) = (0.9,0.7,0.75,1)
                    _ShadowMultColor2 ("Shadow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_ShadowRampTexUsed==1.0}}", Color) = (0.9,0.7,0.75,1)
                    _ShadowMultColor3 ("Shadow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_ShadowRampTexUsed==1.0}}", Color) = (0.9,0.7,0.75,1)
                    _ShadowMultColor4 ("Shadow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_ShadowRampTexUsed==1.0}}", Color) = (0.9,0.7,0.75,1)
                    _ShadowMultColor5 ("Shadow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_ShadowRampTexUsed==1.0}}", Color) = (0.9,0.7,0.75,1)
                [HideInInspector] end_shadow_color ("", Float) = 0
                [HideInInspector] start_hair_shadow("Shadow Colors--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Float) = 0
                    _FirstShadowMultColor ("First Shadow Multiply Color", Color) = (0.9,0.7,0.75,1)
                    _SecondShadowMultColor ("Second Shadow Multiply Color", Color) = (0.75,0.6,0.65,1)
                [HideInInspector] end_hair_shadow("", Float) = 0
                [HideInInspector] _SecondShadow ("Second Shadow Threshold", Range(0, 1)) = 0.51
            [HideInInspector] end_shadow ("Shadow", Float) = 0.0  
            //endex
        [HideInInspector] end_lighting("", Float) = 0

        [HideInInspector] start_reflections("Reflections", Float) = 0
            //ifex _MTMapRampTexUsed == 0
            [HideInInspector] start_metallics("Metallics--{reference_property:_MTMapRampTexUsed}", Int) = 0
                [Toggle]_MTMapRampTexUsed ("Enable", Float) = 0
                _MTMap ("Metal Map Texture--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", 2D) = "white" { }
                _MTMapTileScale ("Metal Map Tile Scale--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Float) = 1
                _MTMapThreshold ("Metal Map Threshold--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Range(0, 1)) = 0.5
                _MTMapBrightness ("Metal Map Brightness--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Float) = 1
                _MTShininess ("Metal Shininess--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Float) = 11
                _MTSpecularAttenInShadow ("Metal Specular Attenuation in Shadow--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Range(0, 1)) = 0.2
                [HideInInspector] start_metallicscolor("Metallic Colors--{condition_show:{type:PROPERTY_BOOL,data:_MTMapRampTexUsed==1.0}}", Int) = 0
                    _MTMapLightColor ("Metal Map Light Color", Color) = (1,1,1,1)
                    _MTMapDarkColor ("Metal Map Dark Color", Color) = (0,0,0,0)
                    _MTShadowMultiColor ("Metal Shadow Multiply Color", Color) = (0.8,0.8,0.8,0.8)
                    _MTSpecularColor ("Metal Specular Color", Color) = (1,1,1,1)
                [HideInInspector] end_metallicscolor ("", Int) = 0
            [HideInInspector] end_metallics("", Int) = 0
            //endex
            //ifex _EnableSpecular == 0
            [HideInInspector] start_specular("Specular Reflections--{reference_property:_EnableSpecular}", Int) = 0
                [Toggle] _EnableSpecular ("Enable Specular", Float) = 1
                [Toggle] _UseSoftSpecular ("Use Soft Specular--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Float) = 0
                _Shininess ("Specular Shininess--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Range(0.1, 100)) = 10
                _SpecSoftRange ("Specular Soft--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Range(0, 0.5)) = 0
                _SpecMulti ("Specular Multiply Factor--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Range(0, 10)) = 0.1
                [HideInInspector] start_specularcolor ("Specular Color--{condition_show:{type:PROPERTY_BOOL,data:variant_selector<2.0}}", Float) = 0
                    [Toggle] _SpecularRampTexUsed ("Use Specular Colors 2 through 5", Float) = 0
                    _LightSpecColor ("Light Specular Color 1", Color) = (1,1,1,1)
                    _LightSpecColor2 ("Light Specular Color 2--{condition_show:{type:PROPERTY_BOOL,data:_SpecularRampTexUsed==1.0}}", Color) = (1,1,1,1)
                    _LightSpecColor3 ("Light Specular Color 3--{condition_show:{type:PROPERTY_BOOL,data:_SpecularRampTexUsed==1.0}}", Color) = (1,1,1,1)
                    _LightSpecColor4 ("Light Specular Color 4--{condition_show:{type:PROPERTY_BOOL,data:_SpecularRampTexUsed==1.0}}", Color) = (1,1,1,1)
                    _LightSpecColor5 ("Light Specular Color 5--{condition_show:{type:PROPERTY_BOOL,data:_SpecularRampTexUsed==1.0}}", Color) = (1,1,1,1)
                [HideInInspector] end_specularcolor ("", Float) = 0       
                //ifex variant_selector !=2.0
                _SpecularOffset ("Specular Offset--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Vector) = (0,0,0,1)
                _SpecularShiftRange ("Specular Shift Range--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Range(-5, 5)) = 0.1
                _RampMap ("Hair Ramp Map Texture--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", 2D) = "white" { } // |RG (2D diffuse ramp) BA (2D specular ramp)
                _JitterMap ("Hair Jitter Map Texture--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", 2D) = "gray" { } // |A (jitter noise)
                [HideInInspector]_HairStripPatternsTex ("Hair Strip Pattern", 2D) = "white" {}
                [HideInInspector] start_masking("Mask--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Float) = 0
                    _SpecularMaskMap ("Specular Mask Map", 2D) = "White" { }
                    _SpecularMaskLerp ("Specular Mask Lerp", Range(0, 1)) = 1
                [HideInInspector] end_masking("", Float) = 0
                [HideInInspector] start_LowGrp ("Low--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Float) = 0
                    _SpecularLowColor ("Specular Low Color", Color) = (0.5,0.5,0.5,1)
                    _SpecularLowIntensity ("Specular Low Intensity", Range(0, 3)) = 0.5
                    _SpecularLowShift ("Specular Low Shift", Range(-5, 5)) = 0
                    _SpecularLowJitterRangeMin ("Specular Low Jitter Min", Range(0, 5)) = 0
                    _SpecularLowJitterRangeMax ("Specular Low Jitter Max", Range(0, 5)) = 1
                    _SpecularLowShininessRangeMin ("Specular Low Shininess Range Min", Range(0, 2500)) = 0.1
                    _SpecularLowShininessRangeMax ("Specular Low Shininess Range Max", Range(0, 2500)) = 0.1
                [HideInInspector] end_LowGrp ("Low", Float) = 0
                [HideInInspector] start_HighGrp ("High--{condition_show:{type:PROPERTY_BOOL,data:variant_selector==2.0}}", Float) = 0
                    _SpecularHighColor ("Specular High Color", Color) = (0.5,0.5,0.5,1)
                    _SpecularHighIntensity ("Specular High Intensity", Range(0, 3)) = 0.5
                    _SpecularHighShift ("Specular High Shift", Range(-5, 5)) = 0
                    _SpecularHighJitterRangeMin ("Specular High Jitter Min", Range(-5, 5)) = 0
                    _SpecularHighJitterRangeMax ("Specular High Jitter Max", Range(-5, 5)) = 1
                    _SpecularHighShininessRangeMin ("Specular High Shininess Range Min", Range(0, 2500)) = 0.1
                    _SpecularHighShininessRangeMax ("Specular High Shininess Range Max", Range(0, 2500)) = 0.1
                [HideInInspector] end_HighGrp ("High", Float) = 0
                //endex
            [HideInInspector] end_specular("", Int) = 0
            //endex
            //ifex _EnableRimGlow == 0
            [HideInInspector] start_rimglow("Rim Glow--{reference_property:_EnableRimGlow}", Float) = 0
                [Toggle] _EnableRimGlow ("Enable Rim Glow", Float) = 0
                _RGPower ("Rim Glow Power--{condition_show:{type:PROPERTY_BOOL,data:_EnableRimGlow==1.0}}", Range(0.001, 100)) = 1
                _RGSoftRange ("Rim Glow Soft Range--{condition_show:{type:PROPERTY_BOOL,data:_EnableRimGlow==1.0}}", Range(0, 1)) = 0.1
                _RimGlowStrength ("Rim Glow Emission Strength--{condition_show:{type:PROPERTY_BOOL,data:_EnableRimGlow==1.0}}", Range(0, 100)) = 1
                    [HideInInspector] start_rimcolor("Rim Color", Float) = 0
                        [Toggle] _RGRampTexUsed ("Use Rim Colors 2 through 5", Float) = 0
                        _RGColor ("Rim Glow Color 1", Color) = (1,1,1,1)
                        _RGColor2 ("Rim Glow Color 2--{condition_show:{type:PROPERTY_BOOL,data:_RGRampTexUsed==1.0}}", Color) = (1,1,1,1)
                        _RGColor3 ("Rim Glow Color 3--{condition_show:{type:PROPERTY_BOOL,data:_RGRampTexUsed==1.0}}", Color) = (1,1,1,1)
                        _RGColor4 ("Rim Glow Color 4--{condition_show:{type:PROPERTY_BOOL,data:_RGRampTexUsed==1.0}}", Color) = (1,1,1,1)
                        _RGColor5 ("Rim Glow Color 5--{condition_show:{type:PROPERTY_BOOL,data:_EnableRimGlow==1.0}}", Color) = (1,1,1,1)
                    [HideInInspector] end_rimcolor("", Float) = 0
            [HideInInspector] end_rimglow("", Float) = 0
            //endex
        [HideInInspector] end_reflections ("", Float) = 0

        //ifex _EnableOutline == 0
        [HideInInspector] start_outlines("Outlines--{reference_property:_EnableOutline}", Float) = 0
            [Toggle] _EnableOutline ("Enable Outlines", Float) = 1
            _OutlineWidth ("Outline Width", Range(0, 100)) = 0.04
            _Scale ("Outline Scale", Range(0, 100)) = 0.04
            _GlobalOutlineScale("Global Outline Scale", Vector) = (1,1,1,0)
            //_OutlineEmission ("Outline Emission", Range(0, 100)) = 1
            
            
            [HideInInspector] start_outline_color("Color", Float) = 0 
                [Toggle]_More_Outline_Color ("Use Outline Colors 2 through 5", Float) = 0
                _OutlineColor ("Outline Color 1", Color) = (0,0,0,1)
                _OutlineColor2 ("Outline Color 2--{condition_show:{type:PROPERTY_BOOL,data:_More_Outline_Color==1.0}}", Color) = (0,0,0,1)
                _OutlineColor3 ("Outline Color 3--{condition_show:{type:PROPERTY_BOOL,data:_More_Outline_Color==1.0}}", Color) = (0,0,0,1)
                _OutlineColor4 ("Outline Color 4--{condition_show:{type:PROPERTY_BOOL,data:_More_Outline_Color==1.0}}", Color) = (0,0,0,1)
                _OutlineColor5 ("Outline Color 5--{condition_show:{type:PROPERTY_BOOL,data:_More_Outline_Color==1.0}}", Color) = (0,0,0,1)
            [HideInInspector] end_outline_color("", Float) = 0 
        [HideInInspector] end_outlinescolor ("", Float) = 0
        //endex
        [HideInInspector] start_specialeffects("Special Effects", Float) = 0
            //ifex _EnableStencil == 0.0
            [HideInInspector] start_xray("Stencil--{reference_property:_EnableStencil}", Float) = 0
                [Toggle] _EnableStencil ("Enable Stencil", float)  = 0
                _HairBlendSilhouette ("Hair Blend Silhouette", Range(0, 1)) = 0.5
                [IntRange] _StencilRef ("Stencil Reference Value", Range(0, 255)) = 0
                [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassA ("Stencil Pass Op A", Float) = 0
                [Enum(UnityEngine.Rendering.StencilOp)] _StencilPassB ("Stencil Pass Op B", Float) = 0
                [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
            [HideInInspector] end_xray("", Float) = 0
            //endex
            //ifex _Emission_Type == 0
            [HideInInspector] start_emissionglow("Emission--{reference_property:_hiddenemission}", Float) = 0
                [HideInInspector] [Toggle] _hiddenemission ("stop looking at this--{on_value_actions:[
                {value:0,actions:[{type:SET_PROPERTY,data:_Emission_Type=0}]},
                {value:1,actions:[{type:SET_PROPERTY,data:_Emission_Type=1}]}]}", Float) = 0 //this is so stupid
                [Enum(Off, 0, On, 1)] _Emission_Type ("Emission--{on_value_actions:[
                {value:0,actions:[{type:SET_PROPERTY,data:_hiddenemission=0}]},
                {value:1,actions:[{type:SET_PROPERTY,data:_hiddenemission=1}]}]}", Float) = 0
                _EmissionStrength ("Emission Strength", Range(0, 100)) = 1
                [Toggle]_MulAlbedo ("Multiply Emission by Diffuse", Float) = 0
                [Toggle]_UseMainTexAsEmission ("Use Diffuse Alpha as Emission Mask", Float) = 0
                [HideInInspector] start_emission_color("Color", Float) = 0 
                    [Toggle]_EmissionRampTexUsed ("Use Emission Colors 2 through 5", Float) = 0
                    _EmissionColor ("Emission Color 1", Color) = (1,1,1,1)
                    _EmissionColor2 ("Emission Color 2--{condition_show:{type:PROPERTY_BOOL,data:_EmissionRampTexUsed==1.0}}", Color) = (0,0,0,1)
                    _EmissionColor3 ("Emission Color 3--{condition_show:{type:PROPERTY_BOOL,data:_EmissionRampTexUsed==1.0}}", Color) = (0,0,0,1)
                    _EmissionColor4 ("Emission Color 4--{condition_show:{type:PROPERTY_BOOL,data:_EmissionRampTexUsed==1.0}}", Color) = (0,0,0,1)
                    _EmissionColor5 ("Emission Color 5--{condition_show:{type:PROPERTY_BOOL,data:_EmissionRampTexUsed==1.0}}", Color) = (0,0,0,1)
                [HideInInspector] end_emission_color("", Float) = 0 
            [HideInInspector] end_emissionglow ("", Float) = 0
            //endex

        [HideInInspector] end_specialeffects ("", Float) = 0

        [HideInInspector] start_renderingOptions("Rendering Options", Float) = 0
            [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull", Float) = 0
            [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Int) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
            [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Int) = 1
            [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Int) = 0
            _OffsetRate ("Offset Rate", Range(-1,1)) = 0
            _OffsetUnits ("Offset Units", Range(-1,1)) = 0

            //ifex _DebugMode == 0
            // Debug Options
            [HideInInspector] start_debugOptions("Debug--{reference_property:_DebugMode}", Float) = 0
                [Toggle] _DebugMode ("Enable Debug Mode", float) = 0
                [Enum(Off, 0, RGB, 1, A, 2)] _DebugDiffuse("Diffuse Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugLightMap ("Light Map Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugFaceMap ("Face Map Debug Mode", Float) = 0
                [Enum(Off, 0, R, 1, G, 2, B, 3, A, 4)] _DebugExpMap ("Face Expression Map Debug Mode", Float) = 0
                [Enum(Off, 0, Bump, 1)] _DebugNormalMap ("Normal Map Debug Mode", Float) = 0
                [HoyoToonWideEnum(Off, 0, R, 1, G, 2, B, 3, A, 4, RGB, 5)] _DebugVertexColor ("Vertex Color Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugRimLight ("Rim Light Debug Mode", Float) = 0
                [Enum(Off, 0, Original (Encoded), 1, Original (Raw), 2, Bumped (Encoded), 3, Bumped (Raw), 4)] _DebugNormalVector ("Normals Debug Mode", Float) = 0 
                [Enum(Off, 0, On, 1)] _DebugTangent ("Tangents/Secondary Normal Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugMetal ("Metal Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugSpecular ("Specular Debug Mode", Float) = 0
                [Enum(Off, 0, Factor, 1, Color, 2, Both, 3)] _DebugEmission ("Emission Debug Mode", Float) = 0 
                [Enum(Off, 0, Forward, 1, Right, 2, Up, 3)] _DebugFaceVector ("Facing Vector Debug Mode", Float) = 0
                [Enum(Off, 0, On, 1)] _DebugLights ("Lights Debug Mode", Float) = 0
                [HoyoToonWideEnum(Off, 0, Materail ID 1, 1, Material ID 2, 2, Material ID 3, 3, Material ID 4, 4, Material ID 5, 5, All(Color Coded), 6)] _DebugMaterialIDs ("Material ID Debug Mode", Float) = 0
            [HideInInspector] end_debugOptions("Debug", Float) = 0
            //endex
        
        [HideInInspector] end_renderingOptions("Rendering Options", Float) = 0
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE

        // material macros
        //ifex _EnableShadow == 0
        #define use_shadow
        //endex
        //ifex _EnableSpecular == 0
        #define use_specular
        //endex
        //ifex variant_selector!=1
        #define faceishadow
        //endex
        //ifex variant_selector!=2
        #define is_hair
        //endex
        //ifex _MTMapRampTexUsed == 0
        #define use_metal
        //endex
        //ifex _EnableRimGlow == 0
        #define use_rimglow
        //endex
        //ifex _Emission_Type == 0
        #define use_emission
        //endex

        #include "UnityCG.cginc"
        #include "UnityPBSLighting.cginc"
        #include "UnityShaderVariables.cginc"
        #include "AutoLight.cginc"
        #include "UnityLightingCommon.cginc"
        #include "Lighting.cginc"
        // ==================================================== //
        #include "Includes/Part2-declarations.hlsl"
        #include "Includes/Part2-inputs.hlsl"
        #include "Includes/Part2-common.hlsl"
        ENDHLSL

        Pass // main pass
        {
            Name "Character Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_CullMode]
            Blend [_SrcBlend] [_DstBlend]
            Offset [_OffsetRate], [_OffsetUnits]
            Stencil
            {
                ref [_StencilRef]  
                Comp [_StencilCompA]
                Pass [_StencilPassA] // this doesn't even fucking matter like what?
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model

            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        }

        //ifex _MultiLight == 0
        Pass // main pass
        {
            Name "Character Pass"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_CullMode]
            Blend One One
            Offset [_OffsetRate], [_OffsetUnits]
            Stencil
            {
                ref [_StencilRef]  
                Comp [_StencilCompA]
                Pass [_StencilPassA] // this doesn't even fucking matter like what?
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT
            #pragma vertex vs_model
            #pragma fragment ps_model

            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        }
        //endex
        //ifex _EnableStencil == 0
        Pass // stencil xray
        {
            Name "Character Pass X-RAY"
            Tags{ "LightMode" = "ForwardBase" }
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

            #define is_xray
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model


            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        }
        //endex

        
        //ifex _EnableStencil == 0 || _MultiLight == 0
        Pass // stencil xray
        {
            Name "Character Pass Light"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_CullMode]
            Blend One One

            Stencil
            {
                ref [_StencilRef]              
                Comp [_StencilCompB]
                Pass [_StencilPassB]  
                Fail Keep
                ZFail Keep
            }
            
            HLSLPROGRAM
            
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT

            #define is_xray
            #pragma vertex vs_model
            #pragma fragment ps_model


            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        }
        //endex

        //ifex _EnableOutline == 0
        Pass // edge pass
        {
            Name "Edge Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Stencil
            {
				ref 255
                Comp Always
                Pass Keep
            }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            
            #pragma multi_compile_fwdbase
            
            #pragma vertex vs_edge
            #pragma fragment ps_edge

            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        }
        //endex

        Pass // depth shadow pass
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            
            #pragma multi_compile_fwdbase
            
            #pragma vertex vs_shadow
            #pragma fragment ps_shadow

            #include "Includes/Part2-program.hlsl"
            ENDHLSL
        } 
    }
    CustomEditor "HoyoToon.ShaderEditor"
}
