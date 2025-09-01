// from: https://github.com/cnlohr/shadertrixx/blob/main/README.md#best-practice-for-getting-depth-of-a-given-pixel-from-the-depth-texture
float GetLinearZFromZDepth_WorksWithMirrors(float zDepthFromMap, float2 screenUV)
{
	#if defined(UNITY_REVERSED_Z)
	zDepthFromMap = 1 - zDepthFromMap;
			
	// When using a mirror, the far plane is whack.  This just checks for it and aborts.
	if( zDepthFromMap >= 1.0 ) return _ProjectionParams.z;
	#endif

	float4 clipPos = float4(screenUV.xy, zDepthFromMap, 1.0);
	clipPos.xyz = 2.0f * clipPos.xyz - 1.0f;
	float4 camPos = mul(unity_CameraInvProjection, clipPos);
	return -camPos.z / camPos.w;
}

float packed_channel_picker(SamplerState texture_sampler, Texture2D texture_2D, float2 uv, float channel)
{
    float4 packed = texture_2D.Sample(texture_sampler, uv);

    float choice;
    if(channel == 0) {choice = packed.x;}
    else if(channel == 1) {choice = packed.y;}
    else if(channel == 2) {choice = packed.z;}
    else if(channel == 3) {choice = packed.w;}

    return choice;
}

float3 hue_shift(float3 in_color, float material_id, float shift1, float shift2, float shift3, float shift4, float shift5, float shift6, float shift7, float shift8,float shiftglobal, float autobool, float autospeed, float mask)
{  
    #if defined(can_shift) 
        float auto_shift = (_Time.y * autospeed) * autobool; 
        
        float shift[8] = 
        {
            shift1,
            shift2,
            shift3,
            shift4,
            shift5,
            shift6,
            shift7,
            shift8
        };
        
        float shift_all = 0.0f;
        if(shift[material_id] > 0)
        {
            shift_all = shift[material_id] + auto_shift;
        }
        
        auto_shift = (_Time.y * autospeed) * autobool; 
        if(shiftglobal > 0)
        {
            shiftglobal = shiftglobal + auto_shift;
        }
        
        float hue = shift_all + shiftglobal;
        hue = lerp(0.0f, 6.27f, hue);

        float3 k = (float3)0.57735f;
        float cosAngle = cos(hue);

        float3 adjusted_color = in_color * cosAngle + cross(k, in_color) * sin(hue) + k * dot(k, in_color) * (1.0f - cosAngle);

        return lerp(in_color, adjusted_color, mask);
    #else
        return in_color;
    #endif
}

int material_region(float lightmap_alpha)
{
    int material = 0;
    if(lightmap_alpha > 0.5 && lightmap_alpha < 1.5 )
    {
        material = 1;
    } 
    else if(lightmap_alpha > 1.5f && lightmap_alpha < 2.5f)
    {
        material = 2;
    } 
    else if(lightmap_alpha > 2.5f && lightmap_alpha < 3.5f)
    {
        material = 3;
    } 
    else
    {
        material = (lightmap_alpha > 6.5f && lightmap_alpha < 7.5f) ? 7 : 0;
        material = (lightmap_alpha > 5.5f && lightmap_alpha < 6.5f) ? 6 : material;
        material = (lightmap_alpha > 4.5f && lightmap_alpha < 5.5f) ? 5 : material;
        material = (lightmap_alpha > 3.5f && lightmap_alpha < 4.5f) ? 4 : material;
    }

    if(_HairMaterial) material = 0;

    return material;
}

// float shadow_rate(float ndotl, float lightmap_ao, float vertex_ao, float shadow_ramp, float shadow_map)
float shadow_rate(float ndotl, float lightmap_ao, float vertex_ao, float shadow_ramp, float casted)
{
    float shadow_ndotl  = (ndotl * 0.5f + 0.5f) * casted;
    float shadow_thresh = (lightmap_ao + lightmap_ao) * vertex_ao;
    float shadow_area   = min(1.0f, dot(shadow_ndotl.xx, shadow_thresh.xx));
    #ifndef _IS_PASS_LIGHT
        shadow_area = max(0.001f, shadow_area) * 0.85f + 0.15f;
        shadow_area = (shadow_area > shadow_ramp) ? 0.99f : shadow_area;
    #else
        shadow_area = smoothstep(0.5f, 1.0f, shadow_area);
    #endif
    return shadow_area;
}

float shadow_rate_face(float2 uv, float3 light)
{
    #if defined(faceishadow)
        float3 head_forward = normalize(UnityObjectToWorldDir(_headForwardVector.xyz));
        float3 head_right   = normalize(UnityObjectToWorldDir(_headRightVector.xyz));
        float rdotl = dot((head_right.xz),  (light.xz));
        float fdotl = dot((head_forward.xz), (light.xz));

        float2 faceuv = uv;
        if(rdotl > 0.0f )
        {
            faceuv = uv;
        }  
        else
        {
            faceuv = uv * float2(-1.0f, 1.0f) + float2(1.0f, 0.0f);
        }

        float shadow_step = 1.0f - (fdotl * 0.5f + 0.5f);

        // apply rotation offset
        
        // use only the alpha channel of the texture 
        float facemap = _FaceMap.Sample(sampler_linear_repeat, faceuv).w;
        // interpolate between sharp and smooth face shading
        shadow_step = smoothstep(shadow_step - (_ShadowSoftness), shadow_step + (_ShadowSoftness), facemap);

    #else
        float shadow_step = 1.00f;
    #endif

    return shadow_step;
}

