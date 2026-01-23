#ifndef VRC_TEST_INCLUDE
#define VRC_TEST_INCLUDE

// Standard function available in all modes
float GetStandardMultiplier()
{
    return 1.0;
}

/*#VRC// VRChat-specific helper functions
float GetVRChatMultiplier()
{
    return 1.5;
}

float3 ApplyVRChatLighting(float3 color)
{
    return color * 1.2;
}

float4 VRChatSpecialEffect(float4 input)
{
    return input * float4(1, 1.2, 1, 1);
}#VRC_END*/

// Another standard function
float3 NormalizeColor(float3 color)
{
    return normalize(color);
}

/*#VRC// More VRChat features
#define VRC_ENABLED 1
#define VRC_FEATURE_LEVEL 3

float GetVRCFeatureLevel()
{
    return VRC_FEATURE_LEVEL;
}#VRC_END*/

#endif