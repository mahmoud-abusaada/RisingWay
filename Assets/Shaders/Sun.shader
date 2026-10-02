// The Sun of the solar system in the sky (SolarSystem.cs): its own light, so nothing lights it.
// Two copies of the surface texture drift across each other, which makes the granulation crawl;
// the disc darkens and reddens towards its edge, as the real one does (limb darkening). Output is
// HDR: with bloom on it flares, and without bloom the corona (SunCorona.shader) still gives the
// glow, so the look does not depend on a post effect a weak phone has switched off.
Shader "RisingWay/Sun"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Surface", 2D) = "white" {}
        [HDR] _Core ("Centre colour", Color) = (3.2, 2.9, 2.3, 1)
        [HDR] _Limb ("Edge colour", Color) = (2.4, 1.1, 0.35, 1)
        _Detail ("Surface detail", Range(0, 1)) = 0.55
        _Drift ("Drift speed", Range(0, 0.1)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sun"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Core;
                half4 _Limb;
                half _Detail;
                half _Drift;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half facing : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = v.uv;
                o.facing = dot(TransformObjectToWorldNormal(v.normalOS), normalize(_WorldSpaceCameraPos - positionWS));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Drift;
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(t, 0)).r;
                half b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv * float2(2, 2) + float2(-t * 1.7, t * 0.6)).r;
                half surface = lerp(1.0, a * 0.65 + b * 0.35 + 0.35, _Detail);
                half mu = saturate(i.facing);              // 1 at the centre of the disc, 0 at its edge
                half limb = pow(mu, 0.55);
                half3 c = lerp(_Limb.rgb, _Core.rgb, limb) * surface;
                return half4(c, 1);
            }
            ENDHLSL
        }
        // So that the sun hides what is behind it in the depth texture too.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