float3 specular_base(float shadow_area, float ndoth, float lightmap_spec, float3 specular_color, float3 specular_values, float3 specular_color_global, float specular_intensity_global)
{
    #if defined(use_specular)
        float3 specular = ndoth;
        specular = pow(max(specular, 0.01f), specular_values.x);
        specular_values.y = max(specular_values.y, 0.001f);

        float specular_thresh = 1.0f - lightmap_spec;
        float rough_thresh = specular_thresh - specular_values.y;
        specular_thresh = (specular_values.y + specular_thresh) - rough_thresh;
        specular = shadow_area * specular - rough_thresh; 
        specular_thresh = saturate((1.0f / specular_thresh) * specular);
        specular = (specular_thresh * - 2.0f + 3.0f) * pow(specular_thresh, 2.0f);
        specular = (specular_color * specular_color_global) * specular * ((specular_values.z * specular_intensity_global) * 0.35f);
        return specular;
    #else
        return (float3)0.00f;
    #endif
}

float extract_fov()
{
    return 2.0f * atan((1.0f / unity_CameraProjection[1][1]))* (180.0f / 3.14159265f);
}

float fov_range(float old_min, float old_max, float value)
{
    float new_value = (value - old_min) / (old_max - old_min);
    return new_value; 
}

// https://github.com/cnlohr/shadertrixx/blob/main/README.md#detecting-if-you-are-on-desktop-vr-camera-etc
bool isVR(){
    // USING_STEREO_MATRICES
    #if UNITY_SINGLE_PASS_STEREO
        return true;
    #else
        return false;
    #endif
}

float remap(float value, float old_min, float old_max, float new_min, float new_max)
{
    return new_min + (value - old_min) * (new_max - new_min) / (old_max - old_min);
}

void dissolve_vertex(vs_in i, out float4 dis_pos, out float4 dis_uv)
{
    #if defined(can_dissolve)
        // Get the appropriate UV coordinates based on the dissolve UV setting
        float2 dissolveUV = lerp(i.uv_0, i.uv_1, _DissolveUV);
        
        // Calculate the final UV coordinates for both dissolve and distortion effects
        dis_uv = float4(
            dissolveUV.xy * _DissolveST.xy + _DissolveST.zw,     // Dissolve UVs
            dissolveUV.xy * _DistortionST.xy + _DistortionST.zw  // Distortion UVs
        );
        
        // Store the original UV x-coordinate
        dis_pos.x = dissolveUV.x;
        
        // Transform vertex position to world space
        float4 ws_pos = mul(unity_ObjectToWorld, i.vertex);
        
        // Choose between local and world space position based on mask setting
        float4 dissolvePos = lerp(i.vertex, ws_pos, _DissolvePosMaskWorldON);
        
        // Position mask calculation
        float3 posToLight = dissolvePos.xyz - _ES_EffCustomLightPosition.xyz;
        float3 adjustedPos = dissolvePos.xyz;
        
        // Apply global position adjustment if enabled
        if (_DissolvePosMaskGlobalOn) {
            adjustedPos = dissolvePos.xyz - posToLight;
        }
        
        // Apply root offset
        adjustedPos -= _DissolvePosMaskRootOffset.xyz;
        
        // Calculate light direction
        float3 lightOffset = _ES_EffCustomLightPosition.xyz - unity_ObjectToWorld[3].xyz;
        float3 maskPos = _DissolvePosMaskWorldON ? _DissolvePosMaskPos.xyz - unity_ObjectToWorld[3].xyz : _DissolvePosMaskPos.xyz;
        float3 lightDir = lightOffset - maskPos;
        
        // Apply global adjustment to light direction if enabled
        if (_DissolvePosMaskGlobalOn) {
            lightDir = maskPos;
        }
        
        // Normalize light direction
        float lightDirLength = length(lightDir);
        float3 normalizedLightDir = lightDir / lightDirLength;
        
        // Check if light direction is valid
        bool isValidLightDir = (dot(abs(lightDir), float3(1.0, 1.0, 1.0)) >= 0.001);
        
        // Calculate dot product for position masking
        float dotProduct = dot(normalizedLightDir, adjustedPos);
        
        // Calculate mask threshold and range
        float maskRadius = max(_DissolvePosMaskPos.w, 0.01);
        float maskValue = (abs(dotProduct) + maskRadius) / (2.0 * maskRadius);
        
        // Apply flip if enabled
        if (_DissolvePosMaskFilpOn) {
            maskValue = 1.0 - maskValue;
        }
        
        // Apply mask settings and clamp result
        float finalMask = clamp(maskValue + (1.0 - _DissolvePosMaskOn), 0.0, 1.0);
        
        // Store the calculated mask value or 1.0 if light direction is invalid
        dis_pos.y = isValidLightDir ? finalMask : 1.0;
        dis_pos.zw = float2(0.0, 0.0);
    #endif
}

