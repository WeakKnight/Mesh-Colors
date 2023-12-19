using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class MeshColors : IDisposable
{
    public MeshColors(Transform transform, Mesh mesh, float colorsPerUnit = 64.0f)
    {
        SharedMesh = mesh;

        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        int triCount = triangles.Length / 3;
        int baseAddress = 0;

        MetaBuffer = new ComputeBuffer(triCount, 2 * 4, ComputeBufferType.Structured | ComputeBufferType.Raw, ComputeBufferMode.SubUpdates);
        MetaBuffer.name = "Mesh Colors Meta Buffer";

        NativeArray<MetaInfo> metaInfos = MetaBuffer.BeginWrite<MetaInfo>(0, triCount);

        for (int triIndex = 0; triIndex < triCount; triIndex++)
        {
            int indexA = triangles[triIndex * 3 + 0];
            int indexB = triangles[triIndex * 3 + 1];
            int indexC = triangles[triIndex * 3 + 2];

            float3 A = transform.TransformPoint(vertices[indexA]);
            float3 B = transform.TransformPoint(vertices[indexB]);
            float3 C = transform.TransformPoint(vertices[indexC]);

            float avgEdgeLength = (math.length(A - B) + math.length(A - C) + math.length(B - C)) / 3.0f;

            MetaInfo metaInfo = new MetaInfo();
            metaInfo.resolution = (uint)CeilToNextPowerOfTwo(Mathf.CeilToInt(avgEdgeLength * colorsPerUnit));
            metaInfo.address = (uint)baseAddress;
            metaInfos[triIndex] = metaInfo;

            baseAddress += ColorsPerPatch((int)metaInfo.resolution);
        }

        MetaBuffer.EndWrite<MetaInfo>(triCount);

        PatchBuffer = new ComputeBuffer(baseAddress, 4, ComputeBufferType.Structured | ComputeBufferType.Raw, ComputeBufferMode.SubUpdates);
        PatchBuffer.name = "Mesh Colors Patch Buffer";

        PropertyBlock = new();
        PropertyBlock.SetBuffer(Props.MeshColors_PatchBuffer, PatchBuffer);
        PropertyBlock.SetBuffer(Props.MeshColors_MetaBuffer, MetaBuffer);
    }

    public void ReadDataFromTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        int[] triangles = SharedMesh.triangles;
        Vector2[] uvs = SharedMesh.uv;

        MetaInfo[] metaInfoArr = new MetaInfo[MetaBuffer.count];
        MetaBuffer.GetData(metaInfoArr);

        NativeArray<Color32> pathColors = PatchBuffer.BeginWrite<Color32>(0, PatchBuffer.count);
        int pathColorIndex = 0;
        for (int triIndex = 0; triIndex < MetaBuffer.count; triIndex++)
        {
            int indexA = triangles[triIndex * 3 + 0];
            int indexB = triangles[triIndex * 3 + 1];
            int indexC = triangles[triIndex * 3 + 2];

            float2 uvA = uvs[indexA];
            float2 uvB = uvs[indexB];
            float2 uvC = uvs[indexC];

            MetaInfo metaInfo = metaInfoArr[triIndex];
            for (uint i = 0; i <= metaInfo.resolution; i++)
            {
                for (uint j = 0; j <= metaInfo.resolution - i; j++)
                {
                    float3 bary = P((int)i, (int)j, (int)metaInfo.resolution);
                    float2 uv = uvA * bary.x + uvB * bary.y + uvC * bary.z;
                    int pixelX = (int)(uv.x * texture.width);
                    int pixelY = (int)(uv.y * texture.height);
                    pathColors[pathColorIndex] = texture.GetPixel(pixelX, pixelY);
                    pathColorIndex++;
                }
            }
        }
        PatchBuffer.EndWrite<Color32>(PatchBuffer.count);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MetaInfo
    {
        public uint address;
        public uint resolution;
    }

    int CeilToNextPowerOfTwo(int x)
    {
        if (x <= 1)
        {
            return 1;
        }

        x--;

        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;

        return x + 1;
    }

    int ColorsPerVertex(int resolution)
    {
        return 1;
    }

    int ColorsPerEdge(int resolution)
    {
        return resolution - 1;
    }

    int ColorsPerFace(int resolution)
    {
        return (resolution - 1) * (resolution - 2) / 2;
    }

    int ColorsPerPatch(int resolution)
    {
        return 3 * ColorsPerVertex(resolution) + 3 * ColorsPerEdge(resolution) + ColorsPerFace(resolution);
    }

    float3 P(int i, int j, int resolution)
    {
        float u = i / (float)resolution;
        float v = j / (float)resolution;

        return new float3(u, v, 1.0f - u - v);
    }

    public void Dispose()
    {
        PatchBuffer.Release();
        MetaBuffer.Release();
    }

    public Mesh SharedMesh;
    public MaterialPropertyBlock PropertyBlock;
    public ComputeBuffer PatchBuffer;
    public ComputeBuffer MetaBuffer;

    static class Props
    {
        public static int MeshColors_PatchBuffer = Shader.PropertyToID("_MeshColors_PatchBuffer");
        public static int MeshColors_MetaBuffer = Shader.PropertyToID("_MeshColors_MetaBuffer");
    }
}