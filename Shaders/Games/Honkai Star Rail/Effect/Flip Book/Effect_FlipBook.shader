Shader "HoyoToon/Honkai Star Rail/Effect/Flip Book"
{
    Properties
    {
        [Main(MainGroup, _, off, off)] _MainGroup ("Main", Float) = 0
        [SubToggle(MainGroup, CUSTOMDATA)] _CustomData ("Custom Data", Float) = 0
        [Tex(MainGroup, small, collapsed)] _MainTex ("Diffuse", 2D) = "white" { }
        [Sub(MainGroup)] [HDR] _Color ("Color", Color) = (1,1,1,1)
        [Tex(MainGroup, ramp)] _ColorRamp ("Color Ramp", 2D) = "white" { }
        [SubGroup(MainGroup, AlphaControl)] _AlphaControl ("Alpha Control", Float) = 0
        [Sub(AlphaControl)]_AlphaScale ("Alpha Scale", Float) = 1
        [SubToggle(AlphaControl)] _AlphaFromGray ("Alpha From Gray", Float) = 0
        [Main(FlipBook, _, off, off)] _FlipBook ("Flip Book", Float) = 0
        
        [Sub(FlipBook)] _FlipBookAmount ("Flip Book Amount [x:Horizontal y:Vertical z:Channel w:Valid]", Vector) = (1,1,0,0)
        [Sub(FlipBook)] _FlipBookID ("Flip Book ID", Float) = 0
        [SubEnum(FlipBook, Button to Top, 0, Top to Button, 1)] _FlipBookDirection ("Flip Book Direction", Float) = 1
        [SubToggle(FlipBook, FLIPBOOKBLEND)] _FlipBookBlend ("Flip Book Blend", Float) = 0
        [SubToggle(FlipBook)] _FlipBookAutoPlay ("Auto Play", Float) = 0
        [Sub(FlipBook)] _FlipBookAutoPlaySpeed ("Auto Play Speed", Float) = 0
        [SubToggle(FlipBook)] _FlipBookAutoPlayCurve ("Auto Play Curve", Float) = 0
        [Tex(FlipBook, ramp)] _FlipBookAutoPlayCurveMap ("Auto Play Curve Map", 2D) = "black" { }
        [Main(Effects, _, off, off)] _Effects ("Effects", Float) = 0
        [SubGroup(Effects, SoftParticle)] _SoftParticleEffect ("Soft Particle", Float) = 0
        [Sub(SoftParticle)] _SoftNear ("Soft Near", Range(-5, 5)) = 0
        [Sub(SoftParticle)] _SoftFar ("Soft Far", Range(-5, 5)) = 1
        [Sub(SoftParticle)] _SoftZClip ("Soft Z Clip", Float) = 0
        [Sub(SoftParticle)] _SoftZClipThreshold ("Soft Z Clip Threshold", Range(0, 1)) = 0
        [HideInInspector] _SoftZClipOffset ("Soft Z Clip Offset", Range(1, 10)) = 1
        [SubGroup(Effects, Rendering)] _RenderingEffect ("Rendering Effect", Float) = 0
        [SubEnum(Rendering, UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        [SubEnum(Rendering, Off, 4, On, 0)] _AlwaysOnTop ("Always On Top", Float) = 4
        [SubEnum(Rendering, Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
        [SubEnum(Rendering, AlphaBlend, 0, Additive, 1, Multiply, 2, OneChannel, 3, Opaque, 4)] _RenderingMode ("Rendering Mode", Float) = 0
        [SubEnum(Rendering, UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", Float) = 1
        [SubEnum(Rendering, UnityEngine.Rendering.BlendMode)] _DstBlend ("DstBlend", Float) = 0
    }
    SubShader
    {
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        ENDHLSL

        Pass
        {
            Tags { "LIGHTMODE" = "CustomRPTransparent" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            CBUFFER_START(UnityPerMaterial)
            float4                _MainTex_ST;
            float4                _Color;
            float                _AlphaScale;
            float                _AlphaFromGray;
            float4                _FlipBookAmount;
            float                _FlipBookID;
            float                _FlipBookAutoPlaySpeed;
            int                _FlipBookDirection;
            int                _FlipBookAutoPlay;
            int                _FlipBookAutoPlayCurve;
            float                _SoftZClip;
            float                _SoftZClipThreshold;
            float                _SoftZClipOffset;
            float                _EffectOverrideTimeEnable;
            float                _EffectOverrideTime;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            TEXTURE2D(_ColorRamp);
            TEXTURE2D(_FlipBookAutoPlayCurveMap);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_ColorRamp);
            SAMPLER(sampler_FlipBookAutoPlayCurveMap);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = ws_pos.xyz;
                float4 custom_position = mul(unity_MatrixV, ws_pos);
                float4 position = mul(UNITY_MATRIX_P, custom_position);
                o.vertex = position;
                o.normal = v.normal;
                o.color = v.color;
                // Apply UV scaling and offset
                float2 uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                
                // Calculate total flip book frames
                float totalFramesRaw = _FlipBookAmount.x * _FlipBookAmount.y;
                int totalFramesInt = int(totalFramesRaw);
                float totalFrames = trunc(totalFramesRaw);
                
                // Determine number of channels
                bool hasChannel = _FlipBookAmount.z >= 1.0;
                float channelCount = hasChannel ? trunc(totalFrames * _FlipBookAmount.z) : totalFrames;
                
                // Clamp to valid range if specified
                bool hasValidRange = _FlipBookAmount.w > 0.0;
                float maxFrames = max(_FlipBookAmount.w, 1.0);
                channelCount = hasValidRange ? min(channelCount, maxFrames) : channelCount;
                channelCount = trunc(channelCount);
                
                // Calculate current frame index
                float frameIndex;
                if (_FlipBookAutoPlay != 0) {
                    // Auto play mode: use time-based animation
                    float time = (_EffectOverrideTimeEnable != 0.0) ? _EffectOverrideTime : _Time.y;
                    float normalizedTime = time * _FlipBookAutoPlaySpeed / channelCount;
                    float normalizedTimeFrac = frac(abs(normalizedTime));
                    normalizedTimeFrac = (normalizedTime >= (-normalizedTime)) ? normalizedTimeFrac : (-normalizedTimeFrac);
                    frameIndex = channelCount * normalizedTimeFrac;
                    
                    // Apply curve if enabled
                    if (_FlipBookAutoPlayCurve != 0) {
                        float2 curveUV = float2(frameIndex / channelCount, 0.5);
                        float curveValue = _FlipBookAutoPlayCurveMap.SampleLevel(sampler_FlipBookAutoPlayCurveMap, curveUV, 0).x;
                        frameIndex = channelCount * curveValue;
                    }
                } else {
                    // Manual mode: use FlipBookID
                    float normalizedID = _FlipBookID / channelCount;
                    float normalizedIDFrac = frac(abs(normalizedID));
                    normalizedIDFrac = (normalizedID >= (-normalizedID)) ? normalizedIDFrac : (-normalizedIDFrac);
                    frameIndex = channelCount * normalizedIDFrac;
                }
                
                // Calculate frame coordinates
                float frameRow = floor(frameIndex);
                int frameRowInt = int(frameRow);

                // Split into channel index and in-sheet frame index (sign-preserving division behavior).
                int frameRowAbs = max(frameRowInt, -frameRowInt);
                int totalFramesAbs = max(totalFramesInt, -totalFramesInt);
                int channelIndex = frameRowAbs / totalFramesAbs;
                bool channelNegative = ((asuint(totalFramesInt) ^ asuint(frameRowInt)) & 2147483648u) != 0u;
                channelIndex = channelNegative ? -channelIndex : channelIndex;

                float frameOverTotal = frameRow / totalFrames;
                float frameOverTotalFrac = frac(abs(frameOverTotal));
                frameOverTotalFrac = (frameOverTotal >= (-frameOverTotal)) ? frameOverTotalFrac : (-frameOverTotalFrac);
                int tileIndex = int(totalFrames * frameOverTotalFrac);

                int frameForUV = hasChannel ? tileIndex : frameRowInt;
                int channelForSample = hasChannel ? channelIndex : 0;

                float frameDivX = float(frameForUV) / _FlipBookAmount.x;
                float frameDivXFrac = frac(abs(frameDivX));
                frameDivXFrac = (frameDivX >= (-frameDivX)) ? frameDivXFrac : (-frameDivXFrac);
                float frameX = _FlipBookAmount.x * frameDivXFrac;
                float frameY = floor(frameDivX);
                
                // Handle direction
                if (_FlipBookDirection != 0) {
                    frameY = (_FlipBookAmount.y - 1.0) - frameY;
                }
                
                // Apply frame offset to UV
                float2 frameOffset = trunc(float2(frameX, frameY));
                uv = uv + frameOffset;
                
                o.texcoord0.xy = uv / _FlipBookAmount.xy;
                o.texcoord0.zw = float2(0.0, 0.0);
                o.texcoord1 = float4(0.0, 0.0, float(channelForSample), 0.0);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 identity[4];
                identity[0] = float4(1.0,0.0,0.0,0.0);
                identity[1] = float4(0.0,1.0,0.0,0.0);
                identity[2] = float4(0.0,0.0,1.0,0.0);
                identity[3] = float4(0.0,0.0,0.0,1.0);
                    float4 diffuse = _MainTex.SampleLevel(sampler_MainTex, i.texcoord0.xy, 0.0);
                    float4 output;
                    float4 SV_Target0 = float4(1.0, 1.0, 1.0, 1.0);
                    if(_FlipBookAmount.z>=1.0){
                        output.x = dot(diffuse, identity[uint(round(i.texcoord1.z))]);
                        output.yzw = output.xxx;
                    } else {
                        output = diffuse.wxyz;
                    }
                    float fgrey = dot(output.yzw, float3(0.212672904, 0.715152204, 0.0721750036));
                    output.x = lerp(output.x, fgrey, _AlphaFromGray);
                    float3 ramp = _ColorRamp.SampleLevel(sampler_ColorRamp, float2(fgrey, 0.5), 0.0).xyz;
                    output.x = saturate(output.x * _AlphaScale);
                    float4 xcolor = i.color + _Color;
                    SV_Target0.xyz = ramp * xcolor.xyz;
                    output.x = xcolor.w * output.x;
                    
                    // Apply soft Z-clip effect
                    bool useSoftZClip = _SoftZClip > 0.5;
                    float clipRange = 1.0 - _SoftZClipThreshold;
                    float offsetValue = (_SoftZClipOffset <= 0.0) ? 1.0 : _SoftZClipOffset;
                    float clipStart = 1.0 - clipRange;
                    
                    // Calculate depth-based fade
                    float depthFade = i.worldPos.z * offsetValue - _SoftZClipThreshold;
                    depthFade = depthFade / clipRange;
                    depthFade = clamp(depthFade, 0.0, 1.0);
                    
                    // Apply smoothstep-like curve
                    float smoothCurve = depthFade * -2.0 + 3.0;
                    float fadedAlpha = depthFade * depthFade;
                    fadedAlpha = (1.0 - smoothCurve) * fadedAlpha + 1.0;
                    fadedAlpha = fadedAlpha * output.x;
                    
                    SV_Target0.w = useSoftZClip ? fadedAlpha : output.x;
                    return SV_Target0;
            }
            ENDHLSL
        }
    }
     CustomEditor "LWGUI.LWGUI" 
}   