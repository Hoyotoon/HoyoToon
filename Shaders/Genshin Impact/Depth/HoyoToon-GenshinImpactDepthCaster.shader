Shader "Hidden/HoyoToon/GenshinImpact/DepthCaster"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TessMask ("Tessellation Mask", Range(0, 20)) = 0
        _TessValue ("Tessellation Density", Range(1, 32)) = 1
        _PhongWeight ("Phong Weight", Range(0, 3)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("CullMode", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            Name "DepthCaster"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_CullMode]
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "UnityShaderVariables.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "UnityInstancing.cginc"
            
            Texture2D _MainTex;
            SamplerState sampler_linear_clamp;
            SamplerState sampler_linear_repeat;
             
            float _TessMask;
            float _TessValue;
            float _PhongWeight;
            
            float _MainTexAlphaUse ;
            float _MainTexAlphaCutoff;
            
            #if defined(is_weapon)
                Texture2D _WeaponDissolveTex;
                float _WeaponDissolveValue;
                float _DissolveDirection_Toggle;
            #endif

            
            #include "Includes/depth_input.hlsl"
            #include "Includes/depth_common.hlsl"
            #include "Includes/depth_program.hlsl"
            
            
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment base_pixel
            ENDHLSL
        }
    }
}