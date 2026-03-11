Shader "Custom/ARDepthMask"
{
    SubShader
    {
        Tags { "Queue"="Geometry-1" "RenderType"="Opaque" }
        ZWrite On
        ZTest LEqual
        Cull Off
        ColorMask 0

        Pass { }
    }
}