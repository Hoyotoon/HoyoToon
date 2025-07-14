// utility
// unity lighting
float3 DecodeLightProbe( float3 N )
{
    return ShadeSH9(float4(N,1));
}

void CalcLighting(in float3 normal, inout float3 color)
{
    float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
    float3 light_color = max(ambient_color, _LightColor0.rgb);

    float3 GI_color = DecodeLightProbe(normal);
    GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
    float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
    GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;

    color.xyz = color.xyz * light_color;
    color.xyz = color.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
}

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

float extract_fov()
{
    return 2.0f * atan((1.0f / unity_CameraProjection[1][1]))* (180.0f / 3.14159265f);
}

float fov_range(float old_min, float old_max, float value)
{
    float new_value = (value - old_min) / (old_max - old_min);
    return new_value;

}

// - - - - - - - - - - - - 

float4 Material_Detect(float id) // x: skin y: hair z: 
{
    float4 material = 0;
    // remap ID and check what kind of ID it is 
    id = 4.5 * id;
    id = floor(id);
    id = 4 + -id;
    id = (int)id;
    material.x = ((int)id == asint(_SkinMatId)); // check if its skin mat
    material.y = ((int)id == asint(_HairMatId)); // check if its hair mat
    return material;
}

// - - - - - - - - - - - - 
// vrchat haha funny stuff
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

float3 hue_shift(float3 in_color, float id, float shift1, float shift2, float shift3, float shift4, float shift5, float shiftglobal, float autobool, float autospeed, float mask)
{   
    if(!_EnableHueShift) return in_color;
    float auto_shift = (_Time.y * autospeed) * autobool; 
    
    float4 material_areas = id < float4(0.2f, 0.4f, 0.6f, 0.8f);
    float _ID;
    _ID = material_areas.w ? 1 :  0;
    _ID = material_areas.z ? 2 : _ID;
    _ID = material_areas.y ? 3 : _ID;
    _ID = material_areas.x ? 4 : _ID;
    
    float shift[5] = 
    {
        shift1,
        shift2,
        shift3,
        shift4,
        shift5
    };
    
    
    float shift_all = 0.0f;
    if(shift[_ID % 5] > 0) // the modulo makes it so amd gpus can't be funny
    {
        shift_all = shift[_ID % 5] + auto_shift;
    }
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
}

// - - - - - - - - - - - - 
void apply_alpha(inout float4 color, in float alpha)
{
    if(_UseAlpha)
    {
        if(_AlphaCutoff <= 0.0) 
        {
            clip(alpha - _AlphaCutoff);
        }
        else
        {
            color.a = color.a * alpha;
        }
    }
}

// - - - - - - - - - - - -

// normal mapping online
float3 normal_mapping(float3 normalmap, float4 vertexws, float2 uv, float3 normal, bool front_facing)
{
    float3 bumpmap = normalmap.xyz;
    bumpmap.xy = bumpmap.xy * 2.0f - 1.0f;

    float facing = front_facing ? 1.0f : -1.0f;

    bumpmap.z = dot(bumpmap.xy, bumpmap.xy);
    bumpmap.z = min(1, bumpmap.z);
    bumpmap.z = 1 + -bumpmap.z;
    bumpmap.z = sqrt(bumpmap.z);
    bumpmap.z = bumpmap.z * facing;

    bumpmap.xyz = normalize(bumpmap);   // why why why 

    // world space position derivative
    float3 p_dx = ddx(vertexws);
    float3 p_dy = ddy(vertexws);  
    // texture coord derivative
    float3 uv_dx;
    uv_dx.xy = ddx(uv);
    float3 uv_dy;
    uv_dy.xy = ddy(uv); 
    uv_dy.z = -uv_dx.y;
    uv_dx.z = uv_dy.x;  
    // this functions the same way as the w component of a traditional set of tangents.
    // determinent of the uv the direction of the bitangent
    float3 uv_det = dot(uv_dx.xz, uv_dy.yz);
    uv_det = -sign(uv_det); 
    // normals are inverted in the case of a back-facing poly
    // useful for the two sided dresses and what not... 
    float3 corrected_normal = normal;   
    float2 tangent_direction = uv_det.xy * uv_dy.yz;
    float3 tangent = (tangent_direction.y * p_dy.xyz) + (p_dx * tangent_direction.x);
    tangent = normalize(tangent);
    float3 bitangent = cross(corrected_normal.xyz, tangent.xyz);
    bitangent = bitangent * -uv_det;    
    
    float3x3 tbn = {tangent, bitangent, corrected_normal};  
    float3 mapped_normals = mul(bumpmap.xyz, tbn);  
    mapped_normals = normalize(mapped_normals);
    return mapped_normals; 
}

float3 normal_mapping(float3 normalmap, float3 normal, float4 tangent, bool front_facing)
{   
    normalmap.xy = normalmap.xy * 2.0f - 1.0f;
    normalmap.xy = normalmap.xy * _BumpScale; // flip y axis for some reason
    // normalmap.xy = normalize(normalmap.xy);
    float z_norm = dot(normalmap.xy, normalmap.xy);
    z_norm = min(1, z_norm);
    z_norm = 1 + -z_norm;
    z_norm = sqrt(z_norm);
    float facing = front_facing ? 1.0f : -1.0f;
    z_norm = z_norm * facing;
    
    // normal mapping 
    float3 mapped_normals;
    mapped_normals.yzx = tangent.yzx * normal.zxy;
    mapped_normals.yzx = normal.yzx * tangent.zxy + -mapped_normals.yzx;
    mapped_normals.yzx = tangent.www * mapped_normals.yzx;
    mapped_normals.yzx = normalmap.yyy * mapped_normals.yzx;
    mapped_normals.yzx = normalmap.xxx * tangent.xyz + mapped_normals.yzx;
    mapped_normals.xyz = z_norm * normal.xyz + mapped_normals.yzx;
    return mapped_normals;
}

// - - - - - - - - - - - - 

