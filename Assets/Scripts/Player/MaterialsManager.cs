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

        // The bulk price helpers below are editor-only now and no longer touch IDs. See P2-05.
        // updateBallsPrices();
        // updateFloorsPrices();
        // updatePatternFloorsPrices();

        // Compiled out entirely in release builds - the call site disappears with the method.
        ValidateCosmeticIds();
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

    // The track shares one floor material between all its parts and edits it in place (shader
    // and render queue). Editing the project asset itself is what rewrote .mat files on disk after
    // playing in the Editor - Dark Grey.mat went from queue 3000 to 2010. Callers holding an asset
    // take a runtime copy from here instead: the same sharing, one copy per asset, and the asset
    // is never touched. In a player build the old way only changed the loaded copy; in the Editor
    // it changed the file.
    private readonly Dictionary<Material, Material> runtimeCopies = new Dictionary<Material, Material>();

    public Material getRuntimeCopy(Material asset)
    {
        if (asset == null)
            return null;
        Material copy;
        if (!runtimeCopies.TryGetValue(asset, out copy) || copy == null)
        {
            copy = new Material(asset);
            copy.name = asset.name; // other code matches on material names
            runtimeCopies[asset] = copy;
        }
        return copy;
    }

    // The helpers below modify the material they are given. Handing them a project asset is a bug;
    // in the Editor it is caught and redirected to a runtime copy instead of being written to disk.
    private Material neverAnAsset(Material m)
    {
#if UNITY_EDITOR
        if (m != null && UnityEditor.EditorUtility.IsPersistent(m))
        {
            Debug.LogError("MaterialsManager: asked to modify the material ASSET '" + m.name +
                           "'. Pass getRuntimeCopy(asset) instead. Using a runtime copy.");
            return getRuntimeCopy(m);
        }
#endif
        return m;
    }

    public Material getLitMaterial(Material m, int renderQueue = -1)
    {
        m = neverAnAsset(m);
        m.shader = litShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    public Material getStencilledMaterial(Material m, int renderQueue = -1, bool newInstance = false)
    {
        if (newInstance)
            m = new Material(m);
        else
            m = neverAnAsset(m);
        m.shader = stencilShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    public Material getFadeMaterial(Material m, int renderQueue = -1)
    {
        m = neverAnAsset(m);
        m.shader = fadeShader;
        if (renderQueue != -1)
            m.renderQueue = renderQueue;
        return m;
    }

    // ==================================================================================
    // BULK PRICE HELPERS - P2-05
    //
    // These three used to also reassign IDs by list position (id = i + 1, id = i + 101).
    // Those assignments have been REMOVED and must never come back.
    //
    // Cosmetic IDs are the primary key for what a player owns. Reassigning them by index
    // means that inserting, removing or reordering a single entry in the inspector silently
    // hands every player a different set of planets, with no error and no way to detect it
    // after the fact. See docs/cosmetic-id-contract.md for the frozen ID map.
    //
    // The price logic is kept because the P6-01 economy rebalance will want it - but note it
    // assigns prices by INDEX, so it is only correct while list order matches the frozen
    // contract. Prefer editing prices in the inspector, or rewrite these to key off id.
    //
    // Editor-only, and still uncalled. They were already dead code (the calls in Awake are
    // commented out); this just makes them harmless dead code.
    // ==================================================================================
#if UNITY_EDITOR
    private void updateBallsPrices()
    {
        for (int i = 0; i < ballMaterials.Count; i++)
        {
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
            floorMaterials[i].price = 2000;
        }
    }

    private void updatePatternFloorsPrices()
    {
        for (int i = 0; i < patternFloorMaterials.Count; i++)
        {
            patternFloorMaterials[i].price = 5000;
        }
    }
#endif

    // ==================================================================================
    // COSMETIC ID CONTRACT VALIDATION - P2-05
    //
    // Cosmetic IDs are the primary key for ownership in the save file. Once a build ships
    // they are a permanent contract: changing one silently reassigns what players own.
    //
    // This catches the failure modes that are detectable at runtime - duplicates, non-positive
    // IDs, and entries disappearing below the shipped baseline. It cannot catch an ID being
    // swapped between two items, which is why the frozen map lives in
    // docs/cosmetic-id-contract.md and should be diffed whenever these lists are edited.
    //
    // Development builds only. In release this would be pure cost for a condition that should
    // have been caught long before shipping.
    // ==================================================================================

    /// <summary>Counts as shipped in v1.0.19. Adding items above these is fine; losing them is not.</summary>
    private const int BASELINE_BALL_COUNT = 72;
    private const int BASELINE_COLOUR_FLOOR_COUNT = 12;
    private const int BASELINE_PATTERN_FLOOR_COUNT = 40;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ValidateCosmeticIds()
    {
        ValidateIdList("ballMaterials", ballMaterials.ConvertAll<BaseMaterial>(m => m), BASELINE_BALL_COUNT);

        if (floorMaterials.Count < BASELINE_COLOUR_FLOOR_COUNT)
            Debug.LogError("MaterialsManager: floorMaterials has " + floorMaterials.Count +
                           " entries, below the shipped baseline of " + BASELINE_COLOUR_FLOOR_COUNT +
                           ". Removing floors reassigns player ownership - see docs/cosmetic-id-contract.md");

        if (patternFloorMaterials.Count < BASELINE_PATTERN_FLOOR_COUNT)
            Debug.LogError("MaterialsManager: patternFloorMaterials has " + patternFloorMaterials.Count +
                           " entries, below the shipped baseline of " + BASELINE_PATTERN_FLOOR_COUNT +
                           ". Removing floors reassigns player ownership - see docs/cosmetic-id-contract.md");

        // Colour and pattern floors share one ownership list, so their IDs must not collide.
        List<BaseMaterial> allFloors = getCombinedFloorsList();
        ValidateIdList("combined floors", allFloors, BASELINE_COLOUR_FLOOR_COUNT + BASELINE_PATTERN_FLOOR_COUNT);
    }

    private void ValidateIdList(string listName, List<BaseMaterial> items, int baselineCount)
    {
        HashSet<int> seen = new HashSet<int>();

        for (int i = 0; i < items.Count; i++)
        {
            int id = items[i].id;

            if (id <= 0)
            {
                Debug.LogError("MaterialsManager: " + listName + " entry at index " + i +
                               " has a non-positive id (" + id + "). Ownership cannot be stored for it.");
                continue;
            }

            if (!seen.Add(id))
                Debug.LogError("MaterialsManager: " + listName + " has a DUPLICATE id " + id +
                               " at index " + i + ". Two cosmetics sharing an id means unlocking one " +
                               "unlocks both - see docs/cosmetic-id-contract.md");
        }

        if (items.Count < baselineCount)
            Debug.LogError("MaterialsManager: " + listName + " has " + items.Count +
                           " entries, below the shipped baseline of " + baselineCount + ".");
    }
}
