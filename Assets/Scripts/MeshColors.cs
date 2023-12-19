using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class MeshColors : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MetaInfo
    {
        public uint address;
        public uint resolution;
    }

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

        AdjacencyMapBuffer = new ComputeBuffer(triCount, 6 * 4, ComputeBufferType.Structured | ComputeBufferType.Raw, ComputeBufferMode.SubUpdates);
        AdjacencyMapBuffer.name = "Mesh Colors Adjacency Map Buffer";
    }

    class Edge
    {
        public uint BeginIndex;
        public uint EndIndex;

        public uint LocalEdgeIndex;
        public uint TriangleIndex;

        public Edge(uint begin, uint end)
        {
            BeginIndex = begin;
            EndIndex = end;
        }

        public Edge Reverse()
        {
            Edge edge = new Edge(EndIndex, BeginIndex);
            edge.LocalEdgeIndex = LocalEdgeIndex;
            edge.TriangleIndex = TriangleIndex;
            return edge;
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge other)
            {
                return other.BeginIndex == BeginIndex 
                    && other.EndIndex == EndIndex 
                    && other.LocalEdgeIndex == LocalEdgeIndex
                    && other.TriangleIndex == TriangleIndex;
            }

            return false;
        }

        public override int GetHashCode()
        {
            Hash128 hash128 = new Hash128();
            hash128.Append(BeginIndex);
            hash128.Append(EndIndex);
            hash128.Append(LocalEdgeIndex);
            return hash128.GetHashCode();
        }
    }

    class DoubleEdge
    {
        public Edge edge0;
        public Edge edge1;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AdjacencyInfo
    {
        public uint3 TriangleIndices;
        public uint3 LocalEdgeIndices;
    }

    public void GenerateAdjacencyMap()
    {
        Dictionary<int, DoubleEdge> doubleEdgePairs = new();

        int[] triangles = SharedMesh.triangles;
        int triangleCount = triangles.Length / 3;
        
        NativeArray<AdjacencyInfo> adjacencyInfos = AdjacencyMapBuffer.BeginWrite<AdjacencyInfo>(0, triangleCount);

        for (int triIndex = 0; triIndex < triangleCount; triIndex++)
        {
            uint A = (uint)triangles[triIndex * 3 + 0];
            uint B = (uint)triangles[triIndex * 3 + 1];
            uint C = (uint)triangles[triIndex * 3 + 2];

            Edge[] edges = { new Edge(A, C), new Edge(A, B), new Edge(B, C) };

            for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
            {
                Edge edge = edges[edgeIndex];
                edge.TriangleIndex = (uint)triIndex;
                edge.LocalEdgeIndex = (uint)edgeIndex;

                int edgeHash = edge.GetHashCode();
                int edgeReverseHash = edge.Reverse().GetHashCode();

                if (!doubleEdgePairs.ContainsKey(edgeHash) && !doubleEdgePairs.ContainsKey(edgeReverseHash))
                {
                    DoubleEdge doubleEdge = new DoubleEdge();
                    doubleEdge.edge0 = edge;
                    doubleEdge.edge1 = null;

                    doubleEdgePairs.Add(edgeHash, doubleEdge);
                }
                else if (doubleEdgePairs.ContainsKey(edgeHash))
                {
                    DoubleEdge doubleEdge = doubleEdgePairs[edgeHash];
                    doubleEdge.edge1 = edge;
                }
                else if (doubleEdgePairs.ContainsKey(edgeReverseHash))
                {
                    DoubleEdge doubleEdge = doubleEdgePairs[edgeReverseHash];
                    doubleEdge.edge1 = edge;
                }
            }
        }

        for (int triIndex = 0; triIndex < triangleCount; triIndex++)
        {
            AdjacencyInfo adjacencyInfo = new AdjacencyInfo();

            uint A = (uint)triangles[triIndex * 3 + 0];
            uint B = (uint)triangles[triIndex * 3 + 1];
            uint C = (uint)triangles[triIndex * 3 + 2];

            Edge[] edges = { new Edge(A, C), new Edge(A, B), new Edge(B, C) };

            for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
            {
                Edge edge = edges[edgeIndex];
                edge.LocalEdgeIndex = (uint)edgeIndex;

                int edgeHash = edge.GetHashCode();
                int edgeReverseHash = edge.Reverse().GetHashCode();

                if (doubleEdgePairs.ContainsKey(edgeHash))
                {
                    DoubleEdge doubleEdge = doubleEdgePairs[edgeHash];
                    if (doubleEdge.edge0 != edge && doubleEdge.edge0 != null)
                    {
                        adjacencyInfo.TriangleIndices[edgeIndex] = doubleEdge.edge0.TriangleIndex;
                        adjacencyInfo.LocalEdgeIndices[edgeIndex] = doubleEdge.edge0.LocalEdgeIndex;
                    }
                    else if (doubleEdge.edge1 != edge && doubleEdge.edge1 != null)
                    {
                        adjacencyInfo.TriangleIndices[edgeIndex] = doubleEdge.edge1.TriangleIndex;
                        adjacencyInfo.LocalEdgeIndices[edgeIndex] = doubleEdge.edge1.LocalEdgeIndex;
                    }
                }
                else if (doubleEdgePairs.ContainsKey(edgeReverseHash))
                {
                    DoubleEdge doubleEdge = doubleEdgePairs[edgeReverseHash];
                    if (doubleEdge.edge0 != edge && doubleEdge.edge0 != null)
                    {
                        adjacencyInfo.TriangleIndices[edgeIndex] = doubleEdge.edge0.TriangleIndex;
                        adjacencyInfo.LocalEdgeIndices[edgeIndex] = doubleEdge.edge0.LocalEdgeIndex;
                    }
                    else if (doubleEdge.edge1 != edge && doubleEdge.edge1 != null)
                    {
                        adjacencyInfo.TriangleIndices[edgeIndex] = doubleEdge.edge1.TriangleIndex;
                        adjacencyInfo.LocalEdgeIndices[edgeIndex] = doubleEdge.edge1.LocalEdgeIndex;
                    }
                }
                else
                {
                    adjacencyInfo.TriangleIndices = new uint3(~0u);
                    adjacencyInfo.LocalEdgeIndices = new uint3(~0u);
                }
            }
        }

        AdjacencyMapBuffer.EndWrite<AdjacencyInfo>(triangleCount);
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
    public ComputeBuffer AdjacencyMapBuffer;

    static class Props
    {
        public static int MeshColors_PatchBuffer = Shader.PropertyToID("_MeshColors_PatchBuffer");
        public static int MeshColors_MetaBuffer = Shader.PropertyToID("_MeshColors_MetaBuffer");
    }
}