float3 specular(float3 normal, float3 view, float3 light, float4 other_data, float4 other_data2, float3 color, float shad, float3 pos)
{   
    float4 r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15;
    r8.w = dot(light.xyz, normal.xyz);
    r0.x = _Metallic * other_data.y;
    r3.x = other_data.z;
    r0.y = other_data2.y;
    r7.xyz = color;
    float4 check = other_data.xxxx < float4(0.2f, 0.4f, 0.6f, 0.8f);
    float3 some_color = color;
    r0.w = dot(some_color, float3(0.289999992,0.600000024,0.109999999));
    r0.w = r0.w * 0.287499994 + 1.4375;
    r9.x = dot(normal.xyz, light.xyz);
    r9.y = r9.x + -r8.w;
    r9.y = saturate(-r9.y * 3 + 1);
    r9.z = r9.y + r9.y;
    r9.y = sqrt(r9.y);
    r9.y = r9.z * r9.y;
    r9.y = min(1, r9.y);
    r9.z = r8.w * 0.5 + 0.5;
    r9.w = saturate(r8.w);
    r9.y = r9.z * r9.y + -r9.w;
    r9.y = r9.y * 0.5 + r9.w;
    r9.x = saturate(r9.x);
    r9.z = max(color.y, color.z);
    r9.z = max(r9.z, color.x);
    r10.x = (1 < r9.z);
    r10.yzw = some_color / r9.zzz;
    r10.xyz = r10.xxx ? r10.yzw : some_color;
    r9.z = 1 + -r0.w;
    float power = r9.y * r9.z + r0.w; //r0.w
    // r10.xyz = log2(r10.xyz);
    r10.xyz = pow(r10.xyz, power);
    // r10.xyz = exp2(r10.xyz);
    
    r11.xyz = r10.xyz + -some_color;

    some_color = r11.xyz * float3(0.5,0.5,0.5) + some_color;
    r10.xyz = r10.xyz + -some_color;
    some_color = r9.xxx * r10.xyz + some_color;
    r0.w = -r0.x * 0.959999979 + 0.959999979;
    r9.xyz = some_color * r0.www;
    r10.xyz = float3(-0.0399999991,-0.0399999991,-0.0399999991) + some_color;
    r10.xyz = r0.xxx * r10.xyz + float3(0.0399999991,0.0399999991,0.0399999991);
    r10.w = -r0.y * _Glossiness + 1;
    r10.w = r10.w * r10.w;
    r11.x = r10.w * 4 + 2;
    r11.y = r10.w * r10.w;
    r11.z = r10.w * r10.w + -1;
    r11.w = check.w ? _HighlightShape2 : _HighlightShape;
    r11.w = check.z ? _HighlightShape3 : r11.w;
    r11.w = check.y ? _HighlightShape4 : r11.w;
    r11.w = check.x ? _HighlightShape5 : r11.w;
    r12.xyz = check.www ? _SpecularColor2.xyz : _SpecularColor.xyz;
    r12.xyz = check.zzz ? _SpecularColor3.xyz : r12.xyz;
    r12.xyz = check.yyy ? _SpecularColor4.xyz : r12.xyz;
    r12.xyz = check.xxx ? _SpecularColor5.xyz : r12.xyz;
    r12.xyz = r12.xyz * 0.5f;
    r12.w = (0.5 < r11.w);
    if (r12.w != 0) {
        r4.w = saturate(shad * 1.5 + 0.5);
        r3.z = check.w ? _ShapeSoftness2 : _ShapeSoftness;
        r3.z = check.z ? _ShapeSoftness3 : r3.z;
        r3.z = check.y ? _ShapeSoftness4 : r3.z;
        r3.z = check.x ? _ShapeSoftness5 : r3.z;

        r14.xyz = normalize(light + view);
        r12.w = (0 < _HeadSphereNormalCenter.w);
        r15.xyz = -_HeadSphereNormalCenter.xyz + pos.xyz;
        r14.w = dot(r15.xyz, r15.xyz);
        // r15.w = rsqrt(r14.w);
        r14.w = sqrt(r14.w);
        r14.w = -_HeadSphereNormalCenter.w + r14.w;
        r14.w = saturate(20 * r14.w);
        r14.w = 1 + -r14.w;
        r15.xyz = normalize(r15.xyz) + -normal.xyz;
        r15.xyz = r14.www * r15.xyz + normal.xyz;
        r14.w = dot(light, r15.xyz);
        r14.w = saturate(r14.w * 0.5 + 0.5);
        r15.w = sqrt(r14.w);
        r15.xyzw = r12.wwww ? r15.xyzw : float4(normal, r4.w);
        r4.w = dot(r15.xyz, r14.xyz);
        r4.w = saturate(r4.w * 0.5 + 0.5);
        r4.w = -r4.w * r15.w + 1;
        r4.w = -r4.w + other_data.z;
        r3.x = saturate(r4.w / r3.z);
    }
    r3.x = (_SpecIntensity * 10) * r3.x;
    r12.xyz = r3.xxx * r12.xyz;
    r12.xyz = r12.xyz * r10.xyz;
    r3.x = (r11.w < 0.5);
    r5.xyz = normalize(light + view);
    r3.z = check.w ? _SpecularRange2 : _SpecularRange;
    r3.z = check.z ? _SpecularRange3 : r3.z;
    r3.z = check.y ? _SpecularRange4 : r3.z;
    r3.z = check.x ? _SpecularRange5 : r3.z;
    r3.w = r3.z * r8.w;
    r3.w = saturate(r3.w * 0.75 + 0.25);
    r4.w = dot(normal, r5.xyz);
    r4.w = r4.w * r3.z;
    r4.w = saturate(r4.w * 0.75 + 0.25);
    r5.x = dot(light, r5.xyz);
    r3.z = r5.x * r3.z;
    r3.z = saturate(r3.z * 0.75 + 0.25);
    r4.w = r4.w * r4.w;
    r4.w = r4.w * r11.z + 1.00001001;
    r3.z = r3.z * r3.z;
    r4.w = r4.w * r4.w;
    r3.z = max(0.100000001, r3.z);
    r4.w = r4.w * r3.z;
    r4.w = r4.w * r11.x;
    r4.w = r11.y / r4.w;
    r0.y = saturate(-r0.y * _Glossiness + r4.w);
    r0.y = r0.y * r3.w;
    r4.w = max(9.99999975e-06, r10.w);
    r0.y = r0.y / r4.w;
    r4.w = check.w ? _ToonSpecular2 : _ToonSpecular;
    r4.w = check.z ? _ToonSpecular3 : r4.w;
    r4.w = check.y ? _ToonSpecular4 : r4.w;
    r4.w = check.x ? _ToonSpecular5 : r4.w;
    r5.x = check.w ? _ModelSize2 : _ModelSize;
    r5.x = check.z ? _ModelSize3 : r5.x;
    r5.x = check.y ? _ModelSize4 : r5.x;
    r5.x = check.x ? _ModelSize5 : r5.x;
    r4.w = r5.x * r4.w;
    r0.y = r4.w * r0.y;
    r0.y = saturate(10 * r0.y);
    r0.y = 100 * r0.y;
    r3.z = 0.166663334 / r3.z;
    r3.z = min(1, r3.z);
    r3.z = r3.z * r3.w;
    r3.z = 100 * r3.z;
    r0.y = r3.x ? r0.y : r3.z;
    r3.xzw = r0.yyy * r12.xyz;
    r5.xyz = r3.xzw * r7.xyz;
    return r5.xyz;
}

