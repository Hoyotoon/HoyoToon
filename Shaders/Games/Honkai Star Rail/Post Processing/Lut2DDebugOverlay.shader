Shader "HoyoToon/Honkai Star Rail/Post Processing/Lut2DDebugOverlay"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OverlayLut2D"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            TEXTURE2D(_Lut2DTex);
            SAMPLER(sampler_Lut2DTex);

            float4 _OverlayRect;
            float _OverlayOpacity;
            float _OverlayBorderPixels;
            float4 _OverlayBorderColor;
            float _OverlayFlipLutY;

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return o;
            }

            float3 DrawOverlay(float2 screenUv)
            {
                float2 rectMin = _OverlayRect.xy;
                float2 rectSize = max(_OverlayRect.zw, float2(1e-5, 1e-5));
                float2 localUv = saturate((screenUv - rectMin) / rectSize);

                if (_OverlayFlipLutY > 0.5)
                {
                    localUv.y = 1.0 - localUv.y;
                }

                float3 lutColor = SAMPLE_TEXTURE2D(_Lut2DTex, sampler_Lut2DTex, localUv).rgb;

                float2 rectPixelSize = max(rectSize * _ScreenParams.xy, float2(1.0, 1.0));
                float2 borderUv = _OverlayBorderPixels / rectPixelSize;
                float2 edgeDistance = min(localUv, 1.0 - localUv);
                float2 edgeMask = step(edgeDistance, borderUv);
                float borderMask = saturate(edgeMask.x + edgeMask.y);

                return lerp(lutColor, _OverlayBorderColor.rgb, borderMask * saturate(_OverlayBorderColor.a));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);

                float2 rectMin = _OverlayRect.xy;
                float2 rectMax = _OverlayRect.xy + _OverlayRect.zw;
                float inside = step(rectMin.x, screenUv.x)
                    * step(rectMin.y, screenUv.y)
                    * step(screenUv.x, rectMax.x)
                    * step(screenUv.y, rectMax.y);

                if (inside <= 0.0)
                {
                    return sceneColor;
                }

                float3 overlayColor = DrawOverlay(screenUv);
                sceneColor.rgb = lerp(sceneColor.rgb, overlayColor, saturate(_OverlayOpacity));
                return sceneColor;
            }
            ENDHLSL
        }
    }
}
