// The ball, and what goes round it, seen through the track (SeeThrough.cs).
//
// Drawn as one more material on each of those renderers, before them in the queue: it writes no
// colour, only depth, and that depth is the far plane, under the renderer's own shape. Whatever
// the track left in the depth buffer there is gone, so the ball group drawn next only has to be in
// front of the far plane - and of each other, so a moon still goes behind the ball.
Shader "RisingWay/See Through"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+420" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SeeThrough"
            ColorMask 0
            ZWrite On
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };

            float4 vert(Attributes v) : SV_POSITION
            {
                float4 p = TransformObjectToHClip(v.positionOS.xyz);
                // On the far plane, just inside it so it is not clipped.
            #if UNITY_REVERSED_Z
                p.z = p.w * 1e-6;
            #else
                p.z = p.w * (1.0 - 1e-6);
            #endif
                return p;
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