// - - - - - - - - - - - - 

float3 emission(float3 color, float4 other_data2, float4 other_data, float2 uv)
{
    float emission_mask = other_data2.z;
    emission_mask = -0.200000003 + other_data2.z;
    emission_mask = saturate(1.25 * emission_mask);
    emission_mask = (_UseMatCapMask ? _Emission : 0) ? emission_mask : other_data2.z;
    emission_mask = _Emission ? emission_mask : 0;

    float4 emission_check = other_data.z < float4(0.2f, 0.4f, 0.6f, 0.8f);
    float4 emission_color = _EmissionColor;
    emission_color = emission_check.w ? _EmissionColor2 : emission_color;
    emission_color = emission_check.z ? _EmissionColor3 : emission_color;
    emission_color = emission_check.y ? _EmissionColor4 : emission_color;
    emission_color = emission_check.x ? _EmissionColor5 : emission_color;

    if(_EnableHueShift && _EnableEmissionHue)
    {
        float mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _EmissionMaskSource);
        mask = _UseHueMask ? mask : 1.0f;
        if(_EnableEmissionHue)emission_color.xyz = hue_shift(emission_color.xyz, other_data.x, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, mask);
    }
    if(_DebugEmission == 1)
    {
        float3 emission_final = (emission_color.xyz) * emission_mask;
        return emission_final;
    }
    else
    {
        float3 emission_final = (emission_color.xyz * color) * emission_mask;
        return color + emission_final;
    }
}

float2 rotateUV(float2 uv, float rotation)
{
    float mid = 0.5;
    return float2(
        cos(rotation) * (uv.x - mid) + sin(rotation) * (uv.y - mid) + mid,
        cos(rotation) * (uv.y - mid) - sin(rotation) * (uv.x - mid) + mid
    );
}

float2 rotateUV(float2 uv, float rotation, float2 mid)
{
    return float2(
      cos(rotation) * (uv.x - mid.x) + sin(rotation) * (uv.y - mid.y) + mid.x,
      cos(rotation) * (uv.y - mid.y) - sin(rotation) * (uv.x - mid.x) + mid.y
    );
}

float3 secondary_emission(float3 color, float2 uv, float2 uv1, float mat)
{
    float2 coord = _SecondaryEmissionUseUV2 ? uv1 : uv;
    float4 emission_mask = _SecondaryEmissionMaskTex.Sample(sampler_linear_repeat, uv * _SecondaryEmissionMaskTex_ST.xy + _SecondaryEmissionMaskTex_ST.zw);
    float mask[3] = {emission_mask.x, emission_mask.y, emission_mask.z};
    float4 emission_tex  = _SecondaryEmissionTex.Sample(sampler_linear_repeat, rotateUV((coord * _SecondaryEmissionTex_ST.xy + _SecondaryEmissionTex_ST.zw), _SecondaryEmissionTexRotation) + (_SecondaryEmissionTexSpeed.xy * _Time.yy));


    float3 multiply_color = lerp(1, color, _MultiplyAlbedo);
    if((_DebugEmission != 0) && (_DebugMode != 0)) multiply_color = 1;
    float3 emission_color = (_SecondaryEmissionColor * (_SecondaryEmissionChannel ?  emission_tex.xyz : emission_tex.xxx ) * multiply_color) * mask[_SecondaryEmissionMaskChannel % 3];
    if(_EnableHueShift && _EnableEmissionHue)
    {
        float mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _EmissionMaskSource);
        mask = _UseHueMask ? mask : 1.0f;
        if(_EnableEmissionHue)emission_color.xyz = hue_shift(emission_color.xyz, mat, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, mask);
    }
    if((_DebugEmission != 0) && (_DebugMode != 0))
    {
        return lerp(0, color +  emission_color, mask[_SecondaryEmissionMaskChannel % 3] * _SecondaryEmission);
    }
    else
    {   
        return lerp(color, color + emission_color, mask[_SecondaryEmissionMaskChannel % 3] * _SecondaryEmission);
    }
}

float pulse(float x, float y, float z)
{
    return lerp(x, y, 0.5f + 0.5f * sin(_Time.y * z));
}

float3 screen_image(float3 color, float2 uv, float2 uv1, float4 screen_pos, float mat, float3 view, float3 ws_pos)
{

    float2 screen_uv = _ScreenUVSource == 2 ? screen_pos.xy * _ScreenScale: (((screen_pos.xy / screen_pos.ww))) * _ScreenScale;
    screen_uv = rotateUV(screen_uv, _ScreenTexRotation, _ScreenTexRotationAxis.xy) + (_ScreenImageUvMove.xy * _Time.yy);
    float3 tex = _ScreenTex.Sample(sampler_linear_repeat, screen_uv).xyz;
    float mask = _ScreenMask.Sample(sampler_linear_repeat, (_ScreenMaskUV ? uv1 : uv)).x;

    float3 screen_color = lerp((float3)1.0f, color, _MultiplySrcColor) * _ScreenColor.xyz * tex;
    screen_color = screen_color * (_Blink ? (pulse(0,1, (_BlinkFrequency*10)) * _BlinkOpacity ) : 1);
    if(_EnableHueShift && _EnableEmissionHue)
    {
        float mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv, _EmissionMaskSource);
        mask = _UseHueMask ? mask : 1.0f;
        if(_EnableEmissionHue)screen_color.xyz = hue_shift(screen_color.xyz, mat, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, mask);
    }
    
    return lerp(color, screen_color + color, mask * _ScreenImage);

}

float3 LUT_2D(float3 color, float4 lutParams)
{   
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


    return lutColor;
}

