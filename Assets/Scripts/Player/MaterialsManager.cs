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

    // P2-04: cosmetic ownership and selection now go through PlayerStats, which owns all
    // persistence. This class keeps only the in-memory material lookup and caching.
    //
    // The old code wrote one PlayerPrefs key per item and tested ownership with
    // PlayerPrefs.HasKey, so any value - including 0 - counted as owned. Ownership is now an
    // explicit list in the save file.

    public ColorMaterial getSelectedBallMaterial()
    {
        if (selectedBallMaterial == null)
        {
            int id = PlayerStats.Instance.getSelectedBallId();
            selectedBallMaterial = getBallMaterialById(id);
            if (selectedBallMaterial == null)
                selectedBallMaterial = getBallMaterialById(1);
        }
        return selectedBallMaterial;
    }

    public void setSelectedBallMaterial(int id)
    {
        selectedBallMaterial = getBallMaterialById(id);
        PlayerStats.Instance.setSelectedBallId(id);
    }

    public BaseMaterial getSelectedFloorMaterial()
    {
        if (selectedFloorMaterial == null)
        {
            int id = PlayerStats.Instance.getSelectedFloorId();
            selectedFloorMaterial = getFloorMaterialById(id);
            if (selectedFloorMaterial == null)
                selectedFloorMaterial = getFloorMaterialById(1);
        }
        return selectedFloorMaterial;
    }

    public void setSelectedFloorMaterial(int id)
    {
        selectedFloorMaterial = getFloorMaterialById(id);
        PlayerStats.Instance.setSelectedFloorId(id);
    }

    public void unlockBall(int id)
    {
        PlayerStats.Instance.unlockBall(id);
    }

    public void unlockFloor(int id)
    {
        PlayerStats.Instance.unlockFloor(id);
    }

    public bool isBallOwned(int id)
    {
        return PlayerStats.Instance.isBallOwned(id);
    }

    public bool isFloorOwned(int id)
    {
        // Colour floors use IDs from 1 and pattern floors from 101, so the two ID spaces never
        // collide and one owned-list covers both. The old dual-key lookup is no longer needed.
        return PlayerStats.Instance.isFloorOwned(id);
    }

    // These three are NOT storage keys any more - they are the GameObject names ShopMenu gives
    // its list items, and it looks them up with listContainer.Find(...). Renaming or removing
    // them silently breaks the lock/select animations in the shop.

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
