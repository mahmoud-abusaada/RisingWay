using UnityEngine;

public class AmbientEffectsController : MonoBehaviour
{
    [SerializeField] private Transform cameraController;
    [SerializeField] private Transform sunDirectionalLight;
    [SerializeField] private Transform solarSystem;
    // The galaxy and the stars. How much of them shows is one number, spaceSky.visibility: all of
    // it in the menus and with "stay in space" on; in a run, what the mode's sky leaves to be seen
    // (AtmosphereSky - day by the climb for Standard and Insane, Chill's clock).
    [SerializeField] private SpaceSky spaceSky;
    public float sunSpeed = 1.2f;
    private float targetSunSpeed = 10f;

    // The sky's visibility changes by at most this much a second.
    private const float SPACE_FADE_PER_SECOND = 0.6f;
    private float targetSpace = 1f;

    private AtmosphereSky atmosphere;
    // How high above the camera the solar system sits beyond its usual place: Chill raises it by
    // day and sinks it by night, so the Sun really rises and sets with Chill's clock.
    private float solarLift;

    void Awake()
    {
        GetComponent<SolarSystem>().initSolarSystem();
        atmosphere = gameObject.AddComponent<AtmosphereSky>();
    }

    void Start()
    {
        SunFlare.Install();
    }

    public void onReplay()
    {
        targetSpace = 1f;
        atmosphere.BackToMenus();
    }

    public void setTargetSunSpeed(float targetSunSpeed)
    {
        this.targetSunSpeed = targetSunSpeed;
    }

    public void onGameStarted()
    {
        atmosphere.RunStarted();
    }

    void Update()
    {
        updateSun();

        if (Utility.gameStarted && !PlayerStats.Instance.isStayInSpaceOn())
            targetSpace = atmosphere.SpaceShown();
        else
            targetSpace = 1f;

        if (spaceSky != null)
            spaceSky.visibility = Mathf.MoveTowards(spaceSky.visibility, targetSpace, SPACE_FADE_PER_SECOND * Time.unscaledDeltaTime);
    }

    void FixedUpdate()
    {
        float lift = atmosphere != null && atmosphere.ChillOn && !PlayerStats.Instance.isStayInSpaceOn()
            ? AtmosphereSky.SolarLiftFor(atmosphere.ChillSunUp) : 0f;
        solarLift = Mathf.MoveTowards(solarLift, lift, 400f * Time.fixedDeltaTime);
        solarSystem.position = new Vector3(solarSystem.position.x + (cameraController.position.x - solarSystem.position.x) * 0.002f,
                                            solarSystem.position.y + (cameraController.position.y + solarLift - solarSystem.position.y) * 0.002f,
                                            solarSystem.position.z + (cameraController.position.z - solarSystem.position.z) * 0.002f);
    }

    private void updateSun()
    {
        solarSystem.Rotate(new Vector3(0, Time.deltaTime * sunSpeed, 0));
        sunDirectionalLight.localEulerAngles = new Vector3(solarSystem.localEulerAngles.x, solarSystem.localEulerAngles.y + 180, solarSystem.localEulerAngles.z);

        if (sunSpeed != targetSunSpeed)
        {
            sunSpeed += (targetSunSpeed - sunSpeed) * Time.deltaTime;
        }
    }
}