void dissolve_vertex_sdw(vs_in i, out float4 dis_pos, out float4 dis_uv)
{
    #if defined(can_dissolve)
        // Get the appropriate UV coordinates based on the dissolve UV setting
        float2 dissolveUV = lerp(i.uv_0, i.uv_1, _DissolveUV);
        
        // Calculate the final UV coordinates for both dissolve and distortion effects
        dis_uv = float4(
            dissolveUV.xy * _DissolveST.xy + _DissolveST.zw,     // Dissolve UVs
            dissolveUV.xy * _DistortionST.xy + _DistortionST.zw  // Distortion UVs
        );
        
        // Store the original UV x-coordinate
        dis_pos.x = dissolveUV.x;
        
        // Transform vertex position to world space
        float4 ws_pos = mul(unity_ObjectToWorld, i.vertex);
        
        // Choose between local and world space position based on mask setting
        float4 dissolvePos = lerp(i.vertex, ws_pos, _DissolvePosMaskWorldON);
        
        // Position mask calculation
        float3 posToLight = dissolvePos.xyz - _ES_EffCustomLightPosition.xyz;
        float3 adjustedPos = dissolvePos.xyz;
        
        // Apply global position adjustment if enabled
        if (_DissolvePosMaskGlobalOn) {
            adjustedPos = dissolvePos.xyz - posToLight;
        }
        
        // Apply root offset
        adjustedPos -= _DissolvePosMaskRootOffset.xyz;
        
        // Calculate light direction
        float3 lightOffset = _ES_EffCustomLightPosition.xyz - unity_ObjectToWorld[3].xyz;
        float3 maskPos = _DissolvePosMaskWorldON ? _DissolvePosMaskPos.xyz - unity_ObjectToWorld[3].xyz : _DissolvePosMaskPos.xyz;
        float3 lightDir = lightOffset - maskPos;
        
        // Apply global adjustment to light direction if enabled
        if (_DissolvePosMaskGlobalOn) {
            lightDir = maskPos;
        }
        
        // Normalize light direction
        float lightDirLength = length(lightDir);
        float3 normalizedLightDir = lightDir / lightDirLength;
        
        // Check if light direction is valid
        bool isValidLightDir = (dot(abs(lightDir), float3(1.0, 1.0, 1.0)) >= 0.001);
        
        // Calculate dot product for position masking
        float dotProduct = dot(normalizedLightDir, adjustedPos);
        
        // Calculate mask threshold and range
        float maskRadius = max(_DissolvePosMaskPos.w, 0.01);
        float maskValue = (abs(dotProduct) + maskRadius) / (2.0 * maskRadius);
        
        // Apply flip if enabled
        if (_DissolvePosMaskFilpOn) {
            maskValue = 1.0 - maskValue;
        }
        
        // Apply mask settings and clamp result
        float finalMask = clamp(maskValue + (1.0 - _DissolvePosMaskOn), 0.0, 1.0);
        
        // Store the calculated mask value or 1.0 if light direction is invalid
        dis_pos.y = isValidLightDir ? finalMask : 1.0;
        dis_pos.zw = float2(0.0, 0.0);
    #endif
}

void dissolve_clip(in float4 ws_pos, in float4 dis_pos, in float4 dis_uv, in float2 uv)
{
    #if defined(can_dissolve)
        float rate = _DissolveRate;
        if(_InvertRate) rate = 1.0 - rate;
        if(_DissolveUseDirection)
        {
            float3 dissolvePos = ws_pos.xyz + (float3)1.99999999e-06;
            dissolvePos.xyz = dissolvePos.xyz + (-_DissolveCenter.xyz);
            dissolvePos = dot(dissolvePos.xyz, _DissolveDiretcionXYZ.xyz);

            float test = 0.0f < dissolvePos ? 2 : int(0);
            if(test == 0) discard;
            // clip(dissolvePos);
        }
        else
        {
            float2 dissolveUV = dis_uv.zw + (float2)3.00000011e-06;
            dissolveUV = _DissolveUVSpeed.zw * _Time.yy + dissolveUV.xy;

            float2 dissolveMap = _DissolveMap.Sample(sampler_linear_repeat, dissolveUV);
            dissolveMap.xy = -(dissolveMap + - 0.5f) * (float2)(_DissolveDistortionIntensity) + dis_uv.xy;

            dissolveUV = _DissolveUVSpeed.xy * _Time.yy + dissolveMap.xy;
            dissolveMap = _DissolveMap.Sample(sampler_linear_repeat, dissolveUV).zz + _DissolveMapAdd;

            float4 dissolveMask = _DissolveMask.Sample(sampler_linear_repeat, uv);

            dissolveMask.x = dot(dissolveMask, _DissolveComponent);

            float dissolve = (-dis_pos.x) + _DissoveDirecMask;
            dissolve.x = min(abs(dissolve.x), 1.0);

            dissolve.x = dissolve.x * dissolveMap;
            dissolve.x = dissolveMask.x * dissolve.x;
            dissolve.x = dissolve.x * dis_pos.y;
            dissolve.x = dissolve.x * 1.00999999 + -0.00999999978;
            dissolve.x = dissolve.x + (-rate);
            dissolve.x = dissolve.x + 1.0;
            dissolve.x = floor(dissolve.x);
            dissolve.x = max(dissolve.x, 0.0);

            if((int)dissolve.x == 0) discard;
        }
    #endif
}

