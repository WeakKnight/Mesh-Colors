#include "UnityCG.cginc"
#include "MeshColors.cginc"

/*******************************************************************************
 * Halfedge data accessors
 *
 */
struct cc_Halfedge {
    int twinID;
    int nextID;
    int prevID;
    int faceID;
    int edgeID;
    int vertexID;
    int uvID;
};

StructuredBuffer<cc_Halfedge> _Halfedges;
Texture2D<float4> _HtexTextureAtlas;
uint _HtexTextureNumQuadsX;
uint _HtexTextureNumQuadsY;
uint _HtexTextureQuadWidth;
uint _HtexTextureQuadHeight;
SamplerState _LinearClampSampler;

cc_Halfedge ccm__Halfedge(int halfedgeID)
{
    return _Halfedges[halfedgeID];
}

int ccm_HalfedgeTwinID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).twinID;
}

int ccm_HalfedgeNextID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).nextID;
}

int ccm_HalfedgePrevID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).prevID;
}

int ccm_HalfedgeVertexID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).vertexID;
}

int ccm_HalfedgeUvID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).uvID;
}

int ccm_HalfedgeEdgeID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).edgeID;
}

int ccm_HalfedgeFaceID(int halfedgeID)
{
    return ccm__Halfedge(halfedgeID).faceID;
}

int ccm_HalfedgeFaceID_Quad(int halfedgeID)
{
    return halfedgeID >> 2;
}

int ccm_EdgeCount()
{
    // TODO
    return 0;
}

float4 SampleQuad(int quadID, float2 xy, int channel)
{
    float2 invNumQuads = 1.0 / float2(_HtexTextureNumQuadsX, _HtexTextureNumQuadsY);
    float2 invQuadSize = 1.0 / float2(_HtexTextureQuadWidth, _HtexTextureQuadHeight);

    if (any(xy < -invQuadSize * 0.5) || any(xy > 1 + invQuadSize * 0.5))
    {
        return 0;
    }

    xy = max(invQuadSize * 0.5, xy);
    xy = min(1 - invQuadSize * 0.5, xy);

    uint numQuadsX = _HtexTextureNumQuadsX;
    uint tileX = quadID % numQuadsX;
    uint tileY = quadID / numQuadsX;
    float2 baseUV = float2(tileX, tileY) * invNumQuads;

    return _HtexTextureAtlas.Sample(_LinearClampSampler, baseUV + xy * invNumQuads);
}

float SampleAlpha(int quadID, float2 xy)
{
    float2 invNumQuads = 1.0 / float2(_HtexTextureNumQuadsX, _HtexTextureNumQuadsY);
    float2 invQuadSize = 1.0 / float2(_HtexTextureQuadWidth, _HtexTextureQuadHeight);

    if (any(xy < -invQuadSize * 0.5) || any(xy > 1 + invQuadSize * 0.5))
    {
        return 0;
    }

    if (any(xy < invQuadSize))
    {
        return 1;

        // TODO
        float2 d = abs(xy) / (invQuadSize * 0.5);
        return min(d.x, d.y);
    }

    if (any(xy > 1 - invQuadSize * 0.5))
    {
        return 1;

        // TODO
        float2 d = abs(xy - 1) / (invQuadSize * 0.5);
        return min(d.x, d.y);
    }

    xy = max(invQuadSize, xy);
    xy = min(1 - invQuadSize, xy);

    return 1;
    // ivec2 res = u_QuadLog2Resolutions[quadID];
    // sampler2D alphaTexture = sampler2D(u_HtexAlphaTextureHandles[res.y*HTEX_NUM_LOG2_RESOLUTIONS+res.x]);
    // return texture(alphaTexture, xy).r;
}

float2 TriangleToQuadUV(int halfedgeID, float2 uv)
{
    int twinID = ccm_HalfedgeTwinID(halfedgeID);

    if (halfedgeID > twinID) {
        return uv;
    } else {
        return 1-uv;
    }
}

float4 debugDisplayID(int ID)
{
    float4 debugColors[4] = {
        float4(1, 0, 0, 1),
        float4(0, 1, 0, 1),
        float4(0, 0, 1, 1),
        float4(1, 0, 1, 1),
    };
    return float4(GammaToLinearSpace(debugColors[ID % 4].xyz), 1);
}

float4 Htexture(int halfedgeID, float2 uv, int channel)
{
    int nextID = ccm_HalfedgeNextID(halfedgeID);
    int prevID = ccm_HalfedgePrevID(halfedgeID);

    float4 c = 0;
    float alpha = 0;

    int quadID;
    float2 xy;
    float w;

    quadID = ccm_HalfedgeEdgeID(halfedgeID);
    xy = TriangleToQuadUV(halfedgeID, uv);
    w = SampleAlpha(quadID, xy);
    c += SampleQuad(quadID, xy, channel) * w;
    alpha += w;

    quadID = ccm_HalfedgeEdgeID(nextID);
    xy = TriangleToQuadUV(nextID, float2(uv.y, -uv.x));
    w = SampleAlpha(quadID, xy);
    c += SampleQuad(quadID, xy, channel) * w;
    alpha += w;

    quadID = ccm_HalfedgeEdgeID(prevID);
    xy = TriangleToQuadUV(prevID, float2(-uv.y, uv.x));
    w = SampleAlpha(quadID, xy);
    c += SampleQuad(quadID, xy, channel) * w;
    alpha += w;

    return c / alpha;
}

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float4 vertex : SV_POSITION;
};


v2f vert(appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.normal = normalize(UnityObjectToWorldNormal(v.normal));
    o.uv = v.uv;
    return o;
}

float4 frag(v2f vertexInput, uint primitiveIndex: SV_PrimitiveID, centroid float3 vBaryWeights: SV_Barycentrics) : SV_Target
{
    float4 color = 0;
    color = Htexture(primitiveIndex, vBaryWeights.yz, 0);
    return float4(color.xyz, 1);
}
