// using System;
// using System.Collections;
// using System.Collections.Generic;
// using TMPro;
// using Unity.Collections;
// using Unity.Collections.LowLevel.Unsafe;
// using Unity.Jobs;
// using UnityEngine;
// using UnityEngine.UI;

// public class ShopMenuWithJobs : MonoBehaviour, IJobParallelFor
// {

//     [SerializeField] private Transform player;
//     [SerializeField] private Transform landStart;
//     [SerializeField] private Transform landStraight;
//     [SerializeField] private Transform landLeft;
//     [SerializeField] private Transform landRight;
//     [SerializeField] private Transform curveUp;
//     [SerializeField] private Transform slide;
//     [SerializeField] private Transform curveSt;
//     [SerializeField] private Transform currentBall;
//     [SerializeField] private Transform currentFloor;

//     [SerializeField] public RectTransform contentRect;
//     [SerializeField] public RectTransform scrollViewRect;
//     [SerializeField] public RectTransform my3dListTexture;
//     [SerializeField] public RectTransform my3dScrollView;
//     [SerializeField] public RectTransform titleContainer;
//     [SerializeField] public RectTransform tabsContainer;

//     [SerializeField] public Camera canvasCamera;

//     [SerializeField] private Transform listContainer;
//     // [SerializeField] private Transform listItem;

//     [SerializeField] private TextMeshProUGUI diamondsCountText;
//     [SerializeField] private Shader litShader;
//     [SerializeField] private Shader fadeShader;
//     [SerializeField] private Shader stencilShader;
//     [SerializeField] private Vector3 startPosition = new Vector3(-0.5f, 5.8f, 0);

//     [SerializeField] private float horizontalSpace = 3.5f;
//     [SerializeField] private float verticalSpace = 2.5f;

//     [SerializeField] private List<ColorMaterial> ballMaterials;
//     [SerializeField] private List<ColorMaterial> floorMaterials;
//     [SerializeField] private List<PatternMaterial> patternFloorMaterials;
//     // [SerializeField] private List<Tra> trails;

//     private MenusController menusController;
//     private PlayerStats playerStats;
//     private PartsPool partsPool;
//     private MaterialsManager materialsManager;

//     private List<Transform> mItems = new List<Transform>();
//     private List<Test> mBalls = new List<Test>();
//     private NativeArray<Test> mBallsNativeArray;
//     private List<Test> mFloors = new List<Test>();
//     private NativeArray<Test> mFloorsNativeArray;
//     private Lists? currentList = null;

//     enum Lists { Balls, Floors, Trails };
//     private int currentBallIndex;
//     private int currentFloorIndex;
//     private float delayBetweenItems = 0.008f;
//     private int floorRenderQueue = 2399;
//     private int ballRenderQueue = 2400;
//     JobHandle initListsJobHandle;
//     InitListsJob initListsJob;

//     void Awake()
//     {
//         menusController = FindObjectOfType<MenusController>();
//         partsPool = FindObjectOfType<PartsPool>();
//         materialsManager = FindObjectOfType<MaterialsManager>();
//         playerStats = PlayerStats.Instance;
//         initBallsList();
//         initFloorsList();
//         // setBall(getMaterialIndexByName(ballMaterials, PlayerPrefs.GetString(Utility.Constants.KEY_CURRENT_BALL, "Black and White 2")));
//         // setFloor(getFloorMaterialIndexById(PlayerPrefs.GetInt(Utility.Constants.KEY_CURRENT_FLOOR, 0)));
//         player.GetComponent<Moons>().setMoons();
//         // handleMenuLayout();
//     }

//     private void set3dListSize(int width, int height)
//     {
//         my3dListTexture.sizeDelta = new Vector2(width, height);
//         my3dScrollView.sizeDelta = new Vector2(width, height);
//     }

//     public int getMaterialIndexByName(List<ColorMaterial> list, string name)
//     {
//         for (int i = 0; i < list.Count; i++)
//         {
//             if (list[i].material.name == name)
//                 return i;
//         }

//         return 0;
//     }

//     public int getFloorMaterialIndexById(int id)
//     {
//         for (int i = 0; i < floorMaterials.Count; i++)
//         {
//             if (floorMaterials[i].id == id)
//                 return i;
//         }