void dissolve_color(float4 ws_pos, float4 dis_pos, float4 dis_uv, float2 uv,in float4 diffuse, inout float4 color)
{
    #if defined(can_dissolve)
        float rate = _DissolveRate;
        if(_InvertRate) rate = 1.0 - rate;
        if(_DissolveUseDirection)
        {
            // Calculate vector from dissolve center to world position
            float3 directionVector = ws_pos.xyz - _DissolveCenter.xyz;
            
            // Project vector onto the dissolve direction
            float projectionValue = dot(directionVector, _DissolveDiretcionXYZ.xyz);
            
            // Discard fragments that are behind the dissolve direction
            if (projectionValue <= 0.0) {
                discard;
            }
        }
        else
        {
            float dissolveDirMask =  (-dis_pos.x) + _DissoveDirecMask;
            dissolveDirMask = min(abs(dissolveDirMask), 1.0);

            float2 dissolveUV = _DissolveUVSpeed.zw * _Time.yy + dis_uv.zw;
            float2 dissolveMap = _DissolveMap.Sample(sampler_linear_repeat, dissolveUV).xy;
            dissolveMap = -(dissolveMap + -0.5f) * _DissolveDistortionIntensity.xx + dis_uv.xy;
            dissolveMap = _DissolveUVSpeed.xy * _Time.yy + dissolveMap;

            dissolveMap.x = _DissolveMap.Sample(sampler_linear_repeat, dissolveMap).z + _DissolveMapAdd;
            float4 dissolveMask = _DissolveMask.Sample(sampler_linear_repeat, uv);
            dissolveMask.x = dot(dissolveMask, _DissolveComponent);
            dissolveDirMask = dissolveDirMask * dissolveMap;
            dissolveDirMask = dissolveMask.x * dissolveDirMask;
            dissolveDirMask = dissolveDirMask * dis_pos.y;
            dissolveDirMask = dissolveDirMask * 1.00999999 + -0.00999999978;
            
            // Calculate dissolve outline boundaries
            float outlineOuter = rate + _DissolveOutlineSize1;
            float outlineInner = outlineOuter - _DissolveOutlineSize2;
            
            // Calculate how far the current fragment is from each boundary
            float2 distanceFromBoundaries = float2(
                dissolveDirMask - outlineOuter,
                dissolveDirMask - outlineInner
            );
            
            // Apply smoothstep for soft transitions
            float2 smoothStepFactors = _DissolveOutlineSmoothStep.xy + 0.001f;
            float2 smoothStepInverse = 1.0f / smoothStepFactors;
            
            // Calculate transition factors
            float2 transitionFactors = smoothStepInverse * distanceFromBoundaries;
            transitionFactors = clamp(transitionFactors, 0.0, 1.0);
            
            // Calculate base color for dissolve effect
            float3 dissolveBaseColor = diffuse.xyz * dissolveMap.x + _DissolveOutlineOffset;
            
            // Calculate inner and outer outline colors
            float3 outerOutlineColor = dissolveBaseColor * _DissolveOutlineColor1.xyz;
            float3 innerOutlineColor = dissolveBaseColor * _DissolveOutlineColor2.xyz;
            
            // Blend between inner and outer outline colors
            float3 blendedOutlineColor = lerp(outerOutlineColor, innerOutlineColor, transitionFactors.y);
            
            // Calculate alpha factor for blending with original color
            float alphaFactor = transitionFactors.x + 1.0 - _DissolveOutlineColor1.w;
            alphaFactor = clamp(alphaFactor, 0.0, 1.0);
            
            // Blend between original color and outline color
            float3 finalColor = lerp(blendedOutlineColor, color.xyz * 1.0f, alphaFactor);
            
            // Apply color correction (tone mapping approximation)
            float3 correctionNumerator = finalColor * 278.508514f + 10.7771997f;
            correctionNumerator = correctionNumerator * finalColor;
            
            float3 correctionDenominator = finalColor * 298.604492f + 88.7121964f;
            correctionDenominator = finalColor * correctionDenominator + 80.6889038f;
            
            float3 correctedColor = correctionNumerator / correctionDenominator;
            
            // Final blend based on alpha factor
            color.xyz = lerp(correctedColor, finalColor, alphaFactor);

        }
    #endif
}

void heightlightlerp(float4 pos, inout float4 color)
{
    float height = pos.y + (-_CharaWorldSpaceOffset);

    // Height-based color lerping variables
    float4 u_xlat16_30;
    float u_xlat75;
    float u_xlat76;
    float4 u_xlat2;
    float u_xlat27;
    float4 u_xlat16_7;

    // Use the world space height
    u_xlat75 = height;

    // Bottom region calculation
    float bottomThreshold = max(_ES_HeightLerpBottom, 0.001);
    float bottomFactor = u_xlat75 / bottomThreshold;
    bottomFactor = clamp(bottomFactor, 0.0, 1.0);
    
    // Smooth step for bottom region
    float bottomSmoothStep = (bottomFactor * -2.0) + 3.0;
    bottomFactor = bottomFactor * bottomFactor;
    float bottomBlend = 1.0 - (bottomSmoothStep * bottomFactor);
    
    // Top region calculation
    float topFactor = (u_xlat75 - _ES_HeightLerpTop) * 2.0;
    topFactor = clamp(topFactor, 0.0, 1.0);
    
    // Smooth step for top region
    float topSmoothStep = (topFactor * -2.0) + 3.0;
    topFactor = topFactor * topFactor;
    float topBlend = topFactor * topSmoothStep;
    
    // Middle region calculation
    float middleBlend = 1.0 - bottomBlend;
    middleBlend = middleBlend - (topSmoothStep * topFactor);
    middleBlend = clamp(middleBlend, 0.0, 1.0);
    
    // Blend the three colors based on height regions
    float3 bottomColor = bottomBlend * _ES_HeightLerpBottomColor.xyz * _ES_HeightLerpBottomColor.www;
    float3 middleColor = middleBlend * _ES_HeightLerpMiddleColor.xyz * _ES_HeightLerpMiddleColor.www;
    float3 topColor = topBlend * _ES_HeightLerpTopColor.xyz * _ES_HeightLerpTopColor.www;
    
    // Combine all three color regions
    float3 finalColor = bottomColor + middleColor + topColor;
    finalColor = clamp(finalColor, 0.0, 1.0);
    
    // Apply to the input color
    color.xyz = finalColor * color.xyz;
}

