struct vs_in 
{
    float4 vertex  : POSITION;
    float3 normal  : NORMAL;
    float4 tangent : TANGENT;
    float2 uv_0    : TEXCOORD0;
    float2 uv_1    : TEXCOORD1;
    float2 uv_2    : TEXCOORD2;
    float2 uv_3    : TEXCOORD3;
    float4 color   : COLOR0;
};  

struct vs_out
{
    float4 pos        : SV_POSITION;
    float3 normal     : NORMAL;
    float4 tangent    : TANGENT;
    float4 uv_a       : TEXCOORD0;
    float4 uv_b       : TEXCOORD1;
    float3 view       : TEXCOORD2;
    float4 ws_pos     : TEXCOORD3;
    SHADOW_COORDS(4)
    float4 bit_flag   : TEXCOORD5;
    float4 eye_shadow : TEXCOORD6;
    float4 pos_model  : TEXCOORD7;
    float4 ss_model   : TEXCOORD8;
    float4 test       : TEXCOORD9;
    float4 pos_wvp    : TEXCOORD10;
    float4 pos_grab   : TEXCOORD11;
    float4 color      : COLOR0;
};  

struct shadow_in 
{
    float4 vertex : POSITION; 
    float3 normal : NORMAL;
    float2 uv_0 : TEXCOORD0;
    float2 uv_1 : TEXCOORD1;
};

struct shadow_out
{
    float4 pos : SV_POSITION;
    float4 uv_a : TEXCOORD0;
    float3 normal : NORMAL;
    float4 ws_pos : TEXCOORD1;
    float3 view : TEXCOORD2;
};
