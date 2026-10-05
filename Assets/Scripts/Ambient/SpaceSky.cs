using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The sky around the game: the glow of the galaxy (SpaceSky.shader, on a box around the camera),
/// the stars on it (StarField.shader, one mesh of points), and a few stars out in the space around
/// the track (NearStar.shader) that the ball climbs past. Three draw calls, nothing lit, nothing
/// sorted.
///
/// Everything in the sky follows one number, <see cref="visibility"/>: 0 is full daylight with no
/// space to be seen, 1 is deep space. AmbientEffectsController sets it today (the "stay in space"
/// setting, the climb out of the daytime sky); a day/night cycle would set the same number.
/// </summary>
public class SpaceSky : MonoBehaviour
{
    [SerializeField] private Material galaxyMaterial;
    [SerializeField] private Material starsMaterial;
    [Tooltip("How many stars. About 9,000 can be seen from Earth with the naked eye, over the whole sky.")]
    [SerializeField] private int starCount = 14000;
    [SerializeField] private int seed = 7;
    [Tooltip("How the band of the galaxy lies across the sky: degrees about X, Y and Z.")]
    [SerializeField] private Vector3 galaxyTilt = new Vector3(58f, 0f, 24f);
    [Tooltip("Degrees per second the whole sky turns about the vertical.")]
    [SerializeField] private float turnSpeed = 0.25f;

    // Stars out in the space around the track. The background stars are painted on the sky, at
    // an infinite distance; these are real places, a few hundred to a couple of thousand units
    // out, so they slide past as the camera turns and sink away as the ball climbs. One that falls
    // too far below, or is left too far behind, comes back somewhere above and ahead.
    [SerializeField] private Material nearStarsMaterial;
    [SerializeField] private int nearStarCount = 22;
    [SerializeField] private float nearStarMinDistance = 350f;
    [SerializeField] private float nearStarMaxDistance = 1800f;

    /// <summary>0 no space to be seen (daylight) .. 1 deep space.</summary>
    [Range(0f, 1f)] public float visibility = 1f;

    private static readonly int GalaxyToWorldId = Shader.PropertyToID("_GalaxyToWorld");
    private static readonly int VisibilityId = Shader.PropertyToID("_SpaceVisibility");

    private Transform cameraTransform;
    private float turned;
    private Mesh nearStars;
    private Vector3[] nearPositions;
    private Vector4[] nearCorners;
    private Color[] nearColours;
    private System.Random nearRandom;

    void Awake()
    {
        // Drawn around the camera wherever it is: bounds that are never culled.
        Bounds everywhere = new Bounds(Vector3.zero, Vector3.one * 1e6f);

        Mesh box = BuildBox();
        box.bounds = everywhere;
        addRenderer("Galaxy", box, galaxyMaterial);

        Mesh stars = buildStars(starCount, seed);
        stars.bounds = everywhere;
        addRenderer("Stars", stars, starsMaterial);

        if (nearStarsMaterial != null && nearStarCount > 0)
            buildNearStars(everywhere);

        // For anything else that needs to know what the sky looks like in some direction (the
        // Black Hole ball's bent light).
        if (galaxyMaterial != null)
        {
            Shader.SetGlobalTexture("_GalaxyGlowTex", galaxyMaterial.GetTexture("_MainTex"));
            Shader.SetGlobalFloat("_GalaxyGlowExposure", galaxyMaterial.GetFloat("_Exposure"));
        }

        apply();
    }

    private void addRenderer(string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void LateUpdate()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
        {
            transform.position = cameraTransform.position;
            keepNearStarsAround(cameraTransform.position);
        }
        turned += turnSpeed * Time.unscaledDeltaTime;
        apply();
    }

    private void apply()
    {
        Quaternion rotation = Quaternion.AngleAxis(turned, Vector3.up) * Quaternion.Euler(galaxyTilt);
        Shader.SetGlobalMatrix(GalaxyToWorldId, Matrix4x4.Rotate(rotation));
        Shader.SetGlobalFloat(VisibilityId, visibility);
    }