//         for (int i = 0; i < patternFloorMaterials.Count; i++)
//         {
//             if (patternFloorMaterials[i].id == id)
//                 return floorMaterials.Count + i;
//         }

//         return 0;
//     }

//     void resetShopMenu()
//     {
//         // Utility.clearAllChilds(listContainer);
//         List<Transform> tempItems = new List<Transform>();
//         foreach (Transform listItem in listContainer)
//         {
//             tempItems.Add(listItem);
//         }
//         partsPool.setParts(tempItems);
//         contentRect.localPosition = Vector3.zero;
//         mItems.Clear();
//     }

//     void OnEnable()
//     {
//         diamondsCountText.text = Utility.getFormatedNumber(playerStats.getDiamondsCount());
//         SetBallsList();
//     }

//     void OnDisable()
//     {
//         currentList = null;
//     }

//     private Material getLandItemMaterialByIndex(int index)
//     {
//         Debug.Log("index = " + index);
//         if (index < floorMaterials.Count)
//         {
//             return floorMaterials[index].material;
//         }
//         else
//         {
//             return patternFloorMaterials[index - floorMaterials.Count].landItem;
//         }
//     }

//     public void SetBallsList()
//     {
//         if (currentList == Lists.Balls)
//             return;

//         contentRect.localPosition = Vector3.zero;

//         foreach (Test item in mFloors)
//         {
//             item.t.gameObject.SetActive(false);
//         }
//         foreach (Test item in mBalls)
//         {
//             item.t.GetComponent<ListItemAnimator>().resetPosition();
//             item.t.gameObject.SetActive(true);
//         }
//         currentList = Lists.Balls;
//         setContentHeight();
//     }

//     private void setItemBallAndFloorMaterials(Transform item, Material ballMaterial, Material floorMaterial)
//     {
//         Material newFloorMaterial = new Material(floorMaterial);
//         newFloorMaterial.shader = stencilShader;
//         newFloorMaterial.renderQueue = floorRenderQueue + Moons.getRenderModifier();
//         item.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material = newFloorMaterial;

//         Material newBallMaterial = new Material(ballMaterial);
//         newBallMaterial.shader = stencilShader;
//         newBallMaterial.renderQueue = ballRenderQueue + Moons.getRenderModifier();
//         item.Find("Sphere").GetComponent<MeshRenderer>().material = newBallMaterial;

//         // item.Find("Lock").Find("Lock Image").GetComponent<Image>().material.renderQueue = 3001;
//         // item.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().material.renderQueue = 3001;
//         // item.Find("Lock").Find("Diamond").Find("Mesh").GetComponent<MeshRenderer>().material.renderQueue = 2500;
//     }

//     // void FixedUpdate()
//     // {
//     //     if()
//     // }

//     private void initBallsList()
//     {
//         Moons.resetRenderModifier();
//         Vector3 currentPosition = startPosition;
//         for (int i = 0; i < ballMaterials.Count; i++)
//         {
//             Transform newListItem = partsPool.getPart(Parts.ListItem);

//             newListItem.gameObject.SetActive(false);

//             newListItem.name = "Ball" + i;

//             newListItem.parent = listContainer;

//             newListItem.localScale = Vector3.one;

//             newListItem.localPosition = currentPosition;

//             // setItemBallMaterial(newListItem, ballMaterials[i].material);
//             // setItemLandMaterial(newListItem, getLandItemMaterialByIndex(currentFloorIndex));
//             setItemBallAndFloorMaterials(newListItem, ballMaterials[i].material, getLandItemMaterialByIndex(currentFloorIndex));
//             newListItem.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().text = ballMaterials[i].price.ToString();

//             newListItem.Find("Sphere").GetComponent<Moons>().setMoons();
//             newListItem.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();

//             if (PlayerPrefs.HasKey(ballMaterials[i].material.name) || i == 0)
//                 newListItem.Find("Lock").gameObject.SetActive(false);

//             ObjectOnClick itemOnClick = newListItem.gameObject.AddComponent<ObjectOnClick>();
//             itemOnClick.scrollViewRect = scrollViewRect;
//             itemOnClick.canvasCamera = canvasCamera;
//             itemOnClick.id = ballMaterials[i].id;
//             itemOnClick.unityEvent.AddListener(selectBall);

