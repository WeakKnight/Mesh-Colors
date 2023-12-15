#ifndef MESH_COLOR_CGINC
#define MESH_COLOR_CGINC

StructuredBuffer<uint> _MeshColor_PatchBuffer;
StructuredBuffer<uint2> _MeshColor_MetaBuffer;

struct MeshColor_MetaInfo
{
    uint address;
    uint resolution;
};

MeshColor_MetaInfo MeshColor_LoadMetaInfo(uint primitiveIndex)
{
    MeshColor_MetaInfo metaInfo;
    metaInfo.address = _MeshColor_MetaBuffer[primitiveIndex].x;
    metaInfo.resolution = _MeshColor_MetaBuffer[primitiveIndex].y;
    return metaInfo;
}

uint3 MeshColor_B(float3 bary, uint resolution)
{
    return (uint3)((float)resolution * bary);
}

float3 MeshColor_W(float3 bary, uint resolution)
{
    return (float)resolution * bary - MeshColor_B(bary, resolution);
}

float MeshColor_UnpackR8ToUFLOAT(uint r)
{
    const uint mask = (1U << 8) - 1U;
    return (float)(r & mask) / (float)mask;
}

float4 MeshColor_UnpackR8G8B8A8ToUFLOAT(uint rgba)
{
    float r = MeshColor_UnpackR8ToUFLOAT(rgba);
    float g = MeshColor_UnpackR8ToUFLOAT(rgba >> 8);
    float b = MeshColor_UnpackR8ToUFLOAT(rgba >> 16);
    float a = MeshColor_UnpackR8ToUFLOAT(rgba >> 24);
    return float4(r, g, b, a);
}

float4 MeshColor_C(uint i, uint j, uint resolution, uint baseAddress)
{
    uint offset = i * ((resolution + 1) + (resolution + 2 - i)) / 2;
    uint address = baseAddress + offset + j;

    return MeshColor_UnpackR8G8B8A8ToUFLOAT(_MeshColor_PatchBuffer[address]);
}

float4 MeshColor_Sample(uint primitiveIndex, float3 bary)
{
    MeshColor_MetaInfo metaInfo = MeshColor_LoadMetaInfo(primitiveIndex);

    uint2 ij = MeshColor_B(bary, metaInfo.resolution).xy;
    uint i = ij.x;
    uint j = ij.y;

    float4 col;
#if 1 // Bilinear
    float3 weight = MeshColor_W(bary, metaInfo.resolution);
    if (weight.x + weight.y + weight.z > 1.9f)
    {
        weight = 1.0f - weight;
        col = weight.x * MeshColor_C(i, j + 1, metaInfo.resolution, metaInfo.address)
        + weight.y * MeshColor_C(i + 1, j, metaInfo.resolution, metaInfo.address)
        + weight.z * MeshColor_C(i + 1, j + 1, metaInfo.resolution, metaInfo.address);
    }
    else
    {
        col = weight.x * MeshColor_C(i + 1, j, metaInfo.resolution, metaInfo.address) 
        + weight.y * MeshColor_C(i, j + 1, metaInfo.resolution, metaInfo.address)
        + weight.z * MeshColor_C(i, j, metaInfo.resolution, metaInfo.address);
    }
#else
    col = MeshColor_C(i, j, metaInfo.resolution, metaInfo.address);
#endif

    return col;
}

#endif // MESH_COLOR_CGINC