    // ---------------------------------------------------------------------------------------
    // Near stars. Their mesh holds world positions, so its object sits at the world origin, not
    // under this one (which follows the camera). Only a star that moves rewrites the vertices.
    // ---------------------------------------------------------------------------------------
    private void buildNearStars(Bounds everywhere)
    {
        nearRandom = new System.Random(seed + 101);
        int n = nearStarCount;
        nearPositions = new Vector3[n * 4];
        nearCorners = new Vector4[n * 4];
        nearColours = new Color[n * 4];
        int[] triangles = new int[n * 6];
        Vector3 around = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            placeNearStar(i, around, false);
            int v = i * 4;
            triangles[i * 6] = v; triangles[i * 6 + 1] = v + 1; triangles[i * 6 + 2] = v + 2;
            triangles[i * 6 + 3] = v; triangles[i * 6 + 4] = v + 2; triangles[i * 6 + 5] = v + 3;
        }
        nearStars = new Mesh { name = "Near Stars" };
        nearStars.MarkDynamic();
        nearStars.vertices = nearPositions;
        nearStars.SetUVs(0, nearCorners);
        nearStars.colors = nearColours;
        nearStars.triangles = triangles;
        nearStars.bounds = everywhere;

        GameObject go = new GameObject("Near Stars");
        go.layer = gameObject.layer;
        go.AddComponent<MeshFilter>().sharedMesh = nearStars;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = nearStarsMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void keepNearStarsAround(Vector3 camera)
    {
        if (nearStars == null)
            return;
        bool moved = false;
        for (int i = 0; i < nearStarCount; i++)
        {
            Vector3 d = nearPositions[i * 4] - camera;
            float across = new Vector2(d.x, d.z).magnitude;
            // Sunk far below as the ball climbed, or left behind by the path: bring it back above,
            // where it will come down into view.
            if (d.y < -nearStarMaxDistance * 0.6f || across > nearStarMaxDistance * 1.25f)
            {
                placeNearStar(i, camera, true);
                moved = true;
            }
        }
        if (moved)
        {
            nearStars.vertices = nearPositions;
            nearStars.SetUVs(0, nearCorners);
            nearStars.colors = nearColours;
        }
    }

    // The same few colours real stars have (StarColours), brighter than the background ones: these
    // are the nearest suns.
    private void placeNearStar(int i, Vector3 around, bool above)
    {
        System.Func<float> next = () => (float)nearRandom.NextDouble();
        float angle = next() * Mathf.PI * 2f;
        float distance = Mathf.Lerp(nearStarMinDistance, nearStarMaxDistance, Mathf.Sqrt(next()));
        // Mostly below and level with the camera, which looks down on the track; a new one comes
        // in from above.
        float height = above ? Mathf.Lerp(250f, 700f, next()) : Mathf.Lerp(-900f, 350f, next());
        Vector3 position = around + new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);

