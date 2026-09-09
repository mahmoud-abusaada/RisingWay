using UnityEngine;
using System.Collections;

public class MoveDown : MonoBehaviour
{

    private Vector3 originalPos;
    private float step = 0.25f;
    private Transform previousPart = null;

    public void setPreviousPart(Transform previousPart)
    {
        this.previousPart = previousPart;
    }

    void Start()
    {
        originalPos = transform.position;
        transform.position += new Vector3(0, 10, 0);
    }

    void FixedUpdate()
    {
        if (transform.position.y > originalPos.y)
        {
            if (transform.position.y - step < originalPos.y)
                transform.position = originalPos;
            else
                transform.position -= new Vector3(0, step, 0);
        }
        else
        {
            // gameObject.AddComponent<Destroyer>();
            if (transform.Find("PartStartBlock") != null)
                transform.Find("PartStartBlock").gameObject.SetActive(false);
            if (previousPart != null && previousPart.Find("PartEndBlock") != null)
                previousPart.Find("PartEndBlock").gameObject.SetActive(false);
            Destroy(this);
        }
    }
}