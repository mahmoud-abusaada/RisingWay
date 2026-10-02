// The galaxy: the glow of the Milky Way, drawn on a box around the camera (SpaceSky.cs) and added
// to whatever the camera cleared to. It replaces a 70,000-unit cylinder with a star texture tiled
// 10 x 5 over it and a cube with a nebula cubemap, both in the full Lit shader.
//
// Globals, set by SpaceSky.cs:
//   _GalaxyToWorld      rotates galactic directions into the world: the tilt of the band across the
//                       sky and its slow turn.
//   _SpaceVisibility    0 no space to be seen (full daylight) .. 1 deep space. Everything in the
//                       sky follows this one number, which is what a day/night cycle will drive.
Shader "RisingWay/Space Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Galaxy glow (equirect, galactic coordinates)", 2D) = "black" {}
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Contrast ("Contrast", Range(0.5, 3)) = 1
    }
    SubShader
    {
        // "UniversalPipeline", not "UniversalRenderPipeline": URP 17 strips every variant of a
        // SubShader whose tag it does not know (the path shader shipped empty that way once).
        Tags { "RenderType" = "Background" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SpaceSky"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                float4 _Tint;
                float _Contrast;
            CBUFFER_END
            float4x4 _GalaxyToWorld;
            float _SpaceVisibility;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // The box is centred on the camera: only the direction matters.
                float3 worldDir = TransformObjectToWorldDir(v.positionOS.xyz, false);
                o.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + worldDir * (_ProjectionParams.z * 0.5));
                o.direction = mul(transpose((float3x3)_GalaxyToWorld), worldDir);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float2 uv = float2(atan2(d.x, d.z) * (0.5 * INV_PI) + 0.5, asin(clamp(d.y, -1.0, 1.0)) * INV_PI + 0.5);
                // Across the seam behind the viewer u jumps from 1 to 0: take the jump out of the
                // derivatives, or the GPU picks the smallest mip there and draws a line.
                float2 dx = ddx(uv), dy = ddy(uv);
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);
                half3 c = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, dx, dy).rgb;
                c = pow(c, _Contrast) * _Tint.rgb * _Exposure * _SpaceVisibility;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
