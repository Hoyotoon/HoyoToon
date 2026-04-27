Shader "HoyoToon/Genshin Impact/Character/Uber/Base"
{
    Properties
    {
        
        [Main(MainSettings)] _DummyMainSettings ("MainSettings", Float) = 0
        [Sub(MainSettings)] _ElementViewEleID ("Element ID", Float) = 0
        [SubToggle(MainSettings)] _UseMaskInShadowExport ("Use Mask In Shadow Export", Float) = 0
        [SubToggle(MainSettings)] _UsePVMask1 ("Use PV Mask 1", Float) = 0
        [Sub(MainSettings)] [NoScaleOffset] _PVMask1 ("PV Mask 1 (R Eyelashe, G White Eye, B Eyebrow, A Pupil)", 2D) = "black" { }
        [SubToggle(MainSettings)] _UsePVMask2 ("Use PV Mask 2", Float) = 0
        [Sub(MainSettings)] [NoScaleOffset] _PVMask2 ("PV Mask 2 (R Mouth, G Teeth)", 2D) = "black" { }
        [SubToggle(MainSettings)] _FlareType ("Flare Type (When causing flare)", Float) = 0
        [SubToggle(MainSettings)] _UseNPCMultiEye ("Use NPC MultiEye", Float) = 0
        [SubEnum(MainSettings, Big, 0, Mid, 1, Small, 2)] _NPCMultiEyeSize ("NPC Eye Size", Float) = 0
        [Main(CharacterMainTex)] _CharacterMainTex ("Character Main Texture", Float) = 0
        [Sub(CharacterMainTex)] _MainTex ("Main Tex", 2D) = "white" { }
        [SubEnum(CharacterMainTex, None, 0, AlphaTest, 1, Emission, 2, FaceBlush, 3)] _MainTexAlphaUse ("Main Tex Alpha Use", Float) = 0
        [Sub(CharacterMainTex)] _MainTexAlphaCutoff ("Main Tex Alpha Cutoff", Range(0, 1)) = 0.5
        [Sub(CharacterMainTex)] _FaceBlushStrength ("Face Blush Strength", Range(0, 1)) = 0
        [Sub(CharacterMainTex)] _FaceBlushColor ("Face Blush Color", Color) = (1,0.8,0.7,1)
        [Sub(CharacterMainTex)] _Color ("Tint Color 1", Color) = (1,1,1,1)
        [SubToggle(CharacterMainTex)] _UseEyeMask ("Use Eye Mask Clip", Float) = 0
        [Sub(CharacterMainTex)] [NoScaleOffset] _EyeMask ("Eye Mask (R)", 2D) = "white" { }
        [Sub(CharacterMainTex)] _HairTransparentValue ("Hair Transparent Value", Range(0, 1)) = 0.5
        [SubToggle(CharacterMainTex)] _UseHairAlphaLimitation ("Use Hair Alpha Limitation", Float) = 0
        [SubRemap01Slider (CharacterMainTex)] _HairTransRemapHori ("Hair Transparent Limitation Horizontal", Vector) = (0.8,1,1,0)
        [SubRemap01Slider(CharacterMainTex)] _HairTransRemapVert ("Hair Transparent Limitation Vertical", Vector) = (0.4,0.8,1,0)
        [Sub(CharacterMainTex)] _HairShadowLightShift ("Hair Shadow Light Shift", Range(0, 3)) = 0.7
        [SubRemap01Slider (CharacterMainTex)] _HairShadowVerticalRemap ("Hair Shadow Vertical Remap", Vector) = (0,1,1,0)
        [Sub(CharacterMainTex)] _HairShadowStencilShift ("Hair Shadow Shift", Vector) = (0,0,0,0)
        [SubToggle(CharacterMainTex)] _UseHairAlphaMask ("Use Hair Alpha Mask With Bump B", Float) = 0
        [Sub(CharacterMainTex)] _HairShadowExtrusion ("Hair Shadow Extrusion", Range(0, 2)) = 1
        [SubEnum(CharacterMainTex, UnityEngine.Rendering.BlendMode)] _HairSrcBlendMode ("Hair Src Blend Mode", Float) = 5
        [SubEnum(CharacterMainTex, UnityEngine.Rendering.BlendMode)] _HairDstBlendMode ("Hair Dst Blend Mode", Float) = 10
        [SubEnum(CharacterMainTex, UnityEngine.Rendering.BlendOp)] _HairBlendOP ("Hair Blend Op Mode", Float) = 0
        [Sub(CharacterMainTex)] _DesaturateScale ("Desaturate Scale", Range(0, 1)) = 0
        [Main(Material Masks, MATERIAL_MASK)] _UseMaterialMasksTex ("Material Masks", Float) = 0
        [Sub(Material Masks)] _MaterialMasksTex ("Material Masks", 2D) = "black" { }
        [Main(Light Map, TOON_LIGHTMAP_ON)] _UseToonLightMap ("Light Map", Float) = 0
        [SubToggle(Light Map)] _UseLightMapColorAO ("Use Light Map Color.g For AO", Float) = 1
        [Sub(Light Map)] [NoScaleOffset] _LightMapTex ("Light Map Tex (RGB)", 2D) = "gray" { }
        [Main(Bump Map, BUMP_TEXTURELINE_MAP)] _UseBumpMap ("Bump Map", Float) = 0
        [Sub(Bump Map)] [NoScaleOffset] _BumpMap ("Bump Map(RG) SDF(B)", 2D) = "black" { }
        [Sub(Bump Map)] _BumpScale ("Bump Scale", Range(0.0001, 1)) = 1
        [SubToggle(Bump Map)] _UseMobileBumpCompressSmooth ("Use Mobile Bump Compress Smooth", Float) = 0
        [Main(Character Shadow)] _CharacterShadow ("Character Shadow", Float) = 0
        [Sub(Character Shadow)] _FirstShadowMultColor ("Warm Shadow Color 1", Color) = (0.9,0.7,0.75,1)
        [Sub(Character Shadow)] _CoolShadowMultColor ("Cool Shadow Color 1", Color) = (0.9,0.7,0.75,1)
        [SubToggle(Character Shadow)] _UseVertexColorAO ("Use Vertex Color.r For AO", Float) = 1
        [SubToggle(Character Shadow)] _UseCoolShadowColorOrTex ("Use Cool Shadow Color Or Tex", Float) = 0
        [Sub(Character Shadow)] _LightArea ("Light Area Threshold", Range(0, 1)) = 0.5
        [SubToggle(Character Shadow)] _UseShadowTransition ("Use Shadow Transition (only work when shadow ramp is off)", Float) = 0
        [Sub(Character Shadow)] _ShadowTransitionRange ("Shadow Transition Range 1", Range(0.001, 0.2)) = 0.01
        [Sub(Character Shadow)] _ShadowTransitionSoftness ("Shadow Transition Softness 1", Range(0, 2)) = 0.5
        [Main(Shadow Ramp, SHADOW_RAMP_ON)] _UseShadowRamp ("Shadow Ramp", Float) = 0
        [SubMHYPackedGradient(Shadow Ramp, _ShadowRampTex1 _ShadowRampTex2 _ShadowRampTex3 _ShadowRampTex4 _ShadowRampTex5 _CoolShadowRampTex1 _CoolShadowRampTex2 _CoolShadowRampTex3 _CoolShadowRampTex4 _CoolShadowRampTex5)] _PackedShadowRampTex ("Packed Shadow Ramp Tex", 2D) = "grey" { }
        [Sub(Shadow Ramp)] _ShadowRampWidth ("Shadow Ramp Width", Range(0.01, 10)) = 1
        [SubToggle(Shadow Ramp)] _UseVertexRampWidth ("Use Vertex Shadow Ramp Width", Float) = 0
        [Sub(Shadow Ramp)] _useShadowSoft ("use ShadowSoft?", Range(0, 1)) = 0
        [Sub(Shadow Ramp)] _shadowSoftRange ("shadow SoftRange", Range(0, 2)) = 1
        [Main(Character Specular)] _UseSpecular ("Character Specular", Float) = 0
        [Sub(Character Specular)] _Shininess ("Specular Shininess 1", Range(0.1, 100)) = 10
        [Sub(Character Specular)] _SpecMulti ("Specular Multiply Factor 1", Range(0, 1)) = 0.1
        [Sub(Character Specular)] _SpecualrInShaow ("Specular in Shadow", Range(0, 1)) = 0
        [Sub(Character Specular)] [HDR] _SpecularColor ("Specular Color 1", Color) = (1,1,1,1)
        [Sub(Character Specular)] _SpecOpacity ("Specular Opacity 1", Range(0, 1)) = 0
        [Main(Character PlaneClips, ENABLE_CHARACTER_PLANECLIPS_ON)] _UseCharacterPlaneClips ("Character PlaneClips", Float) = 0
        [Main(Metal Material, METAL_MAT)] _MetalMaterial ("Metal Material", Float) = 0
        [Sub(Metal Material)] _MTMap ("Metal Map", 2D) = "white" { }
        [Sub(Metal Material)] _MTMapBrightness ("Metal Map Brightness", Float) = 1
        [Sub(Metal Material)] _MTMapTileScale ("Metal Map Tile Scale", Float) = 1
        [Sub(Metal Material)] [HDR] _MTMapLightColor ("Metal Map Light Color", Color) = (1,1,1,1)
        [Sub(Metal Material)] [HDR] _MTMapDarkColor ("Metal Map Dark Color", Color) = (0,0,0,0)
        [Sub(Metal Material)] _MTShadowMultiColor ("Metal Shadow Multiply Color", Color) = (0.8,0.8,0.8,0.8)
        [Sub(Metal Material)] _MTShininess ("Metal Shininess", Float) = 11
        [Sub(Metal Material)] _MTSpecularScale ("Metal Specular Scale", Float) = 60
        [Sub(Metal Material)] _MTSpecularAttenInShadow ("Metal Specular Attenuation in Shadow", Range(0, 1)) = 0.2
        [Sub(Metal Material)] [HDR] _MTSpecularColor ("Metal Specular Color", Color) = (1,1,1,1)
        [SubToggle(Metal Material)] _MTUseSpecularRamp ("Use Metal Specular Ramp", Float) = 0
        [SubMMDGradient(Metal Material)] _MTSpecularRamp ("Specular Ramp", 2D) = "grey" { }
        [Sub(Metal Material)] _MTSharpLayerOffset ("Sharp Highlight Offset", Range(0.001, 1)) = 1
        [Sub(Metal Material)] [HDR] _MTSharpLayerColor ("Sharp High Light Color", Color) = (1,1,1,1)
        [Sub(Metal Material)] _MTSpecularOffset ("Specular  Offset", Range(0, 1)) = 0
        [Sub(Metal Material)] _MTSpecularShadowScale ("MTSpecular ShadowScale", Range(0, 1)) = 0
        [SubToggle(Metal Material)] _MTSpecularAO ("Specular Affected by AO", Float) = 0
        [Main(Character Emission)] _CharacterEmission ("Character Emission", Float) = 0
        [Sub(Character Emission)] _EmissionScaler ("Emission Scaler", Range(0, 100)) = 1
        [HideInInspector] _EmissionScaler_State ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_Value1 ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_Value2 ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_LerpParam ("", Vector) = (0,0,0,0) 
        [Sub(Character Emission)] _EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
        [Sub(Character Emission)] [HDR] _EmissionColor_MHY1 ("Emission Color 1", Color) = (1,1,1,1)
        [Sub(Character Emission)] _EmissionScaler1 ("Emission Scaler 1", Range(0, 100)) = 1
        [SubToggle(Character Emission)] _EnableEmissionBloom ("Outline EnableEmission", Float) = 0
        [Sub(Character Emission)] [HDR] _EmissionBloomColor ("Outline EmissionColor", Color) = (1,1,1,1)
        [Sub(Character Emission)] _EmissionBloomScale ("Outline EmissionScale", Range(0, 5)) = 1.3
        [Main(Texture Line, ENABLE_TEXTURE_LINE_ON)] _DummyTextureLine ("Texture Line", Float) = 0
        [Sub(Texture Line)] _TextureLineThickness ("Texture Line Thickness", Range(0, 1)) = 0.6
        [Sub(Texture Line)] _TextureLineSmoothness ("Texture Line Smoothness", Range(0, 0.5)) = 0.1
        [Sub(Texture Line)] _TextureLineDistanceControl ("Thickness Inc/Max, smooth start Depth", Vector) = (0.1,0.6,1,1)
        [Sub(Texture Line)] [HDR] _TextureLineMultiplier ("Texture Line Multiplier", Color) = (1,1,1,1)
        [Main(Outline)] _DummyOutline ("Outline", Float) = 0
        [Sub(Outline)] _OutlineWidth ("Outline Width 1", Range(0, 100)) = 0.04
        [Sub(Outline)] [HDR] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        [Sub(Outline)] _OutLineZOffset ("OutLine Back Face Z Offset", Range(0, 0.1)) = 0
        [Sub(Outline)] _OutLineIntensity ("Outline With Albedo", Range(0, 1)) = 0
        [Sub(Outline)] _MaxOutlineZOffset ("Max Outline Z Offset", Range(0, 100)) = 1
        [SubToggle(Outline)] _OutlineOffsetBlockBChannel ("Offset Block B Channel", Float) = 0
        [Sub(Outline)] _OutlineWidthAdjustZs ("Outline Width Adjust Dist Start (near, middle, far)", Vector) = (0.01,2,6,0)
        [Sub(Outline)] _OutlineWidthAdjustScales ("Outline Width Adjust Scale (near, middle, far)", Vector) = (0.105,0.245,0.6,0)
        [Main(Material 2)] _DummyMaterial2 ("Material 2", Float) = 0
        [SubToggle(Material 2)] _UseMaterial2 ("Use Material 2", Float) = 0
        [Sub(Material 2)] _Color2 ("Tint Color 2", Color) = (1,1,1,1)
        [Sub(Material 2)] _EmissionScaler2 ("Emission Scaler 2", Range(0, 100)) = 1
        [Sub(Material 2)] [HDR] _EmissionColor_MHY2 ("Emission Color 2", Color) = (1,1,1,1)
        [Sub(Material 2)] _FirstShadowMultColor2 ("Warm Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 2)] _CoolShadowMultColor2 ("Cool Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 2)] _Shininess2 ("Specular Shininess 2", Range(0.1, 100)) = 10
        [Sub(Material 2)] _SpecMulti2 ("Specular Multiply Factor 2", Range(0, 1)) = 0.1
        [Sub(Material 2)] _SpecOpacity2 ("Specular Opacity 2", Range(0, 1)) = 0
        [Sub(Material 2)] [HDR] _SpecularColor2 ("Specular Color 2", Color) = (1,1,1,1)
        [Sub(Material 2)] [HDR] _CharacterCubeColor2 ("Character Cube Color 2", Color) = (0,0,0,0)
        [Sub(Material 2)] _OutLineIntensity2 ("Outline Intensity 2", Range(0, 1)) = 0
        [Sub(Material 2)] _OutlineColor2 ("Outline Color 2", Color) = (0,0,0,1)
        [Sub(Material 2)] _ShadowTransitionRange2 ("Shadow Transition Range 2", Range(0.001, 0.2)) = 0.01
        [Sub(Material 2)] _ShadowTransitionSoftness2 ("Shadow Transition Softness 2", Range(0, 2)) = 0.5
        [Sub(Material 2)] _useShadowSoft2 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(Material 2)] _shadowSoftRange2 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(Material 3)] _DummyMaterial3 ("Material 3", Float) = 0
        [SubToggle(Material 3)] _UseMaterial3 ("Use Material 3", Float) = 0
        [Sub(Material 3)] _Color3 ("Tint Color 3", Color) = (1,1,1,1)
        [Sub(Material 3)] _EmissionScaler3 ("Emission Scaler 3", Range(0, 100)) = 1
        [Sub(Material 3)] [HDR] _EmissionColor_MHY3 ("Emission Color 3", Color) = (1,1,1,1)
        [Sub(Material 3)] _FirstShadowMultColor3 ("Warm Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 3)] _CoolShadowMultColor3 ("Cool Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 3)] _Shininess3 ("Specular Shininess 3", Range(0.1, 100)) = 10
        [Sub(Material 3)] _SpecMulti3 ("Specular Multiply Factor 3", Range(0, 1)) = 0.1
        [Sub(Material 3)] _SpecOpacity3 ("Specular Opacity 3", Range(0, 1)) = 0
        [Sub(Material 3)] [HDR] _SpecularColor3 ("Specular Color 3", Color) = (1,1,1,1)
        [Sub(Material 3)] [HDR] _CharacterCubeColor3 ("Character Cube Color 3", Color) = (0,0,0,0)
        [Sub(Material 3)] _OutLineIntensity3 ("Outline Intensity 3", Range(0, 1)) = 0
        [Sub(Material 3)] _OutlineColor3 ("Outline Color 3", Color) = (0,0,0,1)
        [Sub(Material 3)] _ShadowTransitionRange3 ("Shadow Transition Range 3", Range(0.001, 0.2)) = 0.01
        [Sub(Material 3)] _ShadowTransitionSoftness3 ("Shadow Transition Softness 3", Range(0, 2)) = 0.5
        [Sub(Material 3)] _useShadowSoft3 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(Material 3)] _shadowSoftRange3 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(Material 4)] _DummyMaterial4 ("Material 4", Float) = 0
        [SubToggle(Material 4)] _UseMaterial4 ("Use Material 4", Float) = 0
        [Sub(Material 4)] _Color4 ("Tint Color 4", Color) = (1,1,1,1)
        [Sub(Material 4)] _EmissionScaler4 ("Emission Scaler 4", Range(0, 100)) = 1
        [Sub(Material 4)] [HDR] _EmissionColor_MHY4 ("Emission Color 4", Color) = (1,1,1,1)
        [Sub(Material 4)] _FirstShadowMultColor4 ("Warm Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 4)] _CoolShadowMultColor4 ("Cool Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 4)] _Shininess4 ("Specular Shininess 4", Range(0.1, 100)) = 10
        [Sub(Material 4)] _SpecMulti4 ("Specular Multiply Factor 4", Range(0, 1)) = 0.1
        [Sub(Material 4)] _SpecOpacity4 ("Specular Opacity 4", Range(0, 1)) = 0
        [Sub(Material 4)] [HDR] _SpecularColor4 ("Specular Color 4", Color) = (1,1,1,1)
        [Sub(Material 4)] [HDR] _CharacterCubeColor4 ("Character Cube Color 4", Color) = (0,0,0,0)
        [Sub(Material 4)] _OutLineIntensity4 ("Outline Intensity 4", Range(0, 1)) = 0
        [Sub(Material 4)] _OutlineColor4 ("Outline Color 4", Color) = (0,0,0,1)
        [Sub(Material 4)] _ShadowTransitionRange4 ("Shadow Transition Range 4", Range(0.001, 0.2)) = 0.01
        [Sub(Material 4)] _ShadowTransitionSoftness4 ("Shadow Transition Softness 4", Range(0, 2)) = 0.5
        [Sub(Material 4)] _useShadowSoft4 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(Material 4)] _shadowSoftRange4 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(Material 5)] _DummyMaterial5 ("Material 5", Float) = 0
        [SubToggle(Material 5)] _UseMaterial5 ("Use Material 5", Float) = 0
        [Sub(Material 5)] _Color5 ("Tint Color 5", Color) = (1,1,1,1)
        [Sub(Material 5)] _EmissionScaler5 ("Emission Scaler 5", Range(0, 100)) = 1
        [Sub(Material 5)] [HDR] _EmissionColor_MHY5 ("Emission Color 5", Color) = (1,1,1,1)
        [Sub(Material 5)] _FirstShadowMultColor5 ("Warm Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 5)] _CoolShadowMultColor5 ("Cool Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        [Sub(Material 5)] _Shininess5 ("Specular Shininess 5", Range(0.1, 100)) = 10
        [Sub(Material 5)] _SpecMulti5 ("Specular Multiply Factor 5", Range(0, 1)) = 0.1
        [Sub(Material 5)] _SpecOpacity5 ("Specular Opacity 5", Range(0, 1)) = 0
        [Sub(Material 5)] [HDR] _SpecularColor5 ("Specular Color 5", Color) = (1,1,1,1)
        [Sub(Material 5)] [HDR] _CharacterCubeColor5 ("Character Cube Color 5", Color) = (0,0,0,0)
        [Sub(Material 5)] _OutLineIntensity5 ("Outline Intensity 5", Range(0, 1)) = 0
        [Sub(Material 5)] _OutlineColor5 ("Outline Color 5", Color) = (0,0,0,1)
        [Sub(Material 5)] _ShadowTransitionRange5 ("Shadow Transition Range 5", Range(0.001, 0.2)) = 0.01
        [Sub(Material 5)] _ShadowTransitionSoftness5 ("Shadow Transition Softness 5", Range(0, 2)) = 0.5
        [Sub(Material 5)] _useShadowSoft5 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(Material 5)] _shadowSoftRange5 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(Face Map, FACE_MAP_NEW_ON)] _UseFaceMapNew ("Face Map", Float) = 0
        [Sub(Face Map)] [NoScaleOffset] _FaceMapTex ("Face Map Tex (A Linear)", 2D) = "gray" { }
        [Sub(Face Map)] _FaceMapRotateOffset ("Face Map Rotate Offset", Range(-1, 1)) = 0
        [Sub(Face Map)] _FaceMapSoftness ("Face Map Softness", Range(0.000001, 1)) = 0.000001
        [Main(Hair Map, HAIR_MAP_ON)] _UseHairMap ("Hair Map", Float) = 0
        [SubToggle(Hair Map)] _UseBumpAsAOMask ("Use Bump.r as AO", Float) = 0
        [Sub(Hair Map)] _AOShadowWarpScale ("AO ShadowWarpScale", Range(0, 1)) = 1
        [SubEnum(Hair Map, All, 0, Mat1, 1, Mat2, 2, Mat3, 3, Mat4, 4, Mat5, 5)] _SelectMatID ("Select MatID", Float) = 0
        [Main(Back Face, BACK_FACE_ON)] _DrawBackFace ("Back Face", Float) = 0
        [SubToggle(Back Face)] _UseBackFaceUV2 ("Use Back Face UV 2", Float) = 1
        [SubToggle(Back Face)] _BackFaceAlphaClipWithUV1 ("Back Face Alpha Clip With UV 1", Float) = 0
        [SubToggle(Back Face)] _BackFaceLighting ("Back Face Lighting", Float) = 0
        [Main(Main Texture Tint, MAIN_TEX_COLORING_ON)] _MainTexColoring ("Main Texture Tint", Float) = 0
        [Sub(Main Texture Tint)] [HDR] _MainTexTintColor ("Main Tex Tint Color", Color) = (1,1,1,1)
        [Main(HitColor)] _DummyHitColor ("HitColor", Float) = 0
        [Sub(HitColor)] _HitColor ("HitColor", Color) = (0,0,0,0)
        [Sub(HitColor)] _ElementRimColor ("Element Rim Color", Color) = (0,0,0,0)
        [Sub(HitColor)] _HitColorScaler ("HitColor Scaler", Float) = 6
        [Sub(HitColor)] _HitColorFresnelPower ("HitColor Fresnel Power", Float) = 1.5
        [Sub(HitColor)] _EmissionStrengthLerp ("Emission Strength Lerp", Range(0, 1)) = 0
        [Main(UseDither)] _DummyUseDither ("UseDither", Float) = 0
        [SubToggle(UseDither)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(UseDither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Main(UseFakePoint, ENABLE_FAKEPOINT_ON)] _UseFakePoint ("UseFakePoint", Float) = 0
        [SubToggle(UseFakePoint)] _EnableFakePoint ("Enable FakePoint", Float) = 0
        [Sub(UseFakePoint)] _FakePointNoiseTex ("Fake PointNoiseTex", 2D) = "white" { }
        [Sub(UseFakePoint)] _FakePointColor ("Fake PointColor", Color) = (1,1,1,1)
        [Sub(UseFakePoint)] _FakePointRange ("Fake PointRange", Float) = 1
        [Sub(UseFakePoint)] _FakePointIntensity ("Fake PointIntensity", Float) = 1
        [Sub(UseFakePoint)] _FakePointPosition ("Fake PointPosition", Vector) = (0,0,0,0)
        [Sub(UseFakePoint)] _FakePointReflection ("Fake PointReflection", Float) = 1
        [Sub(UseFakePoint)] _FakePointFrequency ("Fake PointFrequency", Float) = 0
        [Sub(UseFakePoint)] _FakePointFrequencyMin ("Fake PointFrequencyMin", Float) = 0
        [Sub(UseFakePoint)] _FakePointSkinIntensity ("Fake PointSkinIntensity", Float) = 1
        [Sub(UseFakePoint)] _FakePointSkinSaturate ("Fake PointSkinSaturate", Float) = 0
        [Main(TexturePerformance, ENABLE_PERFORMANCE_ON)] _EnablePerformance ("Enable Performance", Float) = 0
        [SubToggle(TexturePerformance, ENABLE_PACK_LIGHT_ON)] _PackageLightMapToggle ("Package Light Map Toggle", Float) = 0
        [SubEnum(TexturePerformance, Layer1, 0, Layer2, 1, Layer3, 2)] _LightMapLayerEnum ("Light Map Layer Enum", Float) = 0
        [Sub(TexturePerformance)] [NoScaleOffset] _PackageLightMap ("Package Light Map", 2D) = "white" { }
        [Sub(TexturePerformance)] [NoScaleOffset] _LightMapBlueNoise ("Blue Noise", 2D) = "white" { }
        [Sub(TexturePerformance)] _LightMapBlurSize ("Blur Size", Range(0, 10)) = 1
        [SubToggle(TexturePerformance, ENABLE_PACK_NORMAL_ON)] _PackageNormalMapToggle ("Package Normal Map Toggle", Float) = 0
        [SubEnum(TexturePerformance, Layer1, 0, Layer2, 1, Layer3, 2)] _NormalMapLayerEnum ("Normal Map Layer Enum", Float) = 0
        [Sub(TexturePerformance)] [NoScaleOffset] _NormalPackageMap ("Package Normal Map", 2D) = "white" { }
        [SubToggle(TexturePerformance)] _isNativeMainNormal ("Native Main Normal Toggle", Float) = 0
        [Sub(TexturePerformance)] _NormalMapOffset ("Normal Map Offset", Float) = 0
        [Sub(TexturePerformance)] _NormalMapScale ("Normal Map Scale", Float) = 0
        [Sub(TexturePerformance)] [NoScaleOffset] _TextureLinePackageMap ("Texture Line Package Map", 2D) = "black" { }
        [Main(FixedForNormal)] _DummyFixedForNormal ("Fixed Normal", Float) = 0
        [Main(NyxStateMainSettings)] _MainSettingsGroup ("Nyx State MainSettings", Float) = 0
        [SubMHYPackedGradient(NyxStateMainSettings, _NyxStateColorRamp _NyxStateTimeOfDayRamp)] _NyxStateOutlineColorRamp ("Color Ramp (RGB)", 2D) = "gray" { }
        [Sub(NyxStateMainSettings)] _NyxStateOutlineColorScale ("Color Scale", Float) = 1
        [Sub(NyxStateMainSettings)] _NyxStateOutlineColorOnBodyMultiplier ("OnBody Color Scale", Color) = (1,1,1,1)
        [Sub(NyxStateMainSettings)] [NoScaleOffset] _NyxStateOutlineNoise ("Color and VertAnim Noise (RG)", 2D) = "gray" { }
        [SubVector2(NyxStateMainSettings)] _NyxStateOutlineColorNoiseScale ("Color Noise Scale", Vector) = (2,2,0,0)
        [Sub(NyxStateMainSettings)] _NyxStateOutlineColorNoiseAnim ("Color Noise Anim", Vector) = (0.05,0.05,0,0)
        [Sub(NyxStateMainSettings)] _NyxStateOutlineColorNoiseTurbulence ("Color Noise Turbulence", Range(0, 1)) = 0.25
        [Sub(NyxStateMainSettings)] [NoScaleOffset] _TempNyxStatePaintMaskTex ("Paint Mask", 2D) = "black" { }
        [SubEnum(NyxStateMainSettings, R, 0, G, 1, B, 2, A, 3)] _TempNyxStatePaintMaskChannel ("Paint Mask Channel", Float) = 0
        [Sub(NyxStateMainSettings)] _NyxStateOutlineColorOnBodyOpacity ("Paint Mask Multiplier", Range(0, 1)) = 1
        [Main(NyxStateOutline)] _OutlineGroup ("Nyx State Outline", Float) = 0
        [Sub(NyxStateOutline)] _NyxStateOutlineWidthScale ("Width Scale", Float) = 5
        [SubVector3(NyxStateOutline)] _NyxStateOutlineWidthVarietyWithResolution ("Variety with Resolution", Vector) = (1080,0,0,0)
        [SubToggle(NyxStateOutline)] _NyxStateEnableOutlineWidthScaleHeightLerp ("Enable Width Scale Height Lerp", Float) = 0
        [SubVector2(NyxStateOutline)] _NyxStateOutlineWidthScaleRange ("Width Scale Lerp Range", Vector) = (1,1,0,0)
        [SubMHYRemap01Range(NyxStateOutline)] _NyxStateOutlineWidthScaleLerpHeightRange ("Lerp Height Range", Vector) = (0,1,1,0)
        [SubVector2(NyxStateOutline)] _NyxStateOutlineVertAnimNoiseScale ("VertAnim Noise Scale", Vector) = (2,2,0,0)
        [SubVector2(NyxStateOutline)] _NyxStateOutlineVertAnimNoiseAnim ("VertAnim Noise Anim", Vector) = (0.05,0.05,0,0)
        [Sub(NyxStateOutline)] _NyxStateOutlineVertAnimScale ("VertAnim Scale", Float) = 30
        [SubToggle(NyxStateOutline)] _NyxStateEnableOutlineVertAnimScaleHeightLerp ("Enable VertAnim Scale Height Lerp", Float) = 0
        [SubVector2(NyxStateOutline)] _NyxStateOutlineVertAnimScaleRange ("VertAnim Scale Lerp Range", Vector) = (1,1,0,0)
        [SubMHYRemap01Range(NyxStateOutline)] _NyxStateOutlineVertAnimScaleLerpHeightRange ("Lerp Height Range", Vector) = (0,1,1,0)
        [Dx11ShaderPerformance(DeferredUpdate)] _Dx11ShaderScore ("Dx11 Shader Score", Float) = 0
        [HideInInspector] _Dx11ShaderVsScore ("Dx11 Shader Vs Score", Float) = 0
        [HideInInspector] _Dx11ShaderPsScore ("Dx11 Shader Ps Score", Float) = 0
        [Header(Others)] _MHYZBias ("Z Bias", Float) = 0
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnit ("Polygon Offset Unit", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnit ("Outline Polygon Offset Unit", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _OutlineMotionVectorsCull ("Outline Motion Vector Cull Mode", Float) = 1
        [Space(10)] [Header(Depth Mode)] [Enum(Off, 0, On, 1)] _Zwrite ("ZWrite Mode", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _Ztest ("Ztest Mode", Float) = 4
        [Header(Blend Mode)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Src Blend Mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Dst Blend Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOP ("BlendOp Mode", Float) = 0
        _TransparentAlpha ("Transparent Alpha", Range(0, 1)) = 1
        [HideInInspector] _Scale ("Scale Compared", Float) = 0.01
        [HideInInspector] _TextureBiasWhenDithering ("Texture Bias When Dithering", Float) = -1
        [HideInInspector] _CharacterAmbientSensorShadowOn ("", Float) = 0
        [HideInInspector] _CharacterAmbientSensorForceShadowOn ("", Float) = 0
        [HideInInspector] _CharacterAmbientSensorColorOn ("", Float) = 0
        [HideInInspector] _EnableCustomParams ("Enable Custom Params", Float) = 0
        [HideInInspector] _EnableDistanceLerp ("Enable Distance Lerp", Float) = 0
        [HideInInspector] _SingleColorOutputColor ("Single Color Output Color", Color) = (1,1,1,1)
        [HideInInspector] _PlaneNormalVecU ("Plane Normal Vector U", Vector) = (0,0,0,0)
        [HideInInspector] _PlaneNormalVecV ("Plane Normal Vector V", Vector) = (0,0,0,0)
        [HideInInspector] _VSSKinningPoseTex ("VSSKinningPoseTex", 2D) = "black" { }
    }
    SubShader  
    {
        Tags { "Distortion" = "None" "EntityUseType" = "Character" "IGNOREPROJECTOR" = "true" "OutlineType" = "Complex" "QUEUE" = "Geometry" "Reflected" = "Reflected" "RenderType" = "Opaque" }        
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        ENDHLSL
        Pass
        {
            Name "BODY"
            Tags  {  "DebugView" = "On" "Distortion" = "None" "EntityUseType" = "Character" "IGNOREPROJECTOR" = "true" "LIGHTMODE" = "HYBRIDDEFERRED" "OutlineType" = "Complex" "QUEUE" = "Geometry" "Reflected" = "Reflected" "RenderType" = "Opaque" "tex2DOverride" = "CharacterTex2D" }
            // Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            // Blend [_SrcBlendMode] [_DstBlendMode], One Zero
            // Zwrite [_Zwrite]
            // Ztest [_Ztest]
            // Cull [_CullMode]

            HLSLPROGRAM
            #include "Include/Struct.hlsl"
            #include "Include/Declarations.hlsl"
            #include "Include/Common.hlsl"
            #include "Include/Program.hlsl"

            #pragma shader_feature_local MATERIAL_MASK
            #pragma shader_feature_local TOON_LIGHTMAP_ON
            #pragma shader_feature_local BUMP_TEXTURELINE_MAP
            #pragma shader_feature_local SHADOW_RAMP_ON
            #pragma shader_feature_local ENABLE_CHARACTER_PLANECLIPS_ON
            #pragma shader_feature_local METAL_MAT
            #pragma shader_feature_local ENABLE_TEXTURE_LINE_ON
            #pragma shader_feature_local FACE_MAP_NEW_ON
            #pragma shader_feature_local HAIR_MAP_ON
            #pragma shader_feature_local BACK_FACE_ON
            #pragma shader_feature_local MAIN_TEX_COLORING_ON
            #pragma shader_feature_local ENABLE_FAKEPOINT_ON
            #pragma shader_feature_local ENABLE_PERFORMANCE_ON
            #pragma shader_feature_local ENABLE_PACK_LIGHT_ON
            #pragma shader_feature_local ENABLE_PACK_NORMAL_ON


            #pragma vertex vert_base
            #pragma fragment frag_base
            ENDHLSL
            
        }
    }
    CustomEditor "LWGUI.LWGUI" 
}