void swirl_dissolve(vs_out i, inout float4 output)
{

    // float2 mask_uv = i.uv.zw;
    // float4 mask_tex = _MaskTex.Sample(sampler_linear_repeat, mask_uv);
    // // (u_xlat16_0 = texture(_MaskTex, vs_TEXCOORD0.zw));
    // // (u_xlatb1 = (vec4(0.0, 0.0, 0.0, 0.0) != vec4(_MaskON)));
    // // (u_xlat16_2.xyz = _MaskChannel.xyz);
    // // (u_xlat16_2.w = _MaskChannelA);
    // // (u_xlat16_2.x = dot(u_xlat16_0, u_xlat16_2));
    // // (u_xlat8 = (vs_TEXCOORD0.w * vs_TEXCOORD0.z));
    // float mask = _MaskON ? dot(mask_tex, float4(_MaskChannel.xyz, _MaskChannelA)) : mask_uv.x * mask_uv.y;
    // // (u_xlat16_2.x = (_MaskTransparency * _MaskON));
    // // (u_xlatb8 = (vec4(0.0, 0.0, 0.0, 0.0) != vec4(u_xlat16_2.x)));
    // // (u_xlat16_2 = _MaskTransparencyChannel);
    // // (u_xlat16_2 = clamp(u_xlat16_2, 0.0, 1.0));
    // // (u_xlat16_2.x = dot(u_xlat16_0, u_xlat16_2));
    // // (u_xlat16_2.x = ((u_xlatb8) ? (u_xlat16_2.x) : (1.0)));
    // float trans_mask = _MaskTransparency * _MaskON ? dot(mask_tex, float4(saturate(_MaskTransparencyChannel))) : 1.0f;
    // // (u_xlat0.x = texture(_DisTex, vs_TEXCOORD1.xy).x);
    // // (u_xlat16_7 = texture(_DisTex, vs_TEXCOORD1.zw).y);
    // float main_dis = _DisTex.Sample(sampler_linear_repeat, i.uv_1.xy).x;
    // float mask_dis = _DisTex.Sample(sampler_linear_repeat, i.uv_1.zw).y;
    // (u_xlat16_9.x = (u_xlat16_7 + u_xlat0.x));
    // (u_xlat7.x = (u_xlat16_9.x + _DisRSpeed.z));
    // (u_xlat7.x = (u_xlat7.x * u_xlat1.x));
    // (u_xlat1 = texture(_MainTex, vs_TEXCOORD0.xy));
    // (u_xlat16_3.xyz = (u_xlat1.xyz + _MainChannel.www));
    // (u_xlat16_3.xyz = clamp(u_xlat16_3.xyz, 0.0, 1.0));
    // (u_xlat14 = dot(u_xlat1.xyz, _MainChannel.xyz));
    // (u_xlat16_3.w = (u_xlat14 * u_xlat1.w));
    // (u_xlat16_3.w = clamp(u_xlat16_3.w, 0.0, 1.0));
    // (u_xlatb14.xy = equal(ivec4(ivec4(_CL, _CL, _CL, _CL)), ivec4(1, 2, 1, 2)).xy);
    // (u_xlat16_9.x = dot(_MainChannelRGB, u_xlat1));
    // (u_xlat4 = dot(_MainChannel, u_xlat1));
    // (u_xlat16_4.w = (u_xlat1.w * u_xlat4));
    // (u_xlat16_4.w = clamp(u_xlat16_4.w, 0.0, 1.0));
    // (u_xlat16_4.xyz = u_xlat16_9.xxx);
    // (u_xlat16_4.xyz = clamp(u_xlat16_4.xyz, 0.0, 1.0));
    // (u_xlat16_1 = ((u_xlatb14.y) ? (u_xlat16_4) : (u_xlat1)));
    // (u_xlat16_1 = ((u_xlatb14.x) ? (u_xlat16_3) : (u_xlat16_1)));
    // (u_xlat16_3.y = (vs_COLOR0.w + _DisStep.y));
    // (u_xlat16_3.z = (u_xlat16_3.y + _SmoothStep.w));
    // (u_xlat16_3.x = vs_COLOR0.w);
    // (u_xlat16_9.xyz = ((u_xlat7.xxx * vec3(0.5, 0.5, 0.5)) + (-u_xlat16_3.xyz)));
    // (u_xlat16_3.xyz = (vec3(1.0, 1.0, 1.0) / _SmoothStep.xyz));
    // (u_xlat16_9.xyz = (u_xlat16_9.xyz * u_xlat16_3.xyz));
    // (u_xlat16_9.xyz = clamp(u_xlat16_9.xyz, 0.0, 1.0));
    // (u_xlat16_23 = (((_Mid != 0)) ? (u_xlat16_9.z) : (0.0)));
    // (u_xlat16_3.x = (((_Mid != 0)) ? (u_xlat0.x) : (1.0)));
    // (u_xlat0.xyz = ((u_xlat16_1.xyz * u_xlat16_3.xxx) + _MainSpeed.www));
    // (u_xlat16_3.xyz = (u_xlat0.xyz * _OutSideColor.xyz));
    // (u_xlat16_5.xyz = ((_MidColor.xyz * u_xlat0.xyz) + (-u_xlat16_3.xyz)));
    // (u_xlat16_3.xyz = ((vec3(u_xlat16_23) * u_xlat16_5.xyz) + u_xlat16_3.xyz));
    // (u_xlat16_5.xyz = ((vs_COLOR0.xyz * u_xlat16_1.xyz) + (-u_xlat16_3.xyz)));
    // (u_xlat16_3.xyz = ((u_xlat16_9.yyy * u_xlat16_5.xyz) + u_xlat16_3.xyz));
    // (u_xlat16_9.x = (u_xlat16_1.w * u_xlat16_9.x));
    // (u_xlat16_9.x = (u_xlat16_9.x * _MidColor.w));
    // (u_xlat16_2.x = (u_xlat16_2.x * u_xlat16_9.x));
    // (u_xlat16_2.x = (u_xlat16_2.x * 10.0));
    // (u_xlat0.xyz = (u_xlat16_3.xyz * _MainSpeed.zzz));
    // (u_xlatb21 = (0.5 < _OverrideAlphaByVertexColor));
    // (u_xlat6 = (u_xlat16_2.x * vs_TEXCOORD4.w));
    // (u_xlat16_2.x = ((u_xlatb21) ? (u_xlat6) : (u_xlat16_2.x)));
    // (u_xlat16_2.x = clamp(u_xlat16_2.x, 0.0, 1.0));
    // u_xlat16_9.xy = 1;
    // (u_xlat16_3.xyz = (u_xlat0.xyz * u_xlat16_9.xxx));
    // (u_xlat16_9.xyz = (u_xlat16_9.yyy * u_xlat16_3.xyz));
    // (u_xlat16_9.xyz = (u_xlat16_9.xyz * vec3(vec3(_ES_EffectIntensityScale, _ES_EffectIntensityScale, _ES_EffectIntensityScale))));
    // (u_xlat16_3.x = max(u_xlat16_9.y, u_xlat16_9.x));
    // (u_xlat16_3.x = max(u_xlat16_9.z, u_xlat16_3.x));
    // (u_xlat16_3.x = (u_xlat16_2.x * u_xlat16_3.x));
    // (u_xlat0.x = max(u_xlat16_3.x, 0.0099999998));
    // (u_xlat7.xyz = (u_xlat16_9.xyz / u_xlat0.xxx));
    // (u_xlatb6 = (_ES_EP_EffectParticleBottom < u_xlat0.x));
    // (u_xlat13 = (_ES_EP_EffectParticleTop + (-_ES_EP_EffectParticleBottom)));
    // (u_xlat20 = (u_xlat0.x + (-_ES_EP_EffectParticleBottom)));
    // (u_xlat13 = (1.0 / u_xlat13));
    // (u_xlat13 = (u_xlat13 * u_xlat20));
    // (u_xlat13 = clamp(u_xlat13, 0.0, 1.0));
    // (u_xlat20 = ((u_xlat13 * -2.0) + 3.0));
    // (u_xlat13 = (u_xlat13 * u_xlat13));
    // (u_xlat13 = (u_xlat13 * u_xlat20));
    // (u_xlat16_9.x = (_ES_EP_EffectParticleTop + (-_ES_EP_EffectParticleBottom)));
    // (u_xlat16_9.x = ((u_xlat13 * u_xlat16_9.x) + _ES_EP_EffectParticleBottom));
    // (u_xlat13 = ((-u_xlat0.x) + u_xlat16_9.x));
    // (u_xlat13 = ((_ES_EP_EffectParticle * u_xlat13) + u_xlat0.x));
    // (u_xlat0.x = ((u_xlatb6) ? (u_xlat13) : (u_xlat0.x)));
    // (u_xlat0.xyz = (u_xlat0.xxx * u_xlat7.xyz));
    // (SV_Target0.xyz = u_xlat0.xyz);
    // (SV_Target0.w = u_xlat16_2.x);
}

