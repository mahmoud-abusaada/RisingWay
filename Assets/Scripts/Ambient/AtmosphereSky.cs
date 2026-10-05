using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The sky of a run, by mode (docs/game-modes-plan.md, section 3). Atmosphere.shader draws it on a
/// box around the camera, over the galaxy and the stars: a planet's air and the planet below it.
///
/// A - Standard and Insane: the run starts at dawn just above a planet, the Sun rising on its
/// horizon (the real Sun of the solar system, which sits there). As the ball climbs, the sky goes
/// dark from the top down, the horizon drops away and the planet becomes a ball under the track
/// with a glowing rim: deep space. Insane's dawn is redder.
///
/// B - Chill: the height does not matter; the clock does. A day goes round in a few minutes -
/// morning, noon, sunset, night with the stars out, sunrise - and the Sun and planets really rise
/// and set with it (AmbientEffectsController lifts the solar system).
///
/// Nothing shows in the menus, with "stay in space" on, or before the first run: space as before.
/// </summary>
public class AtmosphereSky : MonoBehaviour
{
    // A: the climb from the planet's air into space, by the camera's height.
    private const float CLIMB_FROM = 10f;
    private const float CLIMB_TO = 320f; // space by a score of about 800
    private const float DAWN_SUN_UP = 0.15f;
    // How far below level the horizon lies: just under level at the start, far below from space.
    private const float HORIZON_START = 0.105f; // sin 6 degrees: the Sun (about 4.6 below level) is just up
    private const float HORIZON_SPACE = 0.6f;   // sin 37 degrees: the planet is a ball under the track, its rim low on the screen

    // B: one whole day, starting mid-morning.
    public const float CHILL_DAY_SECONDS = 240f;
    private const float CHILL_START_SUN_UP = 0.45f;
    private const float CHILL_MAX_SUN_DEGREES = 50f;

    private const float FADE_PER_SECOND = 0.6f;
    // The planet's wall in the depth buffer: farther than any of the track, nearer than the Sun.
    private const float GROUND_DISTANCE = 600f;

    private static readonly Color WarmStandard = new Color(1.0f, 0.62f, 0.30f);
    private static readonly Color WarmInsane = new Color(1.0f, 0.30f, 0.26f);
    private static readonly Color WarmChill = new Color(1.0f, 0.68f, 0.40f);

    private static readonly int AmountId = Shader.PropertyToID("_AtmoAmount");
    private static readonly int SunUpId = Shader.PropertyToID("_AtmoSunUp");
    private static readonly int ThinId = Shader.PropertyToID("_AtmoThin");
    private static readonly int HorizonId = Shader.PropertyToID("_AtmoHorizon");
    private static readonly int GroundDistanceId = Shader.PropertyToID("_AtmoGroundDistance");
    private static readonly int SunDirId = Shader.PropertyToID("_AtmoSunDir");
    private static readonly int WarmId = Shader.PropertyToID("_AtmoWarm");
    private static readonly int SunPositionId = Shader.PropertyToID("_SunPosition");

    private float amount, target;
    private float chillClock;
    private float sunUp = DAWN_SUN_UP, thin, horizon = HORIZON_START;
    private Color warm = WarmStandard;
    private Color menuBackground;
    private bool running;

    // Editor captures (StoreCapture): hold the sky at a point - "-skyClimb 0..1", "-skyDay 0..1".
    private float forcedClimb = -1f, forcedDay = -1f;

    /// <summary>How far up the Sun is for Chill, -1..1 (AmbientEffectsController lifts the solar system by it).</summary>
    public float ChillSunUp { get; private set; } = CHILL_START_SUN_UP;
    public bool ChillOn => running && GameMode.Current == RunMode.Chill;