//             mBalls.Add(new Test { t = newListItem });

//             if (i % 2 == 0)
//             {
//                 currentPosition.x += horizontalSpace;
//             }
//             else
//             {
//                 currentPosition.x -= horizontalSpace;
//                 currentPosition.y -= verticalSpace;
//             }
//         }
//         var x = mBalls.ToArray();
//         // MoveFromByteArray<Test>(ref x, mBallsNativeArray);
//     }

//     public unsafe void MoveFromByteArray<T>(ref Test[] src, ref NativeArray<T> dst) where T : struct
//     {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//         AtomicSafetyHandle.CheckReadAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dst));
//         if (src == null)
//             throw new ArgumentNullException(nameof(src));
// #endif
//         var size = UnsafeUtility.SizeOf<T>();
//         if (src.Length != (size * dst.Length))
//         {
//             dst.Dispose();
//             dst = new NativeArray<T>(src.Length / size, Allocator.Persistent);
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             AtomicSafetyHandle.CheckReadAndThrow(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dst));
// #endif
//         }

//         var dstAddr = (Test*)dst.GetUnsafeReadOnlyPtr();
//         fixed (Test* srcAddr = src)
//         {
//             UnsafeUtility.MemCpy(&dstAddr[0], &srcAddr[0], src.Length);
//         }
//     }

//     public void SetFloorsList()
//     {
//         if (currentList == Lists.Floors)
//             return;

//         contentRect.localPosition = Vector3.zero;

//         foreach (Test item in mBalls)
//         {
//             item.t.gameObject.SetActive(false);
//         }
//         foreach (Test item in mFloors)
//         {
//             item.t.GetComponent<ListItemAnimator>().resetPosition();
//             item.t.gameObject.SetActive(true);
//         }
//         currentList = Lists.Floors;
//         setContentHeight();
//     }

//     private void initFloorsList()
//     {
//         Moons.resetRenderModifier();
//         Vector3 currentPosition = startPosition;
//         for (int i = 0; i < floorMaterials.Count; i++)
//         {
//             Transform newListItem = partsPool.getPart(Parts.ListItem);

//             newListItem.gameObject.SetActive(false);

//             newListItem.name = "Floor" + i;

//             newListItem.parent = listContainer;

//             newListItem.localScale = Vector3.one;

//             newListItem.localPosition = currentPosition;

//             // setItemBallMaterial(newListItem, ballMaterials[currentBallIndex].material);
//             // setItemLandMaterial(newListItem, floorMaterials[i].material);
//             setItemBallAndFloorMaterials(newListItem, ballMaterials[currentBallIndex].material, floorMaterials[i].material);
//             newListItem.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().text = floorMaterials[i].price.ToString();

//             newListItem.Find("Sphere").GetComponent<Moons>().setMoons();
//             newListItem.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();

//             if (PlayerPrefs.HasKey("Material_" + floorMaterials[i].id) || i == 0)
//                 newListItem.Find("Lock").gameObject.SetActive(false);

//             ObjectOnClick itemOnClick = newListItem.gameObject.AddComponent<ObjectOnClick>();
//             itemOnClick.scrollViewRect = scrollViewRect;
//             itemOnClick.canvasCamera = canvasCamera;
//             itemOnClick.id = i;
//             itemOnClick.unityEvent.AddListener(setFloor);

//             mFloors.Add(new Test { t = newListItem });

//             if (i % 2 == 0)
//             {
//                 currentPosition.x += horizontalSpace;
//             }
//             else
//             {
//                 currentPosition.x -= horizontalSpace;
//                 currentPosition.y -= verticalSpace;
//             }
//         }

//         for (int i = 0; i < patternFloorMaterials.Count; i++)
//         {
//             Transform newListItem = partsPool.getPart(Parts.ListItem);

//             newListItem.gameObject.SetActive(false);

//             newListItem.name = "Floor" + (i + floorMaterials.Count);

//             newListItem.parent = listContainer;

//             newListItem.localScale = Vector3.one;

//             newListItem.localPosition = currentPosition;

