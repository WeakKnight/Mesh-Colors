using System.IO;
using UnityEngine;
using UnityEditor.AssetImporters;
using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Htex;

[ScriptedImporter(1, "htx")]
public class HtexImporter : ScriptedImporter
{
    public unsafe override void OnImportAsset(AssetImportContext ctx)
    {
        // Implement your parsing logic here to convert .htx content into a Unity mesh
        // This is a placeholder for the actual implementation
        IntPtr pHtexTexture = Htex.Bindings.HtexTexture_open(ctx.assetPath);
        IntPtr pHalfedgeMesh = Htex.Bindings.HtexTexture_getHalfedgeMesh(pHtexTexture);

        Htex.Info texInfo = Htex.Bindings.HtexTexture_getInfo(pHtexTexture);
        Htex.MeshData meshData = new Htex.MeshData(pHalfedgeMesh);        

        Vector3[] vertices = new Vector3[meshData.VertexCount + meshData.FaceCount];
        Vector3[] normals = new Vector3[meshData.VertexCount + meshData.FaceCount];

        Parallel.For(0, meshData.VertexCount, vertexID =>
        {
            var p = meshData.VertexPoints[vertexID];
            vertices[vertexID] = new Vector3(p.x, p.y, p.z);
            normals[vertexID] = meshData.computeVertexNormal(vertexID);
        });

        Parallel.For(0, meshData.FaceCount, faceID =>
        {
            vertices[faceID + meshData.VertexCount] = meshData.computeBarycenter(faceID);
            normals[faceID + meshData.VertexCount] = meshData.computeFacePointNormal(faceID);
        });

        int[] triangles = new int[meshData.HalfedgeCount * 3];
        Parallel.For(0, meshData.HalfedgeCount, halfedgeID =>
        {
            int nextID = meshData.ccm_HalfedgeNextID(halfedgeID);
            triangles[halfedgeID * 3] = meshData.VertexCount + meshData.ccm_HalfedgeFaceID(halfedgeID);
            triangles[halfedgeID * 3 + 1] = meshData.ccm_HalfedgeVertexID(halfedgeID);
            triangles[halfedgeID * 3 + 2] = meshData.ccm_HalfedgeVertexID(nextID);
        });

        Mesh mesh = new Mesh();
        mesh.name = "HtexMesh";
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
        ctx.AddObjectToAsset("mesh", mesh);

        // Assume all quad textures are the same size
        int quadWidth = 0, quadHeight = 0;
        Htex.Bindings.HtexTexture_getQuadResolution(pHtexTexture, 0, ref quadWidth, ref quadHeight);

        Debug.Assert(texInfo.numChannels == 4);
        Debug.Assert(texInfo.dataType == Htex.DataType.dt_uint8);

        int numQuadsX = Mathf.Min(texInfo.numFaces, 128);
        int numQuadsY = (texInfo.numFaces + numQuadsX - 1) / numQuadsX;
        byte[] textureData = new byte[quadWidth * numQuadsX * quadHeight * numQuadsY * 4];
        Parallel.For(0, texInfo.numFaces, quadID =>
        {
            int tileX = quadID % numQuadsX;
            int tileY = quadID / numQuadsX;

            NativeArray<byte> quadData = new NativeArray<byte>(quadWidth * quadHeight * 4, Allocator.TempJob);
            IntPtr pData = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(quadData);
            Htex.Bindings.HtexTexture_getData(pHtexTexture, quadID, pData, 0);

            for (int y = 0; y < quadHeight; ++y)
            {
                for (int x = 0; x < quadWidth; ++x)
                {
                    int srcIdx = y * quadWidth + x;
                    int dstIdx = ((tileY * quadHeight + y) * numQuadsX * quadWidth + tileX * quadWidth + x);
                    textureData[dstIdx * 4 + 0] = quadData[srcIdx * 4 + 0];
                    textureData[dstIdx * 4 + 1] = quadData[srcIdx * 4 + 1];
                    textureData[dstIdx * 4 + 2] = quadData[srcIdx * 4 + 2];
                    textureData[dstIdx * 4 + 3] = quadData[srcIdx * 4 + 3];
                }
            }
            quadData.Dispose();
        });
        
        Texture2D texture = new Texture2D(quadWidth * numQuadsX, quadHeight * numQuadsY, TextureFormat.RGBA32, false);
        texture.name = "HtexTextureAtlas";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixelData(textureData, 0, 0);
        texture.Apply();
        ctx.AddObjectToAsset("texture", texture);

        // Add the main game object
        GameObject go = new GameObject();        
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        HtexAdditionalRendererData renderData = go.AddComponent<HtexAdditionalRendererData>();
        renderData.HtexTextureAtlas = texture;
        renderData.HtexTextureNumQuads = new Vector2Int(numQuadsX, numQuadsY);
        renderData.HtexTextureQuadSize = new Vector2Int(quadWidth, quadHeight);
        renderData.MeshData = meshData;

        ctx.AddObjectToAsset("GameObject", go);

        ctx.SetMainObject(go);
    }
}
