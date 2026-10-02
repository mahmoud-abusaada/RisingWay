using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

public class Moons : MonoBehaviour
{
    public Transform moonsParent;
    public Transform moon;
    public Transform ring;
    public Material orbitsMaterial;
    public Shader stencilShader;
    [SerializeField] private Material otherMoon1;
    [SerializeField] private Material otherMoon2;
    [SerializeField] private Material otherMoon3;
    [SerializeField] private Material otherMoon4;
    [SerializeField] private Material otherMoon5;

    private Transform myMoonsParent;
    private Transform myMoonsOrbitsParent;
    private List<Transform> moonTransforms = new List<Transform>();
    // private TransformAccessArray moonAccessTransforms;
    private List<Transform> moonRotatingTransforms = new List<Transform>();
    // private TransformAccessArray moonRotatingAccessTransforms;
    private List<Moon> moons = new List<Moon>();
    // private NativeArray<float> moonSpeedsArray;
    private int moonsCount = 0;
    private float moonScale;
    private int renderQueue = 2400;
    private float listItemScale = 1.8f;
    private PartsPool partsPool;

    void Awake()
    {
        partsPool = FindObjectOfType<PartsPool>();
    }

    void OnEnable()
    {
        // setMoons();
    }

    public void setRenderQueue(int renderQueue)
    {
        this.renderQueue = renderQueue;
    }

    public void enableRealOrbits()
    {
        foreach (Transform moon in moonTransforms)
        {
            moon.GetChild(0).gameObject.SetActive(true);
        }
    }

    public void disableRealOrbits()
    {
        foreach (Transform moon in moonTransforms)
        {
            moon.GetChild(0).gameObject.SetActive(false);
        }
    }

