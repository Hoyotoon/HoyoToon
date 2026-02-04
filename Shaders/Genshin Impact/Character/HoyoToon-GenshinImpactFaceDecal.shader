Shader "HoyoToon/Genshin Impact/Character/FaceDecal"
{
    Properties
    {
        [Main(MainSettings, _, off, off)] _DummyMainSettings ("MainSettings", Float) = 0
        [Sub(MainSettings)] _TessValue ("Tessellation Density", Range(1, 32)) = 1
        [Sub(MainSettings)] _PhongWeight ("Phong Weight", Range(0, 1)) = 0
        [SubToggle(MainSettings)] _UseNPCMultiEye ("Use NPC MultiEye", Float) = 0
        [SubEnum(MainSettings, Big, 0, Mid, 1, Small, 2)] _NPCMultiEyeSize ("NPC Eye Size", Float) = 0
        [Main(Textures, _, off, off)] _dummyTextures ("Texture Settings", Float) = 0
        [SubGroup(Textures, CharacterMainTex)] _CharacterMainTex ("Main Texture", Float) = 0
        [Tex(CharacterMainTex)] _MainTex ("Main Tex", 2D) = "white" { }
        [Sub(CharacterMainTex)] _Color ("Tint Color 1", Color) = (1,1,1,1)
        [Sub(CharacterMainTex)] _DesaturateScale ("Desaturate Scale", Range(0, 1)) = 0
        [SubGroup(CharacterMainTex, AlphaGroup)] _dummyAlpha ("Alpha Settings", Float) = 0
        [SubEnum(AlphaGroup, None, 0, AlphaTest, 1, Emission, 2, FaceBlush, 3)] _MainTexAlphaUse ("Main Tex Alpha Use", Float) = 0
        [Sub(AlphaGroup)] _MainTexAlphaCutoff ("Main Tex Alpha Cutoff", Range(0, 1)) = 0.5
        [Sub(AlphaGroup)] _FaceBlushStrength ("Face Blush Strength", Range(0, 1)) = 0
        [Sub(AlphaGroup)] _FaceBlushColor ("Face Blush Color", Color) = (1,0.8,0.7,1)
        [SubGroup(CharacterMainTex, EyeMask)] _dummyEyeMask ("Eye Mask for Stencil", Float) = 0
        [SubToggle(EyeMask)] _UseEyeMask ("Use Eye Mask Clip", Float) = 0
        [Tex(EyeMask)] [NoScaleOffset] _EyeMask ("Eye Mask (R)", 2D) = "white" { }
        [SubToggle(EyeMask)] _EnableEyeMaskDraw ("Enable Eye Mask Draw", float) = 0
        [SubToggle(EyeMask)] _DrawAlphaClipEye ("Enable Eye Mask Draw", float) = 0
        // this i think in a completely different shader
        [SubGroup(CharacterMainTex, HairTransparency)] _dummyHairTransparent ("Hair Transparency", Float) = 0
        [Sub(HairTransparency)] _HairTransparentValue ("Hair Transparent Value", Range(0, 1)) = 0.5
        [SubToggle(HairTransparency)] _UseHairAlphaLimitation ("Use Hair Alpha Limitation", Float) = 0
        [Sub(HairTransparency)] _HairTransRemapHori ("Hair Transparent Limitation Horizontal", Vector) = (0.8,1,1,0)
        [Sub(HairTransparency)] _HairTransRemapVert ("Hair Transparent Limitation Vertical", Vector) = (0.4,0.8,1,0)
        [Sub(HairTransparency)] _HairShadowLightShift ("Hair Shadow Light Shift", Range(0, 3)) = 0.7
        [Sub(HairTransparency)] _HairShadowVerticalRemap ("Hair Shadow Vertical Remap", Vector) = (0,1,1,0)
        [Sub(HairTransparency)] _HairShadowStencilShift ("Hair Shadow Shift", Vector) = (0,0,0,0)
        [SubToggle(HairTransparency)] _UseHairAlphaMask ("Use Hair Alpha Mask With Bump B", Float) = 0
        [Sub(HairTransparency)] _HairShadowExtrusion ("Hair Shadow Extrusion", Range(0, 2)) = 1
        [SubEnum(HairTransparency, UnityEngine.Rendering.BlendMode)] _HairSrcBlendMode ("Hair Src Blend Mode", Float) = 5
        [SubEnum(HairTransparency, UnityEngine.Rendering.BlendMode)] _HairDstBlendMode ("Hair Dst Blend Mode", Float) = 10
        [SubEnum(HairTransparency, UnityEngine.Rendering.BlendOp)] _HairBlendOP ("Hair Blend Op Mode", Float) = 0
        //
        [SubGroup(Textures, MaterialMasks, MATERIAL_MASK, off, on)] _UseMaterialMasksTex ("MaterialMasks", Float) = 0
        [Tex(MaterialMasks)] _MaterialMasksTex ("MaterialMasks", 2D) = "black" { }
        [SubGroup(Textures,LightMap, TOON_LIGHTMAP_ON, on, on)] _UseToonLightMap ("LightMap", Float) = 0
        [SubToggle(LightMap)] _UseLightMapColorAO ("Use LightMap Color.g For AO", Float) = 1
        [Tex(LightMap)] [NoScaleOffset] _LightMapTex ("LightMap Tex (RGB)", 2D) = "gray" { }
        [SubGroup(Textures, BumpMap, BUMP_TEXTURELINE_MAP, off, on)] _UseBumpMap ("BumpMap", Float) = 0
        [Tex(BumpMap)] [NoScaleOffset] _BumpMap ("BumpMap(RG) SDF(B)", 2D) = "black" { }
        [Sub(BumpMap)] _BumpScale ("Bump Scale", Range(0.0001, 1)) = 1
        [SubToggle(BumpMap)] _UseMobileBumpCompressSmooth ("Use Mobile Bump Compress Smooth", Float) = 0
        [Main(CharaccterShadow, _, off, off)] _CharacterShadow ("CharaccterShadow", Float) = 0
        [Sub(CharaccterShadow)] _ES_CharacterColorTone ("Shadow Tone (Cool -> Warm)", Range(0, 1)) = 1
        [Sub(CharaccterShadow)] _FirstShadowMultColor ("Warm Shadow Color 1", Color) = (0.9,0.7,0.75,1)
        [Sub(CharaccterShadow)] _CoolShadowMultColor ("Cool Shadow Color 1", Color) = (0.9,0.7,0.75,1)
        [SubToggle(CharaccterShadow)] _UseVertexColorAO ("Use Vertex Color.r For AO", Float) = 1
        [SubToggle(CharaccterShadow)] _UseCoolShadowColorOrTex ("Use Cool Shadow Color Or Tex", Float) = 0
        [Sub(CharaccterShadow)] _LightArea ("Light Area Threshold", Range(0, 1)) = 0.5
        [SubToggle(CharaccterShadow)] _UseShadowTransition ("Use Shadow Transition (only work when ShadowRamp is off)", Float) = 0
        [Sub(CharaccterShadow)] _ShadowTransitionRange ("Shadow Transition Range 1", Range(0.001, 0.2)) = 0.01
        [Sub(CharaccterShadow)] _ShadowTransitionSoftness ("Shadow Transition Softness 1", Range(0, 2)) = 0.5
        [SubGroup(CharaccterShadow,ShadowRamp, SHADOW_RAMP_ON, off, on)] _UseShadowRamp ("ShadowRamp", Float) = 0
        [Tex(ShadowRamp)] _PackedShadowRampTex ("Packed ShadowRamp Tex", 2D) = "grey" { }
        // [Tex(ShadowRamp, _ShadowRampTex1 _ShadowRampTex2 _ShadowRampTex3 _ShadowRampTex4 _ShadowRampTex5 _CoolShadowRampTex1 _CoolShadowRampTex2 _CoolShadowRampTex3 _CoolShadowRampTex4 _CoolShadowRampTex5)] _PackedShadowRampTex ("Packed ShadowRamp Tex", 2D) = "grey" { }
        [Sub(ShadowRamp)] _ShadowRampWidth ("ShadowRamp Width", Range(0.01, 10)) = 1
        [SubToggle(ShadowRamp)] _UseVertexRampWidth ("Use Vertex ShadowRamp Width", Float) = 0
        [Sub(ShadowRamp)] _useShadowSoft ("use ShadowSoft?", Range(0, 1)) = 0
        [Sub(ShadowRamp)] _shadowSoftRange ("shadow SoftRange", Range(0, 2)) = 1
        [Main(CharacterSpecular, _, off, off)] _UseSpecular ("CharacterSpecular", Float) = 0
        [Sub(CharacterSpecular)] _Shininess ("Specular Shininess 1", Range(0.1, 100)) = 10
        [Sub(CharacterSpecular)] _SpecMulti ("Specular Multiply Factor 1", Range(0, 1)) = 0.1
        [Sub(CharacterSpecular)] _SpecualrInShaow ("Specular in Shadow", Range(0, 1)) = 0
        [Sub(CharacterSpecular)]  _SpecularColor ("Specular Color 1", Color) = (1,1,1,1)
        [Sub(CharacterSpecular)] _SpecOpacity ("Specular Opacity 1", Range(0, 1)) = 0
        [SubGroup(CharacterSpecular, MetalMaterial, METAL_MAT, off, on)] _MetalMaterial ("MetalMaterial", Float) = 0
        [Tex(MetalMaterial)] _MTMap ("Metal Map", 2D) = "white" { }
        [Sub(MetalMaterial)] _MTMapBrightness ("Metal Map Brightness", Float) = 1
        [Sub(MetalMaterial)] _MTMapTileScale ("Metal Map Tile Scale", Float) = 1
        [Sub(MetalMaterial)]  _MTMapLightColor ("Metal Map Light Color", Color) = (1,1,1,1)
        [Sub(MetalMaterial)]  _MTMapDarkColor ("Metal Map Dark Color", Color) = (0,0,0,0)
        [Sub(MetalMaterial)] _MTShadowMultiColor ("Metal Shadow Multiply Color", Color) = (0.8,0.8,0.8,0.8)
        [Sub(MetalMaterial)] _MTShininess ("Metal Shininess", Float) = 11
        [Sub(MetalMaterial)] _MTSpecularScale ("Metal Specular Scale", Float) = 60
        [Sub(MetalMaterial)] _MTSpecularAttenInShadow ("Metal Specular Attenuation in Shadow", Range(0, 1)) = 0.2
        [Sub(MetalMaterial)]  _MTSpecularColor ("Metal Specular Color", Color) = (1,1,1,1)
        [SubToggle(MetalMaterial)] _MTUseSpecularRamp ("Use Metal Specular Ramp", Float) = 0
        [Tex(MetalMaterial)] _MTSpecularRamp ("Specular Ramp", 2D) = "grey" { }
        [Sub(MetalMaterial)] _MTSharpLayerOffset ("Sharp Highlight Offset", Range(0.001, 1)) = 1
        [Sub(MetalMaterial)]  _MTSharpLayerColor ("Sharp High Light Color", Color) = (1,1,1,1)
        [Sub(MetalMaterial)] _MTSpecularOffset ("Specular Offset", Range(0, 1)) = 0
        [Sub(MetalMaterial)] _MTSpecularShadowScale ("MTSpecular ShadowScale", Range(0, 1)) = 0
        [SubToggle(MetalMaterial)] _MTSpecularAO ("Specular Affected by AO", Float) = 0
        [Main(RimLight, _, off, off)] _dummyRimLight ("Character Rim Light", float) = 0
        [Sub(RimLight)] _ES_AvatarRimWidth ("Width", Float) = 1
        [Sub(RimLight)] _ES_AvatarRimWidthScale ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarFrontRimColor ("Front Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarFrontRimIntensity ("Width Scale", Float) = 1
        [Sub(RimLight)] _ES_AvatarBackRimColor ("Back Color", Color) = (1,1,1,1)
        [Sub(RimLight)] _ES_AvatarBackRimIntensity ("Back Intensity", Float) = 1
        [Main(CharacterEmission, _, off, off)] _CharacterEmission ("CharacterEmission", Float) = 0
        [Sub(CharacterEmission)] _EmissionScaler ("Emission Scaler", Range(0, 100)) = 1
        [HideInInspector] _EmissionScaler_State ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_Value1 ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_Value2 ("", Vector) = (0,0,0,0)
        [HideInInspector] _EmissionScaler_LerpParam ("", Vector) = (0,0,0,0)
        [Sub(CharacterEmission)] _EmissionColor_MHY ("Emission Color", Color) = (1,1,1,1)
        [Sub(CharacterEmission)]  _EmissionColor_MHY1 ("Emission Color 1", Color) = (1,1,1,1)
        [Sub(CharacterEmission)] _EmissionScaler1 ("Emission Scaler 1", Range(0, 100)) = 1
        [SubToggle(CharacterEmission)] _EnableEmissionBloom ("Outline EnableEmission", Float) = 0
        [Sub(CharacterEmission)]  _EmissionBloomColor ("Outline EmissionColor", Color) = (1,1,1,1)
        [Sub(CharacterEmission)] _EmissionBloomScale ("Outline EmissionScale", Range(0, 5)) = 1.3
        // [Main(TextureLine, ENABLE_TEXTURE_LINE_ON)] _DummyTextureLine ("TextureLine", Float) = 0
        // [Sub(TextureLine)] _TextureLineThickness ("TextureLine Thickness", Range(0, 1)) = 0.6
        // [Sub(TextureLine)] _TextureLineSmoothness ("TextureLine Smoothness", Range(0, 0.5)) = 0.1
        // [Sub(TextureLine)] _TextureLineDistanceControl ("Thickness Inc/Max, smooth start Depth", Vector) = (0.1,0.6,1,1)
        // [Sub(TextureLine)]  _TextureLineMultiplier ("TextureLine Multiplier", Color) = (1,1,1,1)
        [Main(Outline, ENABLE_OUTLINE_ON, off, on)] _DummyOutline ("Outline", Float) = 0
        [Sub(Outline)] _OutlineWidth ("Outline Width 1", Range(0, 100)) = 0.04
        [Sub(Outline)] _OutlineCorrectionWidth ("Outline Width Correction", Range(0, 100)) = 1.3
        [Sub(Outline)] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        [Sub(Outline)] _OutLineZOffset ("OutLine BackFace Z Offset", Range(0, 0.1)) = 0
        [Sub(Outline)] _OutLineIntensity ("Outline With Albedo", Range(0, 1)) = 0
        [Sub(Outline)] _MaxOutlineZOffset ("Max Outline Z Offset", Range(0, 100)) = 1
        [SubToggle(Outline)] _OutlineOffsetBlockBChannel ("Offset Block B Channel", Float) = 0
        [Sub(Outline)] _OutlineWidthAdjustZs ("Outline Width Adjust Dist Start (near, middle, far)", Vector) = (0.01,2,6,0)
        [Sub(Outline)] _OutlineWidthAdjustScales ("Outline Width Adjust Scale (near, middle, far)", Vector) = (0.105,0.245,0.6,0)
        [Main(MaterialTwo, _, off, off)] _DummyMaterial2 ("MaterialTwo", Float) = 0
        [SubToggle(MaterialTwo)] _UseMaterial2 ("Use MaterialTwo", Float) = 0
        [Sub(MaterialTwo)] _Color2 ("Tint Color 2", Color) = (1,1,1,1)
        [Sub(MaterialTwo)] _EmissionScaler2 ("Emission Scaler 2", Range(0, 100)) = 1
        [Sub(MaterialTwo)]  _EmissionColor_MHY2 ("Emission Color 2", Color) = (1,1,1,1)
        [Sub(MaterialTwo)] _FirstShadowMultColor2 ("Warm Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialTwo)] _CoolShadowMultColor2 ("Cool Shadow Color 2", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialTwo)] _Shininess2 ("Specular Shininess 2", Range(0.1, 100)) = 10
        [Sub(MaterialTwo)] _SpecMulti2 ("Specular Multiply Factor 2", Range(0, 1)) = 0.1
        [Sub(MaterialTwo)] _SpecOpacity2 ("Specular Opacity 2", Range(0, 1)) = 0
        [Sub(MaterialTwo)]  _SpecularColor2 ("Specular Color 2", Color) = (1,1,1,1)
        [Sub(MaterialTwo)]  _CharacterCubeColor2 ("Character Cube Color 2", Color) = (0,0,0,0)
        [Sub(MaterialTwo)] _OutLineIntensity2 ("Outline Intensity 2", Range(0, 1)) = 0
        [Sub(MaterialTwo)] _OutlineColor2 ("Outline Color 2", Color) = (0,0,0,1)
        [Sub(MaterialTwo)] _ShadowTransitionRange2 ("Shadow Transition Range 2", Range(0.001, 0.2)) = 0.01
        [Sub(MaterialTwo)] _ShadowTransitionSoftness2 ("Shadow Transition Softness 2", Range(0, 2)) = 0.5
        [Sub(MaterialTwo)] _useShadowSoft2 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(MaterialTwo)] _shadowSoftRange2 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(MaterialThree, _, off, off)] _DummyMaterial3 ("MaterialThree", Float) = 0
        [SubToggle(MaterialThree)] _UseMaterial3 ("Use MaterialThree", Float) = 0
        [Sub(MaterialThree)] _Color3 ("Tint Color 3", Color) = (1,1,1,1)
        [Sub(MaterialThree)] _EmissionScaler3 ("Emission Scaler 3", Range(0, 100)) = 1
        [Sub(MaterialThree)]  _EmissionColor_MHY3 ("Emission Color 3", Color) = (1,1,1,1)
        [Sub(MaterialThree)] _FirstShadowMultColor3 ("Warm Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialThree)] _CoolShadowMultColor3 ("Cool Shadow Multiply Color 3", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialThree)] _Shininess3 ("Specular Shininess 3", Range(0.1, 100)) = 10
        [Sub(MaterialThree)] _SpecMulti3 ("Specular Multiply Factor 3", Range(0, 1)) = 0.1
        [Sub(MaterialThree)] _SpecOpacity3 ("Specular Opacity 3", Range(0, 1)) = 0
        [Sub(MaterialThree)]  _SpecularColor3 ("Specular Color 3", Color) = (1,1,1,1)
        [Sub(MaterialThree)]  _CharacterCubeColor3 ("Character Cube Color 3", Color) = (0,0,0,0)
        [Sub(MaterialThree)] _OutLineIntensity3 ("Outline Intensity 3", Range(0, 1)) = 0
        [Sub(MaterialThree)] _OutlineColor3 ("Outline Color 3", Color) = (0,0,0,1)
        [Sub(MaterialThree)] _ShadowTransitionRange3 ("Shadow Transition Range 3", Range(0.001, 0.2)) = 0.01
        [Sub(MaterialThree)] _ShadowTransitionSoftness3 ("Shadow Transition Softness 3", Range(0, 2)) = 0.5
        [Sub(MaterialThree)] _useShadowSoft3 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(MaterialThree)] _shadowSoftRange3 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(MaterialFour, _, off, off)] _DummyMaterial4 ("MaterialFour", Float) = 0
        [SubToggle(MaterialFour)] _UseMaterial4 ("Use MaterialFour", Float) = 0
        [Sub(MaterialFour)] _Color4 ("Tint Color 4", Color) = (1,1,1,1)
        [Sub(MaterialFour)] _EmissionScaler4 ("Emission Scaler 4", Range(0, 100)) = 1
        [Sub(MaterialFour)]  _EmissionColor_MHY4 ("Emission Color 4", Color) = (1,1,1,1)
        [Sub(MaterialFour)] _FirstShadowMultColor4 ("Warm Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialFour)] _CoolShadowMultColor4 ("Cool Shadow Multiply Color 4", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialFour)] _Shininess4 ("Specular Shininess 4", Range(0.1, 100)) = 10
        [Sub(MaterialFour)] _SpecMulti4 ("Specular Multiply Factor 4", Range(0, 1)) = 0.1
        [Sub(MaterialFour)] _SpecOpacity4 ("Specular Opacity 4", Range(0, 1)) = 0
        [Sub(MaterialFour)]  _SpecularColor4 ("Specular Color 4", Color) = (1,1,1,1)
        [Sub(MaterialFour)]  _CharacterCubeColor4 ("Character Cube Color 4", Color) = (0,0,0,0)
        [Sub(MaterialFour)] _OutLineIntensity4 ("Outline Intensity 4", Range(0, 1)) = 0
        [Sub(MaterialFour)] _OutlineColor4 ("Outline Color 4", Color) = (0,0,0,1)
        [Sub(MaterialFour)] _ShadowTransitionRange4 ("Shadow Transition Range 4", Range(0.001, 0.2)) = 0.01
        [Sub(MaterialFour)] _ShadowTransitionSoftness4 ("Shadow Transition Softness 4", Range(0, 2)) = 0.5
        [Sub(MaterialFour)] _useShadowSoft4 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(MaterialFour)] _shadowSoftRange4 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(MaterialFive, _, off, off)] _DummyMaterial5 ("MaterialFive", Float) = 0
        [SubToggle(MaterialFive)] _UseMaterial5 ("Use MaterialFive", Float) = 0
        [Sub(MaterialFive)] _Color5 ("Tint Color 5", Color) = (1,1,1,1)
        [Sub(MaterialFive)] _EmissionScaler5 ("Emission Scaler 5", Range(0, 100)) = 1
        [Sub(MaterialFive)]  _EmissionColor_MHY5 ("Emission Color 5", Color) = (1,1,1,1)
        [Sub(MaterialFive)] _FirstShadowMultColor5 ("Warm Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialFive)] _CoolShadowMultColor5 ("Cool Shadow Multiply Color 5", Color) = (0.9,0.7,0.75,1)
        [Sub(MaterialFive)] _Shininess5 ("Specular Shininess 5", Range(0.1, 100)) = 10
        [Sub(MaterialFive)] _SpecMulti5 ("Specular Multiply Factor 5", Range(0, 1)) = 0.1
        [Sub(MaterialFive)] _SpecOpacity5 ("Specular Opacity 5", Range(0, 1)) = 0
        [Sub(MaterialFive)]  _SpecularColor5 ("Specular Color 5", Color) = (1,1,1,1)
        [Sub(MaterialFive)]  _CharacterCubeColor5 ("Character Cube Color 5", Color) = (0,0,0,0)
        [Sub(MaterialFive)] _OutLineIntensity5 ("Outline Intensity 5", Range(0, 1)) = 0
        [Sub(MaterialFive)] _OutlineColor5 ("Outline Color 5", Color) = (0,0,0,1)
        [Sub(MaterialFive)] _ShadowTransitionRange5 ("Shadow Transition Range 5", Range(0.001, 0.2)) = 0.01
        [Sub(MaterialFive)] _ShadowTransitionSoftness5 ("Shadow Transition Softness 5", Range(0, 2)) = 0.5
        [Sub(MaterialFive)] _useShadowSoft5 ("ShadowMap Soft", Range(0, 1)) = 0
        [Sub(MaterialFive)] _shadowSoftRange5 ("ShadowMap SoftRange", Range(0, 2)) = 0
        [Main(FaceMap, FACE_MAP_NEW_ON)] _UseFaceMapNew ("FaceMap", Float) = 0
        [Tex(FaceMap)] [NoScaleOffset] _FaceMapTex ("FaceMap Tex (A Linear)", 2D) = "gray" { }
        [Sub(FaceMap)] _FaceMapRotateOffset ("FaceMap Rotate Offset", Range(-1, 1)) = 0
        [Sub(FaceMap)] _FaceMapSoftness ("FaceMap Softness", Range(0.00000001, 1)) = 0.00000001
        [Main(FacialExpression)] _FacialUVExpressionEnable ("FacialExpression", Float) = 0
        [Sub(FacialExpression)] _FacialExpAtlasTex ("FacialExpAtlasTex", 2D) = "black" { }
        [SubToggle(FacialExpression)] _FacialExpEnable ("FacialExpEnable", Float) = 0
        [Sub(FacialExpression)] _FacialExpAtlasRows ("FacialExpAtlasRows", Float) = 8
        [Sub(FacialExpression)] _FacialExpIndex ("FacialExpIndex", Float) = 0
        [SubToggle(FacialExpression)] _FacialExpSplitIndex ("FacialExpSplitIndex", Float) = 0
        [Sub(FacialExpression)] _FacialExpLeftIndex ("FacialExpLeftIndex", Float) = 0
        [SubToggle(FacialExpression)] _FacialExpRightHoriFlip ("FacialExpRightHoriFlip", Float) = 0
        [Sub(FacialExpression)] _FacialExpRightScale ("FacialExpRightScale", Float) = 5
        [Sub(FacialExpression)] _FacialExpRightOffsetX ("FacialExpRightOffsetX", Float) = -0.13
        [Sub(FacialExpression)] _FacialExpRightOffsetY ("FacialExpRightOffsetY", Float) = -0.05
        [Sub(FacialExpression)] _FacialExpRightRotateAngle ("FacialExpRightRotateAngle", Float) = 0
        [Sub(FacialExpression)] _FacialExpRightRotateSpeed ("FacialExpRightRotateSpeed", Float) = 0
        [SubToggle(FacialExpression)] _FacialExpMirror ("FacialExpMirror", Float) = 1
        [SubToggle(FacialExpression)] _FacialExpLeftHoriFlip ("FacialExpLeftHoriFlip", Float) = 0
        [Sub(FacialExpression)] _FacialExpLeftScale ("FacialExpLeftScale", Float) = 5
        [Sub(FacialExpression)] _FacialExpLeftOffsetX ("FacialExpLeftOffsetX", Float) = -0.13
        [Sub(FacialExpression)] _FacialExpLeftOffsetY ("FacialExpLeftOffsetY", Float) = -0.05
        [Sub(FacialExpression)] _FacialExpLeftRotateAngle ("FacialExpLeftRotateAngle）", Float) = 0
        [Sub(FacialExpression)] _FacialExpLeftRotateSpeed ("FacialExpLeftRotateSpeed）", Float) = 0
        [Sub(FacialExpression)] _FacialExpShadowThreshold ("FacialExpShadowThreshold", Range(-1, 1)) = 0
        [Sub(FacialExpression)] _FacialExpShadowSoftness ("FacialExpShadowSoftness", Range(0, 2)) = 0.5
        [Sub(FacialExpression)] _FacialExpShadowStrength ("FacialExpShadowStrength", Range(0, 1)) = 0.5
        [Main(HairMap, HAIR_MAP_ON)] _UseHairMap ("HairMap", Float) = 0
        [SubToggle(HairMap)] _UseBumpAsAOMask ("Use Bump.r as AO", Float) = 0
        [Sub(HairMap)] _AOShadowWarpScale ("AO ShadowWarpScale", Range(0, 1)) = 1
        [SubEnum(HairMap, All, 0, Mat1, 1, Mat2, 2, Mat3, 3, Mat4, 4, Mat5, 5)] _SelectMatID ("Select MatID", Float) = 0
        [Main(BackFace, BACK_FACE_ON)] _DrawBackFace ("BackFace", Float) = 0
        [SubToggle(BackFace)] _UseBackFaceUV2 ("Use BackFace UV 2", Float) = 1
        [SubToggle(BackFace)] _BackFaceAlphaClipWithUV1 ("BackFace Alpha Clip With UV 1", Float) = 0
        [SubToggle(BackFace)] _BackFaceLighting ("BackFace Lighting", Float) = 0
        [Main(MainTextureTint, MAIN_TEX_COLORING_ON)] _MainTexColoring ("MainTextureTint", Float) = 0
        [Sub(MainTextureTint)]  _MainTexTintColor ("Main Tex Tint Color", Color) = (1,1,1,1)
        [Main(HitColor, _, off, off)] _DummyHitColor ("HitColor", Float) = 0
        [Sub(HitColor)] _HitColor ("HitColor", Color) = (0,0,0,0)
        [Sub(HitColor)] _ElementRimColor ("Element Rim Color", Color) = (0,0,0,0)
        [Sub(HitColor)] _HitColorScaler ("HitColor Scaler", Float) = 6
        [Sub(HitColor)] _HitColorFresnelPower ("HitColor Fresnel Power", Float) = 1.5
        [Sub(HitColor)] _EmissionStrengthLerp ("Emission Strength Lerp", Range(0, 1)) = 0
        [Main(UseDither, _, off, off)] _DummyUseDither ("UseDither", Float) = 0
        [SubToggle(UseDither)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(UseDither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Main(UseFakePoint, ENABLE_FAKEPOINT_ON)] _UseFakePoint ("UseFakePoint", Float) = 0
        [SubToggle(UseFakePoint)] _EnableFakePoint ("Enable FakePoint", Float) = 0
        [Tex(UseFakePoint)] _FakePointNoiseTex ("Fake PointNoiseTex", 2D) = "white" { }
        [Sub(UseFakePoint)] _FakePointColor ("Fake PointColor", Color) = (1,1,1,1)
        [Sub(UseFakePoint)] _FakePointRange ("Fake PointRange", Float) = 1
        [Sub(UseFakePoint)] _FakePointIntensity ("Fake PointIntensity", Float) = 1
        [Sub(UseFakePoint)] _FakePointPosition ("Fake PointPosition", Vector) = (0,0,0,0)
        [Sub(UseFakePoint)] _FakePointReflection ("Fake PointReflection", Float) = 1
        [Sub(UseFakePoint)] _FakePointFrequency ("Fake PointFrequency", Float) = 0
        [Sub(UseFakePoint)] _FakePointFrequencyMin ("Fake PointFrequencyMin", Float) = 0
        [Sub(UseFakePoint)] _FakePointSkinIntensity ("Fake PointSkinIntensity", Float) = 1
        [Sub(UseFakePoint)] _FakePointSkinSaturate ("Fake PointSkinSaturate", Float) = 0
        [Main(TexturePerfomance, ENABLE_PERFORMANCE_ON)] _EnablePerformance ("Enable Performance", Float) = 0
        // [SubToggle(TexturePerfomance, ENABLE_PACK_LIGHT_ON)] _PackageLightMapToggle ("Package LightMap Toggle", Float) = 0
        // [SubEnum(TexturePerfomance, Layer1, 0, Layer2, 1, Layer3, 2)] _LightMapLayerEnum ("LightMap Layer Enum", Float) = 0
        // [Tex(TexturePerfomance)] [NoScaleOffset] _PackageLightMap ("Package LightMap", 2D) = "white" { }
        // [Tex(TexturePerfomance)] [NoScaleOffset] _LightMapBlueNoise ("Blue Noise", 2D) = "white" { }
        // [Sub(TexturePerfomance)] _LightMapBlurSize ("Blur Size", Range(0, 10)) = 1
        [SubToggle(TexturePerfomance, ENABLE_PACK_NORMAL_ON)] _PackageNormalMapToggle ("Package Normal Map Toggle", Float) = 0
        [SubEnum(TexturePerfomance, Layer1, 0, Layer2, 1, Layer3, 2)] _NormalMapLayerEnum ("Normal Map Layer Enum", Float) = 0
        [Tex(TexturePerfomance)] [NoScaleOffset] _NormalPackageMap ("Package Normal Map", 2D) = "white" { }
        [SubToggle(TexturePerfomance)] _isNativeMainNormal ("Native Main Normal Toggle", Float) = 0
        [Sub(TexturePerfomance)] _NormalMapOffset ("Normal Map Offset", Float) = 0
        [Sub(TexturePerfomance)] _NormalMapScale ("Normal Map Scale", Float) = 0
        // [Tex(TexturePerfomance)] [NoScaleOffset] _TextureLinePackageMap ("TextureLine Package Map", 2D) = "black" { }
        [Main(FixedForNormal)] _DummyFixedForNormal ("Fixed Normal", Float) = 0
        // [HideInInspector] _Dx11ShaderVsScore ("Dx11 Shader Vs Score", Float) = 0
        // [HideInInspector] _Dx11ShaderPsScore ("Dx11 Shader Ps Score", Float) = 0
        [Main(FaceDecal, _, off, on)] _EnableCharacterFaceDecal ("FaceDecal", Float) = 0
        [SubEnum(FaceDecal, Copy, 0, Mul, 1)] _CharacterFaceDecalBlendMode ("Blend Mode", Float) = 0
        [Sub(FaceDecal)] [HDR] _CharacterFaceDecalColor ("Color", Color) = (1,1,1,1)
        [Sub(FaceDecal)] _CharacterFaceDecalOpacity ("Opacity", Range(0, 1)) = 1
        [Tex(FaceDecal)] [NoScaleOffset] _CharacterFaceDecalMask ("Mask", 2D) = "white" { }
        [HideInInspector] _CharacterFaceDecalMask_SamplerType ("", Float) = 0
        [SubEnum(FaceDecal,Local,0,1U,1,2U,2)] _CharacterFaceDecalMaskUVSwitch ("Mask UV Switch", Float) = 0
        [Sub(FaceDecal)] _CharacterFaceDecalMaskST ("Mask ST", Vector) = (1,1,0,0)
        [SubToggle(FaceDecal)] _CharacterFaceDecalMask_MirrorU ("Mask Mirror U", Float) = 0
        [SubToggle(FaceDecal)] _CharacterFaceDecalMask_MirrorV ("Mask Mirror V", Float) = 0
        [SubEnum(FaceDecal, R, 0, G, 1, B, 2, A, 3)] _CharacterFaceDecalMaskChannelSwitch ("Mask Channel Switch", Float) = 0
        [Sub(FaceDecal)] _CharacterFaceDecalMaskGradientOffset ("Mask Gradient Offset", Range(-1, 1)) = 0
        [Sub(FaceDecal)] _CharacterFaceDecalMaskGradientSoftness ("Mask Gradient Softness", Range(0.0001, 1)) = 1
        [SubToggle(FaceDecal)] _CharacterFaceDecalSkipByLightmapG ("Skip Eyelash(Lightmap G Grayscale 0)", Float) = 0
        [SubToggle(FaceDecal)] _CharacterFaceDecalSkipByLightmapA ("Skip Sclera(Lightmap A Grayscale 1)", Float) = 0
        [Header(Others)]
        _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        _PolygonOffsetUnit ("Polygon Offset Unit", Float) = 0
        _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        _OutlinePolygonOffsetUnit ("Outline Polygon Offset Unit", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2
        [Space(10)] [Header(Depth Mode)] [Enum(Off, 0, On, 1)] _Zwrite ("ZWrite Mode", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _Ztest ("Ztest Mode", Float) = 4
        [Header(Stencil)] [IntRange] _StencilRef ("Stencil Ref", Range(0, 255)) = 16
        [IntRange] _StencilWriteMask ("Stencil Write Mask", Range(0, 255)) = 255  
        [IntRange] _StencilReadMask ("Stencil Read Mask", Range(0, 255)) = 255
        [IntRange] _ColorMask ("Color Mask", Range(0, 15)) = 15
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOP ("Stencil Op", Float) = 2
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail Op", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFailOp ("Stencil ZFail Op", Float) = 0
        [Header(Blend Mode)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendMode ("Src Blend Mode", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendMode ("Dst Blend Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOP ("BlendOp Mode", Float) = 0
        _TransparentAlpha ("Transparent Alpha", Range(0, 1)) = 1

        [Main(ADVANCED, _, off, off)] _AdvancedGroup("Advanced", Float) = 0
        [Sub(ADVANCED)] _IsVRC ("Vrchat toggle", Float) = 0
        [HideInInspector] _IsYup ("_IsYUp", Float) = 0
    }
    SubShader
    {
        Tags
		{ 
		    "RenderType"="NonHair" 
			"Queue" = "Geometry" 
            "LightMode" = "ForwardBase"
		}
        HLSLINCLUDE 
        #define is_facialuv
        #define is_facedecal
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/GenshinImpact-header.hlsl"
        #include "includes/GenshinImpact-base_input.hlsl"

        #include "includes/GenshinImpact-common.hlsl"
        #include "includes/GenshinImpact-base_program.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"    
            Cull [_CullMode]
            Blend [_SrcBlendMode] [_DstBlendMode] 
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnit]
            ColorMask [_ColorMask]
            ZWrite [_Zwrite]
            ZTest [_Ztest]

            Stencil
            {
                Ref [_StencilRef]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
                Comp [_StencilComp]
                Pass [_StencilOP]
            }
            HLSLPROGRAM
            #pragma shader_feature_local MATERIAL_MASK
            #pragma shader_feature_local TOON_LIGHTMAP_ON 
            #pragma shader_feature_local HAIR_MAP_ON
            #pragma shader_feature_local BUMP_TEXTURELINE_MAP 
            #pragma shader_feature_local SHADOW_RAMP_ON
            #pragma shader_feature_local BACK_FACE_ON
            #pragma shader_feature_local ENABLE_OUTLINE_ON
            // #pragma shader_feature_local ENABLE_TEXTURE_LINE_ON
            #pragma shader_feature_local FACE_MAP_NEW_ON 
            #pragma shader_feature_local MAIN_TEX_COLORING_ON
            #pragma shader_feature_local ENABLE_PERFORMANCE_ON 
            #pragma shader_feature_local ENABLE_PACK_LIGHT_ON 
            #pragma shader_feature_local ENABLE_PACK_NORMAL_ON
            #pragma shader_feature_local METAL_MAT

            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE
			#pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
			#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
			#pragma skip_variants _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fwdbase
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain_base
            #pragma fragment base_pixel


            ENDHLSL
        }

    UsePass "Hidden/HoyoToon/GenshinImpact/DepthCaster/DepthCaster"
    }
    CustomEditor "LWGUI.LWGUI" 
}
