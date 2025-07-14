struct vs_in
{
    float4 vertex     : POSITION;
    float3 normal  : NORMAL;
    float4 tangent : TANGENT;
    float2 uv_0    : TEXCOORD0;
    float2 uv_1    : TEXCOORD1;
    float2 uv_2    : TEXCOORD2;
    float2 uv_3    : TEXCOORD3;
    float4 v_col   : COLOR0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
}; 

struct vs_out
{
    float4 pos       : SV_POSITION;
    float3 normal    : NORMAL; // ws normals
    float4 tangent   : TANGENT; // ws tangents
    float4 uv_a      : TEXCOORD0; // uv0 and uv1
    float4 uv_b      : TEXCOORD1; // uv2 and uv3
    float3 view      : TEXCOORD2; // view vector
    float4 ws_pos    : TEXCOORD3; // world space position
    float4 ss_pos    : TEXCOORD4; // screen space position
    float3 parallax  : TEXCOORD5;
    float4 light_pos : TEXCOORD6;
    float4 n_view    : TEXCOORD8;
    float4 ppos      : TEXCOORD9;
    float4 v_col     : COLOR0; // vertex color 
    UNITY_VERTEX_OUTPUT_STEREO
    SHADOW_COORDS(7)
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

struct light_in
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv_0 : TEXCOORD0;
    float2 uv_1 : TEXCOORD1;
};

struct light_out
{
    float4 pos : SV_POSITION; 
    float3 normal : NORMAL;
    float4 uv_a : TEXCOORD0;
    float4 ws_pos : TEXCOORD1;
    float3 view   : TEXCOORD2;
    SHADOW_COORDS(5)
};