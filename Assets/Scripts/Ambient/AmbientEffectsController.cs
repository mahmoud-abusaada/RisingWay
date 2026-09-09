using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbientEffectsController : MonoBehaviour
{
    private const float DEFAULT_WHITE_STARS_RADIUS = 180;
    private const float DEFAULT_COLORS_STARS_RADIUS = 260;
    private const float HIDDEN_STARS_RADIUS = 800;
    [SerializeField] private Transform cameraController;
    [SerializeField] private Transform starsContainer;
    [SerializeField] private Transform starsHeightContainer;
    [SerializeField] private Transform cloudsContainer;
    [SerializeField] private Transform galaxyContainer;
    [SerializeField] private Transform galaxySky;
    [SerializeField] private Transform cubeSkybox;
    [SerializeField] private Transform sunDirectionalLight;
    [SerializeField] private Transform solarSystem;
    [SerializeField] private GameOverMenu gameOverMenu;
    [SerializeField] private ParticleSystem whiteStars;
    [SerializeField] private ParticleSystem redStars;
    [SerializeField] private ParticleSystem blueStars;
    [SerializeField] private Color daySkyColor;
    [SerializeField] private Color midSkyColor;
    [SerializeField] private Color nightSkyColor;
    [SerializeField] private Sprite[] clouds;
    [SerializeField] private Sprite[] galaxy;
    [SerializeField] private float cloudsMinRadius = 600;
    [SerializeField] private float cloudsMaxRadius = 900;
    [SerializeField] private float cloudsStartHeight = 200;
    [SerializeField] private float cloudsEndHeight = 1000;
    [SerializeField] private List<Transform> aliveClouds = new List<Transform>();
    private ParticleSystem.MainModule whiteStarsMainObj;
    private ParticleSystem.MainModule redStarsMainObj;
    private ParticleSystem.MainModule blueStarsMainObj;
    private ParticleSystem.EmissionModule whiteStarsShapeObj;
    private ParticleSystem.EmissionModule redStarsShapeObj;
    private ParticleSystem.EmissionModule blueStarsShapeObj;
    private ParticleSystemRenderer whiteStarsRenderer;
    private ParticleSystemRenderer redStarsRenderer;
    private ParticleSystemRenderer blueStarsRenderer;
    private IEnumerator changeColorCoroutine;
    private float starsPlaybackTime = 0;
    private Color skyMaterialColor;
    private float minColoredStarsEmission = 0f;
    private float maxColoredStarsEmission = 8f;
    private float minWhiteStarsEmission = 0f;
    private float maxWhiteStarsEmission = 50f;
    private float minParticlesCount = 8000;
    private float currentCameraHeight = 0;
    public float sunSpeed = 1.2f;
    private float targetSunSpeed = 10f;
    private IEnumerator hideStarsCoroutine;
    private IEnumerator starsSlowDownCoroutine;
    private bool hidingStars = false;
    private bool firstFrame = false;
    private float targetGalaxyAlpha = 1f;
    private float starsSpeed = 0.06f;

    void Awake()
    {
        whiteStarsMainObj = whiteStars.main;
        redStarsMainObj = redStars.main;
        blueStarsMainObj = blueStars.main;

        whiteStarsShapeObj = whiteStars.emission;
        redStarsShapeObj = redStars.emission;
        blueStarsShapeObj = blueStars.emission;

        whiteStarsRenderer = whiteStars.GetComponent<ParticleSystemRenderer>();
        redStarsRenderer = redStars.GetComponent<ParticleSystemRenderer>();
        blueStarsRenderer = blueStars.GetComponent<ParticleSystemRenderer>();

        // whiteStarsRenderer.maxParticleSize = 0;
        // redStarsRenderer.maxParticleSize = 0;
        // blueStarsRenderer.maxParticleSize = 0;

        galaxySky.GetComponent<MeshRenderer>().material.renderQueue = 2000;
        skyMaterialColor = galaxySky.GetComponent<MeshRenderer>().material.color;
        // skyMaterialColor.a = 0;
        galaxySky.GetComponent<MeshRenderer>().material.color = skyMaterialColor;

        whiteStarsRenderer.material.renderQueue = 2001;
        redStarsRenderer.material.renderQueue = 2001;
        blueStarsRenderer.material.renderQueue = 2001;

        GetComponent<SolarSystem>().initSolarSystem();
    }

    // [System.Obsolete]
    // IEnumerator Start()
    // {
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/Ambient.mp3"))
    //     {
    //         yield return www;
    //         GetComponent<AudioSource>().clip = www.GetAudioClip();
    //         GetComponent<AudioSource>().Play();
    //     }
    // }

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
            // Instantiate(cloud1, new Vector3(randomX, randomY, randomZ), Quaternion.identity, transform);
        }
    }

    void spawnGalaxy()
    {
        galaxyContainer.position = Vector3.zero;
        float randomX, randomY, randomZ;
        for (int i = 0; i < 80; i++)
        {
            float theta = Random.Range(0, 360);
            randomX = Mathf.Cos(theta) * Random.Range(cloudsMinRadius, cloudsMaxRadius) + transform.position.x;
            randomY = Random.Range(transform.position.y + cloudsStartHeight, transform.position.y + cloudsEndHeight);
            randomZ = Mathf.Sin(theta) * Random.Range(cloudsMinRadius, cloudsMaxRadius) + transform.position.x;
            GameObject newCloud = new GameObject();
            newCloud.AddComponent<SpriteRenderer>();
            newCloud.AddComponent<CloudLookAt>();
            newCloud.GetComponent<CloudLookAt>().cameraController = cameraController;
            newCloud.GetComponent<SpriteRenderer>().sprite = galaxy[Random.Range(0, galaxy.Length)];
            newCloud.transform.position = new Vector3(randomX, randomY, randomZ);
            newCloud.transform.LookAt(new Vector3(transform.position.x, newCloud.transform.position.y, transform.position.z));
            newCloud.transform.parent = galaxyContainer;
            aliveClouds.Add(newCloud.transform);
            // Instantiate(cloud1, new Vector3(randomX, randomY, randomZ), Quaternion.identity, transform);
        }
    }

    public void onReplay()
    {
        // daySkyColor = Color.black;
        // midSkyColor = Color.black;
        hideClouds();
        // targetSunSpeed = 10f;
        targetGalaxyAlpha = 1f;
        // Debug.Log("white start particles count = " + whiteStars.particleCount + ", hiding stars = " + hidingStars);
        if (whiteStars.particleCount < 8000)
        {
            if (hideStarsCoroutine != null)
                StopCoroutine(hideStarsCoroutine);
            if (starsSlowDownCoroutine != null)
                StopCoroutine(starsSlowDownCoroutine);

            whiteStars.Clear();
            redStars.Clear();
            blueStars.Clear();

            hidingStars = false;

            starsHeightContainer.localPosition = new Vector3(0, 160, 0);

            seekStars();
        }
    }

    public void setTargetSunSpeed(float targetSunSpeed)
    {
        this.targetSunSpeed = targetSunSpeed;
    }

    public void onGameStarted()
    {
        if (true)
        {
            if (starsSlowDownCoroutine != null)
                StopCoroutine(starsSlowDownCoroutine);
            if (!PlayerStats.Instance.isStayInSpaceOn())
            {
                targetGalaxyAlpha = 0f;
                starsPlaybackTime = 0;
                whiteStarsMainObj.simulationSpeed = 0;
                redStarsMainObj.simulationSpeed = 0;
                blueStarsMainObj.simulationSpeed = 0;
                if (hideStarsCoroutine == null)
                    hideStarsCoroutine = hideStars();
                StartCoroutine(hideStarsCoroutine);
                hidingStars = true;
                spawnClouds();
            }
            // spawnGalaxy();
            // targetSunSpeed = 2f;
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

    // private IEnumerator hideGalaxySkyCoroutine()
    // {
    //     while (skyMaterialColor.a > 0)
    //     {
    //         skyMaterialColor.a -= 0.005f;
    //         galaxySky.GetComponent<MeshRenderer>().material.color = skyMaterialColor;
    //         yield return new WaitForSecondsRealtime(0.001f);
    //     }
    //     yield return null;
    // }

    // private IEnumerator showGalaxySkyCoroutine()
    // {
    //     while (skyMaterialColor.a < 1)
    //     {
    //         skyMaterialColor.a += 0.005f;
    //         galaxySky.GetComponent<MeshRenderer>().material.color = skyMaterialColor;
    //         yield return new WaitForSecondsRealtime(0.001f);
    //     }
    //     yield return null;
    // }

    private void seekStars()
    {

        whiteStarsMainObj.simulationSpeed = 100f;
        blueStarsMainObj.simulationSpeed = 100f;
        redStarsMainObj.simulationSpeed = 100f;

        whiteStarsShapeObj.rateOverTimeMultiplier = maxWhiteStarsEmission;
        redStarsShapeObj.rateOverTimeMultiplier = maxColoredStarsEmission;
        blueStarsShapeObj.rateOverTimeMultiplier = maxColoredStarsEmission;

        starsSlowDownCoroutine = slowDownEffect();
        StartCoroutine(starsSlowDownCoroutine);
    }

    private IEnumerator slowDownEffect()
    {
        yield return new WaitForSecondsRealtime(0.65f);

        whiteStarsMainObj.simulationSpeed = starsSpeed;
        blueStarsMainObj.simulationSpeed = starsSpeed;
        redStarsMainObj.simulationSpeed = starsSpeed;

        yield return null;
    }

    private IEnumerator hideStars()
    {
        while (starsHeightContainer.localPosition.y < 1000)
        {
            starsHeightContainer.localPosition = new Vector3(starsHeightContainer.localPosition.x, starsHeightContainer.localPosition.y + Time.deltaTime * 700, starsHeightContainer.localPosition.z);
            yield return new WaitForSecondsRealtime(0.005f);
        }

        whiteStars.Clear();
        redStars.Clear();
        blueStars.Clear();

        starsHeightContainer.localPosition = new Vector3(0, 160, 0);

        hideStarsCoroutine = null;

        hidingStars = false;

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
        if (whiteStars.isPlaying)
        {
            starsPlaybackTime += whiteStarsMainObj.simulationSpeed;
            // Debug.Log("white stars playback time = " + whiteStars.particleCount);
        }

        if (!firstFrame && whiteStars.particleCount > 10) // Just to seek stars on start it doesn't seek sometimes when it's on Awake or Start methods
        {
            onReplay();
            firstFrame = true;
        }

        updateSun();

        starsContainer.position = new Vector3(starsContainer.position.x, cameraController.position.y, starsContainer.position.z);

        // solarSystem.position = new Vector3(cameraController.position.x, cameraController.position.y, cameraController.position.z);

        cloudsContainer.transform.position = new Vector3(cameraController.position.x, cloudsContainer.position.y, cameraController.position.z);

        galaxySky.position = new Vector3(cameraController.position.x, cameraController.position.y - 9000, cameraController.position.z);

        cubeSkybox.position = new Vector3(cameraController.position.x, cameraController.position.y, cameraController.position.z);

        if (cameraController.position.y > currentCameraHeight)
            cloudsContainer.position = new Vector3(cloudsContainer.position.x, cloudsContainer.position.y - ((cameraController.position.y - currentCameraHeight) * 5), cloudsContainer.position.z);

        currentCameraHeight = cameraController.position.y;

        cloudsContainer.Rotate(new Vector3(0f, Time.unscaledDeltaTime / 2, 0f));
        if (Utility.gameStarted)
        {
            if (!PlayerStats.Instance.isStayInSpaceOn())
            {
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

            if (currentCameraHeight > 600)
            {
                if (skyMaterialColor.a < 1)
                {
                    skyMaterialColor.a += Time.deltaTime * 0.01f;
                    galaxySky.GetComponent<MeshRenderer>().material.color = skyMaterialColor;
                    galaxySky.GetComponent<MeshRenderer>().material.renderQueue = 2000;
                }
            }

            if (currentCameraHeight > 50f && whiteStarsMainObj.simulationSpeed != 0.2f && whiteStars.particleCount < minParticlesCount)
            {

                whiteStarsRenderer.maxParticleSize = 1;
                redStarsRenderer.maxParticleSize = 1;
                blueStarsRenderer.maxParticleSize = 1;

                whiteStarsShapeObj.rateOverTimeMultiplier = minWhiteStarsEmission;
                redStarsShapeObj.rateOverTimeMultiplier = minColoredStarsEmission;
                blueStarsShapeObj.rateOverTimeMultiplier = minColoredStarsEmission;

                whiteStarsMainObj.simulationSpeed = 0.2f;
                redStarsMainObj.simulationSpeed = 0.2f;
                blueStarsMainObj.simulationSpeed = 0.2f;
            }
            else if (currentCameraHeight > 50f && whiteStars.particleCount < minParticlesCount)
            {
                if (whiteStarsShapeObj.rateOverTimeMultiplier < maxWhiteStarsEmission)
                    whiteStarsShapeObj.rateOverTimeMultiplier += Time.deltaTime * 1.5f;
                if (redStarsShapeObj.rateOverTimeMultiplier < maxColoredStarsEmission)
                {
                    redStarsShapeObj.rateOverTimeMultiplier += Time.deltaTime * 0.1f;
                    blueStarsShapeObj.rateOverTimeMultiplier += Time.deltaTime * 0.1f;
                }
            }
            else if (whiteStars.particleCount > minParticlesCount && whiteStarsMainObj.simulationSpeed > starsSpeed)
            {
                whiteStarsMainObj.simulationSpeed = starsSpeed;
                redStarsMainObj.simulationSpeed = starsSpeed;
                blueStarsMainObj.simulationSpeed = starsSpeed;
            }
        }
    }

    void FixedUpdate()
    {
        if (!Utility.gameStarted)
        {
            if (skyMaterialColor.a != targetGalaxyAlpha)
            {
                skyMaterialColor.a += 0.01f * (skyMaterialColor.a < targetGalaxyAlpha ? 1 : -1);
                galaxySky.GetComponent<MeshRenderer>().material.color = skyMaterialColor;
            }
        }

        galaxySky.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(galaxySky.GetComponent<MeshRenderer>().material.mainTextureOffset.x + 0.00002f, galaxySky.GetComponent<MeshRenderer>().material.mainTextureOffset.y + 0.00003f);

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
