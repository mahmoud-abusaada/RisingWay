using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SolarSystem : MonoBehaviour
{
    [SerializeField] private Transform planet;
    [SerializeField] private Transform parent;
    [SerializeField] private Transform solarParent;
    [SerializeField] private Transform sunTrail;
    [SerializeField] private Material mercury;
    [SerializeField] private Material venus;
    [SerializeField] private Material earth;
    [SerializeField] private Material mars;
    [SerializeField] private Material jupiter;
    [SerializeField] private Material saturn;
    [SerializeField] private Material uranus;
    [SerializeField] private Material neptune;
    [SerializeField] private Material pluto;
    // The look of it (SpaceSkySetup makes and assigns these): a glow around the Sun that does not
    // need bloom, thin fading trails, Saturn's rings. Left empty, each is simply not used.
    [SerializeField] private Material coronaMaterial;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private Material ringMaterial;
    // The planets are drawn this much larger than the orbits' scale would make them: at true
    // proportion they are a pixel or two across from the track.
    [SerializeField] private float planetSize = 1.7f;

    private static readonly int SunPositionId = Shader.PropertyToID("_SunPosition");
    private const int SATURN = 5;
    private static readonly Color SunTrailColour = new Color(1f, 0.78f, 0.42f);
    private Mesh quad;

    private Transform myMoonsParent;
    private List<Transform> planetTransforms = new List<Transform>();
    private List<Transform> moonRotatingTransforms = new List<Transform>();
    private List<Moon> moons = new List<Moon>();
    private int moonsCount = 0;
    private float moonScale;
    private int renderQueue = 2400;
    private static int renderModifier = 0;

    public void initSolarSystem()
    {
        parent.localPosition = new Vector3(0, -120, 1500);
        sunTrail.gameObject.SetActive(true);
        styleTrail(sunTrail.GetComponent<TrailRenderer>(), SunTrailColour, 2.2f, 0.9f);
        addCorona();
        setMoons();
        parent.localScale = new Vector3(120, 120, 120);
        parent.localEulerAngles = new Vector3(0, -15, 15);
        solarParent.localEulerAngles = new Vector3(0, -30, 0);
        solarParent.gameObject.SetActive(true);
    }

    public void enableRealOrbits()
    {
        foreach (Transform moon in planetTransforms)
        {
            moon.GetChild(0).gameObject.SetActive(true);
        }
    }

    public void disableRealOrbits()
    {
        foreach (Transform moon in planetTransforms)
        {
            moon.GetChild(0).gameObject.SetActive(false);
        }
    }

    public void setMoons(bool isCurrentBall = false)
    {
        clearMoons();
        if (myMoonsParent == null)
        {
            myMoonsParent = new GameObject(parent.name + "Moons").transform;
            myMoonsParent.position = parent.position;

            myMoonsParent.parent = parent;

            myMoonsParent.localScale = Vector3.one;
        }

        Vector3 solarSystemRotation = new Vector3(0, 0, 90);

        // Sizes a little nearer the real order (the giants larger, the rocky ones small), spread a
        // little wider so the giants' moons have room.
        moons.Add(new Moon(1.5f, 0.50f, 0.034f, mercury, solarSystemRotation));
        moons.Add(new Moon(1.3f, 0.555f, 0.052f, venus, solarSystemRotation));
        moons.Add(new Moon(1.5f, 0.615f, 0.056f, earth, solarSystemRotation));
        moons.Add(new Moon(1.3f, 0.675f, 0.042f, mars, solarSystemRotation));
        moons.Add(new Moon(1.1f, 0.78f, 0.105f, jupiter, solarSystemRotation));
        moons.Add(new Moon(0.9f, 0.96f, 0.09f, saturn, solarSystemRotation));
        moons.Add(new Moon(0.8f, 1.11f, 0.066f, uranus, solarSystemRotation));
        moons.Add(new Moon(0.6f, 1.26f, 0.064f, neptune, solarSystemRotation));
        moons.Add(new Moon(0.6f, 1.38f, 0.03f, pluto, solarSystemRotation));

        for (int i = 0; i < moons.Count; i++)
        {
            Transform newMoonRotatingParent = new GameObject("Moon" + i + "RotatingParent").transform;
            newMoonRotatingParent.parent = myMoonsParent;
            newMoonRotatingParent.localPosition = Vector3.zero;
            newMoonRotatingParent.localScale = Vector3.one;

            Transform newMoon = Instantiate(planet, Vector3.zero, Quaternion.identity, newMoonRotatingParent);
            newMoon.localPosition = new Vector3(0, 0, moons[i].distance);
            float scale = moons[i].scale * planetSize;

            newMoon.localScale = new Vector3(scale, scale, scale);

            if (moons[i].material != null)
                newMoon.GetComponent<Renderer>().material = moons[i].material;

            newMoonRotatingParent.localEulerAngles = moons[i].rotation;

            // Debug.Log("new moon position = " + newMoon.position);
            newMoonRotatingParent.Rotate(new Vector3(0, Random.Range(0, 360), 0));
            planetTransforms.Add(newMoon);
            moonRotatingTransforms.Add(newMoonRotatingParent);
            newMoon.GetChild(0).gameObject.SetActive(true);
            // Each planet's trail in its own colour, wider for the giants.
            styleTrail(newMoon.GetChild(0).GetComponent<TrailRenderer>(), PlanetTrailColours[i], 0.45f + moons[i].scale * 6f, 0.6f);
            if (i == SATURN)
                addRings(newMoon);
            addSatellites(i, newMoonRotatingParent, newMoon, scale);
        }
    }

    // ---- Moons of the planets --------------------------------------------------------------------
    // Each on its own little orbit round its planet, with a short fine trail in the planet's colour,
    // lighter: loops drawn along the planet's way round the Sun. They ride on a holder beside the
    // planet, not on the planet, which spins.

    // The trail colour of each planet, from its look.
    private static readonly Color[] PlanetTrailColours =
    {
        new Color(0.78f, 0.70f, 0.62f), // Mercury, warm grey
        new Color(1.00f, 0.84f, 0.50f), // Venus, pale gold
        new Color(0.35f, 0.70f, 1.00f), // Earth, blue
        new Color(1.00f, 0.45f, 0.28f), // Mars, rust red
        new Color(1.00f, 0.66f, 0.38f), // Jupiter, orange tan
        new Color(1.00f, 0.86f, 0.55f), // Saturn, gold
        new Color(0.45f, 0.95f, 0.95f), // Uranus, cyan
        new Color(0.35f, 0.50f, 1.00f), // Neptune, deep blue
        new Color(0.80f, 0.68f, 1.00f), // Pluto, lavender
    };

    private struct Satellite
    {
        public int planet;     // index in the planets above
        public float orbit;    // in the planet's radii
        public float size;     // in the planet's radii
        public float speed;    // degrees a second
        public int look;       // 0 grey rock, 1 sulphur yellow, 2 ice white, 3 hazy orange
        public Satellite(int planet, float orbit, float size, float speed, int look)
        { this.planet = planet; this.orbit = orbit; this.size = size; this.speed = speed; this.look = look; }
    }

    private static readonly Satellite[] Satellites =
    {
        new Satellite(2, 2.2f, 0.32f, 160f, 0),  // the Moon
        new Satellite(3, 1.8f, 0.16f, 260f, 0),  // Phobos
        new Satellite(3, 2.5f, 0.13f, 190f, 0),  // Deimos
        new Satellite(4, 1.5f, 0.12f, 240f, 1),  // Io
        new Satellite(4, 1.9f, 0.10f, 190f, 2),  // Europa
        new Satellite(4, 2.4f, 0.15f, 140f, 0),  // Ganymede
        new Satellite(4, 2.9f, 0.13f, 110f, 0),  // Callisto
        new Satellite(5, 2.6f, 0.15f, 120f, 3),  // Titan, past the rings
        new Satellite(5, 3.1f, 0.08f, 95f, 2),   // Iapetus
        new Satellite(6, 1.9f, 0.14f, 170f, 2),  // Titania
        new Satellite(6, 2.5f, 0.13f, 130f, 0),  // Oberon
        new Satellite(7, 2.0f, 0.18f, 150f, 2),  // Triton
        new Satellite(8, 2.2f, 0.45f, 120f, 0),  // Charon
    };

    private readonly List<Transform> satellitePivots = new List<Transform>();
    private readonly List<float> satelliteSpeeds = new List<float>();
    private Material[] satelliteLooks;

    private Material satelliteLook(int look)
    {
        if (satelliteLooks == null)
        {
            satelliteLooks = new Material[4];
            Material[] from = { mercury, venus, pluto, venus };
            float[] brightness = { 1.05f, 1.35f, 1.5f, 0.9f };
            for (int i = 0; i < 4; i++)
            {
                if (from[i] == null)
                    continue;
                satelliteLooks[i] = new Material(from[i]) { name = "Moon look " + i };
                if (satelliteLooks[i].HasProperty("_Brightness"))
                    satelliteLooks[i].SetFloat("_Brightness", brightness[i]);
                if (satelliteLooks[i].HasProperty("_Shadow"))
                    satelliteLooks[i].SetFloat("_Shadow", 0.3f); // small: a black dot reads as a hole, not a moon
                if (satelliteLooks[i].HasProperty("_Atmosphere"))
                    satelliteLooks[i].SetColor("_Atmosphere", Color.black);
            }
        }
        return satelliteLooks[Mathf.Clamp(look, 0, 3)];
    }

    private void addSatellites(int index, Transform orbit, Transform planetBody, float planetScale)
    {
        float radius = planetScale * 0.5f; // the planet prefab is a unit sphere
        Transform holder = null;
        foreach (Satellite s in Satellites)
        {
            if (s.planet != index)
                continue;
            if (holder == null)
            {
                holder = new GameObject("MoonsOf" + index).transform;
                holder.SetParent(orbit, false);
                holder.localPosition = planetBody.localPosition;
                // Saturn's moons go round in its tipped equator, with the rings.
                holder.localRotation = index == SATURN ? planetBody.localRotation : Quaternion.Euler(Random.Range(-12f, 12f), 0f, 0f);
            }
            Transform pivot = new GameObject("MoonPivot").transform;
            pivot.SetParent(holder, false);
            pivot.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), 0f);
            Transform body = Instantiate(planet, pivot);
            body.name = "Moon";
            body.localPosition = new Vector3(0f, 0f, radius * s.orbit);
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one * Mathf.Max(radius * 2f * s.size, 0.004f);
            Material look = satelliteLook(s.look);
            if (look != null)
                body.GetComponent<Renderer>().sharedMaterial = look;
            if (body.childCount > 0)
            {
                TrailRenderer trail = body.GetChild(0).GetComponent<TrailRenderer>();
                body.GetChild(0).gameObject.SetActive(trail != null);
                if (trail != null)
                {
                    styleTrail(trail, Color.Lerp(PlanetTrailColours[index], Color.white, 0.45f), 0.12f, 0.45f);
                    trail.time = 0.35f;
                    trail.Clear();
                }
            }
            satellitePivots.Add(pivot);
            satelliteSpeeds.Add(s.speed * (Random.value < 0.15f ? -1f : 1f));
        }
    }

    // A trail that is a thin line of light fading to nothing, not a solid ribbon.
    private void styleTrail(TrailRenderer trail, Color colour, float width, float strength)
    {
        if (trail == null || trailMaterial == null)
            return;
        trail.sharedMaterial = trailMaterial;
        trail.widthMultiplier = width;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f));
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
            new[] { new GradientAlphaKey(strength, 0f), new GradientAlphaKey(strength * 0.35f, 0.35f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = fade;
        trail.numCapVertices = 0;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void addCorona()
    {
        if (coronaMaterial == null)
            return;
        Transform sun = sunTrail.parent;
        GameObject corona = new GameObject("Corona");
        corona.layer = sun.gameObject.layer;
        corona.transform.SetParent(sun, false);
        corona.AddComponent<MeshFilter>().sharedMesh = unitQuad();
        MeshRenderer r = corona.AddComponent<MeshRenderer>();
        r.sharedMaterial = coronaMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Saturn: tipped over by its 27 degrees, with the rings in its equator.
    private void addRings(Transform saturn)
    {
        saturn.localRotation = Quaternion.Euler(27f, 0f, 0f);
        if (ringMaterial == null)
            return;
        GameObject rings = new GameObject("Rings");
        rings.layer = saturn.gameObject.layer;
        rings.transform.SetParent(saturn, false);
        rings.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rings.transform.localScale = Vector3.one * 2.35f; // out to 2.35 of the planet's radius
        rings.AddComponent<MeshFilter>().sharedMesh = unitQuad();
        MeshRenderer r = rings.AddComponent<MeshRenderer>();
        r.sharedMaterial = ringMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private Mesh unitQuad()
    {
        if (quad == null)
        {
            quad = new Mesh { name = "Unit Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            // The corona is drawn around its centre whichever way the quad itself faces.
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 30f);
        }
        return quad;
    }

    public void clearMoons()
    {
        if (myMoonsParent != null)
        {
            planetTransforms.Clear();
            moonRotatingTransforms.Clear();
            moons.Clear();
            satellitePivots.Clear();
            satelliteSpeeds.Clear();
            Utility.clearAllChilds(myMoonsParent);
            GameObject.Destroy(myMoonsParent.gameObject);
            myMoonsParent = null;
        }
    }

    void Update()
    {
        if (myMoonsParent != null)
            myMoonsParent.position = parent.position;
        for (int i = 0; i < satellitePivots.Count; i++)
            if (satellitePivots[i] != null)
                satellitePivots[i].Rotate(0f, satelliteSpeeds[i] * Time.deltaTime, 0f, Space.Self);
        // Where the planets' light comes from (Planet.shader).
        Shader.SetGlobalVector(SunPositionId, sunTrail.parent.position);

        for (int i = 0; i < planetTransforms.Count; i++)
        {
            planetTransforms[i].Rotate(new Vector3(0, Time.deltaTime * -60f, 0));
            moonRotatingTransforms[i].Rotate(new Vector3(0, Time.deltaTime * -moons[i].speed * 150, 0));
        }
    }

    private void rotateAround(Transform obj, Vector3 point, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis);
        parent.position = obj.position + rot * point;
        parent.localRotation = rot;
    }
}
