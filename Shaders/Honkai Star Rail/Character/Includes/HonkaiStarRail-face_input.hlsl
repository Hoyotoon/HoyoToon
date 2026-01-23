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
    float2 uv         : TEXCOORD0;
    float2 uv2        : TEXCOORD1;
    float4 color      : TEXCOORD2;
    float3 normal     : TEXCOORD3;
    float3 view       : TEXCOORD4;
    float4 ws_pos     : TEXCOORD5;
    float4 face_veca  : TEXCOORD6;
    float4 diss_uv    : TEXCOORD7;
    float4 diss_pos   : TEXCOORD8; // z is the dither rate
    float4 screenpos : TEXCOORD9;
    float4 hairpos     : TEXCOORD11;
    float4 vertex     : SV_POSITION;
    SHADOW_COORDS(10)   
};