using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShopMenu : MonoBehaviour
{
    [SerializeField] private Transform currentBall;
    [SerializeField] private Transform currentFloor;
    [SerializeField] public RectTransform contentRect;
    [SerializeField] public RectTransform scrollViewRect;
    [SerializeField] public RectTransform mysteryBoxRect;
    [SerializeField] public RectTransform choicesContainer;
    [SerializeField] public Image ballsBtnImage;
    [SerializeField] public Image floorsBtnImage;
    [SerializeField] public TextMeshProUGUI choiceCount;
    [SerializeField] public TextMeshProUGUI mystreyBoxesCount;
    [SerializeField] public ParticleSystem mystreyBoxEffect;
    [SerializeField] public ConfirmationDialog confirmationDialog;
    [SerializeField] public Camera canvasCamera;
    [SerializeField] private Transform listContainer;
    [SerializeField] private TextMeshProUGUI diamondsCountText;
    [SerializeField] private Vector3 startPosition = new Vector3(-0.5f, 5.8f, 0);

    [SerializeField] private float horizontalSpace = 3.5f;
    [SerializeField] private float verticalSpace = 2.5f;

    private MenusController menusController;
    private PlayerStats playerStats;
    private PartsPool partsPool;
    private MaterialsManager materialsManager;
    private AdmobManager adsManager;

    private List<Transform> mBalls = new List<Transform>();
    private List<Transform> mFloors = new List<Transform>();
    private Lists? currentList = null;

    enum Lists { Balls, Floors, Trails };
    private int currentBallIndex;
    private int currentFloorIndex;
    private int floorRenderQueue = 2399;
    private int ballRenderQueue = 2400;
    // Each item takes this many render queue slots, counting down the list: its floor, its ball,
    // the ball's disk or moons, an Earth's layers. So everything of the item above is drawn after
    // everything of the one below - a black hole's disk or a planet's orbits reaching up are
    // covered by the floor above them, not drawn over it.
    private const int QUEUES_PER_ITEM = 5;
    // The header's floor, then its ball (so a black hole's disk is not hidden by its own floor).
    // Both after the menu itself (its canvas is the transparent queue, 3000): the menu's shade,
    // dark at the top, was over the back of the floor - a dark band. (Away from play a black
    // hole bends nothing, BlackHoleBall, so the scene's copy not having them does not matter.)
    private const int HEADER_FLOOR_QUEUE = 3050;
    private const int HEADER_BALL_QUEUE = 3052;
    // What each item shows at the moment: the ball on a floor item, the floor under a ball item.
    private readonly Dictionary<Transform, int> itemBallId = new Dictionary<Transform, int>();
    private readonly Dictionary<Transform, int> itemFloorId = new Dictionary<Transform, int>();
    private GameObject ballsDot, floorsDot;
    private Image boxShade;
    private Material headerFloorMaterial;
    private float boxShadeTarget;
    private bool shouldUpdateList = false;
    private Vector3 currentBallPosition;
    private Vector3 currentFloorPosition;
    private ColorMaterial currentAppliedBallMaterial;
    private BaseMaterial currentAppliedFloorMaterial;
    private int floorRenderModifier = 0;
    private int diamondsCount;
    private bool mysteryBoxAdLoaded = false;

    void Awake()
    {
        menusController = FindObjectOfType<MenusController>();
        partsPool = FindObjectOfType<PartsPool>();
        materialsManager = FindObjectOfType<MaterialsManager>();
        adsManager = FindObjectOfType<AdmobManager>();
        playerStats = PlayerStats.Instance;
        currentBallPosition = startPosition;
        currentFloorPosition = startPosition;
        currentAppliedBallMaterial = materialsManager.getSelectedBallMaterial();
        currentAppliedFloorMaterial = materialsManager.getSelectedFloorMaterial();
        setCurrentBallMaterial();
        setCurrentFloorMaterial();

        buildLook();

        // The ODDS button sits over the list, where the stock translucent grey disappears.
        Transform odds = mysteryBoxRect.Find("OddsButton");
        if (odds != null && odds.GetComponent<Image>() != null)
            odds.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.17f, 0.92f);
    }

    void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || STORE_CAPTURE
        MysteryBoxRevealTest.Apply(playerStats);
