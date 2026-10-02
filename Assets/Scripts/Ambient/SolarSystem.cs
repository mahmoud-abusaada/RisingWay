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
    private static readonly Color PlanetTrailColour = new Color(0.62f, 0.74f, 1f);
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

        moons.Add(new Moon(1.5f, 0.52f, 0.05f, mercury, solarSystemRotation));
        moons.Add(new Moon(1.3f, 0.56f, 0.05f, venus, solarSystemRotation));
        moons.Add(new Moon(1.5f, 0.61f, 0.075f, earth, solarSystemRotation));
        moons.Add(new Moon(1.3f, 0.66f, 0.06f, mars, solarSystemRotation));
        moons.Add(new Moon(1.1f, 0.73f, 0.075f, jupiter, solarSystemRotation));
        moons.Add(new Moon(0.9f, 0.9f, 0.075f, saturn, solarSystemRotation));
        moons.Add(new Moon(0.8f, 1f, 0.05f, uranus, solarSystemRotation));
        moons.Add(new Moon(0.6f, 1.25f, 0.05f, neptune, solarSystemRotation));
        moons.Add(new Moon(0.6f, 1.4f, 0.05f, pluto, solarSystemRotation));

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
            styleTrail(newMoon.GetChild(0).GetComponent<TrailRenderer>(), PlanetTrailColour, 0.7f, 0.5f);
            if (i == SATURN)
                addRings(newMoon);
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
            Utility.clearAllChilds(myMoonsParent);
            GameObject.Destroy(myMoonsParent.gameObject);
            myMoonsParent = null;
        }
    }

    void Update()
    {
        if (myMoonsParent != null)
            myMoonsParent.position = parent.position;
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
