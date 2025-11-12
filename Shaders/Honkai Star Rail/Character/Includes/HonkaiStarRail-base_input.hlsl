struct vertex_input
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv2  : TEXCOORD1;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct vertex_output
{
    float2 uv        : TEXCOORD0;
    float2 uv2       : TEXCOORD1;
    float4 color     : TEXCOORD2;
    float3 normal    : TEXCOORD3;
    float4 tangent   : TEXCOORD4;
    float3 view      : TEXCOORD5;
    float4 ws_pos    : TEXCOORD6;
    float4 screenpos : TEXCOORD7;
    float4 opos      : TEXCOORD8;
    float4 diss_uv   : TEXCOORD9;
    float4 diss_pos  : TEXCOORD10;
    UNITY_FOG_COORDS(10)
    float4 vertex  : SV_POSITION;
};