float4 starry_sky(float4 color, float4 diffuse, float2 uv)
{
    if(_StarrySky)
    {
        float2 sky_uv = uv * _SkyTex_ST.xy + _SkyTex_ST.zw;
        float2 mask_uv = uv * _SkyMask_ST.xy + _SkyMask_ST.zw;
        float3 sky_tex = _SkyTex.Sample(sampler_linear_repeat, sky_uv);
        float sky_mask = _SkyMask.Sample(sampler_linear_repeat, mask_uv).x;

        float3 colored = diffuse * color;
        diffuse.xyz = -diffuse * color + sky_tex;
        diffuse.xyz = (sky_mask + _SkyRange) * diffuse + colored;
        return diffuse;
    }
    else
    {
        return diffuse * color;
    }
}

float4 starry_cloak(float4 sspos, float3 view, float2 uv, float4 position, float3 tangents, float4 out_color)
{
    float4 output;

    float2 star_uv = sspos.xy/sspos.ww;

    star_uv = length(view) * (star_uv + (float2)-0.5f) * _SkyStarDepthScale;
    star_uv = star_uv * _SkyStarTex_ST.xy + _SkyStarTex_ST.zw;
    star_uv = _Time.yy * _SkyStarSpeed.xy + star_uv;
    float3 skystar = (_SkyStarTex.Sample(sampler_linear_repeat, star_uv).xxx * _SkyStarColor) * _SkyStarTexScale.x;
    // output.xyz = output.xyz * ;

    float2 skymask = ((_SkyMask.Sample(sampler_linear_repeat, uv * _SkyMask_ST.xy + _SkyMask_ST.zw).xy + _SkyRange));

    float2 mask_uv = uv.xy * _SkyStarMaskTex_ST.xy + _SkyStarMaskTex_ST.zw;
    mask_uv = _Time.yy * _SkyStarMaskTexSpeed.xx + mask_uv;
    float mask = _SkyStarMaskTex.Sample(sampler_linear_repeat, mask_uv).x * _SkyStarMaskTexScale;
    // output.xyz = output.xyz * mask;

    float4 pos = mul(UNITY_MATRIX_V, position);
    pos.xyz = pos / float3(_OSScale, _OSScale.xx * 0.5.xx);

    float3 spos = smoothstep(1.0f, -1.0f, position.yzx / (_OSScale * float3(0.5f, 0.5f, 1.0f)));

    float2 pos_star_uv = spos.yz * 20.0f;

    float star_tex_w = _SkyStarTex.Sample(sampler_linear_repeat, pos_star_uv).w;
    float2 star_tex_yz = _SkyStarTex.Sample(sampler_linear_repeat, uv).yz;

    float star_density = -star_tex_yz.x * _StarDensity + star_tex_w;
    
    star_density = saturate(star_density / (-_StarDensity  + 1.0f));

    // Transform position coordinates to star texture UV space
    float4 starTexCoords = spos.xzyz * _SkyStarTex_ST.xyxy + _SkyStarTex_ST.zwzw;
    
    // Sample star texture at two different coordinates
    float starSample1 = _SkyStarTex.Sample(sampler_linear_repeat, starTexCoords.xy).x;
    float starSample2 = _SkyStarTex.Sample(sampler_linear_repeat, starTexCoords.zw).x;
    
    // Blend between the two star samples based on the Y component of star_tex_yz
    float star_blend = lerp(starSample2, starSample1, star_tex_yz.y);

    float3 stars = star_blend * (star_density * _SkyStarColor) * _SkyStarTexScale;

    tangents = normalize(tangents);

    // tangents = normalize(mul((float3x3)unity_MatrixV, tangents));

    float tdotv = dot(tangents, view);

    float test = pow(1.0f - tdotv, 4.0f);

    // Calculate fresnel effect parameters
    float halfOffset = 0.5;
    float fresnelSmoothWithOffset = _SkyFresnelSmooth + halfOffset;
    
    // Calculate adjusted ranges for smooth interpolation
    float2 fresnelRanges = float2(halfOffset, 1.0) - float2(_SkyFresnelSmooth, _SkyFresnelBaise);
    
    // Apply test factor (view-tangent dot product) to calculate fresnel intensity
    float fresnelIntensity = fresnelRanges.y * test + _SkyFresnelBaise;
    
    // Calculate normalization factor for smooth interpolation
    float normalizationFactor = 1.0 / (fresnelSmoothWithOffset - fresnelRanges.x);
    
    // Normalize the fresnel intensity for interpolation
    float normalizedIntensity = (fresnelIntensity - fresnelRanges.x) * normalizationFactor;
    normalizedIntensity = clamp(normalizedIntensity, 0.0, 1.0);
    
    // Apply smoothstep function (t²(3-2t)) for smooth transition
    float smoothStepFactor = normalizedIntensity * -2.0 + 3.0;
    float finalFresnel = normalizedIntensity * normalizedIntensity * smoothStepFactor;
    
    // Calculate final star fresnel color
    float3 star_fresnel = (finalFresnel * _SkyFresnelScale) * _SkyFresnelColor;
    
    float3 something = skystar.xyz * mask;
    something = something * skymask.x;

    output.xyz = (stars * skymask.x) * mask + -something;
    output.xyz = _StarMode * output.xyz + something;
    output.xyz = star_fresnel * skymask.y + output.xyz;
    
    if(_StarsAreDiffuse) out_color.xyz = lerp(out_color.xyz, 0.0f, skymask.x);
    output.xyz = output.xyz + out_color.xyz;

    // output.xyz = star_fresnel;
    output.w = out_color.w;
    return output;
}

