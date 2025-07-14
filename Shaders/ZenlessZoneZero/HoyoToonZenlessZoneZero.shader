Shader "HoyoToon/Zenless Zone Zero/Character"
{
    Properties 
    { 
        [HideInInspector] shader_is_using_HoyoToon_editor("", Float)=0
        // shader header
        [HideInInspector] ShaderBG ("UI/background", Float) = 0
        [HideInInspector] ShaderLogo ("UI/zzzlogo", Float) = 0
        [HideInInspector] CharacterLeft ("UI/zzzl", Float) = 0
        [HideInInspector] CharacterRight ("UI/zzzr", Float) = 0
        [HideInInspector] shader_is_using_hoyeditor ("", Float) = 0
		[HideInInspector] footer_github ("{texture:{name:hoyogithub},action:{type:URL,data:https://github.com/HoyoToon/HoyoToon},hover:Github}", Float) = 0
		[HideInInspector] footer_discord ("{texture:{name:hoyodiscord},action:{type:URL,data:https://discord.gg/hoyotoon},hover:Discord}", Float) = 0
        // header End
        [Toggle] _ShowTextureTips ("Show Texture Tips", Float) = 0
        [HoyoToonWideEnum(Base, 0, Face, 1, Eye, 2, Shadow, 3, Hair, 4, EyeHighlight, 5, EyeShadow, 6)] _MaterialType ("Material Type", Float) = 0
        [HideInInspector] start_main ("Main", Float) = 0
            // _Color ("Main Color", Color) = (1,1,1,1) 
            _MainTex ("Diffuse Texture", 2D) = "white" {}
            [Helpbox] _LightTexHelp ("The Light Texture changes depending on if its hair/base or face. For Faces the R is the normal SDF that we're used to for Genshin and Honkai games. G is a cheek and nose highlight. B is the outline threshold but after 1.7 that was moved into the vertex colors. And finally A is the under chin AO. For Bodies and hair, the R and G are actually Normal maps. The Green is the a shadow map like the ones that show up in the Genshin lightmaps. These commonly have the _N suffix to the texture names.--{condition_show:{type:PROPERTY_BOOL,data:_ShowTextureTips==1.0}}", Float) = 0
            _LightTex ("Light Texture", 2D) = "linearGray" { }
            [Helpbox] _OtherDataTexHelp ("The Other Data Texture is a packed texture where each channel has a different use. The R channel is the material ID texture, think like Genshin's LightMap Alpha channel. The G channel is the metallic texture. The B channel is the specular mask, on hair you will see this area be where the hair highlight is. A is unused. These commonly have the _M suffix to the texture names.--{condition_show:{type:PROPERTY_BOOL,data:_ShowTextureTips==1.0}}", Float) = 0
            _OtherDataTex ("Other Data Tex", 2D) = "white" { }
            [Helpbox] _OtherDataTex2Help ("The Other Data 2 Texture is a packed texture where each channel has a different use. The R channel is a transparency texture, on hair you will see this area be where the stencils show through. The G channel is the smoothness texture. The B channel is the emission mask, on hair you will see this area be where the hair highlight is. A is unused. These commonly have the _A suffix to the texture names.--{condition_show:{type:PROPERTY_BOOL,data:_ShowTextureTips==1.0}}", Float) = 0
            _OtherDataTex2 ("Other Data 2 Tex", 2D) = "white" { }
            _EyeColorMap ("Eye Color Map--{condition_show:{type:PROPERTY_BOOL,data:_MaterialType==2.0}}", 2D) = "white" { }
            [Toggle] _LegacyOtherData ("Use Legacy Other Data 2", Float) = 0
            [HideInInspector] start_alpha("Alpha Options", Float) = 0
                [Toggle] _UseAlpha ("Use Alpha", Float) = 0
                _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.0
            [HideInInspector] end_alpha ("", Float) = 0

            [HideInInspector] start_uvoptions ("UV Options", Float) = 0
                [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _DoubleUV ("Double/Symmetry UV Selector", Float) = 1
                [Toggle] _SymmetryUV ("Symmetry UV", Float) = 0
                [Toggle] _DoubleSided ("Double Sided", Float) = 0
            [HideInInspector] end_uvoptions ("", Float) = 0
            
            [HideInInspector] start_lighting ("Lighting Options", Float) = 0
                [Toggle] _MultiLight ("Enable Lighting from Multiple Sources", Float) = 1
                [Toggle] _ApplyLighting ("Apply Enviro Lighting", Float) = 1
                [Toggle] _FilterLight ("Limit Spot/Point Light Intensity", Float) = 1 // because VRC world creators are fucking awful at lighting you need to do shit like this to not blow your models the fuck up
            [HideInInspector] end_lighting ("", Float) = 0
            [HideInInspector] start_color("Color", Float) = 0
                _Color ("Color 1", Color) = (1,1,1,1) 
                _Color2 ("Color 2", Color) = (1,1,1,1) 
                _Color3 ("Color 3", Color) = (1,1,1,1) 
                _Color4 ("Color 4", Color) = (1,1,1,1) 
                _Color5 ("Color 5", Color) = (1,1,1,1) 
            [HideInInspector] end_color ("", Float) = 0
            [HideInInspector] start_directions("Facing Directions", Float) = 0
                _headForwardVector ("Forward Vector | XYZ", Vector) = (0, 0, 1, 0)
                _headRightVector ("Right Vector | XYZ", Vector) = (-1, 0, 0, 0)
                _headUpVector ("Up Vector || XYZ", Vector) = (0, 1, 0, 0)
            [HideInInspector] end_directions ("", Float) = 0
            
            [HideInInspector] start_materialID ("Material ID", Float) = 0
                [Toggle] _UseLegacyFace ("Use Legacy Face ID", Float) = 0
                _HairMatId ("Hair ID", Float) = 0.0
                _SkinMatId ("Skin Material ID", Float) = 0.0
            [HideInInspector] end_materialID ("", Float) = 0
            [HideInInspector] end_main ("", Float) = 0
            
            [HideInInspector] start_normalmap("Normal Map", Float) = 0
                [Toggle] _UseBumpMap ("Use Bump Map", Float) = 1
                _BumpScale ("Normal Scale", Range(-5, 5)) = 1
            [HideInInspector] end_normalmap ("", Float) = 0
        // main End
        [HideInInspector] start_face ("Face--{condition_show:{type:PROPERTY_BOOL,data:_MaterialType==1.0}}", Float) = 0
            [HideInInspector] start_nosesettings ("Nose Settings", Float) = 0    
                _NoseLineLkDnDisp ("Nose Line Down Threshold", Float) = 0.575
                _NoseLineHoriDisp ("Nose Line Horizontal Threshold", Float) = 0.865
                // not needed to be exposed but in case they need to be edited via script
                [HideInInspector] _NoseSmoothX ("Nose Smooth X", Float) = 0.0
                [HideInInspector] _NoseSmoothY ("Nose Smooth Y", Float) = 0.1
            [HideInInspector] end_nosesettings ("", Float) = 0
            [HideInInspector] start_headmatrix ("Head Matrix", Float) = 0
                [Helpbox] _HeadWarning ("Do not edit these unless you're absolutely sure you know what you're doing.", Float) = 0
                _HeadMatrixWS2OS0 ("Head Matrix Row 0", Vector) = (1.0, 0.0, 0.0, 0.0)
                _HeadMatrixWS2OS1 ("Head Matrix Row 1", Vector) = (0.0, 1.0, 0.0, 0.0)
                _HeadMatrixWS2OS2 ("Head Matrix Row 2", Vector) = (0.0, 0.0, 1.0, 0.0)
                _HeadMatrixWS2OS3 ("Head Matrix Row 3", Vector) = (1.0, 1.0, 1.0, 0.0)
            [HideInInspector] end_headmatrix ("", Float) = 0
        [HideInInspector] end_face ("", Float) = 0

        [HideInInspector] start_shadow ("Shadow", Float) = 0
            [Toggle] _UseSelfShadow ("Use Self Shadow", Float) = 0
            _AlbedoSmoothness ("Albedo Smoothness", Float) = 0.05
            [HideInInspector] start_shadow_colors ("Shadow Colors", Float) = 0
                _ShadowColor ("Shadow Color 1", Color) = (0.6,0.6,0.6,1)
                _ShadowColor2 ("Shadow Color 2", Color) = (0.6,0.6,0.6,1)
                _ShadowColor3 ("Shadow Color 3", Color) = (0.6,0.6,0.6,1)
                _ShadowColor4 ("Shadow Color 4", Color) = (0.6,0.6,0.6,1)
                _ShadowColor5 ("Shadow Color 5", Color) = (0.6,0.6,0.6,1)
            [HideInInspector] end_shadow_colors ("", Float) = 0
            [HideInInspector] start_shallow_color ("Shallow Colors", Float) = 0
                _ShallowColor ("Shallow Color 1", Color) = (0.8,0.8,0.8,1)
                _ShallowColor2 ("Shallow Color 2", Color) = (0.8,0.8,0.8,1)
                _ShallowColor3 ("Shallow Color 3", Color) = (0.8,0.8,0.8,1)
                _ShallowColor4 ("Shallow Color 4", Color) = (0.8,0.8,0.8,1)
                _ShallowColor5 ("Shallow Color 5", Color) = (0.8,0.8,0.8,1)
            [HideInInspector] end_shallow_color ("", Float) = 0
            // scripted values for post colors on the shadows, set to a nice default
            [HideInInspector] start_post_colors ("Post Colors", Float) = 0
                _PostShallowTint ("Post Shallow Tint", Color) = (0.956862748,0.960784256,0.9019608,1)
                _PostShallowFadeTint ("Post Shallow Fade Tint", Color) = (0.8745098,0.8,0.7921569,1)
                _PostShadowTint ("Post Shadow Tint", Color) = (0.8509804,0.78039217,0.772549033,1)
                _PostShadowFadeTint ("Post Shadow Fade Tint", Color) = (0.929411769,0.8509804,0.8431372,1)
                _PostFrontTint ("Post Front Tint", Color) = (1,0.996078432,0.929411769,1)
                _PostSssTint ("Post SSS Tint", Color) = (1,0.9481711,0.929411769,1)
            [HideInInspector] end_post_colors ("", Float) = 0
        [HideInInspector] end_shadow ("", Float) = 0

        [HideInInspector] start_reflection ("Reflections", Float) = 0
            [HideInInspector] start_rflmatcap ("Matcap--{condition_show:{type:PROPERTY_BOOL,data:_MaterialType!=1.0}, reference_property:_MatCap}", Float) = 0
                [Toggle] _MatCap ("Use Matcap", Float) = 0
                [Toggle] _UseMatCapMask ("Use Matcap Mask", Float) = 0
                _MatCapTex ("Matcap", 2D) = "black" {}
                _MatCapTex2 ("Matcap 2", 2D) = "black" {}
                _MatCapTex3 ("Matcap 3", 2D) = "black" {}
                _MatCapTex4 ("Matcap 4", 2D) = "black" {}
                _MatCapTex5 ("Matcap 5", 2D) = "black" {}
                [HideInInspector] start_matcap_params ("Matcap Parameters", Float) = 0
                    [HideInInspector] start_matcap_tint ("Matcap Tint", Float) = 0
                        _MatCapColorTint ("Color Tint 1", Color) = (1,1,1,1)
                        _MatCapColorTint2 ("Color Tint 2", Color) = (1,1,1,1)
                        _MatCapColorTint3 ("Color Tint 3", Color) = (1,1,1,1)
                        _MatCapColorTint4 ("Color Tint 4", Color) = (1,1,1,1)
                        _MatCapColorTint5 ("Color Tint 5", Color) = (1,1,1,1)
                    [HideInInspector] end_matcap_tint ("", Float) = 0

                    [HideInInspector] start_alpha_burst ("Alpha Burst", Float) = 0
                        _MatCapAlphaBurst ("Alpha Burst 1", Float) = 1.0
                        _MatCapAlphaBurst2 ("Alpha Burst 2", Float) = 0.3
                        _MatCapAlphaBurst3 ("Alpha Burst 3", Float) = 1.0
                        _MatCapAlphaBurst4 ("Alpha Burst 4", Float) = 0.8
                        _MatCapAlphaBurst5 ("Alpha Burst 5", Float) = 0.8
                    [HideInInspector] end_alpha_burst ("", Float) = 0

                    [HideInInspector] start_blend_mode ("Blend Mode", Float) = 0
                        [Enum(AlphaBlended, 0, Add, 1, Overlay, 2)] _MatCapBlendMode ("Blend Mode 1", Float) = 2
                        [Enum(AlphaBlended, 0, Add, 1, Overlay, 2)] _MatCapBlendMode2 ("Blend Mode 2", Float) = 1
                        [Enum(AlphaBlended, 0, Add, 1, Overlay, 2)] _MatCapBlendMode3 ("Blend Mode 3", Float) = 0
                        [Enum(AlphaBlended, 0, Add, 1, Overlay, 2)] _MatCapBlendMode4 ("Blend Mode 4", Float) = 2
                        [Enum(AlphaBlended, 0, Add, 1, Overlay, 2)] _MatCapBlendMode5 ("Blend Mode 5", Float) = 2
                    [HideInInspector] end_blend_mode ("", Float) = 0

                    [HideInInspector] start_color_burst ("Color Burst", Float) = 0
                        _MatCapColorBurst ("Color Burst 1", Float) = 0.15
                        _MatCapColorBurst2 ("Color Burst 2", Float) = 1.5
                        _MatCapColorBurst3 ("Color Burst 3", Float) = 1.0
                        _MatCapColorBurst4 ("Color Burst 4", Float) = 0.2
                        _MatCapColorBurst5 ("Color Burst 5", Float) = 0.2
                    [HideInInspector] end_color_burst ("", Float) = 0

                    [HideInInspector] start_tex_id ("Texture ID", Float) = 0
                        _MatCapTexID ("Tex ID 1", Float) = 0.0
                        _MatCapTexID2 ("Tex ID 2", Float) = 1.0
                        _MatCapTexID3 ("Tex ID 3", Float) = 100.0
                        _MatCapTexID4 ("Tex ID 4", Float) = 0.0
                        _MatCapTexID5 ("Tex ID 5", Float) = 0.0
                    [HideInInspector] end_tex_id ("", Float) = 0

                    [HideInInspector] start_speed ("Speed", Float) = 0
                        _MatCapUSpeed ("U Speed 1", Float) = 0.0
                        _MatCapUSpeed2 ("U Speed 2", Float) = 0.0
                        _MatCapUSpeed3 ("U Speed 3", Float) = 0.0
                        _MatCapUSpeed4 ("U Speed 4", Float) = 0.0
                        _MatCapUSpeed5 ("U Speed 5", Float) = 0.0

                        _MatCapVSpeed ("V Speed 1", Float) = 0.0
                        _MatCapVSpeed2 ("V Speed 2", Float) = 0.0
                        _MatCapVSpeed3 ("V Speed 3", Float) = 0.0
                        _MatCapVSpeed4 ("V Speed 4", Float) = 0.0
                        _MatCapVSpeed5 ("V Speed 5", Float) = 0.0
                    [HideInInspector] end_speed ("", Float) = 0

                    [HideInInspector] start_refract ("Refract", Float) = 0
                        [Toggle] _MatCapRefract ("Refract 1", Float) = 0.0
                        [Toggle] _MatCapRefract2 ("Refract 2", Float) = 0.0
                        [Toggle] _MatCapRefract3 ("Refract 3", Float) = 0.0
                        [Toggle] _MatCapRefract4 ("Refract 4", Float) = 0.0
                        [Toggle] _MatCapRefract5 ("Refract 5", Float) = 0.0
                    [HideInInspector] end_refract ("", Float) = 0

                    [HideInInspector] start_refract_depth ("Refract Depth", Float) = 0
                        _RefractDepth ("Refract Depth 1", Range(0, 2)) = 0.5
                        _RefractDepth2 ("Refract Depth 2", Range(0, 2)) = 0.5
                        _RefractDepth3 ("Refract Depth 3", Range(0, 2)) = 0.5
                        _RefractDepth4 ("Refract Depth 4", Range(0, 2)) = 0.5
                        _RefractDepth5 ("Refract Depth 5", Range(0, 2)) = 0.5
                    [HideInInspector] end_refract_depth ("", Float) = 0

                    [HideInInspector] start_refract_param ("Refract WrapOffset", Float) = 0
                        _RefractParam ("Refract WrapOffset 1", Vector) = (5,5,0,0)
                        _RefractParam2 ("Refract WrapOffset 2", Vector) = (5,5,0,0)
                        _RefractParam3 ("Refract WrapOffset 3", Vector) = (5,5,0,0)
                        _RefractParam4 ("Refract WrapOffset 4", Vector) = (5,5,0,0)
                        _RefractParam5 ("Refract WrapOffset 5", Vector) = (5,5,0,0)
                    [HideInInspector] end_refract_param ("", Float) = 0

                [HideInInspector] end_matcap_params ("", Float) = 0
            [HideInInspector] end_rflmatcap ("", Float) = 0

            [HideInInspector] start_specular ("Specular--{condition_show:{type:PROPERTY_BOOL,data:_MaterialType!=1.0}}", Float) = 0
                _Glossiness ("Smoothness", Range(0, 1)) = 0.5
                _Metallic ("Metallic", Range(0, 1)) = 0
                _SpecIntensity ("Specular Intensity", Range(0, 1)) = 0.1
                _HeadSphereNormalCenter ("Head Sphere Normal Center", Vector) = (1, 1, 1, 0)
                [HideInInspector] start_highlight_shape ("Highlight Shape", Float) = 0
                    [Toggle]_HighlightShape ("Highlight Shape 1", Float) = 0
                    [Toggle]_HighlightShape2 ("Highlight Shape 2", Float) = 0
                    [Toggle]_HighlightShape3 ("Highlight Shape 3", Float) = 0
                    [Toggle]_HighlightShape4 ("Highlight Shape 4", Float) = 0
                    [Toggle]_HighlightShape5 ("Highlight Shape 5", Float) = 0
                [HideInInspector] end_highlight_shape ("", Float) = 0
                [HideInInspector] start_toon_specular ("Toon Specular", Float) = 0
                    _ToonSpecular ("Toon Specular 1", Range(0, 1)) = 0.01
                    _ToonSpecular2 ("Toon Specular 2", Range(0, 1)) = 0.01
                    _ToonSpecular3 ("Toon Specular 3", Range(0, 1)) = 0.01
                    _ToonSpecular4 ("Toon Specular 4", Range(0, 1)) = 0.01
                    _ToonSpecular5 ("Toon Specular 5", Range(0, 1)) = 0.01
                [HideInInspector] end_toon_specular ("", Float) = 0
                [HideInInspector] start_specular_range ("Specular Range", Float) = 0
                    _SpecularRange ("Specular Range 0", Range(0, 2)) = 1
                    _SpecularRange2 ("Specular Range 1", Range(0, 2)) = 1
                    _SpecularRange3 ("Specular Range 2", Range(0, 2)) = 1
                    _SpecularRange4 ("Specular Range 3", Range(0, 2)) = 1
                    _SpecularRange5 ("Specular Range 4", Range(0, 2)) = 1
                [HideInInspector] end_specular_range ("", Float) = 0
                [HideInInspector] start_shape_softness ("Shape Softness", Float) = 0
                    _ShapeSoftness ("Shape Softness 1", Range(0, 1)) = 0.1
                    _ShapeSoftness2 ("Shape Softness 2", Range(0, 1)) = 0.1
                    _ShapeSoftness3 ("Shape Softness 3", Range(0, 1)) = 0.1
                    _ShapeSoftness4 ("Shape Softness 4", Range(0, 1)) = 0.1
                    _ShapeSoftness5 ("Shape Softness 5", Range(0, 1)) = 0.1
                [HideInInspector] end_shape_softness ("", Float) = 0
                [HideInInspector] start_specular_color ("Specular Color", Float) = 0
                    [HDR] _SpecularColor ("Specular Color 1", Color) = (1,1,1,1)
                    [HDR] _SpecularColor2 ("Specular Color 2", Color) = (1,1,1,1)
                    [HDR] _SpecularColor3 ("Specular Color 3", Color) = (1,1,1,1)
                    [HDR] _SpecularColor4 ("Specular Color 4", Color) = (1,1,1,1)
                    [HDR] _SpecularColor5 ("Specular Color 5", Color) = (1,1,1,1)
                [HideInInspector] end_specular_color ("", Float) = 0
                [HideInInspector] start_model_size ("Model Size", Float) = 0
                    _ModelSize ("Model Size", Float) = 1
                    _ModelSize2 ("Model Size 2", Float) = 1
                    _ModelSize3 ("Model Size 3", Float) = 1
                    _ModelSize4 ("Model Size 4", Float) = 1
                    _ModelSize5 ("Model Size 5", Float) = 1
            [HideInInspector] end_model_size ("", Float) = 0
            [HideInInspector] end_specular ("", Float) = 0

            [HideInInspector] start_rimlight ("Rim Light--{reference_property:_RimGlow}", float) = 0
                [Toggle] _RimGlow ("Enable Rim Light", float) = 0
                _RimWidth ("Rim Width", float) = 1
                [HideInInspector] start_rimglowlight_color ("Rim Glow Light Color", Float) = 0
                    _RimGlowLightColor ("Rim Light Color 1", Color) = (1,1,1,1)
                    _RimGlowLightColor2 ("Rim Light Color 2", Color) = (1,1,1,1)
                    _RimGlowLightColor3 ("Rim Light Color 3", Color) = (1,1,1,1)
                    _RimGlowLightColor4 ("Rim Light Color 4", Color) = (1,1,1,1)
                    _RimGlowLightColor5 ("Rim Light Color 5", Color) = (1,1,1,1)
                [HideInInspector] end_rimglowlight_color ("", Float) = 0
                [HideInInspector] start_ui_sun_color ("Sun Color", Float) = 0
                    _UISunColor ("Sun Color 1", Color) = (1.0, 0.92, 0.9, 1.0)
                    _UISunColor2 ("Sun Color 2", Color) = (1.0, 0.92, 0.9, 1.0)
                    _UISunColor3 ("Sun Color 3", Color) = (1.0, 0.92, 0.9, 1.0)
                    _UISunColor4 ("Sun Color 4", Color) = (1.0, 0.92, 0.9, 1.0)
                    _UISunColor5 ("Sun Color 5", Color) = (1.0, 0.92, 0.9, 1.0)
                [HideInInspector] end_ui_sun_color ("", Float) = 0
            [HideInInspector] end_rimlight ("", Float) = 0
        [HideInInspector] end_reflection ("", Float) = 0

        [HideInInspector] start_special_effects ("Special Effects", Float) = 0
            [HideInInspector] start_emission ("Emission--{reference_property:_Emission}", Float) = 0
                [Toggle]_Emission ("Use Emission", Float) = 0.0
                [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
                [HDR] _EmissionColor2 ("Emission Color 2", Color) = (1,1,1,1)
                [HDR] _EmissionColor3 ("Emission Color 3", Color) = (1,1,1,1)
                [HDR] _EmissionColor4 ("Emission Color 4", Color) = (1,1,1,1)
                [HDR] _EmissionColor5 ("Emission Color 5", Color) = (1,1,1,1)
            [HideInInspector] end_emission ("", Float) = 0

            [HideInInspector] start_secondaryemission ("Secondary Emission--{reference_property:_SecondaryEmission}", Float) = 0
                [Toggle] _SecondaryEmission ("Use Secondary Emission", Float) = 0
                _SecondaryEmissionTex ("Emission Texture", 2D) = "white" {}
                _SecondaryEmissionTexSpeed ("Speed", Vector) = (0,0,0,0)
                _SecondaryEmissionTexRotation ("Rotation", Range(0,1)) = 0
                [HDR] _SecondaryEmissionColor ("Emission Color", Color) = (1,1,1,1)
                [Toggle] _SecondaryEmissionUseUV2 ("Use UV2", Float) = 0
                [Enum(R, 0, RGB, 1)] _SecondaryEmissionChannel ("Emission Channel", Float) = 0
                [Toggle] _MultiplyAlbedo ("Multiply Albedo", Float) = 1
                _SecondaryEmissionMaskTex ("Mask Tex", 2D) = "white" {}
                [Enum(R, 0, G, 1, B, 2)] _SecondaryEmissionMaskChannel ("Mask Channel", Float) = 0
            [HideInInspector] end_secondaryemission ("", Float) = 0
            // zzzs funny way of saying starcock
            [HideInInspector] start_screen_image ("Screen Image--{reference_property:_ScreenImage}", Float) = 0
                [Toggle] _ScreenImage ("Screen Image", Float) = 0
                [Enum(Screen, 0, Model World Position, 1, UV, 2)] _ScreenUVSource ("Screen UV Source", Float) = 2
                [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _ScreenUVEnum ("Screen UV Enum", Float) = 2
                _ScreenScale ("Screen Scale", Float) = 1
                _MultiplySrcColor ("Multiply Source Color", Float) = 0
                [HDR] _ScreenColor ("Screen Color", Color) = (1,1,1,1)
                _ScreenTex ("Screen Texture", 2D) = "gray" {}
                _ScreenTexRotation ("Rotation", Range(0,1)) = 0
                [Vector2] _ScreenTexRotationAxis ("Rotation Axis", Vector) = (0.5,0.5,0,0)
                _ScreenMask ("Screen Mask", 2D) = "white" {}
                [Enum(UV0, 0, UV1, 1)] _ScreenMaskUV ("Screen Mask UV", Float) = 0
                _ScreenImageUvMove ("UV Move", Vector) = (0,0,0,0)
                [Toggle] _Blink ("Blink", Float) = 0
                _BlinkFrequency ("Blink Frequency", Range(0,5)) = 1
                _BlinkOpacity ("Blink Opacity", Range(0,1)) = 0 
            [HideInInspector] end_screen_image ("", Float) = 0

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
                    _ColorHue2 ("Hue Shift 2", Range(0.0, 1.0)) = 0
                    _ColorHue3 ("Hue Shift 3", Range(0.0, 1.0)) = 0
                    _ColorHue4 ("Hue Shift 4", Range(0.0, 1.0)) = 0
                    _ColorHue5 ("Hue Shift 5", Range(0.0, 1.0)) = 0
                [HideInInspector] end_colorhue ("", Float) = 0
                // Outline Hue
                [HideInInspector] start_outlinehue ("Outline", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _OutlineMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableOutlineHue ("Enable Outline Hue Shift", Float) = 1
                    [Toggle] _AutomaticOutlineShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftOutlineSpeed ("Shift Speed", Float) = 0.0
                    _GlobalOutlineHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _OutlineHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _OutlineHue2 ("Hue Shift 2", Range(0.0, 1.0)) = 0
                    _OutlineHue3 ("Hue Shift 3", Range(0.0, 1.0)) = 0
                    _OutlineHue4 ("Hue Shift 4", Range(0.0, 1.0)) = 0
                    _OutlineHue5 ("Hue Shift 5", Range(0.0, 1.0)) = 0
                [HideInInspector] end_outlinehue ("", Float) = 0
                // Glow Hue
                [HideInInspector] start_glowhue ("Emission", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _EmissionMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableEmissionHue ("Enable Emission Hue Shift", Float) = 1
                    [Toggle] _AutomaticEmissionShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftEmissionSpeed ("Shift Speed", Float) = 0.0
                    _GlobalEmissionHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _EmissionHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _EmissionHue2 ("Hue Shift 2", Range(0.0, 1.0)) = 0
                    _EmissionHue3 ("Hue Shift 3", Range(0.0, 1.0)) = 0
                    _EmissionHue4 ("Hue Shift 4", Range(0.0, 1.0)) = 0
                    _EmissionHue5 ("Hue Shift 5", Range(0.0, 1.0)) = 0
                [HideInInspector] end_glowhue ("", Float) = 0
                // Rim Hue
                [HideInInspector] start_rimhue ("Rim", Float) = 0
                    [Enum(R, 0, G, 1, B, 2, A, 3)] _RimMaskSource ("Hue Mask Channel--{condition_show:{type:PROPERTY_BOOL,data:_UseHueMask==1.0}}", Float) = 0
                    [Toggle] _EnableRimHue ("Enable Rim Hue Shift", Float) = 1
                    [Toggle] _AutomaticRimShift ("Enable Auto Hue Shift", Float) = 0
                    _ShiftRimSpeed ("Shift Speed", Float) = 0.0
                    _GlobalRimHue ("Main Hue Shift", Range(0.0, 1.0)) = 0
                    _RimHue ("Hue Shift 1", Range(0.0, 1.0)) = 0
                    _RimHue2 ("Hue Shift 2", Range(0.0, 1.0)) = 0
                    _RimHue3 ("Hue Shift 3", Range(0.0, 1.0)) = 0
                    _RimHue4 ("Hue Shift 4", Range(0.0, 1.0)) = 0
                    _RimHue5 ("Hue Shift 5", Range(0.0, 1.0)) = 0
                [HideInInspector] end_rimhue ("", Float) = 0
            [HideInInspector] end_hueshift ("", float) = 0

            [HideInInspector] start_lut("Built In Tonemapping--{reference_property:_EnableLUT}", Float) = 0
                [Toggle] _EnableLUT ("Enable LUT", float) = 0 
                _Lut2DTex ("LUT", 2D) = "black" {}
                _Lut2DTexParam ("LUT Paramters", Vector) = (0.00098, 0.03125, 31, 0)
            [HideInInspector] end_lut ("", Float) = 0
        [HideInInspector] end_special_effects ("", Float) = 0

        [HideInInspector] start_outline ("Outline", Float) = 0
            [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _NormalUV ("UV Normal Map Selector for Normal Map", Float) = 3
            [Toggle] _Outline ("Outline", Float) = 0.0
            [Toggle] _UseLightMapOL ("Legacy Light Map Outline", Float) = 0
            [Toggle] _DisableFOVScalingOL ("Disable FOV Scaling", Float) = 0
            // _MaxOutlineZOffset ("Max Outline Z Offset", Float) = 0.0
            [Toggle]_OutlineZOff ("Outline Z Offset Disable", Float) = 0
            _OutlineWidth ("Outline Width", Float) = 0.0
            // _OutlineWidthUIAdjustment ("Outline Width UI Adjustment", Float) = 0.0
            [HideInInspector] start_outline_color ("Outline Color", Float) = 0
                _OutlineColor ("Outline Color", Color) = (1,1,1,1)
                _OutlineColor2 ("Outline Color 2", Color) = (1,1,1,1)
                _OutlineColor3 ("Outline Color 3", Color) = (1,1,1,1)
                _OutlineColor4 ("Outline Color 4", Color) = (1,1,1,1)
                _OutlineColor5 ("Outline Color 5", Color) = (1,1,1,1)
            [HideInInspector] end_outline_color ("", Float) = 0
        [HideInInspector] end_outline ("", Float) = 0

        [HideInInspector] start_stencil_options ("Rendering Options", Float) = 0
            [HideInInspector] start_stencil ("Stencil Options", Float) = 0
            [Helpbox] _StencilHelp0("The stencil alpha is what actually makes the fade work, stencils will always be applied no matter if your shader has parameters for it. It just sets them to default if not specified.", Float) = 0
            [HideInInspector] start_stencilalpha ("Fade Controls", Float) = 0
                    [Toggle] _EnableStencil ("Use Alpha Stencils", Float) = 0
                    _MinStencilAlpha ("Mininmum Stencil Opacity", Float) = 0.2
                [HideInInspector] end_stencilalpha ("", Float) = 0

                [HideInInspector] start_stencil_a ("Stencil A Settings", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)]       _StencilPassA ("Stencil Pass Op A", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompA ("Stencil Compare Function A", Float) = 8
                [HideInInspector] end_stencil_a ("", Float) = 0
                [HideInInspector] start_stencil_b ("Stencil B Settings", Float) = 0
                    [Enum(UnityEngine.Rendering.StencilOp)]       _StencilPassB ("Stencil Pass Op B", Float) = 0
                    [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompB ("Stencil Compare Function B", Float) = 8
                [HideInInspector] end_stencil_b ("", Float) = 0
                [IntRange] _StencilRef ("Stencil Reference Value", Range(0, 255)) = 0 
            [HideInInspector] end_stencil ("", Float) = 0
            [HideInInspector] start_blending ("Blending Options", Float) = 0
                [Enum(UnityEngine.Rendering.BlendMode)]       _SrcBlend ("Source Blend", Int) = 1
                [Enum(UnityEngine.Rendering.BlendMode)]       _DstBlend ("Destination Blend", Int) = 0
                [Enum(UnityEngine.Rendering.BlendMode)]       _SrcBlendB ("Source Blend B", Int) = 1
                [Enum(UnityEngine.Rendering.BlendMode)]       _DstBlendB ("Destination Blend B", Int) = 0
            [HideInInspector] end_blending ("", Float) = 0
            [Enum(Off, 0, On, 1)] _ZWrite ("Zwrite", Int) = 1
            [Enum(UnityEngine.Rendering.CompareFunction)] _ZTesting ("ZTest", Float) = 2 // mihoyo uses this in their shader but set it to something that doesnt work 
            [Toggle] _ZClip ("ZClip", Int) = 1  
        [HideInInspector] end_stencil_options ("", Float) = 0

        [HideInInspector] start_debug("Debug Options", Float) = 0
            [Toggle] _DebugMode ("Enable Debug Mode", float) = 0
            [Enum(Off, 0, RGB, 1, A, 2)] _DebugDiffuse("Diffuse Debug Mode", Float) = 0
            [Enum(Off, 0, XY (Normal Map), 1, B (Diffuse Bias), 2, A, 3)] _DebugLightMap ("Light Map Debug Mode", Float) = 0
            [Enum(Off, 0, R (Material ID), 1, G (Metallic Texture), 2, B (Specular Mask), 3, A, 4)] _DebugOtherData ("Other Data Debug Mode", Float) = 0
            [Enum(Off, 0, R (Transparency), 1, G (Smoothness Texture), 2, B (Emission Mask), 3, A, 4)] _DebugOtherData2 ("Other Data Debug Mode", Float) = 0
            [Enum(Off, 0, R (Outline Threshold), 1, G, 2, B, 3, A, 4)] _DebugVertexColor ("Vertex Color Debug Mode", Float) = 0
            [Enum(Off, 0, UV0, 1, UV1 (Seconadary UV), 2, UV2 (Projected UV), 3, UV3 (Outline Normal Map), 4, UV3 (Outline Normal Map Encoded), 5)] _DebugUV ("UV Debug Mode", Float) = 0
            [Enum(Off, 0, On, 1)] _DebugTangent ("Tangents Debug Mode", Float) = 0
            [Enum(Off, 0, Original (Encoded), 1, Original (Raw), 2, Bumped (Encoded), 3, Bumped (Raw), 4)] _DebugNormalVector ("Normals Debug Mode", Float) = 0 
            [Enum(Off, 0, On, 1)] _DebugMatcap ("Matcap Debug Mode", Float) = 0
            [Enum(Off, 0, On, 1)] _DebugSpecular ("Specular Debug Mode", Float) = 0
            [Enum(Off, 0, On, 1)] _DebugRimLight ("Rim Light Debug Mode", Float) = 0
            [Enum(Off, 0, Forward, 1, Right, 2, Up, 3)] _DebugFaceVector ("Facing Vector Debug Mode", Float) = 0
            [Enum(Off, 0, Basic Emission, 1, Secondary Emission, 2, Both, 3)] _DebugEmission ("Emission Debug Mode", Float) = 0 
            [Enum(Off, 0, On, 1)] _DebugLights ("Lights Debug Mode", Float) = 0
            [HoyoToonWideEnum(Off, 0, Materail ID 1, 1, Material ID 2, 2, Material ID 3, 3, Material ID 4, 4, Material ID 5, 5, All(Color Coded), 6)] _DebugMaterialIDs ("Material ID Debug Mode", Float) = 0
        [HideInInspector] end_debug("Debug Options", Float) = 0    
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE

        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityShaderVariables.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "UnityInstancing.cginc"
        #include "Include/zzz-declarations.hlsl"
        #include "Include/zzz-inputs.hlsl"
        #include "Include/zzz-common.hlsl"
        ENDHLSL

        Pass
        {
            Name "Character Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
				ref [_StencilRef]
                Comp [_StencilCompA]
				Pass [_StencilPassA]
            }
            ZWrite [_ZWrite]
            ZTest [_ZTesting]
            ZClip [_ZClip]
            Cull Off
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma multi_compile _IS_PASS_BASE
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "Include/zzz-program.hlsl"
            ENDHLSL
        }  
        
        Pass
        {
            Name "Stencil Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Blend [_SrcBlendB] [_DstBlendB]
            Stencil
            {
				ref [_StencilRef]
                Comp [_StencilCompB]
				Pass [_StencilPassB]
            }
            Cull Off
            HLSLPROGRAM
            #define _IS_STENCIL
            #pragma multi_compile _IS_PASS_BASE
            #pragma multi_compile_fwdbase
            #pragma vertex vs_model
            #pragma fragment ps_model
            #include "Include/zzz-program.hlsl"
            ENDHLSL
        }   

        Pass // Character Light Pass
        {
            Name "Character Light Pass"
            Tags{ "LightMode" = "ForwardAdd" }
            Cull [_Cull]
            // Stencil
            // {
			// 	ref [_StencilRef]
            //     Comp [_StencilCompA]
			// 	Pass [_StencilPassA]
            // }
            ZWrite Off
            Blend One One     
            
            HLSLPROGRAM
            
            #pragma multi_compile_fwdadd
            #pragma multi_compile _IS_PASS_LIGHT

            #pragma vertex vs_model
            #pragma fragment ps_model 

            #include "Include/zzz-program.hlsl"
            ENDHLSL
        }  

        Pass
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
				ref 255
                Comp Always
                Pass Keep
            }
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex vs_outline
            #pragma fragment ps_outline
            #include "Include/zzz-program.hlsl"
            ENDHLSL
        }

        // UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
        Pass // Shadow Pass, this ensures the model shows up in CameraDepthTexture
        {
            Name "Shadow Pass"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_Cull]
            // Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            
            #pragma multi_compile_fwdbase
            
            #pragma vertex vs_shadow
            #pragma fragment ps_shadow

            #include "Include/zzz-program.hlsl"
            ENDHLSL
        } 

    }

    CustomEditor "HoyoToon.ShaderEditor"
}