#endif
        diamondsCount = playerStats.getDiamondsCount();
        diamondsCountText.text = Utility.getFormatedNumber(diamondsCount);
        // Opened again: the list starts empty and the balls come in as the first time. (The items
        // of the list left open last time - the floors - stayed up until hidden one a step.)
        foreach (Transform item in mFloors) item.gameObject.SetActive(false);
        foreach (Transform item in mBalls) item.gameObject.SetActive(false);
        prepareMysteryBoxButton();
        if (boxShade != null)
            boxShade.color = UiKit.WithAlpha(boxShade.color, boxShadeTarget); // no fade on the way in
        refreshDots();
        if (PlayerStats.Instance.getBoxesCount() == 0 && !mysteryBoxAdLoaded)
            adsManager.LoadMysteryBoxAd();
        SetBallsList();
    }

    void OnDisable()
    {
        currentList = null;
    }

    public void SetBallsList()
    {
        if (currentList == Lists.Balls)
            return;

        NebulaSkin.Tab(floorsBtnImage, false);
        NebulaSkin.Tab(ballsBtnImage, true);

        contentRect.localPosition = Vector3.zero;

        // foreach (Transform item in mFloors)
        // {
        //     item.gameObject.SetActive(false);
        // }
        // foreach (Transform item in mBalls)
        // {
        //     item.GetComponent<ListItemAnimator>().resetPosition();
        //     item.gameObject.SetActive(true);
        // }
        currentList = Lists.Balls;
        shouldUpdateList = true;
        floorRenderModifier = 0;
        currentBallIndex = 0;
        currentFloorIndex = 0;
        currentAppliedBallMaterial = materialsManager.getSelectedBallMaterial();
        setContentHeight();
    }

    public void SetFloorsList()
    {
        if (currentList == Lists.Floors)
            return;

        NebulaSkin.Tab(floorsBtnImage, true);
        NebulaSkin.Tab(ballsBtnImage, false);

        contentRect.localPosition = Vector3.zero;

        // foreach (Transform item in mBalls)
        // {
        //     item.gameObject.SetActive(false);
        // }
        // foreach (Transform item in mFloors)
        // {
        //     item.GetComponent<ListItemAnimator>().resetPosition();
        //     item.gameObject.SetActive(true);
        // }
        currentList = Lists.Floors;
        shouldUpdateList = true;
        floorRenderModifier = 0;
        currentBallIndex = 0;
        currentFloorIndex = 0;
        currentAppliedFloorMaterial = materialsManager.getSelectedFloorMaterial();
        setContentHeight();
    }

    // ---- Mystery box ---------------------------------------------------------------------------
    // What a box gives is MysteryBoxPrizes' job and how it is shown is MysteryBoxReveal's. Here
    // the prize is rolled, paid out, the box used up and the save written, all before the
    // animation starts: skipping the animation or the app dying half-way cannot lose the prize
    // or hand it out twice. (The old sequence granted on its last flash and used the box up a
    // second later.)
    public bool isMysteryBoxSeeking = false;
    private MysteryBoxReveal mysteryBoxReveal;

    private MysteryBoxReveal reveal()
    {
        if (mysteryBoxReveal == null)
        {
            mysteryBoxReveal = gameObject.AddComponent<MysteryBoxReveal>();
            mysteryBoxReveal.Init((RectTransform)choicesContainer.parent, choicesContainer, choiceCount,
                                  mysteryBoxRect, diamondsCountText.transform, materialsManager, mystreyBoxEffect,
                                  listContainer.gameObject);
            choicesContainer.parent.gameObject.AddComponent<MysteryBoxRevealTap>().reveal = mysteryBoxReveal;
            // The button sits over the overlay: a press on it skips or collects too (the press
            // that opens a box comes before the reveal plays, so it is not taken as a skip).
            mysteryBoxRect.gameObject.AddComponent<MysteryBoxRevealTap>().reveal = mysteryBoxReveal;
        }
        return mysteryBoxReveal;
    }

    public void MysteryBoxClick()
    {
        if (isMysteryBoxSeeking)
            return;

        if (playerStats.getBoxesCount() > 0)
        {
            isMysteryBoxSeeking = true;
            MysteryBoxPrize prize = MysteryBoxPrizes.Roll(materialsManager, playerStats.getBoxesSinceCosmetic());
            grantPrize(prize);
            GameAnalytics.MysteryBoxOpened(prize);
            playerStats.setBoxesSinceCosmetic(MysteryBoxPrizes.IsCosmetic(prize.kind) ? 0 : playerStats.getBoxesSinceCosmetic() + 1);
            playerStats.subtractBoxes();
            playerStats.Flush();
            if (playerStats.getBoxesCount() == 0)
                adsManager.LoadMysteryBoxAd();

            reveal().Play(prize,
                () => prizeCollected(prize),
                () =>
                {
                    isMysteryBoxSeeking = false;
                    prepareMysteryBoxButton();
                });
        }
        else if (playerStats.getBoxesCount() == 0 && mysteryBoxAdLoaded)
        {
            adsManager.ShowMysteryBoxAd();
            mysteryBoxAdLoaded = false;
        }
    }

    private void grantPrize(MysteryBoxPrize prize)
    {
        switch (prize.kind)
        {
            case PrizeKind.Diamonds: playerStats.addDiamonds(prize.amount); break;
            case PrizeKind.Bolts: playerStats.addBolts(prize.amount); break;
            case PrizeKind.DoublePoints: playerStats.addDoublePoints(prize.amount); break;
            case PrizeKind.Chances: playerStats.addChances(prize.amount); break;
            case PrizeKind.Floor:
                materialsManager.unlockFloor(prize.cosmetic.id);
                GameAnalytics.CosmeticUnlocked("floor", prize.cosmetic.id, "box", 0);
                break;
            case PrizeKind.Ball:
                materialsManager.unlockBall(prize.cosmetic.id);
                GameAnalytics.CosmeticUnlocked("ball", prize.cosmetic.id, "box", 0);
                break;
        }
    }

    // The visible side of the grant, once the reveal has "delivered" it.
    private void prizeCollected(MysteryBoxPrize prize)
    {
        if (prize.kind == PrizeKind.Diamonds)
            shouldUpdateDiamonds = true; // the counter rolls up to the new balance
        else if (prize.kind == PrizeKind.Floor)
        {
            removeLock(materialsManager.getFloorKey(prize.cosmetic.id));
            setNewTag(listContainer.Find(materialsManager.getFloorKey(prize.cosmetic.id)), true);
        }
        else if (prize.kind == PrizeKind.Ball)
        {
            removeLock(materialsManager.getBallKey(prize.cosmetic.id));
            setNewTag(listContainer.Find(materialsManager.getBallKey(prize.cosmetic.id)), true);
        }
        refreshDots();
    }

    /// <summary>The odds, as Play policy requires them shown before a box is bought.</summary>
    public void ShowMysteryBoxOdds()
    {
        if (isMysteryBoxSeeking) { revealBack(); return; } // a press during a reveal moves it on
        if (!MultiClickHandler.Instance.CanClick()) return;
        confirmationDialog.setInfoDialog("Mystery Box Odds", MysteryBoxPrizes.OddsText(materialsManager), 30f);
        GameAnalytics.OddsViewed("shop");
    }

    void setContentHeight()
    {
        int rowsCount = (int)Mathf.Ceil((currentList == Lists.Floors ? mFloors.Count : mBalls.Count) / 2f);
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, rowsCount * 420);
    }

    private void setCurrentBallMaterial()
    {
        currentBall.GetComponent<MeshRenderer>().material = materialsManager.getSelectedBallMaterial().material;
        currentBall.GetComponent<MeshRenderer>().material.renderQueue = HEADER_BALL_QUEUE;
        currentBall.GetComponent<Moons>().setMoons(true);
        currentBall.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
        currentBall.GetComponent<Outline>().enabled = !MaterialsManager.isSolarBall(currentBall.GetComponent<MeshRenderer>().material);
    }

    private void setCurrentFloorMaterial()
    {
        setPartMaterial(currentFloor.Find("PathLandItem"), materialsManager.getSelectedFloorMaterial());
    }

    private void animateSelectedItem(string name)
    {
        if (listContainer.Find(name) != null)
            listContainer.Find(name).GetComponent<Animation>().Play();
    }

    private void animateItemLock(string name, BaseMaterial baseMaterial)
    {
        if (listContainer.Find(name) != null)
        {
            listContainer.Find(name).Find("Lock").GetComponent<Animation>().Play();
            listContainer.Find(name).Find("LockMystery").GetComponent<Animation>().Play();
        }
        if (baseMaterial.price == -1)
        {
            if (PlayerStats.Instance.getBoxesCount() > 0)
            {
                confirmationDialog.setConfirmationDialog("Item Locked", "You have to use mystery box to get this item, Do you want to use one?", true, () =>
                {
                    MysteryBoxClick();
                });
            }
            else
            {
                confirmationDialog.setConfirmationDialog("Item Locked", "You have to use mystery box to get this item, Do you want to purchase more mystery boxes?", true, () =>
                {
                    menusController.hideCurrentMenu(false);
                    menusController.showAndAddMenuToStack(Menus.PurchaseMenu);
                });
            }
        }
        else
        {
            confirmationDialog.setConfirmationDialog("Item Locked", "You need more diamonds to buy this item, Do you want to puchase more diamonds?", true, () =>
            {
                menusController.hideCurrentMenu(false);
                menusController.showAndAddMenuToStack(Menus.PurchaseMenu);
            });
        }

    }

    private void removeLock(string name)
    {
        if (listContainer.Find(name) != null)
        {
            listContainer.Find(name).Find("Lock").gameObject.SetActive(false);
            listContainer.Find(name).Find("LockMystery").gameObject.SetActive(false);
        }
    }

    public void selectBall(int id)
    {
        SoundManager.Instance?.PlayMenu(); // a tap on an item (it is a 3D object, not a button)
        ColorMaterial selectedMaterial = materialsManager.getBallMaterialById(id);
        if (materialsManager.isBallOwned(id))
        {
            applySelectedBall(id);
        }
        else if (selectedMaterial.price > 0)
        {
            if (playerStats.getDiamondsCount() >= selectedMaterial.price)
            {
                confirmationDialog.setConfirmationDialog("Confirmation", "Are you sure you want to buy this ball?", true, () =>
                {
                    applySelectedBall(id);
                    removeLock(materialsManager.getBallKey(id));
                    subtractDiamonds(selectedMaterial.price);
                    GameAnalytics.CosmeticUnlocked("ball", id, "diamonds", selectedMaterial.price);
                });
            }
            else
            {
                animateItemLock(materialsManager.getBallKey(id), selectedMaterial);
            }
        }
        else
        {
            animateItemLock(materialsManager.getBallKey(id), selectedMaterial);
        }
    }

    private void applySelectedBall(int id)
    {
        GameAnalytics.CosmeticSelected("ball", id);
        materialsManager.setSelectedBallMaterial(id);
        setCurrentBallMaterial();
        animateSelectedItem(materialsManager.getBallKey(id));
        setNewTag(listContainer.Find(materialsManager.getBallKey(id)), false);
        refreshDots();
    }

    private void selectFloor(int id)
    {
        SoundManager.Instance?.PlayMenu();
        BaseMaterial selectedMaterial = materialsManager.getFloorMaterialById(id);

        if (materialsManager.isFloorOwned(id))
        {
            applySelectedFloor(id);
        }
        else if (selectedMaterial.price > 0)
        {
            if (playerStats.getDiamondsCount() >= selectedMaterial.price)
            {
                confirmationDialog.setConfirmationDialog("Confirmation", "Are you sure you want to buy this floor?", true, () =>
                {
                    applySelectedFloor(id);
                    removeLock(materialsManager.getFloorKey(id));
                    subtractDiamonds(selectedMaterial.price);
                    GameAnalytics.CosmeticUnlocked("floor", id, "diamonds", selectedMaterial.price);
                });
            }
            else
            {
                animateItemLock(materialsManager.getFloorKey(id), selectedMaterial);
            }
        }
        else
        {
            animateItemLock(materialsManager.getBallKey(id), selectedMaterial);
        }
    }

    private void applySelectedFloor(int id)
    {
        GameAnalytics.CosmeticSelected("floor", id);
        materialsManager.setSelectedFloorMaterial(id);
        setCurrentFloorMaterial();
        animateSelectedItem(materialsManager.getFloorKey(id));
        setNewTag(listContainer.Find(materialsManager.getFloorKey(id)), false);
        refreshDots();
    }

    private float timeElapsedDiamonds = 0;
    private bool shouldUpdateDiamonds = false;
    void FixedUpdate()
    {
        if (shouldUpdateList)
        {
            if (currentList == Lists.Floors)
            {
                // Prepare Floors
                if (currentFloorIndex < mFloors.Count || !isInitedAllFloors())
                {
                    prepareFloorItem(currentFloorIndex);
                    currentFloorIndex++;
                }

                // Hide Balls
                if (currentBallIndex < mBalls.Count)
                {
                    mBalls[currentBallIndex].gameObject.SetActive(false);
                    currentBallIndex++;
                }
            }
            else if (currentList == Lists.Balls)
            {
                // Prepare Balls
                if (currentBallIndex < mBalls.Count || !isInitedAllBalls())
                {
                    prepareBallItem(currentBallIndex);
                    currentBallIndex++;
                }

                // Hide Balls
                if (currentFloorIndex < mFloors.Count)
                {
                    mFloors[currentFloorIndex].gameObject.SetActive(false);
                    currentFloorIndex++;
                }
            }

            if (currentFloorIndex >= mFloors.Count && isInitedAllFloors() && currentBallIndex >= mBalls.Count && isInitedAllBalls())
                shouldUpdateList = false;
        }

        if (shouldUpdateDiamonds)
        {
            int value = (int)Mathf.Lerp(diamondsCount, PlayerStats.Instance.getDiamondsCount(), timeElapsedDiamonds / 1);
            diamondsCountText.text = Utility.getFormatedNumber(value);
            timeElapsedDiamonds += Time.deltaTime;
            if (value == PlayerStats.Instance.getDiamondsCount())
            {
                shouldUpdateDiamonds = false;
                timeElapsedDiamonds = 0;
                diamondsCount = PlayerStats.Instance.getDiamondsCount();
            }
        }
    }

    private bool isInitedAllBalls()
    {
        return mBalls.Count == materialsManager.getBallsMaterials().Count;
    }

    private bool isInitedAllFloors()
    {
        return mFloors.Count == materialsManager.getCombinedFloorsList().Count;
    }

    // After every item's floor, ball and disk (2399 down), before the near stars (2405+).
    private const int LOCK_QUEUE = 2402;

    private static void lockOnTop(Transform item)
    {
        foreach (string name in new[] { "Lock", "LockMystery" })
        {
            Transform l = item.Find(name);
            if (l == null)
                continue;
            foreach (Renderer r in l.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer))
                    r.material.renderQueue = LOCK_QUEUE;
        }
    }

    private Transform initBallItem(ColorMaterial ballMaterial)
    {
        Transform newListItem = partsPool.getPart(Parts.ListItem);
        newListItem.gameObject.SetActive(false);
        newListItem.name = materialsManager.getBallKey(ballMaterial.id);
        newListItem.parent = listContainer;
        newListItem.localScale = Vector3.one;
        newListItem.localPosition = currentBallPosition;

        if (!materialsManager.isBallOwned(ballMaterial.id))
        {
            if (ballMaterial.price == -1)
            {
                newListItem.Find("LockMystery").gameObject.SetActive(true);
            }
            else
            {
                newListItem.Find("Lock").gameObject.SetActive(true);
                stylePrice(newListItem, ballMaterial.price);
            }
        }
        else
        {
            newListItem.Find("LockMystery").gameObject.SetActive(false);
            newListItem.Find("Lock").gameObject.SetActive(false);
        }

        setItemFloorMaterial(newListItem, currentAppliedFloorMaterial, mBalls.Count);
        setItemBallMaterial(newListItem, ballMaterial);
        setNewTag(newListItem, playerStats.isBallNew(ballMaterial.id));
        lockOnTop(newListItem);

        ObjectOnClick itemOnClick = newListItem.gameObject.AddComponent<ObjectOnClick>();
        itemOnClick.scrollViewRect = scrollViewRect;
        itemOnClick.mysteryBoxRect = mysteryBoxRect;
        itemOnClick.canvasCamera = canvasCamera;
        itemOnClick.id = ballMaterial.id;
        itemOnClick.confirmationDialog = confirmationDialog;
        itemOnClick.shopMenu = this;
        itemOnClick.unityEvent.AddListener(selectBall);

        if (mBalls.Count % 2 == 0)
        {
            currentBallPosition.x += horizontalSpace;
        }
        else
        {
            currentBallPosition.x -= horizontalSpace;
            currentBallPosition.y -= verticalSpace;
        }

        setContentHeight();

        mBalls.Add(newListItem);

        return newListItem;
    }

    private Transform initFloorItem(BaseMaterial floorMaterial)
    {
        Transform newListItem = partsPool.getPart(Parts.ListItem);
        newListItem.gameObject.SetActive(false);
        newListItem.name = materialsManager.getFloorKey(floorMaterial.id);
        newListItem.parent = listContainer;
        newListItem.localScale = Vector3.one;
        newListItem.localPosition = currentFloorPosition;

        if (!materialsManager.isFloorOwned(floorMaterial.id))
        {
            if (floorMaterial.price == -1)
            {
                newListItem.Find("LockMystery").gameObject.SetActive(true);
            }
            else
            {
                newListItem.Find("Lock").gameObject.SetActive(true);
                stylePrice(newListItem, floorMaterial.price);
            }
        }
        else
        {
            newListItem.Find("LockMystery").gameObject.SetActive(false);
            newListItem.Find("Lock").gameObject.SetActive(false);
        }

        setItemFloorMaterial(newListItem, floorMaterial, mFloors.Count);
        setItemBallMaterial(newListItem, materialsManager.getSelectedBallMaterial());
        setNewTag(newListItem, playerStats.isFloorNew(floorMaterial.id));
        lockOnTop(newListItem);

        ObjectOnClick itemOnClick = newListItem.gameObject.AddComponent<ObjectOnClick>();
        itemOnClick.scrollViewRect = scrollViewRect;
        itemOnClick.mysteryBoxRect = mysteryBoxRect;
        itemOnClick.canvasCamera = canvasCamera;
        itemOnClick.id = floorMaterial.id;
        itemOnClick.confirmationDialog = confirmationDialog;
        itemOnClick.shopMenu = this;
        itemOnClick.unityEvent.AddListener(selectFloor);

        if (mFloors.Count % 2 == 0)
        {
            currentFloorPosition.x += horizontalSpace;
        }
        else
        {
            currentFloorPosition.x -= horizontalSpace;
            currentFloorPosition.y -= verticalSpace;
        }

        setContentHeight();

        mFloors.Add(newListItem);

        return newListItem;
    }

    private void prepareBallItem(int index)
    {
        Transform ballItem;
        if (index >= mBalls.Count)
            ballItem = initBallItem(materialsManager.getBallsMaterials()[index]);
        else
            ballItem = mBalls[index];

        BaseMaterial selectedMaterial = materialsManager.getSelectedFloorMaterial();
        int shown;
        if (!itemFloorId.TryGetValue(ballItem, out shown) || shown != selectedMaterial.id)
            setItemFloorMaterial(ballItem, selectedMaterial, index);

        setNewTag(ballItem, playerStats.isBallNew(ballItem.GetComponent<ObjectOnClick>().id)); // won since it was made
        ballItem.GetComponent<ListItemAnimator>().resetPosition();
        ballItem.gameObject.SetActive(true);
    }

    private void prepareFloorItem(int index)
    {
        Transform floorItem;
        if (index >= mFloors.Count)
            floorItem = initFloorItem(materialsManager.getCombinedFloorsList()[index]);
        else
            floorItem = mFloors[index];

        // The ball each floor item shows is the one selected now (it used to be compared with the
        // ball selected when the balls tab was last opened, so after trying a black hole and then
        // another ball, every floor still had the black hole on it).
        ColorMaterial selectedMaterial = materialsManager.getSelectedBallMaterial();
        int shown;
        if (!itemBallId.TryGetValue(floorItem, out shown) || shown != selectedMaterial.id)
            setItemBallMaterial(floorItem, selectedMaterial);

        setNewTag(floorItem, playerStats.isFloorNew(floorItem.GetComponent<ObjectOnClick>().id));
        floorItem.GetComponent<ListItemAnimator>().resetPosition();
        floorItem.gameObject.SetActive(true);
    }

    private void setItemBallMaterial(Transform item, ColorMaterial selectedMaterial)
    {
        itemBallId[item] = selectedMaterial.id;
        int renderQueue = item.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material.renderQueue;
        Material tempBallMaterial = materialsManager.getStencilledMaterial(selectedMaterial.material, renderQueue + 1, true);
        item.GetChild(0).GetComponent<MeshRenderer>().material = tempBallMaterial;
        item.GetChild(0).GetComponent<Moons>().setRenderQueue(renderQueue + 2);
        item.GetChild(0).GetComponent<Moons>().setMoons();
        item.GetChild(0).GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
    }

    private void setItemFloorMaterial(Transform item, BaseMaterial selectedMaterial, int index)
    {
        itemFloorId[item] = selectedMaterial.id;
        Material tempFloorMaterial;
        if (selectedMaterial is PatternMaterial)
        {
            tempFloorMaterial = (selectedMaterial as PatternMaterial).landItem;
        }
        else
        {
            tempFloorMaterial = (selectedMaterial as ColorMaterial).material;
        }
        tempFloorMaterial = materialsManager.getStencilledMaterial(tempFloorMaterial, floorRenderQueue - QUEUES_PER_ITEM * index, true);
        // The floor hides what is behind it - the stars and the sky's lines were drawn over it.
        // Opaque, so writing depth does not make its faces shade each other (a dark band).
        tempFloorMaterial.SetFloat("_Surface", 0f);
        tempFloorMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        tempFloorMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        tempFloorMaterial.SetFloat("_ZWrite", 1f);
        tempFloorMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        tempFloorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        tempFloorMaterial.SetOverrideTag("RenderType", "Opaque");
        item.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material = tempFloorMaterial;
    }

    private void setPartMaterial(Transform part, BaseMaterial baseMaterial)
    {
        Material material;
        if (baseMaterial is PatternMaterial)
        {
            PatternMaterial patternMaterial = baseMaterial as PatternMaterial;
            switch (part.tag)
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
                case "Land":
                case "ListItem":
                    material = patternMaterial.landItem;
                    // material.SetFloat("_FadeEndDistance", 0);
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

        material = materialsManager.getRuntimeCopy(material); // never edit the asset itself

        if (part.tag == "Land")
        {
            material = materialsManager.getLitMaterial(material, floorRenderQueue + floorRenderModifier);
        }
        else if (part.tag == "ListItem")
        {
            // Its own material. The runtime copy is shared with the track's floor, and the track
            // keeps setting it up for itself as it lays parts (its shader, its queue, its fade):
            // the header's floor changed with it - a dark band across it, whatever the ball.
            if (headerFloorMaterial != null)
                Destroy(headerFloorMaterial);
            headerFloorMaterial = new Material(material) { name = material.name };
            material = materialsManager.getLitMaterial(headerFloorMaterial, HEADER_FLOOR_QUEUE);
            // Opaque: the stars stay behind it. (Transparent with depth written, its own faces
            // shaded each other in draw order - a dark band across its top.)
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = HEADER_FLOOR_QUEUE;
        }
        else
        {
            material = materialsManager.getFadeMaterial(material, 2000);
        }

        if (part.Find("Mesh") != null)
        {
            // if (part.tag == "Land")
            // {
            //     material = new Material(material);
            //     material.shader = litShader;
            // }
            part.Find("Mesh").GetComponent<Renderer>().material = material;
            // if (part.tag == "Land")
            // {
            //     part.Find("Mesh").GetComponent<Renderer>().sharedMaterial.SetFloat("_FadeEndDistance", 0);
            // }
        }
        if (part.Find("PartStartBlock") != null)
            part.Find("PartStartBlock").GetComponent<Renderer>().material = material;
        if (part.Find("PartEndBlock") != null)
            part.Find("PartEndBlock").GetComponent<Renderer>().material = material;
    }

    private IEnumerator subtractDiamondsAnimation(int diamondsCount, int amount)
    {
        for (int i = 1; i <= amount; i++)
        {
            diamondsCountText.text = Utility.getFormatedNumber(diamondsCount - i);
            yield return new WaitForSeconds(0.6f / amount);
        }
    }

    private void subtractDiamonds(int amount)
    {
        PlayerStats.Instance.subtractDiamonds(amount);
        shouldUpdateDiamonds = true;
    }

    private IEnumerator addDiamondsAnimation(int diamondsCount, int amount)
    {
        for (int i = 1; i <= amount; i++)
        {
            diamondsCountText.text = Utility.getFormatedNumber(diamondsCount + i);
            yield return new WaitForSeconds(0.6f / amount);
        }
    }

    private void addDiamonds(int amount)
    {
        PlayerStats.Instance.addDiamonds(amount);
        shouldUpdateDiamonds = true;
    }

    // ---- Look -----------------------------------------------------------------------------------

    // Made once, over the scene's objects (the Nebula theme itself is NebulaSkin's).
    private void buildLook()
    {
        // The selected ball and floor, between the tabs: a little lower, clear of the title (the
        // ball covered the foot of "Shop"), and a little bigger.
        RectTransform ground = currentBall.parent != null ? currentBall.parent.parent as RectTransform : null;
        if (ground != null && ground.name == "CurrentBallGround")
        {
            ground.anchoredPosition += new Vector2(0f, -18f);
            ground.localScale = Vector3.one * 1.08f;
            // (No dark band across it from the menu's shade: it draws after the menu,
            // HEADER_FLOOR_QUEUE.) The list's box starts under it: its top edge ran across the floor, a dark band.
            const float BOX_DOWN = 12f;
            scrollViewRect.sizeDelta -= new Vector2(0f, BOX_DOWN);
            scrollViewRect.anchoredPosition -= new Vector2(0f, BOX_DOWN * (1f - scrollViewRect.pivot.y));
        }

        // A dot on a tab while it has something the player has never put on.
        ballsDot = UiKit.Dot(ballsBtnImage.transform, "SomethingNew", new Vector2(-8, -8), 28);
        floorsDot = UiKit.Dot(floorsBtnImage.transform, "SomethingNew", new Vector2(-8, -8), 28);

        // The menu's shade in two: the dark foot behind the mystery box button fades out with the
        // button (when the boxes run out), instead of darkening the list for nothing.
        Transform shade = transform.Find("Shade");
        if (shade != null && boxShade == null)
        {
            Image top = shade.GetComponent<Image>();
            UiKit.Style(top, "shade_top", top.color);
            boxShade = UiKit.Image(transform, "BoxShade", "shade_bottom", top.color);
            UiKit.Stretch(boxShade.rectTransform);
            boxShade.transform.SetSiblingIndex(shade.GetSiblingIndex() + 1);
        }
    }

    void Update()
    {
        if (boxShade != null && !Mathf.Approximately(boxShade.color.a, boxShadeTarget))
            boxShade.color = UiKit.WithAlpha(boxShade.color, Mathf.MoveTowards(boxShade.color.a, boxShadeTarget, Time.unscaledDeltaTime / 0.45f));
    }

    private void refreshDots()
    {
        if (ballsDot != null) ballsDot.SetActive(playerStats.hasNewBall());
        if (floorsDot != null) floorsDot.SetActive(playerStats.hasNewFloor());
    }

    // The price of a locked item: "1,000" beside the gem, on a dark glass pill, so it reads as a
    // price tag rather than big digits painted on the floor.
    private void stylePrice(Transform item, int price)
    {
        Transform lockT = item.Find("Lock");
        TextMeshProUGUI text = lockT.Find("Price").GetComponent<TextMeshProUGUI>();
        text.text = Utility.getFormatedNumber(price);
        if (lockT.Find("PriceBack") != null)
            return;
        Transform gem = lockT.Find("Diamond");
        text.enableVertexGradient = true;
        text.colorGradientPreset = null;
        text.colorGradient = new VertexGradient(Color.white, Color.white, UiKit.Hex("E9C8FF"), UiKit.Hex("E9C8FF"));
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;

        Image back = UiKit.Image(lockT, "PriceBack", "round_sheen", Color.white);
        UiKit.Gradient(back, UiKit.WithAlpha(UiKit.Hex("2A2F6E"), 0.62f), UiKit.WithAlpha(UiKit.Hex("120F35"), 0.62f), true);
        back.pixelsPerUnitMultiplier = 116f; // the sprite's 36 px corner to half the pill's height, in these small units
        RectTransform br = back.rectTransform;
        br.pivot = new Vector2(0.5f, 0.5f);
        br.localRotation = Quaternion.identity;
        br.localScale = Vector3.one;
        br.localPosition = new Vector3(0f, -0.27f, -1.47f); // just behind the gem and the number
        br.sizeDelta = Vector2.zero; // until fitted
        back.transform.SetAsFirstSibling();
        // Fitted to the number as drawn, once the item is on screen (PriceTagFit).
        PriceTagFit fit = back.gameObject.AddComponent<PriceTagFit>();
        fit.text = text;
        fit.gem = gem;
        Image edge = UiKit.Image(back.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.5f));
        edge.pixelsPerUnitMultiplier = back.pixelsPerUnitMultiplier;
        UiKit.Gradient(edge, UiKit.Hex("8FE9FF"), UiKit.Hex("B79BFF"), false);
        UiKit.Stretch(edge.rectTransform);
    }

    // NEW on an owned item never put on: a gold tag on the front of its floor.
    private void setNewTag(Transform item, bool on)
    {
        if (item == null)
            return;
        Transform tag = item.Find("NewTag");
        if (tag == null)
        {
            if (!on)
                return;
            tag = makeNewTag(item);
        }
        tag.gameObject.SetActive(on);
    }

    private Transform makeNewTag(Transform item)
    {
        Transform lockT = item.Find("Lock");
        TextMeshProUGUI like = lockT.Find("Price").GetComponent<TextMeshProUGUI>();
        RectTransform r = UiKit.Rect("NewTag", item);
        // On the floor's front, where a price would be (the lock's place, which is off).
        r.localRotation = lockT.localRotation;
        r.localPosition = lockT.localPosition + lockT.localRotation * new Vector3(0f, -0.27f, -1.56f);
        r.localScale = Vector3.one;
        r.sizeDelta = new Vector2(0.95f, 0.42f);
        Image pill = r.gameObject.AddComponent<Image>();
        UiKit.Style(pill, "round_sheen", Color.white);
        pill.pixelsPerUnitMultiplier = 171f; // corners half the height: a pill
        UiKit.Gradient(pill, UiKit.Gold, UiKit.Amber, true);
        TextMeshProUGUI label = UiKit.Label(like, r, "Text", "NEW", 1.5f, UiKit.Hex("2A1600"), TextAlignmentOptions.Center);
        label.fontMaterial = like.font.material; // plain letters, without the price's blue outline
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.rectTransform.offsetMin = new Vector2(0.1f, 0.06f);
        label.rectTransform.offsetMax = new Vector2(-0.1f, -0.06f);
        label.rectTransform.localPosition = new Vector3(0, 0, -0.01f);
        label.enableAutoSizing = true; // fits the tag (the font's sizes are far off in these small units)
        label.fontSizeMin = 0.05f;
        label.fontSizeMax = 2f;
        label.characterSpacing = 4;
        return r;
    }

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (!isMysteryBoxSeeking)
            menusController.hideCurrentMenu(false);
    }

    /// <summary>Android back during a box reveal: the same as a tap - skip the spin, then collect.</summary>
    public void revealBack()
    {
        if (mysteryBoxReveal != null)
            mysteryBoxReveal.OnTap();
    }

    public void MysteryBoxAdLoaded()
    {
        mysteryBoxAdLoaded = true;
        prepareMysteryBoxButton();
    }

    private void prepareMysteryBoxButton()
    {
        if (playerStats.getBoxesCount() == 0)
        {
            if (mysteryBoxAdLoaded)
            {
                mystreyBoxesCount.text = "Watch Ad";
                mysteryBoxRect.gameObject.SetActive(true);
            }
            else
                mysteryBoxRect.gameObject.SetActive(false);
        }
        else
        {
            mystreyBoxesCount.text = Utility.getFormatedNumber(playerStats.getBoxesCount());
            mysteryBoxRect.gameObject.SetActive(true);
        }
        // The dark foot behind the button is there only while the button is (Update fades it).
        boxShadeTarget = mysteryBoxRect.gameObject.activeSelf ? 1f : 0f;
    }

}

