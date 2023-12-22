#ifndef VIRTUAL_MESH_COLORS
#define VIRTUAL_MESH_COLORS

#include "MeshColors.cginc"

/* Remap Info
bitmask: 32 bit, each bit represent occupancy of 1 chunk
256 * 256
*/
RWByteAddressBuffer VMC_OccupancyBuffer : u4;

struct VirtualMeshColorsDesc
{
    uint address;
};

StructuredBuffer<VirtualMeshColorsDesc> VMC_DescBuffer;
StructuredBuffer<MeshColors_MetaInfo> VMC_MetaBuffer;

struct VirtualMeshColors
{
    static const uint ChunkSize = 64;
    static const uint ChunkNumPerGroup = 32;

    static void __MarkColor(uint i, uint j, uint resolution, uint baseAddress)
    {
        uint offset = i * ((resolution + 1) + (resolution + 2 - i)) / 2;
        uint address = baseAddress + offset + j;
        uint chunkIndex = address / ChunkSize;
        uint chunkGroupIndex = chunkIndex / ChunkNumPerGroup;
        uint offsetInChunkGroup = chunkIndex - chunkGroupIndex * ChunkNumPerGroup;
        uint bitmask = 1 << offsetInChunkGroup;

        uint occupancyBitfieldAddress = chunkGroupIndex * 4u;
        uint occupancyBitfield = VMC_OccupancyBuffer.Load(occupancyBitfieldAddress);
        if ((occupancyBitfield & bitmask) == 0)
        {
            VMC_OccupancyBuffer.InterlockedOr(occupancyBitfieldAddress, bitmask);
        }
    }

    static void MarkChunk(uint primitiveIndex, float3 bary)
    {
        MeshColors_MetaInfo metaInfo = MeshColors_LoadMetaInfo(primitiveIndex);
        
        uint2 ij = MeshColors_B(bary, metaInfo.resolution).xy;
        uint i = ij.x;
        uint j = ij.y;
        
        float3 weight = MeshColors_W(bary, metaInfo.resolution);
        if (weight.x + weight.y + weight.z > 1.9f)
        {
            weight = 1.0f - weight;

            __MarkColor(i, j + 1, metaInfo.resolution, metaInfo.address);
            __MarkColor(i + 1, j, metaInfo.resolution, metaInfo.address);
            __MarkColor(i + 1, j + 1, metaInfo.resolution, metaInfo.address);
        }
        else
        {
            __MarkColor(i + 1, j, metaInfo.resolution, metaInfo.address);
            __MarkColor(i, j + 1, metaInfo.resolution, metaInfo.address);
            __MarkColor(i, j, metaInfo.resolution, metaInfo.address);
        }
    }
};

#endif // VIRTUAL_MESH_COLORS