        float pick = next(), sum = 0f;
        int c = 0;
        for (; c < StarColours.Length - 1; c++)
        {
            sum += StarColourShare[c];
            if (pick < sum)
                break;
        }
        float brightness = Mathf.Lerp(0.8f, 2.6f, next() * next());
        Color colour = StarColours[c] * brightness;
        float halfSize = Mathf.Lerp(35f, 90f, next());
        float phase = next();
        for (int k = 0; k < 4; k++)
        {
            nearPositions[i * 4 + k] = position;
            nearCorners[i * 4 + k] = new Vector4(k == 0 || k == 3 ? -1f : 1f, k < 2 ? -1f : 1f, halfSize, phase);
            nearColours[i * 4 + k] = colour;
        }
    }

    // A unit cube seen from inside. The shader uses each vertex only as a direction.
    internal static Mesh BuildBox()
    {
        Vector3[] v =
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
        };
        int[] t =
        {
            0, 1, 2, 0, 2, 3, 5, 4, 7, 5, 7, 6, 4, 0, 3, 4, 3, 7,
            1, 5, 6, 1, 6, 2, 3, 2, 6, 3, 6, 7, 4, 5, 1, 4, 1, 0
        };
        Mesh m = new Mesh { name = "Space Sky Box" };
        m.vertices = v;
        m.triangles = t;
        return m;
    }

    // ---------------------------------------------------------------------------------------
    // The stars, in galactic coordinates (the galaxy's plane is y = 0, its centre is +z, to match
    // the galaxy texture).
    //
    // Brightness: for every magnitude fainter there are about three times as many stars, which is
    // what makes a real sky look the way it does - a handful of bright stars, a dust of faint ones.
    // Where: two in five lie in the band of the galaxy, denser towards its centre; the rest are
    // spread evenly. Colour: mostly white and blue-white, some yellow, a few orange and red, the
    // colours stars actually are - pale, not saturated.
    // ---------------------------------------------------------------------------------------
    private const float BRIGHTEST_MAGNITUDE = -1.0f;
    private const float FAINTEST_MAGNITUDE = 6.6f;

    private static readonly Color[] StarColours =
    {
        new Color(0.62f, 0.72f, 1.00f), // O/B: blue
        new Color(0.78f, 0.85f, 1.00f), // A: blue-white
        new Color(1.00f, 1.00f, 1.00f), // F: white
        new Color(1.00f, 0.96f, 0.84f), // G: yellow-white (the Sun)
        new Color(1.00f, 0.86f, 0.66f), // K: orange
        new Color(1.00f, 0.70f, 0.50f), // M: red
    };
    // How common each is among the stars bright enough to see.
    private static readonly float[] StarColourShare = { 0.12f, 0.24f, 0.22f, 0.16f, 0.18f, 0.08f };

    private static Mesh buildStars(int count, int seed)
    {
        System.Random random = new System.Random(seed);
        System.Func<float> next = () => (float)random.NextDouble();

        List<Vector3> positions = new List<Vector3>(count * 4);
        List<Vector4> corners = new List<Vector4>(count * 4);
        List<Color> colours = new List<Color>(count * 4);
        List<int> triangles = new List<int>(count * 6);

        float span = FAINTEST_MAGNITUDE - BRIGHTEST_MAGNITUDE;
        float growth = Mathf.Log10(3f); // stars per magnitude: x3
        for (int i = 0; i < count; i++)
        {
            Vector3 direction;
            if (next() < 0.4f)
            {
                // In the band: latitude within a few degrees of the plane, longitude anywhere but
                // more often towards the centre.
                float latitude = gaussian(next) * 9f * Mathf.Deg2Rad;
                float longitude = (next() < 0.45f ? gaussian(next) * 50f : next() * 360f - 180f) * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Cos(latitude) * Mathf.Sin(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Cos(longitude));
            }
            else
            {
                float y = next() * 2f - 1f;
                float a = next() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(1f - y * y);
                direction = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
            }

            // Magnitude with the right share of faint stars: invert N(<m) ~ 3^m.
            float u = next();
            float magnitude = BRIGHTEST_MAGNITUDE + Mathf.Log10(1f + u * (Mathf.Pow(10f, growth * span) - 1f)) / growth;
            float faintness = (magnitude - BRIGHTEST_MAGNITUDE) / span; // 0 brightest .. 1 faintest
            // Light falls by 2.5x a magnitude; the eye and a screen compress that, or only the
            // brightest dozen would show at all.
            float flux = Mathf.Pow(10f, -0.4f * (magnitude - 1.5f));
            float brightness = Mathf.Min(Mathf.Pow(flux, 0.4f), 1.8f);
            float radius = Mathf.Lerp(1.6f, 8f, Mathf.Pow(1f - faintness, 2.4f));

            float pick = next(), sum = 0f;
            int c = 0;
            for (; c < StarColours.Length - 1; c++)
            {
                sum += StarColourShare[c];
                if (pick < sum)
                    break;
            }
            Color colour = StarColours[c] * brightness;
            colour.a = faintness;

            float phase = next();
            int first = positions.Count;
            for (int k = 0; k < 4; k++)
            {
                positions.Add(direction);
                corners.Add(new Vector4(k == 0 || k == 3 ? -1f : 1f, k < 2 ? -1f : 1f, radius, phase));
                colours.Add(colour);
            }
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        Mesh m = new Mesh { name = "Star Field" };
        m.indexFormat = count * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertices(positions);
        m.SetUVs(0, corners);
        m.SetColors(colours);
        m.SetTriangles(triangles, 0, false);
        return m;
    }

    private static float gaussian(System.Func<float> next)
    {
        float u1 = Mathf.Max(next(), 1e-6f), u2 = next();
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }
}
