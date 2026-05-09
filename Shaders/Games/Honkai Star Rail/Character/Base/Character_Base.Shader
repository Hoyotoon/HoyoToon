Shader "HoyoToon/Honkai Star Rail/Character/Base"
{
    Properties
    {
        [MaterialIDCount(8)] _MateirialIDCount ("Material ID Count", Float) = 8
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Options)] _Options ("Options", Float) = 0
        [SubGroup(Options, Rendering)] _RenderMode ("Rendering", Float) = 0
            [MaterialID(0)] [SubKeyword(Rendering, _IS_DUALFACE)] _EnableDualFace ("Enable Dual Face Rendering", Float) = 0
                [SubEnum(Rendering, UV1, 0, UV2, 1)] _UVChannelFront ("UV Channel for Front", Float) = 0
                [SubEnum(Rendering, UV1, 0, UV2, 1)] _UVChannelBack ("UV Channel for Back", Float) = 1
                [Sub(Rendering)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
                [Sub(Rendering)] _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
                [Sub(Rendering)] _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
                [Sub(Rendering)] _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
                [Sub(Rendering)] _StencilRef ("Stencil Ref", Float) = 16
                [Sub(Rendering)] _StencilOP ("Stencil Op", Float) = 2
                [Sub(Rendering)] _StencilComp ("Stencil Comp", Float) = 8
                [Sub(Rendering)] _StencilMask ("Stencil Read Mask", Float) = 255
                [Sub(Rendering)] _RenderingMode ("Rendering Mode", Float) = 0
                [Sub(Rendering)] _ZWrite ("ZWrite", Float) = 1
                [Sub(Rendering)] _SrcBlend ("Src Blend", Float) = 1
                [Sub(Rendering)] _DstBlend ("Dst Blend", Float) = 0
                [Sub(Rendering)] _CullMode ("Cull Mode", Float) = 2
                [Sub(Rendering)] _ZClip ("ZClip", Float) = 0
            [SubGroup(Options, AlphaClipping)] _Clipping ("Alpha Clipping", Float) = 0
            [SubKeyword(AlphaClipping, _FRAGMENT_CLIP)] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
                [Sub(AlphaClipping)] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
                [Sub(AlphaClipping)] _AlphaTestThreshold ("AlphaTest Threshold", Range(0, 1)) = 0.5
            [SubGroup(Options, SelfShadow)] _SelfShadow ("Self Shadow", Float) = 0
            [Sub(SelfShadow)] _ReceiveShadows ("Receive Shadows", Float) = 1
            [Sub(SelfShadow)] _SelfShadowDepthOffset ("_SelfShadowDepthOffset", Float) = 0
            [Sub(SelfShadow)] _SelfShadowSampleOffset ("_SelfShadowSampleOffset", Float) = 0.001
            [Sub(SelfShadow)] _SelfShadowDarken ("_SelfShadowDarken", Float) = 0.6
            [SubToggle(Options)] _HideCharaParts ("Hide Chara Parts", Float) = 0
            [Sub(Options)] _ShowPartID ("Show Part ID", Float) = 0
            [Sub(Options)] VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
            [Sub(Options)] IsMonster ("Is Monster", Float) = 0
            [Sub(Options)] CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        [SubGroup(MainGroup, MainTextures)] _Maps ("Main Textures", Float) = 0
            [Tex(MainTextures, small, collapsed)] _MainTex ("Diffuse", 2D) = "white" { }
            [Tex(MainTextures, small, collapsed)] _LightMap ("Light Map", 2D) = "grey" { }
            [SubGroup(MainGroup, MaterialLUT)] _MaterialLUTGroup ("Material LUT", Float) = 0
            [Sub(MaterialLUT)] _UseMaterialValuesLUT ("Use Mat Lut", Float) = 0
            [Tex(MaterialLUT)] _MaterialValuesPackLUT ("Mat Pack LUT", 2D) = "white" { }
        [SubGroup(MainGroup, MainColors)] _MainColors("Main Colors", Float) = 0
        [Sub(MainColors)] [HideInInspector] _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        [Sub(MainColors)] _Color ("Color", Color) = (1,1,1,1)
        [Sub(MainColors)] _BackColor ("BackColor", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [Sub(MainColors)] [HideInInspector] _AddColor ("Add Color", Color) = (0,0,0,0)
        [Main(NormalMap, _, off, off)] _NormalGroup ("Normal Map", Float) = 0
        [SubKeyword(NormalMap, _USE_NORMAL_MAP)] _UseNormalMap ("UseNormalMap", Float) = 0
        [Tex(NormalMap, small, collapsed)] _NormalMap ("NormalMap", 2D) = "bump" { }
        [Sub(NormalMap)] _NormalScale ("NormalScale", Range(0, 4)) = 1

        [Main(LightingGroup, _, off, off)] _LightingGroup ("Lighting", Float) = 0
            [SubGroup(LightingGroup, ShadowsGroup)] _ShadowGroup ("Shadow", Float) = 0
                [Tex(ShadowsGroup, ramp)] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
                [Tex(ShadowsGroup, ramp)] _DiffuseRampMultiTex ("Shadow Multiple Ramp", 2D) = "white" { }
                [Sub(ShadowsGroup)] _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
                [SubToggle(ShadowsGroup)] _ShadowBoost ("Shadow Boost Enable", Float) = 0
                [Sub(ShadowsGroup)] _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
            [SubGroup(LightingGroup, SpecularGroup)] _SpecularGroup ("Specular Group", Float) = 0
                [SubGroup(SpecularGroup, SpecularColor)] _SpecularColor ("Specular Color", Float) = 0
                [MaterialID(0)] [Sub(SpecularColor)] _SpecularColor0 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(1)] [Sub(SpecularColor)] _SpecularColor1 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(2)] [Sub(SpecularColor)] _SpecularColor2 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(3)] [Sub(SpecularColor)] _SpecularColor3 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(4)] [Sub(SpecularColor)] _SpecularColor4 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(5)] [Sub(SpecularColor)] _SpecularColor5 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(6)] [Sub(SpecularColor)] _SpecularColor6 ("Specular Color", Color) = (1,1,1,1)
                [MaterialID(7)] [Sub(SpecularColor)] _SpecularColor7 ("Specular Color", Color) = (1,1,1,1)
            [SubGroup(SpecularGroup, SpecularParams)] _SpecularParams ("Specular Params", Float) = 0
            // [SubGroup(SpecularParams, Shininess)] _Shininess ("Shininess", Float) = 0
                [Sub(SpecularParams)] [MaterialID(0)] _SpecularShininess0 ("Shininess 0", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(1)] _SpecularShininess1 ("Shininess 1", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(2)] _SpecularShininess2 ("Shininess 2", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(3)] _SpecularShininess3 ("Shininess 3", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(4)] _SpecularShininess4 ("Shininess 4", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(5)] _SpecularShininess5 ("Shininess 5", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(6)] _SpecularShininess6 ("Shininess 6", Range(0.1, 500)) = 10
                [Sub(SpecularParams)] [MaterialID(7)] _SpecularShininess7 ("Shininess 7", Range(0.1, 500)) = 10
            // [SubGroup(SpecularParams, Roughness)] _Roughness ("Roughness", Float) = 0
                [MaterialID(0)] [Sub(SpecularParams)] _SpecularRoughness0 ("Specular Roughness 0", Range(0, 1)) = 0.02
                [MaterialID(1)] [Sub(SpecularParams)] _SpecularRoughness1 ("Specular Roughness 1", Range(0, 1)) = 0.02
                [MaterialID(2)] [Sub(SpecularParams)] _SpecularRoughness2 ("Specular Roughness 2", Range(0, 1)) = 0.02
                [MaterialID(3)] [Sub(SpecularParams)] _SpecularRoughness3 ("Specular Roughness 3", Range(0, 1)) = 0.02
                [MaterialID(4)] [Sub(SpecularParams)] _SpecularRoughness4 ("Specular Roughness 4", Range(0, 1)) = 0.02
                [MaterialID(5)] [Sub(SpecularParams)] _SpecularRoughness5 ("Specular Roughness 5", Range(0, 1)) = 0.02
                [MaterialID(6)] [Sub(SpecularParams)] _SpecularRoughness6 ("Specular Roughness 6", Range(0, 1)) = 0.02
                [MaterialID(7)] [Sub(SpecularParams)] _SpecularRoughness7 ("Specular Roughness 7", Range(0, 1)) = 0.02
            // [SubGroup(SpecularParams, Intensity)] _Intensity ("Intensity", Float) = 1
                [MaterialID(0)]  [Sub(SpecularParams)] _SpecularIntensity0 ("Intensity 0", Range(0, 50)) = 1
                [MaterialID(1)]  [Sub(SpecularParams)] _SpecularIntensity1 ("Intensity 1", Range(0, 50)) = 1
                [MaterialID(2)]  [Sub(SpecularParams)] _SpecularIntensity2 ("Intensity 2", Range(0, 50)) = 1
                [MaterialID(3)]  [Sub(SpecularParams)] _SpecularIntensity3 ("Intensity 3", Range(0, 50)) = 1
                [MaterialID(4)]  [Sub(SpecularParams)] _SpecularIntensity4 ("Intensity 4", Range(0, 50)) = 1
                [MaterialID(5)]  [Sub(SpecularParams)] _SpecularIntensity5 ("Intensity 5", Range(0, 50)) = 1
                [MaterialID(6)]  [Sub(SpecularParams)] _SpecularIntensity6 ("Intensity 6", Range(0, 50)) = 1
                [MaterialID(7)]  [Sub(SpecularParams)] _SpecularIntensity7 ("Intensity 7", Range(0, 50)) = 1
        [SubGroup(LightingGroup, RimLights)] _RimLightGroup ("Rim Lights", Float) = 0
            [SubGroup(RimLights, RegularRim)] _RimLightGroupDummy("Screen Rim Light", Float) = 0
                [Sub(RegularRim)] _RimLightMode ("Use lightmap Red Channel for Mask", Range(0, 1)) = 1
                [Sub(RegularRim)] _Rimintensity ("Rim Intensity", Float) = 1
                [Sub(RegularRim)] _RimFeatherWidth ("Rim Feather Width", Float) = 0.01
                [Sub(RegularRim)] _RimWidth ("RimWidth", Float) = 1
                [Sub(RegularRim)] _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
                [Sub(RegularRim)] _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015 
                [SubGroup(RegularRim, RRParams)] _RimParams ("Rim Params", Float) = 0
                // [SubGroup(RegularRim, RRColor)] _RRColor ("Rim Color", Float) = 0
                    [MaterialID(0)] [Sub(RRParams)] _RimColor0 ("Color 0", Color) = (1,1,1,1)
                    [MaterialID(1)] [Sub(RRParams)] _RimColor1 ("Color 1", Color) = (1,1,1,1)
                    [MaterialID(2)] [Sub(RRParams)] _RimColor2 ("Color 2", Color) = (1,1,1,1)
                    [MaterialID(3)] [Sub(RRParams)] _RimColor3 ("Color 3", Color) = (1,1,1,1)
                    [MaterialID(4)] [Sub(RRParams)] _RimColor4 ("Color 4", Color) = (1,1,1,1)
                    [MaterialID(5)] [Sub(RRParams)] _RimColor5 ("Color 5", Color) = (1,1,1,1)
                    [MaterialID(6)] [Sub(RRParams)] _RimColor6 ("Color 6", Color) = (1,1,1,1)
                    [MaterialID(7)] [Sub(RRParams)] _RimColor7 ("Color 7", Color) = (1,1,1,1)
                // [SubGroup(RegularRim, RREdge)] _RRimEdge ("Rim Edge", Float) = 0
                    [MaterialID(0)] [Sub(RRParams)] _RimEdgeSoftness0 ("Softness 0", Range(0.01, 0.9)) = 0.1
                    [MaterialID(1)] [Sub(RRParams)] _RimEdgeSoftness1 ("Softness 1", Range(0.01, 0.9)) = 0.1
                    [MaterialID(2)] [Sub(RRParams)] _RimEdgeSoftness2 ("Softness 2", Range(0.01, 0.9)) = 0.1
                    [MaterialID(3)] [Sub(RRParams)] _RimEdgeSoftness3 ("Softness 3", Range(0.01, 0.9)) = 0.1
                    [MaterialID(4)] [Sub(RRParams)] _RimEdgeSoftness4 ("Softness 4", Range(0.01, 0.9)) = 0.1
                    [MaterialID(5)] [Sub(RRParams)] _RimEdgeSoftness5 ("Softness 5", Range(0.01, 0.9)) = 0.1
                    [MaterialID(6)] [Sub(RRParams)] _RimEdgeSoftness6 ("Softness 6", Range(0.01, 0.9)) = 0.1
                    [MaterialID(7)] [Sub(RRParams)] _RimEdgeSoftness7 ("Softness 7", Range(0.01, 0.9)) = 0.1
                // [SubGroup(RegularRim, RRType)] _RRType ("Rim Type", Float) = 0
                    [MaterialID(0)] [Sub(RRParams)] _RimType0 ("Blend Mode 0", Range(0, 1)) = 1
                    [MaterialID(1)] [Sub(RRParams)] _RimType1 ("Blend Mode 1", Range(0, 1)) = 1
                    [MaterialID(2)] [Sub(RRParams)] _RimType2 ("Blend Mode 2", Range(0, 1)) = 1
                    [MaterialID(3)] [Sub(RRParams)] _RimType3 ("Blend Mode 3", Range(0, 1)) = 1
                    [MaterialID(4)] [Sub(RRParams)] _RimType4 ("Blend Mode 4", Range(0, 1)) = 1
                    [MaterialID(5)] [Sub(RRParams)] _RimType5 ("Blend Mode 5", Range(0, 1)) = 1
                    [MaterialID(6)] [Sub(RRParams)] _RimType6 ("Blend Mode 6", Range(0, 1)) = 1
                    [MaterialID(7)] [Sub(RRParams)] _RimType7 ("Blend Mode 7", Range(0, 1)) = 1
                // [SubGroup(RegularRim, RRDarken)] _RRDarken ("Rim Darken", Float) = 0
                    [MaterialID(0)] [Sub(RRParams)] _RimDark0 ("Darken 0", Range(0, 1)) = 0.5
                    [MaterialID(1)] [Sub(RRParams)] _RimDark1 ("Darken 1", Range(0, 1)) = 0.5
                    [MaterialID(2)] [Sub(RRParams)] _RimDark2 ("Darken 2", Range(0, 1)) = 0.5
                    [MaterialID(3)] [Sub(RRParams)] _RimDark3 ("Darken 3", Range(0, 1)) = 0.5
                    [MaterialID(4)] [Sub(RRParams)] _RimDark4 ("Darken 4", Range(0, 1)) = 0.5
                    [MaterialID(5)] [Sub(RRParams)] _RimDark5 ("Darken 5", Range(0, 1)) = 0.5
                    [MaterialID(6)] [Sub(RRParams)] _RimDark6 ("Darken 6", Range(0, 1)) = 0.5
                    [MaterialID(7)] [Sub(RRParams)] _RimDark7 ("Darken 7", Range(0, 1)) = 0.5
            [SubGroup(RimLights, Fresnel)] _Fresnel("Fresnel", Float) = 0
                [Sub(Fresnel)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
                [Sub(Fresnel)] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
                [Sub(Fresnel)] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
            [SubKeyword(Fresnel, _RIM_BACKLIGHT_ON)] _EnableBackRimLight ("Enable Back Rim Light", Float) = 0
            [SubGroup(RimLights, RimShadow)] _RimShadowDummy ("Rim Shadow", Float) = 0
                [Sub(RimShadow)] _RimShadowCt ("Rim Shadow Ct", Float) = 1
                [Sub(RimShadow)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
                [Sub(RimShadow)] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
                [Sub(RimShadow)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
                [SubGroup(RimShadow, RimShadowParams)] _RimShadowParams ("Rim Shadow Params", Float) = 0
                // [SubGroup(RimShadowParams, RimShadowColor)] _RimShadowColor ("Color", Float) = 0
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowColor0 ("Color 0", Color) = (1,1,1,1)
                    [MaterialID(1)] [Sub(RimShadowParams)] _RimShadowColor1 ("Color 1", Color) = (1,1,1,1)
                    [MaterialID(2)] [Sub(RimShadowParams)] _RimShadowColor2 ("Color 2", Color) = (1,1,1,1)
                    [MaterialID(3)] [Sub(RimShadowParams)] _RimShadowColor3 ("Color 3", Color) = (1,1,1,1)
                    [MaterialID(4)] [Sub(RimShadowParams)] _RimShadowColor4 ("Color 4", Color) = (1,1,1,1)
                    [MaterialID(5)] [Sub(RimShadowParams)] _RimShadowColor5 ("Color 5", Color) = (1,1,1,1)
                    [MaterialID(6)] [Sub(RimShadowParams)] _RimShadowColor6 ("Color 6", Color) = (1,1,1,1)
                    [MaterialID(7)] [Sub(RimShadowParams)] _RimShadowColor7 ("Color 7", Color) = (1,1,1,1)
                // [SubGroup(RimShadowParams, RimShadowWidth)] _RimShadowWidth ("Rim Shadow Width", Float) = 0
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowWidth0 ("Width 0", Float) = 1
                    [MaterialID(1)] [Sub(RimShadowParams)] _RimShadowWidth1 ("Width 1", Float) = 1
                    [MaterialID(2)] [Sub(RimShadowParams)] _RimShadowWidth2 ("Width 2", Float) = 1
                    [MaterialID(3)] [Sub(RimShadowParams)] _RimShadowWidth3 ("Width 3", Float) = 1
                    [MaterialID(4)] [Sub(RimShadowParams)] _RimShadowWidth4 ("Width 4", Float) = 1
                    [MaterialID(5)] [Sub(RimShadowParams)] _RimShadowWidth5 ("Width 5", Float) = 1
                    [MaterialID(6)] [Sub(RimShadowParams)] _RimShadowWidth6 ("Width 6", Float) = 1
                    [MaterialID(7)] [Sub(RimShadowParams)] _RimShadowWidth7 ("Width 7", Float) = 1
                // [SubGroup(RimShadowParams, RimShadowFeather)] _RimShadowFeather ("Rim Shadow Feather", Float) = 0
                    [MaterialID(0)] [Sub(RimShadowParams)] _RimShadowFeather0 ("Feather 0", Range(0.01, 0.99)) = 0.01
                    [MaterialID(1)] [Sub(RimShadowParams)] _RimShadowFeather1 ("Feather 1", Range(0.01, 0.99)) = 0.01
                    [MaterialID(2)] [Sub(RimShadowParams)] _RimShadowFeather2 ("Feather 2", Range(0.01, 0.99)) = 0.01
                    [MaterialID(3)] [Sub(RimShadowParams)] _RimShadowFeather3 ("Feather 3", Range(0.01, 0.99)) = 0.01
                    [MaterialID(4)] [Sub(RimShadowParams)] _RimShadowFeather4 ("Feather 4", Range(0.01, 0.99)) = 0.01
                    [MaterialID(5)] [Sub(RimShadowParams)] _RimShadowFeather5 ("Feather 5", Range(0.01, 0.99)) = 0.01
                    [MaterialID(6)] [Sub(RimShadowParams)] _RimShadowFeather6 ("Feather 6", Range(0.01, 0.99)) = 0.01
                    [MaterialID(7)] [Sub(RimShadowParams)] _RimShadowFeather7 ("Feather 7", Range(0.01, 0.99)) = 0.01
        
        [Main(Outline, _, off, off)] _Outline ("Outline", Range(0, 1)) = 0
            [SubGroup(Outline, OutlineColors)] _OutlineColors ("Outline Colors", Float) = 0
                [MaterialID(0)] [Sub(OutlineColors)] _OutlineColor0 ("Color 0", Color) = (0,0,0,1)
                [MaterialID(1)] [Sub(OutlineColors)] _OutlineColor1 ("Color 1", Color) = (0,0,0,1)
                [MaterialID(2)] [Sub(OutlineColors)] _OutlineColor2 ("Color 2", Color) = (0,0,0,1)
                [MaterialID(3)] [Sub(OutlineColors)] _OutlineColor3 ("Color 3", Color) = (0,0,0,1)
                [MaterialID(4)] [Sub(OutlineColors)] _OutlineColor4 ("Color 4", Color) = (0,0,0,1)
                [MaterialID(5)] [Sub(OutlineColors)] _OutlineColor5 ("Color 5", Color) = (0,0,0,1)
                [MaterialID(6)] [Sub(OutlineColors)] _OutlineColor6 ("Color 6", Color) = (0,0,0,1)
                [MaterialID(7)] [Sub(OutlineColors)] _OutlineColor7 ("Color 7", Color) = (0,0,0,1)
            [Sub(Outline)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
            [SubKeywordEnum(Outline, Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 1
            [Sub(Outline)] _OutlineColorIntensity ("Outline Color Intensity", Float) = 0
            [Sub(Outline)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
            [Sub(Outline)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
            [Sub(Outline)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
            [Sub(Outline)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, Emission)] _EmissionGroup ("Emission", Float) = 0
                [Sub(Emission)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
                [Sub(Emission)] _EmissionIntensity ("Emission Intensity", Float) = 0
        [SubGroup(Effects, Stockings)] _Stockings("Stockings", Float) = 0
                [SubKeyword(Stockings, _WITHSTOCKINGS)] _EnableStocking ("With Stockings", Float) = 0
                [Sub(Stockings)] _StockRangeTex ("Stocking Range Texutre", 2D) = "black" { }
                [Sub(Stockings)] _Stockcolor ("Stockings Color", Color) = (1,1,1,1)
                [Sub(Stockings)] _StockDarkcolor ("Stockings Darkend Color", Color) = (1,1,1,1)
                [Sub(Stockings)] _StockDarkWidth ("Stockings Rim Width", Range(0, 0.96)) = 0.5
                [Sub(Stockings)] _Stockpower ("Stockings Power", Range(0.04, 1)) = 1
                [Sub(Stockings)] _Stockpower1 ("Stockings Lighted Width", Range(1, 32)) = 1
                [Sub(Stockings)] _StockSP ("Stockings Lighted Intensity", Range(0, 1)) = 0.25
                [Sub(Stockings)] _StockRoughness ("Stockings Texture Intensity", Range(0, 1)) = 1
                [Sub(Stockings)] _Stockthickness ("Stockings Thickness", Range(0, 1)) = 0
            [SubGroup(Effects, StarrySky)] _StarrySkyEffect("StarrySky Effect", Float) = 0
                [SubKeyword(StarrySky, _STATTYSKY)] _StarrySky ("With StarrySky", Float) = 0
                [Sub(StarrySky)] _SkyTex ("StarrySky Base Texture", 2D) = "black" { }
                [Sub(StarrySky)] _SkyMask ("StarrySky Mask Texture", 2D) = "black" { }
                [Sub(StarrySky)] _SkyRange ("StarrySky Range", Range(-1, 1)) = 0
                [Sub(StarrySky)] _SkyStarColor ("StarrySky Star Color", Color) = (1,1,1,1)
                [Sub(StarrySky)] _SkyStarTex ("StarrySky Star Texture", 2D) = "black" { }
                [Sub(StarrySky)] _SkyStarTexScale ("StarrySky Star Texture Scale", Float) = 1
                [Sub(StarrySky)] _SkyStarSpeed ("StarrySky Star Speed(XY)", Vector) = (0,0,0,0)
                [Sub(StarrySky)] _SkyStarDepthScale ("StarrySky Star DepthScale", Float) = 1
                [Sub(StarrySky)] _SkyStarMaskTex ("StarrySky Star Mask Texture", 2D) = "whilte" { }
                [Sub(StarrySky)] _SkyStarMaskTexScale ("StarrySky Star Mask Texture Scale", Float) = 1
                [Sub(StarrySky)] _SkyStarMaskTexSpeed ("StarrySky Star flicker frequency", Range(0, 20)) = 0
                [Sub(StarrySky)] _SkyFresnelColor ("StarrySky FresnelColor", Color) = (0,0,0,1)
                [Sub(StarrySky)] _SkyFresnelBaise ("StarrySky FresnelBaise", Float) = 0
                [Sub(StarrySky)] _SkyFresnelScale ("StarrySky FresnelScale", Float) = 0
                [Sub(StarrySky)] _SkyFresnelSmooth ("StarrySky FresnelSmooth", Range(0, 0.5)) = 0
                [Sub(StarrySky)] _OSScale ("StarrySky Model Scale", Range(0, 30)) = 1
                [Sub(StarrySky)] _StarDensity ("StarrySky Density", Range(0, 1)) = 0.5
                [Sub(StarrySky)] _StarMode ("StarrySky Mode", Range(0, 1)) = 0
            [SubGroup(Effects, FlameCrystal)] _FlameCrystalEffect("FlameCrystal Effect", Float) = 0
                [SubKeyword(FlameCrystal, _FLAMECRYSTALEFFECT)] _FlameCrystal ("With FlameCrystal", Float) = 0
                [Sub(FlameCrystal)] _FlameTex ("Fmale Texture", 2D) = "black" { }
                [Sub(FlameCrystal)] _CrystalTex ("Crystal Texture", 2D) = "black" { }
                [Sub(FlameCrystal)] _FlameID ("Material ID for Flame", Float) = 1
                [Sub(FlameCrystal)] _FlameColorOut ("OutSide Flame Color", Color) = (1,1,1,1)
                [Sub(FlameCrystal)] _FlameColorIn ("Inside Flame Color", Color) = (1,1,1,1)
                [Sub(FlameCrystal)] _FlameHeight ("Flame Height", Range(0, 1)) = 1
                [Sub(FlameCrystal)] _FlameWidth ("Flame Width", Range(0, 1)) = 1
                [Sub(FlameCrystal)] _FlameSpeed ("Flame Waving Speed", Float) = 1
                [Sub(FlameCrystal)] _FlameSwirilTexScale ("Flame Swiril Texture Scale", Float) = 1
                [Sub(FlameCrystal)] _FlameSwirilSpeed ("Flame Swiril Speed", Float) = 1
                [Sub(FlameCrystal)] _FlameSwirilScale ("Flame Swiril Scale", Float) = 1
                [Sub(FlameCrystal)] _CrystalTransparency ("Crystal Transparency", Range(0, 1)) = 0.35
                [Sub(FlameCrystal)] _CrystalRange1 ("Effect Progress in", Range(0, 1)) = 0
                [Sub(FlameCrystal)] _CrystalRange2 ("Effect Progress out", Range(0, 1)) = 1
                [Sub(FlameCrystal)] _ColorIntensity ("Effect Progress Intensity", Range(0, 1)) = 0.5
                [Sub(FlameCrystal)] _EffectColor0 ("Effect Color 0 (ID = 0)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor1 ("Effect Color 1 (ID = 31)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor2 ("Effect Color 2 (ID = 63)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor3 ("Effect Color 3 (ID = 95)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor4 ("Effect Color 4 (ID = 127)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor5 ("Effect Color 5 (ID = 159)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor6 ("Effect Color 6 (ID = 192)", Color) = (0,0,0,1)
                [Sub(FlameCrystal)] _EffectColor7 ("Effect Color 7 (ID = 223)", Color) = (0,0,0,1)
            [SubGroup(Effects, MoonHalo)] _MoonHaloEffect("MoonHalo Effect", Float) = 0
                [SubKeyword(MoonHalo, _CHARACTER_SPECIAL_FEATURE)]_UseMoonHalo ("Use MoonHalo", Float) = 0
                [Sub(MoonHalo)] _MoonHaloRange ("MoonHalo Range", Range(0, 1)) = 0
                [Sub(MoonHalo)] _MoonDir ("Use MoonHalo", Vector) = (0,0,0,1)
                [Sub(MoonHalo)] _MoonAnim ("Use MoonHalo Speed", Vector) = (0.35,0.65,0,0)
                [Sub(MoonHalo)] _MoonUVType ("MoonHalo UV Shape", Float) = 0
            [SubGroup(Effects, OverHeated)] _OverHeatedEffect("OverHeated Effect", Float) = 0
                [SubKeyword(OverHeated, _USE_OVERHEATED)] _UseOverHeated ("Use OverHeated", Float) = 0
                [Sub(OverHeated)] _HeatInst ("OverHeated Intensity", Range(0, 1)) = 0
                [Sub(OverHeated)] _HeatDir ("OverHeated Direction", Vector) = (0,-1,0,0.25)
                [Sub(OverHeated)] _HeatedHeight ("OverHeated Height", Range(0, 1)) = 0
                [Sub(OverHeated)] _HeatedThreshould ("OverHeated Height", Range(0, 1)) = 0
                [Sub(OverHeated)] _HeatColor0 ("OverHeated Color", Color) = (1,1,1,1)
                [Sub(OverHeated)] _HeatColor1 ("OverHeated Color", Color) = (1,1,1,1)
                [Sub(OverHeated)] _HeatColor2 ("OverHeated Color", Color) = (1,1,1,1)
            [SubGroup(Effects, MatCap)] _MatCapEffect("MatCap Effect", Float) = 0
                [SubKeyword(MatCap, _USE_MATCAP)] _UseMatcap ("Use MatCap", Float) = 0
                [Sub(MatCap)] _MatCapTex ("MatCap", 2D) = "black" { }
                [Sub(MatCap)] _MatCapMaskTex ("MatCap Mask", 2D) = "white" { }
                [Sub(MatCap)] _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
                [Sub(MatCap)] _MatCapStrength ("_MatCapStrength", Range(0, 5)) = 1
                [Sub(MatCap)] _MatCapStrengthInShadow ("_MatCapStrengthInShadow", Range(0, 1)) = 0.5
            [SubGroup(Effects, Glint)] _GlintEffect("Glint Effect", Float) = 0
            [SubKeyword(Glint, _USE_GLINT)] _UseGlint ("Use Glint", Float) = 0
            [Sub(Glint)] _GlintWorldPosUV ("Use World Pos UV", Float) = 0
            [Sub(Glint)] _GlintScaleBackface ("Backface Scale", Float) = 1
            [Sub(Glint)] _GlintUVTillingY ("Y Tilling", Float) = 1
            [Sub(Glint)] _GlobalGlintScale ("Global Glint Tilling", Float) = 100
            [Sub(Glint)] _GlobalGlintPointScale ("Global Glint Scale", Range(0, 0.5)) = 0.1
            [Sub(Glint)] _GlobalGlintIntensity ("Global Glint Instensity", Range(0, 5)) = 1
            [Sub(Glint)] _GlobalGlintShadow ("Global Glint Shadow", Range(0, 1)) = 0.3
            [Sub(Glint)] [HDR] _GlobalGlintColor ("Global Glint Color", Color) = (1,1,1,1)
            [Sub(Glint)] _GlobalGlintDensity ("Global Glint Density", Range(0, 1)) = 0.3
            [Sub(Glint)] _GlobalGlintSparkle ("Global Glint Sparkle", Range(0, 10)) = 2
            [Sub(Glint)] _GlobalGlintSparkFreq ("Global Glint Sparkle Frequence", Range(0, 10)) = 2
            [Sub(Glint)] _GlobalGlintViewFreq ("Global Glint View Frequence", Range(0, 10)) = 5
            [Sub(Glint)] _GlintScale ("Glint Tilling", Float) = 300
            [Sub(Glint)] _GlintPointScale ("Glint Scale", Range(0, 0.5)) = 0.1
            [Sub(Glint)] _GlintDensity ("Glint Density", Range(0, 1)) = 1
            [Sub(Glint)] _GlintConcentration ("Glint Contrast", Range(0, 2)) = 1
            [Sub(Glint)] _GlintIntensity ("Glint Instensity", Range(0, 1)) = 0.5
            [Sub(Glint)] [HDR] _GlintColor ("Glint Color", Color) = (1,1,1,1)
            [Sub(Glint)] _GlintRandom ("Glint Random", Range(-1.5, 1.5)) = 1
            [Sub(Glint)] _GlintMask ("Glint Texture", 2D) = "white" { }
            [Sub(Glint)] _GlintSparkle ("Glint Sparkle", Range(0, 10)) = 2
            [Sub(Glint)] _GlintSparkFreq ("Glint Sparkle Frequence", Range(0, 10)) = 2
            [Sub(Glint)] _GlintViewFreq ("Glint View Frequence", Range(0, 10)) = 5
        [SubGroup(Effects, FakeReflection)] _FakeReflectionEffect("Fake Reflection Effect", Float) = 0
            [SubKeyword(FakeReflection, _FAKEREFLECTION)] _ReflectionRoughness ("Fake Reflection Roughness", Range(0.01, 16)) = 1
            [Sub(FakeReflection)] _ReflectionThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.5
            [Sub(FakeReflection)] _ReflectionSoftness ("Fake Reflection Softness", Range(0, 1)) = 0.05
            [Sub(FakeReflection)] _ReflectionBlendThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
            [Sub(FakeReflection)] _ReflectionReversedThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
            [Sub(FakeReflection)] _FakeRefBlendIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.1
            [Sub(FakeReflection)] _FakeRefAddIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.25
            [Sub(FakeReflection)] _ReflectionColor ("", Color) = (1,1,1,1)
            [Sub(FakeReflection)] _ReflectionBlendColor ("", Color) = (1,1,1,1)
        [SubGroup(Effects, Dissolve)] _DissolveEffect("Dissolve Effect", Float) = 0
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
        [SubGroup(Effects, Bloom)] _BloomEffect("Bloom Effect", Float) = 0
        [SubGroup(Bloom, BloomParams)] _BloomParams ("Bloom Params", Float) = 0
        // [SubGroup(BloomParams, BloomIntensity)] _BloomIntensity ("Intensity", Range(0, 1)) = 0
        [MaterialID(0)] [Sub(BloomParams)] _mBloomIntensity0 ("Intensity 0", Float) = 0
        [MaterialID(1)] [Sub(BloomParams)] _mBloomIntensity1 ("Intensity 1", Float) = 0
        [MaterialID(2)] [Sub(BloomParams)] _mBloomIntensity2 ("Intensity 2", Float) = 0
        [MaterialID(3)] [Sub(BloomParams)] _mBloomIntensity3 ("Intensity 3", Float) = 0
        [MaterialID(4)] [Sub(BloomParams)] _mBloomIntensity4 ("Intensity 4", Float) = 0
        [MaterialID(5)] [Sub(BloomParams)] _mBloomIntensity5 ("Intensity 5", Float) = 0
        [MaterialID(6)] [Sub(BloomParams)] _mBloomIntensity6 ("Intensity 6", Float) = 0
        [MaterialID(7)] [Sub(BloomParams)] _mBloomIntensity7 ("Intensity 7", Float) = 0
        // [SubGroup(BloomParams, BloomColor)] _BloomColor ("Bloom Color", Float) = 0
            [MaterialID(0)] [Sub(BloomParams)] _mBloomColor0 ("Color 0", Color) = (1,1,1,1)
            [MaterialID(1)] [Sub(BloomParams)] _mBloomColor1 ("Color 1", Color) = (1,1,1,1)
            [MaterialID(2)] [Sub(BloomParams)] _mBloomColor2 ("Color 2", Color) = (1,1,1,1)
            [MaterialID(3)] [Sub(BloomParams)] _mBloomColor3 ("Color 3", Color) = (1,1,1,1)
            [MaterialID(4)] [Sub(BloomParams)] _mBloomColor4 ("Color 4", Color) = (1,1,1,1)
            [MaterialID(5)] [Sub(BloomParams)] _mBloomColor5 ("Color 5", Color) = (1,1,1,1)
            [MaterialID(6)] [Sub(BloomParams)] _mBloomColor6 ("Color 6", Color) = (1,1,1,1)
            [MaterialID(7)] [Sub(BloomParams)] _mBloomColor7 ("Color 7", Color) = (1,1,1,1)
        [SubGroup(Effects, CustomParams)] _CustomParams ("Custom Params", Float) = 0
            // [SubGroup(CustomParams, CustomParamA)] _CustomParamA ("CustomParamA", Float) = 0
                [MaterialID(0)] [Sub(CustomParams)] _CustomParamA0 ("Param A 0", Float) = 1
                [MaterialID(1)] [Sub(CustomParams)] _CustomParamA1 ("Param A 1", Float) = 1
                [MaterialID(2)] [Sub(CustomParams)] _CustomParamA2 ("Param A 2", Float) = 1
                [MaterialID(3)] [Sub(CustomParams)] _CustomParamA3 ("Param A 3", Float) = 1
                [MaterialID(4)] [Sub(CustomParams)] _CustomParamA4 ("Param A 4", Float) = 1
                [MaterialID(5)] [Sub(CustomParams)] _CustomParamA5 ("Param A 5", Float) = 1
                [MaterialID(6)] [Sub(CustomParams)] _CustomParamA6 ("Param A 6", Float) = 1
                [MaterialID(7)] [Sub(CustomParams)] _CustomParamA7 ("Param A 7", Float) = 1
            // [SubGroup(CustomParams, CustomParamB)] _CustomParamB ("CustomParamB", Float) = 0
                [MaterialID(0)] [Sub(CustomParams)] _CustomParamB0 ("Param B 0", Float) = 1
                [MaterialID(1)] [Sub(CustomParams)] _CustomParamB1 ("Param B 1", Float) = 1
                [MaterialID(2)] [Sub(CustomParams)] _CustomParamB2 ("Param B 2", Float) = 1
                [MaterialID(3)] [Sub(CustomParams)] _CustomParamB3 ("Param B 3", Float) = 1
                [MaterialID(4)] [Sub(CustomParams)] _CustomParamB4 ("Param B 4", Float) = 1
                [MaterialID(5)] [Sub(CustomParams)] _CustomParamB5 ("Param B 5", Float) = 1
                [MaterialID(6)] [Sub(CustomParams)] _CustomParamB6 ("Param B 6", Float) = 1
                [MaterialID(7)] [Sub(CustomParams)] _CustomParamB7 ("Param B 7", Float) = 1
        [HideInInspector] _Opaqueness ("Opaqueness", Range(0, 1)) = 1
        [SubGroup(Effects, Dither)] _DitherEffect("Dither Effect", Float) = 0
        [SubKeyword(Dither, _USE_DITHER)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Sub(Dither)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [Sub(Dither)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        
        [HideInInspector] _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        [HideInInspector] _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 0
        [HideInInspector] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
    }
    SubShader
    {
        Tags { "QUEUE" = "Geometry" "RenderType" = "Opaque+40" }
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
            Cull [_CullMode]
            Stencil
            {
                Ref 16
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
            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            
            #pragma shader_feature_local _FRAGMENT_CLIP
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma multi_compile _ _HEIGHTLERP
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _CHARACTER_SPECIAL_FEATURE
            #pragma shader_feature_local _USE_OVERHEATED
            #pragma shader_feature_local _RIM_BACKLIGHT_ON
            #pragma shader_feature_local _WITHSTOCKINGS
            #pragma shader_feature_local _STATTYSKY
            #pragma shader_feature_local _FLAMECRYSTALEFFECT
            #pragma shader_feature_local _USE_MATCAP
            #pragma multi_compile _USE_GLINT
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
            Name "forward"
            Tags { "LightMode"="ForwardEmission" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Blend One Zero
            Cull [_CullMode]
            ZWrite On
            // Stencil
            // {
            //     Ref 16
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
            
            #pragma shader_feature_local _FRAGMENT_CLIP
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma shader_feature_local _STATTYSKY
            #pragma shader_feature_local _FLAMECRYSTALEFFECT
            #pragma shader_feature_local _USE_OVERHEATED
            #pragma multi_compile _ _HEIGHTLERP
            #pragma multi_compile _ _ENABLE_FOG
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _USE_GLINT
            #pragma shader_feature_local _USE_DITHER
            #pragma shader_feature_local _FAKEREFLECTION
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                WriteMask 255
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
            #pragma multi_compile _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _USE_DITHER
            #pragma shader_feature_local _FRAGMENT_CLIP
            #pragma vertex vert_edge
            #pragma fragment frag_edge

            ENDHLSL
        } 


        Pass 
        {
            Name "ShadowCaster"
            Tags { "LightMode"="SHADOWCASTER" }
            
            // Important for performance and depth precision
            ZWrite On 
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"
            #pragma shader_feature_local _DIRECTIONALDISSOLVE
            #pragma shader_feature_local _FRAGMENT_CLIP
            #pragma shader_feature_local _IS_DUALFACE
            
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            ENDHLSL
        }
        // UsePass "Standard/ShadowCaster"
    }
    CustomEditor "LWGUI.LWGUI" 
    
    FallBack "Diffuse"
}
