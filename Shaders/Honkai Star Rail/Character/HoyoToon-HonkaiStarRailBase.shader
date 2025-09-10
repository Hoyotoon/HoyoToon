Shader "HoyoToon/Honkai Star Rail/Character/Base"
{
    Properties
    {
        [Title(I love my husband)]
        //  group name, keyword name, folding state, display toggle
        [Main(MainGroup, _, off, off)] _MainGroup ("Main Settings", Float) = 0
        [Tex(MainGroup)] _MainTex ("Main Texture", 2D) = "white" {}
        [Tex(MainGroup)] _LightMap ("Light Map", 2D) = "grey" { }
        [SubEnum(MainGroup, UV1, 0, UV2, 1)] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [SubEnum(MainGroup, UV1, 0, UV2, 1)] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [Sub(MainGroup)] _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)
        [Sub(MainGroup)] _VertexShadowColor ("Vertex Shadow Color", Color) = (0,0,0,1)
        [Sub(MainGroup)] _Color ("Color", Color) = (1,1,1,1)
        [Sub(MainGroup)] _BackColor ("BackColor", Color) = (1,1,1,1)
        [Sub(MainGroup)] _EnvColor ("Env Color", Color) = (1,1,1,1)
        [Sub(MainGroup)] _AddColor ("Add Color", Color) = (0,0,0,0)
        // material lut
        [Advanced(Material LUT)][SubToggle(MainGroup)] _UseMaterialValuesLUT ("Use Mat Lut", Float) = 0
        [Advanced][SubToggle(MainGroup)]_MaterialValuesPackLUT ("Mat Pack LUT", 2D) = "white" { }        
        // alpha cutoff
        [Advanced(Alpha)][SubToggle(MainGroup)] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        [Advanced][Sub(MainGroup)]_AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Advanced][Sub(MainGroup)]_AlphaTestThreshold ("AlphaTest Threshold", Range(0, 1)) = 0.5
        // hide character parts
        [Advanced(Hide Chara Parts)][Sub(MainGroup)]  _HideCharaPartdfsds ("Hide Chara Parts", Float) = 0
        [Advanced][SubToggle(MainGroup)] _HideCharaParts ("Hide Chara Parts", Float) = 0
        [Advanced][Sub(MainGroup)][IntRange] _ShowPartID ("Show Part ID", Range(0, 256)) = 0
        // emission group
        [Main(EmissionGroup, _, off, off)] _EmissionGroup ("Emission Settings", Float) = 0
        [Sub(EmissionGroup)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        [Sub(EmissionGroup)] _EmissionIntensity ("Emission Texture", Float) = 0
        [Tex(EmissionGroup)] _EmissionTex ("Emission Tex", 2D) = "black" { }
        [Sub(EmissionGroup)] _EmissionTintColor ("Emission TintColor", Color) = (1,1,1,1)
        // shadow group        
        [Main(ShadowGroup, _, off, off)] _ShadowGroup ("Shadow Settings", Float) = 0
        [SubToggle(ShadowGroup)] _ES_CharacterToonRampMode ("Ramp Mode", Float) = 0
        [Tex(ShadowGroup)] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
        [Tex(ShadowGroup)] _DiffuseRampMultiTex ("Shadow Multiple Ramp", 2D) = "white" { }
        [Sub(ShadowGroup)] _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
        [SubToggle(ShadowGroup)] _ShadowBoost ("Shadow Boost Enable", Float) = 0
        [Sub(ShadowGroup)] _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
        [Advanced(Story Lighting)][SubToggle(ShadowGroup)] _ES_LEVEL_ADJUST_ON ("Enable Level Adjust", Float) = 0
        [Advanced][Sub(ShadowGroup)]_ES_LevelSkinLightColor ("Skin Shadow Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelSkinShadowColor ("Skin Light Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelHighLightColor ("Base Shadow Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelShadowColor ("Base Light Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelShadow ("Shadow Level", Range(0, 1)) = 0.0
        [Advanced][Sub(ShadowGroup)]_ES_LevelMid ("Mid Level", Range(0, 1)) = 0.55
        [Advanced][Sub(ShadowGroup)]_ES_LevelHighLight ("High Light Level", Range(0, 1)) = 1.0
        // specular group
        [Main(SpecularGroup, _, off, off)] _SpecularGroup ("Specular Settings", Float) = 0
        [Advanced(Specular Color)][Sub(SpecularGroup)] _SpecularColor0 ("Specular Color 0", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor1 ("Specular Color 1", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor2 ("Specular Color 2", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor3 ("Specular Color 3", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor4 ("Specular Color 4", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor5 ("Specular Color 5", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor6 ("Specular Color 6", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _SpecularColor7 ("Specular Color 7", Color) = (1,1,1,1)
        [Advanced(Specular Shininess)] [Sub(SpecularGroup)] _SpecularShininess0 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess1 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess2 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess3 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess4 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess5 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess6 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced] [Sub(SpecularGroup)] _SpecularShininess7 ("Specular Shininess 0", Range(0.1, 500)) = 10
        [Advanced(Specular Roughness)] [Sub(SpecularGroup)] _SpecularRoughness0 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness1 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness2 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness3 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness4 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness5 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness6 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced] [Sub(SpecularGroup)] _SpecularRoughness7 ("Specular Roughness 0", Range(0, 1)) = 0
        [Advanced(Specular Intensity)][Sub(SpecularGroup)] _SpecularIntensity0 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity1 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity2 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity3 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity4 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity5 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity6 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced][Sub(SpecularGroup)] _SpecularIntensity7 ("Specular Intensity 0", Range(0, 50)) = 1
        [Advanced(Scripted Values)][Sub(SpecularGroup)] _ES_SPColor ("Color", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _ES_SPIntensity ("intensity", Float) = 1.0
        // outline group
        [Main(OutlineGroup, _, off, off)]  _Outline ("Outline", Range(0, 1)) = 0
        [Advanced(Outline Color)][Sub(OutlineGroup)] _OutlineColor0 ("Outline Color 0 (ID = 0)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor1 ("Outline Color 1 (ID = 31)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor2 ("Outline Color 2 (ID = 63)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor3 ("Outline Color 3 (ID = 95)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor4 ("Outline Color 4 (ID = 127)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor5 ("Outline Color 5 (ID = 159)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor6 ("Outline Color 6 (ID = 192)", Color) = (0,0,0,1)
        [Advanced][Sub(OutlineGroup)] _OutlineColor7 ("Outline Color 7 (ID = 223)", Color) = (0,0,0,1)
        [Sub(OutlineGroup)]_OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [SubEnum(OutlineGroup, Normal, 0, Tangent, 1, UV2, 2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        [Sub(OutlineGroup)]_OutlineColorIntensity ("Outline Color Intensity", Float) = 0
        [Sub(OutlineGroup)]_OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        [Sub(OutlineGroup)]_OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        [Sub(OutlineGroup)]_OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        [Sub(OutlineGroup)]_OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        // rim group
        [Main(RimGroup, _, off, off)] _rimlightgroup("Rim Light", Float) = 0
        [Sub(RimGroup)] _RimLightMode ("0:don't use lightmap.r, 1:use", Range(0, 1)) = 1
        [Sub(RimGroup)] _RimLight ("Rim Light", Range(0, 1)) = 0
        [Sub(RimGroup)] _RimWidth0 ("RimWidth 0 (ID = 0)", Float) = 1
        [Advanced(Rim Color)][Sub(RimGroup)] _RimColor0 ("RimColor 0 (ID = 0)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor1 ("RimColor 1 (ID = 31)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor2 ("RimColor 2 (ID = 63)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor3 ("RimColor 3 (ID = 95)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor4 ("RimColor 4 (ID = 127)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor5 ("RimColor 5 (ID = 159)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor6 ("RimColor 6 (ID = 192)", Color) = (1,1,1,1)
        [Advanced][Sub(RimGroup)] _RimColor7 ("RimColor 7 (ID = 223)", Color) = (1,1,1,1)
        [Advanced(Rim Softness)][Sub(RimGroup)] _RimEdgeSoftness0 ("Rim Edge Softness 0 (ID = 0)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness1 ("Rim Edge Softness 1 (ID = 1)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness2 ("Rim Edge Softness 2 (ID = 2)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness3 ("Rim Edge Softness 3 (ID = 3)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness4 ("Rim Edge Softness 4 (ID = 4)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness5 ("Rim Edge Softness 5 (ID = 5)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness6 ("Rim Edge Softness 6 (ID = 6)", Range(0.01, 0.9)) = 0.1
        [Advanced][Sub(RimGroup)] _RimEdgeSoftness7 ("Rim Edge Softness 7 (ID = 7)", Range(0.01, 0.9)) = 0.1
        [Advanced(Rim Type)][Sub(RimGroup)] _RimType0 ("Rim Blend Mode 0 (ID = 0)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType1 ("Rim Blend Mode 1 (ID = 1)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType2 ("Rim Blend Mode 2 (ID = 2)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType3 ("Rim Blend Mode 3 (ID = 3)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType4 ("Rim Blend Mode 4 (ID = 4)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType5 ("Rim Blend Mode 5 (ID = 5)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType6 ("Rim Blend Mode 6 (ID = 6)", Range(0, 1)) = 1
        [Advanced][Sub(RimGroup)] _RimType7 ("Rim Blend Mode 7 (ID = 7)", Range(0, 1)) = 1
        [Advanced(Rim Dark)][Sub(RimGroup)] _RimDark0 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark1 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark2 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark3 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark4 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark5 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark6 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced][Sub(RimGroup)] _RimDark7 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimGroup)] _Rimintensity ("Rim Intensity", Float) = 1
        [Sub(RimGroup)] _RimFeatherWidth ("Rim Feather Width", Float) = 0.01
        [Sub(RimGroup)] _RimWidth ("RimWidth", Float) = 1
        [Sub(RimGroup)] _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
        [Sub(RimGroup)] _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015
        [Sub(RimGroup)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        [Sub(RimGroup)] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        [Sub(RimGroup)] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        // rim shadow
        [Main(RimShadowGroup, _, off, off)] _rimshadowgroup ("Rim Shadow", Float) = 0
        [Sub(RimShadowGroup)] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [Sub(RimShadowGroup)] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
        [Advanced(Rim Shadow Color)][Sub(RimShadowGroup)] _RimShadowColor0 ("Rim Shadow Color 0 (ID = 0)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor1 ("Rim Shadow Color 1 (ID = 1)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor2 ("Rim Shadow Color 2 (ID = 2)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor3 ("Rim Shadow Color 3 (ID = 3)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor4 ("Rim Shadow Color 4 (ID = 4)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor5 ("Rim Shadow Color 5 (ID = 5)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor6 ("Rim Shadow Color 6 (ID = 6)", Color) = (1,1,1,1)
        [Advanced][Sub(RimShadowGroup)] _RimShadowColor7 ("Rim Shadow Color 7 (ID = 7)", Color) = (1,1,1,1)
        [Advanced(Rim Shadow Feather)][Sub(RimShadowGroup)] _RimShadowFeather0 ("Rim Shadow Feather 0 (ID = 0)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather1 ("Rim Shadow Feather 1 (ID = 1)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather2 ("Rim Shadow Feather 2 (ID = 2)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather3 ("Rim Shadow Feather 3 (ID = 3)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather4 ("Rim Shadow Feather 4 (ID = 4)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather5 ("Rim Shadow Feather 5 (ID = 5)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather6 ("Rim Shadow Feather 6 (ID = 6)", Range(0.01, 0.99)) = 0.01
        [Advanced][Sub(RimShadowGroup)] _RimShadowFeather7 ("Rim Shadow Feather 7 (ID = 7)", Range(0.01, 0.99)) = 0.01
        [Advanced(Rim Shadow Width)][Sub(RimShadowGroup)] _RimShadowWidth0 ("Rim Shadow Width 0 (ID = 0)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth1 ("Rim Shadow Width 1 (ID = 1)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth2 ("Rim Shadow Width 2 (ID = 2)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth3 ("Rim Shadow Width 3 (ID = 3)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth4 ("Rim Shadow Width 4 (ID = 4)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth5 ("Rim Shadow Width 5 (ID = 5)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth6 ("Rim Shadow Width 6 (ID = 6)", Float) = 1
        [Advanced][Sub(RimShadowGroup)] _RimShadowWidth7 ("Rim Shadow Width 7 (ID = 7)", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        // stocking group
        [Main(StockingGroup, _, off, off)] _stockinggroup ("Stockings", Float) = 0
        [Sub(StockingGroup)] _EnableStocking ("With Stockings", Float) = 0
        [Sub(StockingGroup)] _StockRangeTex ("Stocking Range Texutre", 2D) = "black" { }
        [Sub(StockingGroup)] _Stockcolor ("Stockings Color", Color) = (1,1,1,1)
        [Sub(StockingGroup)] _StockDarkcolor ("Stockings Darkend Color", Color) = (1,1,1,1)
        [Sub(StockingGroup)] _StockDarkWidth ("Stockings Rim Width", Range(0, 0.96)) = 0.5
        [Sub(StockingGroup)] _Stockpower ("Stockings Power", Range(0.04, 1)) = 1
        [Sub(StockingGroup)] _Stockpower1 ("Stockings Lighted Width", Range(1, 32)) = 1
        [Sub(StockingGroup)] _StockSP ("Stockings Lighted Intensity", Range(0, 1)) = 0.25
        [Sub(StockingGroup)] _StockRoughness ("Stockings Texture Intensity", Range(0, 1)) = 1
        [Sub(StockingGroup)] _Stockthickness ("Stockings Thickness", Range(0, 1)) = 0
        // starry sky group
        [Main(StarryGroup, _, off, off)] _starrygroup ("Starry Sky", Float) = 0
        [SubToggle(StarryGroup)] _StarrySky ("With StarrySky", Float) = 0
        [Sub(StarryGroup)] _SkyTex ("StarrySky Base Texture", 2D) = "black" { }
        [Sub(StarryGroup)] _SkyMask ("StarrySky Mask Texture", 2D) = "black" { }
        [Sub(StarryGroup)] _SkyRange ("StarrySky Range", Range(-1, 1)) = 0
        [Sub(StarryGroup)] _SkyStarColor ("StarrySky Star Color", Color) = (1,1,1,1)
        [Sub(StarryGroup)] _SkyStarTex ("StarrySky Star Texture", 2D) = "black" { }
        [Sub(StarryGroup)] _SkyStarTexScale ("StarrySky Star Texture Scale", Float) = 1
        [Sub(StarryGroup)] _SkyStarSpeed ("StarrySky Star Speed(XY)", Vector) = (0,0,0,0)
        [Sub(StarryGroup)] _SkyStarDepthScale ("StarrySky Star DepthScale", Float) = 1
        [Sub(StarryGroup)] _SkyStarMaskTex ("StarrySky Star Mask Texture", 2D) = "whilte" { }
        [Sub(StarryGroup)] _SkyStarMaskTexScale ("StarrySky Star Mask Texture Scale", Float) = 1
        [Sub(StarryGroup)] _SkyStarMaskTexSpeed ("StarrySky Star flicker frequency", Range(0, 20)) = 0
        [Sub(StarryGroup)] _SkyFresnelColor ("StarrySky FresnelColor", Color) = (0,0,0,1)
        [Sub(StarryGroup)] _SkyFresnelBaise ("StarrySky FresnelBaise", Float) = 0
        [Sub(StarryGroup)] _SkyFresnelScale ("StarrySky FresnelScale", Float) = 0
        [Sub(StarryGroup)] _SkyFresnelSmooth ("StarrySky FresnelSmooth", Range(0, 0.5)) = 0
        [Sub(StarryGroup)] _OSScale ("StarrySky Model Scale", Range(0, 30)) = 1
        [Sub(StarryGroup)] _StarDensity ("StarrySky Density", Range(0, 1)) = 0.5
        [Sub(StarryGroup)] _StarMode ("StarrySky Mode", Range(0, 1)) = 0
        // flame crystal
        [Main(FlameCrystalGroup, _, off, off)] _flamecrystalgroup("Flame Crystal", Float) = 0 
        [SubToggle(FlameCrystalGroup)] _FlameCrystal ("With FlameCrystal", Float) = 0
        [Sub(FlameCrystalGroup)] _TangentDirTex ("Tangent Direction Texture", 2D) = "bump" { }
        [Sub(FlameCrystalGroup)] _FlameTex ("Fmale Texture", 2D) = "black" { }
        [Sub(FlameCrystalGroup)] _CrystalTex ("Crystal Texture", 2D) = "black" { }
        [Sub(FlameCrystalGroup)] _FlameID ("Material ID for Flame", Float) = 1
        [Sub(FlameCrystalGroup)] _FlameColorOut ("OutSide Flame Color", Color) = (1,1,1,1)
        [Sub(FlameCrystalGroup)] _FlameColorIn ("Inside Flame Color", Color) = (1,1,1,1)
        [Sub(FlameCrystalGroup)] _FlameHeight ("Flame Height", Range(0, 1)) = 1
        [Sub(FlameCrystalGroup)] _FlameWidth ("Flame Width", Range(0, 1)) = 1
        [Sub(FlameCrystalGroup)] _FlameSpeed ("Flame Waving Speed", Float) = 1
        [Sub(FlameCrystalGroup)] _FlameSwirilTexScale ("Flame Swiril Texture Scale", Float) = 1
        [Sub(FlameCrystalGroup)] _FlameSwirilSpeed ("Flame Swiril Speed", Float) = 1
        [Sub(FlameCrystalGroup)] _FlameSwirilScale ("Flame Swiril Scale", Float) = 1
        [Sub(FlameCrystalGroup)] _CrystalTransparency ("Crystal Transparency", Range(0, 1)) = 0.35
        [Sub(FlameCrystalGroup)] _CrystalRange1 ("Effect Progress in", Range(0, 1)) = 0
        [Sub(FlameCrystalGroup)] _CrystalRange2 ("Effect Progress out", Range(0, 1)) = 1
        [Sub(FlameCrystalGroup)] _ColorIntensity ("Effect Progress Intensity", Range(0, 1)) = 0.5
        [Sub(FlameCrystalGroup)] _EffectColor0 ("Effect Color 0 (ID = 0)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor1 ("Effect Color 1 (ID = 31)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor2 ("Effect Color 2 (ID = 63)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor3 ("Effect Color 3 (ID = 95)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor4 ("Effect Color 4 (ID = 127)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor5 ("Effect Color 5 (ID = 159)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor6 ("Effect Color 6 (ID = 192)", Color) = (0,0,0,1)
        [Sub(FlameCrystalGroup)] _EffectColor7 ("Effect Color 7 (ID = 223)", Color) = (0,0,0,1)
        // moon group
        [Main(MoonHaloGroup, _, off, off)]  _moonhalogroup("Moon Halo", float) = 0
        [SubToggle(MoonHaloGroup)] _UseMoonHalo ("Use MoonHalo", Float) = 0
        [Sub(MoonHaloGroup)] _MoonHaloRange ("MoonHalo Range", Range(0, 1)) = 0
        [Sub(MoonHaloGroup)] _MoonDir ("MoonHalo Dir", Vector) = (0,0,0,1)
        [Sub(MoonHaloGroup)] _MoonAnim ("MoonHalo Speed", Vector) = (0.35,0.65,0,0)
        [Sub(MoonHaloGroup)] _MoonUVType ("MoonHalo UV Shape", Float) = 0
        // overheated
        [Main(OverHeatedGroup, _, off, off)] _overheatedgroup ("OverHeated", Float) = 0
        [SubToggle(OverHeatedGroup)] _UseOverHeated ("Use OverHeated", Float) = 0
        [Sub(OverHeatedGroup)] _HeatInst ("OverHeated Intensity", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatDir ("OverHeated Direction", Vector) = (0,-1,0,0.25)
        [Sub(OverHeatedGroup)] _HeatedHeight ("OverHeated Height", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatedThreshould ("OverHeated Height", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatColor0 ("OverHeated Color", Color) = (1,1,1,1)
        [Sub(OverHeatedGroup)] _HeatColor1 ("OverHeated Color", Color) = (1,1,1,1)
        [Sub(OverHeatedGroup)] _HeatColor2 ("OverHeated Color", Color) = (1,1,1,1)
        // matcap group
        [Main(MatCapGroup, _, off, off)] _matcapgroup ("MatCap", Float) = 0 
        [SubToggle(MatCapGroup)] _UseMatcap ("Use MatCap", Float) = 0
        [Sub(MatCapGroup)] _MatCapTex ("MatCap", 2D) = "black" { }
        [Sub(MatCapGroup)] _MatCapMaskTex ("MatCap Mask", 2D) = "white" { }
        [Sub(MatCapGroup)] _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        [Sub(MatCapGroup)] _MatCapStrength ("_MatCapStrength", Range(0, 5)) = 1
        [Sub(MatCapGroup)] _MatCapStrengthInShadow ("_MatCapStrengthInShadow", Range(0, 1)) = 0.5
        // glint group
        [Main(GlintGroup, _, off, off)] _glintgroup ("Glint", Float) = 0
        [SubToggle(GlintGroup)] _UseGlint ("Use Glint", Float) = 0
        [Sub(GlintGroup)] _GlintWorldPosUV ("Use World Pos UV", Float) = 0
        [Sub(GlintGroup)] _GlintScaleBackface ("Backface Scale", Float) = 1
        [Sub(GlintGroup)] _GlintUVTillingY ("Y Tilling", Float) = 1
        [Sub(GlintGroup)] _GlobalGlintScale ("Global Glint Tilling", Float) = 100
        [Sub(GlintGroup)] _GlobalGlintPointScale ("Global Glint Scale", Range(0, 0.5)) = 0.1
        [Sub(GlintGroup)] _GlobalGlintIntensity ("Global Glint Instensity", Range(0, 5)) = 1
        [Sub(GlintGroup)] _GlobalGlintShadow ("Global Glint Shadow", Range(0, 1)) = 0.3
        [Sub(GlintGroup)] [HDR] _GlobalGlintColor ("Global Glint Color", Color) = (1,1,1,1)
        [Sub(GlintGroup)] _GlobalGlintDensity ("Global Glint Density", Range(0, 1)) = 0.3
        [Sub(GlintGroup)] _GlobalGlintSparkle ("Global Glint Sparkle", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlobalGlintSparkFreq ("Global Glint Sparkle Frequence", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlobalGlintViewFreq ("Global Glint View Frequence", Range(0, 10)) = 5
        [Sub(GlintGroup)] _GlintScale ("Glint Tilling", Float) = 300
        [Sub(GlintGroup)] _GlintPointScale ("Glint Scale", Range(0, 0.5)) = 0.1
        [Sub(GlintGroup)] _GlintDensity ("Glint Density", Range(0, 1)) = 1
        [Sub(GlintGroup)] _GlintConcentration ("Glint Contrast", Range(0, 2)) = 1
        [Sub(GlintGroup)] _GlintIntensity ("Glint Instensity", Range(0, 1)) = 0.5
        [Sub(GlintGroup)] [HDR] _GlintColor ("Glint Color", Color) = (1,1,1,1)
        [Sub(GlintGroup)] _GlintRandom ("Glint Random", Range(-1.5, 1.5)) = 1
        [Sub(GlintGroup)] _GlintMask ("Glint Texture", 2D) = "white" { }
        [Sub(GlintGroup)] _GlintSparkle ("Glint Sparkle", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlintSparkFreq ("Glint Sparkle Frequence", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlintViewFreq ("Glint View Frequence", Range(0, 10)) = 5
        // reflection 
        [Main(ReflectionGroup, _, off, off)] _reflectiongroup ("Reflection Group", float) = 0
        [Sub(ReflectionGroup)] _ReflectionRoughness ("Fake Reflection Roughness", Range(0.01, 16)) = 1
        [Sub(ReflectionGroup)] _ReflectionThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.5
        [Sub(ReflectionGroup)] _ReflectionSoftness ("Fake Reflection Softness", Range(0, 1)) = 0.05
        [Sub(ReflectionGroup)] _ReflectionBlendThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _ReflectionReversedThreshold ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _FakeRefBlendIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _FakeRefAddIntensity ("Fake Reflection Threshold", Range(0, 1)) = 0.25
        [Sub(ReflectionGroup)] _ReflectionColor ("", Color) = (1,1,1,1)
        [Sub(ReflectionGroup)] _ReflectionBlendColor ("", Color) = (1,1,1,1)
        //  dissolve k y s
        [Main(DissolveGroup, _, off, off)] _dissolvegroup ("Dissolve", Float) = 0
        [SubToggle(DissolveGroup)] _DissoveON ("Enable Dissolve", Float) = 0
        [SubToggle(DissolveGroup)] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
        [Sub(DissolveGroup)] _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
        [Sub(DissolveGroup)] _DissolveMap ("Dissolve Map", 2D) = "white" { }
        [Sub(DissolveGroup)] _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
        [Sub(DissolveGroup)] _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
        [Sub(DissolveGroup)] _DissolveDistortionIntensity ("", Float) = 0.01
        [Sub(DissolveGroup)] _DissolveOutlineSize1 ("", Float) = 0.05
        [Sub(DissolveGroup)] _DissolveOutlineSize2 ("", Float) = 0
        [Sub(DissolveGroup)] _DissolveOutlineOffset ("", Float) = 0
        [Sub(DissolveGroup)] _DissolveOutlineColor1 ("", Color) = (1,1,1,1)
        [Sub(DissolveGroup)] _DissolveOutlineColor2 ("", Color) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissoveDirecMask ("", Float) = 2
        [Sub(DissolveGroup)] _DissolveMapAdd ("", Float) = 0
        [Sub(DissolveGroup)] _DissolveOutlineSmoothStep ("", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveUV ("", Range(0, 1)) = 0
        [Sub(DissolveGroup)] _DissolveUVSpeed ("", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveMask ("Dissolve Mask", 2D) = "white" { }
        [Sub(DissolveGroup)] _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
        [Sub(DissolveGroup)] _DissolvePosMaskPos ("", Vector) = (1,0,0,1)
        [Sub(DissolveGroup)] _DissolvePosMaskWorldON ("", Float) = 0
        [Sub(DissolveGroup)] _DissolvePosMaskRootOffset ("", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolvePosMaskFilpOn ("", Float) = 0
        [Sub(DissolveGroup)] _DissolvePosMaskOn ("", Float) = 0
        [Sub(DissolveGroup)] _DissolveMaskUVSet ("", Range(0, 1)) = 0
        [Sub(DissolveGroup)] _DissolveUseDirection ("_DissolveUseDirection", Float) = 0
        [Sub(DissolveGroup)] _DissolveCenter ("_DissolveCenter", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveDiretcionXYZ ("_DissolveDiretcionXYZ", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolvePosMaskGlobalOn ("_DissolvePosMaskGlobalOn", Float) = 0
        // add light
        [Main(AddLightGroup, _, off, off)] _addlightgroup ("Add Light Group", Float) = 0
        [Sub(AddLightGroup)] _AddLightOffset ("Add Light Offset", Range(0, 1)) = 0.5
        [Sub(AddLightGroup)] _AddLightStrengthen ("Add Light Strengthen", Range(0, 3)) = 0.3
        [Sub(AddLightGroup)] _AddLightFeather ("Add Light Feather", Range(0, 0.1)) = 0.03
        // bloom group
        [Main(BloomGroup, _, off, off)] _bloomgroup ("Extra Bloom", float) = 0
        [Advanced(Bloom Intensity)] [Sub(BloomGroup)] _mBloomIntensity0 ("Bloom Intensity 0 (ID = 0)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity1 ("Bloom Intensity 1 (ID = 31)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity2 ("Bloom Intensity 2 (ID = 63)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity3 ("Bloom Intensity 3 (ID = 95)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity4 ("Bloom Intensity 4 (ID = 127)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity5 ("Bloom Intensity 5 (ID = 159)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity6 ("Bloom Intensity 6 (ID = 192)", Float) = 0
        [Advanced][Sub(BloomGroup)] _mBloomIntensity7 ("Bloom Intensity 7 (ID = 223)", Float) = 0
        [Advanced(Bloom Color)][Sub(BloomGroup)] _mBloomColor0 ("Bloom Color 0 (ID = 0)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor1 ("Bloom Color 1 (ID = 31)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor2 ("Bloom Color 2 (ID = 63)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor3 ("Bloom Color 3 (ID = 95)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor4 ("Bloom Color 4 (ID = 127)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor5 ("Bloom Color 5 (ID = 159)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor6 ("Bloom Color 6 (ID = 192)", Color) = (1,1,1,1)
        [Advanced][Sub(BloomGroup)] _mBloomColor7 ("Bloom Color 7 (ID = 223)", Color) = (1,1,1,1)
        [Advanced(Bloom Color)][Sub(BloomGroup)] _CustomParamA0 ("CustomParamA 0 (ID = 0)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA1 ("CustomParamA 1 (ID = 31)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA2 ("CustomParamA 2 (ID = 63)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA3 ("CustomParamA 3 (ID = 95)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA4 ("CustomParamA 4 (ID = 127)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA5 ("CustomParamA 5 (ID = 159)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA6 ("CustomParamA 6 (ID = 192)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamA7 ("CustomParamA 7 (ID = 223)", Float) = 1
        [Advanced(Bloom Color)][Sub(BloomGroup)] _CustomParamB0 ("CustomParamB 0 (ID = 0)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB1 ("CustomParamB 1 (ID = 31)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB2 ("CustomParamB 2 (ID = 63)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB3 ("CustomParamB 3 (ID = 95)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB4 ("CustomParamB 4 (ID = 127)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB5 ("CustomParamB 5 (ID = 159)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB6 ("CustomParamB 6 (ID = 192)", Float) = 1
        [Advanced][Sub(BloomGroup)] _CustomParamB7 ("CustomParamB 7 (ID = 223)", Float) = 1
        
        [Main(HeightGroup, _, off, off)] _heightgroup ("Height Light Group", Float) = 0
        [SubToggle(HeightGroup)] _UseHeightLerp ("Enable Height Light", Float) = 0
        [Sub(HeightGroup)] _CharaWorldSpaceOffset ("World Space Offset", Float) = 0
        [Sub(HeightGroup)]_ES_HeightLerpBottom ("Height Bottom", Float) = 0
        [Sub(HeightGroup)]_ES_HeightLerpTop ("Height Top", Float) = 1
        [Advanced(Colors)][Sub(HeightGroup)] _ES_HeightLerpBottomColor ("Light Bottom Color", Color) = (0.5,0.5,0.5,1)
        [Advanced][Sub(HeightGroup)] _ES_HeightLerpMiddleColor ("Light Middle Color", Color) = (1,1,1,1)
        [Advanced][Sub(HeightGroup)] _ES_HeightLerpTopColor ("Light Top Color", Color) = (1,1,1,1)


        [Main(RenderinGroup, _, off, off)] _renderinggroup("Rendering Settings", float) = 0
        [Sub(RenderinGroup)] _StencilRef ("Stencil Ref", Float) = 16
        [Sub(RenderinGroup)] _StencilOP ("Stencil Op", Float) = 2
        [Sub(RenderinGroup)] _StencilComp ("Stencil Comp", Float) = 8
        [Sub(RenderinGroup)] _StencilMask ("Stencil Read Mask", Float) = 255
        [Sub(RenderinGroup)] _RenderingMode ("Rendering Mode", Float) = 0
        [Sub(RenderinGroup)] _ZWrite ("ZWrite", Float) = 1
        [SubEnum(RenderinGroup, UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [SubEnum(RenderinGroup, UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [SubEnum(RenderinGroup,UnityEngine.Rendering.CullMode)] _CullMode ("CullMode", Float) = 2
        // place to collect all the scripted values
        [Main(ScriptedGroup, _, off, off)] _scriptedvalues ("Scripted Values", Float) = 0
        
        [SubToggle(ScriptedGroup)] _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 0
        [Sub(ScriptedGroup)] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
    }
    SubShader
    {
        HLSLINCLUDE 
        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityShaderVariables.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "UnityInstancing.cginc"
        #include "includes/HonkaiStarRail-header.hlsl"
        #include "includes/HonkaiStarRail-common.hlsl"
        #include "includes/HonkaiStarRail-base_input.hlsl"
        #include "includes/HonkaiStarRail-base_declaration.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull [_CullMode]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex base_vertex
            #pragma fragment base_pixel
            // make fog work
            #pragma multi_compile_fog

            #include "includes/HonkaiStarRail-base_program.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "LWGUI.LWGUI"
}
