using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Unity.Collections;
using System;

[RequireComponent(typeof(MeshRenderer)), ExecuteInEditMode]
public class MeshColorsRendererData : MonoBehaviour
{
    public Texture2D testTexture;

    MeshColors meshColors;

    void OnEnable()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf?.sharedMesh == null)
        {
            return;
        }

        if (meshColors != null)
        {
            meshColors.Dispose();
        }
        meshColors = new MeshColors(transform, mf.sharedMesh, 256);
        meshColors.ReadDataFromTexture(testTexture);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.SetPropertyBlock(meshColors.PropertyBlock);
    }

    void OnDisable()
    {
        meshColors?.Dispose();
        meshColors = null;
    }
}
