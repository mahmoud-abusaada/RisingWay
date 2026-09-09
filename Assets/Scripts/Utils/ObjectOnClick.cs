using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ObjectOnClick : MonoBehaviour
{

    [SerializeField] public UnityEvent<int> unityEvent = new UnityEvent<int>();
    [SerializeField] public RectTransform scrollViewRect;
    [SerializeField] public RectTransform mysteryBoxRect;
    [SerializeField] public Camera canvasCamera;
    [SerializeField] public ConfirmationDialog confirmationDialog;

    private ShopMenu shopMenu;
    private bool pressDown = false;
    private Vector3 pressDownPosition = Vector3.zero;
    private bool pressUp = false;
    private Vector3 pressUpPosition = Vector3.zero;

    public int id = 0;

    void Start()
    {
        shopMenu = FindObjectOfType<ShopMenu>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool raycast = Physics.Raycast(ray, out hit);

            if (hit.transform != null && hit.transform.parent != null && !confirmationDialog.gameObject.activeSelf)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, Input.mousePosition, Camera.main, out localPoint);

                Vector2 mysteryLocalPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(mysteryBoxRect, Input.mousePosition, Camera.main, out mysteryLocalPoint);

                if (raycast && (hit.transform.parent.gameObject == gameObject || hit.transform.parent.parent.gameObject == gameObject) && scrollViewRect.rect.Contains(localPoint) && !mysteryBoxRect.rect.Contains(mysteryLocalPoint))
                {
                    // Debug.Log("Hit gameObejct " + hit.transform.parent.gameObject.name + " clicked Down at " + gameObject.name);
                    pressDown = true;
                    pressDownPosition = Input.mousePosition;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool raycast = Physics.Raycast(ray, out hit);

            if (hit.transform != null && hit.transform.parent != null && !confirmationDialog.gameObject.activeSelf)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollViewRect, Input.mousePosition, Camera.main, out localPoint);

                Vector2 mysteryLocalPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(mysteryBoxRect, Input.mousePosition, Camera.main, out mysteryLocalPoint);

                if (raycast && (hit.transform.parent.gameObject == gameObject || hit.transform.parent.parent.gameObject == gameObject) && scrollViewRect.rect.Contains(localPoint) && !mysteryBoxRect.rect.Contains(mysteryLocalPoint))
                {
                    // Debug.Log("Hit gameObejct " + hit.transform.parent.gameObject.name + " clicked Up at " + gameObject.name);
                    pressUp = true;
                    pressUpPosition = Input.mousePosition;

                    if (Vector3.Distance(pressDownPosition, pressUpPosition) >= 10)
                    {
                        pressDown = false;
                        pressUp = false;
                        pressDownPosition = Vector3.zero;
                        pressUpPosition = Vector3.zero;
                    }

                    // Debug.Log("Down = " + pressDownPosition + ", Up = " + pressUpPosition + ", distance = " + Vector3.Distance(pressDownPosition, pressUpPosition));
                }
            }
        }

        if (pressDown && pressUp && Vector3.Distance(pressDownPosition, pressUpPosition) < 10 && !shopMenu.isMysteryBoxSeeking)
        {
            try
            {
                unityEvent.Invoke(id);
                pressDown = false;
                pressUp = false;
                pressDownPosition = Vector3.zero;
                pressUpPosition = Vector3.zero;
            }
            catch (UnityException) { }
        }
    }

    bool IsPointInRT(Vector3 point, RectTransform rt)
    {
        // Get the rectangular bounding box of your UI element
        Rect rect = rt.rect;

        // Get the left, right, top, and bottom boundaries of the rect
        float leftSide = rt.anchoredPosition.x - rect.width / 2;
        float rightSide = rt.anchoredPosition.x + rect.width / 2;
        float topSide = rt.anchoredPosition.y + rect.height / 2;
        float bottomSide = rt.anchoredPosition.y - rect.height / 2;

        // Debug.Log(leftSide + ", " + rightSide + ", " + topSide + ", " + bottomSide);
        // Debug.Log(point.x + ", " + point.y);

        // Check to see if the point is in the calculated bounds
        if (point.x >= leftSide &&
            point.x <= rightSide &&
            point.y >= bottomSide &&
            point.y <= topSide)
        {
            return true;
        }
        return false;
    }
}