    void Awake()
    {
        Material material = Resources.Load<Material>("Sky/Atmosphere");
        if (material == null)
        {
            Debug.LogWarning("[Atmosphere] Resources/Sky/Atmosphere.mat is missing: no sky.");
            enabled = false;
            return;
        }
        GameObject go = new GameObject("Atmosphere");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        Mesh box = SpaceSky.BuildBox();
        box.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f); // drawn wherever the camera is
        go.AddComponent<MeshFilter>().sharedMesh = box;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Shader.SetGlobalFloat(AmountId, 0f);
        Shader.SetGlobalFloat(GroundDistanceId, GROUND_DISTANCE);
#if UNITY_EDITOR
        forcedClimb = ArgFloat("-skyClimb");
        forcedDay = ArgFloat("-skyDay");
#endif
    }

    /// <summary>A run starts: the mode's sky comes in.</summary>
    public void RunStarted()
    {
        chillClock = 0f;
        ChillSunUp = CHILL_START_SUN_UP;
        running = true;
        target = PlayerStats.Instance.isStayInSpaceOn() ? 0f : 1f;
        Debug.Log("[Atmosphere] run in " + GameMode.Current + (forcedClimb >= 0f ? " climb " + forcedClimb : "") + (forcedDay >= 0f ? " day " + forcedDay : ""));
        if (Camera.main != null)
        {
            // The sky shows the camera's clear colour through it at night: space is black.
            menuBackground = Camera.main.backgroundColor;
            Camera.main.backgroundColor = Color.black;
        }
    }

    /// <summary>Back to the menus: space again.</summary>
    public void BackToMenus()
    {
        if (running && Camera.main != null)
            Camera.main.backgroundColor = menuBackground;
        running = false;
        target = 0f;
    }

    /// <summary>How much of the galaxy and the stars the sky leaves to be seen (SpaceSky.visibility).</summary>
    public float SpaceShown()
    {
        float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, -0.22f, sunUp));
        float cover = Mathf.Lerp(1f, 0.18f, night) * (1f - thin);
        return 1f - amount * cover;
    }

    void Update()
    {
        if (!enabled)
            return;
        if (running && PlayerStats.Instance.isStayInSpaceOn())
            target = 0f;
        amount = Mathf.MoveTowards(amount, target, FADE_PER_SECOND * Time.unscaledDeltaTime);

        Camera cam = Camera.main;
        float height = cam != null ? cam.transform.position.y : 0f;

        if (GameMode.Current == RunMode.Chill)
        {
            if (running && !Utility.isGamePaused)
                chillClock += Time.unscaledDeltaTime;
            float phase = Mathf.Asin(CHILL_START_SUN_UP) + chillClock / CHILL_DAY_SECONDS * 2f * Mathf.PI;
            if (forcedDay >= 0f)
                phase = forcedDay * 2f * Mathf.PI;
            ChillSunUp = Mathf.Sin(phase);
            sunUp = ChillSunUp;
            thin = 0f;
            horizon = Mathf.Lerp(HORIZON_START, 0.26f, Mathf.InverseLerp(CLIMB_FROM, CLIMB_TO * 1.5f, height));
            warm = WarmChill;
        }
        else
        {
            float climb = forcedClimb >= 0f ? forcedClimb : Mathf.InverseLerp(CLIMB_FROM, CLIMB_TO, height);
            thin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, climb));
            horizon = Mathf.Lerp(HORIZON_START, HORIZON_SPACE, Mathf.Pow(climb, 1.4f));
            sunUp = DAWN_SUN_UP + 0.08f * climb;
            warm = GameMode.Current == RunMode.Insane ? WarmInsane : WarmStandard;
        }

        Vector3 sunDir = Vector3.forward;
        if (cam != null)
        {
            Vector3 toSun = (Vector3)Shader.GetGlobalVector(SunPositionId) - cam.transform.position;
            if (toSun.sqrMagnitude > 1f)
                sunDir = toSun.normalized;
        }

        Shader.SetGlobalFloat(AmountId, amount);
        Shader.SetGlobalFloat(SunUpId, sunUp);
        Shader.SetGlobalFloat(ThinId, thin);
        Shader.SetGlobalFloat(HorizonId, horizon);
        Shader.SetGlobalVector(SunDirId, sunDir);
        Shader.SetGlobalColor(WarmId, warm);
    }

    /// <summary>How high the solar system should sit so the Sun is where Chill's clock puts it.</summary>
    public static float SolarLiftFor(float sunUp)
    {
        // The Sun is 1500 out and 120 below the solar system's centre (SolarSystem.initSolarSystem).
        float degrees = Mathf.Clamp(sunUp, -1f, 1f) * CHILL_MAX_SUN_DEGREES;
        return 1500f * Mathf.Tan(degrees * Mathf.Deg2Rad) + 120f;
    }

#if UNITY_EDITOR
    private static float ArgFloat(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                return v;
        return -1f;
    }
#endif
}
