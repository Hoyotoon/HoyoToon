#ifndef DEPTH_SHADER
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
    float height = pos.y + (-_CharaWorldSpaceOffset.y);

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

float4 starry_cloak(float4 sspos, float3 view, float2 uv, float4 position, float3 tangents, float4 out_color)
{
    #if defined(is_baseshader)
    float4 output;

    float2 star_uv = sspos.xy/sspos.ww;

    star_uv = length(view) * (star_uv + (float2)-0.5f) * _SkyStarDepthScale;
    star_uv = star_uv * _SkyStarTex_ST.xy + _SkyStarTex_ST.zw;
    star_uv = _Time.yy * _SkyStarSpeed.xy + star_uv;
    float3 skystar = (_SkyStarTex.Sample(sampler_linear_repeat, star_uv).xxx * _SkyStarColor) * _SkyStarTexScale.x;
    // output.xyz = output.xyz * ;

    float2 skymask = saturate((_SkyMask.Sample(sampler_linear_repeat, uv * _SkyMask_ST.xy + _SkyMask_ST.zw).xy + _SkyRange));

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
    
    output.xyz = output.xyz + out_color.xyz;

    // output.xyz = star_fresnel;
    output.w = out_color.w;
    #else 
    float4 output = 1;
    #endif
    return output;
}

// 2D to 1D Pseudo-Random Number Generator
float hash21(float2 p)
{
    float n = dot(p, float2(12.9898005, 78.2330017));
    n = sin(n) * 43758.5469;
    return frac(n);
}

// 2D to 2D Pseudo-Random Number Generator
float2 hash22(float2 p)
{
    float n1 = hash21(p);
    float2 p2 = (float2)(n1) + p;
    float n2 = hash21(p2);
    return float2(n1, n2);
}

float3 GetGlintVector(float rand1, float rand2)
{
    float phi = rand1 * 6.28318024;
    float cos_theta_val = (-rand2) * 2.0 + 1.0;
    
    // Fast acos(x) approximation: abs(x)*-0.018729...+1.5707...
    float abs_cos_theta = abs(cos_theta_val);
    float acos_approx = abs_cos_theta * -0.0187292993 + 0.0742610022;
    acos_approx = acos_approx * abs_cos_theta + -0.212114394;
    acos_approx = acos_approx * abs_cos_theta + 1.57072878;
    
    float sqrt_term = sqrt(-abs_cos_theta + 1.0);
    float theta_part1 = acos_approx * sqrt_term;
    float theta_part2 = theta_part1 * -2.0 + 3.14159274;
    
    // Ternary: (cos_theta_val < -cos_theta_val) ? theta_part2 : 0.0
    float theta = (cos_theta_val < 0.0) ? theta_part2 : 0.0;
    theta = theta_part1 + theta;
    
    float sin_theta = sin(theta);
    float cos_theta = cos(theta);
    float sin_phi = sin(phi);
    float cos_phi = cos(phi);
    
    float3 v = float3(
        sin_theta * cos_phi,
        sin_theta * sin_phi,
        cos_theta
    );
    
    return normalize(v);
}
#endif
void dissolve_clip_world(in float3 ws_pos, out float dissolve_area, out float dis_out)
{
    float3 ws_dis = ws_pos + 0.000001f;
    ws_dis = ws_dis - _DissolveCenter.xyz;
    dissolve_area = dot(ws_dis, _DissolveDiretcionXYZ.xyz);
    int dis_clip = 0.0f < dissolve_area ? 2 : 0;
    if(dis_clip == 0) discard;
    dis_out = 0.000001f;
}

void dissolve_clip_uv(in float4 dissolve_uv, in float2 dissolve_pos, in float2 uv, out float dissolve_area, out float dis_out, out float map)
{
    dis_out = 0.000003f;
    float diss_x = min(abs((-dissolve_pos.x) + _DissoveDirecMask), 1.0);
    float2 dis_uv = _DissolveUVSpeed.zw * _Time.yy + (dissolve_uv.zw + 0.000003f);
    float2 dis_map_a = _DissolveMap.Sample(sampler_linear_repeat, dis_uv);
    
    dis_uv = dis_map_a - 0.5f;
    dis_uv = _DissolveUVSpeed.xy * _Time.yy + (-dis_uv * _DissolveDistortionIntensity + dissolve_uv.xy);

    float dis_map_b = _DissolveMap.Sample(sampler_linear_repeat, dis_uv).z;
    map = dis_map_b;

    float2 mask_uv = lerp(uv, dissolve_uv.xy, _DissolveMaskUVSet);
    float3 mask = _DissolveMask.Sample(sampler_linear_repeat, mask_uv.xy);
    mask.xyz = dot(mask, _DissolveComponent);

    dissolve_area = (((mask.x * (diss_x.x * (dis_map_b.x + _DissolveMapAdd))) * dissolve_pos.y) * 1.01f + -0.01f);
    diss_x = (dissolve_area + (-_DissolveRate)) + 1.0f; 
    diss_x = max(floor(diss_x), 0.0f);
    if((int)diss_x == 0) discard;
}

void dissolve_outline(inout float4 color, in float dissolve_area, in float map)
{
    float2 range = dissolve_area - ((_DissolveRate + _DissolveOutlineSize1) + (-_DissolveOutlineSize2));
    float2 smooth_inv = 1.0 / (_DissolveOutlineSmoothStep.xy + 0.001);
    float2 blend = saturate(range * smooth_inv);
    float3 base = color.xyz * map + _DissolveOutlineOffset;
    float3 color_a = base * _DissolveOutlineColor1.xyz;
    float3 color_diff = base * _DissolveOutlineColor2.xyz - color_a;
    float3 final = color_diff * blend.y + color_a;
    blend.x = blend.x + 1.0;
    blend.x = blend.x + (-_DissolveOutlineColor1.w);
    blend.x = saturate(blend.x);
    
    color.xyz = lerp(final, color.xyz, blend.x);
}