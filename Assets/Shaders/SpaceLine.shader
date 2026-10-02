// For the thin glowing things in the sky: the trails the Sun and the planets leave, and Saturn's
// rings. Unlit, added to the picture, coloured and faded by the vertex colour (a TrailRenderer's
// gradient), times an optional texture.
//
// With _RingFromCentre on, the texture is read along the distance from the mesh's centre instead
// of by UV: a flat square becomes a ring (Saturn's ring texture is one strip, inner edge to outer).
Shader "RisingWay/Space Line"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Colour", Color) = (1, 1, 1, 1)
        [Toggle] _RingFromCentre ("Ring: read the texture from the centre outwards", Float) = 0
        _RingInner ("Ring inner radius (0..1)", Range(0, 1)) = 0.55
    }
    SubShader
    {
        // After every track part (2000 + its place), so the track, which writes depth, hides it.
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+406" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SpaceLine"
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
                half4 _Color;
                float _RingFromCentre;
                float _RingInner;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float2 flat : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.flat = v.positionOS.xy * 2.0; // a unit quad: -1..1
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Along the ring: 0 at its inner edge, 1 at its outer. No branch: the ring switch
                // just picks which coordinates the one texture read uses.
                float r = length(i.flat);
                float along = (r - _RingInner) / max(1.0 - _RingInner, 0.001);
                float ring = _RingFromCentre;
                float2 uv = lerp(i.uv, float2(along, 0.5), ring);
                float inside = (along >= 0.0 && along <= 1.0) ? 1.0 : 0.0;
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float alpha = tex.a * i.color.a * lerp(1.0, inside, ring);
                return half4(tex.rgb * i.color.rgb * alpha, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
