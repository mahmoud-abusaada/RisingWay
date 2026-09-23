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
    private bool shouldUpdateList = false;
    private Vector3 currentBallPosition;
    private Vector3 currentFloorPosition;
    private ColorMaterial currentAppliedBallMaterial;
    private BaseMaterial currentAppliedFloorMaterial;
    private int floorRenderModifier = 0;
    private int diamondsCount;
    private bool mysteryBoxAdLoaded = false;
    private Color selectedColor = new Color32(121, 202, 255, 215);
    private Color unselectedColor = new Color32(110, 110, 110, 65);

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

        // The ODDS button sits over the list, where the stock translucent grey disappears.
        Transform odds = mysteryBoxRect.Find("OddsButton");
        if (odds != null && odds.GetComponent<Image>() != null)
            odds.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.17f, 0.92f);
    }

    void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MysteryBoxRevealTest.Apply(playerStats);
#endif
        diamondsCount = playerStats.getDiamondsCount();
        diamondsCountText.text = Utility.getFormatedNumber(diamondsCount);
        prepareMysteryBoxButton();
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

        floorsBtnImage.color = unselectedColor;
        ballsBtnImage.color = selectedColor;

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

        floorsBtnImage.color = selectedColor;
        ballsBtnImage.color = unselectedColor;

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
            case PrizeKind.Floor: materialsManager.unlockFloor(prize.cosmetic.id); break;
            case PrizeKind.Ball: materialsManager.unlockBall(prize.cosmetic.id); break;
        }
    }

    // The visible side of the grant, once the reveal has "delivered" it.
    private void prizeCollected(MysteryBoxPrize prize)
    {
        if (prize.kind == PrizeKind.Diamonds)
            shouldUpdateDiamonds = true; // the counter rolls up to the new balance
        else if (prize.kind == PrizeKind.Floor)
            removeLock(materialsManager.getFloorKey(prize.cosmetic.id));
        else if (prize.kind == PrizeKind.Ball)
            removeLock(materialsManager.getBallKey(prize.cosmetic.id));
    }

    /// <summary>The odds, as Play policy requires them shown before a box is bought.</summary>
    public void ShowMysteryBoxOdds()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;
        confirmationDialog.setInfoDialog("Mystery Box Odds", MysteryBoxPrizes.OddsText(materialsManager), 30f);
    }

    void setContentHeight()
    {
        int rowsCount = (int)Mathf.Ceil((currentList == Lists.Floors ? mFloors.Count : mBalls.Count) / 2f);
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, rowsCount * 420);
    }

    private void setCurrentBallMaterial()
    {
        currentBall.GetComponent<MeshRenderer>().material = materialsManager.getSelectedBallMaterial().material;
        currentBall.GetComponent<Moons>().setMoons(true);
        currentBall.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
        currentBall.GetComponent<Outline>().enabled = !(currentBall.GetComponent<MeshRenderer>().material.name.Contains("Earth") || currentBall.GetComponent<MeshRenderer>().material.name.Contains("Saturn") || currentBall.GetComponent<MeshRenderer>().material.name.Contains("Bright"));
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
        materialsManager.setSelectedBallMaterial(id);
        setCurrentBallMaterial();
        animateSelectedItem(materialsManager.getBallKey(id));
    }

    private void selectFloor(int id)
    {
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
        materialsManager.setSelectedFloorMaterial(id);
        setCurrentFloorMaterial();
        animateSelectedItem(materialsManager.getFloorKey(id));
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
                newListItem.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().text = ballMaterial.price.ToString();
            }
        }
        else
        {
            newListItem.Find("LockMystery").gameObject.SetActive(false);
            newListItem.Find("Lock").gameObject.SetActive(false);
        }

        setItemFloorMaterial(newListItem, currentAppliedFloorMaterial);
        setItemBallMaterial(newListItem, ballMaterial);

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
                newListItem.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().text = floorMaterial.price.ToString();
            }
        }
        else
        {
            newListItem.Find("LockMystery").gameObject.SetActive(false);
            newListItem.Find("Lock").gameObject.SetActive(false);
        }

        setItemFloorMaterial(newListItem, floorMaterial);
        setItemBallMaterial(newListItem, currentAppliedBallMaterial);

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
        if (selectedMaterial.id != currentAppliedFloorMaterial.id)
            setItemFloorMaterial(ballItem, selectedMaterial);

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

        ColorMaterial selectedMaterial = materialsManager.getSelectedBallMaterial();
        if (selectedMaterial.id != currentAppliedBallMaterial.id)
            setItemBallMaterial(floorItem, selectedMaterial);

        floorItem.GetComponent<ListItemAnimator>().resetPosition();
        floorItem.gameObject.SetActive(true);
    }

    private void setItemBallMaterial(Transform item, ColorMaterial selectedMaterial)
    {
        int renderQueue = item.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material.renderQueue;
        Material tempBallMaterial = materialsManager.getStencilledMaterial(selectedMaterial.material, renderQueue + 1, true);
        item.GetChild(0).GetComponent<MeshRenderer>().material = tempBallMaterial;
        item.GetChild(0).GetComponent<Moons>().setRenderQueue(renderQueue + 2);
        item.GetChild(0).GetComponent<Moons>().setMoons();
        item.GetChild(0).GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
    }

    private void setItemFloorMaterial(Transform item, BaseMaterial selectedMaterial)
    {
        Material tempFloorMaterial;
        if (selectedMaterial is PatternMaterial)
        {
            tempFloorMaterial = (selectedMaterial as PatternMaterial).landItem;
        }
        else
        {
            tempFloorMaterial = (selectedMaterial as ColorMaterial).material;
        }
        tempFloorMaterial = materialsManager.getStencilledMaterial(tempFloorMaterial, floorRenderQueue + floorRenderModifier, true);
        floorRenderModifier--;
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
            material = materialsManager.getLitMaterial(material, 2009);
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

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (!isMysteryBoxSeeking)
            menusController.hideCurrentMenu(false);
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
    }

}