//             // setItemBallMaterial(newListItem, ballMaterials[currentBallIndex].material);
//             // setItemLandMaterial(newListItem, patternFloorMaterials[i].landItem);
//             setItemBallAndFloorMaterials(newListItem, ballMaterials[currentBallIndex].material, patternFloorMaterials[i].landItem);
//             newListItem.Find("Lock").Find("Price").GetComponent<TextMeshProUGUI>().text = "0";

//             newListItem.Find("Sphere").GetComponent<Moons>().setMoons();
//             newListItem.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();

//             if (PlayerPrefs.HasKey("Material_" + patternFloorMaterials[i].id) || i == 0)
//                 newListItem.Find("Lock").gameObject.SetActive(false);

//             ObjectOnClick itemOnClick = newListItem.gameObject.AddComponent<ObjectOnClick>();
//             itemOnClick.scrollViewRect = scrollViewRect;
//             itemOnClick.canvasCamera = canvasCamera;
//             itemOnClick.id = floorMaterials.Count + i;
//             itemOnClick.unityEvent.AddListener(setFloor);

//             mFloors.Add(new Test { t = newListItem });

//             if ((i + floorMaterials.Count) % 2 == 0)
//             {
//                 currentPosition.x += horizontalSpace;
//             }
//             else
//             {
//                 currentPosition.x -= horizontalSpace;
//                 currentPosition.y -= verticalSpace;
//             }
//         }
//         // mFloorsNativeArray = GetNativeVertexArrays(mFloors.ToArray());
//     }

//     void setContentHeight()
//     {
//         int rowsCount = (int)Mathf.Ceil((currentList == Lists.Floors ? mFloors.Count : mBalls.Count) / 2f);
//         contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, rowsCount * 420);
//         Debug.Log("Rows count = " + rowsCount);
//     }

//     private void setCurrentBallMaterial()
//     {
//         currentBall.GetComponent<MeshRenderer>().material = materialsManager.getSelectedBallMaterial().material;
//         currentBall.GetComponent<Moons>().setMoons(true);
//         currentBall.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
//     }

//     private void animateSelectedItem(string name)
//     {
//         if (listContainer.Find(name) != null)
//             listContainer.Find(name).GetComponent<Animation>().Play();
//     }

//     private void animateItemLock(string name)
//     {
//         if (listContainer.Find(name) != null)
//             listContainer.Find(name).Find("Lock").GetComponent<Animation>().Play();
//     }

//     private void removeLock(string name)
//     {
//         if (listContainer.Find(name) != null)
//             listContainer.Find(name).Find("Lock").gameObject.SetActive(false);
//     }

//     public void selectBall(int id)
//     {
//         ColorMaterial selectedMaterial = materialsManager.getBallMaterialById(id);
//         if (materialsManager.isBallOwned(id) || selectedMaterial.price == 0 || true)
//         {
//             applySelectedBall(id);
//         }
//         else if (selectedMaterial.price > 0)
//         {
//             if (playerStats.getDiamondsCount() >= selectedMaterial.price)
//             {
//                 applySelectedBall(1);
//                 removeLock(materialsManager.getBallKey(id));
//                 StartCoroutine(subtractDiamondsAnimation(playerStats.getDiamondsCount(), selectedMaterial.price));
//                 playerStats.subtractDiamonds(selectedMaterial.price);
//             }
//             else
//             {
//                 animateItemLock(materialsManager.getBallKey(id));
//             }
//         }
//     }

//     private void applySelectedBall(int id)
//     {
//         materialsManager.setSelectedBallMaterial(id);
//         setCurrentBallMaterial();
//         animateSelectedItem(materialsManager.getBallKey(id));
//         Moons.resetRenderModifier();

//         initListsJob = new InitListsJob()
//         {
//             items = mFloorsNativeArray,
//             ballMaterial = materialsManager.getSelectedBallMaterial().material
//         };

//         initListsJobHandle = initListsJob.Schedule(mFloors.Count, 64);
//     }

