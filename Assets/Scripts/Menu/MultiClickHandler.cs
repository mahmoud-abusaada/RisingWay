using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiClickHandler : MonoBehaviour
{
    public static MultiClickHandler Instance;
    private IEnumerator clickCoroutine;
    private bool canClick = true;

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

        clickCoroutine = clickDelay();
    }

    public bool CanClick()
    {
        bool result = canClick;
        if (clickCoroutine != null)
            StopCoroutine(clickCoroutine);
        clickCoroutine = clickDelay();
        StartCoroutine(clickCoroutine);
        return result;
    }

    private IEnumerator clickDelay()
    {
        canClick = false;
        Debug.Log("Can click = " + canClick);
        yield return new WaitForSecondsRealtime(0.2f);
        canClick = true;
        Debug.Log("Can click = " + canClick);
    }
}