/// <summary>
/// A shop price's pill fitted to what is drawn: from the gem to the end of the number, centred on
/// the number's height. Measured the first time the item is on screen - the number sizes itself to
/// its box, and measured before (the item still switched off) it came out wrong on a phone: the
/// pill too low and running past the number.
/// </summary>
public class PriceTagFit : MonoBehaviour
{
    public TMP_Text text;
    public Transform gem;
    private bool fitted;

    void LateUpdate()
    {
        if (fitted || text == null || !text.isActiveAndEnabled)
            return;
        text.ForceMeshUpdate();
        if (text.textInfo.characterCount == 0)
            return;
        Transform lockT = transform.parent;
        Bounds b = text.textBounds; // in the text's own space
        Vector3 min = lockT.InverseTransformPoint(text.transform.TransformPoint(b.min));
        Vector3 max = lockT.InverseTransformPoint(text.transform.TransformPoint(b.max));
        float right = Mathf.Max(min.x, max.x) + 0.16f;
        float left = (gem != null ? gem.localPosition.x : Mathf.Min(min.x, max.x)) - 0.34f;
        float y = (min.y + max.y) / 2f;
        float height = Mathf.Abs(max.y - min.y) + 0.2f;
        RectTransform r = (RectTransform)transform;
        r.sizeDelta = new Vector2(right - left, height);
        // Corners half the height: a pill (the sprite's corner is 36 px).
        foreach (Image img in GetComponentsInChildren<Image>(true))
            img.pixelsPerUnitMultiplier = 72f / height;
        r.localPosition = new Vector3((left + right) / 2f, y, r.localPosition.z);
        fitted = true;
    }
}
