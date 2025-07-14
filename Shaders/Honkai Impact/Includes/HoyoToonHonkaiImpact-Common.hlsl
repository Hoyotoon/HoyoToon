float shadow_rate_face(float facemap, float facemap_mirror, float ao, float4 ws_pos)
{
    // head directions
    float3 head_forward = normalize(UnityObjectToWorldDir(_headForward.xyz));
    float3 head_right   = normalize(UnityObjectToWorldDir(_headRight.xyz));
    
    float2 light_dir = normalize(_WorldSpaceLightPos0.xz);
    #if defined(POINT) || defined(SPOT)
        light_dir = normalize(_WorldSpaceLightPos0.xyz - ws_pos.xyz);
    #endif
    // get dot products of each head direction and the lightDir
    half FdotL = dot(light_dir, head_forward.xz);
    half RdotL = dot(light_dir, head_right.xz);
    // remap both dot products from { -1, 1 } to { 0, 1 } and invert
    RdotL = RdotL * 0.5 + 0.5;
    FdotL = 1 - (FdotL * 0.5 + 0.5);
    // get direction of lightmap based on RdotL being above 0.5 or below
    float lightmapDir = (RdotL <= 0.5) ? facemap_mirror : facemap;
    
    // use FdotL to drive the face SDF, make sure FdotL has a maximum of 0.999 so that it doesn't glitch
    half shadow_range = min(0.999, FdotL);
    shadow_range = pow(shadow_range, pow(1, 3));
    // finally drive faceFactor
    half face_factor = smoothstep(shadow_range - _ShadowFeather, shadow_range + _ShadowFeather, lightmapDir + (_LightArea - 0.51f)); // i couldn't think of any other way

    return face_factor * ao;
}

float hi3_shadow(float lightmap, float ndotl, float vertex)
{


    float shadow_out = lightmap * vertex;

    float shadow = floor(vertex * lightmap + 0.909999967);
    float shadow_area =  max(floor((-vertex) * lightmap + 1.5), 0.0);

    float2 whar = shadow_out * float2(1.20000005, 1.25) + float2(-0.100000001, -0.125); // i actually don't know what to call this

    shadow_out = (shadow_area != 0) ? whar.y : whar.x;
    shadow_out = max(floor(((shadow_out + ndotl) * _LightArea - 0.5f) + 1.0f), 0.0);

    return shadow_out;
}

float3 hi3_specular(float ndoth, float2 lightmap)
{

    ndoth = pow(max(ndoth, 0.0), _Shininess);
    float Mask = (1.0f - lightmap.y) - ndoth;
    float SpecFlag = int(max(floor(Mask + 1.0), 0.0));
    float3 SpecColor = (SpecFlag != 0) ? 0.0 : lightmap.x * (_SpecMulti * _LightSpecColor.xyz);

    return SpecColor;
}

float3 hi3_rim(float ndotv, float lightmap, float ndotl, float3 diffuse) 
// yeah i'm not cleaning this more than i need to
{
    float3 env_world = _Color.xyz * _EnvColor.xyz;
    diffuse.xyz = diffuse.xyz * env_world.xyz;



    float DotProd = ndotv;
    float Rim = 1.0f - DotProd;

    Rim = clamp(Rim, 0.0f, 1.0f);

    Rim = (pow(Rim, _RGShininess)) * _RGScale + _RGBias;

    float Rim_Out = Rim;
    float Ratio = Rim_Out * _RGRatio;
    
    float3 RGcolor = Rim * _RGColor.xyz;

    float4 RColors = rim_cols(lightmap);
    float3 Rim_col = RColors.xyz * Rim;

    RGcolor.xyz = (int(_MoreHardRimColor) != 0) ? Rim_col.xyz : RGcolor.xyz;
    RColors = (int(_MoreHardRimColor) != 0) ? RColors : _RGColor;
    DotProd = max(DotProd, 0.0f);
    DotProd = (-DotProd) + 1.0f;


    float Rim_mask = DotProd * (ndotl + 0.5f);

    bool RimCheck = RColors.w < Rim_mask;


    Rim_mask = (RimCheck) ? 1.0 : 0.0;
    Rim_Out = (_Hardness * (Rim_mask - (Rim_Out * _RGRatio))) + Ratio;
    Rim_col.xyz = RColors.xyz * Rim_Out;
    Rim_col.xyz = Rim_col.xyz * _HRRimPower + diffuse.xyz;
    RimCheck = RColors.w < 0.0099f;

    Rim_Out = (RimCheck) ? 0.0 : Ratio;

    diffuse.xyz = lerp(diffuse.xyz, diffuse * env_world + RGcolor, Rim_Out);
    diffuse.xyz = lerp(diffuse.xyz, Rim_col, _HRRimIntensity);


    return diffuse;
}