float3 DecodeLightProbe( float3 N )
{
    return ShadeSH9(float4(N,1));
}

void matcap_color(in float3 normal, in float3 view, in float shadow, in float mask, in float2 uv, inout float specular_int, inout float4 specular_color, inout float3 color)
{
    #if defined(use_matcap)

    float2 sphere_uv = mul(normal, (float3x3)UNITY_MATRIX_I_V ).xy;
        sphere_uv = sphere_uv * 0.5f + 0.5f;  

        float3 cube_uv = reflect(view, normal);

        float4 matcap = (_UseCubeMap) ? _CubeMap.Sample(sampler_linear_repeat, cube_uv) : _MatCapTex.Sample(sampler_linear_repeat, sphere_uv);
        float matcap_mask = _MatCapMaskTex.Sample(sampler_linear_repeat, uv).x;

        float matcap_area = _OnlyMask ? matcap_mask : mask.x * matcap_mask;

        float matcap_strength = ((saturate(((shadow.x * 5.0) + -4.0)) * (((-_MatCapStrengthInShadow) * _MatCapStrength) + _MatCapStrength)) + (_MatCapStrength * _MatCapStrengthInShadow));
        float3 matcap_color = (matcap_strength * matcap.xyz); // mask * matcap * matcap_color
        matcap_color.xyz = (matcap_color.xyz * _MatCapColor.xyz);
        matcap_color.xyz = (matcap_area * matcap_color.xyz); // * spec color
        matcap_color.xyz = (specular_color.xyz * matcap_color.xyz); // * spec intensity
        matcap_color.xyz = (specular_int * matcap_color.xyz);

        float matcap_ceil = ((matcap_mask * mask.x) + -0.0099999998);
        matcap_ceil = clamp(matcap_ceil, 0.0, 1.0);
        matcap_ceil = ceil(matcap_ceil);

        matcap.xyz = (matcap_color.xyz * matcap_ceil);
        if(_ReplaceColor) color = lerp(color, matcap, matcap_area);
        else color = color * 1 /*this would be the rim shadow*/ + matcap;
    #endif
}

bool hide_parts(float2 vcol)
{
    // turn vertex colors into rgb 
    float2 rgb_vcol = vcol.yx * 256.f;
    int2 ivcol = (int2)rgb_vcol & (uint2)_ShowPartID; 

    bool check1 = 0 < _HideCharaParts; 
    bool check2 = 0 < _HideNPCParts; 

    int check3 = (check1) ? ivcol.x : (check2) ? ivcol.y : 1;

    return 0 < check3;
}

void custom_coloring(inout float4 color, in float alpha_tex)
{
    // Determine ID based on alpha texture
    float id = (0.95f < alpha_tex) ? 1.0 : alpha_tex;
    
    // Check if ID is less than thresholds
    float2 thresholds = float2(0.95f, 1.0) > id.xx;
    
    // Calculate floor ID for color selection
    float floor_id = floor(id.x * 8.0);
    
    // Initialize variables
    float4 temp;
    temp.x = (thresholds.y) ? 0.0 : 1.0;
    
    // Start with base color
    float4 base_color = color.xyzx * temp.xxxx;
    
    // Define color pairs for interpolation
    float4 color_0 = lerp(_CustomColor1.xyzx, _CustomColor0.xyzx, color.xxxx);
    float4 color_1 = lerp(_CustomColor3.xyzx, _CustomColor2.xyzx, color.xxxx);
    float4 color_2 = lerp(_CustomColor5.xyzx, _CustomColor4.xyzx, color.xxxx);
    float4 color_3 = lerp(_CustomColor7.xyzx, _CustomColor6.xyzx, color.xxxx);
    float4 color_4 = lerp(_CustomColor9.xyzx, _CustomColor8.xyzx, color.xxxx);
    float4 color_5 = lerp(_CustomColor11.xyzx, _CustomColor10.xyzx, color.xxxx);
    float4 color_6 = lerp(_CustomColor13.xyzx, _CustomColor12.xyzx, color.xxxx);
    float4 color_skin = lerp(_CustomSkinColor1.xyzx, _CustomSkinColor.xyzx, color.xxxx);
    
    // Check which color set to use based on floor_id (0-3)
    float4 id_check = float4(0.0, 1.0, 2.0, 3.0) == floor_id.xxxx;

    // Apply skin color if floor_id is 0
    color_skin = (-color.xyzx) * temp.xxxx + color_skin;
    base_color = id_check.xxxx * color_skin + base_color;
    
    // Apply custom colors based on floor_id (1-3)
    base_color = lerp(base_color, color_0, id_check.yyyy); 
    base_color = lerp(base_color, color_1, id_check.zzzz);  
    base_color = lerp(base_color, color_2, id_check.wwww);  
    
    // Check which color set to use based on floor_id (4-7)
    id_check = float4(4.0, 5.0, 6.0, 7.0) == floor_id.xxxx;
    
    // Apply custom colors based on floor_id (4-7)
    base_color = lerp(base_color, color_3, id_check.xxxx);    
    base_color = lerp(base_color, color_4, id_check.yyyy);
    base_color = lerp(base_color, color_5, id_check.zzzz);
    base_color = lerp(base_color, color_6, id_check.wwww);
    
    // Set final color components
    float original_tex_check = (thresholds.y) ? color.z : float(1.0);
    
    // Apply the calculated color
    color.xyz = lerp(color.xyz, base_color.xyz, original_tex_check);
}

