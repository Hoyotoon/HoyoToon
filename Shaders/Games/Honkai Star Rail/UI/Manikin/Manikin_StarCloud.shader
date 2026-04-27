Shader "HoyoToon/Honkai Star Rail/UI/Manikin/StarCloud"
{
    Properties
    {
        [Main(Nebular, _, off, off)] _NebulaDummy ("Nebula", float) = 0
        [Sub(Nebular)]_Cloud01Color ("Cloud01 Color", Color) = (0,1,0.7103448,0)
        [Tex(Nebular, small, collapsed)] _Cloud01Tex ("Cloud01Tex", 2D) = "white" { }
        [Sub(Nebular)]_CloudTex01UV1Coord ("Cloud01 UV1 Coord", Vector) = (1,1,0,0)
        [Sub(Nebular)]_CloudTex01UV2Coord ("Cloud01 UV2 Coord", Vector) = (1,1,0,0)
        [Sub(Nebular)]_CloudOffset ("Cloud01Offset", Float) = 0
        [Sub(Nebular)]_CloudMultiplyer ("Cloud01Multiplyer", Float) = 2.5
        [Space(10)] [Sub(Nebular)]_Cloud02Color ("Cloud02 Color", Color) = (1,0.3676471,0.3676471,0)
        [Tex(Nebular, small, collapsed)] _Cloud02Tex ("Cloud02Tex", 2D) = "white" { }
        [Sub(Nebular)]_CloudTex02UV1Coord ("Cloud02 UV1 Coord", Vector) = (1,1,0,0)
        [Sub(Nebular)]_CloudTex02UV2Coord ("Cloud02 UV2 Coord", Vector) = (1,1,0,0)
        [Sub(Nebular)]_Cloud02Offset ("Cloud02Offset", Float) = -0.09
        [Sub(Nebular)]_Cloud02Multipler ("Cloud02Multipler", Float) = 1
        [Main(CloudsBlend, _, off, off)] _CloudsBlendDummy ("Clouds Blend", float) = 0
        [Sub(CloudsBlend)] _AllCloudsAlpha ("All Clouds Alpha", Range(0, 1)) = 1
        [Sub(CloudsBlend)] _GradientRange ("GradientRange(Blend uv1 and uv2)", Float) = 8
        [Sub(CloudsBlend)] _GradientOffset ("GradientOffset(Blend uv1 and uv2)", Float) = -6
        [Main(Flow, _, off, off)] _FlowDummy ("Flow", float) = 0
        [Tex(Flow, small, collapsed)] _FlowTex ("FlowTex", 2D) = "gray" { }
        [Sub(Flow)] _FlowSpeed ("Flow Speed", Float) = 0.2
        [Sub(Flow)] _FlowStrength ("Flow Strength", Float) = 0.1
        [Main(PerlinNoise, _, off, off)] _PerlinNoiseDummy ("Perlin Noise", float) = 0 
        [Sub(PerlinNoise)] _PerlinNoiseScale ("PerlinNoiseScale", Float) = 0.01
        [Sub(PerlinNoise)] _PerlinNoiseOffset ("PerlinNoiseOffset", Float) = 0
        [Sub(PerlinNoise)] _PerlinNoiseMultiply ("PerlinNoiseMultiply", Float) = 1
        [Vector3(PerlinNoise)] _PerlinNoisePosOffset ("PerlinNoisePosOffset", Vector) = (0,0,0,0)
        [Main(Tint, _, off, off)] _TintDummy ("Tint", float) = 0 
        [Tex(Tint, small, collapsed)] _TintColorTex ("TintColorTex", 2D) = "white" { }
        [Sub(Tint)] _TintColorTexUV1Coord ("TintColorTexUV1Coord", Vector) = (1,1,0,0)
        [Sub(Tint)] _TintColorTexUV2Coord ("TintColorTexUV2Coord", Vector) = (1,1,0,0)
        [Sub(Tint)] _TintColorTexScale ("TintColorTexScale", Range(0, 1)) = 1
        [Main(Stars, _, off, off)] _StarsDummy ("Stars", float) = 0
        [Sub(Stars)] _StarDepth ("StarDepth", Float) = 14.89
        [Sub(Stars)] _StarBrightness1 ("StarBrightness", Float) = 10
        [Sub(Stars)] _StarBrightness2 ("StarBrightness2", Float) = 10
        [Tex(Stars, small, collapsed)] _StarTex ("StarTex", 2D) = "black" { }
        [Sub(Stars)] _StarTexUV1Coord ("StarTexUV1Coord", Vector) = (1,1,0,0)
        [Sub(Stars)] _StarTexUV2Coord ("StarTexUV2Coord", Vector) = (1,1,0,0)
        [Sub(Stars)] _StarNoiseTiling ("StarNoiseTiling", Vector) = (2,2,0,0)
        [Sub(Stars)] _StarScintillationSpeed ("StarScintillation", Float) = 0.17
        [Tex(Stars, small, collapsed)] _ColorPalette ("ColorPalette", 2D) = "white" { }
        [Sub(Stars)] _Desaturate ("Desaturate", Range(0, 1)) = 0
        [Sub(Stars)] _ColorPalletteSpeed ("ColorPalletteSpeed", Float) = -1.95
        [Sub(Stars)] _FadeAlpha ("FadeAlpha", Range(0, 1)) = 1
        [Sub(Stars)] _Power ("Power", Float) = 10
        [Sub(Stars)] _Scale ("Scale", Float) = 33.58
        [Main(Rendering, _, off, off)] _RenderingDummy ("Rendering", float) = 0 
        [SubEnum(Rendering,UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [SubEnum(Rendering, Off, 0, On, 1)] _Zwrite ("ZWrite Mode", Float) = 1
        [SubEnum(Rendering, UnityEngine.Rendering.CompareFunction)] _Ztest ("ZTest Mode", Float) = 4
        [SubEnum(Rendering, UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend Mode", Float) = 1
        [SubEnum(Rendering, UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend Mode", Float) = 1  
        [HideInInspector] _OneMinusGlobalMainIntensityEnable ("OneMinusGlobalMainIntensityEnable", Float) = 1
        [HideInInspector] _OneMinusGlobalMainIntensity ("OneMinusGlobalMainIntensity", Float) = 1
        [HideInInspector] _GlobalOneMinusAvatarIntensityEnable ("GlobalOneMinusAvatarIntensityEnable", Float) = 1  
    }
    SubShader
    {
        Name "CustomForward"
        Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry-40" "RenderType" = "Opaque" }
        Offset 20, 20
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        ENDHLSL

        Pass
        {
            Name "CustomForward"
            Tags { "LIGHTMODE" = "CustomForwardOpaque2" "QUEUE" = "Geometry-40" "RenderType" = "Opaque" }
            ZWrite [_Zwrite]
            HLSLPROGRAM
                #include "Common/Declarations.hlsl"
                #pragma vertex vert
                #pragma fragment frag

                float _OneMinusGlobalMainIntensityEnable;
                float _OneMinusGlobalMainIntensity;
                float _GlobalOneMinusAvatarIntensityEnable;
                float4 _Cloud01Color;
                float4 _CloudTex01UV1Coord;
                float4 _CloudTex01UV2Coord;
                float _GradientRange;
                float _GradientOffset;
                float _CloudOffset;
                float _CloudMultiplyer;
                float4 _Cloud02Color;
                float4 _CloudTex02UV1Coord;
                float4 _CloudTex02UV2Coord;
                float _Cloud02Offset;
                float _Cloud02Multipler;
                float4 _ColorPalette_ST;
                float _ColorPalletteSpeed;
                float _AllCloudsAlpha;
                float _PerlinNoiseScale;
                float _PerlinNoiseOffset;
                float _PerlinNoiseMultiply;
                float3 _PerlinNoisePosOffset;
                float _Desaturate;
                float _NoiseSpeed;
                float4 _TintColorTexUV2Coord;
                float4 _TintColorTexUV1Coord;
                float _TintColorTexScale;
                float4 _StarTexUV1Coord;
                float _StarDepth;
                float4 _StarTexUV2Coord;
                float4 _StarNoiseTiling;
                float _StarScintillationSpeed;
                float _StarBrightness1;
                float _StarBrightness2;
                float _FlowSpeed;
                float _FlowStrength;

                TEXTURE2D(_Cloud01Tex);
                TEXTURE2D(_Cloud02Tex);
                TEXTURE2D(_ColorPalette);
                TEXTURE2D(_TintColorTex);
                TEXTURE2D(_FlowTex);
                TEXTURE2D(_StarTex);
                SAMPLER(sampler_linear_repeat);
                SAMPLER(sampler_linear_clamp);


                struct vertex_input
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float2 uv1 : TEXCOORD1;
                    float2 uv2 : TEXCOORD2;
                    float4 color : COLOR;
                    float4 tangent : TANGENT;
                    float3 normal : NORMAL;
                };

                struct vertex_output 
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float2 uv1 : TEXCOORD1;
                    float2 uv2 : TEXCOORD2;
                    float4 ss_pos : TEXCOORD3;
                    float4 color : COLOR;
                    float3 parallax_y : TEXCOORD4;
                    float3 view : TEXCOORD5;
                    float3 parallax_z : TEXCOORD6;
                    float3 ws_pos : TEXCOORD7;

                };  

                vertex_output vert(vertex_input v)
                {
                    vertex_output o;
                    float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                    o.ws_pos = ws_pos.xyz;
                    float4 position =  mul(unity_MatrixMVP, v.vertex);
                    position.z = (_ES_EP_Enable) ? position.w * -2 : position.z;
                    o.vertex = position;
                    o.uv = v.uv;
                    o.uv1 = v.uv1;
                    o.uv2 = v.uv2;
                    o.ss_pos = ComputeScreenPos(position);
                    o.color = v.color;

                    // Transform normal to world space
                    float3 ws_normal = float3(
                        dot(v.normal, (float3)unity_WorldToObject[0]),
                        dot(v.normal, (float3)unity_WorldToObject[1]),
                        dot(v.normal, (float3)unity_WorldToObject[2])
                    );
                    ws_normal = normalize(ws_normal);
                    
                    // Transform tangent to world space
                    float3 ws_tangent = float3(
                        dot(v.tangent.xyz, (float3)unity_ObjectToWorld[0]),
                        dot(v.tangent.xyz, (float3)unity_ObjectToWorld[1]),
                        dot(v.tangent.xyz, (float3)unity_ObjectToWorld[2])
                    );
                    ws_tangent = normalize(ws_tangent);
                    
                    // Calculate bitangent
                    float3 ws_bitangent = cross(ws_normal, ws_tangent);
                    ws_bitangent = normalize(ws_bitangent);
                    ws_bitangent *= v.tangent.w * unity_WorldTransformParams.w;
                    
                    // Build TBN matrix
                    o.parallax_y = float3(ws_tangent.y, ws_bitangent.x, ws_normal.y);
                    o.parallax_z = float3(ws_tangent.x, ws_bitangent.y, ws_bitangent.z);
                    
                    // Calculate view direction
                    float3 view_dir = normalize(_WorldSpaceCameraPos.xyz - o.ws_pos);
                    o.parallax_y += float3(ws_tangent.y, ws_normal.x, 0) * view_dir.y;
                    o.view = view_dir;
                    o.parallax_z += float3(ws_tangent.z, ws_bitangent.z, ws_normal.z) * view_dir.z;
                    return o;
                }

                // Gradient function
                float hash(float2 p)
                {
                    float h = dot(p, float2(127.1, 311.7));
                    return frac(sin(h) * 43758.5453);
                }
                
                float gradient(float2 p, float2 offset)
                {
                    float h = hash(p + offset);
                    float angle = h * 2.0 * 3.14159265;
                    float2 grad = float2(cos(angle), sin(angle));
                    return dot(grad, offset - floor(offset));
                }

                float4 frag(vertex_output i) : SV_Target
                {
                    float4 SV_Target0;
                    float4 u_xlat0;
                    float4 u_xlat16_0;
                    float4 u_xlat1;
                    float3 u_xlat16_1;
                    float4 u_xlat2;
                    float3 u_xlat16_2;
                    float4 u_xlat3;
                    float4 u_xlat4;
                    float4 u_xlat5;
                    float3 u_xlat16_6;
                    float2 u_xlat16_7;
                    float u_xlat8;
                    float u_xlat16_8;
                    float2 u_xlat9;
                    float u_xlat16_9;
                    float2 u_xlat10;
                    float2 u_xlat16_10;
                    float3 u_xlat16_14;
                    float2 u_xlat16;
                    float u_xlat16_16;
                    float2 u_xlat17;
                    float u_xlat16_17;
                    bool u_xlatb17;
                    float u_xlat18;
                    float u_xlat24;
                    float u_xlat16_24;
                    float u_xlat25;
                    float u_xlat16_25;
                    float u_xlat16_30;
                    // float4 color = float4(1,1,1,1);
                    // Calculate Perlin noise in 3D space
                    float3 noisePos = i.ws_pos.xyz + _PerlinNoisePosOffset;
                    float3 scaledPos = noisePos * _PerlinNoiseScale;
                    float3 floorPos = floor(scaledPos);
                    float3 fracPos = frac(scaledPos);

                    // Smoothstep interpolation curve (3t^2 - 2t^3)
                    float3 smoothCurve = fracPos * fracPos * (3.0 - 2.0 * fracPos);

                    // Hash and gradient calculations for 8 corners of the cube
                    float4 hash0 = frac(sin(float4(
                        dot(floorPos, float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(1,0,0), float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(0,1,0), float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(1,1,0), float3(127.1, 311.7, 74.7))
                    )) * 43758.5453);

                    float4 hash1 = frac(sin(float4(
                        dot(floorPos + float3(0,0,1), float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(1,0,1), float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(0,1,1), float3(127.1, 311.7, 74.7)),
                        dot(floorPos + float3(1,1,1), float3(127.1, 311.7, 74.7))
                    )) * 43758.5453);

                    // Generate gradients and compute dot products with offset vectors
                    float g0 = dot(2.0 * hash0.x - 1.0, fracPos);
                    float g1 = dot(2.0 * hash0.y - 1.0, fracPos - float3(1,0,0));
                    float g2 = dot(2.0 * hash0.z - 1.0, fracPos - float3(0,1,0));
                    float g3 = dot(2.0 * hash0.w - 1.0, fracPos - float3(1,1,0));
                    float g4 = dot(2.0 * hash1.x - 1.0, fracPos - float3(0,0,1));
                    float g5 = dot(2.0 * hash1.y - 1.0, fracPos - float3(1,0,1));
                    float g6 = dot(2.0 * hash1.z - 1.0, fracPos - float3(0,1,1));
                    float g7 = dot(2.0 * hash1.w - 1.0, fracPos - float3(1,1,1));

                    // Interpolate along z
                    float gz0 = lerp(g0, g4, smoothCurve.z);
                    float gz1 = lerp(g1, g5, smoothCurve.z);
                    float gz2 = lerp(g2, g6, smoothCurve.z);
                    float gz3 = lerp(g3, g7, smoothCurve.z);

                    // Interpolate along y
                    float gy0 = lerp(gz0, gz2, smoothCurve.y);
                    float gy1 = lerp(gz1, gz3, smoothCurve.y);

                    // Interpolate along x
                    float perlinNoise = lerp(gy0, gy1, smoothCurve.x);

                    // Apply strength and offset
                    // Apply Perlin noise multiplier and offset
                    float perlinValue = perlinNoise * _PerlinNoiseMultiply;
                    perlinValue = perlinValue * 0.5 + _PerlinNoiseOffset;
                    perlinValue = perlinValue + 0.5;
                    perlinValue = clamp(perlinValue, 0.0, 1.0);

                    // Calculate flow animation time
                    float flowTime = _Time.y * _FlowSpeed + 0.5;
                    flowTime = frac(flowTime);
                    float flowInfluence = flowTime * _FlowStrength;

                    // Sample and blend Cloud02 with flow
                    float2 cloud02UV2 = i.uv1.xy * _CloudTex02UV2Coord.xy + _CloudTex02UV2Coord.zw;
                    float2 flowOffset = _FlowTex.Sample(sampler_linear_repeat, cloud02UV2).xy;
                    flowOffset = flowOffset * 2.0 - 1.0;
                    
                    float2 cloud02FlowUV = flowOffset * flowInfluence + cloud02UV2;
                    float cloud02SampleA = _Cloud02Tex.Sample(sampler_linear_repeat, cloud02FlowUV).x;
                    
                    float flowTimeLerp = _FlowSpeed * _Time.y;
                    flowTimeLerp = frac(flowTimeLerp);
                    float flowInfluenceB = flowTimeLerp * _FlowStrength;
                    flowTimeLerp = flowTimeLerp * 2.0 - 1.0;
                    
                    float2 cloud02FlowUVB = flowOffset * flowInfluenceB + cloud02UV2;
                    float cloud02SampleB = _Cloud02Tex.Sample(sampler_linear_repeat, cloud02FlowUVB).x;
                    
                    float cloud02Blended = lerp(cloud02SampleB, cloud02SampleA, abs(flowTimeLerp));

                    // Sample Cloud02 with UV1
                    float2 cloud02UV1 = i.uv.xy * _CloudTex02UV1Coord.xy + _CloudTex02UV1Coord.zw;
                    float2 flowOffset1 = _FlowTex.Sample(sampler_linear_repeat, cloud02UV1).xy;
                    flowOffset1 = flowOffset1 * 2.0 - 1.0;
                    
                    float2 cloud02UV1FlowA = flowOffset1 * flowInfluence + cloud02UV1;
                    float2 cloud02UV1FlowB = flowOffset1 * flowInfluenceB + cloud02UV1;
                    float cloud02UV1SampleA = _Cloud02Tex.Sample(sampler_linear_repeat, cloud02UV1FlowA).x;
                    float cloud02UV1SampleB = _Cloud02Tex.Sample(sampler_linear_repeat, cloud02UV1FlowB).x;
                    
                    float cloud02UV1Blended = lerp(cloud02UV1SampleB, cloud02UV1SampleA, abs(flowTimeLerp));

                    // Blend between UV2 and UV1
                    float gradientUpper = _GradientOffset + _GradientRange;
                    gradientUpper = i.uv.y * (-_GradientRange) + gradientUpper;
                    gradientUpper = clamp(gradientUpper, 0.0, 1.0);
                    
                    float gradientLower = i.uv.y * _GradientRange + _GradientOffset;
                    gradientLower = clamp(gradientLower, 0.0, 1.0);
                    
                    float gradientBlend = gradientUpper + (gradientUpper - gradientLower) * gradientUpper;
                    
                    float cloud02Final = lerp(cloud02UV1Blended, cloud02Blended, gradientBlend);
                    cloud02Final = cloud02Final + _Cloud02Offset;
                    cloud02Final = cloud02Final * _Cloud02Multipler;
                    cloud02Final = clamp(cloud02Final, 0.0, 2.0);

                    // Sample and blend Cloud01 with flow
                    float2 cloud01UV2 = i.uv1.xy * _CloudTex01UV2Coord.xy + _CloudTex01UV2Coord.zw;
                    float2 flowOffset01 = _FlowTex.Sample(sampler_linear_repeat, cloud01UV2).xy;
                    flowOffset01 = flowOffset01 * 2.0 - 1.0;
                    
                    float2 cloud01FlowA = flowOffset01 * flowInfluence + cloud01UV2;
                    float2 cloud01FlowB = flowOffset01 * flowInfluenceB + cloud01UV2;
                    float cloud01UV2SampleA = _Cloud01Tex.Sample(sampler_linear_repeat, cloud01FlowA).x;
                    float cloud01UV2SampleB = _Cloud01Tex.Sample(sampler_linear_repeat, cloud01FlowB).x;
                    
                    float cloud01UV2Blended = lerp(cloud01UV2SampleB, cloud01UV2SampleA, abs(flowTimeLerp));

                    // Sample Cloud01 with UV1
                    float2 cloud01UV1 = i.uv.xy * _CloudTex01UV1Coord.xy + _CloudTex01UV1Coord.zw;
                    float2 flowOffset01UV1 = _FlowTex.Sample(sampler_linear_repeat, cloud01UV1).xy;
                    flowOffset01UV1 = flowOffset01UV1 * 2.0 - 1.0;
                    
                    float2 cloud01UV1FlowA = flowOffset01UV1 * flowInfluence + cloud01UV1;
                    float2 cloud01UV1FlowB = flowOffset01UV1 * flowInfluenceB + cloud01UV1;
                    float cloud01UV1SampleA = _Cloud01Tex.Sample(sampler_linear_repeat, cloud01UV1FlowA).x;
                    float cloud01UV1SampleB = _Cloud01Tex.Sample(sampler_linear_repeat, cloud01UV1FlowB).x;
                    
                    float cloud01UV1Blended = lerp(cloud01UV1SampleB, cloud01UV1SampleA, abs(flowTimeLerp));

                    // Blend Cloud01 UV2 and UV1
                    float cloud01Final = lerp(cloud01UV1Blended, cloud01UV2Blended, gradientBlend);
                    cloud01Final = cloud01Final + _CloudOffset;
                    cloud01Final = cloud01Final * _CloudMultiplyer;
                    cloud01Final = clamp(cloud01Final, 0.0, 2.0);

                    // Combine clouds
                    float4 cloud01Colored = cloud01Final * _Cloud01Color;
                    float4 cloud02Colored = cloud02Final * _Cloud02Color;
                    float4 combinedClouds = lerp(cloud01Colored, cloud02Colored, perlinValue);

                    // Apply tint color
                    float2 tintUV2 = i.uv1.xy * _TintColorTexUV2Coord.xy + _TintColorTexUV2Coord.zw;
                    tintUV2.y = _Time.y * _NoiseSpeed + tintUV2.y;
                    float3 tintColor2 = _TintColorTex.Sample(sampler_linear_repeat, tintUV2).xyz;
                    
                    float2 tintUV1 = i.uv.xy * _TintColorTexUV1Coord.xy + _TintColorTexUV1Coord.zw;
                    tintUV1.y = _Time.y * _NoiseSpeed + tintUV1.y;
                    float3 tintColor1 = _TintColorTex.Sample(sampler_linear_repeat, tintUV1).xyz;
                    
                    float3 tintFinal = lerp(tintColor1, tintColor2, gradientBlend);
                    tintFinal = clamp(tintFinal, 0.0, 1.0);
                    
                    // Apply desaturation
                    float luminance = dot(tintFinal, float3(0.299, 0.587, 0.114));
                    tintFinal = lerp(luminance, tintFinal, _TintColorTexScale);
                    
                    float4 cloudWithTint = float4(combinedClouds.xyz * tintFinal, combinedClouds.w);
                    cloudWithTint *= _AllCloudsAlpha;

                    // Calculate stars brightness threshold
                    float starsBrightness = dot(cloudWithTint.xyz, float3(0.299, 0.587, 0.114));
                    starsBrightness = (starsBrightness - 0.04) * 10.0;
                    starsBrightness = clamp(starsBrightness, 0.0, 1.0);

                    // Sample stars
                    float2 starsUV2 = i.uv1.xy * _StarTexUV2Coord.xy + _StarTexUV2Coord.zw;
                    float2 parallaxOffset = i.parallax_y.xy * _StarDepth;
                    float2 starsParallaxUV2 = starsUV2 * 0.4 + parallaxOffset;
                    
                    float starsSample1X = _StarTex.Sample(sampler_linear_repeat, starsUV2).x;
                    float starsSample1Y = _StarTex.Sample(sampler_linear_repeat, starsParallaxUV2).y;
                    
                    float2 starsUV1 = i.uv.xy * _StarTexUV1Coord.xy + _StarTexUV1Coord.zw;
                    float2 starsParallaxUV1 = starsUV1 * 0.4 + parallaxOffset;
                    
                    float starsSample2X = _StarTex.Sample(sampler_linear_repeat, starsUV1).x;
                    float starsSample2Y = _StarTex.Sample(sampler_linear_repeat, starsParallaxUV1).y;
                    
                    float starsBlendedY = lerp(starsSample2Y, starsSample1Y, gradientBlend);
                    float starsBlendedX = lerp(starsSample2X, starsSample1X, gradientBlend);
                    
                    float2 starsIntensity = float2(starsBlendedX, starsBlendedY) * float2(_StarBrightness1, _StarBrightness2);
                    float starsCombined = starsIntensity.x * starsBrightness + starsIntensity.y;

                    // Sample scintillation noise
                    float scintillationTime = _StarScintillationSpeed * _Time.y;
                    float2 scintillationUV = i.uv2.xy * _StarNoiseTiling.xy;
                    float2 scintillationUVA = scintillationUV * 2.0 + scintillationTime * float2(0.1, 0.5);
                    float2 scintillationUVB = scintillationUV + scintillationTime * float4(0.4, 0.2, 0.1, 0.5).xy;
                    
                    float scintillationA = _StarTex.Sample(sampler_linear_repeat, scintillationUVB).z;
                    float scintillationB = _StarTex.Sample(sampler_linear_repeat, scintillationUVA).z;
                    float scintillation = scintillationA * scintillationB * 3.0;
                    scintillation = clamp(scintillation, 0.0, 1.0);
                    
                    float starsWithScintillation = starsCombined * scintillation;

                    // Sample color palette
                    float2 paletteUV2 = i.uv1.xy * _ColorPalette_ST.xy + _ColorPalette_ST.zw;
                    paletteUV2.x = _Time.y * _ColorPalletteSpeed + paletteUV2.y;
                    float3 paletteColor2 = _ColorPalette.Sample(sampler_linear_repeat, paletteUV2).xyz;
                    
                    float2 paletteUV1 = i.uv.xy * _ColorPalette_ST.xy + _ColorPalette_ST.zw;
                    paletteUV1.x = _Time.y * _ColorPalletteSpeed + paletteUV1.y;
                    float3 paletteColor1 = _ColorPalette.Sample(sampler_linear_repeat, paletteUV1).xyz;
                    
                    float3 paletteFinal = lerp(paletteColor1, paletteColor2, gradientBlend);
                    float3 paletteDesaturated = lerp(paletteFinal, float3(1.0, 1.0, 1.0), _Desaturate);
                    
                    float3 finalColor = starsWithScintillation * paletteDesaturated + cloudWithTint.xyz;

                    SV_Target0.xyz = finalColor;
                    SV_Target0.w = cloudWithTint.w;
                    return SV_Target0;
                }   
            ENDHLSL

        }
    }

    CustomEditor "LWGUI.LWGUI" 
    FallBack "Diffuse"
}