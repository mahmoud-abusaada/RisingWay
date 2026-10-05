// The sky of a run (AtmosphereSky.cs): a planet's atmosphere around the track and the planet itself
// below the horizon, drawn on a box around the camera over the galaxy and the stars (SpaceSky).
//
// One picture, three numbers:
//   _AtmoAmount   0 nothing (space, as in the menus) .. 1 the full sky.
//   _AtmoSunUp    the time of day: the Sun's height in the sky, -1 deep night .. 0 the horizon
//                 (dawn, dusk) .. 1 noon. Only the colours follow it; the glow sits where the
//                 Sun really is (_AtmoSunDir).
//   _AtmoThin     0 inside the atmosphere .. 1 above it: the sky darkens to space from the top
//                 down and what is left is the glowing rim of the planet.
// _AtmoHorizon is how far below level the horizon lies (the sine of the angle): a little at the
// start, a lot from high up, where the planet is a ball under the track.
//
// The surface is drawn twice: in colour here, and as depth by the second pass, which puts a wall
// _AtmoGroundDistance away below the horizon so the Sun, the planets and the near stars set behind
// the planet instead of showing through it. The track is far nearer than that.
Shader "RisingWay/Atmosphere"
{
    Properties
    {
        _DayZenith ("Day zenith", Color) = (0.16, 0.38, 0.86, 1)
        _DayHorizon ("Day horizon", Color) = (0.58, 0.78, 0.98, 1)
        _DuskZenith ("Dusk zenith", Color) = (0.12, 0.16, 0.42, 1)
        _DuskHorizon ("Dusk horizon (away from the Sun)", Color) = (0.36, 0.26, 0.52, 1)
        _NightZenith ("Night zenith", Color) = (0.005, 0.01, 0.03, 1)
        _NightHorizon ("Night horizon", Color) = (0.03, 0.05, 0.11, 1)
        _GroundDay ("Ground by day", Color) = (0.07, 0.15, 0.24, 1)
        _GroundNight ("Ground by night", Color) = (0.008, 0.012, 0.025, 1)
        _Rim ("Rim of the atmosphere", Color) = (0.35, 0.65, 1.0, 1)
    }
    SubShader
    {
        // "UniversalPipeline": URP 17 strips every variant of a SubShader whose tag it does not
        // know. After the galaxy (Background) and the stars (Background+10).
        Tags { "RenderType" = "Background" "Queue" = "Background+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _DayZenith, _DayHorizon, _DuskZenith, _DuskHorizon, _NightZenith, _NightHorizon;
            float4 _GroundDay, _GroundNight, _Rim;
        CBUFFER_END
        float _AtmoAmount, _AtmoSunUp, _AtmoThin, _AtmoHorizon, _AtmoGroundDistance;
        float3 _AtmoSunDir;
        float4 _AtmoWarm; // the colour of the light round a low Sun (golden; redder for Insane)
        ENDHLSL

        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha // premultiplied: what shows of the space behind is (1 - alpha)
            ZWrite Off
            ZTest Always // behind everything anyway; and the ground's depth wall may be drawn first
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldDir = TransformObjectToWorldDir(v.positionOS.xyz, false);
                o.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + worldDir * (_ProjectionParams.z * 0.45));
                o.direction = worldDir;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            // Cloud cover seen from above: streaky, banded like weather on a globe.
            float clouds(float2 p)
            {
                p *= float2(1.0, 2.2);
                float n = noise(p) * 0.55 + noise(p * 2.03 + 7.1) * 0.3 + noise(p * 4.1 + 3.7) * 0.15;
                return smoothstep(0.48, 0.78, n);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sunDir = normalize(_AtmoSunDir);
                float sunUp = _AtmoSunUp;

                // Time of day as three weights.
                float day = smoothstep(0.0, 0.35, sunUp);
                float night = smoothstep(0.0, -0.22, sunUp);
                float dusk = saturate(1.0 - day - night);

                // Height above the horizon the viewer sees (negative: the planet).
                float h = d.y + _AtmoHorizon;
                float towardSun = saturate(dot(normalize(float2(d.x, d.z) + 1e-5), normalize(float2(sunDir.x, sunDir.z) + 1e-5)) * 0.5 + 0.5);
                float cosSun = dot(d, sunDir);

                // The colour of the horizon: by day pale blue; at dawn and dusk warm toward the Sun.
                float3 warm = _AtmoWarm.rgb;
                float3 horizon = _DayHorizon.rgb * day
                               + lerp(_DuskHorizon.rgb, warm, pow(towardSun, 3.0)) * dusk
                               + _NightHorizon.rgb * night;
                float3 zenith = _DayZenith.rgb * day + _DuskZenith.rgb * dusk + _NightZenith.rgb * night;

                // Inside the air the gradient spans the whole sky; from above it hugs the horizon.
                float spread = lerp(1.0, 0.05, _AtmoThin);
                float up = saturate(h / spread);
                float3 sky = lerp(horizon, zenith * (1.0 - _AtmoThin), pow(up, 0.5));

                // Light scattered round the Sun: a wide glow and a tight halo.
                float sunSeen = smoothstep(-0.25, 0.02, sunUp);
                float glow = pow(saturate(cosSun), 6.0) * 0.45 + pow(saturate(cosSun), 90.0) * 0.9;
                sky += warm * glow * sunSeen * lerp(0.6, 1.0, dusk) * (1.0 - 0.6 * _AtmoThin);

                // How much of the space behind the sky hides: all of it by day, little at night,
                // and none above the air.
                float cover = lerp(1.0, 0.18, night);
                cover *= lerp(1.0, saturate(1.0 - up) * 0.9, _AtmoThin);
                float skyAlpha = cover;

                // The planet.
                float below = saturate(-h);
                float2 ground = d.xz / max(-d.y, 0.02); // where the view meets a level plane
                float c = clouds(ground * 0.35 + float2(_Time.y * 0.004, 0));
                float lit = saturate(sunUp * 2.5 + 0.35);
                float3 surface = lerp(_GroundNight.rgb, _GroundDay.rgb, lit);
                surface = lerp(surface, lerp(float3(0.05, 0.06, 0.09), float3(0.85, 0.9, 1.0), lit), c * 0.55);
                // Dawn and dusk light the cloud tops on the Sun's side.
                surface += warm * c * dusk * pow(towardSun, 4.0) * 0.35;
                // Haze: the ground fades into the horizon colour toward the limb.
                float haze = exp(-below * lerp(9.0, 30.0, _AtmoThin));
                surface = lerp(surface, horizon, haze * 0.85);

                // The rim: a thin bright line where the air meets space, stronger from above.
                float rim = exp(-abs(h) * lerp(40.0, 140.0, _AtmoThin)) * lerp(0.25, 1.0, _AtmoThin);
                float3 rimColour = lerp(_Rim.rgb, warm, pow(towardSun, 2.0) * (dusk + 0.3 * day));
                rimColour *= lerp(0.35, 1.0, 1.0 - night * 0.7);

                float isGround = smoothstep(0.0, -0.004, h);
                float3 colour = lerp(sky, surface, isGround) + rimColour * rim;
                float alpha = lerp(skyAlpha, 1.0, isGround);
                alpha = saturate(max(alpha, rim));

                return half4(colour * _AtmoAmount, alpha * _AtmoAmount);
            }
            ENDHLSL
        }

        // The planet as a wall in the depth buffer, so what is behind it (the Sun after it sets,
        // the planets, the near stars) is hidden. Nothing on screen: depth only.
        Pass
        {
            Name "AtmosphereGround"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 worldDir = TransformObjectToWorldDir(v.positionOS.xyz, false);
                o.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + worldDir * _AtmoGroundDistance);
                o.direction = worldDir;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                clip(-(d.y + _AtmoHorizon) - 0.002);
                clip(_AtmoAmount - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
