#ifndef PRE_Z_CGINC
#define PRE_Z_CGINC

#include "UnityCG.cginc"

struct v2f
{
    float4 vertex : SV_POSITION;
};

struct VertexData
{
    float3 position : POSITION;
};

v2f vert(VertexData vertex)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(vertex.position);
    return o;
}

[earlydepthstencil]
fixed4 frag(v2f i) : SV_Target
{
    return 0.0f;
}

#endif // PRE_Z_CGINC