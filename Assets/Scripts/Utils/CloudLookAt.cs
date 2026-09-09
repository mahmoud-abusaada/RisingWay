using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudLookAt : MonoBehaviour
{
    [SerializeField] public Transform cameraController;
    [SerializeField] public bool printPositionOnScreen = false;
    private SpriteRenderer sprite;
    private Color spriteColor;

    void Start()
    {
        sprite = GetComponent<SpriteRenderer>();
        spriteColor = sprite.color;
    }

    private IEnumerator ShowCloud()
    {
        while (spriteColor.a < 1)
        {
            spriteColor.a += Time.deltaTime / 2;
            yield return new WaitForSecondsRealtime(0.01f);
        }
    }

    public void StartDestroyCoroutine()
    {
        StartCoroutine(DestroyCloud());
    }

    private IEnumerator DestroyCloud()
    {
        while (spriteColor.a > 0)
        {
            spriteColor.a -= Time.deltaTime / 10;
            yield return new WaitForSecondsRealtime(0.01f);
        }
        GameObject.Destroy(gameObject);
    }

    Vector3 onScreenPoint;
    float value;
    // Update is called once per frame
    void Update()
    {
        transform.LookAt(new Vector3(cameraController.position.x, cameraController.position.y, cameraController.position.z));
        // float value = (cameraController.position.y - transform.position.y - 600);           // Bad
        // if (transform.eulerAngles.x > 320 && transform.eulerAngles.x <= 340 && Vector3.Distance(transform.position, cameraController.position) > 400)                // Cool
        // {
        //     spriteColor.a = 1 - Mathf.Abs(transform.eulerAngles.x - 340) / 20;
        //     sprite.color = spriteColor;
        // }

        // if (sprite.isVisible)
        // {
        onScreenPoint = Camera.main.WorldToScreenPoint(transform.position);
        if (onScreenPoint.y > 0 && onScreenPoint.y < Screen.height && onScreenPoint.x > 0 && onScreenPoint.x < Screen.width)
        {
            value = onScreenPoint.y / Screen.height;  // Not working fine
            if (value > 0 && value < 0.3f)
            {
                spriteColor.a = value / 0.3f;
                sprite.color = spriteColor;
            }
            else if (value >= 0.2f && spriteColor.a < 1)
            {
                spriteColor.a = 1;
                sprite.color = spriteColor;
            }
            else if (value < 0 && spriteColor.a > 0)
            {
                spriteColor.a = 0;
                sprite.color = spriteColor;
            }

            if (spriteColor.a < 0.05f)
            {
                GameObject.Destroy(gameObject);
                Debug.Log("Destroyed a cloud!");
            }

            if (printPositionOnScreen)
                Debug.Log("y = " + Camera.main.WorldToScreenPoint(transform.position).y + ", value = " + value);
        }
    }
    // }
}