//     public void setBall(int index)
//     {
//         if (PlayerPrefs.HasKey(ballMaterials[index].material.name) || index == 0)
//         {
//             Debug.Log("Setting player material!");
//             Material materialIns = new Material(ballMaterials[index].material);
//             materialIns.shader = litShader;
//             materialIns.renderQueue = ballRenderQueue;
//             player.GetComponent<MeshRenderer>().material = materialIns;
//             player.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
//             // player.GetComponent<Moons>().setMoons();
//             // player.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", Color.black);
//             // if (ballMaterials[index].isBright)
//             //     currentBall.GetComponent<MeshRenderer>().material = Utility.decreaseIntensity(ballMaterials[index].material);
//             // else
//             currentBall.GetComponent<MeshRenderer>().material = materialIns;
//             currentBall.GetComponent<Moons>().setMoons(true);
//             currentBall.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);

//             if (listContainer.Find("Ball" + index) != null)
//                 listContainer.Find("Ball" + index).GetComponent<Animation>().Play();

//             PlayerPrefs.SetString(Utility.Constants.KEY_CURRENT_BALL, ballMaterials[index].material.name);
//             PlayerPrefs.Save();

//             currentBallIndex = index;

//             Moons.resetRenderModifier();
//             foreach (Test item in mFloors)
//             {
//                 Material tempMaterial = new Material(materialIns);
//                 tempMaterial.shader = stencilShader;
//                 tempMaterial.renderQueue = ballRenderQueue + Moons.getRenderModifier();
//                 item.t.Find("Sphere").GetComponent<MeshRenderer>().material = tempMaterial;
//                 item.t.Find("Sphere").GetComponent<Moons>().setMoons();
//                 item.t.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
//             }
//         }
//         else
//         {
//             if (playerStats.getDiamondsCount() >= ballMaterials[index].price)
//             {
//                 Debug.Log("Setting player material!");
//                 Material materialIns = new Material(ballMaterials[index].material);
//                 materialIns.shader = litShader;
//                 materialIns.renderQueue = ballRenderQueue;
//                 player.GetComponent<MeshRenderer>().material = materialIns;
//                 player.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
//                 currentBall.GetComponent<MeshRenderer>().material = materialIns;
//                 currentBall.GetComponent<Moons>().setMoons(true);
//                 currentBall.GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);

//                 if (listContainer.Find("Ball" + index) != null)
//                 {
//                     listContainer.Find("Ball" + index).GetComponent<Animation>().Play();
//                     listContainer.Find("Ball" + index).Find("Lock").gameObject.SetActive(false);
//                 }

//                 PlayerPrefs.SetString(Utility.Constants.KEY_CURRENT_BALL, ballMaterials[index].material.name);
//                 PlayerPrefs.SetInt(ballMaterials[index].material.name, 1);
//                 PlayerPrefs.Save();

//                 currentBallIndex = index;

//                 StartCoroutine(subtractDiamondsAnimation(playerStats.getDiamondsCount(), ballMaterials[index].price));
//                 playerStats.subtractDiamonds(ballMaterials[index].price);

//                 // foreach (Transform item in mFloors)
//                 // {
//                 //     item.Find("Sphere").GetComponent<MeshRenderer>().material = materialIns;
//                 //     item.Find("Sphere").GetComponent<Moons>().setMoons();
//                 // }

//                 Moons.resetRenderModifier();
//                 foreach (Test item in mFloors)
//                 {
//                     Material tempMaterial = new Material(materialIns);
//                     tempMaterial.shader = stencilShader;
//                     tempMaterial.renderQueue = ballRenderQueue + Moons.getRenderModifier();
//                     item.t.Find("Sphere").GetComponent<MeshRenderer>().material = tempMaterial;
//                     item.t.Find("Sphere").GetComponent<Moons>().setMoons();
//                     item.t.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
//                 }
//             }
//             else
//             {
//                 Debug.Log("You don't have enough diamonds");

//                 if (listContainer.Find("Ball" + index) != null)
//                     listContainer.Find("Ball" + index).Find("Lock").GetComponent<Animation>().Play();
//             }
//         }
//     }

//     public void setFloor(int index)
//     {
//         Material selectedMainPartMaterial;
//         Material selectedLandItemMaterial;
//         Material selectedLandLeftMaterial;
//         Material selectedLandRightMaterial;
//         Material selectedLandStartMaterial;
//         int id;
//         BaseMaterial selectedMaterial;

