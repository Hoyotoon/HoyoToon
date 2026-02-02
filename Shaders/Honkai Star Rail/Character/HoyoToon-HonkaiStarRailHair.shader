Shader "HoyoToon/Honkai Star Rail/Character/Hair"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, TextureGroup, _, off, off)] _TexturesGroup ("Textures & Settings", Float) = 0
        [Tex(TextureGroup)] _MainTex ("Albedo |RGB(base color) A (alpha)", 2D) = "white" { }
        [Tex(TextureGroup)] _LightMap ("Light Map |R (sepcular intensity) G (diffuse threshold) B (specular threshold) A (material id)", 2D) = "grey" { }
        [Sub(TextureGroup)] _MainMaps_ST ("Main Maps ST", Vector) = (1,1,0,0)
        [Sub(TextureGroup)] _HairBlendWeight ("Hair Blend Weight", Range(0, 2)) = 1
        [Sub(TextureGroup)] _HairBlendOffset ("Hair Blend Offset", Range(-1, 0.1)) = 0

        [SubGroup(MainGroup, ColorGroup, _, off, off)] _ColorGroup ("Color", Float) = 0
        [Sub(ColorGroup)]_Color ("Color", Color) = (1,1,1,1)
        [Sub(ColorGroup)]_BackColor ("Back Color", Color) = (1,1,1,1)
        
        [SubGroup(MainGroup, AlphaGroup, _, off, off)] _AlphaGroup ("Alpha", Float) = 0
        [SubToggle(AlphaGroup)] _EnableAlphaCutoff ("Enable Alpha Cutoff", Float) = 0
        [Sub(AlphaGroup)] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        
        [SubGroup(MainGroup, HideParts)] _HideParts("Hide Parts", Float) = 0
        [SubToggle(HideParts)] _HideCharaParts ("Toggle Hide Chara Parts", Float) = 0
        [Sub(HideParts)] _ShowPartID ("Show Part ID", Range(0, 256)) = 0

        [Sub(MainGroup)] [HideInInspector] _UVChannelFront ("UV Channel for Frontside", Float) = 0
        [Sub(MainGroup)] [HideInInspector] _UVChannelBack ("UV Channel for Backside", Float) = 1
        [Sub(MainGroup)] [HideInInspector] _CharaWorldSpaceOffset ("_CharaWorldSpaceOffset", Vector) = (0,0,0,0)
        
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

        [SubGroup(LightingGroup, ShadowGroup, _, off, off)] _ShadowGroup ("Shadow", Float) = 0
        [Tex(ShadowGroup)]_DiffuseCoolRampMultiTex ("Cool Shadow Multiple Ramp", 2D) = "white" { }
        [Tex(ShadowGroup)]_DiffuseRampMultiTex ("Diffuse Multiple Ramp", 2D) = "white" { }
        [Sub(ShadowGroup)] _ShadowRamp ("Shadow Ramp", Range(0.01, 1)) = 1
        [Sub(ShadowGroup)]_ShadowBoost ("Shadow Boost Enable", Float) = 0
        [Sub(ShadowGroup)]_ShadowBoostVal ("Shadow Boost Intensity", Range(0, 0.5)) = 0
        [SubGroup(LightingGroup, SpecularGroup, _, off, off)] _SpecularGroup ("Specular", Float) = 0
        [Sub(SpecularGroup)] _SpecularColor0 ("Specular Color (ID = 0)", Color) = (1,1,1,1)
        [Sub(SpecularGroup)] _SpecularShininess0 ("Specular Shininess", Range(0.1, 500)) = 10
        [Sub(SpecularGroup)] _SpecularRoughness0 ("Specular Roughness", Range(0, 1)) = 0
        [Sub(SpecularGroup)] _SpecularIntensity0 ("Specular Intensity", Range(0, 50)) = 1
        [Sub(SpecularGroup)] _SpecularShadowOffset ("Specular Shadow Offset", Range(0, 1)) = 0.78
        [Sub(SpecularGroup)] _SpecularShadowIntensity ("Specular Shadow Intensity", Range(0, 1)) = 0
        [Advanced(Scripted Values)][Sub(SpecularGroup)] _ES_SPColor ("Color", Color) = (1,1,1,1)
        [Advanced][Sub(SpecularGroup)] _ES_SPIntensity ("intensity", Float) = 1.0

        [SubGroup(LightingGroup, RimGroup, _, off, off)] _RimGroup ("Rim Light", Float) = 0
        [Sub(RimGroup)] _RimLightMode ("0:don't use lightmap.r, 1:use", Range(0, 1)) = 1
        [Sub(RimGroup)] _RimLight ("Rim Light", Range(0, 1)) = 0
        [Sub(RimGroup)] _RimWidth0 ("RimWidth 0 (ID = 0)", Float) = 1
        [Sub(RimGroup)] _RimColor0 ("RimColor 0 (ID = 0)", Color) = (1,1,1,1)
        [Sub(RimGroup)] _RimEdgeSoftness0 ("RimSoftness 0 (ID = 0)", Range(0.01, 0.9)) = 0.1
        [Sub(RimGroup)] _RimType0 ("RimType 0 (ID = 0)", Range(0, 1)) = 1
        [Sub(RimGroup)] _RimDark0 ("RimType 0 (ID = 0)", Range(0, 1)) = 0.5
        [Sub(RimGroup)] _Rimintensity ("Rim Intensity", Float) = 3
        [Sub(RimGroup)] _RimWidth ("RimWidth", Float) = 1
        [Sub(RimGroup)] _RimOffset ("Rim Offset", Vector) = (0,0,0,0)
        [Sub(RimGroup)] _RimEdge ("Rim Edge Base", Range(0.01, 0.02)) = 0.015
        [Sub(RimGroup)][HDR] _FresnelColor ("FresnelColor", Color) = (0,0,0,0)
        [Sub(RimGroup)]_FresnelBSI ("Fresnel BSI", Vector) = (1,1,1,0)
        [Sub(RimGroup)]_FresnelColorStrength ("FresnelColorStrength", Float) = 1
        [Advanced(Scripted Values)] [Sub(RimGroup)] _ES_RimLightWidth ("ES Rim Width", float) = 1.0
        [Advanced][Sub(RimGroup)] _ES_RimLightOffset ("Rim Offset", vector) = (0,0,0,0)
        [Advanced][Sub(RimGroup)] _ES_RimLightAddMode ("Rim Light Add Mode", Float) = 0.07
        [Advanced][Sub(RimGroup)] _ES_RimLightIntensity ("Rim Light Intensity", Float) = 1.0
        [Advanced][Sub(RimGroup)] _ES_RimLightColor ("Rim Light Color", Color) = (1,1,1,1)
        
        [SubGroup(LightingGroup, RimShadowGroup, _, off, off)] _RimShadowGroup ("Rim Shadow", Float) = 0
        [Sub(RimShadowGroup)] [HideInInspector] _RimShadowCt ("Rim Shadow Ct", Float) = 1
        [Sub(RimShadowGroup)] [HideInInspector] _RimShadowIntensity ("Rim Shadow Intensity", Float) = 1
        [Sub(RimShadowGroup)] _RimShadow ("Rim Shadow", Range(0, 1)) = 0
        [Sub(RimShadowGroup)] _RimShadowColor0 ("Rim Shadow Color 0 (ID = 0)", Color) = (1,1,1,1)
        [Sub(RimShadowGroup)] _RimShadowWidth0 ("Rim Shadow Width 0 (ID = 0)", Float) = 1
        [Sub(RimShadowGroup)] _RimShadowFeather0 ("Rim Shadow Feather 0 (ID = 0)", Range(0.01, 0.99)) = 0.01
        [Sub(RimShadowGroup)] _RimShadowOffset ("Rim Shadow Offset", Vector) = (0,0,0,0)
        [Advanced(Scripted Values)] [Sub(RimShadowGroup)] _ES_RimShadowIntensity ("ES Rim Shadow Width", float) = 1.0
        [Advanced][Sub(RimShadowGroup)] _ES_RimShadowColor ("Rim Shadow Offset", vector) = (0,0,0,0)
        
        [Main(OutlineGroup, _, off, off)] _OutlineGroup ("Outline", Float) = 0
        [Sub(OutlineGroup)] _Outline ("Outline", Range(0, 1)) = 0
        [Sub(OutlineGroup)] _OutlineColorTex ("Outline Color", 2D) = "white" { }
        [Sub(OutlineGroup)] _OutlineColor0 ("Outline Color 0 (ID = 0)", Color) = (0,0,0,1)
        [Sub(OutlineGroup)] _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        [Sub(OutlineGroup)] _OutlineScale ("Outline Scale", Range(0, 1)) = 0.005
        [Sub(OutlineGroup)] _OutlineBlendWeight ("Outline Blend Weight", Range(0, 4)) = 1
        [Sub(OutlineGroup)] _OutlineBlendOffset ("Outline Blend Offset", Range(-1, 0.1)) = 0
        [Sub(OutlineGroup)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        [Sub(OutlineGroup)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        [Sub(OutlineGroup)] _OutlineExtdMode ("Outline Extend Max Distance", Float) = 0
        [Sub(OutlineGroup)] _OutlineNormalFrom ("Outline Normal From", Float) = 0
        [Sub(OutlineGroup)] _OutlineEnhanceAmt ("Outline Enhance By Distance Amount", Range(0, 0.5)) = 0.5
        [Sub(OutlineGroup)] _OutlineOffset ("Outline Offset", Range(-1, 1)) = 0

        [Main(SpecialFX, _, off, off)] _SpecialFX ("Special FX", Float) = 0
        [SubGroup(SpecialFX,EmissionGroup, _, off, off)] _EmissionGroup ("Emission Settings", Float) = 0
        [Sub(EmissionGroup)] _EmissionThreshold ("Emission Threshold", Range(0, 1)) = 1
        [Sub(EmissionGroup)] _EmissionIntensity ("Emission Texture", Float) = 0
        [SubGroup(SpecialFX,DissolveGroup, _, off, off)] _dissolvegroup ("Dissolve", Float) = 0
        [SubToggle(DissolveGroup)] _DissoveON ("Enable Dissolve", Float) = 0
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
        [HideInInspector] _DissolveShadowOff ("Disable Disolve Shadow", Float) = 0        

        [SubGroup(SpecialFX, DitherGroup, _, off, off)] _dithergroup("Dither Control Group", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [SubToggle(DitherGroup)] _UsingDitherAlphaArt ("UsingDitherAlpha Art", Float) = 0
        [SubToggle(DitherGroup)] _DITHER_FADE_IN ("_DITHER_FADE_IN", Float) = 0
        [Sub(DitherGroup)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        
        [Main(RenderingSettings, _, off, off)] _RenderingSettings ("Rendering Settings", Float) = 0
        [Sub(RenderingSettings)] _PolygonOffsetFactor ("Polygon Offset Factor", Float) = 0
        [Sub(RenderingSettings)] _PolygonOffsetUnits ("Polygon Offset Units", Float) = 0
        [Sub(RenderingSettings)] _OutlinePolygonOffsetFactor ("Outline Polygon Offset Factor", Float) = 0
        [Sub(RenderingSettings)] _OutlinePolygonOffsetUnits ("Outline Polygon Offset Units", Float) = 0
        [Sub(RenderingSettings)] _StencilRefA ("Stencil Reference Value", Range(0, 255)) = 25
        [Sub(RenderingSettings)] _StencilRefB ("Stencil Reference Value", Range(0, 255)) = 27
        [Enum(UnityEngine.Rendering.CullMode)] [Sub(RenderingSettings)] _CullMode ("Cull Mode", Float) = 2
}
    SubShader
    {
        Tags
		{ 
		    "RenderType"="Hair" 
			"Queue" = "Geometry+5" 
			"PerformanceChecks" = "False" 
		}
         
        HLSLINCLUDE 
        #define is_hairshader
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "includes/HonkaiStarRail-header.hlsl"
        #include "includes/HonkaiStarRail-hair_input.hlsl"
        #include "includes/HonkaiStarRail-hair_declaration.hlsl"
        #include "includes/HonkaiStarRail-common.hlsl"
        ENDHLSL

        Pass
        {
            Name "Base Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull [_CullMode]
            Stencil
            {
                Ref [_StencilRefA]
                CompFront Greater
                PassFront Keep
            }
            HLSLPROGRAM
            #pragma multi_compile_fwdbase
            #pragma vertex base_vertex
            #pragma fragment base_pixel
            #include "includes/HonkaiStarRail-hair_program.hlsl"


            ENDHLSL
        }
        
        Pass
        {
            Name "HairEye Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Offset [_PolygonOffsetFactor], [_PolygonOffsetUnits]
            Cull Back
            Stencil
            {
                    Ref [_StencilRefB]
                    Comp Greater
                    Pass Keep
            }
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #define is_stencil
            #pragma vertex base_vertex
            #pragma fragment base_pixel
            #include "includes/HonkaiStarRail-hair_program.hlsl"


            ENDHLSL
        }

        Pass
        {
            Name "Outline Pass"
            Tags{ "LightMode" = "ForwardBase" }
            Offset [_OutlinePolygonOffsetFactor], [_OutlinePolygonOffsetUnits]
            Cull Front
            Stencil
            {
                Ref 255
                Comp Always
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex outline_vertex
            #pragma fragment outline_pixel
            // make fog work
            #pragma multi_compile_fog

            #include "includes/HonkaiStarRail-hair_program.hlsl"

            ENDHLSL
        }
        
        UsePass "HoyoToon/Honkai Star Rail/Character/Depth Caster/Shadow Pass"
    }
    CustomEditor "LWGUI.LWGUI" 
}
