using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Unity.Collections;
using System;

[RequireComponent(typeof(MeshRenderer)), ExecuteInEditMode]
public class MeshColorRendererData : MonoBehaviour
{
    public Texture2D testTexture;

    MeshColor meshColor;

    void OnEnable()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf?.sharedMesh == null)
        {
            return;
        }

        if (meshColor != null)
        {
            meshColor.Dispose();
        }
        meshColor = new MeshColor(transform, mf.sharedMesh, 256);
        meshColor.ReadDataFromTexture(testTexture);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.SetPropertyBlock(meshColor.PropertyBlock);
    }

    void OnDisable()
    {
        meshColor?.Dispose();
        meshColor = null;
    }
}
