Shader "Reyes/Principled"
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
            Tags
            {
                "LightMode" = "ForwardShading"
            }

            CGPROGRAM
            #pragma use_dxc
            #pragma require barycentrics
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag

            #include "ForwardShading.cginc"

            ENDCG
        }
    }
}