void increase_bloom(float4 bloom_color, float bloom_intensity, inout float3 out_color)
{
    // Calculate bloom effect based on bloom color and intensity
    float3 bloom_effect = bloom_color.xyz * bloom_intensity;
    
    // Apply bloom effect to the output color
    out_color.xyz = out_color.xyz * bloom_effect + out_color.xyz;
}

void LUT_2D(inout float3 color, in float4 lutParams)
{   
    #if defined(is_tonemapped)
    // Apply initial transformations to the color
    float3 adjustedColor = color.zxy * 5.55555582f + 0.0479959995f;
    adjustedColor = log2(adjustedColor);
    adjustedColor = adjustedColor * 0.0734997839f + 0.386036009f;
    adjustedColor = clamp(adjustedColor, 0.0, 1.0);

    // Calculate LUT coordinates
    float3 lutCoord = adjustedColor * lutParams.z;
    float xCoord = floor(lutCoord.x);
    
    // Calculate base and next LUT sample positions
    float2 lutSize = lutParams.xy * 0.5;
    float2 lutPos = lutCoord.yz * lutParams.xy + lutSize;
    float2 lutPos1 = float2(xCoord * lutParams.y + lutPos.x, lutPos.y);
    float2 lutPos2 = lutPos1 + float2(lutParams.y, 0);


    // Sample the LUT
    float3 sample1 = _Lut2DTex.Sample(sampler_linear_clamp, lutPos1).rgb;
    float3 sample2 = _Lut2DTex.Sample(sampler_linear_clamp, lutPos2).rgb;

    // Interpolate between the two 
    float lerpFactor = lutCoord.x - xCoord;
    float3 lutColor = lerp(sample1, sample2, lerpFactor);

    // Clamp the final color
    lutColor = saturate(lutColor);

    color = lutColor;
    #endif
}

float4 fake_reflection(float3 normal, float3 view, float2 skymask)
{
    float ndotv = dot(normal, view);
    float3 ref = normal * (ndotv + ndotv) - view;
    ref = normalize(ref);
    float ref_value = dot(normal, ref);
    
    // float refl = max(ref_value, 0.001);
    // refl = pow(refl, _ReflectionRoughness);
    // refl = smoothstep(_ReflectionThreshold, _ReflectionSoftness, refl);


    // Calculate reflection blend factor
    float blendBase = max(ref_value, 0.001f);
    float blendPow = pow(blendBase, _ReflectionRoughness);
    float blendThresh = blendPow - _ReflectionThreshold;
    float blendSoft = blendThresh / _ReflectionSoftness;
    blendSoft = clamp(blendSoft, 0.0, 1.0);
    float blendSmooth = blendSoft * blendSoft * (-2.0f * blendSoft + 3.0f);

    // Calculate reversed blend factor
    float reversedBase = (ref_value - _ReflectionBlendThreshold) * 20.0f;
    float reversedBlend = clamp(reversedBase, 0.0, 1.0);
    float reversedSmooth = reversedBlend * reversedBlend * (-2.0f * reversedBlend + 3.0f);

    // Calculate reversed threshold blend
    float reversedThreshBase = ref_value - (_ReflectionReversedThreshold + 0.05f);
    float reversedThreshRange = _ReflectionReversedThreshold - (_ReflectionReversedThreshold + 0.05f);
    float reversedThreshNorm = reversedThreshBase / reversedThreshRange;
    reversedThreshNorm = clamp(reversedThreshNorm, 0.0, 1.0);
    float reversedThreshSmooth = reversedThreshNorm * reversedThreshNorm * (-2.0f * reversedThreshNorm + 3.0f);

    // Blend colors
    float3 blendColor = reversedThreshSmooth * _ReflectionBlendColor.xyz * _FakeRefBlendIntensity;
    float3 reversedBlendColor = reversedSmooth * _ReflectionBlendColor.xyz * _FakeRefBlendIntensity;
    float3 reflectionColor = blendSmooth * _ReflectionColor.xyz * _FakeRefAddIntensity;

    // Final reflection value
    // float refl = dot(blendColor, float3(1.0, 1.0, 1.0)); // If you want a scalar, otherwise use blendColor, reversedBlendColor, reflectionColor as needed

    reflectionColor.xyz = skymask.xxx * reflectionColor.xyz;
    reversedBlendColor.xyz = reversedBlendColor.xyz * skymask.xxx + reflectionColor.xyz;
    float3 output = blendColor.xyz * skymask.yyy + reversedBlendColor.xyz;

    return float4(output, 1.0);
}