// - - - - - - - - - - - - 
// this determines the facing direction of the head and will change the uv depending on it 
void vertex_face(float2 uv0, float2 uv1, float4 vertex_color, float4 ws_pos, out float4 output)
{
    output.z = int(vertex_color.z * 255) & 16;
    output.z = output.z < 0.5f ? 0.0f : 1.0f; // bit flag for face direction
    output.z = _UseLegacyFace ? 0.0f : output.z; // disable face direction if vertex material ID is used


    float3 head_forward = normalize(UnityObjectToWorldDir(_headForwardVector.xyz));
    float3 head_right   = normalize(UnityObjectToWorldDir(_headRightVector.xyz));

    float3 light = normalize(_WorldSpaceLightPos0.xyz);
    #if defined(POINT) || defined(SPOT)
        light = normalize(_WorldSpaceLightPos0.xyz - ws_pos.xyz);
    #endif
    float rdotl = dot((head_right.xz),  (light.xz));
    float fdotl = dot((head_forward.xz), (light.xz));

    float2 uv = output.z ? uv0 : uv1;
    if(rdotl > 0.0f )
    {
        uv = uv;
    }  
    else
    {
        uv = uv * float2(-1.0f, 1.0f) + float2(1.0f, 0.0f);
    }

    float shadow = 1.0 - (fdotl * 0.5 + 0.5);
        
    output.xy = uv;
    output.w = shadow;
}

void vertex_face(float2 uv0, float2 uv1, float4 vertex_color, out float4 output)
{
    output.z = int(vertex_color.z * 255) & 16;
    output.z = output.z < 0.5f ? 0.0f : 1.0f; // bit flag for face direction
    
    if(_UseLegacyFace){uv1 = uv0;}


    float3 head_forward = normalize(UnityObjectToWorldDir(_headForwardVector.xyz));
    float3 head_right   = normalize(UnityObjectToWorldDir(_headRightVector.xyz));

    float3 light = normalize(_WorldSpaceLightPos0.xyz);
   
    float rdotl = dot((head_right.xz),  (light.xz));
    float fdotl = dot((head_forward.xz), (light.xz));

    float2 uv = output.z ? uv0 : uv1;
    if(rdotl > 0.0f )
    {
        uv = uv;
    }  
    else
    {
        uv = uv * float2(-1.0f, 1.0f) + float2(1.0f, 0.0f);
    }

    float shadow = 1.0 - (fdotl * 0.5 + 0.5);
        
    output.xy = uv;
    output.w = shadow;
}

float4 seperate_eyeshadow(float vc)
{
    int4 load_uv;
    uint tmp;
    vc = vc * 255.f;
    tmp = (uint)vc;
    load_uv.x = (int)tmp & 15;
    tmp = (uint)tmp >> 4;
    load_uv.y = (int)-tmp + 15;
    load_uv.zw = float2(0,0);
    float4 eye_lut = _EyeColorMap.Load(load_uv);
    return eye_lut * float4((float3)2.f, 1.f);
}

float shadow_id(float tex_data)
{
    float id = 0;
    id = (tex_data.r < 0.8) ? 1 : 0;
    id = (tex_data.r < 0.6) ? 2 : id;
    id = (tex_data.r < 0.4) ? 3 : id;
    id = (tex_data.r < 0.2) ? 4 : id;
    return min(max(id, 0), 4); // prevent amd cards from being stupid
}

float2 shadow_area_face(Texture2D smp, float4 face_values, float3 light)
{
    float facemap = smp.Sample(sampler_linear_repeat, face_values.xy).x * 0.9 + 0.1f;
    float ao = smp.Sample(sampler_linear_repeat, face_values.xy).w;
    if(face_values.z < 0.5f) ao = 1.0f;

    // interpolate between sharp and smooth face shading
    float shadow_step = smoothstep(face_values.w - 0.5, face_values.w + 0.5, facemap) * ao;
    // shadow_step = shadow_step * ao;

    return float2(shadow_step, ao);
}

float3 normalize_color(float3 color, float4 tmp)
{
    float2 magic = float2(0.562750012, 0.437249988);
    float tmp2;
    color = 0.00006f + color;
    tmp2 = tmp.x + tmp.y;
    tmp2 = tmp2 + tmp.z;
    tmp2 = 0.333330005 * tmp2;
    float3 color_div = saturate(color / tmp2);
    color = color * magic.yyy;
    color = color_div * magic.xxx + color;
    return color;
}

