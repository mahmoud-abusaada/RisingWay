using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlphaManager : MonoBehaviour
{

    private Camera gameCamera;
    private Transform player = null;
    private Renderer myRenderer;
    private float myAlpha = 1;
    private Color myColor;
    private float distanceBtwCameraAndPlayer = 0;
    private float distanceBtwCameraAndPart = 0;
    private Material currentFloorMaterial;
    private List<Renderer> partsRenderersList = new List<Renderer>();

    public void setPlayer(Transform player)
    {
        this.player = player;
    }

    public void setGameCamera(Camera gameCamera)
    {
        this.gameCamera = gameCamera;
    }

    public void setCurrentFloorMaterial(BaseMaterial baseMaterial)
    {
        Material material;
        if (baseMaterial is PatternMaterial)
        {
            PatternMaterial patternMaterial = baseMaterial as PatternMaterial;
            switch (transform.tag)
            {
                case "LandStart":
                    material = patternMaterial.landStart;
                    break;
                case "LandRight":
                    material = patternMaterial.landRight;
                    break;
                case "LandLeft":
                    material = patternMaterial.landLeft;
                    break;
                default:
                    material = patternMaterial.mainPart;
                    break;
            }
        }
        else
        {
            material = (baseMaterial as ColorMaterial).material;
        }
        currentFloorMaterial = new Material(material);
        myColor = currentFloorMaterial.color;
    }

    // Start is called before the first frame update
    void Start()
    {
        // gameCamera = Camera.main;
        myRenderer = GetComponentInChildren<Renderer>();
        myColor = currentFloorMaterial.color;
        if (transform.Find("Mesh") != null)
            partsRenderersList.Add(transform.Find("Mesh").GetComponent<Renderer>());
        if (transform.Find("PartStartBlock") != null)
            partsRenderersList.Add(transform.Find("PartStartBlock").GetComponent<Renderer>());
        if (transform.Find("PartEndBlock") != null)
            partsRenderersList.Add(transform.Find("PartEndBlock").GetComponent<Renderer>());
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null || gameCamera == null) //  || player.position.y - player.GetComponent<Collider>().bounds.extents.y > transform.position.y
        {
            // if (myColor.a != 1)
            // {
            //     myColor.a = 1;
            //     currentFloorMaterial.color = myColor;
            // }
            return;
        }

        distanceBtwCameraAndPlayer = Vector3.Distance(gameCamera.transform.position, player.localPosition);
        distanceBtwCameraAndPart = Vector3.Distance(gameCamera.transform.position, transform.localPosition);

        if (distanceBtwCameraAndPart > distanceBtwCameraAndPlayer || distanceBtwCameraAndPart < 0 || !Utility.camFollowPlayer)
        {
            if (myColor.a != 1)
            {
                myColor.a = 1;
                currentFloorMaterial.color = myColor;
            }
            return;
        }
        myAlpha = Mathf.InverseLerp(0, distanceBtwCameraAndPlayer - 1, distanceBtwCameraAndPart);
        myColor.a = myAlpha;
        currentFloorMaterial.color = myColor;

        for (int i = 0; i < partsRenderersList.Count; i++)
            partsRenderersList[i].material = currentFloorMaterial;
    }


    // void Update()
    // {
    //     if (player == null || gameCamera == null)
    //         return;

    //     distanceBtwCameraAndPlayer = Vector3.Distance(gameCamera.transform.position, player.localPosition);
    //     distanceBtwCameraAndPart = Vector3.Distance(gameCamera.transform.position, transform.localPosition);
    //     distanceBtwCameraAndPlayer -= 0.5f;

    //     if (distanceBtwCameraAndPart > distanceBtwCameraAndPlayer)
    //     {
    //         myColor.a = 1;
    //         myRenderer.material.color = myColor;
    //     }
    //     else
    //     {
    //         myColor.a = 0.3f;
    //         myRenderer.material.color = myColor;
    //     }
    // }
}