//         if (index < floorMaterials.Count)
//         {
//             selectedMainPartMaterial = selectedLandItemMaterial = selectedLandLeftMaterial = selectedLandRightMaterial = selectedLandStartMaterial = new Material(floorMaterials[index].material);
//             id = floorMaterials[index].id;
//             selectedMaterial = floorMaterials[index];
//         }
//         else
//         {
//             int indexInPatterns = index - floorMaterials.Count;
//             selectedMainPartMaterial = new Material(patternFloorMaterials[indexInPatterns].mainPart);
//             selectedLandItemMaterial = new Material(patternFloorMaterials[indexInPatterns].landItem);
//             selectedLandLeftMaterial = new Material(patternFloorMaterials[indexInPatterns].landLeft);
//             selectedLandRightMaterial = new Material(patternFloorMaterials[indexInPatterns].landRight);
//             selectedLandStartMaterial = new Material(patternFloorMaterials[indexInPatterns].landStart);
//             id = patternFloorMaterials[indexInPatterns].id;
//             selectedMaterial = patternFloorMaterials[indexInPatterns];
//         }

//         if (PlayerPrefs.HasKey("Material_" + id) || index == 0)
//         {
//             Debug.Log("Setting floor material!");
//             // setPartMaterial(landStart, selectedMaterial);
//             // setPartMaterial(landStraight, selectedMaterial);
//             // setPartMaterial(landLeft, selectedMaterial);
//             // setPartMaterial(landRight, selectedMaterial);
//             // setPartMaterial(curveUp, selectedMaterial);
//             // setPartMaterial(slide, selectedMaterial);
//             // setPartMaterial(curveSt, selectedMaterial);
//             setPartMaterial(currentFloor.Find("PathLandItem"), selectedMaterial);

//             partsPool.setPartsMaterial(selectedMaterial);

//             if (listContainer.Find("Floor" + index) != null)
//                 listContainer.Find("Floor" + index).GetComponent<Animation>().Play();

//             PlayerPrefs.SetInt(Utility.Constants.KEY_CURRENT_FLOOR, id);
//             PlayerPrefs.Save();

//             currentFloorIndex = index;

//             Moons.resetRenderModifier();
//             foreach (Test item in mBalls)
//             {
//                 Material tempMaterial;
//                 if (selectedMaterial is PatternMaterial)
//                 {
//                     tempMaterial = new Material((selectedMaterial as PatternMaterial).landItem);
//                 }
//                 else
//                 {
//                     tempMaterial = new Material((selectedMaterial as ColorMaterial).material);
//                 }
//                 tempMaterial.shader = stencilShader;
//                 tempMaterial.renderQueue = floorRenderQueue + Moons.getRenderModifier();
//                 Moons.decreaseRenderQueue();
//                 item.t.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material = tempMaterial;
//             }
//         }
//         else
//         {
//             if (true || playerStats.getDiamondsCount() >= floorMaterials[index].price)
//             {
//                 Debug.Log("Setting floor material!");
//                 setPartMaterial(landStart, selectedMaterial);
//                 setPartMaterial(landStraight, selectedMaterial);
//                 setPartMaterial(landLeft, selectedMaterial);
//                 setPartMaterial(landRight, selectedMaterial);
//                 setPartMaterial(curveUp, selectedMaterial);
//                 setPartMaterial(slide, selectedMaterial);
//                 setPartMaterial(curveSt, selectedMaterial);
//                 setPartMaterial(currentFloor.Find("PathLandItem"), selectedMaterial);

//                 if (selectedMaterial is PatternMaterial)
//                 {
//                     partsPool.setPatternMaterial(selectedMaterial as PatternMaterial);
//                 }
//                 else
//                 {
//                     partsPool.setPartsMaterial(selectedMaterial as ColorMaterial);
//                 }

//                 if (listContainer.Find("Floor" + index) != null)
//                 {
//                     listContainer.Find("Floor" + index).GetComponent<Animation>().Play();
//                     listContainer.Find("Floor" + index).Find("Lock").gameObject.SetActive(false);
//                 }

//                 PlayerPrefs.SetInt(Utility.Constants.KEY_CURRENT_FLOOR, id);
//                 PlayerPrefs.SetInt("Material_" + id, 1);
//                 PlayerPrefs.Save();

//                 currentFloorIndex = index;