float4 shadow_body(float3 normal, float3 light, float4 tex_data, float id, float selfshadow)
{
    float3 light_direction = light;

    float4 shadow_thresholds,temp0,temp1,temp2,temp3,temp4;
    
    float ao_tex = tex_data.z;
    ao_tex = ao_tex * (selfshadow);
    ao_tex = ao_tex * 2 + -1;

    float albedo_smoothness = max(0.00000999999975f, _AlbedoSmoothness); // fuck whoever gets on my ass about this being too small of a number and should be 0
    float inverse_smoothness = rcp(albedo_smoothness);
    float ndotl = dot(normal, light_direction);
    ndotl = ndotl * saturate(selfshadow);

    float shad = ao_tex * 2 + ndotl;
    float albedo_step = -albedo_smoothness * 3 + 2;
    albedo_step = 3 / albedo_step;
    shadow_thresholds.yz = albedo_smoothness * float2(0.5,1.5) + float2(-0.333299994,0.333299994);
    shadow_thresholds.x = -1;
    shadow_thresholds.xyz = -shadow_thresholds.xyz + shad; // shadow regions
    temp0.xyw = shadow_thresholds.xyz * albedo_step; // shadow step
    shadow_thresholds.xyz = -shadow_thresholds.xyz * albedo_step + float3(1,1,1);
    temp1.xyz = float3(0.333299994,-0.333299994,-0.333299994) + shad;
    temp1.xyz = temp1.xyz * inverse_smoothness + float3(0.5,0.5,-0.5);
    temp2.xyz = float3(1,1,1) + -temp1.xyz;
    temp3.xy = min(temp2.yx, temp0.yx);
    temp0.xz = min(temp1.xz, shadow_thresholds.yz);
    temp3.z = shadow_thresholds.x;
    temp3.w = temp0.x;
    shadow_thresholds.xyz = saturate(temp3.zyw);

    temp3.y = saturate(min(temp2.z, temp1.y));
    temp3.x = saturate(temp3.x);
    temp0.zw = saturate(temp0.zw);
    temp1.xyzw = 1.0f * float4(-2,2,2,-2) + float4(1,0,-1,2);
    temp1.y = saturate(min(temp1.y, temp1.w));
    temp1.xz = saturate(temp1.xz);
    
        
    temp0.xy = temp1.xy;
    inverse_smoothness = 1 + -shadow_thresholds.x;
    inverse_smoothness = inverse_smoothness + -shadow_thresholds.y;
    inverse_smoothness = inverse_smoothness + -shadow_thresholds.z;
    inverse_smoothness = temp0.x * inverse_smoothness + shadow_thresholds.z;
    albedo_smoothness = temp1.y + temp1.z;
    shadow_thresholds.zw = temp3.xy * albedo_smoothness;
    albedo_smoothness = temp0.z + temp0.w;
    albedo_smoothness = albedo_smoothness * temp0.y + shadow_thresholds.w;
    albedo_step = temp1.z * temp0.z;
    shadow_thresholds.x =  shadow_thresholds.x;
    shadow_thresholds.x = shadow_thresholds.y + shadow_thresholds.x;
    
    float3 shallow_color = _ShallowColor.xyz;
    shallow_color = id < 0.8f ? _ShallowColor2.xyz : shallow_color;
    shallow_color = id < 0.6f ? _ShallowColor3.xyz : shallow_color;
    shallow_color = id < 0.4f ? _ShallowColor4.xyz : shallow_color;
    shallow_color = id < 0.2f ? _ShallowColor5.xyz : shallow_color;

    float3 shadow_color = _ShadowColor.xyz;
    shadow_color = id < 0.8f ? _ShadowColor2.xyz : shadow_color;
    shadow_color = id < 0.6f ? _ShadowColor3.xyz : shadow_color;
    shadow_color = id < 0.4f ? _ShadowColor4.xyz : shadow_color;
    shadow_color = id < 0.2f ? _ShadowColor5.xyz : shadow_color;
    

    shallow_color = normalize_color(shallow_color, temp0);
    shadow_color = normalize_color(shadow_color, temp1);


    float3 post_shallow;
    post_shallow = _PostShallowTint.xyz * shallow_color;
    shallow_color = _PostShallowFadeTint.xyz * shallow_color;

    float3 post_shadow;
    post_shadow = _PostShadowTint.xyz * shadow_color;
    shadow_color = _PostShadowFadeTint.xyz * shadow_color;
    temp4.xyz = 1.17549435e-38 + 1;
    shadow_thresholds.y = max(temp4.x, temp4.y);
    shadow_thresholds.y = max(shadow_thresholds.y, temp4.z);
    shadow_thresholds.y = rcp(shadow_thresholds.y);
    
    float3 post_fss_tint;
    post_fss_tint = _PostFrontTint.xyz * albedo_step;
    post_fss_tint = _PostSssTint.xyz * albedo_smoothness + post_fss_tint;
    post_fss_tint = temp0.www * temp1.zzz + post_fss_tint;
    post_shadow = post_shadow * shadow_thresholds.xxx;

    float3 final_color;
    final_color = shallow_color * inverse_smoothness + post_shadow;
    final_color = post_shallow * shadow_thresholds.zzz + final_color;
    final_color = final_color;
    final_color = post_fss_tint + final_color;
    

    return float4(saturate(final_color), shad);
}

float3 shadow_face(float3 normal, float3 light, float4 tex_data, float id, float shad_area, float face_value)
{
    float3 light_direction = light;


	float4 shadow_thresholds,temp0,temp1,temp2,temp3,temp4;
    

    float albedo_smoothness = (face_value < 0.5) ? _AlbedoSmoothness : 0.025;
    albedo_smoothness =  max(0.00000999999975f, _AlbedoSmoothness); // fuck whoever gets on my ass about this being too small of a number and should be 0
    float inverse_smoothness = rcp(albedo_smoothness);
    float ndotl = shad_area;
    shadow_thresholds.x = 1;
    float shadow_area = ndotl;
    float albedo_step = -albedo_smoothness * 3 + 2;
    albedo_step = 3 / albedo_step;
    shadow_thresholds.yz = albedo_smoothness * float2(0.5,1.5) + float2(-0.333299994,0.333299994);
    shadow_thresholds.x = -1;
    shadow_thresholds.xyz = -shadow_thresholds.xyz + shadow_area;
    temp0.xyw = shadow_thresholds.xyz * albedo_step;
    shadow_thresholds.xyz = -shadow_thresholds.xyz * albedo_step + float3(1,1,1);
    temp1.xyz = float3(0.333299994,-0.333299994,-0.333299994) + shadow_area;
    temp1.xyz = temp1.xyz * inverse_smoothness + float3(0.5,0.5,-0.5);
    temp2.xyz = float3(1,1,1) + -temp1.xyz;
    temp3.xy = min(temp2.yx, temp0.yx);
    temp0.xz = min(temp1.xz, shadow_thresholds.yz);
    temp3.z = shadow_thresholds.x;
    temp3.w = temp0.x;
    shadow_thresholds.xyz = saturate(temp3.zyw);
    temp3.y = saturate(min(temp2.z, temp1.y));
    temp3.x = saturate(temp3.x);
    temp0.zw = saturate(temp0.zw);
    temp1.xyzw = 1.0f * float4(-2,2,2,-2) + float4(1,0,-1,2);
    temp1.y = saturate(min(temp1.y, temp1.w));
    temp1.xz = saturate(temp1.xz);
    
        
    temp0.xy = temp1.xy;
    inverse_smoothness = 1 + -shadow_thresholds.x;
    inverse_smoothness = inverse_smoothness + -shadow_thresholds.y;
    inverse_smoothness = inverse_smoothness + -shadow_thresholds.z;
    inverse_smoothness = temp0.x * inverse_smoothness + shadow_thresholds.z;
    albedo_smoothness = temp1.y + temp1.z;
    shadow_thresholds.zw = temp3.xy * albedo_smoothness;
    albedo_smoothness = temp0.z + temp0.w;
    albedo_smoothness = albedo_smoothness * temp0.y + shadow_thresholds.w;
    albedo_step = temp1.z * temp0.z;
    shadow_thresholds.x =  shadow_thresholds.x;
    shadow_thresholds.x = shadow_thresholds.y + shadow_thresholds.x;
    
    float2 shad_determine = id.xx < float2(0.6f, 0.8f);

    float3 shallow_color = shad_determine.yyy ? _ShallowColor2.xyz : _ShallowColor.xyz;
    shallow_color.xyz = shad_determine.xxx ? _ShallowColor3.xyz : shallow_color.xyz;
    float3 shadow_color = shad_determine.yyy ? _ShadowColor2.xyz : _ShadowColor.xyz;
    shadow_color.xyz = shad_determine.xxx ? _ShadowColor3.xyz : shadow_color.xyz;

    // float3 shallow_color = _ShallowColor.xyz;
    // shallow_color = id < 0.8f ? _ShallowColor2.xyz : shallow_color;
    // shallow_color = id < 0.6f ? _ShallowColor3.xyz : shallow_color;
    // shallow_color = id < 0.4f ? _ShallowColor4.xyz : shallow_color;
    // shallow_color = id < 0.2f ? _ShallowColor5.xyz : shallow_color;

    // float3 shadow_color = _ShadowColor.xyz;
    // shadow_color = id < 0.8f ? _ShadowColor2.xyz : shadow_color;
    // shadow_color = id < 0.6f ? _ShadowColor3.xyz : shadow_color;
    // shadow_color = id < 0.4f ? _ShadowColor4.xyz : shadow_color;
    // shadow_color = id < 0.2f ? _ShadowColor5.xyz : shadow_color;
    

    shallow_color = normalize_color(shallow_color, temp0);
    shadow_color = normalize_color(shadow_color, temp1);


    float3 post_shallow;
    post_shallow = _PostShallowTint.xyz * shallow_color;
    shallow_color = _PostShallowFadeTint.xyz * shallow_color;

    float3 post_shadow;
    post_shadow = _PostShadowTint.xyz * shadow_color;
    shadow_color = _PostShadowFadeTint.xyz * shadow_color;
    temp4.xyz = 1;
    shadow_thresholds.y = max(temp4.x, temp4.y);
    shadow_thresholds.y = max(shadow_thresholds.y, temp4.z);
    shadow_thresholds.y = rcp(shadow_thresholds.y);
    
    float3 post_fss_tint;
    post_fss_tint = _PostFrontTint.xyz * albedo_step;
    post_fss_tint = _PostSssTint.xyz * albedo_smoothness + post_fss_tint;
    post_fss_tint = temp0.www * temp1.zzz + post_fss_tint;
    post_shadow = post_shadow * shadow_thresholds.xxx;
    shadow_thresholds.xyw =  post_shadow;

    float3 final_color;
    final_color = shallow_color * inverse_smoothness + shadow_thresholds.xyw;
    final_color = post_shallow * shadow_thresholds.zzz + final_color;
    final_color = final_color;
    final_color = post_fss_tint + final_color;
    

    // return final_color;
    return saturate(final_color);
}

