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
                "LightMode" = "PreZ"
            }

            ZWrite On

            ColorMask 0

            CGPROGRAM
            #pragma use_dxc
            #pragma require barycentrics
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag

            #include "PreZ.cginc"

            ENDCG
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ForwardShading"
            }

            ZWrite Off
            ZTest Equal
            
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
