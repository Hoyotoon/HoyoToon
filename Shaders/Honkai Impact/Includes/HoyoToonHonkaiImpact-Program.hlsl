edge_out edge_model(edge_in i)
{
    edge_out o;
    o = (edge_out)0.0f;
    //cast entire structure to 0 before modifying them later, this pervents warnings in unity and in general is good practice
    if(_EnableOutline)
    {
        float3 outline_normal;
        float3 pos, scale, scale_check;

        outline_normal = mul((float3x3)UNITY_MATRIX_IT_MV, i.tangent.xyz);
        // outline_normal.z = 0.001;
        outline_normal.xy = normalize(outline_normal.xy);

        float4 wv_pos = mul(UNITY_MATRIX_MV, i.pos);
        float4 wvtmp = wv_pos;

        wvtmp.x = (-wvtmp.z) + (-_ProjectionParams.y);
        wvtmp.x = wvtmp.x * _OutlineColor.w;

        scale = _OutlineWidth * _Scale * i.vertexcolor.w;
        scale = ((unity_CameraProjection[3].w == 1) ? 2 : 5.0f) * scale;

        pos.x = normalize(wv_pos.xyz);

        pos.xyz = normalize(wv_pos.xyz) * ((unity_CameraProjection[3].w == 1) ? 0.001 : _MaxOutlineZOffset) * _Scale;

        pos.xyz = pos.xyz + wv_pos.xyz;
        pos.xyz = outline_normal.xyz * (float3)(scale) + pos.xyz;

        wv_pos.xyz = pos;
        o.pos = mul(UNITY_MATRIX_P, wv_pos);
        
        o.vertex = i.vertexcolor;
        o.uv = i.uv;

        o.normal = mul((float3x3)unity_ObjectToWorld, i.normal);
    }
    else
    {
        o = (edge_out)0.f;
    }

    return o;
}

float4 ps_edge(edge_out i) : COLOR0
{
    if(_EnableOutline)
    {
        float3 GI_color = DecodeLightProbe(normalize(i.normal));
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;  
        GI_color = (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);

        float4 main_tex = _MainTex.Sample(sampler_MainTex, i.uv);

        float4 out_col = edge_cols(_LightMapTex.Sample(sampler_linear_repeat, i.uv).w);
        #if defined(faceishadow)
            if(variant_selector == 1 || variant_selector == 3) out_col = _OutlineColor;
        #endif

        if(_MainTex.Sample(sampler_MainTex, i.uv).w <= 0.5 && _TrasOutline) discard;
        if (i.vertex.w <= 0.001) discard;

        out_col.xyz = out_col.xyz * light_color + GI_color;

        if(_OutlineWidth == 0.0) discard;
        return out_col;
    }
    else
    {
        discard;
    }
    
    return -1; // if for some reason it escapes the above loops
}

vs_out vs_model (vs_in v)
{
    vs_out o;
    o = (vs_out)0.0f;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    if(variant_selector >= 4 ) o.pos.z += 0.0001;
    o.uv = v.uv;
    o.uv2 = v.uv2;
    o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
    o.VertexColor = v.VertexColor;
    o.view = normalize(_WorldSpaceCameraPos.xyz - (mul(unity_ObjectToWorld, v.vertex).xyz));
    o.ws_pos = mul(unity_ObjectToWorld, v.vertex);
    #if defined(can_dissolve)
        float2 maskuv = (_DissolveUseUV2) ? v.uv2 : v.uv;
        float maskspeed = (_Time.y * _MaskOffsetSpeed) * 0.1f + 1.0f;

        o.mask_uv.xy = maskuv * _MaskTillingOffset.xy + ((float2)maskspeed * _MaskTillingOffset.zw);
        o.mask_uv.zw = maskuv.xy * _NoiseTillingOffset.xy + -(((float2)_Time.y * _NoiseTillingOffset) * (float2)0.1f);
            // vs_TEXCOORD7
        float angle = _DisAngle * 0.0174f;
        float cangle = cos(angle);
        float sangle = sin(angle);
        float2 angle_uv = v.uv2.xy + (float2)-0.5;
        float2 rot;
        rot.x = dot(float2(cangle, -sangle), angle_uv.xy);
        rot.y = dot(float2(sangle, cangle), angle_uv.xy);
        o.dis_angle = rot + 0.5f; // vs_TEXCOORD6
    #endif
    TRANSFER_SHADOW(o)
    return o;
}