    public void setMoons(bool isCurrentBall = false)
    {
        if (partsPool == null)
        {
            partsPool = FindObjectOfType<PartsPool>();
        }

        clearMoons();
        if (myMoonsParent == null)
        {
            myMoonsParent = new GameObject(transform.name + "Moons").transform;
            myMoonsParent.position = transform.position;
            myMoonsOrbitsParent = new GameObject(transform.name + "Orbits").transform;
            myMoonsOrbitsParent.position = transform.position;

            if (transform.CompareTag("Player"))
            {
                myMoonsParent.parent = moonsParent;
                myMoonsOrbitsParent.parent = moonsParent;

                if (PlayerStats.Instance.isRealOrbitOn())
                {
                    moonsParent.localEulerAngles = new Vector3(90, 0, 0);
                }
                else
                {
                    moonsParent.localEulerAngles = new Vector3(0, 0, 0);
                }
            }
            else
            {
                // renderModifier--;
                myMoonsParent.parent = transform.parent;
                myMoonsOrbitsParent.parent = transform.parent;
            }

            if (transform.CompareTag("MysteryBall"))
            {
                myMoonsParent.localScale = Vector3.one * 600;
                myMoonsOrbitsParent.localScale = Vector3.one * 600;
            }
            else
            {
                myMoonsParent.localScale = Vector3.one;
                myMoonsOrbitsParent.localScale = Vector3.one;
            }
        }

        if (transform.GetComponent<Renderer>().material.name.Contains("Earth"))
        {
            moons.Add(new Moon(1f, 1f, 0.1f, null, new Vector3(0, 0, -2.862f)));
        }
        else if (transform.GetComponent<Renderer>().material.name.Contains("Mars"))
        {
            moons.Add(new Moon(1.5f, 0.6f, 0.05f, otherMoon3, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1f, 1.05f, 0.05f, otherMoon5, new Vector3(0, 0, 14)));
        }
        else if (transform.GetComponent<Renderer>().material.name.Contains("Jupiter"))
        {
            moons.Add(new Moon(1.5f, 0.55f, 0.05f, otherMoon1));
            moons.Add(new Moon(0.9f, 0.65f, 0.05f, otherMoon2));
            moons.Add(new Moon(1.2f, 0.85f, 0.07f, otherMoon3));
            moons.Add(new Moon(0.8f, 1.05f, 0.075f, otherMoon4));
        }
        else if (transform.GetComponent<Renderer>().material.name.Contains("Saturn"))
        {
            moons.Add(new Moon(1.5f, 0.52f, 0.05f, otherMoon1, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1.3f, 0.56f, 0.05f, otherMoon2, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1.5f, 0.61f, 0.062f, otherMoon3, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1.3f, 0.66f, 0.055f, otherMoon4, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1.1f, 0.73f, 0.062f, otherMoon5, new Vector3(0, 0, 14)));
            moons.Add(new Moon(0.9f, 0.9f, 0.062f, otherMoon1, new Vector3(0, 0, 14)));
            moons.Add(new Moon(0.8f, 1f, 0.05f, otherMoon2, new Vector3(0, 0, 14)));
            moons.Add(new Moon(0.6f, 1.25f, 0.05f, otherMoon3, new Vector3(0, 0, 8)));
            Transform saturnRing = Instantiate(ring, Vector3.zero, Quaternion.identity, myMoonsOrbitsParent);
            saturnRing.localPosition = Vector3.zero;
            saturnRing.localEulerAngles = moons[0].rotation;
            saturnRing.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            if (transform.CompareTag("Player"))
            {
                saturnRing.GetComponent<Renderer>().material.renderQueue = SeeThrough.GROUP_QUEUE + 1;
                SeeThrough.Add(saturnRing.GetComponent<Renderer>());
            }
            else if (transform.CompareTag("Planet"))
            {
                saturnRing.GetComponent<Renderer>().material.renderQueue = 2011;
            }
            else
            {
                Material newRingMaterial = new Material(saturnRing.GetComponent<Renderer>().material);
                newRingMaterial.shader = stencilShader;
                newRingMaterial.renderQueue = transform.CompareTag("MysteryBall") ? 3000 : renderQueue;
                saturnRing.GetComponent<Renderer>().material = newRingMaterial;
            }
        }
        else if (transform.GetComponent<Renderer>().material.name.Contains("Uranus"))
        {
            moons.Add(new Moon(1.5f, 0.52f, 0.05f, otherMoon1, new Vector3(0, 0, -22.62f)));
            moons.Add(new Moon(1.3f, 0.6f, 0.05f, otherMoon1, new Vector3(0, 0, -22.62f)));
            moons.Add(new Moon(1.5f, 0.69f, 0.075f, otherMoon1, new Vector3(0, 0, -22.62f)));
            moons.Add(new Moon(1.3f, 0.77f, 0.06f, otherMoon1, new Vector3(0, 0, -22.62f)));
            moons.Add(new Moon(1.1f, 0.92f, 0.07f, otherMoon1, new Vector3(0, 0, -22.62f)));
            moons.Add(new Moon(0.9f, 1.05f, 0.075f, otherMoon1, new Vector3(0, 0, -22.62f)));
        }
        else if (transform.GetComponent<Renderer>().material.name.Contains("Neptune"))
        {
            moons.Add(new Moon(-2.3f, 0.65f, 0.05f, otherMoon1, new Vector3(0, 0, 14)));
            moons.Add(new Moon(1f, 1.05f, 0.05f, otherMoon1, new Vector3(0, 0, 22.62f)));
        }

        // Debug.Log(transform.GetComponent<Renderer>().material.name);

        for (int i = 0; i < moons.Count; i++)
        {
            Transform newMoonRotatingParent = new GameObject("Moon" + i + "RotatingParent").transform;
            newMoonRotatingParent.parent = myMoonsParent;
            newMoonRotatingParent.localPosition = Vector3.zero;
            newMoonRotatingParent.localScale = Vector3.one;

            // Transform newMoon = Instantiate(moon, Vector3.zero, Quaternion.identity, newMoonRotatingParent);
            Transform newMoon = partsPool.getPart(Parts.Moon);
            SeeThrough.Remove(newMoon.GetComponent<Renderer>()); // pooled: it may have been the player's
            newMoon.parent = newMoonRotatingParent;
            newMoon.localPosition = new Vector3(0, 0, moons[i].distance);
            float scale = moons[i].scale / 2;

            Transform newMoonOrbit = new GameObject(newMoon.name + "Orbit").transform;
            newMoonOrbit.parent = myMoonsOrbitsParent;
            newMoonOrbit.localPosition = Vector3.zero;
            newMoonOrbit.localScale = Vector3.one;

            // if (!transform.CompareTag("Player"))
            // {
            //     newMoon.gameObject.layer = LayerMask.NameToLayer("ObjectsScrollAndPlanet");
            //     newMoonOrbit.gameObject.layer = LayerMask.NameToLayer("ObjectsScrollAndPlanet");
            //     // scale *= 1.5f;
            // }
            newMoon.localScale = new Vector3(scale, scale, scale);

            if (transform.CompareTag("MysteryBall"))
            {
                DrawCircle(newMoonOrbit, moons[i].distance, 0.0008f);
                Material newOrbitsMaterial = new Material(newMoonOrbit.GetComponent<Renderer>().material);
                newOrbitsMaterial.shader = stencilShader;
                newOrbitsMaterial.renderQueue = 3000;
                newMoonOrbit.GetComponent<Renderer>().material = newOrbitsMaterial;
            }
            else if (!transform.CompareTag("Player"))
            {
                DrawCircle(newMoonOrbit, moons[i].distance, 0.0004f);

                Material newMoonMaterial = new Material(moons[i].material == null ? newMoon.GetComponent<Renderer>().material : moons[i].material);
                if (!isCurrentBall)
                    newMoonMaterial.shader = stencilShader;
                newMoon.GetComponent<Renderer>().material = newMoonMaterial;

                Material newOrbitsMaterial = new Material(newMoonOrbit.GetComponent<Renderer>().material);
                if (!isCurrentBall)
                    newOrbitsMaterial.shader = stencilShader;
                newOrbitsMaterial.renderQueue = renderQueue - 1;
                newMoonOrbit.GetComponent<Renderer>().material = newOrbitsMaterial;

                if (isCurrentBall)
                {
                    myMoonsParent.localEulerAngles = Vector3.zero;
                    myMoonsOrbitsParent.localEulerAngles = Vector3.zero;
                }
            }
            else
            {
                if (moons[i].material != null)
                    newMoon.GetComponent<Renderer>().material = moons[i].material;
                DrawCircle(newMoonOrbit, moons[i].distance, 0.007f);

                newMoon.GetComponent<Renderer>().material.renderQueue = SeeThrough.GROUP_QUEUE;
                SeeThrough.Add(newMoon.GetComponent<Renderer>());
                SeeThrough.Add(newMoonOrbit.GetComponent<Renderer>());
            }

            newMoonOrbit.localEulerAngles = moons[i].rotation;
            newMoonRotatingParent.localEulerAngles = moons[i].rotation;

            // Debug.Log("new moon position = " + newMoon.position);
            newMoonRotatingParent.Rotate(new Vector3(0, Random.Range(0, 360), 0));
            moonTransforms.Add(newMoon);
            moonRotatingTransforms.Add(newMoonRotatingParent);
            newMoon.gameObject.SetActive(true);
        }

        // moonAccessTransforms = new TransformAccessArray(moonTransforms.ToArray());
        // moonRotatingAccessTransforms = new TransformAccessArray(moonRotatingTransforms.ToArray());
        // moonSpeedsArray = new NativeArray<float>(moonTransforms.Count, Allocator.Persistent);
        // for (var i = 0; i < moonSpeedsArray.Length; ++i)
        //     moonSpeedsArray[i] = moons[i].speed;

        if (!transform.CompareTag("Player") && !transform.CompareTag("MysteryBall"))
        {
            transform.localScale = Vector3.one / 2f * listItemScale;
            // transform.localPosition = new Vector3(0, transform.GetComponent<Collider>().bounds.extents.y, 0);
            myMoonsParent.localScale = Vector3.one * listItemScale / 1.1f;
            myMoonsOrbitsParent.localScale = Vector3.one * listItemScale / 1.1f;
        }

        if (transform.CompareTag("Player"))
        {
            // Seen through the track, with its moons (SeeThrough; PlayerLinkedObjectsController
            // does the ball's own renderers).
            GetComponent<Renderer>().material.renderQueue = SeeThrough.GROUP_QUEUE;
        }
    }

