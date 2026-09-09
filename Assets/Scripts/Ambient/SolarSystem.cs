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
            float scale = moons[i].scale;

            newMoon.localScale = new Vector3(scale, scale, scale);

            if (moons[i].material != null)
                newMoon.GetComponent<Renderer>().material = moons[i].material;

            newMoonRotatingParent.localEulerAngles = moons[i].rotation;

            // Debug.Log("new moon position = " + newMoon.position);
            newMoonRotatingParent.Rotate(new Vector3(0, Random.Range(0, 360), 0));
            planetTransforms.Add(newMoon);
            moonRotatingTransforms.Add(newMoonRotatingParent);
            newMoon.GetChild(0).gameObject.SetActive(true);
        }
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