void dissolve_a(in float2 uv2, in float4 mask_uv, in float2 dis_angle, inout float3 color, out float blend_alpha, out float add_alpha, out float out_noise, out float2 out_mask)
{
    #if defined(can_dissolve)
        float noise = _NoiseTex.Sample(sampler_linear_repeat, mask_uv.zw).x * _NoiseIntensity;
        float2 mUV = noise * (float2)0.0099f + mask_uv.xy;
        float2 mask = _MaskDisTex.Sample(sampler_linear_repeat, mUV).xy * (float2)_MaskDisTexScale;
        // since the second dissolve function needs the masks and noise, output them like this
        out_mask = mask;
        out_noise = noise; 
        if(_LengthWaysDisBlend) // if(_LengthWaysDisBlend != 0)
        {
            float alpha = _AlphaPosition + _Edge;
            alpha = saturate((alpha - 0.5f) * mask.x - 1.0f);
            alpha = ceil(alpha);

            float4 main2 = _MainTex2.Sample(sampler_MainTex, uv2);
            add_alpha = main2.w * _AddLightColor.w;
            float idk_alpha = saturate(main2.w * _AddLightColor.w + -alpha);
            float3 idk = main2.xyz * _AddLightColor.xyz  + -color;
            color = idk_alpha.xxx * idk + color;
            blend_alpha = alpha;
        }
        else 
        {
            add_alpha = 1.0f;
            blend_alpha = 0.0f;
        }
    #endif
}

void dissolve_b(float2 uv2, float4 mask_uv, float2 dis_angle, float in_noise, float2 in_mask, inout float4 in_color, float add_alpha, float blend_alpha)
{
    #if defined(can_dissolve)
        if(_BackFaceUseUV2)
        {
            float noise_tint = min(pow(in_noise, _TintColorPower), 1.0f);
            float noise_check = (in_noise * in_noise) < 0.001f;
            float3 dissolve_color = (float3)_DisColorScale * _DisColor.xyz;
            float dis_noise = noise_check ? -1.0f : noise_tint + -1.0f;
            dis_noise = _TintColorEdge * dis_noise + 1.0f;

            if(_LengthWaysDisBlend)
            {
                noise_tint = _AlphaPosition + _Edge;
                float2 noise_mask = (-noise_tint) * in_mask.xy + (float2)1.0f;

                // smoothstep
                float soft = 1.0f / _Soft;
                noise_mask = saturate(noise_mask * soft);
                float2 mask_something = noise_mask * (float2)-2.0f + (float2)3.0f;
                noise_mask = -mask_something * (noise_mask * noise_mask) + (float2)1.0f;

                float edge = -blend_alpha + noise_mask;
                add_alpha = saturate(add_alpha * edge); 

                float3 color = dissolve_color * dis_noise + in_color.xyz;

                in_color.xyz  = add_alpha * color + in_color.xyz;

                float alpha = -(noise_mask.y) * blend_alpha + 1.5f;       
                alpha = floor(alpha);
                int(alpha);
                if(int(alpha)==0)clip(-1.0f);
                // clip(alpha);
            }
            else
            {
                float uv_dis = dis_angle.y + (-mask_uv.y);
                uv_dis = _DissolveUseUV2 * uv_dis + mask_uv.y;
                uv_dis = (_OnlyUseMaskDis) ? 1.0f : uv_dis;

                float alpha = _AlphaPosition + -0.5f;

                float idk = alpha + _Edge;
                idk = (-idk) * in_mask.x + uv_dis;

                float soft = 1.f / _Soft;
                idk = saturate(idk * soft);
                soft = idk * -2.0f + 3.0f;
                soft = -idk * soft + 1.0f;

                float3 color = dissolve_color * dis_noise + in_color.xyz;

                in_color.xyz  = soft * color + in_color.xyz;

                float clip_alpha = -alpha * in_mask.x + uv_dis;
                clip_alpha = max(floor(clip_alpha + 1.0f), 0.0f);
                if(int(clip_alpha)==0) clip(-1.f);
            }
        }
    #endif 
    // return in_color;
}

// sorry chi, this was the only place i could think of adding this function
float3 DecodeLightProbe( float3 N )
{
    return ShadeSH9(float4(N,1));
}

// once again invading to drop another function in...
float3 camera_position()
{
    #ifdef USING_STEREO_MATRICES
        return lerp(unity_StereoWorldSpaceCameraPos[0], unity_StereoWorldSpaceCameraPos[1], 0.5);
    #endif
    return _WorldSpaceCameraPos;
}
