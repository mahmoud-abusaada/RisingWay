using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLinkedObjectsController : MonoBehaviour
{
    [SerializeField] private Transform defaultStuff;
    [SerializeField] private Transform outline;
    [SerializeField] private Transform earthStuff;
    [SerializeField] private Transform earthAtmosphere;
    [SerializeField] private Transform earthClouds;
    [SerializeField] private Transform earthWater;
    [SerializeField] private Transform earthNight;
    [SerializeField] private Shader stencilShader;
    [SerializeField] private TrailRenderer normalTrail;
    [SerializeField] private TrailRenderer boltTrail;
    [SerializeField] private TrailRenderer doublePointsTrail;
    [SerializeField] private TrailRenderer chanceTrail;
    [SerializeField] private ParticleSystem spawningEffect;
    [SerializeField] private ParticleSystem doublePointsAmbient;
    [SerializeField] private ParticleSystem boltAmbient;
    // The Black Hole ball's disk and lensing (BlackHoleBall, BlackHole.shader).
    [SerializeField] private Material blackHoleLensMaterial;
    private float originalNormalTrailTime;
    private float originalBoltTrailTime;
    private float originalDoublePointsTrailTime;
    private float originalChanceTrailTime;
    private PickUpType? selectedTrail = null; // Using PickUpType to identify the trail (Diamond for normal trail)

    void Awake()
    {
        originalNormalTrailTime = normalTrail.time;
        originalBoltTrailTime = boltTrail.time;
        originalDoublePointsTrailTime = doublePointsTrail.time;
        originalChanceTrailTime = chanceTrail.time;
    }

    public void reset()
    {
        // A chance's respawn may have been under way when the run was left.
        spawningEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        spawningEffect.gameObject.SetActive(false);
        normalTrail.enabled = false;
        boltTrail.enabled = false;
        doublePointsTrail.enabled = false;
        chanceTrail.enabled = false;
        disableTrails();
    }

    public void prepareLinkedObjects(bool isStenciled = true)
    {
        if (isStenciled)
        {
            // Each layer keeps its own keywords under the stencil shader. A build only keeps the
            // StencilledLit variants some material in it uses: the Earth's water (transparent, no
            // emission) had none, fell back to the opaque one on the phone, and its texture is
            // white over the land. Resources/ShaderVariants/StencilledLit Transparent keeps it.
            outline.GetComponent<Renderer>().material.shader = stencilShader;
            earthAtmosphere.GetComponent<Renderer>().material.shader = stencilShader;
            earthClouds.GetComponent<Renderer>().material.shader = stencilShader;
            earthWater.GetComponent<Renderer>().material.shader = stencilShader;
            earthNight.GetComponent<Renderer>().material.shader = stencilShader;

            int renderQueue = GetComponent<Renderer>().material.renderQueue;
            outline.GetComponent<Renderer>().material.renderQueue = renderQueue + 3;
            earthAtmosphere.GetComponent<Renderer>().material.renderQueue = renderQueue + 3;
            earthClouds.GetComponent<Renderer>().material.renderQueue = renderQueue + 3;
            earthWater.GetComponent<Renderer>().material.renderQueue = renderQueue + 2;
            earthNight.GetComponent<Renderer>().material.renderQueue = renderQueue + 2;
        }

        BlackHoleBall.Apply(gameObject, blackHoleLensMaterial);

        // The ball in play is seen through the track, with everything on it (its moons: Moons).
        if (CompareTag("Player"))
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer && r.name != BlackHoleBall.LENS_NAME)
                    SeeThrough.Add(r);

        if (GetComponent<Renderer>().material.name.Contains("Earth"))
        {
            earthStuff.gameObject.SetActive(true);
            defaultStuff.gameObject.SetActive(false);
            // No city lights: the layer glows over every continent, on the sunlit side too, and
            // on a phone with HDR and bloom that glow turned the land white.
            earthNight.gameObject.SetActive(false);
        }
        else if (MaterialsManager.isSolarBall(GetComponent<Renderer>().material)) // no outline ring (see isSolarBall)
        {
            earthStuff.gameObject.SetActive(false);
            defaultStuff.gameObject.SetActive(false);
        }
        else
        {
            earthStuff.gameObject.SetActive(false);
            defaultStuff.gameObject.SetActive(true);
        }
    }

    public void PlaySpawningEffect()
    {
        spawningEffect.gameObject.SetActive(true);
        spawningEffect.Play();
    }

    public void PlayDoublePointsAmbient()
    {
        // doublePointsAmbient.gameObject.SetActive(true);
        // doublePointsAmbient.Play();
    }

    public void PlayBoltAmbient()
    {
        // boltAmbient.gameObject.SetActive(true);
        // boltAmbient.Play();
    }

    public void StopDoublePointsAmbient()
    {
        // doublePointsAmbient.Stop();
    }

    public void StopBoltAmbient()
    {
        // boltAmbient.Stop();
    }

    void Update()
    {
        outline.LookAt(Camera.main.transform.position);
        earthAtmosphere.LookAt(Camera.main.transform.position);
        if (!Utility.gameStarted)
        {
            earthClouds.transform.Rotate(new Vector3(0, Time.deltaTime * Utility.Constants.ROTATION_SPEED * -0.5f, 0));
        }
        else
        {
            if (!Utility.stoppedForTutorials && !Utility.spawningAfterChance)
            {
                if (!Utility.boltIsOn && !Utility.doublePointIsOn && !Utility.chanceIsOn && selectedTrail != PickUpType.Diamond)
                    setNormalTrail();
                if (Utility.boltIsOn && selectedTrail != PickUpType.Bolt)
                    setBoltTrail();
                if (!Utility.boltIsOn && Utility.doublePointIsOn && selectedTrail != PickUpType.DoublePoints)
                    setDoublePointsTrail();
                if (!Utility.boltIsOn && !Utility.doublePointIsOn && Utility.chanceIsOn && selectedTrail != PickUpType.Chance)
                    setChanceTrail();
            }
            else if (selectedTrail != null)
            {
                reset();
            }
        }
    }

    public void setChanceTrail()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(SlowTrailDisable(boltTrail));
            StartCoroutine(SlowTrailDisable(doublePointsTrail));
            StartCoroutine(SlowTrailDisable(normalTrail));
            EnableTrail(chanceTrail, originalChanceTrailTime);
        }
        else
        {
            boltTrail.enabled = false;
            doublePointsTrail.enabled = false;
            normalTrail.enabled = false;
            chanceTrail.enabled = true;
        }
        selectedTrail = PickUpType.Chance;
    }

    public void setNormalTrail()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(SlowTrailDisable(boltTrail));
            StartCoroutine(SlowTrailDisable(doublePointsTrail));
            StartCoroutine(SlowTrailDisable(chanceTrail));
            EnableTrail(normalTrail, originalNormalTrailTime);
        }
        else
        {
            boltTrail.enabled = false;
            doublePointsTrail.enabled = false;
            chanceTrail.enabled = false;
            normalTrail.enabled = true;
        }
        selectedTrail = PickUpType.Diamond;
    }

    public void setBoltTrail()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(SlowTrailDisable(normalTrail));
            StartCoroutine(SlowTrailDisable(doublePointsTrail));
            StartCoroutine(SlowTrailDisable(chanceTrail));
            EnableTrail(boltTrail, originalBoltTrailTime);
        }
        else
        {
            normalTrail.enabled = false;
            doublePointsTrail.enabled = false;
            chanceTrail.enabled = false;
            boltTrail.enabled = true;
        }
        selectedTrail = PickUpType.Bolt;
    }

    public void setDoublePointsTrail()
    {
        if (gameObject.activeSelf)
        {
            StartCoroutine(SlowTrailDisable(normalTrail));
            StartCoroutine(SlowTrailDisable(boltTrail));
            StartCoroutine(SlowTrailDisable(chanceTrail));
            EnableTrail(doublePointsTrail, originalDoublePointsTrailTime);
        }
        else
        {
            normalTrail.enabled = false;
            boltTrail.enabled = false;
            chanceTrail.enabled = false;
            doublePointsTrail.enabled = true;
        }
        selectedTrail = PickUpType.DoublePoints;
    }

    IEnumerator SlowTrailDisable(TrailRenderer trail)
    {
        float rate = trail.time / 15f;
        while (trail.time > 0)
        {
            trail.time -= rate;
            yield return 0;
        }
        trail.enabled = false;
        trail.gameObject.SetActive(false);
    }

    public void EnableTrail(TrailRenderer trail, float originalTrailTime)
    {
        if (trail != null)
        {
            enableTrails();
            trail.Clear();
            trail.enabled = true;
            trail.gameObject.SetActive(true);
            trail.time = originalTrailTime;
        }
    }

    private void disableTrails()
    {
        if (transform.Find("Trail") != null)
        {
            transform.Find("Trail").gameObject.SetActive(false);
        }
        if (PlayerStats.Instance.isRealOrbitOn())
        {
            GetComponent<Moons>().disableRealOrbits();
        }
        selectedTrail = null;
    }

    private void enableTrails()
    {
        if (transform.Find("Trail") != null)
        {
            transform.Find("Trail").gameObject.SetActive(true);
        }
        if (PlayerStats.Instance.isRealOrbitOn())
        {
            GetComponent<Moons>().enableRealOrbits();
        }
    }
}
