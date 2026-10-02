// A planet of the solar system in the sky (SolarSystem.cs). Lit by the Sun and by nothing else:
// the side towards it bright, the other side in shadow, a soft line between them - which is what
// makes a lit ball read as a planet. URP Lit could not do that here: every other light in the
// scene reached the planets too, and they all showed full faces.
//
//   _SunPosition   (global, set by SolarSystem.cs) where the Sun is, in world space
//   atmosphere     a thin rim of colour on the sunlit edge, for the planets that have air
//   night map      what glows on the dark side (the Earth's cities)
Shader "RisingWay/Planet"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Surface", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 4)) = 1.25
        _Shadow ("Light on the night side", Range(0, 0.5)) = 0.02
        _Terminator ("Softness of the day/night line", Range(0.01, 1)) = 0.18
        [HDR] _Atmosphere ("Atmosphere colour", Color) = (0, 0, 0, 1)
        _AtmospherePower ("Atmosphere thinness", Range(1, 8)) = 3
        [NoScaleOffset] _NightTex ("Night side", 2D) = "black" {}
        [HDR] _NightColor ("Night side colour", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Planet"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NightTex);
            SAMPLER(sampler_NightTex);
            CBUFFER_START(UnityPerMaterial)
                half _Brightness;
                half _Shadow;
                half _Terminator;
                half4 _Atmosphere;
                half _AtmospherePower;
                half4 _NightColor;
            CBUFFER_END
            float3 _SunPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 toSun = normalize(_SunPosition - i.positionWS);
                half3 toEye = normalize(_WorldSpaceCameraPos - i.positionWS);
                half ndl = dot(n, toSun);
                half day = smoothstep(-_Terminator, _Terminator, ndl);
                half3 surface = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                half3 c = surface * (_Shadow + day * saturate(ndl * 0.85 + 0.15) * _Brightness);
                // Air: brightest where we look through the most of it, at the edge, on the day side.
                half rim = pow(1.0 - saturate(dot(n, toEye)), _AtmospherePower);
                c += _Atmosphere.rgb * rim * (0.08 + 0.92 * day);
                c += SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, i.uv).rgb * _NightColor.rgb * (1.0 - day);
                return half4(c, 1);
            }
            ENDHLSL
        }
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
