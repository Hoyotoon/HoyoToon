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
    float4 pos     : SV_POSITION;
    float3 normal  : NORMAL; // ws normals
    float4 tangent : TANGENT; // ws tangents
    float4 uv      : TEXCOORD0; // uv0 and uv1
    float3 view    : TEXCOORD1; // view vector
    float4 ws_pos  : TEXCOORD2; // world space position, this is used to sample the camera depth texture 
    float4 ss_pos  : TEXCOORD3;
    float4 vertex    : TEXCOORD4; 
    float4 dis_uv  : TEXCOORD5; // dissolve uv and distortion uv
    float4 dis_pos : TEXCOORD6;
    float4 uv_2    : TEXCOORD7;
    float4 v_col   : COLOR0; // vertex color 
    UNITY_VERTEX_OUTPUT_STEREO
    SHADOW_COORDS(8)
};

struct shadow_in 
{
    float4 vertex  : POSITION;
    float3 normal  : NORMAL;
    float4 tangent : TANGENT;
    float2 uv_0    : TEXCOORD0;
    float2 uv_1    : TEXCOORD1;
    float2 uv_2    : TEXCOORD2;
    float2 uv_3    : TEXCOORD3;
    float4 v_col   : COLOR0;
};

struct shadow_out
{
    float4 pos : SV_POSITION;
    float4 uv_a : TEXCOORD0;
    float3 normal : NORMAL;
    float4 ws_pos : TEXCOORD1;
    float3 view : TEXCOORD2;
    float4 dis_uv : TEXCOORD3;
    float4 dis_pos : TEXCOORD4;
    float hide : TEXCOORD5;
};