//                 // foreach (Transform item in mBalls)
//                 // {
//                 //     if (selectedMaterial is PatternMaterial)
//                 //     {
//                 //         item.Find("Mesh").GetComponent<MeshRenderer>().material = (selectedMaterial as PatternMaterial).landItem;
//                 //     }
//                 //     else
//                 //     {
//                 //         item.Find("Mesh").GetComponent<MeshRenderer>().material = (selectedMaterial as MaterialPricePair).material;
//                 //     }
//                 // }

//                 Moons.resetRenderModifier();
//                 foreach (Test item in mBalls)
//                 {
//                     Material tempMaterial;
//                     if (selectedMaterial is PatternMaterial)
//                     {
//                         tempMaterial = new Material((selectedMaterial as PatternMaterial).landItem);
//                     }
//                     else
//                     {
//                         tempMaterial = new Material((selectedMaterial as ColorMaterial).material);
//                     }
//                     tempMaterial.shader = stencilShader;
//                     tempMaterial.renderQueue = floorRenderQueue + Moons.getRenderModifier();
//                     Moons.decreaseRenderQueue();
//                     item.t.Find("PathLandItem").Find("Mesh").GetComponent<MeshRenderer>().material = tempMaterial;
//                 }

//                 // playerStats.subtractDiamonds(floorMaterials[index].price);
//                 // StartCoroutine(subtractDiamondsAnimation(floorMaterials[index].price));
//             }
//             else
//             {
//                 Debug.Log("You don't have enough diamonds");

//                 if (listContainer.Find("Floor" + index) != null)
//                     listContainer.Find("Floor" + index).Find("Lock").GetComponent<Animation>().Play();
//             }
//         }
//     }

//     private void setPartMaterial(Transform part, BaseMaterial baseMaterial)
//     {
//         Material material;
//         if (baseMaterial is PatternMaterial)
//         {
//             PatternMaterial patternMaterial = baseMaterial as PatternMaterial;
//             switch (part.tag)
//             {
//                 case "LandStart":
//                     material = patternMaterial.landStart;
//                     break;
//                 case "LandRight":
//                     material = patternMaterial.landRight;
//                     break;
//                 case "LandLeft":
//                     material = patternMaterial.landLeft;
//                     break;
//                 case "Land":
//                 case "ListItem":
//                     material = patternMaterial.landItem;
//                     // material.SetFloat("_FadeEndDistance", 0);
//                     break;
//                 default:
//                     material = patternMaterial.mainPart;
//                     break;
//             }
//         }
//         else
//         {
//             material = (baseMaterial as ColorMaterial).material;
//         }

//         material = new Material(material);

//         if (part.tag == "Land")
//         {
//             material.shader = litShader;
//             material.renderQueue = floorRenderQueue + Moons.getRenderModifier();
//         }
//         else if (part.tag == "ListItem")
//         {
//             material.shader = litShader;
//             material.renderQueue = 2009;
//         }
//         else
//         {
//             material.shader = fadeShader;
//             material.renderQueue = 2000;
//         }

//         if (part.Find("Mesh") != null)
//         {
//             // if (part.tag == "Land")
//             // {
//             //     material = new Material(material);
//             //     material.shader = litShader;
//             // }
//             part.Find("Mesh").GetComponent<Renderer>().material = material;
//             // if (part.tag == "Land")
//             // {
//             //     part.Find("Mesh").GetComponent<Renderer>().sharedMaterial.SetFloat("_FadeEndDistance", 0);
//             // }
//         }
//         if (part.Find("PartStartBlock") != null)
//             part.Find("PartStartBlock").GetComponent<Renderer>().material = material;
//         if (part.Find("PartEndBlock") != null)
//             part.Find("PartEndBlock").GetComponent<Renderer>().material = material;
//     }

//     private IEnumerator subtractDiamondsAnimation(int diamondsCount, int amount)
//     {
//         for (int i = 1; i <= amount; i++)
//         {
//             diamondsCountText.text = Utility.getFormatedNumber(diamondsCount - i);
//             yield return new WaitForSeconds(0.6f / amount);
//         }
//     }

//     public void Back()
//     {
//         menusController.hideCurrentMenu(false);
//     }

