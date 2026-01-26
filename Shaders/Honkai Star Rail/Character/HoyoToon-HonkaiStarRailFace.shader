Shader "HoyoToon/Honkai Star Rail/Character/Face"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, TextureGroup, _, off, off)] _TexturesGroup ("Textures & Settings", Float) = 0
        [Tex(TextureGroup)] _MainTex ("Albedo", 2D) = "white" { }
        [Tex(TextureGroup)] _FaceMap ("Face Map |R(specular field) A (distance field)", 2D) = "white" { }
        [Tex(TextureGroup)] _FaceExpression ("Face Expression Map |R (Cheek) G (Shy) B (Shadow)", 2D) = "black" { }
        [SubToggle(TextureGroup)] _UseUVChannel2 ("Use UV Channel2 For Facemap", Float) = 1
        [SubToggle(TextureGroup)] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [SubToggle(TextureGroup)] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [Sub(TextureGroup)] _MainMaps_ST ("Main Maps ST", Vector) = (1,1,0,0)
        [Sub(TextureGroup)] _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        [SubGroup(MainGroup, AlphaGroup, _, off, off)] _AlphaGroup ("Alpha", Float) = 0
        [Sub(AlphaGroup)] _Opaqueness ("Opaqueness", Range(0, 1)) = 1
        [Sub(AlphaGroup)] _HairBlendSilhouette ("Hair Blend Silhouette", Range(0, 1)) = 0.5
        [Sub(AlphaGroup)] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        
        [SubGroup(MainGroup, ColorGroup, _, off, off)] _ColorGroup ("Color", Float) = 0 
        [Sub(ColorGroup)] _Color ("Color", Color) = (1,1,1,1)
        [SubGroup(MainGroup, HideParts)] _HideParts("Hide Parts", Float) = 0
        [SubToggle(HideParts)] _HideCharaParts ("Toggle Hide Chara Parts", Float) = 0
        [Sub(HideParts)] _ShowPartID ("Show Part ID", Range(0, 256)) = 0
        [SubGroup(MainGroup, FaceDetailGroup, _, off, off)] _FaceDetailGroup ("Face Detail", Float) = 0
        [SubGroup(FaceDetailGroup, NoseGroup, _, off, off)] _NoseGroup ("Nose Detail", Float) = 0
        [Sub(NoseGroup)] _NoseLineColor ("Nose Line Color", Color) = (1,1,1,1)
        [Sub(NoseGroup)] _NoseLinePower ("Nose Line Power", Range(0, 8)) = 1
        [SubGroup(FaceDetailGroup, LipLineGroup, _, off, off)] _LipLineGroup ("Lip Line Detail", Float) = 0
        [Sub(LipLineGroup)] _LipLineFixScale ("Lip Line Fix Threshold", Range(0.01, 4)) = 1
        [Sub(LipLineGroup)] _LipLinefixColor ("Lip Line Fix Color", Color) = (0,0,0,0)
        [Sub(LipLineGroup)] _LipLineFixThrd ("Lip Line Fix Threshold", Range(0, 0.95)) = 0.3
        [Sub(LipLineGroup)] _LipLineFixStart ("Lip Line Fix Start Distance", Range(0.04, 0.95)) = 0.25
        [Sub(LipLineGroup)] _LipLineFixMax ("Lip Line Fix Maximum Distance", Range(1, 2)) = 1.5
        [Sub(LipLineGroup)] _LipLineFixSC ("_LipLineFixSC", Float) = 0
        [SubGroup(FaceDetailGroup, ExpressionGroup, _, off, off)] _ExpressionGroup ("Expression", Float) = 0
        [Sub(ExpressionGroup)]_ExCheekColor ("Expression Cheek Color", Color) = (1,1,1,1)
        [Sub(ExpressionGroup)] _ExMapThreshold ("Expression Map Threshold", Range(0, 1)) = 0.5
        [Sub(ExpressionGroup)] _ExSpecularIntensity ("Expression Specular Intensity", Range(0, 7)) = 0
        [Sub(ExpressionGroup)] _ExCheekIntensity ("Expression Cheek Intensity", Range(0, 1)) = 0
        [Sub(ExpressionGroup)] [Space(10)] _ExShyColor ("Expression Shy Color", Color) = (1,1,1,1)
        [Sub(ExpressionGroup)] _ExShyIntensity ("Expression Shy Intensity", Range(0, 1)) = 0
        [Sub(ExpressionGroup)] [Space(10)] _ExShadowColor ("Expression Shadow Color", Color) = (1,1,1,1)
        [Sub(ExpressionGroup)] _ExEyeColor ("Expression Eye Color", Color) = (1,1,1,1)
        [Sub(ExpressionGroup)] _ExShadowIntensity ("Expression Shadow Intensity", Range(0, 1)) = 0

        [SubGroup(FaceDetailGroup, SpecialEyeGroup, _, off, off)] _SpecialEyeGroup ("Special Eye", Float) = 0
        [SubToggle(SpecialEyeGroup)] _UseSpecialEye ("Use Special Eye", Float) = 0
        [Tex(SpecialEyeGroup)]_SpecialEyeShapeTexture ("Special Eye Shape Texture", 2D) = "black" { }
        [Sub(SpecialEyeGroup)]_EyeEffectProcs ("Eye Effect Process", Range(0, 1)) = 0
        [Sub(SpecialEyeGroup)]_EyeCenter ("Special Eye Vector", Vector) = (0,0,0,0)
        [Sub(SpecialEyeGroup)]_SpecialEyeIntensity ("Special Eye Intensity", Range(0, 5)) = 1
        [Sub(SpecialEyeGroup)]_EyeEffectColor ("Eye Effect Color", Color) = (1,1,1,1)
        [Sub(SpecialEyeGroup)]_EyeSPColor1 ("Special Eye Color1", Color) = (1,1,1,1)
        [Sub(SpecialEyeGroup)]_EyeSPColor2 ("Special Eye Color2", Color) = (1,1,1,1)
        
        [Main(LightingGroup, _, off, off)] _LightingGroup ("Lighting", Float) = 0
        
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

        
        [SubGroup(LightingGroup, SpecularGroup, _, off, off)] _SpecularGroup ("Specular", Float) = 0
        [Sub(SpecularGroup)] _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        [Sub(SpecularGroup)] _SpecularPow ("Specular Pow", Float) = 1
        [Sub(SpecularGroup)] _SpecularIntensity ("Specular Intensity", Float) = 1
        [Sub(SpecularGroup)] _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 1

        [SubGroup(LightingGroup, ShadowGroup, _, off, off)] _ShadowGroup ("Shadow", Float) = 0
        [Sub(ShadowGroup)] _ShadowColor ("Dark Color", Color) = (0.5,0.5,0.5,1)
        [Sub(ShadowGroup)] _DarkColor ("Shadow Color", Color) = (0.85,0.85,0.85,1)
        [Sub(ShadowGroup)] _EyeShadowColor ("Eye Shadow Color", Color) = (1,1,1,1)
        [Sub(ShadowGroup)] _EyeBaseShadowColor ("EyeBase Shadow Color", Color) = (1,1,1,1)
        [Sub(ShadowGroup)] _EyeShadowAngleMin ("EyeBase Shadow Min Angle", Range(0.36, 1.36)) = 0.85
        [Sub(ShadowGroup)] _EyeShadowMaxAngle ("EyeBase Shadow Max Angle", Range(0, 1)) = 1
        [Sub(ShadowGroup)] _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        [Sub(ShadowGroup)] _ShadowFeather ("Shadow Feather", Range(0.0001, 0.5)) = 0.0001
        [SubToggle(ShadowGroup)] _ShadowBoost ("Shadow Boost Enable", Float) = 0
        [Sub(ShadowGroup)] _ShadowBoostVal ("Shadow Boost IOntensity", Range(0, 0.5)) = 0
        [Sub(ShadowGroup)] _BackShadowRange ("Back Shadow Range", Range(0, 1)) = 0
        [Advanced(Story Lighting)][SubToggle(ShadowGroup)] _ES_LEVEL_ADJUST_ON ("Enable Level Adjust", Float) = 0
        [Advanced][Sub(ShadowGroup)]_ES_LevelSkinLightColor ("Skin Shadow Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelSkinShadowColor ("Skin Light Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelHighLightColor ("Base Shadow Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelShadowColor ("Base Light Color", Color) = (1, 1, 1, 0.5)
        [Advanced][Sub(ShadowGroup)]_ES_LevelShadow ("Shadow Level", Range(0, 1)) = 0.0
        [Advanced][Sub(ShadowGroup)]_ES_LevelMid ("Mid Level", Range(0, 1)) = 0.55
        [Advanced][Sub(ShadowGroup)]_ES_LevelHighLight ("High Light Level", Range(0, 1)) = 1.0

        [SubGroup(LightingGroup, RimShadowGroup, _, off, off)] _RimShadowGroup ("Rim Shadow", Float) = 0        
        [Sub(RimShadowGroup)] _RimShadowColor ("Rim Shadow Color", Color) = (1,1,1,1)
        [Sub(RimShadowGroup)] _RimShadowWidth ("Rim Shadow Width", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowFeather ("Rim Shadow Feather", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowGroup)] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 0
        [Sub(RimShadowGroup)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)

        [Main(OutlineGroup, _, off, off)] _OutlineGroup ("Outline", Float) = 0
        [Sub(OutlineGroup)] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        [Sub(OutlineGroup)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [Sub(OutlineGroup)] _OutlineScale ("Outline Scale", Range(0, 1)) = 0.005
        [SubEnum(OutlineGroup, Normal, 0, Tangent, 1, UV2, 2)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        [Sub(OutlineGroup)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0
        [SubGroup(OutlineGroup, OutlineExtdGroup, _, off, off)] _outlineextdgroup ("Outline Extend", Float) = 0
        [Sub(OutlineExtdGroup)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        [Sub(OutlineExtdGroup)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        [Sub(OutlineExtdGroup)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        [SubGroup(OutlineGroup, OutlineFixRangeGroup, _, off, off)] _outlinefixrangegroup ("Lip Outline Fix Settings", Float) = 0
        [Sub(OutlineFixRangeGroup)] _OutlineFixRange1 ("Lip Outline Show Start", Range(0, 1)) = 0.1
        [Sub(OutlineFixRangeGroup)] _OutlineFixRange2 ("Lip Outline Show Max", Range(0, 1)) = 0.1
        [Sub(OutlineFixRangeGroup)] _OutlineFixRange3 ("Lip Outline Show Start", Range(0, 1)) = 0.1
        [Sub(OutlineFixRangeGroup)] _OutlineFixRange4 ("Lip Outline Show Max", Range(0, 1)) = 0.1
        [Sub(OutlineFixRangeGroup)] _OutlineFixSide ("Outline Fix Star Side", Range(0, 1)) = 0.6
        [Sub(OutlineFixRangeGroup)] _OutlineFixFront ("Outline Fix Star Front", Range(0, 1)) = 0.05
        [Sub(OutlineFixRangeGroup)] _FixLipOutline ("TurnOn Temp Lip Outline", Range(0, 1)) = 0

        [Main(SpecialFX, _, off, off)] _SpecialFX ("Special FX", Float) = 0

        [SubGroup(SpecialFX,EmissionGroup, _, off, off)] _EmissionGroup ("Emission Settings", Float) = 0
        [Sub(EmissionGroup)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        [Sub(EmissionGroup)] _EmissionIntensity ("Emission Texture", Float) = 0

        [SubGroup(SpecialFX, DissolveGroup, _, off, off)] _dissolvegroup ("Dissolve", Float) = 0
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

        [SubGroup(SpecialFX, DitherGroup, _, off, off)] _dithergroup("Dither Control Group", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [SubToggle(DitherGroup)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [Sub(DitherGroup)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Main(RenderingSettings, _, off, off)] _RenderingSettings ("Rendering Settings", Float) = 0

        [Sub(RenderingSettings)] _StencilRefA ("Stencil Reference Value", Range(0, 255)) = 2
        [Sub(RenderingSettings)] _StencilRefB ("Stencil Reference Value", Range(0, 255)) = 26
        
        [HideInInspector] _IsYup ("_IsYUp", Float) = 0
        
        

    }
    SubShader
    {
        Tags
		{ 
			"RenderType"="NonHair"
			"Queue" = "Geometry+1" 
			"PerformanceChecks" = "False" 
		}
        HLSLINCLUDE 
        #define is_faceshader
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/HonkaiStarRail-header.hlsl"
        #include "includes/HonkaiStarRail-face_input.hlsl"
        #include "includes/HonkaiStarRail-face_declaration.hlsl"
        #include "includes/HonkaiStarRail-common.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"
            Tags{ "LightMode" = "ForwardBase" }//  first alpha testing : 
            Stencil
            {
                Ref [_StencilRefA]
                Comp Always
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex base_vertex
            #pragma fragment base_pixel
            #include "includes/HonkaiStarRail-face_program.hlsl"

            ENDHLSL
        }

        Pass 
        {
            Name "Eye Mask"
            Tags{ "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha 
            Stencil
            {
                Ref [_StencilRefB]
                Comp Always
                Pass Replace
            }

            ColorMask 0

            HLSLPROGRAM
            #define is_eye_mask
            #pragma vertex base_vertex
            #pragma fragment base_pixel
            #include "includes/HonkaiStarRail-face_program.hlsl"

            ENDHLSL
        }

        Pass 
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Cull Front
            Offset 2, 1
            Stencil
            {
                Ref 255
                Comp Always
                Pass Keep
            }
            // Blend [_SrcBlend] [_DstBlend] 
            
            HLSLPROGRAM
            #pragma vertex outline_vertex
            #pragma fragment outline_pixel
            #include "includes/HonkaiStarRail-face_program.hlsl"

            ENDHLSL
        }

        


        UsePass "HoyoToon/Honkai Star Rail/Character/Depth Caster/Shadow Pass"
    }
    CustomEditor "LWGUI.LWGUI" 
}
