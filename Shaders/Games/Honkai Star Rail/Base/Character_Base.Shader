Shader "HoyoToon/Honkai Star Rail/Character/Base"
{
    Properties
    {
        [MHYHelpBox(Info, Vertex Color B (outline width))] [Header(OPTIONS)] [Toggle] _EnableDualFace ("Enable Dual Face Rendering", Float) = 0
        [Enum(UV1, 0, UV2, 1)] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [Enum(UV1, 0, UV2, 1)] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [Toggle] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AlphaTestThreshold ("AlphaTest Threshold", Range(0, 1)) = 0.5
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        _SelfShadowDepthOffset ("_SelfShadowDepthOffset", Float) = 0
        _SelfShadowSampleOffset ("_SelfShadowSampleOffset", Float) = 0.001
        _SelfShadowDarken ("_SelfShadowDarken", Float) = 0.6
        [Toggle]_HideCharaParts ("Hide Chara Parts", Float) = 0
        _ShowPartID ("Show Part ID", Float) = 0
        _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        _IsMonster ("Is Monster", Float) = 0
        _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        [Header(MAPS)] [Header(Main Maps)] [MHYSingleLineTextureNoScaleOffset(_Color)] _MainTex ("Albedo |RGB(base color) A (alpha)", 2D) = "white" { }
        [HideInInspector] _VertexShadowColor ("Vertex Shadow Color", Color) = (1,1,1,1)
        [HideInInspector] _CustomMainLightDir ("Custom Light Dir", Vector) = (1,1,1,0)
        _Color ("Color", Color) = (1,1,1,1)
        _BackColor ("BackColor", Color) = (1,1,1,1)
        [HideInInspector] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [HideInInspector] _AddColor ("Add Color", Color) = (0,0,0,0)
        [MHYSingleLineTextureNoScaleOffset] _LightMap ("Light Map |R (sepcular intensity) G (diffuse threshold) B (specular threshold) A (material id)", 2D) = "grey" { }
        [Toggle] _UseNormalMap ("UseNormalMap", Float) = 0
        _NormalMap ("NormalMap", 2D) = "bump" { }
        _NormalScale ("NormalScale", Range(0, 4)) = 1
        [Toggle] _AnisotropySpecular ("AnisotropySpecular", Float) = 0
        [Header(Emission)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        _EmissionIntensity ("Emission Intensity", Float) = 0
        [Header(DIFFUSE)] [MHYColorGradient] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
        [MHYColorGradient] _DiffuseRampMultiTex ("Shadow Multiple Ramp", 2D) = "white" { }
        _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
        [Toggle]_ShadowBoost ("Shadow Boost Enable", Float) = 0
        _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
        [Header(SPECULAR)] [MHYMaterialIDFold] _SpecularColor ("Specular Color", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor0 ("Specular Color (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess0 ("Specular Shininess (ID = 0)", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness0 ("Specular Roughness (ID = 0)", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity0 ("Specular Intensity (ID = 0)", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor1 ("Specular Color (ID = 31)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess1 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness1 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity1 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor2 ("Specular Color (ID = 63)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess2 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness2 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity2 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor3 ("Specular Color (ID = 95)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess3 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness3 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity3 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor4 ("Specular Color (ID = 127)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess4 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness4 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity4 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor5 ("Specular Color (ID = 159)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess5 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness5 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity5 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor6 ("Specular Color (ID = 192)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess6 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness6 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity6 ("Specular Intensity", Range(0, 50)) = 1
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularColor7 ("Specular Color (ID = 223)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularShininess7 ("Specular Shininess", Range(0.1, 500)) = 10
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularRoughness7 ("Specular Roughness", Range(0, 1)) = 0.02
        [MHYMaterialIDProperty(_SpecularColor)] _SpecularIntensity7 ("Specular Intensity", Range(0, 50)) = 1
        _SpecularShininess ("Specular Shininess", Range(0.1, 500)) = 10
        _SpecularRoughness ("Specular Roughness", Range(0, 1)) = 0
        _SpecularIntensity ("Specular Intensity", Range(0, 50)) = 1
        [Header(OUTLINE)] [MHYMaterialIDFold] _Outline ("Outline", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_Outline)] _OutlineColor0 ("Outline Color 0 (ID = 0)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor1 ("Outline Color 1 (ID = 31)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor2 ("Outline Color 2 (ID = 63)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor3 ("Outline Color 3 (ID = 95)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor4 ("Outline Color 4 (ID = 127)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor5 ("Outline Color 5 (ID = 159)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor6 ("Outline Color 6 (ID = 192)", Color) = (0,0,0,1)
        [MHYMaterialIDProperty(_Outline)] _OutlineColor7 ("Outline Color 7 (ID = 223)", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [KeywordEnum(Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        _OutlineColorIntensity ("Outline Color Intensity", Float) = 0
        _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        [Header(RIMLIGHT)] _RimLightMode ("0:don't use lightmap.r, 1:use", Range(0, 1)) = 1
        [MHYMaterialIDFold] _RimLight ("Rim Light", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_RimLight)] _RimWidth0 ("RimWidth 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimColor0 ("RimColor 0 (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness0 ("Rim Edge Softness 0 (ID = 0)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType0 ("Rim Blend Mode 0 (ID = 0)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark0 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor1 ("RimColor 1 (ID = 31)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness1 ("Rim Edge Softness 1 (ID = 1)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType1 ("Rim Blend Mode 1 (ID = 1)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark1 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor2 ("RimColor 2 (ID = 63)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness2 ("Rim Edge Softness 2 (ID = 2)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType2 ("Rim Blend Mode 2 (ID = 2)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark2 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor3 ("RimColor 3 (ID = 95)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness3 ("Rim Edge Softness 3 (ID = 3)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType3 ("Rim Blend Mode 3 (ID = 3)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark3 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor4 ("RimColor 4 (ID = 127)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness4 ("Rim Edge Softness 4 (ID = 4)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType4 ("Rim Blend Mode 4 (ID = 4)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark4 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor5 ("RimColor 5 (ID = 159)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness5 ("Rim Edge Softness 5 (ID = 5)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType5 ("Rim Blend Mode 5 (ID = 5)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark5 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor6 ("RimColor 6 (ID = 192)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness6 ("Rim Edge Softness 6 (ID = 6)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType6 ("Rim Blend Mode 6 (ID = 6)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark6 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [MHYMaterialIDProperty(_RimLight)] _RimColor7 ("RimColor 7 (ID = 223)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimLight)] _RimEdgeSoftness7 ("Rim Edge Softness 7 (ID = 7)", Range(0.01, 0.9)) = 0.1
        [MHYMaterialIDProperty(_RimLight)] _RimType7 ("Rim Blend Mode 7 (ID = 7)", Range(0, 1)) = 1
        [MHYMaterialIDProperty(_RimLight)] _RimDark7 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        _Rimintensity ("Rim Intensity", Float) = 1
        _RimFeatherWidth ("Rim Feather Width", Float) = 0.01
        _RimWidth ("RimWidth", Float) = 1
        _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
        _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015
        [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [Toggle] _EnableBackRimLight ("Enable Back Rim Light", Float) = 0
        _RimShadowCt ("Rim Shadow Ct", Float) = 1
        _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [Header(RIMSHADOW)] [MHYMaterialIDFold] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor0 ("Rim Shadow Color 0 (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth0 ("Rim Shadow Width 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather0 ("Rim Shadow Feather 0 (ID = 0)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor1 ("Rim Shadow Color 1 (ID = 1)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth1 ("Rim Shadow Width 1 (ID = 1)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather1 ("Rim Shadow Feather 1 (ID = 1)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor2 ("Rim Shadow Color 2 (ID = 2)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth2 ("Rim Shadow Width 2 (ID = 2)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather2 ("Rim Shadow Feather 2 (ID = 2)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor3 ("Rim Shadow Color 3 (ID = 3)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth3 ("Rim Shadow Width 3 (ID = 3)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather3 ("Rim Shadow Feather 3 (ID = 3)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor4 ("Rim Shadow Color 4 (ID = 4)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth4 ("Rim Shadow Width 4 (ID = 4)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather4 ("Rim Shadow Feather 4 (ID = 4)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor5 ("Rim Shadow Color 5 (ID = 5)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth5 ("Rim Shadow Width 5 (ID = 5)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather5 ("Rim Shadow Feather 5 (ID = 5)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor6 ("Rim Shadow Color 6 (ID = 6)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth6 ("Rim Shadow Width 6 (ID = 6)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather6 ("Rim Shadow Feather 6 (ID = 6)", Range(0.01, 0.99)) = 0.01
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowColor7 ("Rim Shadow Color 7 (ID = 7)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowWidth7 ("Rim Shadow Width 7 (ID = 7)", Float) = 1
        [MHYMaterialIDProperty(_RimShadow)] _RimShadowFeather7 ("Rim Shadow Feather 7 (ID = 7)", Range(0.01, 0.99)) = 0.01
        _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        [Header(_USE_FUR)] [MHYSingleLineTextureKeywordDrawer(_FUR_ON)] _FurTex ("FurTex", 2D) = "white" { }
        [MHYKeywordFilter(_FUR_ON On)] _FurTexSmooth ("FurTexSmooth", Float) = 0.95
        [MHYKeywordFilter(_FUR_ON On)] _FurColor ("FurColor", Color) = (0,0,0,0)
        [MHYKeywordFilter(_FUR_ON On)] _FurTile ("Fur Tile", Float) = 10
        [Header(SHOWPARTSBYID)] [MHYMaterialIDFold] _ShowPartzID ("Show Parts", Float) = 0
        _XorShadowPartID ("Xor Shadow Parts", Float) = 0
        [Header(STOCKINGS)] [Toggle] _EnableStocking ("With Stockings", Float) = 0
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _StockRangeTex ("Stocking Range Texutre", 2D) = "black" { }
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _Stockcolor ("Stockings Color", Color) = (1,1,1,1)
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _StockDarkcolor ("Stockings Darkend Color", Color) = (1,1,1,1)
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _StockDarkWidth ("Stockings Rim Width", Range(0, 0.96)) = 0.5
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _Stockpower ("Stockings Power", Range(0.04, 1)) = 1
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _Stockpower1 ("Stockings Lighted Width", Range(1, 32)) = 1
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _StockSP ("Stockings Lighted Intensity", Range(0, 1)) = 0.25
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _StockRoughness ("Stockings Texture Intensity", Range(0, 1)) = 1
        [MHYKeywordFilter(_WITHSTOCKINGS On)] _Stockthickness ("Stockings Thickness", Range(0, 1)) = 0
        [Header(STATTYSKY)] [Toggle] _StarrySky ("With StarrySky", Float) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _SkyTex ("StarrySky Base Texture", 2D) = "black" { }
        [MHYKeywordFilter(_STATTYSKY On)] _SkyMask ("StarrySky Mask Texture", 2D) = "black" { }
        [MHYKeywordFilter(_STATTYSKY On)] _SkyRange ("StarrySky Range", Range(-1, 1)) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarColor ("StarrySky Star Color", Color) = (1,1,1,1)
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarTex ("StarrySky Star Texture", 2D) = "black" { }
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarTexScale ("StarrySky Star Texture Scale", Float) = 1
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarSpeed ("StarrySky Star Speed(XY)", Vector) = (0,0,0,0)
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarDepthScale ("StarrySky Star DepthScale", Float) = 1
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarMaskTex ("StarrySky Star Mask Texture", 2D) = "whilte" { }
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarMaskTexScale ("StarrySky Star Mask Texture Scale", Float) = 1
        [MHYKeywordFilter(_STATTYSKY On)] _SkyStarMaskTexSpeed ("StarrySky Star flicker frequency", Range(0, 20)) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _SkyFresnelColor ("StarrySky FresnelColor", Color) = (0,0,0,1)
        [MHYKeywordFilter(_STATTYSKY On)] _SkyFresnelBaise ("StarrySky FresnelBaise", Float) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _SkyFresnelScale ("StarrySky FresnelScale", Float) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _SkyFresnelSmooth ("StarrySky FresnelSmooth", Range(0, 0.5)) = 0
        [MHYKeywordFilter(_STATTYSKY On)] _OSScale ("StarrySky Model Scale", Range(0, 30)) = 1
        [MHYKeywordFilter(_STATTYSKY On)] _StarDensity ("StarrySky Density", Range(0, 1)) = 0.5
        [MHYKeywordFilter(_STATTYSKY On)] _StarMode ("StarrySky Mode", Range(0, 1)) = 0
        [Header(_FLAMECRYSTALEFFECT)] [Toggle] _FlameCrystal ("With FlameCrystal", Float) = 0
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _TangentDirTex ("Tangent Direction Texture", 2D) = "bump" { }
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameTex ("Fmale Texture", 2D) = "black" { }
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _CrystalTex ("Crystal Texture", 2D) = "black" { }
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameID ("Material ID for Flame", Float) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameColorOut ("OutSide Flame Color", Color) = (1,1,1,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameColorIn ("Inside Flame Color", Color) = (1,1,1,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameHeight ("Flame Height", Range(0, 1)) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameWidth ("Flame Width", Range(0, 1)) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameSpeed ("Flame Waving Speed", Float) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameSwirilTexScale ("Flame Swiril Texture Scale", Float) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameSwirilSpeed ("Flame Swiril Speed", Float) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _FlameSwirilScale ("Flame Swiril Scale", Float) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _CrystalTransparency ("Crystal Transparency", Range(0, 1)) = 0.35
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _CrystalRange1 ("Effect Progress in", Range(0, 1)) = 0
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _CrystalRange2 ("Effect Progress out", Range(0, 1)) = 1
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _ColorIntensity ("Effect Progress Intensity", Range(0, 1)) = 0.5
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor0 ("Effect Color 0 (ID = 0)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor1 ("Effect Color 1 (ID = 31)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor2 ("Effect Color 2 (ID = 63)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor3 ("Effect Color 3 (ID = 95)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor4 ("Effect Color 4 (ID = 127)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor5 ("Effect Color 5 (ID = 159)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor6 ("Effect Color 6 (ID = 192)", Color) = (0,0,0,1)
        [MHYKeywordFilter(_FLAMECRYSTALEFFECT On)] _EffectColor7 ("Effect Color 7 (ID = 223)", Color) = (0,0,0,1)
        [Header(MOONHALOEFFECT)] [Toggle]_UseMoonHalo ("Use MoonHalo", Float) = 0
        _MoonHaloRange ("MoonHalo Range", Range(0, 1)) = 0
        _MoonDir ("Use MoonHalo", Vector) = (0,0,0,1)
        _MoonAnim ("Use MoonHalo Speed", Vector) = (0.35,0.65,0,0)
        _MoonUVType ("MoonHalo UV Shape", Float) = 0
        [Header(OVERHEATEDEFFECT)] [Toggle] _UseOverHeated ("Use OverHeated", Float) = 0
        _HeatInst ("OverHeated Intensity", Range(0, 1)) = 0
        _HeatDir ("OverHeated Direction", Vector) = (0,-1,0,0.25)
        _HeatedHeight ("OverHeated Height", Range(0, 1)) = 0
        _HeatedThreshould ("OverHeated Height", Range(0, 1)) = 0
        _HeatColor0 ("OverHeated Color", Color) = (1,1,1,1)
        _HeatColor1 ("OverHeated Color", Color) = (1,1,1,1)
        _HeatColor2 ("OverHeated Color", Color) = (1,1,1,1)
        [Header(MatCap)] [Toggle] _UseMatcap ("Use MatCap", Float) = 0
        _MatCapTex ("MatCap", 2D) = "black" { }
        _MatCapMaskTex ("MatCap Mask", 2D) = "white" { }
        _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        _MatCapStrength ("_MatCapStrength", Range(0, 5)) = 1
        _MatCapStrengthInShadow ("_MatCapStrengthInShadow", Range(0, 1)) = 0.5
        [Header(Glint)] [Toggle] _UseGlint ("Use Glint", Float) = 0
        _GlintWorldPosUV ("Use World Pos UV", Float) = 0
        _GlintScaleBackface ("Backface Scale", Float) = 1
        _GlintUVTillingY ("Y Tilling", Float) = 1
        _GlobalGlintScale ("Global Glint Tilling", Float) = 100
        _GlobalGlintPointScale ("Global Glint Scale", Range(0, 0.5)) = 0.1
        _GlobalGlintIntensity ("Global Glint Instensity", Range(0, 5)) = 1
        _GlobalGlintShadow ("Global Glint Shadow", Range(0, 1)) = 0.3
        [HDR] _GlobalGlintColor ("Global Glint Color", Color) = (1,1,1,1)
        _GlobalGlintDensity ("Global Glint Density", Range(0, 1)) = 0.3
        _GlobalGlintSparkle ("Global Glint Sparkle", Range(0, 10)) = 2
        _GlobalGlintSparkFreq ("Global Glint Sparkle Frequence", Range(0, 10)) = 2
        _GlobalGlintViewFreq ("Global Glint View Frequence", Range(0, 10)) = 5
        _GlintScale ("Glint Tilling", Float) = 300
        _GlintPointScale ("Glint Scale", Range(0, 0.5)) = 0.1
        _GlintDensity ("Glint Density", Range(0, 1)) = 1
        _GlintConcentration ("Glint Contrast", Range(0, 2)) = 1
        _GlintIntensity ("Glint Instensity", Range(0, 1)) = 0.5
        [HDR] _GlintColor ("Glint Color", Color) = (1,1,1,1)
        _GlintRandom ("Glint Random", Range(-1.5, 1.5)) = 1
        _GlintMask ("Glint Texture", 2D) = "white" { }
        _GlintSparkle ("Glint Sparkle", Range(0, 10)) = 2
        _GlintSparkFreq ("Glint Sparkle Frequence", Range(0, 10)) = 2
        _GlintViewFreq ("Glint View Frequence", Range(0, 10)) = 5
        [Header(Dissolve)] _ReflectionRoughness ("Fake Reflection Roughness", Range(0.01, 16)) = 1
        _ReflectionThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.5
        _ReflectionSoftness ("Fake Reflection Softness", Range(0, 1)) = 0.05
        _ReflectionBlendThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        _ReflectionReversedThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        _FakeRefBlendIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        _FakeRefAddIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.25
        _ReflectionColor ("", Color) = (1,1,1,1)
        _ReflectionBlendColor ("", Color) = (1,1,1,1)
        [Header(Dissolve)] [Toggle] _DissoveON ("Enable Dissolve", Float) = 0
        [HideInInspector] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
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
        _DissolveMapAdd ("", Float) = 0
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
        _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0
        [Header(ADDLIGHT)] _AddLightOffset ("Add Light Offset", Range(0, 1)) = 0.5
        _AddLightStrengthen ("Add Light Strengthen", Range(0, 3)) = 0.3
        _AddLightFeather ("Add Light Feather", Range(0, 0.1)) = 0.03
        [Header(BLOOM INSTENSITY)] [MHYMaterialIDFold] _BloomIntensity ("Bloom Intensity", Range(0, 1)) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity0 ("Bloom Intensity 0 (ID = 0)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity1 ("Bloom Intensity 1 (ID = 31)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity2 ("Bloom Intensity 2 (ID = 63)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity3 ("Bloom Intensity 3 (ID = 95)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity4 ("Bloom Intensity 4 (ID = 127)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity5 ("Bloom Intensity 5 (ID = 159)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity6 ("Bloom Intensity 6 (ID = 192)", Float) = 0
        [MHYMaterialIDProperty(_BloomIntensity)] _mBloomIntensity7 ("Bloom Intensity 7 (ID = 223)", Float) = 0
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor0 ("Bloom Color 0 (ID = 0)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor1 ("Bloom Color 1 (ID = 31)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor2 ("Bloom Color 2 (ID = 63)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor3 ("Bloom Color 3 (ID = 95)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor4 ("Bloom Color 4 (ID = 127)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor5 ("Bloom Color 5 (ID = 159)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor6 ("Bloom Color 6 (ID = 192)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_BloomColor)] _mBloomColor7 ("Bloom Color 7 (ID = 223)", Color) = (1,1,1,1)
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA0 ("CustomParamA 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA1 ("CustomParamA 1 (ID = 31)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA2 ("CustomParamA 2 (ID = 63)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA3 ("CustomParamA 3 (ID = 95)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA4 ("CustomParamA 4 (ID = 127)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA5 ("CustomParamA 5 (ID = 159)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA6 ("CustomParamA 6 (ID = 192)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamA)] _CustomParamA7 ("CustomParamA 7 (ID = 223)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB0 ("CustomParamB 0 (ID = 0)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB1 ("CustomParamB 1 (ID = 31)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB2 ("CustomParamB 2 (ID = 63)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB3 ("CustomParamB 3 (ID = 95)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB4 ("CustomParamB 4 (ID = 127)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB5 ("CustomParamB 5 (ID = 159)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB6 ("CustomParamB 6 (ID = 192)", Float) = 1
        [MHYMaterialIDProperty(_CustomParamB)] _CustomParamB7 ("CustomParamB 7 (ID = 223)", Float) = 1
        [HideInInspector] _Opaqueness ("Opaqueness", Range(0, 1)) = 1
        [Header(DITHER)]
        [Toggle] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [Toggle] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 16
        [HideInInspector] _StencilOP ("Stencil Op", Float) = 2
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _RenderingMode ("Rendering Mode", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Src Blend", Float) = 0
        [HideInInspector] _CullMode ("Src Blend", Float) = 2
        [MHYSingleLineTextureNoScaleOffset(_EmissionTintColor)] _EmissionTex ("Emission Tex", 2D) = "black" { }
        _EmissionTintColor ("Emission TintColor", Color) = (1,1,1,1)
        [HideInInspector] _ZClip ("ZClip", Float) = 0
        _UseMaterialValuesLUT ("Use Mat Lut", Float) = 0
        _MaterialValuesPackLUT ("Mat Pack LUT", 2D) = "white" { }
        _CharacterPerDrawEx ("_CharacterPerDrawEx", Float) = 1
        _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 0
        _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
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
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _IS_DUALFACE
            #pragma shader_feature_local _CHARACTER_SPECIAL_FEATURE
            #pragma shader_feature_local _USE_OVERHEATED
            #pragma shader_feature_local _RIM_BACKLIGHT_ON
            #pragma shader_feature_local _WITHSTOCKINGS
            #pragma shader_feature_local _STATTYSKY
            #pragma shader_feature_local _FLAMECRYSTALEFFECT
            #pragma shader_feature_local _USE_MATCAP
            // #pragma multi_compile _USE_GLINT
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
            #pragma shader_feature_local _HEIGHTLERP
            #pragma shader_feature_local _ENABLE_FOG
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