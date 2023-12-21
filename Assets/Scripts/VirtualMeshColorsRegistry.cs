using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class VirtualMeshColorsRegistry : System.IDisposable
{
    const uint MaxTexelPerUnit = 256;
    
    const uint MeshCapacity = 512;

    const uint MetaBufferCapacity = 1024 * 1024;
    const uint AdjacencyMapBufferCapacity = 1024 * 1024;

    class MeshColors : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MetaInfo
        {
            public uint address;
            public uint resolution;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AdjacencyInfo
        {
            public uint3 TriangleIndices;
            public uint3 LocalEdgeIndices;
        }

        public MeshColors(Transform transform, Mesh mesh, float colorsPerUnit = 64.0f)
        {
            SharedMesh = mesh;

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;

            int triCount = triangles.Length / 3;
            int baseAddress = 0;

            MetaBuffer = new NativeArray<MetaInfo>(triCount, Allocator.Persistent);

            for (int triIndex = 0; triIndex < triCount; triIndex++)
            {
                int indexA = triangles[triIndex * 3 + 0];
                int indexB = triangles[triIndex * 3 + 1];
                int indexC = triangles[triIndex * 3 + 2];

                float3 A = vertices[indexA];
                float3 B = vertices[indexB];
                float3 C = vertices[indexC];

                float avgEdgeLength = (math.length(A - B) + math.length(A - C) + math.length(B - C)) / 3.0f;

                MetaInfo metaInfo = new MetaInfo();
                metaInfo.resolution = (uint)CeilToNextPowerOfTwo(Mathf.CeilToInt(avgEdgeLength * colorsPerUnit));
                metaInfo.address = (uint)baseAddress;
                MetaBuffer[triIndex] = metaInfo;

                baseAddress += ColorsPerPatch((int)metaInfo.resolution);
            }

            TotalColorNum = baseAddress;

            AdjacencyMapBuffer = new NativeArray<AdjacencyInfo>(triCount, Allocator.Persistent);
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

        public void GenerateAdjacencyMap()
        {
            Dictionary<int, DoubleEdge> doubleEdgePairs = new();

            int[] triangles = SharedMesh.triangles;
            int triangleCount = triangles.Length / 3;

            for (int triIndex = 0; triIndex < triangleCount; triIndex++)
            {
                uint A = (uint)triangles[triIndex * 3 + 0];
                uint B = (uint)triangles[triIndex * 3 + 1];
                uint C = (uint)triangles[triIndex * 3 + 2];

                Edge[] edges = { new Edge(A, B), new Edge(B, C), new Edge(A, C) };

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
                        if (!doubleEdge.edge0.Equals(edge))
                        {
                            doubleEdge.edge1 = edge;
                        }
                    }
                    else if (doubleEdgePairs.ContainsKey(edgeReverseHash))
                    {
                        DoubleEdge doubleEdge = doubleEdgePairs[edgeReverseHash];
                        if (!doubleEdge.edge0.Equals(edge))
                        {
                            doubleEdge.edge1 = edge;
                        }
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

                AdjacencyMapBuffer[triIndex] = adjacencyInfo;
            }
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

        int ColorsPerVertex()
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
            return 3 * ColorsPerVertex() + 3 * ColorsPerEdge(resolution) + ColorsPerFace(resolution);
        }

        public void Dispose()
        {
            MetaBuffer.Dispose();
            AdjacencyMapBuffer.Dispose();
        }

        public Mesh SharedMesh;

        public int TotalColorNum;
        public NativeArray<MetaInfo> MetaBuffer;
        public NativeArray<AdjacencyInfo> AdjacencyMapBuffer;
    }

    ComputeBuffer DescBuffer;
    ComputeBuffer MetaBuffer;
    ComputeBuffer AdjacencyMapBuffer;

    public VirtualMeshColorsRegistry()
    {
    }

    public void AddMesh(Mesh mesh)
    {
    }

    public void Dispose()
    {
    }
}
