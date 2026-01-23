// 1. PLACE STRUCTS AT THE TOP
struct PN_Control_Patch {
    float3 p300, p030, p003;
    float3 p210, p120, p021, p012, p102, p201;
    float3 p111;
    float4 n200; // xyz: smooth normal, w: sharpness
    float4 n020; // xyz: smooth normal, w: sharpness
    float4 n002; // xyz: smooth normal, w: sharpness
};
struct control_struct 
{
    float4 vertex : INTERNAL_POS;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
    float2 uv3 : TEXCOORD2;
    float2 uv4 : TEXCOORD3;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct tess_struct 
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

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
    float4 uv        : TEXCOORD0;
    float4 uv1       : TEXCOORD1;
    float4 uv2       : TEXCOORD2;
    float4 color     : TEXCOORD3;
    float3 normal    : TEXCOORD4;
    float4 n_view    : TEXCOORD5;
    float3 view      : TEXCOORD6;
    float4 ws_pos    : TEXCOORD7;
    float4 screenpos : TEXCOORD8;
    float4 pos       : TEXCOORD9;
    float4 faceangle : TEXCOORD10;
    float4 os_pos    : TEXCOORD11;
    float4 vertex  : SV_POSITION;
    SHADOW_COORDS(12)   
};