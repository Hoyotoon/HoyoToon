struct vertex_input
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float4 color   : COLOR0;
};

struct vertex_output
{
    float4 vertex    : SV_POSITION;
    float3 normal    : NORMAL0;
    float4 screenpos : TEXCOORD1;
    float3 parallax  : TEXCOORD2;
    float4 uv_set_A  : TEXCOORD3;
    float4 uv_set_B  : TEXCOORD4;
    float4 uv_set_C  : TEXCOORD5;
    float4 uv_set_D  : TEXCOORD6;
    float4 heights   : TEXCOORD7;
    float4 color     : COLOR0;
};