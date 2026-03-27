Shader "HoyoToon/Honkai Star Rail/Character/Effect/Aura Outline"
{
    Properties
    {
        [MaterialIDCount(8)] _MateirialIDCount ("Material ID Count", Float) = 8
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubGroup(MainGroup, Options)] _Options ("Options", Float) = 0
        [SubGroup(Options, Rendering)] _RenderMode ("Rendering", Float) = 0
        [Sub(Rendering)]_ZWrite ("ZWrite", Float) = 0
        [Sub(Rendering)] _StencilRef ("Stencil Ref", Float) = 1
        [SubEnum(Rendering, UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8
        [Sub(Rendering)] _StencilMask ("Stencil Read Mask", Float) = 255
        [SubToggle(Options)] _HideCharaParts ("Hide Chara Parts", Float) = 0
        [Sub(Options)] _ShowPartID ("Show Part ID", Float) = 0
        [Main(OutlineGroup, _, off, off)] _OutlineGroup ("Outline", Float) = 0
        [SubKeywordEnum(OutlineGroup, Normal, Tangent, UV2)] _OutlineNormalFrom ("Outline Normal From", Float) = 1
        [Sub(OutlineGroup)] _AuraScrPosScale ("Aura ScrPos Scale", Range(0, 1)) = 1
        [Sub(OutlineGroup)] _AuraScrPosXYOffset ("Aura ScrPos XY Offset", Float) = 0
        [Tex(OutlineGroup, small, collapsed)] _AuraNoise ("Outline Aura Noise Texture", 2D) = "white" { }
        [Sub(OutlineGroup)] _AuraWidth ("Outline Aura Width", Range(0, 5)) = 0.5
        [Sub(OutlineGroup)] _AuraOffset ("Outline Aura Offset", Range(0, 0.5)) = 0
        [Sub(OutlineGroup)] _AuraColor0 ("Outline Aura Color 1 ", Color) = (0,0,0,0)
        [Sub(OutlineGroup)] _AuraColor1 ("Outline Aura Color 2 A", Color) = (1,1,1,1)
        [Sub(OutlineGroup)] _AuraDisStep ("DisStep     Y=OutEdge   Z=intensity W=Noise", Vector) = (0.2,0.05,1,0.2)
        [Sub(OutlineGroup)] _AuraSmoothStep ("SmoothXY ", Vector) = (0,0,0,0)
        [Sub(OutlineGroup)] _DissolveMapAdd ("Dissolve Map Add", Float) = 0
        [Sub(OutlineGroup)] _IntensityAdd ("Intensity Add", Float) = 0
        [Sub(OutlineGroup)] _AuraSpeed ("AuraSpeed          NoiseSpeed", Vector) = (0,0.3,0,0.1)
        [Sub(OutlineGroup)] _Aurafresnel ("FresnelRGB=XY    FresnelA=ZW", Vector) = (0,1,0,1)
        [Sub(OutlineGroup)] _OutlineExtdStart ("Outline Extend Start Distance", Range(0, 128)) = 6.5
        [Sub(OutlineGroup)] _OutlineExtdMax ("Outline Extend Max Distance", Range(0, 128)) = 18
        
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, Dither)] _DitherEffect("Dither Effect", Float) = 0
        [SubKeyword(Dither, _USINGDITHERALPAH)] _UsingDitherAlpha ("Using Dither Alpha", Float) = 0
        [Sub(Dither)] _DitherAlpha ("Dither Alpha Value", Range(0, 1)) = 1
        [Sub(Dither)] _UsingDitherAlphaArt ("Dither Art Toggle", Float) = 0
        [Sub(Dither)] _DITHER_FADE_IN ("Enable Dither Fade In", Float) = 0
    
        [HideInInspector] _OneMinusCharacterOutlineWidthScale ("OneMinusCharacterOutlineWidthScale", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"            
        ENDHLSL

        Pass
        {
            Name "Aura Outline"
            Tags { "LightMode"="CustomRPTransparent" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _OUTLINENORMALFROM_NORMAL _OUTLINENORMALFROM_TANGENT _OUTLINENORMALFROM_UV2


            

            CBUFFER_START(CRP_PerView)
                float4x4 _unity_MatrixInvP;
                float4x4 _NonJitteredProjMatrix;
                float4 _SplitUVTrans;
            CBUFFER_END


            #ifndef UNITY_MATRIX_MV
                #define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
            #endif

            #ifndef UNITY_MATRIX_MVP
                #define UNITY_MATRIX_MVP mul(unity_MatrixVP, unity_ObjectToWorld)
            #endif

            #define unity_MatrixMV  UNITY_MATRIX_MV
            #define unity_MatrixMVP UNITY_MATRIX_MVP


            #define UNITY_MATRIX_M          unity_ObjectToWorld
            #define UNITY_MATRIX_I_M        unity_WorldToObject
            #define UNITY_PREV_MATRIX_M     unity_MatrixPreviousM
            #define UNITY_PREV_MATRIX_I_M   unity_MatrixPreviousMI
            #define UNITY_MATRIX_V          unity_MatrixV
            #define UNITY_MATRIX_I_V        unity_MatrixInvV
            #define UNITY_MATRIX_VP         unity_MatrixVP

            CBUFFER_START (CRP_PerDrawEx) 
                float4                _CharacterLocalMainLightPosition;
                float4                _CharacterLocalMainLightColor;
                float4                _CharacterLocalMainLightColor1;
                float4                _CharacterLocalMainLightColor2;
                float4                _CharacterLocalMainLightDark;
                float4                _CharacterLocalMainLightDark1;
                float4                _NewLocalLightDir;
                float4                _NewLocalLightCharCenter;
                float4                _NewLocalLightStrength;
                float                _DisableCharacterLocalLight;
                float                _EnableCustomCameraOverride;
                float4                _CharacterSelfShadowAtlasRect;
                float                _CharacterSelfShadowSliceIndex;
                float                _CharacterSelfShadowValid;
                float2               _PadCharacterSelfShadow;
            CBUFFER_END

            CBUFFER_START(UnityPerMaterial)
                int _HideCharaParts;
                int _ShowPartID;
                float _OutlineExtdStart;
                float _OutlineExtdMax;
                float _DissolveMapAdd;
                int _UsingDitherAlpha;
                float _DitherAlpha;
                int _HideNPCParts;
                float _AuraOffset;
                float _AuraScrPosScale;
                float _OneMinusCharacterOutlineWidthScale;
                float4 _AuraColor0;
                float4 _AuraColor1;
                float4 _AuraDisStep;
                float4 _AuraSmoothStep;
                float4 _AuraSpeed;
                float4 _Aurafresnel;
                float _AuraScrPosXYOffset;
                float4 _AuraNoise_ST;
                float _IntensityAdd;
                int _DITHER_FADE_IN;
            CBUFFER_END

            CBUFFER_START(RPGEnv_PerMainCamera)
                float _GlobalOneMinusAvatarIntensity;
                float3 _XPad0;
                float3 _ES_MonsterLightDir;
                float _ES_Indoor;
                float _ES_TransitionRate;
                float _ES_SelfShadowLerpHair;
                float _ES_LEVEL_ADJUST_ON;
                float _XPad1;
                float4 _ES_GlobalRotMatrix[4];
                float _ES_CharacterToonRampMode;
                float _ES_CharacterDisableLocalMainLight;
                float2 _XPad2;
                float4 _ES_AddColor;
                float4 _ES_SPColor;
                float _ES_SPIntensity;
                float3 _XPad3;
                float4 _ES_RimShadowColor;
                float _ES_RimShadowIntensity;
                float _ES_CharacterShadowFactor;
                float _ES_OutLineDarkenVal;
                float _ES_OutLineLightedVal;
                float _ES_OutlineDisableDistanceScale;
                float _ES_OutlineFallbackScale;
                float _ES_HeightLerpTop;
                float _ES_HeightLerpBottom;
                float4 _ES_HeightLerpTopColor;
                float4 _ES_HeightLerpMiddleColor;
                float4 _ES_HeightLerpBottomColor;
                float2 _ES_RimLightOffset;
                float _ES_RimLightWidth;
                float _ES_RimLightIntensity;
                float _ES_RimLightAddMode;
                float _ES_RimLightMode;
                float2 _XPad4;
                float4 _ES_RimLightColor;
                float4 _ES_LevelSkinLightColor;
                float4 _ES_LevelSkinShadowColor;
                float4 _ES_LevelHighLightColor;
                float4 _ES_LevelShadowColor;
                float _ES_LevelShadow;
                float _ES_LevelMid;
                float _ES_LevelHighLight;
                float _ES_LevelEyeShadowIntensity;
                float _ES_IndoorCharShadowAsCookie;
                float _ES_FogColor;
                float _ES_FogDensity;
                float _ES_FogNear;
                float _ES_FogFar;
                float _ES_HeightFogColor;
                float _ES_HeightFogBaseHeight;
                float _ES_HeightFogRange;
                float _ES_HeightFogDensity;
                float _ES_HeightFogFogNear;
                float _ES_HeightFogFogFar;
                float _ES_FogCharacterNearFactor;
                float _ES_HeightFogAddAjust;
                float _ES_DisableFogTransition;
                float2 _XPad5;
                float4 _ES_EffCustomLightPosition;
                float _OutlineScale;
            CBUFFER_END

            #if defined(_USE_DITHER)
                void dither(float4 screen_pos)
                {
                    float2 dither_screen_pos = floor((screen_pos.xy / screen_pos.w) * _ScaledScreenParams);
                        float4x4 identity;
                        identity[0] = float4(1.0,0.0,0.0,0.0);
                        identity[1] = float4(0.0,1.0,0.0,0.0);
                        identity[2] = float4(0.0,0.0,1.0,0.0);
                        identity[3] = float4(0.0,0.0,0.0,1.0);

                        float4x4 bayer;
                        bayer[0] = float4(1.0, 13.0, 4.0, 16.0);
                        bayer[1] = float4(9.0, 5.0, 12.0, 8.0);
                        bayer[2] = float4(3.0, 15.0, 2.0, 14.0);
                        bayer[3] = float4(11.0, 7.0, 10.0, 6.0);
                        
                        if( _UsingDitherAlpha)
                        {
                            // if(out_coord < 0.94f)
                            // {
                                uint2 screen = (uint2)dither_screen_pos & (uint2)3;
                                
                                float4 lookup;
                                lookup.x = dot(bayer[0], identity[screen.y]);
                                lookup.y = dot(bayer[1], identity[screen.y]);
                                lookup.z = dot(bayer[2], identity[screen.y]);
                                lookup.w = dot(bayer[3], identity[screen.y]);

                                float dither_value = dot(lookup, identity[screen.x]);

                                dither_value = _DITHER_FADE_IN != 0 ? 17-dither_value : dither_value;
                                dither_value =  _DitherAlpha  * 17.0 - dither_value;
                                dither_value = dither_value + 0.99f;
                                dither_value = max(floor(dither_value), 0.0);
                                if((int)dither_value == 0) discard;            
                            // }
                        }
                }
            #endif

            float _AuraWidth;


            TEXTURE2D(_AuraNoise);
            SAMPLER(sampler_AuraNoise);

            struct HSRComputeSkinnedVertex
            {
                float3 pos;
                float _Pad0;
                float3 norm;
                float _Pad1;
                float4 tangent;
                float4 tangent1;
            };

            StructuredBuffer<HSRComputeSkinnedVertex> _HSRComputeSkinnedVertices;
            int _HSRComputeSkinningEnabled;
            int _HSRComputeSkinningVertexOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv7     : TEXCOORD6;
                float2 uv8     : TEXCOORD7;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            inline void ApplyComputeSkinningVertex(inout appdata v, uint vertexID)
            {
                if (_HSRComputeSkinningEnabled == 0)
                    return;

                uint globalVertexIndex = vertexID + (uint)max(_HSRComputeSkinningVertexOffset, 0);
                HSRComputeSkinnedVertex skinnedVertex = _HSRComputeSkinnedVertices[globalVertexIndex];
                v.vertex = float4(skinnedVertex.pos, 1.0);
                v.normal = skinnedVertex.norm;
                v.tangent = skinnedVertex.tangent;
                v.uv7 = skinnedVertex.tangent1.xy;
                v.uv8 = skinnedVertex.tangent1.zw;
            }

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float4 ws_pos : TEXCOORD1;
                float4 ss_pos : TEXCOORD2;
                float4 view_uv : TEXCOORD3;
                float3 view_dir : TEXCOORD4;
            };

            struct aura_out
            {
                float4 color : SV_Target0;
                float4 alphaMask : SV_Target1;
            };

            v2f vert(appdata v, uint vertexID : SV_VertexID)
            {
                ApplyComputeSkinningVertex(v, vertexID);
                v2f o;
                o.uv.xy = v.uv.xy;
                o.uv.zw = v.uv.xy * _AuraNoise_ST.xy + _AuraNoise_ST.zw;
                float4x4 InvMV = mul(UNITY_MATRIX_I_V, UNITY_MATRIX_M);
                float3 outline_norm = 0.f;
                #if defined(_OUTLINENORMALFROM_TANGENT)
                    outline_norm = v.tangent; 
                #elif defined(_OUTLINENORMALFROM_NORMAL)
                    outline_norm = v.normal;
                #elif defined(_OUTLINENORMALFROM_UV2)
                    outline_norm = float3(v.uv1.xy, 1.f);
                #endif
                outline_norm = mul((float3x3)InvMV, outline_norm.xyz);
                outline_norm = normalize(outline_norm);

                float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                float4 custom_position = mul(unity_MatrixV, ws_pos);

                float4 view = mul(UNITY_MATRIX_M, v.vertex) - float4(_WorldSpaceCameraPos, 0);
                float view_len = length(view);
                float extend_factor = smoothstep(_OutlineExtdStart, _OutlineExtdMax, view_len);
                extend_factor = min(extend_factor, 0.5) + 1.0f;
                extend_factor = ((extend_factor * v.color.w) * _AuraWidth) * _OutlineScale;
                custom_position.xyz = extend_factor *  outline_norm + custom_position.xyz;

                float offset = _AuraOffset / max(normalize(custom_position.xyz).z, 0.001f);

                custom_position.xyz = custom_position.xyz * offset + custom_position.xyz;
                float4 position = mul(UNITY_MATRIX_P, custom_position);
                int2 hidepart = int2(v.color.yx * 256);
                hidepart.x = int(uint(hidepart.x) & uint(_ShowPartID));
                hidepart.y = int(uint(hidepart.y) & uint(_ShowPartID));
                int tmp = _HideNPCParts ? hidepart.y : 1;
                tmp = _HideCharaParts ? hidepart.x : tmp;
                o.vertex = (tmp > 0) ? position : float4(-99.0, -99.0, -99.0, 1.0);
                
                float4 ss_tmp;
                ss_tmp.w = (position.y * _ProjectionParams.x) * 0.5f;
                ss_tmp.xz = position.xw * 0.5f;
                o.ss_pos.zw = position.zw;
                o.ss_pos.xy = ss_tmp.zz + ss_tmp.xw;
                o.view_uv.xy = o.ss_pos.xy;
                o.view_uv.w = position.w;
                float3 view_dir = _WorldSpaceCameraPos.xyz - unity_ObjectToWorld[3].xyz;
                o.view_dir = view;
                o.view_uv.z = sqrt(dot(view_dir, view_dir)) * _AuraScrPosScale + (1 - _AuraScrPosScale);

                o.normal = v.normal;
                o.color = v.color;
                return o;
            }

            aura_out frag(v2f i)
            {

                float2 screen = i.view_uv.xy / i.view_uv.w;
                
                #if defined(_USE_DITHER)
                    dither(i.view_uv);
                #endif

                screen = (-i.ss_pos.xy) * _AuraScrPosXYOffset + screen;
                screen = screen * i.view_dir.zzzz;
                // Sample noise texture at screen-space UV
                float2 noise_uv = screen * _AuraNoise_ST.xy + _AuraNoise_ST.zw;
                float2 animated_uv = noise_uv - _Time.y * _AuraSpeed.xy;
                float2 noise_sample1 = SAMPLE_TEXTURE2D(_AuraNoise, sampler_AuraNoise, animated_uv).xy;

                // Apply noise offset for second sample
                float2 noise_uv2 = animated_uv - noise_sample1 * _AuraDisStep.w;
                float noise_sample2_z = SAMPLE_TEXTURE2D(_AuraNoise, sampler_AuraNoise, noise_uv2).z;

                // Calculate Fresnel effect
                float fresnel = dot(i.normal, normalize(i.view_dir));
                fresnel = clamp(fresnel, 0.0, 1.0);
                float2 fresnel_range = clamp((fresnel - _Aurafresnel.xz) / _Aurafresnel.yw, 0.0, 1.0);

                // Calculate dissolve step
                float dissolve_value = noise_sample2_z + _DissolveMapAdd;
                float edge_dist = fresnel * dissolve_value - _AuraDisStep.x;
                float edge_softness = clamp(edge_dist / _AuraSmoothStep.x, 0.0, 1.0);
                float outer_edge = clamp((fresnel * dissolve_value - _AuraDisStep.y) / _AuraSmoothStep.y, 0.0, 1.0);

                // Blend between two aura colors
                float intensity = noise_sample2_z + _IntensityAdd;
                float3 color = lerp(_AuraColor0.xyz, _AuraColor1.xyz, outer_edge) * intensity * _AuraDisStep.z;

                // Output final color
                aura_out output;
                output.color.xyz = fresnel_range.x * color;
                output.color.w = fresnel_range.y * edge_softness * _AuraColor1.w;
                output.alphaMask = output.color.w;
                return output;
            }
            ENDHLSL
        }
    }
     CustomEditor "LWGUI.LWGUI" 
}