struct vertex_in
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float3 normal : NORMAL;
    float3 tangent : TANGENT;
    float4 color : COLOR;
};

struct vertex_out
{
    float4 vertex : SV_POSITION;
    float4 uv : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    float4 uv2 : TEXCOORD2;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float3 view : TEXCOORD3;
    float4 ss_pos : TEXCOORD4;
    float3 ws_pos : TEXCOORD5;
    float4 packed : TEXCOORD6;
};

struct buffer_out
{
    float4 forward : SV_Target0;
    float4 normal : SV_Target1;
    float4 shadow : SV_Target2;
};