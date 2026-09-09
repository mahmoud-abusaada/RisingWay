using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialsManager : MonoBehaviour
{
    public static MaterialsManager Instance;
    [SerializeField] private Shader litShader;
    [SerializeField] private Shader fadeShader;
    [SerializeField] private Shader stencilShader;
    [SerializeField] private List<ColorMaterial> ballMaterials;
    [SerializeField] private List<ColorMaterial> floorMaterials;
    [SerializeField] private List<PatternMaterial> patternFloorMaterials;

    private ColorMaterial selectedBallMaterial = null;
    private BaseMaterial selectedFloorMaterial = null;
    private List<BaseMaterial> combinedFloorsList = null;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // updateBallsPrices();
        // updateFloorsPrices();
        // updatePatternFloorsPrices();
    }

    public ColorMaterial getSelectedBallMaterial()
    {
        if (selectedBallMaterial == null)
        {
            int id = PlayerPrefs.GetInt(Utility.Constants.KEY_CURRENT_BALL, 1);
            selectedBallMaterial = getBallMaterialById(id);
            if (selectedBallMaterial == null)
                selectedBallMaterial = getBallMaterialById(1);
        }
        return selectedBallMaterial;
    }

    public void setSelectedBallMaterial(int id)
    {
        selectedBallMaterial = getBallMaterialById(id);
        PlayerPrefs.SetInt(Utility.Constants.KEY_CURRENT_BALL, id);
        unlockBall(id);
        PlayerPrefs.Save();
    }

    public BaseMaterial getSelectedFloorMaterial()
    {
        if (selectedFloorMaterial == null)
        {
            int id = PlayerPrefs.GetInt(Utility.Constants.KEY_CURRENT_FLOOR, 1);
            selectedFloorMaterial = getFloorMaterialById(id);
            if (selectedFloorMaterial == null)
                selectedFloorMaterial = getFloorMaterialById(1);
        }
        return selectedFloorMaterial;
    }

    public void setSelectedFloorMaterial(int id)
    {
        selectedFloorMaterial = getFloorMaterialById(id);
        PlayerPrefs.SetInt(Utility.Constants.KEY_CURRENT_FLOOR, id);
        unlockFloor(id);
        PlayerPrefs.Save();
    }

    public void unlockBall(int id)
    {
        PlayerPrefs.SetInt(getBallKey(id), 1);
        PlayerPrefs.Save();
    }

    public void unlockFloor(int id)
    {
        PlayerPrefs.SetInt(getFloorKey(id), 1);
        PlayerPrefs.Save();
    }

    public bool isBallOwned(int id)
    {
        return isMaterialOwned(getBallKey(id));
    }

    public bool isFloorOwned(int id)
    {
        return isMaterialOwned(getFloorKey(id)) || isMaterialOwned(getFloorPatternKey(id));
    }

    public string getBallKey(int id)
    {
        return Utility.Constants.BALL_KEY_NAME + id;
    }

    public string getFloorKey(int id)
    {
        return Utility.Constants.FLOOR_KEY_NAME + id;
    }

    public string getFloorPatternKey(int id)
    {
        return Utility.Constants.FLOOR_PATTERN_KEY_NAME + id;
    }

    private bool isMaterialOwned(string name)
    {
        return PlayerPrefs.HasKey(name);
    }

    public ColorMaterial getBallMaterialById(int id)
    {
        return ballMaterials.Find(x => x.id == id);
    }

    public BaseMaterial getFloorMaterialById(int id)
    {
        return getCombinedFloorsList().Find(x => x.id == id);
    }

    public List<BaseMaterial> getCombinedFloorsList()
    {
        if (combinedFloorsList == null)
        {
            combinedFloorsList = new List<BaseMaterial>();
            combinedFloorsList.AddRange(floorMaterials);
            combinedFloorsList.AddRange(patternFloorMaterials);
        }
        return combinedFloorsList;
    }

    public List<ColorMaterial> getLockedBallsList()
    {
        List<ColorMaterial> lockedBalls = new List<ColorMaterial>();
        foreach (ColorMaterial material in ballMaterials)
        {
            if (!isBallOwned(material.id))
            {
                lockedBalls.Add(material);
            }
        }
        return lockedBalls;
    }

    public List<BaseMaterial> getLockedFloorsList()
    {
        List<BaseMaterial> lockedFloors = new List<BaseMaterial>();
        foreach (BaseMaterial material in getCombinedFloorsList())
        {
            if (!isFloorOwned(material.id))
            {
                lockedFloors.Add(material);
            }
        }
        return lockedFloors;
    }

    public List<ColorMaterial> getBallsMaterials()
    {
        return ballMaterials;
    }

    public Material getLitMaterial(Material m, int renderQueue = -1)
    {
        // Material m = new Material(material);
        m.shader = litShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    public Material getStencilledMaterial(Material m, int renderQueue = -1, bool newInstance = false)
    {
        if (newInstance)
            m = new Material(m);
        m.shader = stencilShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    public Material getFadeMaterial(Material m, int renderQueue = -1)
    {
        // Material m = new Material(material);
        m.shader = fadeShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    private void updateBallsPrices()
    {
        for (int i = 0; i < ballMaterials.Count; i++)
        {
            ballMaterials[i].id = i + 1;
            if (i <= 13)
                ballMaterials[i].price = 1000;
            else if (i <= 37)
                ballMaterials[i].price = 2000;
            else if (i <= 51)
                ballMaterials[i].price = 3000;
            else
                ballMaterials[i].price = -1;
        }
    }

    private void updateFloorsPrices()
    {
        for (int i = 0; i < floorMaterials.Count; i++)
        {
            floorMaterials[i].id = i + 1;
            floorMaterials[i].price = 2000;
        }
    }

    private void updatePatternFloorsPrices()
    {
        for (int i = 0; i < patternFloorMaterials.Count; i++)
        {
            patternFloorMaterials[i].id = i + 101;
            patternFloorMaterials[i].price = 5000;
        }
    }
}
