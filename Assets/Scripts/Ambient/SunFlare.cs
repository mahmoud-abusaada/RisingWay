using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// When the Sun's lens flare (the scene's LensFlareComponentSRP on the Sun, its own look) is
/// hidden. The renderer's own test reads the depth buffer round the Sun, and that also counts the
/// planets and moons going round the Sun - they cross in front of it many times a minute, so the
/// flare dropped out with nothing on screen between the camera and the Sun. Here it is hidden by
/// what really stands in the way: the ball and the track (a ray from the camera to the Sun against
/// their colliders), and the planet of a run's sky once the Sun has set behind it (AtmosphereSky).
/// It fades in and out rather than switching.
/// </summary>
public class SunFlare : MonoBehaviour
{
    private const float FADE_PER_SECOND = 6f;
    // Only things near the camera can hide the Sun this way: the track and the ball, not the
    // solar system round the Sun.
    private const float NEAR = 400f;

    private static readonly int AmountId = Shader.PropertyToID("_AtmoAmount");
    private static readonly int HorizonId = Shader.PropertyToID("_AtmoHorizon");

    private LensFlareComponentSRP flare;
    private float baseIntensity, shown = 1f;

    /// <summary>Finds the Sun's flare and takes over hiding it.</summary>
    public static void Install()
    {
        foreach (LensFlareComponentSRP f in FindObjectsByType<LensFlareComponentSRP>(FindObjectsInactive.Exclude))
            if (f.enabled && f.GetComponent<SunFlare>() == null && f.gameObject.name == "Sun")
                f.gameObject.AddComponent<SunFlare>();
    }

    void Awake()
    {
        flare = GetComponent<LensFlareComponentSRP>();
        if (flare == null)
        {
            enabled = false;
            return;
        }
        baseIntensity = flare.intensity;
        flare.useOcclusion = false;
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (flare == null || cam == null)
            return;
        Vector3 from = cam.transform.position;
        Vector3 toSun = transform.position - from;
        float distance = toSun.magnitude;
        Vector3 dir = toSun / Mathf.Max(distance, 0.001f);

        float target = 1f;
        if (Physics.Raycast(from, dir, Mathf.Min(distance, NEAR), ~0, QueryTriggerInteraction.Ignore))
            target = 0f;
        // Set behind the planet of a run's sky.
        if (target > 0f && Shader.GetGlobalFloat(AmountId) > 0.5f)
            target = Mathf.Clamp01((dir.y + Shader.GetGlobalFloat(HorizonId)) / 0.02f + 0.5f);

        shown = Mathf.MoveTowards(shown, target, FADE_PER_SECOND * Time.unscaledDeltaTime);
        flare.intensity = baseIntensity * shown;
    }
}