    private void DrawCircle(Transform parent, float radius, float lineWidth)
    {
        if (myMoonsParent == null)
            return;

        var segments = 360;
        var line = parent.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = segments + 1;
        line.material = orbitsMaterial;

        var pointCount = segments + 1; // add extra point to make startpoint and endpoint the same to close the circle
        var points = new Vector3[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            var rad = Mathf.Deg2Rad * (i * 360f / segments);
            points[i] = new Vector3(Mathf.Sin(rad) * radius, 0, Mathf.Cos(rad) * radius);
        }

        line.SetPositions(points);
    }

    public void clearMoons()
    {
        if (myMoonsParent != null)
        {
            foreach (Transform moon in moonTransforms)
            {
                SeeThrough.Remove(moon.GetComponent<Renderer>());
                partsPool.setPart(moon);
            }
            moonTransforms.Clear();
            moonRotatingTransforms.Clear();
            moons.Clear();
            Utility.clearAllChilds(myMoonsParent);
            Utility.clearAllChilds(myMoonsOrbitsParent);
            GameObject.Destroy(myMoonsParent.gameObject);
            GameObject.Destroy(myMoonsOrbitsParent.gameObject);
            myMoonsParent = null;
            myMoonsOrbitsParent = null;
        }
    }

    // void Update()
    // {
    //     if (myMoonsParent != null)
    //         myMoonsParent.position = transform.position;