// rim light and high light functions : 
// face highlight
float3 face_high(float3 normal, float3 light, float3 view, Texture2D smp, float4 face_values)
// expected smp is actually the light map
{
    // assume the normal, light, and view are normalized
    float3 half_vector = normalize(light + view);
    float ndoth = saturate(pow(saturate(dot(normal, half_vector)), 10.0f));
    face_values.w = max(face_values.w, 0.75);

    float face_highlight = saturate(smp.Sample(sampler_linear_repeat, face_values.xy).y-0.5) ;
    // face_highlight = (face_highlight > 0.1) ? 0.0f : face_highlight;
    face_highlight = smoothstep(face_values.w - 0.75, face_values.w + 0.75, face_highlight);
    face_highlight = saturate(face_highlight);
    face_highlight = face_highlight * ((face_values.x > 0.45) && (face_values.x < 0.55));
    if( face_values.z < 0.5f) face_highlight = 0.0f; // if the vertices are actually the eyes/teeth

    return (face_highlight * (ndoth * 10));
    // return face_highlight;
}

float3 ndotv_rim(float3 normal, float3 view, float3 light, float4 other_data, float4 other_data2, float3 color, float4 bit_flag)
{
    // remap ID and check what kind of ID it is 
    float skin_area = Material_Detect(other_data.x).x;
    float4 color_check = (other_data.xxxx < float4(0.200000003,0.400000006,0.600000024,0.800000012));
    // create 
    // assuming the normal and view are already normalized
    float4 rim; // rim
    float4 r10 = 1;
    float4 r7 = 1;
    float4 r6 = 1;
    float4 r4;
    float4 r2;
    float4 r3;
    float4 r1;
    float4 r0 = 1;
    rim.x = dot(view.xyz, -light.xyz); // vdotl
    rim.x = pow(-rim.x * 0.5 + 0.5, 2);
    rim.x = pow(rim.x, 2.0);
    rim.y = rim.x * 0.5 + 0.5;
    rim.z = (lerp(normal.y, 1, 0.5)) * 0.5 + 0.5;
    r3.x = pow(rim.z, 2.0);
    rim.z = saturate(skin_area ? rim.z : r3.x);
    r3.x = rim.z * -2 + 3;
    rim.z = rim.z * rim.z;
    rim.z = r3.x * rim.z;
    r3.x = rim.z * rim.z;
    r3.x = r3.x * r3.x;
    r3.x = r3.x * rim.z;
    float3 something;
    something.xyz = skin_area ? float3(1,0.5,-1) : float3(0.9,1,-0.9);
    r3.y = something.y + something.z;
    r3.x = r3.x * r3.y + something.x;
    rim.z = r3.x * rim.z;
    r3.x = 1 * 1;
    r3.x = r3.x * 0.949999988 + 0.0500000007;

    r3.y = 1;
    rim.y = rim.y * rim.z;
    rim.y = rim.y * r3.x;
    rim.y = rim.y * r3.y;
    r0.w = sqrt(r0.w);
    rim.z = 0.0833333358 * r0.w;
    rim.z = min(1, rim.z);
    r3.xy = rim.zz * float2(-0.5,-0.5) + float2(0.75,0.5);
    rim.z = dot(view, normal);
    rim.z = 1 + -rim.z;
    r3.xy = rim.zz + -r3.xy;
    r3.xy = saturate(float2(5.00000048,3.33333325) * r3.xy); // original values: (5.00000048,3.33333325)
    r3.zw = r3.xy * float2(-2,-2) + float2(3,3);
    r3.xy = r3.xy * r3.xy;
    r3.xy = r3.zw * r3.xy;
    rim.z = skin_area ? r3.x : r3.y;
    float3 sun_color; // r3.xyz
    sun_color.xyz = color_check.www ? _UISunColor2.xyz : _UISunColor.xyz; // UISunColor
    sun_color.xyz = color_check.zzz ? _UISunColor3.xyz : sun_color.xyz;
    sun_color.xyz = color_check.yyy ? _UISunColor4.xyz : sun_color.xyz;
    sun_color.xyz = color_check.xxx ? _UISunColor5.xyz : sun_color.xyz;
    sun_color.xyz = skin_area ? sun_color.xyz : dot(sun_color, float3(0.300000012,0.600000024,0.100000001));
    float grey; // r3.w
    grey = skin_area ? dot(sun_color, float3(0.330000013,0.330000013,0.330000013)) : dot(sun_color, float3(0.300000012,0.600000024,0.100000001));
    sun_color = pow(sun_color, 8.0f);
    grey = rcp(6.10351563e-005 + dot(sun_color, (float3)0.7f)) * grey;
    sun_color = grey * sun_color + -r7.xyz;
    sun_color = 1 * sun_color + r7.xyz;
    rim.x = pow(rim.x, 20);

    something.xyz = r7.xyz + -sun_color;
    sun_color = rim.xxx * something.xyz + sun_color;
    rim.x = 1;
    r1.z = 0 * 1 + rim.x;
    r1.z = 0.330000013 * r1.z;
    r1.z = r1.z * r1.z;
    r1.z = r1.z * -0.199999988 + 1;
    rim.x = 1;
    r1.z = 0.100000001 * r1.z;

    something.xyz = 1.0f;

    something.xyz = normalize(something.xyz);
    r6.xyz = something.xyz * r1.zzz;
    something.xyz = -r1.zzz * something.xyz + r10.xyz;
    something.xyz = r0.xxx * something.xyz + r6.xyz;
    something.xyz = something.xyz * rim.xxx;
    r0.x = rim.y * rim.z;
    rim.xyz = r0.xxx ;
    rim.xyz = rim.xyz * something.xyz;
    float3 rg_color; // r3
    rg_color.xyz = color_check.www ? _RimGlowLightColor2.xyz : _RimGlowLightColor.xyz; // this is the rim light color
    rg_color.xyz = color_check.zzz ? _RimGlowLightColor3.xyz : rg_color.xyz;
    rg_color.xyz = color_check.yyy ? _RimGlowLightColor4.xyz : rg_color.xyz;
    rg_color.xyz = color_check.xxx ? _RimGlowLightColor5.xyz : rg_color.xyz;
    sun_color.xyz = rg_color.xyz * sun_color.xyz;
    r0.x = saturate(r0.w * 0.200000003 + -1);
    r0.x = r0.x * -0.699999988 + 1;
    rim.xyw = rim.xyz * r0.xxx;
    r0.w = rim.x + rim.y;
    r0.x = rim.z * r0.x + r0.w;
    r0.x = 0.330000013 * r0.x;
    r0.x = r0.x * r0.x;
    r0.x = r0.x * 0.5 + 1;
    rim.xyz = rim.xyw * r0.xxx;
    r0.x = (0.5 < bit_flag.z) ? 1 : 1;
    rim.xyz = rim.xyz * r0.xxx * sun_color;



    return rim;
}

