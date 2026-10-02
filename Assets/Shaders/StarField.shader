// The stars: one mesh, one draw call (SpaceSky.cs builds it). Each star is a small square that the
// vertex shader places on the sky and sizes in screen pixels, so a star is a sharp point of the
// same size on every phone and at every render scale - a star in a texture is a blurred square.
// It replaces three particle systems of up to 39,900 lit, transparent, sorted billboards.
//
// Per vertex:
//   POSITION   the star's direction (galactic coordinates), the same for its four corners
//   TEXCOORD0  xy: which corner (-1..1)   z: radius in pixels at 1080p   w: twinkle phase
//   COLOR      rgb: colour x brightness (HDR: the bright ones bloom)   a: when it comes out as the
//              sky darkens - 0 for the brightest stars, first to show, up to 1 for the faintest
// Globals (SpaceSky.cs): _GalaxyToWorld, _SpaceVisibility - see SpaceSky.shader.
Shader "RisingWay/Star Field"
{
    Properties
    {
        _Brightness ("Brightness", Range(0, 8)) = 1
        _Size ("Size", Range(0.25, 4)) = 1
        _Twinkle ("Twinkle", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StarField"
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
                float _Brightness;
                float _Size;
                float _Twinkle;
            CBUFFER_END
            float4x4 _GalaxyToWorld;
            float _SpaceVisibility;

            struct Attributes { float3 positionOS : POSITION; float4 corner : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half3 color : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldDir = mul((float3x3)_GalaxyToWorld, v.positionOS);
                float4 clip = TransformWorldToHClip(_WorldSpaceCameraPos + worldDir * (_ProjectionParams.z * 0.45));
                // Out to the corner, in pixels. Never under 1.25: smaller than that a star falls
                // between pixels and flickers as the camera turns.
                float pixels = max(v.corner.z * _Size * _ScreenParams.y / 1080.0, 1.25);
                clip.xy += v.corner.xy * pixels * 2.0 / _ScreenParams.xy * clip.w;
                o.positionCS = clip;
                o.corner = v.corner.xy;

                // Brightest first as the sky darkens, the faintest only in full dark.
                float shown = saturate((_SpaceVisibility - v.color.a * 0.75) * 4.0);
                float twinkle = 1.0 + _Twinkle * sin(_Time.y * (1.5 + 2.5 * frac(v.corner.w * 7.31)) + v.corner.w * 6.2832);
                o.color = v.color.rgb * (_Brightness * shown * twinkle);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // A tight core and a faint wide skirt: a point of light through a lens.
                half r2 = dot(i.corner, i.corner);
                half a = exp(-r2 * 7.0) + 0.2 * exp(-r2 * 1.8);
                a *= saturate(1.0 - r2); // to nothing at the edge of the square
                return half4(i.color * a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
