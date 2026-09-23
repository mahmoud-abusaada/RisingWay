using UnityEngine;
using System.Collections;

public class Destroyer : MonoBehaviour
{

    public Vector3 originalPos;
    private PickUpsManager pickUpsManager;
    private PartsPool partsPool;
    private PathMaker pathMaker;
    public static int destroyedBricksCount = 0;
    private IEnumerator delayCoroutine;
    private TurnDirection? nextDirection = null;

    void Awake()
    {
        originalPos = transform.parent.position;
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        partsPool = FindObjectOfType<PartsPool>();
        pathMaker = FindObjectOfType<PathMaker>();
    }

    public void setOriginalPos(Vector3 position)
    {
        originalPos = position;
    }

    public void setNextDirection(TurnDirection nextDirection)
    {
        // Debug.Log("Set Distination = " + nextDirection.nextDistination);
        this.nextDirection = nextDirection;
    }

    // static int skippedDiamonds = 0;

    private void Update()
    {
        if (transform.parent.position.y < originalPos.y - 50)
            returnToPool();
    }

    /// <summary>
    /// Back to the pool, with any pickup on it. Normally once the part has fallen 50 below where it
    /// stood; PathMaker also calls it for parts of an old path that have not got there in time.
    /// </summary>
    public void returnToPool()
    {
        if (transform.parent.Find("PickUp") != null)
        {
            if (transform.parent.Find("PickUp").CompareTag("Bolt"))
                pathMaker.boltIsOver();
            if (transform.parent.Find("PickUp").CompareTag("Chance"))
                pathMaker.chanceIsOver();
            if (transform.parent.Find("PickUp").CompareTag("DoublePoint"))
                pathMaker.doublePointsIsOver();
            // if (transform.parent.Find("PickUp").CompareTag("Diamond"))
            // {
            //     skippedDiamonds++;
            //     Debug.Log("Skipped a diamond, count = " + skippedDiamonds);
            // }

            partsPool.setPart(transform.parent.Find("PickUp"));
        }
        partsPool.setPart(transform.parent);
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
        }
        // Destroy(transform.parent.gameObject);
    }

    private void OnTriggerEnter(Collider obj)
    {
        if (obj.gameObject.CompareTag("Player"))
        {
            Utility.shouldDequeue = true;

            if (nextDirection != null)
                pathMaker.nextDirection = nextDirection;
            else
            {
                pathMaker.setFirstTurnAsNextDirection();
            }
            // Debug.Log("New Distination = " + nextDirection?.nextDistination);
        }
    }

    private void OnTriggerExit(Collider obj)
    {
        // Utility.shouldDequeue = false;
        if (Utility.gameStarted && obj.gameObject.CompareTag("Player") && !Utility.spawningAfterChance)
        {
            destroy(true);
        }
    }

    public void destroy(bool dequeue = false)
    {
        transform.parent.gameObject.name = Utility.Constants.DESTROYING_OBJECT_NAME;

        // Only for the path the ball is on. An old path taken down after a fall also comes through
        // here - repeatedly, while its last parts fall away - and once the revived ball was
        // rolling again, this knocked down every part of the NEW path under it. The ball then fell
        // with the camera still following, until the old path was gone and the fall was noticed.
        if (Utility.gameStarted && Utility.camFollowPlayer && pathMaker.isOnCurrentPath(transform.parent))
        {
            pathMaker.destroySkippedParts(transform.parent);
        }

        if (transform.parent.Find("PartStartBlock") != null)
            transform.parent.Find("PartStartBlock").gameObject.SetActive(true);

        if (transform.parent.Find("PartEndBlock") != null)
            transform.parent.Find("PartEndBlock").gameObject.SetActive(true);

        pathMaker.updatePartsRenderQueue();

        delayCoroutine = delayDestroy();
        StartCoroutine(delayCoroutine);
    }

    private IEnumerator delayDestroy()
    {
        yield return new WaitForSeconds(0.08f);

        if (transform.parent.GetChild(0).gameObject.GetComponent<MeshCollider>())
            (transform.parent.GetChild(0).gameObject.GetComponent<MeshCollider>()).enabled = false;

        transform.parent.gameObject.GetComponent<Rigidbody>().useGravity = true;
        transform.parent.gameObject.GetComponent<Rigidbody>().isKinematic = false;
    }
}