float rim_screen_mask(float3 normal, float3 light, float4 ss_pos, float3 ws_pos)
{
    float ndotl = dot(normal, light);
    // ndotl = smoothstep(0.0, 0.5, ndotl);
    float2 screen = (ss_pos.xy / ss_pos.w);

    float3 vs_normal = mul((float3x3)UNITY_MATRIX_V, normal);

    float depth = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screen), screen);

    float camera_dist = saturate(1.0f / distance(_WorldSpaceCameraPos.xyz, ws_pos));
    float fov = extract_fov();
    fov = clamp(fov, 0, 150);
    float range = fov_range(0, 180, fov);
    float width_depth = camera_dist / range;

    float3 rim_width = _RimWidth * 0.0025f;
    rim_width = lerp(rim_width * 0.5f, rim_width * 0.45f, range) * width_depth;

    float2 offset_pos = screen;
    offset_pos = offset_pos + (rim_width * vs_normal);

    float offset = GetLinearZFromZDepth_WorksWithMirrors(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, offset_pos.xy), screen); 

    float3 rim = ((offset-depth) );


    return rim * ndotl;    
}

// matcap functions: 
// first spherical mapping
float2 sphere_uv(float3 normal)
{
    float2 sphere_uv = mul(normal, (float3x3)UNITY_MATRIX_I_V ).xy;
    sphere_uv.x = sphere_uv.x ; 
    sphere_uv = sphere_uv * 0.5f + 0.5f;
    return sphere_uv;   
}

int matcap_id(float other_data)
{
    float id = other_data.r;
    id = id * 5.0f;
    id = floor(id);
    id = 4.0f - id;
    id = max(0.0f, id);
    return (int)id;
}

