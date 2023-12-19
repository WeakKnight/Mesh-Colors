#ifndef MESH_COLORS_CGINC
#define MESH_COLORS_CGINC

StructuredBuffer<uint> _MeshColors_PatchBuffer;
StructuredBuffer<uint2> _MeshColors_MetaBuffer;

struct MeshColors_MetaInfo
{
    uint address;
    uint resolution;
};

MeshColors_MetaInfo MeshColors_LoadMetaInfo(uint primitiveIndex)
{
    MeshColors_MetaInfo metaInfo;
    metaInfo.address = _MeshColors_MetaBuffer[primitiveIndex].x;
    metaInfo.resolution = _MeshColors_MetaBuffer[primitiveIndex].y;
    return metaInfo;
}

uint3 MeshColors_B(float3 bary, uint resolution)
{
    return (uint3)((float)resolution * bary);
}

float3 MeshColors_W(float3 bary, uint resolution)
{
    return (float)resolution * bary - MeshColors_B(bary, resolution);
}

float MeshColors_UnpackR8ToUFLOAT(uint r)
{
    const uint mask = (1U << 8) - 1U;
    return (float)(r & mask) / (float)mask;
}

float4 MeshColors_UnpackR8G8B8A8ToUFLOAT(uint rgba)
{
    float r = MeshColors_UnpackR8ToUFLOAT(rgba);
    float g = MeshColors_UnpackR8ToUFLOAT(rgba >> 8);
    float b = MeshColors_UnpackR8ToUFLOAT(rgba >> 16);
    float a = MeshColors_UnpackR8ToUFLOAT(rgba >> 24);
    return float4(r, g, b, a);
}

float4 MeshColors_C(uint i, uint j, uint resolution, uint baseAddress)
{
    uint offset = i * ((resolution + 1) + (resolution + 2 - i)) / 2;
    uint address = baseAddress + offset + j;

    return MeshColors_UnpackR8G8B8A8ToUFLOAT(_MeshColors_PatchBuffer[address]);
}

float4 MeshColors_Sample(uint primitiveIndex, float3 bary)
{
    MeshColors_MetaInfo metaInfo = MeshColors_LoadMetaInfo(primitiveIndex);

    uint2 ij = MeshColors_B(bary, metaInfo.resolution).xy;
    uint i = ij.x;
    uint j = ij.y;

    float4 col;
#if 1 // Bilinear
    float3 weight = MeshColors_W(bary, metaInfo.resolution);
    if (weight.x + weight.y + weight.z > 1.9f)
    {
        weight = 1.0f - weight;
        col = weight.x * MeshColors_C(i, j + 1, metaInfo.resolution, metaInfo.address)
        + weight.y * MeshColors_C(i + 1, j, metaInfo.resolution, metaInfo.address)
        + weight.z * MeshColors_C(i + 1, j + 1, metaInfo.resolution, metaInfo.address);
    }
    else
    {
        col = weight.x * MeshColors_C(i + 1, j, metaInfo.resolution, metaInfo.address) 
        + weight.y * MeshColors_C(i, j + 1, metaInfo.resolution, metaInfo.address)
        + weight.z * MeshColors_C(i, j, metaInfo.resolution, metaInfo.address);
    }
#else
    col = MeshColors_C(i, j, metaInfo.resolution, metaInfo.address);
#endif

    return col;
}

#endif // MESH_COLORS_CGINC