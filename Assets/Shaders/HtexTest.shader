Shader "Unlit/HtexTest"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma use_dxc
            #pragma require barycentrics
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag

            #include "HtexTest.cginc"

            ENDCG
        }
    }
}