float4 matcap_body(float3 normal, float other_data, float other_data2, float2 uv, float4 color)
{

    // arrays
    float matcap_ID[5] =
    {
        _MatCapTexID < 99.0f ? _MatCapTexID : 0,
        _MatCapTexID < 99.0f ? _MatCapTexID : 1,
        _MatCapTexID < 99.0f ? _MatCapTexID : 2,
        _MatCapTexID < 99.0f ? _MatCapTexID : 3,
        _MatCapTexID < 99.0f ? _MatCapTexID : 4
    };
    
    float refract_check[5] = 
    {
        _MatCapRefract,
        _MatCapRefract2,
        _MatCapRefract3,
        _MatCapRefract4,
        _MatCapRefract5
    };

    float refrat_depthj[5] = 
    {
        _RefractDepth,
        _RefractDepth2,
        _RefractDepth3,
        _RefractDepth4,
        _RefractDepth5
    };
    
    float4 refract_param[5] =
    {
        _RefractParam,
        _RefractParam2,
        _RefractParam3,
        _RefractParam4,
        _RefractParam5
    };
    
    float4 _MatCapColorTintArray[5] = 
    {
        _MatCapColorTint,
        _MatCapColorTint2,
        _MatCapColorTint3,
        _MatCapColorTint4,
        _MatCapColorTint5
    };

    float u_speed[5] = 
    {
        _MatCapUSpeed,
        _MatCapUSpeed2,
        _MatCapUSpeed3,
        _MatCapUSpeed4,
        _MatCapUSpeed5
    };

    float v_speed[5] = 
    {
        _MatCapVSpeed,
        _MatCapVSpeed2,
        _MatCapVSpeed3,
        _MatCapVSpeed4,
        _MatCapVSpeed5
    };

    float color_burst[5] = 
    {
        _MatCapColorBurst,
        _MatCapColorBurst2,
        _MatCapColorBurst3,
        _MatCapColorBurst4,
        _MatCapColorBurst5
    };
    
    float alpha_burst[5] = 
    {
        _MatCapAlphaBurst,
        _MatCapAlphaBurst2,
        _MatCapAlphaBurst3,
        _MatCapAlphaBurst4,
        _MatCapAlphaBurst5
    };

    float blend_mode[5] = 
    {
        _MatCapBlendMode,
        _MatCapBlendMode2,
        _MatCapBlendMode3,
        _MatCapBlendMode4,
        _MatCapBlendMode5
    };

    float4 matcap_result = float4(0,0,0,0);

    if (_MatCap)
    {
       int matcap_id = (int)max(4.0f + -floor(5.0f * other_data), 0.0f);

        if(matcap_ID[matcap_id] < 50)
        {
            float mask = (0.0f) ? saturate(5.0999999 * other_data2) * (0.200000003 >= other_data2) : other_data2; // i fucking hate this

            float2 matcap_uv = sphere_uv(normal);
            

            if(refract_check[matcap_id])
            {
                float2 refract_uv = refract_param[matcap_id].xy * uv + refract_param[matcap_id].zw;
                matcap_uv = refrat_depthj[matcap_id] * matcap_uv + refract_uv;           
            }

            matcap_uv = float2(u_speed[matcap_id] , v_speed[matcap_id]) * _Time.yy + matcap_uv;

            float4 matcap_array[5] =
            {
                _MatCapTex.Sample(sampler_linear_repeat, matcap_uv),
                _MatCapTex2.Sample(sampler_linear_repeat, matcap_uv),
                _MatCapTex3.Sample(sampler_linear_repeat, matcap_uv),
                _MatCapTex4.Sample(sampler_linear_repeat, matcap_uv),
                _MatCapTex5.Sample(sampler_linear_repeat, matcap_uv)
            };

            float4 matcap = matcap_array[matcap_id];

            float3 tinted_matcap = matcap.xyz * _MatCapColorTintArray[matcap_id].xyz;
            float matcap_alpha = saturate(matcap.w * mask);
            if(blend_mode[matcap_id] < 0.5f) // alpha blended
            {  
                float3 tmp = tinted_matcap * color_burst[matcap_id] + -color.xyz;
                color.xyz = saturate(alpha_burst[matcap_id] * matcap_alpha) * tmp + color.xyz;
            }
            else
            {
                if((blend_mode[matcap_id] < 1.5f)) // add
                {
                    float3 tmp = tinted_matcap * saturate(alpha_burst[matcap_id] * matcap_alpha);
                    color.xyz = tmp * color_burst[matcap_id] + color.xyz;
                }
                else // overlay
                {
                    float3 tmp = matcap * _MatCapColorTintArray[matcap_id].xyz;
                    tinted_matcap = saturate(tmp * color_burst[matcap_id] + tinted_matcap);
                    float mat_z = matcap_alpha;
                    tinted_matcap = lerp((float3)0.5f, tinted_matcap, mat_z);

                    float3 dark_result = 2.0f * color.xyz * tinted_matcap.xyz; // 2 * base * blend
                    float3 light_result = 1.0f - 2.0f * (1.0f - color.xyz) * (1.0f - tinted_matcap.xyz); // 1 - 2 * (1 - base) * (1 - blend)
                    float3 is_base_light = (color.xyz >= float3(0.5,0.5,0.5)); // Check if base color is >= 0.5
                    
                    // Select appropriate result based on base color brightness
                    color.xyz = (lerp(dark_result, light_result, is_base_light));
                }
            }
            
        }
    }
    return color;
}

// outline related functions :
// a not totally accurate nose line implementation but because of their
// reliance on scripting, this is the best I can do without requiring extra
float3 nose_line(float3 view, float3 normal, float id, float nose_tex)
{
    // assuming view is normalized
    float ndotv = pow(dot(normal, view), 10);
    
    float2 color_check = (id.xx < float2(0.600000024,0.800000012));
    float3 color = color_check.yyy ? _OutlineColor2.xyz : _OutlineColor.xyz;
    color.xyz = color_check.xxx ? _OutlineColor3.xyz : color.xyz;

    color = pow(color*0.5, 2.1);

    float noseline = smoothstep(_NoseSmoothX, _NoseSmoothY, ndotv) - nose_tex;

    float3 nose_color = lerp(1.0f, color, saturate(noseline));

    return nose_color;
}

float4 outline_color(float4 diffuse, float id, float3 normal, float3 light)
{
    // lie more to me 
    float ndotl = saturate(dot(normal, light));
    float shadow_area = smoothstep(0.2800001, 0.3800001, ndotl);
    float red = ( ndotl  * 0.5 + 0.5);

    float4 color = (float4)1.0f;
    float4 shadow = (float4)0.9f;
    if(_MaterialType == 1)
    {
        float2 color_check =  (id.xx < float2(0.600000024,0.800000012));
        color.xyz = color_check.yyy ? _OutlineColor2.xyz : _OutlineColor.xyz;
        color.xyz = color_check.xxx ? _OutlineColor3.xyz : color.xyz;

    }
    else
    {
        float4 color_check = (id.xxxx < float4(0.200000003,0.400000006,0.600000024,0.800000012));
        color.xyz = color_check.www ? _OutlineColor2.xyz : _OutlineColor.xyz;
        color.xyz = color_check.zzz ? _OutlineColor3.xyz : color.xyz;
        color.xyz = color_check.yyy ? _OutlineColor4.xyz : color.xyz;
        color.xyz = color_check.xxx ? _OutlineColor5.xyz : color.xyz;

    }
    color = pow(color*0.5, 1.5);
    color = saturate(color);
    color.w = 1.0f;
    shadow.xyz = color.xyz * 0.01f;
    color.xyz = lerp(shadow*diffuse, color*diffuse, saturate(red));
    // color.xyz = float3(0.0, 0.0, 0.0);
    return color;
}