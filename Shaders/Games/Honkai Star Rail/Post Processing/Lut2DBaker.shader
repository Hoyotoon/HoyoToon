Shader "HoyoToon/Honkai Star Rail/Post Processing/Lut2DBaker"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _LutSize("Lut Size", Float) = 16
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        HLSLINCLUDE
            float _LevelHighTone;
            float _LevelShadowTone;
            float3 _LevelColor;
            float4 _ColorSaturationShadow;
            float4 _ColorContrastShadow;
            float4 _ColorGainShadow;
            float4 _ColorSaturationMidtone;
            float4 _ColorContrastMidtone;
            float4 _ColorGainMidtone;
            float4 _ColorSaturationHighlight;
            float4 _ColorContrastHighlight;
            float4 _ColorGainHighlight;
            float _ColorCorrectionShadowMax;
            float _ColorCorrectionHighlightMin;
            float4 _Lut2D_Params;
            float3 _HueSatCon;
            float4 _CustomToneCurve;
            float4 _ToeSegmentA;
            float4 _ToeSegmentB;
            float4 _MidSegmentA;
            float4 _MidSegmentB;
            float4 _ShoSegmentA;
            float4 _ShoSegmentB;
            float _ExpandGamut;
            float _HDRHeadroom;
            float _EnableHDRTonemapping;
            float _DebugHDROutputIntermediate;
            float _ForceDisableToneMapping;
        ENDHLSL

        Pass
        {
            Name "BakeLUT2D"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            static const float3 kSRLumaWeights = float3(0.272228986, 0.674081981, 0.0536894985);

            float GetSRLuminance(float3 color)
            {
                return dot(color, kSRLumaWeights);
            }

            float3 DecodeLutUvToLinear(float2 uv)
            {
                float2 lutOffset = uv - _Lut2D_Params.yz;
                float lutSliceFrac = frac(lutOffset.x * _Lut2D_Params.x);
                float lutSliceCoord = lutOffset.x - (lutSliceFrac / _Lut2D_Params.x);

                float3 encoded = float3(lutSliceFrac, lutOffset.y, lutSliceCoord) * _Lut2D_Params.www + float3(-0.413588405, -0.413588405, -0.413588405);
                encoded = encoded * _HueSatCon.zzz + float3(0.413588405, 0.413588405, 0.413588405);

                float3 lin = encoded + float3(-0.386036009, -0.386036009, -0.386036009); 
                lin = exp2(lin * float3(13.6054821, 13.6054821, 13.6054821));
                lin = (lin + float3(-0.0479959995, -0.0479959995, -0.0479959995)) * float3(0.179999992, 0.179999992, 0.179999992);
                return max(lin, float3(0.0, 0.0, 0.0));
            }

            float3 ApplySaturationContrastGain(float3 color, float luminance, float4 saturation, float4 contrast, float4 gain)
            {
                float3 adjusted = saturation.www * saturation.xyz;
                adjusted = adjusted * (color - luminance) + luminance;
                adjusted = max(adjusted, float3(0.00100000005, 0.00100000005, 0.00100000005));
                adjusted = adjusted * float3(5.55555534, 5.55555534, 5.55555534);
                adjusted = pow(adjusted, contrast.www * contrast.xyz);
                adjusted = adjusted * float3(0.180000007, 0.180000007, 0.180000007);
                adjusted = adjusted * (gain.www * gain.xyz);
                return adjusted;
            }

            float3 ApplyLevelTones(float3 color, float luminance)
            {
                float toneRange = _LevelShadowTone + _LevelHighTone;
                float pivot = toneRange * 0.5;
                bool useUpperRange = pivot < luminance;

                float3 upperRangeColor = (-toneRange) * float3(0.5, 0.5, 0.5) + color;
                float upperRangeDenominator = (-toneRange) * 0.5 + _LevelHighTone;
                upperRangeColor = upperRangeColor / upperRangeDenominator;
                upperRangeColor = upperRangeColor * float3(0.5, 0.5, 0.5) + float3(0.5, 0.5, 0.5);
                upperRangeColor = max(upperRangeColor, float3(0.0, 0.0, 0.0));

                float3 lowerRangeColor = toneRange * float3(0.5, 0.5, 0.5) + (-color);
                float lowerRangeDenominator = toneRange * 0.5 + (-_LevelShadowTone);
                lowerRangeColor = lowerRangeColor / lowerRangeDenominator;
                lowerRangeColor = (-lowerRangeColor) * float3(0.5, 0.5, 0.5) + float3(0.5, 0.5, 0.5);
                lowerRangeColor = max(lowerRangeColor, float3(0.0, 0.0, 0.0));

                float3 leveledColor = useUpperRange ? upperRangeColor : lowerRangeColor;
                return leveledColor * _LevelColor;
            }

            void SelectToneCurveSegment(float scaledValue, out float4 segmentA, out float2 segmentB)
            {
                bool useToe = scaledValue < _CustomToneCurve.y;
                bool useMid = scaledValue < _CustomToneCurve.z;

                segmentA = useMid ? _MidSegmentA : _ShoSegmentA;
                segmentB = useMid ? _MidSegmentB.xy : _ShoSegmentB.xy;

                if (useToe)
                {
                    segmentA = _ToeSegmentA;
                    segmentB = _ToeSegmentB.xy;
                }
            }

            void SelectToeOrMidToneCurveSegment(float scaledValue, out float4 segmentA, out float2 segmentB)
            {
                bool useToe = scaledValue < _CustomToneCurve.y;
                segmentA = useToe ? _ToeSegmentA : _MidSegmentA;
                segmentB = useToe ? _ToeSegmentB.xy : _MidSegmentB.xy;
            }

            float EvalToneCurveSegment(float value, float4 segmentA, float2 segmentB)
            {
                float segmentInput = value * _CustomToneCurve.x + (-segmentA.x);
                segmentInput = segmentA.z * segmentInput;

                bool isPositive = 0.0 < segmentInput;
                float segmentOutput = 0.0;

                if (isPositive)
                {
                    float logValue = log2(segmentInput);
                    float naturalLog = logValue * 0.693147182;
                    float expInput = segmentB.y * naturalLog + segmentB.x;
                    expInput = expInput * 1.44269502;
                    segmentOutput = exp2(expInput);
                }

                return segmentOutput * segmentA.w + segmentA.y;
            }

            float3 ApplyCustomToneCurve(float3 color)
            {
                float4 segmentA;
                float2 segmentB;
                float3 curveColor;

                SelectToneCurveSegment(color.x * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.x = EvalToneCurveSegment(color.x, segmentA, segmentB);

                SelectToneCurveSegment(color.y * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.y = EvalToneCurveSegment(color.y, segmentA, segmentB);

                SelectToneCurveSegment(color.z * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.z = EvalToneCurveSegment(color.z, segmentA, segmentB);

                return curveColor;
            }

            float3 ApplyCustomToneCurveToeMid(float3 color)
            {
                float4 segmentA;
                float2 segmentB;
                float3 curveColor;

                SelectToeOrMidToneCurveSegment(color.x * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.x = EvalToneCurveSegment(color.x, segmentA, segmentB);

                SelectToeOrMidToneCurveSegment(color.y * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.y = EvalToneCurveSegment(color.y, segmentA, segmentB);

                SelectToeOrMidToneCurveSegment(color.z * _CustomToneCurve.x, segmentA, segmentB);
                curveColor.z = EvalToneCurveSegment(color.z, segmentA, segmentB);

                return curveColor;
            }

            float ApplyHdrHeadroomKnee(float value)
            {
                float normalized = value / _HDRHeadroom;
                float distanceToLinearKnee = normalized - 0.899999976;
                float distanceToQuadraticCenter = normalized - 0.699999988;

                bool useQuadraticKnee = 0.200000003 >= abs(distanceToLinearKnee);
                bool useLinearShoulder = 0.200000003 < distanceToLinearKnee;

                float quadraticKnee = distanceToQuadraticCenter * distanceToQuadraticCenter;
                quadraticKnee = quadraticKnee * -1.171875 + normalized;

                float linearShoulder = distanceToLinearKnee * 0.0625 + 0.899999976;

                float shaped = useLinearShoulder ? linearShoulder : normalized;
                shaped = useQuadraticKnee ? quadraticKnee : shaped;
                return shaped * _HDRHeadroom;
            }

            float3 ApplyHdrHeadroomKnee(float3 color)
            {
                return float3(
                    ApplyHdrHeadroomKnee(color.x),
                    ApplyHdrHeadroomKnee(color.y),
                    ApplyHdrHeadroomKnee(color.z)
                );
            }

            float3 ToHdrTonemapSpace(float3 color)
            {
                float3 tonemapSpace;
                tonemapSpace.x = dot(float3(0.613189995, 0.339509994, 0.0473700017), color);
                tonemapSpace.y = dot(float3(0.0702100024, 0.916339993, 0.0134500004), color);
                tonemapSpace.z = dot(float3(0.0206199996, 0.109569997, 0.869610012), color);
                return tonemapSpace;
            }

            float3 FromHdrTonemapSpace(float3 color)
            {
                float3 lin;
                lin.x = dot(float3(1.70504999, -0.621789992, -0.0832599998), color);
                lin.y = dot(float3(-0.130260006, 1.1408, -0.0105499998), color);
                lin.z = dot(float3(-0.0240000002, -0.128969997, 1.15296996), color);
                return lin;
            }

            float3 ExpandHdrGamut(float3 tonemapSpaceColor)
            {
                float tonemapLuma = GetSRLuminance(tonemapSpaceColor);
                float3 normalizedChromaticity = tonemapSpaceColor / tonemapLuma;
                float3 chromaOffset = normalizedChromaticity + float3(-1.0, -1.0, -1.0);

                float chromaDistance = dot(chromaOffset, chromaOffset);
                float chromaBlend = 1.0 - exp2(chromaDistance * -4.0);

                float lumaBlend = tonemapLuma * tonemapLuma;
                lumaBlend = lumaBlend * _ExpandGamut;
                lumaBlend = 1.0 - exp2(lumaBlend * -4.0);

                float gamutBlend = lumaBlend * chromaBlend;

                float3 expanded;
                expanded.x = dot(float3(1.37041104, -0.329291195, -0.0636843145), tonemapSpaceColor);
                expanded.y = dot(float3(-0.0834370106, 1.09708822, -0.0108630517), tonemapSpaceColor);
                expanded.z = dot(float3(-0.0257899351, -0.0986270159, 1.20369244), tonemapSpaceColor);

                return gamutBlend * (expanded - tonemapSpaceColor) + tonemapSpaceColor;
            }

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o = (v2f)0;
                o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 color = DecodeLutUvToLinear(i.uv);
                float luma = GetSRLuminance(color);

                float shadowWeight = 1.0 - smoothstep(0.0, _ColorCorrectionShadowMax, luma);
                float highlightWeight = smoothstep(_ColorCorrectionHighlightMin, 1.0, luma);
                float midtoneWeight = (1.0 - shadowWeight) - highlightWeight;

                float3 shadowColor = ApplySaturationContrastGain(color, luma, _ColorSaturationShadow, _ColorContrastShadow, _ColorGainShadow);
                float3 highlightColor = ApplySaturationContrastGain(color, luma, _ColorSaturationHighlight, _ColorContrastHighlight, _ColorGainHighlight);
                float3 midtoneColor = ApplySaturationContrastGain(color, luma, _ColorSaturationMidtone, _ColorContrastMidtone, _ColorGainMidtone);

                color = shadowColor * shadowWeight + midtoneColor * midtoneWeight + highlightColor * highlightWeight;
                color = ApplyLevelTones(color, luma);

                if (0.5 >= _ForceDisableToneMapping)
                {
                    if (0.5 < _EnableHDRTonemapping)
                    {
                        color = ExpandHdrGamut(ToHdrTonemapSpace(color));

                        if (!(0.5 < _DebugHDROutputIntermediate))
                        {
                            float3 toneCurved = ApplyCustomToneCurve(color);
                            float3 headroomCurved = ApplyCustomToneCurveToeMid(color);
                            float3 headroomShaped = ApplyHdrHeadroomKnee(headroomCurved);

                            float headroomLuma = GetSRLuminance(headroomShaped);
                            float toneCurveLuma = GetSRLuminance(toneCurved);
                            bool nearZeroLuma = toneCurveLuma < 9.99999997e-07;

                            float luminanceScale = headroomLuma / toneCurveLuma;
                            luminanceScale = nearZeroLuma ? 0.0 : luminanceScale;

                            color = FromHdrTonemapSpace(luminanceScale * toneCurved);
                        }
                    }
                    else
                    {
                        color = ApplyCustomToneCurve(color);
                    }
                }

                bool bypassClamp = 0.800000012 < _EnableHDRTonemapping;

                float4 outputColor;
                outputColor.xyz = max(color, float3(0.0, 0.0, 0.0));
                outputColor.w = 1.0;
                if (bypassClamp)
                {
                    outputColor.xyz = color;
                    outputColor.w = 1.0;
                }

                return outputColor;
            }

            ENDHLSL
        }
    }
}