    //     if (myMoonsOrbitsParent != null)
    //         myMoonsOrbitsParent.position = transform.position;
    // }

    void Update()
    {
        if (myMoonsParent != null)
            myMoonsParent.position = transform.position;

        if (myMoonsOrbitsParent != null)
            myMoonsOrbitsParent.position = transform.position;

        // var moonJob = new MoonJob()
        // {
        //     deltaTime = Time.deltaTime
        // };
        // JobHandle moonJobHandle = moonJob.Schedule(moonAccessTransforms);
        // moonJobHandle.Complete();

        // var moonRotatingJob = new MoonRotatingJob()
        // {
        //     moonSpeeds = moonSpeedsArray,
        //     deltaTime = Time.deltaTime,
        //     speed = (PlayerStats.Instance.isRealOrbitOn() ? 120 : 40)
        // };
        // JobHandle moonRotatingJobHandle = moonRotatingJob.Schedule(moonAccessTransforms);
        // moonRotatingJobHandle.Complete();

        for (int i = 0; i < moonTransforms.Count; i++)
        {
            // moonTransforms[i].Rotate(new Vector3(0, Time.deltaTime * -60f, 0));
            // moonTransforms[i].rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, Time.deltaTime * -60f, 0), Time.deltaTime * 5f);
            moonRotatingTransforms[i].Rotate(new Vector3(0, Time.deltaTime * -moons[i].speed * (PlayerStats.Instance.isRealOrbitOn() ? 120 : 40), 0));
            // moonTransforms[i].localPosition = new Vector3(0, 0, moons[i].distance);
            // moonTransforms[i].RotateAround(transform.position, new Vector3(moons[i].rotation, 1, 0), -moons[i].speed); // Time.deltaTime * -10 - ((i + 1f) / moonsCount) * 0.1f
            // rotateAround(moonTransforms[i], transform.position, new Vector3(moons[i].rotation, 1, 0), -moons[i].speed);
        }
    }

    private void rotateAround(Transform obj, Vector3 point, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis);
        transform.position = obj.position + rot * point;
        transform.localRotation = rot;
    }

    void OnDestroy()
    {
        // TransformAccessArrays must be disposed manually.
        // moonAccessTransforms.Dispose();
        // moonRotatingAccessTransforms.Dispose();
        // moonSpeedsArray.Dispose();
    }

    struct MoonJob : IJobParallelForTransform
    {
        public float deltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, deltaTime * -60f, 0), deltaTime * 5f);
        }
    }

    struct MoonRotatingJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float> moonSpeeds;
        public float deltaTime;
        public int speed;

        public void Execute(int index, TransformAccess transform)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, deltaTime * -moonSpeeds[index] * speed, 0), deltaTime * 5f);
        }
    }
}
