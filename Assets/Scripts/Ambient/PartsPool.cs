using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartsPool : MonoBehaviour
{
    public Transform player;
    public Camera gameCamera;
    public Transform partsPoolParent;
    public Transform landStart;
    public Transform landStraight;
    public Transform landLeft;
    public Transform landRight;
    public Transform curveUp;
    public Transform slide;
    public Transform curveStraight;
    public Transform diamond;
    public Transform bolt;
    public Transform doublePoint;
    public Transform chance;
    public Transform box;
    public Transform listItem;
    public Transform moon;

    private int landStartsCount = 1;
    private int landStraightsCount = 10;
    private int landLeftsCount = 25;
    private int landRightsCount = 25;
    private int curveUpsCount = 20;
    private int slidesCount = 30;
    private int curveStraightsCount = 20;
    private int diamondsCount = 20;
    private int boltsCount = 2;
    private int doublePointsCount = 2;
    private int chancesCount = 2;
    private int boxesCount = 2;
    private int listItemsCount = 126;
    private int moonsCount = 600;

    public Queue landStarts = new Queue();
    public Queue landStraights = new Queue();
    public Queue landRights = new Queue();
    public Queue landLefts = new Queue();
    public Queue curveUps = new Queue();
    public Queue slides = new Queue();
    public Queue curveStraights = new Queue();
    public Queue diamonds = new Queue();
    public Queue bolts = new Queue();
    public Queue doublePoints = new Queue();
    public Queue chances = new Queue();
    public Queue boxes = new Queue();
    public Queue listItems = new Queue();
    public Queue moons = new Queue();

    private static int numberOfInstansiatedParts = 0;
    private BaseMaterial currentFloorMaterial;
    // The floor each track part last got. The floor is set on the parts in the pool at that moment:
    // a part out of it then (on the path, or still falling away from the last one) came back with
    // the old floor, and a part made because the pool ran out had the prefab's - the odd part in
    // the wrong material. So a part also gets the current floor as it leaves the pool, if it lacks it.
    private readonly Dictionary<Transform, BaseMaterial> partFloors = new Dictionary<Transform, BaseMaterial>();
    private MaterialsManager materialsManager;

    void Awake()
    {
        materialsManager = FindObjectOfType<MaterialsManager>();

        int i = 0;

        for (i = 0; i < landStartsCount; i++)
            landStarts.Enqueue(instantiatePart(landStart));

        for (i = 0; i < landStraightsCount; i++)
            landStraights.Enqueue(instantiatePart(landStraight));

        for (i = 0; i < landRightsCount; i++)
            landRights.Enqueue(instantiatePart(landRight));

        for (i = 0; i < landLeftsCount; i++)
            landLefts.Enqueue(instantiatePart(landLeft));

        for (i = 0; i < curveUpsCount; i++)
            curveUps.Enqueue(instantiatePart(curveUp));

        for (i = 0; i < slidesCount; i++)
            slides.Enqueue(instantiatePart(slide));

        for (i = 0; i < curveStraightsCount; i++)
            curveStraights.Enqueue(instantiatePart(curveStraight));

        for (i = 0; i < diamondsCount; i++)
            diamonds.Enqueue(instantiatePart(diamond));

        for (i = 0; i < boltsCount; i++)
            bolts.Enqueue(instantiatePart(bolt));

        for (i = 0; i < doublePointsCount; i++)
            doublePoints.Enqueue(instantiatePart(doublePoint));

        for (i = 0; i < chancesCount; i++)
            chances.Enqueue(instantiatePart(chance));

        for (i = 0; i < boxesCount; i++)
            boxes.Enqueue(instantiatePart(box));

        for (i = 0; i < listItemsCount; i++)
            listItems.Enqueue(instantiatePart(listItem));

        for (i = 0; i < moonsCount; i++)
            moons.Enqueue(instantiatePart(moon));

    }

    private Transform instantiatePart(Transform part)
    {
        Transform clone = Instantiate(part, Vector3.zero, Quaternion.identity, partsPoolParent);

        clone.gameObject.SetActive(false);
        return clone;
    }

    public Transform getPart(Parts targetPart, PickUpType? pickUpType = null)
    {

        Transform part;

        float pickUpHeight = 0;

        switch (targetPart)
        {
            case Parts.LandStart:
                part = getPart(landStarts, landStart);
                pickUpHeight = 0.3f;
                break;
            case Parts.LandStraight:
                part = getPart(landStraights, landStraight);
                pickUpHeight = 0.3f;
                break;
            case Parts.LandRight:
                part = getPart(landRights, landRight);
                pickUpHeight = 0.3f;
                break;
            case Parts.LandLeft:
                part = getPart(landLefts, landLeft);
                pickUpHeight = 0.3f;
                break;
            case Parts.CurveUp:
                part = getPart(curveUps, curveUp);
                pickUpHeight = 1f;
                break;
            case Parts.Slide:
                part = getPart(slides, slide);
                pickUpHeight = 1.35f;
                break;
            case Parts.CurveStraight:
                part = getPart(curveStraights, curveStraight);
                pickUpHeight = 0.1f;
                break;
            case Parts.ListItem:
                part = getPart(listItems, listItem);
                break;
            case Parts.Moon:
                part = getPart(moons, moon);
                break;
            default:
                part = instantiatePart(landStraight);
                pickUpHeight = 0.3f;
                break;
        }

        if (pickUpType == PickUpType.Diamond)
        {
            Transform myDiamond = getPart(diamonds, diamond);
            myDiamond.position = part.position + new Vector3(0f, pickUpHeight, 0f);
            myDiamond.parent = part;
            myDiamond.GetComponent<DiamondPickUp>().reset();
        }

        if (pickUpType == PickUpType.Bolt)
        {
            Transform myBolt = getPart(bolts, bolt);
            myBolt.position = part.position + new Vector3(0f, pickUpHeight, 0f);
            myBolt.parent = part;
            myBolt.GetComponent<BoltPickUp>().reset();
        }

        if (pickUpType == PickUpType.DoublePoints)
        {
            Transform myDoublePoints = getPart(doublePoints, doublePoint);
            myDoublePoints.position = part.position + new Vector3(0f, pickUpHeight, 0f);
            myDoublePoints.parent = part;
            myDoublePoints.GetComponent<DoublePointPickUp>().reset();
        }

        if (pickUpType == PickUpType.Chance)
        {
            Transform myChance = getPart(chances, chance);
            myChance.position = part.position + new Vector3(0f, pickUpHeight, 0f);
            myChance.parent = part;
            myChance.GetComponent<ChancePickUp>().reset();
        }

        if (pickUpType == PickUpType.MysteryBox)
        {
            Transform myBox = getPart(boxes, box);
            myBox.position = part.position + new Vector3(0f, pickUpHeight, 0f);
            myBox.parent = part;
            myBox.GetComponent<BoxPickUp>().reset();
        }

        if (targetPart == Parts.ListItem)
        {
            if (part.GetComponent<ObjectOnClick>() != null)
            {
                Destroy(part.GetComponent<ObjectOnClick>());
            }
        }
        else if (targetPart != Parts.Moon)
        {
            BaseMaterial floor;
            if (currentFloorMaterial != null && (!partFloors.TryGetValue(part, out floor) || floor != currentFloorMaterial))
                setPartMaterial(part, currentFloorMaterial);

            if (part.gameObject.GetComponent<MoveDown>() != null)
                Destroy(part.gameObject.GetComponent<MoveDown>());

            part.gameObject.AddComponent<MoveDown>();

            if (part.gameObject.GetComponent<AlphaManager>() != null)
                Destroy(part.gameObject.GetComponent<AlphaManager>());

            // part.gameObject.AddComponent<AlphaManager>();
            // part.gameObject.GetComponent<AlphaManager>().setPlayer(player);
            // part.gameObject.GetComponent<AlphaManager>().setGameCamera(gameCamera);
            // part.gameObject.GetComponent<AlphaManager>().setCurrentFloorMaterial(currentFloorMaterial);

        }

        return part;

    }

    private Transform getPart(Queue partsQueue, Transform part)
    {
        if (partsQueue.Count == 0)
        {
            print("Number of instansiated parts = " + ++numberOfInstansiatedParts + ", part name = " + part.name);
            return instantiatePart(part);
        }
        else
        {
            return (Transform)partsQueue.Dequeue();
        }
    }

    private IEnumerator partsCoroutine;

    private IEnumerator initPartsCoroutine(List<Transform> parts)
    {
        foreach (Transform part in parts)
        {
            setPart(part);
            yield return new WaitForSecondsRealtime(0.005f);
        }
    }

    public void setParts(List<Transform> parts)
    {
        partsCoroutine = initPartsCoroutine(parts);
        StartCoroutine(partsCoroutine);
    }

    public void setPart(Transform part)
    {

        if (part == null)
            return;

        part.gameObject.SetActive(false);

        if (part.gameObject.GetComponent<MoveDown>() != null)
            Destroy(part.gameObject.GetComponent<MoveDown>());

        if (part.gameObject.GetComponent<AlphaManager>() != null)
            Destroy(part.gameObject.GetComponent<AlphaManager>());

        // setPartMaterial(part, currentFloorMaterial);

        part.parent = partsPoolParent;

        switch (part.tag)
        {
            case "LandStart":
                landStarts.Enqueue(part);
                part.name = getPartName(landStart);
                break;
            case "LandStraight":
                landStraights.Enqueue(part);
                part.name = getPartName(landStraight);
                break;
            case "LandRight":
                landRights.Enqueue(part);
                part.name = getPartName(landRight);
                break;
            case "LandLeft":
                landLefts.Enqueue(part);
                part.name = getPartName(landLeft);
                break;
            case "CurveUp":
                curveUps.Enqueue(part);
                part.name = getPartName(curveUp);
                break;
            case "Slide":
                slides.Enqueue(part);
                part.name = getPartName(slide);
                break;
            case "CurveSt":
                curveStraights.Enqueue(part);
                part.name = getPartName(curveStraight);
                break;
            case "Diamond":
                diamonds.Enqueue(part);
                part.name = getPartName(diamond);
                break;
            case "Bolt":
                bolts.Enqueue(part);
                part.name = getPartName(bolt);
                break;
            case "DoublePoint":
                doublePoints.Enqueue(part);
                part.name = getPartName(doublePoint);
                break;
            case "Chance":
                chances.Enqueue(part);
                part.name = getPartName(chance);
                break;
            case "MysteryBox":
                boxes.Enqueue(part);
                part.name = getPartName(box);
                break;
            case "ListItem":
                listItems.Enqueue(part);
                part.name = getPartName(listItem);
                break;
            case "Planet":
                moons.Enqueue(part);
                part.name = getPartName(moon);
                break;
        }
    }

    private string getPartName(Transform part)
    {
        return part.name + "(Clone)";
    }

    public void setPartAfterTime(Transform part, float timeInSeconds)
    {
        StartCoroutine(setPartAfterTimeCoroutine(part, timeInSeconds));
    }

    private IEnumerator setPartAfterTimeCoroutine(Transform part, float timeInSeconds)
    {
        yield return new WaitForSeconds(timeInSeconds);
        if (part.parent.name == Utility.Constants.DESTROYING_OBJECT_NAME) // To fix disapearing diamonds
            setPart(part);
    }

    public void refreshMaterials()
    {
        setPartsMaterial(materialsManager.getSelectedFloorMaterial());
    }

    public void setPartsMaterial(BaseMaterial material)
    {

        currentFloorMaterial = material;

        int i;

        for (i = 0; i < landStarts.ToArray().Length; i++)
            setPartMaterial(landStarts.ToArray()[i] as Transform, material);

        for (i = 0; i < landStraights.ToArray().Length; i++)
            setPartMaterial(landStraights.ToArray()[i] as Transform, material);

        for (i = 0; i < landRights.ToArray().Length; i++)
            setPartMaterial(landRights.ToArray()[i] as Transform, material);

        for (i = 0; i < landLefts.ToArray().Length; i++)
            setPartMaterial(landLefts.ToArray()[i] as Transform, material);

        for (i = 0; i < curveUps.ToArray().Length; i++)
            setPartMaterial(curveUps.ToArray()[i] as Transform, material);

        for (i = 0; i < slides.ToArray().Length; i++)
            setPartMaterial(slides.ToArray()[i] as Transform, material);

        for (i = 0; i < curveStraights.ToArray().Length; i++)
            setPartMaterial(curveStraights.ToArray()[i] as Transform, material);

        // for (i = 0; i < listItems.ToArray().Length; i++)
        //     setPartMaterial(listItems.ToArray()[i] as Transform, material);
    }

    public void setPatternMaterial(PatternMaterial material)
    {

        currentFloorMaterial = material;

        int i;

        for (i = 0; i < landStarts.ToArray().Length; i++)
            setPartMaterial(landStarts.ToArray()[i] as Transform, material);

        for (i = 0; i < landStraights.ToArray().Length; i++)
            setPartMaterial(landStraights.ToArray()[i] as Transform, material);

        for (i = 0; i < landRights.ToArray().Length; i++)
            setPartMaterial(landRights.ToArray()[i] as Transform, material);

        for (i = 0; i < landLefts.ToArray().Length; i++)
            setPartMaterial(landLefts.ToArray()[i] as Transform, material);

        for (i = 0; i < curveUps.ToArray().Length; i++)
            setPartMaterial(curveUps.ToArray()[i] as Transform, material);

        for (i = 0; i < slides.ToArray().Length; i++)
            setPartMaterial(slides.ToArray()[i] as Transform, material);

        for (i = 0; i < curveStraights.ToArray().Length; i++)
            setPartMaterial(curveStraights.ToArray()[i] as Transform, material);

        // for (i = 0; i < listItems.ToArray().Length; i++)
        //     setPartMaterial(listItems.ToArray()[i] as Transform, material);
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
                case "ListItem":
                    material = patternMaterial.landItem;
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

        material = materialsManager.getLitMaterial(materialsManager.getRuntimeCopy(material), 2010);
        partFloors[part] = baseMaterial;

        if (part.Find("Mesh") != null)
            part.Find("Mesh").GetComponent<Renderer>().material = material;
        if (part.Find("PartStartBlock") != null)
            part.Find("PartStartBlock").GetComponent<Renderer>().material = material;
        if (part.Find("PartEndBlock") != null)
            part.Find("PartEndBlock").GetComponent<Renderer>().material = material;
    }
}