//     private void setCurrentBallY(float newY)
//     {
//         currentBall.parent.localPosition = new Vector3(currentBall.parent.localPosition.x, newY, currentBall.parent.localPosition.z);
//     }

//     private void handleMenuLayout()
//     {
//         float ratio = (float)Screen.height / Screen.width;

//         if (ratio >= 1.1f && ratio < 1.3f) // 2176x1812 Fold
//         {
//             set3dListSize(1000, 900);
//             setCurrentBallY(10);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -818, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -818, 0);
//             currentBall.parent.localScale = new Vector3(65, 60, 60);
//         }
//         else if (ratio >= 1.55f && ratio < 1.64f) // Tablet 2560x1600
//         {
//             set3dListSize(1000, 1300);
//             setCurrentBallY(60);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1040, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1040, 0);
//             listContainer.localPosition = new Vector3(listContainer.localPosition.x, -915, listContainer.localPosition.z);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -20, 0);
//         }
//         else if (ratio >= 1.64f && ratio < 1.7f) // Tablet 2000x1200
//         {
//             set3dListSize(1000, 1350);
//             setCurrentBallY(60);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1040, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1040, 0);
//             listContainer.localPosition = new Vector3(listContainer.localPosition.x, -915, listContainer.localPosition.z);
//         }
//         else if (ratio >= 1.7f && ratio < 1.9f) // 1920x1080
//         {
//             set3dListSize(1000, 1500);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1130, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1130, 0);
//             // listContainer.localPosition = new Vector3(listContainer.localPosition.x, -915, listContainer.localPosition.z);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -10, 0);
//         }
//         else if (ratio >= 1.9f && ratio < 2.05f) // 2160x1080
//         {
//             set3dListSize(1000, 1700);
//             setCurrentBallY(100);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1230, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1230, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -10, 0);
//         }
//         else if (ratio >= 2.03f && ratio < 2.11f) // 2960x1440
//         {
//             set3dListSize(1000, 1700);
//             setCurrentBallY(110);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1231, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1231, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -10, 0);
//         }
//         else if (ratio >= 2.11f && ratio < 2.19f) // 2616x1212
//         {
//             set3dListSize(1000, 1700);
//             setCurrentBallY(120);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1300, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1300, 0);
//             titleContainer.anchoredPosition3D = new Vector3(0, -35, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -70, 0);
//         }
//         else if (ratio >= 2.19f && ratio < 2.3f) // 2400x1080
//         {
//             set3dListSize(1000, 1850);
//             setCurrentBallY(130);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1334, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1334, 0);
//             titleContainer.anchoredPosition3D = new Vector3(0, -10, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -40, 0);
//         }
//         else if (ratio >= 2.3f && ratio < 2.5f) // 3840x1644 (21:9)
//         {
//             set3dListSize(1000, 2000);
//             setCurrentBallY(135);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1403, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1403, 0);
//             titleContainer.anchoredPosition3D = new Vector3(0, -20, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -30, 0);
//         }
//         else if (ratio >= 2.5f && ratio < 2.6f) // 2316x904 (23.1:9) Fold cover
//         {
//             set3dListSize(1000, 2200);
//             setCurrentBallY(176);
//             my3dListTexture.anchoredPosition3D = new Vector3(0, -1544, 0);
//             my3dScrollView.anchoredPosition3D = new Vector3(0, -1544, 0);
//             titleContainer.anchoredPosition3D = new Vector3(0, -25, 0);
//             tabsContainer.anchoredPosition3D = new Vector3(0, -60, 0);
//         }
//     }

//     public void Execute(int index)
//     {
//     }

//     public struct InitListsJob : IJobParallelFor
//     {
//         public NativeArray<Test> items;
//         public Material ballMaterial;

//         public void Execute(int index)
//         {
//             Material tempMaterial = new Material(ballMaterial);
//             // tempMaterial.shader = stencilShader;
//             // tempMaterial.renderQueue = ballRenderQueue + Moons.getRenderModifier();
//             items[index].t.Find("Sphere").GetComponent<MeshRenderer>().material = tempMaterial;
//             items[index].t.Find("Sphere").GetComponent<Moons>().setMoons();
//             items[index].t.Find("Sphere").GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects();
//         }
//     }

//     public struct Test
//     {
//         public Transform t;
//     }
// }
