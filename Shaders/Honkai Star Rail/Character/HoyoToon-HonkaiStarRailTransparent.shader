Shader "HoyoToon/Honkai Star Rail/Character/Transparent"
{
    Properties
    {
        //  group name, keyword name, folding state, display toggle
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Textures)] _TexturesGroup ("Texture & Settings", Float) = 0
        [Tex(Textures)] _MainTex ("Main Texture", 2D) = "white" {}
        [Tex(Textures)] _LightMap ("Light Map", 2D) = "grey" { }
        [SubEnum(Textures, UV1, 0, UV2, 1)] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [SubEnum(Textures, UV1, 0, UV2, 1)] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [Sub(Textures)] _CharaWorldSpaceOffset ("World Space Offset", Vector) = (0,0,0,0)
        [SubGroup(MainGroup, ColorGroup)] _ColorGroup("Colors", Float) = 0
        [Sub(ColorGroup)] _Color ("Color", Color) = (1,1,1,1)
        [Sub(ColorGroup)] _BackColor ("BackColor", Color) = (1,1,1,1)
        [Sub(ColorGroup)] _VertexShadowColor ("Vertex Shadow Color", Color) = (0,0,0,1)
        [Sub(ColorGroup)] _VertexColorSwitch ("Vertex Color Switch", Vector) = (1,1,1,1)

        // material lut
        // [Advanced(Material LUT)][SubToggle(MainGroup)]
        [SubGroup(MainGroup, LutGroup)] _LutGroup("Material LookUp Table", Float) = 0
        [SubToggle(LutGroup)] _UseMaterialValuesLUT ("Use Mat Lut", Float) = 0
        [Tex(LutGroup)] _MaterialValuesPackLUT ("Mat Pack LUT", 2D) = "white" { }        
        // alpha cutoff
        [SubGroup(MainGroup, AlphaGroup)] _AlphaGroup("Alpha Group", Float) = 0
        [Sub(AlphaGroup)] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        [Sub(AlphaGroup)]_AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Sub(AlphaGroup)]_AlphaTestThreshold ("AlphaTest Threshold", Range(0, 1)) = 0.5
        // hide character parts
        
        [SubGroup(MainGroup, HideParts)] _HideParts("Hide Parts", Float) = 0
        [SubToggle(HideParts)] _HideCharaParts ("Toggle Hide Chara Parts", Float) = 0
        [Sub(HideParts)] _ShowPartID ("Show Part ID", Range(0, 256)) = 0

        [Main(LightingGroup, _, off, off)] _LightingGroup ("Lighting Settings", Float) = 0
        [SubGroup(LightingGroup, Lights, _, off, off)] _LightsGroup ("Lights", Float) = 0
        [SubGroup(Lights, Fog)] _FogGroup("Fog", Float) = 0
        [Sub(Fog)] _FakeFogColor ("Fog Color", Color) = (1,1,1,1)
        [Sub(Fog)] _FakeFogDensity ("Fog Density", Float) = 0.005
        [Sub(Fog)] _FakeFogHeightFalloff ("Fog Height Falloff", Float) = 0.2
        [Sub(Fog)] _FakeFogStartHeight ("Fog Start Height", Float) = 0
        [SubGroup(Lights, PointLights)] _PointLights("Point Lights", Float) = 0
        [SubEnum(PointLights,Zero, 0, One, 1, Two, 2, Three, 3)] _FakePointLightNum ("Fake Point Lights Num", Int) = 0
        [SubGroup(PointLights, First)] _FakePointLight0 ("First Point Light", Float) = 0
        [Sub(First)] [HDR] _FakePointLight0Color ("Color", Color) = (1,1,1,1)
        [Sub(First)] _FakePointLight0Pos ("Pos", Vector) = (0,100,0,0)
        [Sub(First)] _FakePointLight0Intensity ("Intensity 0", Float) = 100
        [Sub(First)] _FakePointLight0AttenuationRadius ("Radius 0", Float) = 100
        [SubGroup(PointLights, Second)] _FakePointLight1 ("Second Point Light", Float) = 0
        [Sub(Second)] [HDR] _FakePointLight1Color ("Color", Color) = (1,1,1,1)
        [Sub(Second)] _FakePointLight1Pos ("Pos", Vector) = (0,100,0,0)
        [Sub(Second)] _FakePointLight1Intensity ("Intensity", Float) = 100
        [Sub(Second)] _FakePointLight1AttenuationRadius ("Radius", Float) = 100
        [SubGroup(PointLights, Third)] _FakePointLight2 ("Third Point Light", Float) = 0
        [Sub(Third)] [HDR] _FakePointLight2Color ("Color", Color) = (1,1,1,1)
        [Sub(Third)] _FakePointLight2Pos ("Position", Vector) = (0,100,0,0)
        [Sub(Third)] _FakePointLight2Intensity ("Intensity", Float) = 100
        [Sub(Third)] _FakePointLight2AttenuationRadius ("Radius", Float) = 100
        [SubGroup(Lights, Directional)] _DirectionalGroup("Directional Light", Float) = 0
        [SubEnum(Directional, Character Light,0,Fake Light,1)] _UseFakeDirectionalLight ("Toggle Fake Directional Light", Float) = 0
        [Sub(Directional)] _FakeDirectionalLightRotation ("Fake Directional Light Rotation", Vector) = (0,0,0,0)

        // shadow group        
        [SubGroup(LightingGroup, ShadowGroup, _, off, off)] _ShadowGroup ("Shadow Settings", Float) = 0
        [SubToggle(ShadowGroup)] _ES_CharacterToonRampMode ("Ramp Mode", Float) = 0
        [Tex(ShadowGroup)] _DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
        [Tex(ShadowGroup)] _DiffuseRampMultiTex ("Shadow Multiple Ramp", 2D) = "white" { }
        [Sub(ShadowGroup)] _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
        [SubToggle(ShadowGroup)] _ShadowBoost ("Shadow Boost Enable", Float) = 0
        [Sub(ShadowGroup)] _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
        [SubGroup(ShadowGroup, CGLighting)] _CGLighting("CG Lighting", Float) = 0
        [SubToggle(CGLighting)] _ES_LEVEL_ADJUST_ON ("Enable Level Adjust", Float) = 0
        [Sub(CGLighting)]_ES_LevelSkinLightColor ("Skin Shadow Color", Color) = (1, 1, 1, 0.5)
        [Sub(CGLighting)]_ES_LevelSkinShadowColor ("Skin Light Color", Color) = (1, 1, 1, 0.5)
        [Sub(CGLighting)]_ES_LevelHighLightColor ("Base Shadow Color", Color) = (1, 1, 1, 0.5)
        [Sub(CGLighting)]_ES_LevelShadowColor ("Base Light Color", Color) = (1, 1, 1, 0.5)
        [Sub(CGLighting)]_ES_LevelShadow ("Shadow Level", Range(0, 1)) = 0.0
        [Sub(CGLighting)]_ES_LevelMid ("Mid Level", Range(0, 1)) = 0.55
        [Sub(CGLighting)]_ES_LevelHighLight ("High Light Level", Range(0, 1)) = 1.0
        // specular group
        [SubGroup(LightingGroup, SpecularGroup, _, off, off)] _SpecularGroup ("Specular Settings", Float) = 0
        [SubGroup(SpecularGroup, SpecularColors)] _SpColors("Specular Colors", Float) = 0
        [Sub(SpecularColors)] _SpecularColor0 ("Color 0", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor1 ("Color 1", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor2 ("Color 2", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor3 ("Color 3", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor4 ("Color 4", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor5 ("Color 5", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor6 ("Color 6", Color) = (1,1,1,1)
        [Sub(SpecularColors)] _SpecularColor7 ("Color 7", Color) = (1,1,1,1)
        [SubGroup(SpecularGroup, SpecularShininess)] _SpShine("Specular Shininess", Float) = 0
        [Sub(SpecularShininess)] _SpecularShininess0 ("Shininess 0", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess1 ("Shininess 1", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess2 ("Shininess 2", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess3 ("Shininess 3", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess4 ("Shininess 4", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess5 ("Shininess 5", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess6 ("Shininess 6", Range(0.1, 500)) = 10
        [Sub(SpecularShininess)] _SpecularShininess7 ("Shininess 7", Range(0.1, 500)) = 10
        [SubGroup(SpecularGroup, SpecularRoughness)] _SpRough("Specular Roughness", Float) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness0 ("Roughness 0", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness1 ("Roughness 1", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness2 ("Roughness 2", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness3 ("Roughness 3", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness4 ("Roughness 4", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness5 ("Roughness 5", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness6 ("Roughness 6", Range(0, 1)) = 0
        [Sub(SpecularRoughness)] _SpecularRoughness7 ("Roughness 0", Range(0, 1)) = 0
        [SubGroup(SpecularGroup, SpecularIntensity)] _SpInt ("Specular Intensity", Float) = 0
        [Sub(SpecularIntensity)] _SpecularIntensity0 ("Intensity 0", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity1 ("Intensity 1", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity2 ("Intensity 2", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity3 ("Intensity 3", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity4 ("Intensity 4", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity5 ("Intensity 5", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity6 ("Intensity 6", Range(0, 50)) = 1
        [Sub(SpecularIntensity)] _SpecularIntensity7 ("Intensity 7", Range(0, 50)) = 1
        [Advanced(Scripted Values)][Sub(SpecularGroup)] _ES_SPColor ("Color", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _ES_SPIntensity ("intensity", Float) = 1.0

        // rim group
        [SubGroup(LightingGroup, RimGroup, _, off, off)] _rimlightgroup("Rim Light", Float) = 0
        [SubToggle(RimGroup)] _RimLightMode ("Use Lightmap as Mask", Range(0, 1)) = 1
        [Sub(RimGroup)] _RimLight ("Rim Light", Range(0, 1)) = 0
        [Sub(RimGroup)] _Rimintensity ("Rim Intensity", Float) = 1
        [Sub(RimGroup)] _RimWidth ("RimWidth", Float) = 1
        [Sub(RimGroup)] _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
        [Sub(RimGroup)] _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015
        [Sub(RimGroup)] [HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        [Sub(RimGroup)] _FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        [Sub(RimGroup)] _FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [SubGroup(RimGroup, RimColor)] _RimColorGroup("Rim Colors", Float) = 0
        [Sub(RimColor)] _RimColor0 ("RimColor 0 (ID = 0)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor1 ("RimColor 1 (ID = 31)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor2 ("RimColor 2 (ID = 63)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor3 ("RimColor 3 (ID = 95)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor4 ("RimColor 4 (ID = 127)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor5 ("RimColor 5 (ID = 159)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor6 ("RimColor 6 (ID = 192)", Color) = (1,1,1,1)
        [Sub(RimColor)] _RimColor7 ("RimColor 7 (ID = 223)", Color) = (1,1,1,1)
        [SubGroup(RimGroup, RimSoftness)] _RimSoftness("Rim Softness", Float) = 0
        [Sub(RimSoftness)] _RimEdgeSoftness0 ("Rim Edge Softness 0 (ID = 0)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness1 ("Rim Edge Softness 1 (ID = 1)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness2 ("Rim Edge Softness 2 (ID = 2)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness3 ("Rim Edge Softness 3 (ID = 3)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness4 ("Rim Edge Softness 4 (ID = 4)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness5 ("Rim Edge Softness 5 (ID = 5)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness6 ("Rim Edge Softness 6 (ID = 6)", Range(0.01, 0.9)) = 0.1
        [Sub(RimSoftness)] _RimEdgeSoftness7 ("Rim Edge Softness 7 (ID = 7)", Range(0.01, 0.9)) = 0.1
        [SubGroup(RimGroup, RimType)] _RimTypeGroup("Rim Type", Float) = 0
        [Sub(RimType)] _RimType0 ("Rim Blend Mode 0 (ID = 0)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType1 ("Rim Blend Mode 1 (ID = 1)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType2 ("Rim Blend Mode 2 (ID = 2)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType3 ("Rim Blend Mode 3 (ID = 3)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType4 ("Rim Blend Mode 4 (ID = 4)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType5 ("Rim Blend Mode 5 (ID = 5)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType6 ("Rim Blend Mode 6 (ID = 6)", Range(0, 1)) = 1
        [Sub(RimType)] _RimType7 ("Rim Blend Mode 7 (ID = 7)", Range(0, 1)) = 1
        [SubGroup(RimGroup, RimDarken)] _RimDarkenGroup("Rim Darken", Float) = 0
        [Sub(RimDarken)] _RimDark0 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark1 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark2 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark3 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark4 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark5 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark6 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimDarken)] _RimDark7 ("Rim Darken Value 0 (ID = 0)", Range(0, 1)) = 0.5
        [Advanced(Scripted Values)] [Sub(RimGroup)] _ES_RimLightWidth ("ES Rim Width", float) = 1.0
        [Advanced][Sub(RimGroup)] _ES_RimLightOffset ("Rim Offset", vector) = (0,0,0,0)
        [Advanced][Sub(RimGroup)] _ES_RimLightAddMode ("Rim Light Add Mode", Float) = 0.07
        // rim shadow
        [SubGroup(LightingGroup, RimShadowGroup, _, off, off)] _rimshadowgroup ("Rim Shadow", Float) = 0
        [Sub(RimShadowGroup)] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [Sub(RimShadowGroup)] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
        [Sub(RimShadowGroup)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        [SubGroup(RimShadowGroup, RimShadowColor)] _RimShadowColorGroup("Rim Shadow Colors", Float) = 0
        [Sub(RimShadowColor)] _RimShadowColor0 ("Rim Shadow Color 0 (ID = 0)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor1 ("Rim Shadow Color 1 (ID = 1)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor2 ("Rim Shadow Color 2 (ID = 2)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor3 ("Rim Shadow Color 3 (ID = 3)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor4 ("Rim Shadow Color 4 (ID = 4)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor5 ("Rim Shadow Color 5 (ID = 5)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor6 ("Rim Shadow Color 6 (ID = 6)", Color) = (1,1,1,1)
        [Sub(RimShadowColor)] _RimShadowColor7 ("Rim Shadow Color 7 (ID = 7)", Color) = (1,1,1,1)
        [SubGroup(RimShadowGroup, RimShadowFeather)] _RimShadowFeather("Rim Shadow Feather", Float) = 0
        [Sub(RimShadowFeather)] _RimShadowFeather0 ("Rim Shadow Feather 0 (ID = 0)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather1 ("Rim Shadow Feather 1 (ID = 1)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather2 ("Rim Shadow Feather 2 (ID = 2)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather3 ("Rim Shadow Feather 3 (ID = 3)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather4 ("Rim Shadow Feather 4 (ID = 4)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather5 ("Rim Shadow Feather 5 (ID = 5)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather6 ("Rim Shadow Feather 6 (ID = 6)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowFeather)] _RimShadowFeather7 ("Rim Shadow Feather 7 (ID = 7)", Range(0.01, 0.99)) = 0.01
        [SubGroup(RimShadowGroup, RimShadowWidth)] _RimShadowWidth("Rim Shadow Width", Float) = 0
        [Sub(RimShadowWidth)] _RimShadowWidth0 ("Rim Shadow Width 0 (ID = 0)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth1 ("Rim Shadow Width 1 (ID = 1)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth2 ("Rim Shadow Width 2 (ID = 2)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth3 ("Rim Shadow Width 3 (ID = 3)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth4 ("Rim Shadow Width 4 (ID = 4)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth5 ("Rim Shadow Width 5 (ID = 5)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth6 ("Rim Shadow Width 6 (ID = 6)", Float) = 1
        [Sub(RimShadowWidth)] _RimShadowWidth7 ("Rim Shadow Width 7 (ID = 7)", Float) = 1
        [Advanced(Scripted Values)] [Sub(RimShadowGroup)] _ES_RimShadowIntensity ("ES Rim Shadow Width", float) = 1.0
        [Advanced][Sub(RimShadowGroup)] _ES_RimShadowColor ("Rim Shadow Offset", vector) = (0,0,0,0)
        // eye highlight group
        // stocking group
        [SubGroup(LightingGroup, StockingGroup, _, off, off)] _stockinggroup ("Stockings", Float) = 0
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

        // matcap group
        [SubGroup(LightingGroup, MatCapGroup, _, off, off)] _matcapgroup ("MatCap", Float) = 0 
        [SubToggle(MatCapGroup)] _UseMatcap ("Use MatCap", Float) = 0
        [Sub(MatCapGroup)] _MatCapTex ("MatCap", 2D) = "black" { }
        [Sub(MatCapGroup)] _MatCapMaskTex ("MatCap Mask", 2D) = "white" { }
        [Sub(MatCapGroup)] _MatCapColor ("MatCapColor", Color) = (1,1,1,1)
        [Sub(MatCapGroup)] _MatCapStrength ("_MatCapStrength", Range(0, 5)) = 1
        [Sub(MatCapGroup)] _MatCapStrengthInShadow ("_MatCapStrengthInShadow", Range(0, 1)) = 0.5

         // outline group
        [Main(OutlineGroup, _, off, off)]  _Outline ("Outline", Range(0, 1)) = 0
        [Sub(OutlineGroup)] _OutlineColorIntensity ("Outline Color Intensity", Float) = 0
        [Sub(OutlineGroup)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [Sub(OutlineGroup)] _OutlineScale ("Outline Scale", Range(0, 1)) = 0.005
        [SubEnum(OutlineGroup, Normal, 0, Tangent, 1, UV2, 2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        [Sub(OutlineGroup)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        [Sub(OutlineGroup)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        [Sub(OutlineGroup)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        [Sub(OutlineGroup)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        [SubGroup(OutlineGroup, OutlineColor)] _OutlineColors ("Outline Color", Float) = 0
        [Sub(OutlineColor)] _OutlineColor0 ("Color 0 (ID = 0)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor1 ("Color 1 (ID = 31)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor2 ("Color 2 (ID = 63)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor3 ("Color 3 (ID = 95)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor4 ("Color 4 (ID = 127)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor5 ("Color 5 (ID = 159)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor6 ("Color 6 (ID = 192)", Color) = (0,0,0,1)
        [Sub(OutlineColor)] _OutlineColor7 ("Color 7 (ID = 223)", Color) = (0,0,0,1)
        // [Advanced(Scripted Values)][Sub(OutlineGroup)] _ES_OutLineDarkenVal ("ES Outline Darkened", float) = 0.0
        // [Advanced][Sub(OutlineGroup)] _ES_OutLineLightedVal ("ES Outline Lightened", float) = 0.0

        [Main(SpecialFX, _, off, off)] _SpecialFX ("Special Effects", Float) = 0
        // emission group
        [SubGroup(SpecialFX,EmissionGroup, _, off, off)] _EmissionGroup ("Emission Settings", Float) = 0
        [Sub(EmissionGroup)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        [Sub(EmissionGroup)] _EmissionIntensity ("Emission Texture", Float) = 0
        [Tex(EmissionGroup)] _EmissionTex ("Emission Tex", 2D) = "black" { }
        [Sub(EmissionGroup)] _EmissionTintColor ("Emission TintColor", Color) = (1,1,1,1)
        // starry sky group
        [SubGroup(SpecialFX, StarryGroup, _, off, off)] _starrygroup ("Starry Sky", Float) = 0
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
        // sun glasses, this is only in the transparent shader and is for things like kafkas glasses
        [SubGroup(SpecialFX, SunGlasses)] _SunglassesGroup ("Sun Glasses", Float) = 0
        [SubToggle(SunGlasses)] _Sunglasses ("Sunglasses", Float) = 0
        [Sub(SunGlasses)] _SunGlassesTilingOffset ("SunGlassesTilingOffset", Vector) = (1,1,0,0)
        [Sub(SunGlasses)] [HDR] _SunglassesSpecluarColor ("SunglassesSpecluarColor", Color) = (1,1,1,1)
        [Sub(SunGlasses)] _HighlightWidthL ("HighlightWidthL", Range(0, 2)) = 0
        [Sub(SunGlasses)] _HighlightWidthR ("HighlightWidthR", Range(0, 2)) = 0
        [Sub(SunGlasses)] _TotalSizeL ("TotalSizeL", Range(0, 2)) = 0.5
        [Sub(SunGlasses)] _TotalSizeR ("TotalSizeR", Range(0, 2)) = 0.5
        [Sub(SunGlasses)] _BlendRadiusL ("BlendRadiusL", Range(-1.1, 1.1)) = 0
        [Sub(SunGlasses)] _BlendRadiusR ("BlendRadiusR", Range(-1.1, 1.1)) = 0
        [Sub(SunGlasses)] _HighlightAngleL ("HighlightL", Float) = 0
        [Sub(SunGlasses)] _HighlightAngleR ("HighlightR", Float) = 0
        [Sub(SunGlasses)] _HighlightOffsetL ("_HighlightOffsetL", Float) = 0
        [Sub(SunGlasses)] _HighlightOffsetR ("_HighlightOffsetR", Float) = 0
        [Sub(SunGlasses)] _BendValue ("BendValue", Range(-1, 1)) = 1
        // flame crystal
        [SubGroup(SpecialFX, FlameCrystalGroup, _, off, off)] _flamecrystalgroup("Flame Crystal", Float) = 0 
        [SubToggle(FlameCrystalGroup)] _FlameCrystal ("With FlameCrystal", Float) = 0
        [Sub(FlameCrystalGroup)] _TangentDirTex ("Tangent Direction Texture", 2D) = "bump" { }
        [Sub(FlameCrystalGroup)] _FlameTex ("Fmale Texture", 2D) = "black" { }
        [Sub(FlameCrystalGroup)] _CrystalTex ("Crystal Texture", 2D) = "black" { }
        [SubIntRange(FlameCrystalGroup)] _FlameID ("Material ID for Flame", Range(0,7)) = 1
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
        [SubGroup(SpecialFX, MoonHaloGroup, _, off, off)]  _moonhalogroup("Moon Halo", float) = 0
        [SubToggle(MoonHaloGroup)] _UseMoonHalo ("Use MoonHalo", Float) = 0
        [Sub(MoonHaloGroup)] _MoonHaloRange ("MoonHalo Range", Range(0, 1)) = 0
        [Sub(MoonHaloGroup)] _MoonDir ("MoonHalo Dir", Vector) = (0,0,0,1)
        [Sub(MoonHaloGroup)] _MoonAnim ("MoonHalo Speed", Vector) = (0.35,0.65,0,0)
        [Sub(MoonHaloGroup)] _MoonUVType ("MoonHalo UV Shape", Float) = 0
        // overheated
        [SubGroup(SpecialFX,OverHeatedGroup, _, off, off)] _overheatedgroup ("OverHeated", Float) = 0
        [SubToggle(OverHeatedGroup)] _UseOverHeated ("Use OverHeated", Float) = 0
        [Sub(OverHeatedGroup)] _HeatInst ("OverHeated Intensity", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatDir ("OverHeated Direction", Vector) = (0,-1,0,0.25)
        [Sub(OverHeatedGroup)] _HeatedHeight ("OverHeated Height", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatedThreshould ("OverHeated Height", Range(0, 1)) = 0
        [Sub(OverHeatedGroup)] _HeatColor0 ("OverHeated Color", Color) = (1,1,1,1)
        [Sub(OverHeatedGroup)] _HeatColor1 ("OverHeated Color", Color) = (1,1,1,1)
        [Sub(OverHeatedGroup)] _HeatColor2 ("OverHeated Color", Color) = (1,1,1,1)
    
        // glint group
        [SubGroup(SpecialFX, GlintGroup, _, off, off)] _glintgroup ("Glint", Float) = 0
        [SubToggle(GlintGroup)] _UseGlint ("Use Glint", Float) = 0
        [Sub(GlintGroup)] _GlintWorldPosUV ("Use World Pos UV", Float) = 0
        [Sub(GlintGroup)] _GlintScaleBackface ("Backface Scale", Float) = 1
        [Sub(GlintGroup)] _GlintUVTillingY ("Y Tilling", Float) = 1
        [Title(GlintGroup, Local Glint Settings, 25)]
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
        [Title(GlintGroup, Global Glint Settings, 25)]
        [Sub(GlintGroup)] _GlobalGlintScale ("Global Glint Tilling", Float) = 100
        [Sub(GlintGroup)] _GlobalGlintPointScale ("Global Glint Scale", Range(0, 0.5)) = 0.1
        [Sub(GlintGroup)] _GlobalGlintIntensity ("Global Glint Instensity", Range(0, 5)) = 1
        [Sub(GlintGroup)] _GlobalGlintShadow ("Global Glint Shadow", Range(0, 1)) = 0.3
        [Sub(GlintGroup)] [HDR] _GlobalGlintColor ("Global Glint Color", Color) = (1,1,1,1)
        [Sub(GlintGroup)] _GlobalGlintDensity ("Global Glint Density", Range(0, 1)) = 0.3
        [Sub(GlintGroup)] _GlobalGlintSparkle ("Global Glint Sparkle", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlobalGlintSparkFreq ("Global Glint Sparkle Frequence", Range(0, 10)) = 2
        [Sub(GlintGroup)] _GlobalGlintViewFreq ("Global Glint View Frequence", Range(0, 10)) = 5
        [SubGroup(GlintGroup, CustomParamA)] _CustomParamAGroup("Custom Paramater A", Float) = 0
        [Sub(CustomParamA)] _CustomParamA0 ("CustomParamA 0 (ID = 0)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA1 ("CustomParamA 1 (ID = 31)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA2 ("CustomParamA 2 (ID = 63)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA3 ("CustomParamA 3 (ID = 95)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA4 ("CustomParamA 4 (ID = 127)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA5 ("CustomParamA 5 (ID = 159)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA6 ("CustomParamA 6 (ID = 192)", Float) = 1
        [Sub(CustomParamA)] _CustomParamA7 ("CustomParamA 7 (ID = 223)", Float) = 1
        [SubGroup(GlintGroup, CustomParamB)] _CustomParamBGroup("Custom Paramater B", Float) = 0
        [Sub(CustomParamB)] _CustomParamB0 ("CustomParamB 0 (ID = 0)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB1 ("CustomParamB 1 (ID = 31)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB2 ("CustomParamB 2 (ID = 63)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB3 ("CustomParamB 3 (ID = 95)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB4 ("CustomParamB 4 (ID = 127)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB5 ("CustomParamB 5 (ID = 159)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB6 ("CustomParamB 6 (ID = 192)", Float) = 1
        [Sub(CustomParamB)] _CustomParamB7 ("CustomParamB 7 (ID = 223)", Float) = 1
        // reflection 
        [SubGroup(SpecialFX,ReflectionGroup, _, off, off)] _reflectiongroup ("Reflection Group", float) = 0
        [Sub(ReflectionGroup)] _ReflectionRoughness ("Reflection Roughness", Range(0.01, 16)) = 1
        [Sub(ReflectionGroup)] _ReflectionThreshold ("Reflection Threshold", Range(0, 1)) = 0.5
        [Sub(ReflectionGroup)] _ReflectionSoftness ("Reflection Softness", Range(0, 1)) = 0.05
        [Sub(ReflectionGroup)] _ReflectionBlendThreshold ("Reflection Blend Threshold", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _ReflectionReversedThreshold ("Reflection Reversed Threshold", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _FakeRefBlendIntensity ("Reflection Blend Intensity", Range(0, 1)) = 0.1
        [Sub(ReflectionGroup)] _FakeRefAddIntensity ("Reflection Add Intensity", Range(0, 1)) = 0.25
        [Sub(ReflectionGroup)] _ReflectionColor ("Reflection Color", Color) = (0,0,0,1)
        [Sub(ReflectionGroup)] _ReflectionBlendColor ("Reflection Blend Color", Color) = (0,0,0,1)
        //  dissolve k y s
        [SubGroup(SpecialFX,DissolveGroup, _, off, off)] _dissolvegroup ("Dissolve", Float) = 0
        [SubToggle(DissolveGroup)] _DissoveON ("Enable Dissolve", Float) = 0
        [SubToggle(DissolveGroup)] _DissolveShadowOff ("Disable Dissolve Shadow", Float) = 0
        [Sub(DissolveGroup)] _DissolveRate ("Dissolve Rate", Range(0, 1)) = 0
        [Sub(DissolveGroup)] _DissolveMap ("Dissolve Map", 2D) = "white" { }
        [Sub(DissolveGroup)] _DissolveST ("Dissolve ST", Vector) = (1,1,0,0)
        [Sub(DissolveGroup)] _DistortionST ("Distortion ST", Vector) = (1,1,0,0)
        [Sub(DissolveGroup)] _DissolveDistortionIntensity ("Distortion Intensity", Float) = 0.01
        [Sub(DissolveGroup)] _DissolveOutlineSize1 ("Outline Size 1", Float) = 0.05
        [Sub(DissolveGroup)] _DissolveOutlineSize2 ("Outline Size 2", Float) = 0
        [Sub(DissolveGroup)] _DissolveOutlineOffset ("Outline Offset", Float) = 0
        [Sub(DissolveGroup)][HDR] _DissolveOutlineColor1 ("Outline Color 1", Color) = (1,1,1,1)
        [Sub(DissolveGroup)][HDR] _DissolveOutlineColor2 ("Outline Color 2", Color) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissoveDirecMask ("Direction Mask", Float) = 2
        [Sub(DissolveGroup)] _DissolveMapAdd ("Map Add", Float) = 0
        [Sub(DissolveGroup)] _DissolveOutlineSmoothStep ("Outline SmoothStep", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveUV ("UV0 -> UV1", Range(0, 1)) = 0
        [Sub(DissolveGroup)] _DissolveUVSpeed ("UV Speed", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveMask ("Dissolve Mask", 2D) = "white" { }
        [Sub(DissolveGroup)] _DissolveComponent ("MaskChannel RGBA=0/1", Vector) = (1,0,0,0)
        [Sub(DissolveGroup)] _DissolvePosMaskPos ("Mask Position", Vector) = (1,0,0,1)
        [SubToggle(DissolveGroup)] _DissolvePosMaskWorldON ("World Mask On/Off", Float) = 0
        [Sub(DissolveGroup)] _DissolvePosMaskRootOffset ("World Offset", Vector) = (0,0,0,0)
        [SubToggle(DissolveGroup)] _DissolvePosMaskFilpOn ("Mask Flip On/Off", Float) = 0
        [SubToggle(DissolveGroup)] _DissolvePosMaskOn ("Mask On/Off", Float) = 0
        [Sub(DissolveGroup)] _DissolveMaskUVSet ("UV Set", Range(0, 1)) = 0
        [SubToggle(DissolveGroup)] _DissolveUseDirection ("Use Direction", Float) = 0
        [Sub(DissolveGroup)] _DissolveCenter ("Center", Vector) = (0,0,0,0)
        [Sub(DissolveGroup)] _DissolveDiretcionXYZ ("XYZ Direction", Vector) = (0,0,0,0)
        [SubToggle(DissolveGroup)] _DissolvePosMaskGlobalOn ("Global Mask On/Off", Float) = 0 
        // bloom group
        [SubGroup(SpecialFX,BloomGroup, _, off, off)] _bloomgroup ("Extra Bloom", float) = 0
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
        // height light group, this is normally controlled by a script
        [SubGroup(SpecialFX, HeightGroup, _, off, off)] _heightgroup ("Height Light Group", Float) = 0
        [SubToggle(HeightGroup)] _UseHeightLerp ("Enable Height Light", Float) = 0
        [Sub(HeightGroup)]_ES_HeightLerpBottom ("Height Bottom", Float) = 0
        [Sub(HeightGroup)]_ES_HeightLerpTop ("Height Top", Float) = 1
        [Advanced(Colors)][Sub(HeightGroup)] _ES_HeightLerpBottomColor ("Light Bottom Color", Color) = (0.5,0.5,0.5,1)
        [Advanced][Sub(HeightGroup)] _ES_HeightLerpMiddleColor ("Light Middle Color", Color) = (1,1,1,1)
        [Advanced][Sub(HeightGroup)] _ES_HeightLerpTopColor ("Light Top Color", Color) = (1,1,1,1)
        // dither group
        [SubGroup(SpecialFX, DitherGroup, _, off, off)] _dithergroup("Dither Control Group", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [SubToggle(DitherGroup)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [Sub(DitherGroup)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1

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
    }
    SubShader
    {
        Tags
		{ 
		    "RenderType"="NonHair" 
			"Queue" = "Geometry+6" 
            "LightMode" = "ForwardBase"
		}
        HLSLINCLUDE 
        #define is_baseshader
        #define is_transparent
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/HonkaiStarRail-header.hlsl"
        #include "includes/HonkaiStarRail-base_input.hlsl"
        #include "includes/HonkaiStarRail-base_declaration.hlsl"
        #include "includes/HonkaiStarRail-common.hlsl"
        #include "includes/HonkaiStarRail-base_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"    
            Cull [_CullMode]
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref [_StencilRef]
                ReadMask 255
                WriteMask [_StencilMask]
                Comp [_StencilComp]
                Pass [_StencilOP]
                Fail Keep
                ZFail Keep
            }
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex base_vertex
            #pragma fragment base_pixel


            ENDHLSL
        }

        Pass
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Stencil
            {
                Ref 10
                ReadMask 255
                WriteMask 255
                CompFront Always
                CompBack Always
                FailFront Keep
                FailBack Keep
                PassFront Keep
                PassBack Replace
                ZFailFront Keep
                ZFailBack Keep
            }

            HLSLPROGRAM
            #pragma vertex outline_vertex
            #pragma fragment outline_pixel
            // make fog work
            #pragma multi_compile_fog

            // #include "includes/HonkaiStarRail-base_program.hlsl"

            ENDHLSL
        }
        
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
        // UsePass "HoyoToon/Honkai Star Rail/Character/Depth Caster/Shadow Pass"
    }
    CustomEditor "LWGUI.LWGUI" 
}
