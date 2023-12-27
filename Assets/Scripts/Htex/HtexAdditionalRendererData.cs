using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Unity.Collections;
using System;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshRenderer))]
public class HtexAdditionalRendererData : MonoBehaviour
{
    public Vector2Int HtexTextureNumQuads;
    public Vector2Int HtexTextureQuadSize;
    public Texture2D HtexTextureAtlas;

    [SerializeField]
    public Htex.MeshData MeshData;

    GraphicsBuffer _VertexToHalfedgeIDs;
    GraphicsBuffer _EdgeToHalfedgeIDs;
    GraphicsBuffer _FaceToHalfedgeIDs;
    GraphicsBuffer _Halfedges;

    void OnEnable()
    {
        if (MeshData == null)
        {
            return;
        }

        _VertexToHalfedgeIDs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MeshData.VertexToHalfedgeIDs.Length, sizeof(int));
        _VertexToHalfedgeIDs.SetData(MeshData.VertexToHalfedgeIDs);

        _EdgeToHalfedgeIDs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MeshData.EdgeToHalfedgeIDs.Length, sizeof(int));
        _EdgeToHalfedgeIDs.SetData(MeshData.EdgeToHalfedgeIDs);

        _FaceToHalfedgeIDs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MeshData.FaceToHalfedgeIDs.Length, sizeof(int));
        _FaceToHalfedgeIDs.SetData(MeshData.FaceToHalfedgeIDs);

        _Halfedges = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MeshData.Halfedges.Length, Marshal.SizeOf(typeof(Htex.cc_Halfedge)));
        _Halfedges.SetData(MeshData.Halfedges);

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.GetPropertyBlock(propertyBlock);
        {
            propertyBlock.SetBuffer("_VertexToHalfedgeIDs", _VertexToHalfedgeIDs);
            propertyBlock.SetBuffer("_EdgeToHalfedgeIDs", _EdgeToHalfedgeIDs);
            propertyBlock.SetBuffer("_FaceToHalfedgeIDs", _FaceToHalfedgeIDs);
            propertyBlock.SetBuffer("_Halfedges", _Halfedges);
            propertyBlock.SetTexture("_HtexTextureAtlas", HtexTextureAtlas);
            propertyBlock.SetInteger("_HtexTextureNumQuadsX", HtexTextureNumQuads.x);
            propertyBlock.SetInteger("_HtexTextureNumQuadsY", HtexTextureNumQuads.y);
            propertyBlock.SetInteger("_HtexTextureQuadWidth", HtexTextureQuadSize.x);
            propertyBlock.SetInteger("_HtexTextureQuadHeight", HtexTextureQuadSize.y);
        }
        mr.SetPropertyBlock(propertyBlock);
    }

    void OnDisable()
    {
        _VertexToHalfedgeIDs?.Dispose();
        _VertexToHalfedgeIDs = null;
        _EdgeToHalfedgeIDs?.Dispose();
        _EdgeToHalfedgeIDs = null;
        _FaceToHalfedgeIDs?.Dispose();
        _FaceToHalfedgeIDs = null;
        _Halfedges?.Dispose();
        _Halfedges = null;
    }
}
