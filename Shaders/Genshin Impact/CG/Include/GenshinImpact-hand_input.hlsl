struct vertex_input
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float3 normal : NORMAL;
};

struct vertex_output
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float3 view : TEXCOORD3;
    float3 normal : NORMAL;
    float3 ws_pos : TEXCOORD5;
    float4 color : COLOR;
};