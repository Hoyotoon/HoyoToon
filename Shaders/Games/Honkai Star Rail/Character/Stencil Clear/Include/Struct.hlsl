struct vertex_in
{
    float4 vertex  : POSITION;
    float2 uv      : TEXCOORD0;
    float2 uv1     : TEXCOORD1;
    float2 uv7     : TEXCOORD6;
    float2 uv8     : TEXCOORD7;
    float3 normal  : NORMAL;
    float4 tangent : TANGENT;
    float4 color   : COLOR;
};

struct vertex_out
{
    float4 vertex  : SV_POSITION;
    float4 uv      : TEXCOORD0;
    float3 normal  : NORMAL;
    float4 color   : COLOR;
    float4 view    : TEXCOORD1;
    float4 ss_pos  : TEXCOORD2;
    float3 ws_pos  : TEXCOORD3;
    float4 sdw_pos : TEXCOORD4;
    #if defined(_DIRECTIONALDISSOLVE)
        float4 diss_uv   : TEXCOORD5;
        float4 diss_pos  : TEXCOORD6; // z is the dither rate
    #endif
    float3 tangent : TEXCOORD7;
    #if defined(_USE_NORMAL_MAP)
        float3 bitangent : TEXCOORD8;
    #endif
    float4 pos : TEXCOORD9;
    float4 os_pos : TEXCOORD10;
};

struct buffer_out
{
    float4 forward  : SV_Target0; // Albedo
    float4 normal  : SV_Target1; // Normals
    float4 ssrMask : SV_Target2; // SSR Mask (R8)
    float4 alphaMask : SV_Target3; // Alpha Mask
};

struct appdata_hsr {
    float4 pos_os : POSITION;
    float2 uv : TEXCOORD0;
    float3 normal_os : NORMAL;
};

struct v2f_hsr {
    float4 pos_cs : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normal_ws : TEXCOORD1;
};