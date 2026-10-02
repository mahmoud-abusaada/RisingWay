using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbientEffectsController : MonoBehaviour
{
    [SerializeField] private Transform cameraController;
    [SerializeField] private Transform cloudsContainer;
    [SerializeField] private Transform sunDirectionalLight;
    [SerializeField] private Transform solarSystem;
    [SerializeField] private GameOverMenu gameOverMenu;
    // The galaxy and the stars. How much of them shows is one number, spaceSky.visibility, which
    // this class moves towards `targetSpace`: 1 in the menus and with "stay in space" on, and
    // otherwise rising from 0 as the run climbs out of the daytime sky. A day/night cycle would
    // drive the same number (and the sky colours below) from the time of day instead of the height.
    [SerializeField] private SpaceSky spaceSky;
    [SerializeField] private Color daySkyColor;
    [SerializeField] private Color midSkyColor;
    [SerializeField] private Color nightSkyColor;
    [SerializeField] private Sprite[] clouds;
    [SerializeField] private float cloudsMinRadius = 600;
    [SerializeField] private float cloudsMaxRadius = 900;
    [SerializeField] private float cloudsStartHeight = 200;
    [SerializeField] private float cloudsEndHeight = 1000;
    [SerializeField] private List<Transform> aliveClouds = new List<Transform>();
    private IEnumerator changeColorCoroutine;
    private float currentCameraHeight = 0;
    public float sunSpeed = 1.2f;
    private float targetSunSpeed = 10f;

    // Climbing out of the daytime sky: the first stars at this height, all of space by that one.
    private const float FIRST_STARS_HEIGHT = 250f;
    private const float DEEP_SPACE_HEIGHT = 750f;
    // The sky's visibility changes by at most this much a second.
    private const float SPACE_FADE_PER_SECOND = 0.6f;
    private float targetSpace = 1f;

    void Awake()
    {
        GetComponent<SolarSystem>().initSolarSystem();
    }

    void spawnClouds()
    {
        cloudsContainer.position = Vector3.zero;
        float randomX, randomY, randomZ;
        Utility.clearAllChilds(cloudsContainer);
        aliveClouds.Clear();
        for (int i = 0; i < 120; i++)
        {
            float theta = Random.Range(0, 360);
            randomX = Mathf.Cos(theta) * Random.Range(cloudsMinRadius, cloudsMaxRadius) + transform.position.x;
            randomY = Random.Range(transform.position.y + cloudsStartHeight, transform.position.y + cloudsEndHeight);
            randomZ = Mathf.Sin(theta) * Random.Range(cloudsMinRadius, cloudsMaxRadius) + transform.position.x;
            GameObject newCloud = new GameObject();
            newCloud.AddComponent<SpriteRenderer>();
            newCloud.AddComponent<CloudLookAt>();
            newCloud.GetComponent<CloudLookAt>().cameraController = cameraController;
            newCloud.GetComponent<SpriteRenderer>().sprite = clouds[Random.Range(0, clouds.Length)];
            newCloud.GetComponent<SpriteRenderer>().material.renderQueue = 2000;
            newCloud.transform.position = new Vector3(randomX, randomY, randomZ);
            newCloud.transform.LookAt(new Vector3(transform.position.x, newCloud.transform.position.y, transform.position.z));
            newCloud.transform.parent = cloudsContainer;
            aliveClouds.Add(newCloud.transform);
        }
    }

    public void onReplay()
    {
        hideClouds();
        targetSpace = 1f;
    }

    public void setTargetSunSpeed(float targetSunSpeed)
    {
        this.targetSunSpeed = targetSunSpeed;
    }

    public void onGameStarted()
    {
        if (!PlayerStats.Instance.isStayInSpaceOn())
        {
            targetSpace = 0f;
            spawnClouds();
        }
    }

    public void hideClouds()
    {
        if (cloudsContainer.position.y < -2000)
        {
            cloudsContainer.position = Vector3.zero;
        }
        else
        {
            StartCoroutine(hideCloudsCoroutine());
        }
    }

    private IEnumerator hideCloudsCoroutine()
    {
        while (cloudsContainer.position.y < 0)
        {
            cloudsContainer.position = new Vector3(cloudsContainer.position.x, cloudsContainer.position.y + Time.deltaTime * 1000f, cloudsContainer.position.z);
            yield return new WaitForSecondsRealtime(0.001f);
        }
        cloudsContainer.position = Vector3.zero;
        yield return null;
    }

    private IEnumerator ChangeSkyColor(Color targetColor)
    {
        float tick = 0f;
        Color currentColor = Camera.main.backgroundColor;
        while (Camera.main.backgroundColor != targetColor)
        {
            tick += Time.deltaTime * 0.5f;
            Camera.main.backgroundColor = Color.Lerp(currentColor, targetColor, tick);
            yield return null;
        }
    }

    public void setDaySkyColor()
    {
        if (changeColorCoroutine != null)
            StopCoroutine(changeColorCoroutine);

        changeColorCoroutine = ChangeSkyColor(daySkyColor);
        StartCoroutine(changeColorCoroutine);
    }

    public void setMidSkyColor()
    {
        if (changeColorCoroutine != null)
            StopCoroutine(changeColorCoroutine);

        changeColorCoroutine = ChangeSkyColor(midSkyColor);
        StartCoroutine(changeColorCoroutine);
    }

    public void setNightSkyColor()
    {
        if (changeColorCoroutine != null)
            StopCoroutine(changeColorCoroutine);

        changeColorCoroutine = ChangeSkyColor(nightSkyColor);
        StartCoroutine(changeColorCoroutine);
    }

    // Update is called once per frame
    void Update()
    {
        updateSun();

        cloudsContainer.transform.position = new Vector3(cameraController.position.x, cloudsContainer.position.y, cameraController.position.z);

        if (cameraController.position.y > currentCameraHeight)
            cloudsContainer.position = new Vector3(cloudsContainer.position.x, cloudsContainer.position.y - ((cameraController.position.y - currentCameraHeight) * 5), cloudsContainer.position.z);

        currentCameraHeight = cameraController.position.y;

        cloudsContainer.Rotate(new Vector3(0f, Time.unscaledDeltaTime / 2, 0f));
        if (Utility.gameStarted)
        {
            if (!PlayerStats.Instance.isStayInSpaceOn())
            {
                targetSpace = Mathf.InverseLerp(FIRST_STARS_HEIGHT, DEEP_SPACE_HEIGHT, currentCameraHeight);

                if (currentCameraHeight < 400)
                {
                    if (Camera.main.backgroundColor != midSkyColor)
                        Camera.main.backgroundColor = Color.Lerp(daySkyColor, midSkyColor, Mathf.InverseLerp(0f, 400f, currentCameraHeight));
                }
                else if (currentCameraHeight >= 400 && currentCameraHeight < 450)
                {
                    if (Camera.main.backgroundColor != midSkyColor)
                        setMidSkyColor();
                }
                else if (currentCameraHeight >= 450 && currentCameraHeight < 800)
                {
                    if (Camera.main.backgroundColor != nightSkyColor)
                        Camera.main.backgroundColor = Color.Lerp(Camera.main.backgroundColor, nightSkyColor, Time.deltaTime * 0.01f);
                }
            }
        }

        if (spaceSky != null)
            spaceSky.visibility = Mathf.MoveTowards(spaceSky.visibility, targetSpace, SPACE_FADE_PER_SECOND * Time.unscaledDeltaTime);
    }

    void FixedUpdate()
    {
        solarSystem.position = new Vector3(solarSystem.position.x + (cameraController.position.x - solarSystem.position.x) * 0.002f,
                                            solarSystem.position.y + (cameraController.position.y - solarSystem.position.y) * 0.002f,
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
