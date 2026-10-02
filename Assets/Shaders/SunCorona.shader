// The glow around the Sun: a square that always faces the camera, added to the picture. A bright
// inner halo, a wide faint one, and soft streamers that turn slowly. It is drawn by the shader
// itself, not by bloom, so the Sun glows the same on a phone with bloom switched off.
//
// The mesh is a unit quad (corners at -0.5..0.5); _Reach is the glow's radius in multiples of the
// object's scale, so put it on a child of the Sun with the Sun's own scale.
Shader "RisingWay/Sun Corona"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1.6, 1.15, 0.6, 1)
        _Reach ("Reach (sun radii)", Range(1, 12)) = 5
        _Inner ("Inner halo", Range(0, 4)) = 1.2
        _Outer ("Outer halo", Range(0, 1)) = 0.22
        _Rays ("Streamers", Range(0, 1)) = 0.35
    }
    SubShader
    {
        // After every track part (2000 + its place), so the track, which writes depth, hides it.
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+405" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SunCorona"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Reach;
                half _Inner;
                half _Outer;
                half _Rays;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 offset : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // The object's centre in view space, then out to the corner in the view's own x and
                // y: always square-on to the camera. The sun sphere has radius 0.5 of its scale.
                float3 centreVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float radius = length(TransformObjectToWorldDir(float3(1, 0, 0), false)) * 0.5;
                float2 corner = v.positionOS.xy * 2.0; // -1..1
                float3 positionVS = centreVS + float3(corner * radius * _Reach, 0);
                // A little towards the camera, in front of the sun's own surface.
                positionVS.z += radius * 1.05;
                o.positionCS = TransformWViewToHClip(positionVS);
                o.offset = corner * _Reach; // in sun radii
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half r = length(i.offset);              // 1 at the sun's edge
                half outside = max(r - 0.9, 0.0);
                half inner = _Inner * exp(-outside * 3.2);
                half outer = _Outer * exp(-outside * 0.75);
                // Streamers: a few broad lobes by angle, drifting.
                half angle = atan2(i.offset.y, i.offset.x);
                half t = _Time.y * 0.05;
                half lobes = 0.5 + 0.25 * sin(angle * 3.0 + t * 1.3) + 0.15 * sin(angle * 7.0 - t * 2.1) + 0.10 * sin(angle * 13.0 + t * 3.7);
                half rays = _Rays * lobes * exp(-outside * 1.1);
                // To nothing before the edge of the square.
                half fade = saturate((_Reach - r) / (_Reach * 0.35));
                half glow = (inner + outer + rays) * fade * fade;
                return half4(_Color.rgb * glow, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
