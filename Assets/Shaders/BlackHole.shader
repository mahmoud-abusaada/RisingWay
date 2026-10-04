// The Black Hole ball (BlackHoleBall.cs). The ball itself is only the event horizon, pure black;
// everything else is drawn here, on a square around it that always faces the camera, by tracing
// the light backwards from the camera past the hole:
//
//   - each ray bends towards the hole (the photon orbit equation of a Schwarzschild black hole,
//     integrated in steps: a'' = -1.5 h^2 x / r^5, units of the Schwarzschild radius), so the
//     accretion disk behind the hole shows above and below it - the "Interstellar" look - and a
//     thin bright photon ring hugs the shadow;
//   - a ray that falls in is black; a ray that crosses the disk picks up its light (hot and white
//     at the inner edge, orange further out, streaked by turbulence that spirals in, brighter on
//     the side coming towards us - Doppler beaming - and dimmer deep in the well - gravitational
//     redshift); the disk is thin gas, so a ray can cross it again further on;
//   - a ray that escapes shows what is behind the hole in that bent direction, read from the
//     camera's copy of the scene (_CameraOpaqueTexture): the stars and the track behind it bend
//     around the hole. BlackHoleBall turns that copy on for the camera while the ball is in use
//     (_Lensing); without it the ray just shows through, unbent.
//
// The bending is real close to the hole and eased off further out (_Bend): with the full
// strength the square was a round window full of mirrored track, and read as one big ball - the
// player could not tell what would fall. Now it is a warp that fades into the scene around it,
// with no edge, and the ball is the black shadow inside the thin photon ring.
//
// Queued just before the transparent queue (3000): the menus are a canvas drawn by this camera
// in that queue, and they must be on top of the hole, as must the trail, which is in front of it.
//
// Sizes are in radii of the ball (the sphere mesh's 0.5 x its scale), which is also the radius of
// the shadow, so what the player sees is what the track collides with.
Shader "RisingWay/Black Hole"
{
    Properties
    {
        _Reach ("Square size (ball radii)", Range(2, 6)) = 3.4
        _Bend ("Bending reach past the shadow (ball radii)", Range(0.1, 2)) = 0.7
        _MaxShift ("Most the bending moves the picture (ball radii)", Range(0, 2)) = 0.5
        // The Einstein ring's radius (ball radii): where a star right behind the hole shows as a
        // ring. Inside it, the far side's sky, turned round; outside, the sky pushed out into arcs.
        _Einstein ("Einstein ring (ball radii)", Range(1, 3)) = 2.2
        _InnerGlow ("Glow round the shadow", Range(0, 2)) = 0
        _GlowWidth ("Glow width (ball radii)", Range(0.05, 1)) = 0.32
        // Where the square sits: this far in front of the ball's centre (ball radii). Far enough
        // for the whole disk in play; just in front of the ball in the shop, so its lock (in front
        // of the ball) still covers it. BlackHoleBall sets it with the depth test below.
        _Lift ("Towards the camera (ball radii)", Range(0, 6)) = 3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        _DiskInner ("Disk inner edge (ball radii)", Range(1, 3)) = 1.25
        _DiskOuter ("Disk outer edge (ball radii)", Range(1.5, 6)) = 2.7
        _DiskTilt ("Disk tilt towards the camera (degrees)", Range(0, 45)) = 11
        _DiskRoll ("Disk roll (degrees)", Range(-45, 45)) = -12
        [HDR] _HotColor ("Inner (hot) colour", Color) = (4.2, 3.4, 2.4, 1)
        [HDR] _CoolColor ("Outer (cooler) colour", Color) = (1.7, 0.55, 0.12, 1)
        _DiskBrightness ("Disk brightness", Range(0, 4)) = 1.1
        _Doppler ("Doppler beaming", Range(0, 1.5)) = 0.75
        _Turbulence ("Turbulence", Range(0, 1)) = 0.6
        _Spin ("Disk spin", Range(0, 4)) = 1
        [HDR] _RingColor ("Photon ring colour", Color) = (3, 2.4, 1.7, 1)
        _Ring ("Photon ring", Range(0, 2)) = 0.7
        [Toggle] _Lensing ("Lensing (needs the camera's opaque texture)", Float) = 0
        // The brightest the light may be. In the shop it is held to 1 (BlackHoleBall): brighter,
        // it bloomed on a phone, a glow over the item above (its mystery box).
        _MaxLight ("Brightest light", Float) = 64
        // In the shop the balls are drawn only inside the list's mask (stencil 1, as
        // StencilledLit): BlackHoleBall sets Equal there; Always elsewhere.
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comparison", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "BlackHole"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            Stencil
            {
                Ref 1
                Comp [_StencilComp]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Reach;
                float _Bend;
                float _InnerGlow;
                float _MaxShift;
                float _Einstein;
                float _GlowWidth;
                float _Lift;
                float _DiskInner;
                float _DiskOuter;
                float _DiskTilt;
                float _DiskRoll;
                float4 _HotColor;
                float4 _CoolColor;
                float _DiskBrightness;
                float _Doppler;
                float _Turbulence;
                float _Spin;
                float4 _RingColor;
                float _Ring;
                float _Lensing;
                float _MaxLight;
            CBUFFER_END

            // The shadow of a Schwarzschild hole is 2.6 of its Schwarzschild radius: make it the ball.
            #define SHADOW_IN_RS 2.6
            #define STEPS 56

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centreWS : TEXCOORD1;
                float radius : TEXCOORD2;          // the ball's, in world units
                float2 corner : TEXCOORD3;         // -1..1 across the square
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float radius = length(TransformObjectToWorldDir(float3(1, 0, 0), false)) * 0.5;
                float3 centreVS = TransformWorldToView(centre);
                float2 corner = v.positionOS.xy * 2.0;
                float3 positionVS = centreVS + float3(corner * radius * _Reach, 0);
                // In front of everything the disk can reach, towards the camera.
                positionVS.z += radius * _Lift;
                o.positionCS = TransformWViewToHClip(positionVS);
                o.positionWS = mul(UNITY_MATRIX_I_V, float4(positionVS, 1)).xyz;
                o.centreWS = centre;
                o.radius = radius;
                o.corner = corner;
                return o;
            }

            // Value noise in 3D, for the disk's turbulence: cheap, and only ever evaluated where a
            // ray crosses the disk.
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x),
                                 lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                                 lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // The disk's light where a ray crosses it (all in units of the Schwarzschild radius).
            // Returns colour in rgb and how much of the ray's light it takes in a.
            #define FLOW_PERIOD 6.0

            // The disk's streaks at this angle (already turned) and radius; seed, another copy.
            float streaksAt(float angle, float r, float seed)
            {
                float wound = angle + log(r) * 2.2;
                // Short curved strokes along the flow, three sizes of them (long rings, as
                // before, read as a still pattern).
                float3 q = float3(cos(wound) * 5.0, sin(wound) * 5.0, log(r) * 13.0 + seed);
                return noise(q) * 0.5 + noise(q * 2.1 + 11.0) * 0.32 + noise(q * 4.3 + 29.0) * 0.18;
            }

            float4 diskAt(float3 hit, float3 rayDir, float3 n, float3 e1, float3 e2, float rin, float rout)
            {
                float r = length(hit);
                float x = saturate((r - rin) / (rout - rin));              // 0 inner .. 1 outer
                // Right from the shadow's edge (BlackHoleSetup puts the inner edge there: a gap
                // left an empty band round the hole), brightest inside, fading out softly.
                float edge = smoothstep(0.0, 0.03, x) * (1.0 - smoothstep(0.3, 1.0, x));
                float profile = 0.35 + 0.9 * pow(1.0 - x, 1.6);

                // Turbulence that spirals inwards: the angle wound by how fast the gas turns there
                // (Kepler: faster inside), then noise on a circle so the angle has no seam.
                //
                // Turning faster inside than outside, the gas winds itself up: left running, the
                // broad streaks of the first seconds became fine rings after a minute or so, and
                // the disk looked still. So the pattern is two copies, each turning for only
                // FLOW_PERIOD seconds before starting over, unwound, while the other is at its
                // clearest - each fades out before it restarts. The disk always looks as it does
                // just after it starts turning. (It also keeps the angles small: _Time grows
                // without end, and a phone's floats lose the detail of a big angle.)
                float angle = atan2(dot(hit, e2), dot(hit, e1));
                float omega = _Spin * 2.2 * pow(rin / r, 1.5);
                float phase = _Time.y / FLOW_PERIOD;
                float age1 = frac(phase), age2 = frac(phase + 0.5);
                float w1 = 1.0 - abs(1.0 - 2.0 * age1);                      // 0 as it restarts
                float w2 = 1.0 - w1;
                float s1 = streaksAt(angle + omega * age1 * FLOW_PERIOD, r, 0.0);
                float s2 = streaksAt(angle + omega * age2 * FLOW_PERIOD, r, 23.0);
                // Two noises averaged are flatter than one: put the contrast back.
                float streaks = 0.5 + (s1 * w1 + s2 * w2 - 0.5) / sqrt(w1 * w1 + w2 * w2);
                float density = edge * lerp(1.0, 0.25 + 1.2 * saturate(streaks), _Turbulence);

                // Hotter inside; dimmer deep in the well (gravitational redshift).
                float3 colour = lerp(_HotColor.rgb, _CoolColor.rgb, pow(x, 0.55));
                float redshift = sqrt(saturate(1.0 - 1.0 / r));
                // Beaming: the gas moving towards the camera is brighter and a little bluer.
                float3 flow = normalize(cross(n, hit));
                float towards = dot(flow, -rayDir);
                // Toned down from the physics (as Interstellar did): full strength, one side was ten
                // times the other - a white blaze on the right, next to nothing on the left.
                float beaming = pow(max(1.0 + _Doppler * 0.32 * towards, 0.05), 2.5);
                colour = lerp(colour, colour * float3(0.85, 0.95, 1.25), saturate(towards) * _Doppler * 0.5);

                float3 light = colour * density * profile * beaming * redshift * _DiskBrightness;
                // Thick and bright inside, thin and faint outside: the outer gas hides little of
                // what is behind it (opaque there, it drew a dark band round the hole). The same
                // for the side turning away, dimmed by beaming and the redshift: it hid the track
                // without lighting it, a dark crescent under the hole.
                float glowing = lerp(0.6, 1.0, saturate(beaming * redshift * 1.3));
                return float4(light, saturate(density * 0.9 * (1.0 - 0.85 * x)) * glowing);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float rs = i.radius / SHADOW_IN_RS;
                float reach = _Reach * SHADOW_IN_RS;                       // the square's reach, in rs
                float3 camera = _WorldSpaceCameraPos;
                float3 dir = normalize(i.positionWS - camera);

                // The disk's plane, fixed to the view so it always shows its best side.
                float tilt = radians(_DiskTilt), roll = radians(_DiskRoll);
                float3 nView = float3(-sin(roll) * cos(tilt), cos(roll) * cos(tilt), sin(tilt));
                float3 n = normalize(mul((float3x3)UNITY_MATRIX_I_V, nView));
                float3 e1 = normalize(cross(n, abs(n.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 e2 = cross(n, e1);
                float rin = _DiskInner * SHADOW_IN_RS, rout = _DiskOuter * SHADOW_IN_RS;

                // Start where the ray enters the sphere the effect lives in.
                float3 p = (camera - i.centreWS) / rs;
                float3 v = dir;
                float b = dot(p, v);
                float c = dot(p, p) - reach * reach;
                float disc = b * b - c;
                float fade = saturate((1.0 - max(abs(i.corner.x), abs(i.corner.y))) * 8.0);
                if (disc <= 0.0)
                    return half4(0, 0, 0, 0);
                p += v * max(-b - sqrt(disc), 0.0);

                float3 h = cross(p, v);
                float h2 = dot(h, h);                                        // conserved along the ray
                // How much of the bending is shown: all of it at the shadow's edge, fading with the
                // ray's distance from the hole (its impact parameter, sqrt(h2)), none at the rim.
                float impact = sqrt(h2);
                float edge = 1.0 - smoothstep(reach * 0.7, reach * 0.98, impact);
                float bend = exp(-max(impact - SHADOW_IN_RS, 0.0) / (_Bend * SHADOW_IN_RS)) * edge;
                float3 light = 0;
                float through = 1.0;                                         // light still let through
                float closest = 1e5;
                bool captured = false;

                [loop]
                for (int k = 0; k < STEPS; k++)
                {
                    float r2 = dot(p, p);
                    float r = sqrt(r2);
                    closest = min(closest, r);
                    if (r < 1.0) { captured = true; break; }
                    if (r > reach + 0.01 && dot(p, v) > 0.0) break;         // on its way out

                    float dt = clamp(0.12 * r, 0.04, 0.9);
                    float3 before = p;
                    v += (-1.5 * h2 / (r2 * r2 * r)) * p * dt;
                    p += v * dt;

                    float d0 = dot(before, n), d1 = dot(p, n);
                    if (d0 * d1 < 0.0)
                    {
                        float3 hit = lerp(before, p, d0 / (d0 - d1));
                        float rh = length(hit);
                        if (rh > rin && rh < rout)
                        {
                            float4 d = diskAt(hit, normalize(v), n, e1, e2, rin, rout);
                            light += through * d.rgb;
                            through *= 1.0 - d.a;
                        }
                    }
                    if (through < 0.02)
                        break;
                }

                // The photon ring: rays that skimmed the photon sphere (1.5 rs) circled it before
                // getting away, and it glows as a thin line just outside the shadow.
                if (!captured)
                    light += through * _RingColor.rgb * _Ring * exp(-pow((closest - 1.55) * 5.0, 2.0));

                // Hot light round the shadow - the inner disk's glow, bent all round the hole.
                // Without it there was an empty band between the shadow and the disk: dark in the
                // shop, and in play a bent picture of the sky laid over the track.
                float glow = captured ? 0.0 : exp(-max(impact - SHADOW_IN_RS, 0.0) / (_GlowWidth * SHADOW_IN_RS));
                light += through * lerp(_HotColor.rgb, _CoolColor.rgb, 0.3) * (0.5 * _InnerGlow) * glow * glow;
                float covered = saturate(glow * glow * _InnerGlow); // only where it is bright: wider, it was a dark ring

                float3 behind = 0;
                float alpha;
                if (_Lensing > 0.5)
                {
                    if (captured)
                        alpha = 1.0;
                    else
                    {
                        // Gravitational lensing of what is behind, as a point mass bends it - the lens
                        // equation: a pixel at angle theta from the hole shows the sky at
                        // beta = theta - thetaE^2 / theta. Outside the Einstein ring (thetaE) the sky
                        // is pushed outwards and stretched round the hole into arcs (a star right
                        // behind becomes a ring); inside it, between the ring and the shadow, the sky
                        // from the far side of the hole shows, turned round, squeezed into a thin
                        // band. All on the screen, in ball radii, so it only ever reads the
                        // picture close round the hole (the old version followed each bent ray off
                        // across the sky - the far side mirrored, the sky over the track; held back,
                        // it was only a magnifying glass). The disk is ray traced above, so its far
                        // side rises over the shadow and shows again under it, as it should.
                        float4 here = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                        float4 mid = ComputeScreenPos(TransformWorldToHClip(i.centreWS));
                        float4 rim = ComputeScreenPos(TransformWorldToHClip(i.centreWS + UNITY_MATRIX_I_V._m00_m10_m20 * i.radius));
                        float2 own = here.xy / here.w;
                        float2 centre = mid.xy / mid.w;
                        float2 px = _ScreenParams.xy;                          // uv to pixels: the same scale both ways
                        float radiusPx = max(length((rim.xy / rim.w - centre) * px), 1e-3);
                        float2 offset = (own - centre) * px / radiusPx;        // in ball radii
                        float theta = max(length(offset), 1e-3);
                        // The deflection fades to nothing at the square's edge, so it meets the scene
                        // round it without a seam.
                        float taper = 1.0 - smoothstep(_Reach * 0.55, _Reach * 0.97, theta);
                        float beta = theta - _Einstein * _Einstein / theta * taper;
                        float2 uv = centre + offset / theta * beta * radiusPx / px;
                        // Read from off the screen, the copy has nothing: ease back to unbent there.
                        float2 outside = max(-uv, uv - 1.0);
                        uv = lerp(uv, own, saturate(max(outside.x, outside.y) * 3.0));
                        behind = SampleSceneColor(saturate(uv));
                        // Towards the rim what is already there shows instead (the same scene,
                        // unbent), so the square has no edge whatever was drawn before it.
                        behind *= through * edge;
                        // Just outside the shadow the light has come round the hole from all over
                        // the sky - a busy, mirrored picture: the glow covers it.
                        behind *= 1.0 - covered;
                        alpha = 1.0 - through * (1.0 - edge);
                    }
                }
                else
                {
                    alpha = captured ? 1.0 : 1.0 - through * (1.0 - covered);
                }
                light = min(light, _MaxLight);
                return half4((light + behind) * fade, alpha * fade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
