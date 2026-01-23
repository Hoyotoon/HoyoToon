struct vertex_input
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv2  : TEXCOORD1;
    float2 uv3  : TEXCOORD2;
    float2 uv4  : TEXCOORD3;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct vertex_output
{
    float4 vertex : SV_POSITION;
    float4 uv : TEXCOORD0;  
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
    float4 view : TEXCOORD3;
    float4 screenpos : TEXCOORD5;
    float4 color : COLOR0;
    float3 normal : NORMAL0;

    SHADOW_COORDS(12)   
};