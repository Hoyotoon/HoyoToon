float2 offset_tiling(float2 uv, float4 st)
{
    return float2(uv.xy * st.xy + st.zw);
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

// https://github.com/cnlohr/shadertrixx/blob/main/README.md#detecting-if-you-are-on-desktop-vr-camera-etc
bool isVR(){
    // USING_STEREO_MATRICES
    #if UNITY_SINGLE_PASS_STEREO
        return true;
    #else
        return false;
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

    return material;
}

float shadow_rate(float ndotl, float lightmap_ao, float vertex_ao, float shadow_ramp)
{
    float shadow_ndotl  = (ndotl * 0.5f + 0.5f);
    float shadow_thresh = (lightmap_ao + lightmap_ao) * vertex_ao;
    float shadow_area   = min(1.0f, dot(shadow_ndotl.xx, shadow_thresh.xx));
    shadow_area = max(0.001f, shadow_area) * 0.85f + 0.15f;
    shadow_area = (shadow_area > shadow_ramp) ? 0.99f : shadow_area;
    return shadow_area;
}

float3 specular_base(float shadow_area, float ndoth, float lightmap_spec, float3 specular_color, float3 specular_values, float3 specular_color_global, float specular_intensity_global)
{
    float3 specular = ndoth;
    specular = pow(max(specular, 0.01f), specular_values.x);
    specular_values.y = max(specular_values.y, 0.001f);

    float specular_thresh = 1.0f - lightmap_spec;
    float rough_thresh = specular_thresh - specular_values.y;
    specular_thresh = (specular_values.y + specular_thresh) - rough_thresh;
    specular = shadow_area * specular - rough_thresh; 
    specular = smoothstep(specular_thresh, 1.f, specular) * (specular_color * specular_color_global) * (specular_values.z * specular_intensity_global);
    return specular;
}

void heightlightlerp(float4 pos, inout float4 color)
{
    float height = pos.y + (-_CharaWorldSpaceOffset);

    // Use the world space height
    float wsHeight = height;

    // Bottom region calculation
    float bottomThreshold = max(_ES_HeightLerpBottom, 0.001);
    float bottomFactor = wsHeight / bottomThreshold;
    bottomFactor = clamp(bottomFactor, 0.0, 1.0);
    
    // Smooth step for bottom region
    float bottomSmoothStep = (bottomFactor * -2.0) + 3.0;
    bottomFactor = bottomFactor * bottomFactor;
    float bottomBlend = 1.0 - (bottomSmoothStep * bottomFactor);
    
    // Top region calculation
    float topFactor = (wsHeight - _ES_HeightLerpTop) * 2.0;
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

void increase_bloom(float4 bloom_color, float bloom_intensity, inout float3 out_color)
{
    // Calculate bloom effect based on bloom color and intensity
    float3 bloom_effect = bloom_color.xyz * bloom_intensity;
    
    // Apply bloom effect to the output color
    out_color.xyz = out_color.xyz * bloom_effect + out_color.xyz;
}
