// Stars that are out there in the space around the track, not painted on the sky (SpaceSky.cs
// places them): a few dozen, at a few hundred to a couple of thousand units from the ball. They
// have parallax - they slide past as the camera turns and sink away as the ball climbs - which is
// what makes them read as real objects rather than as the backdrop.
//
// Each is a square that always faces the camera, drawn as a telescope sees a bright star: a hard
// white-hot core, a soft coloured glow, and four thin diffraction spikes. The spikes line up with
// the screen, as they do in a photograph (they come from the telescope, not from the star).
//
// Per vertex:
//   POSITION   the star's position in the world, the same for its four corners
//   TEXCOORD0  xy: which corner (-1..1)   z: half-size in world units   w: twinkle phase
//   COLOR      rgb: colour x brightness (HDR)
// Global: _SpaceVisibility (SpaceSky.shader) - they fade with the rest of the sky.
Shader "RisingWay/Near Star"
{
    Properties
    {
        _Brightness ("Brightness", Range(0, 8)) = 1.5
        _Core ("Core size", Range(0.005, 0.2)) = 0.035
        _Glow ("Glow", Range(0, 2)) = 0.55
        _Spikes ("Spikes", Range(0, 2)) = 0.8
        _Twinkle ("Twinkle", Range(0, 1)) = 0.18
    }
    SubShader
    {
        // After every track part (2000 + its place), so the track, which writes depth, hides it.
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+407" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "NearStar"
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
                float _Core;
                float _Glow;
                float _Spikes;
                float _Twinkle;
            CBUFFER_END
            float _SpaceVisibility;

            struct Attributes { float3 positionOS : POSITION; float4 corner : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half3 color : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centreVS = TransformWorldToView(v.positionOS);
                // Never smaller than a few pixels, however far: a star is never lost to a pixel gap.
                float pixelsPerUnit = _ScreenParams.y / (2.0 * max(-centreVS.z, 1.0) * tan(radians(30.0)));
                float halfSize = max(v.corner.z, 14.0 / max(pixelsPerUnit, 1e-4));
                o.positionCS = TransformWViewToHClip(centreVS + float3(v.corner.xy * halfSize, 0));
                o.corner = v.corner.xy;
                // Twinkle: two slow waves, so it never repeats exactly.
                float t = _Time.y;
                float twinkle = 1.0 + _Twinkle * (0.6 * sin(t * 2.3 + v.corner.w * 6.28) + 0.4 * sin(t * 5.1 + v.corner.w * 17.0));
                o.color = v.color.rgb * (_Brightness * twinkle * saturate(_SpaceVisibility));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.corner;             // -1..1 across the square
                float r2 = dot(p, p);
                float core = exp(-r2 / (_Core * _Core));
                float glow = _Glow * exp(-sqrt(r2) * 7.0);
                // Spikes: thin in one axis, long in the other, both ways.
                float spikeH = exp(-abs(p.y) * 160.0) * exp(-abs(p.x) * 3.2);
                float spikeV = exp(-abs(p.x) * 160.0) * exp(-abs(p.y) * 3.2);
                float spikes = _Spikes * (spikeH + spikeV) * 0.5;
                float edge = saturate((1.0 - max(abs(p.x), abs(p.y))) * 6.0); // nothing at the square's edge
                // The core is white-hot; the colour shows in the glow and the spikes.
                half lum = dot(i.color, half3(0.3, 0.5, 0.2));
                half3 c = (lum * core * 2.5 + i.color * (glow + spikes)) * edge;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