float4 ps_model (vs_out i, bool vface : SV_ISFRONTFACE) : SV_Target
{
    // ---------------------------------------------------------
    // Linking shader properties to others 
    float4 backFaceColor = _BackFaceColor * _BackColor;


    // ---------------------------------------------------------
    float4 out_col = (float4)1.0f;
    UNITY_LIGHT_ATTENUATION(atten, i, i.ws_pos.xyz);
    float2 uv =  (!vface && _BackFaceUseUV2) ? i.uv2 : i.uv; //this saves on doing an extra pass!
    float3 normal = vface ? i.normal : -1.0f * i.normal;
    float3 view = normalize(i.view);
    float4 vertex = i.VertexColor;

    float4 color = (vface) ? _Color : backFaceColor ;

    // light direction 
    float3 light_dir = _WorldSpaceLightPos0.xyz;
    #ifdef _IS_PASS_LIGHT
        #if defined(POINT) || defined(SPOT) 
            light_dir = normalize(_WorldSpaceLightPos0.xyz - i.ws_pos.xyz);
        #endif
    #endif
    // dots
    float ndotl = min(dot(normal,  light_dir) * 0.497500002 + 0.5, 1.0);
    float ndotv = dot(normal, view);
    float ndoth = dot(normal, normalize(light_dir + view));
    if(variant_selector == 1 || variant_selector == 2 || variant_selector == 3) ndotv = 1;

    // textures
    float4 diffuse = _MainTex.Sample(sampler_MainTex, uv * _MainTex_ST.xy + _MainTex_ST.zw);
    float4 Alpha = _MaskDisTex.Sample(sampler_linear_repeat, uv);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv); 
    
    float s = 1.0f;
    float light_shadow = 0.0f;
    #if defined(use_shadow)
        #if defined(faceishadow)
            // shadow stuff
            float shadow_right = (_FaceMapTex.Sample(sampler_linear_repeat, float2(      uv.x, uv.y)).a);
            float shadow_left  = (_FaceMapTex.Sample(sampler_linear_repeat, float2(1.0 - uv.x, uv.y)).a);
        #endif  

        light_shadow = hi3_shadow(lightmap.y, ndotl, vertex.x);
        
        float4 shadow_col = shadow_cols(lightmap.a);
        if(variant_selector == 1 || variant_selector == 3) shadow_col = _ShadowColor;
        if(variant_selector == 1)
        {   
            light_shadow = saturate(dot(float3(0.5f, 0.5f, 1.0f), light_dir));
            #if defined(faceishadow)
                s = shadow_rate_face(shadow_right, shadow_left, lightmap.y, i.ws_pos);
            #endif
        }
        else
        {
            s = hi3_shadow(lightmap.y, ndotl, vertex.x);
            // light_shadow = s;
        }
        float4 _shadow = min(s + (shadow_col), 1.0);
    #else
        float4 _shadow = 1.0f;
    #endif
    
    float3 specular = 0.0f;
    #if defined(use_specular)
        if(_EnableSpecular)
        {
            specular = hi3_specular(ndoth, lightmap.xz);
        }
    #else
        specular = (float3)0.0f;
    #endif
    if(!_EnableShadow) _shadow = 1.0f;


    #ifdef _IS_PASS_BASE
        // inputs
        if(variant_selector == 1 ||  variant_selector == 3) specular = 0;
        
        out_col = diffuse * color;
        

        #if defined(can_dissolve)
            // initialize dissolve shit
            float blend_alpha;
            float add_alpha;
            float dis_noise;
            float2 dis_mask;

            float3 dummy = out_col;
            dissolve_a(uv, i.mask_uv, i.dis_angle,  dummy.xyz, blend_alpha, add_alpha, dis_noise, dis_mask);
        #endif

        out_col.xyz = out_col.xyz * _shadow + specular;

        #if defined(use_rimlight)
            if(_RimGlow) out_col.xyz = hi3_rim(ndotv, lightmap.a, dot(normal, normalize(_WorldSpaceLightPos0)), out_col.xyz); // the dot code should be left alone
            // they do not natively use point lights and it could fuck it up
        #endif

        if(_AlphaType == 1)
        {
            out_col.w = diffuse.a;
        }
        else if(_AlphaType == 2)
        {
            #if defined(use_emission)
                if(_EnableEmission)
                {
                    float EmissionPulse = 1;

                    if(_usepulse)
                    {
                        float pulse = sin(_Time.yy * _PulseRate) * 0.5f + 0.5f;
                        EmissionPulse = smoothstep(_MinPulse, _MaxPulse, pulse);
                    }
                    float4 emission_color =1;            
                    if(_EmissionColorToggle) emission_color = emi_cols(lightmap.a);
                    out_col.xyz += max(((diffuse.xyz * emission_color) * (diffuse.a> 0.45) * EmissionPulse) * _EmissionStr, 0.0);
                }
            #endif
        }
        
        if(_AlphaClip) clip(diffuse.w - 0.5f);
        out_col.w = out_col.w * _Opaqueness;

        #if defined(faceishadow)
            if(variant_selector == 1)
            { // face expression
                float4 expressionmap = _FacExpTex.Sample(sampler_linear_repeat, uv.xy);

                float blush = saturate(pow(expressionmap.x * _ExpBlushIntensity, 2));
                float2 otherexp = saturate(float2(expressionmap.y * _ExpShadowIntensity, expressionmap.z * _ExpShadowIntensity2));

                float final_exp = (-expressionmap.w + 1.0);
                final_exp = pow(final_exp, 2) * _ExpShadowIntensity3;

                out_col.xyz *= (1.0 - blush) + blush * _ExpBlushColor;
                out_col.xyz *= (1.0 - otherexp.x) + otherexp.x * _ExpShadowColor;
                out_col.xyz *= (1.0 - otherexp.y) + otherexp.y * _ExpShadowColor2;
                out_col.xyz *= (1.0 - final_exp) + final_exp * _ExpShadowColor3; // I didn't want to lerp it so many times
            }else if(variant_selector == 3)
            {
                float2 eye_uv = (uv - _EyeEffectCenterPos.xy) / (_EyeEffectLocalScale.xy * 0.5) + float2(0.5, 0.5);
                float4 eye_star = _EyeEffectTex.Sample(sampler_linear_repeat, eye_uv);
                out_col = lerp(out_col, eye_star, eye_star.w * _EyeEffectPupil);
            }
        #endif


        #ifdef stencil
            if(_EnableStencil == 1)
            {
                if(variant_selector == 1) 
                {
                    clip(lightmap.x - 0.45f); 
                }
                else if(variant_selector == 2)
                {
                    float3 up      = UnityObjectToWorldDir(_headUp.xyz);
                    float3 forward = UnityObjectToWorldDir(_headForward.xyz);
                    float3 right   = UnityObjectToWorldDir(_headRight.xyz);

                    float3 view_xz = normalize(view - dot(view, up) * up);
                    float cosxz    = max(0.0f, dot(view_xz, forward));
                    float alpha_a  = saturate((1.0f - cosxz) / 0.858f);

                    float3 view_yz = normalize(view - dot(view, right) * right);
                    float cosyz    = max(0.0f, dot(view_yz, forward));
                    float alpha_b  = saturate((1.0f - cosyz) / 0.593f);

                    float hair_alpha = max(alpha_a, alpha_b);
                    out_col.w = max(hair_alpha, 0.5f);        
                }
                else if(variant_selector != 3)
                {
                    discard;
                }
            }
            else 
            {
                discard;
            }
        #endif 

        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);
        out_col.xyz = out_col.xyz * light_color;
        #if defined(can_dissolve)
            dissolve_b(i.uv2, i.mask_uv, i.dis_angle, dis_noise, dis_mask, out_col, add_alpha, blend_alpha);
        #endif

        float3 GI_color = DecodeLightProbe(normal);
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;

        out_col.xyz = out_col.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
    #endif
    #ifdef _IS_PASS_LIGHT
        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        float3 light_pass_color = ((diffuse.xyz * 5.0f) * _LightColor0.xyz) * atten * saturate(light_shadow) * 0.5f;
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), _FilterLight); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
        out_col.xyz = (light_color) * 0.5f;
        #elif defined(DIRECTIONAL)
        out_col.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
    #endif


    return out_col; 
}
