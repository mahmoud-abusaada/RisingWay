using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIFader : MonoBehaviour
{

    private CanvasGroup uiElement;
    public static bool isFadingIn = false;
    public static bool isFadingOut = false;
    public bool isFadingInLocal = false;
    public bool isFadingOutLocal = false;

    void Awake()
    {
        uiElement = GetComponent<CanvasGroup>();
    }

    public void FadeIn()
    {
        if (!isFadingInLocal)
            gameObject.SetActive(true);
        // uiElement.interactable = false;
        // uiElement.blocksRaycasts = false;
        isFadingInLocal = true;
        StartCoroutine(FadeCanvasGroup(uiElement, 0, 1, .2f));
    }

    public void FadeOut()
    {
        isFadingOutLocal = true;
        StartCoroutine(FadeCanvasGroup(uiElement, 1, 0, .2f));
    }

    public IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float lerpTime = 1)
    {
        float _timeStartedLerping = Time.unscaledTime;
        float timeSinceStarted = Time.unscaledTime - _timeStartedLerping;
        float percentageComplete = timeSinceStarted / lerpTime;

        while (true)
        {
            timeSinceStarted = Time.unscaledTime - _timeStartedLerping;
            percentageComplete = timeSinceStarted / lerpTime;

            float currentValue = Mathf.Lerp(start, end, percentageComplete);

            cg.alpha = currentValue;

            if (percentageComplete >= 1) break;

            yield return new WaitForSecondsRealtime(0.02f);
        }

        if (end == 0)
        {
            gameObject.SetActive(false);
            isFadingOutLocal = false;
        }

        if (end == 1)
        {
            // uiElement.interactable = true;
            // uiElement.blocksRaycasts = true;
            isFadingInLocal = false;
        }

        print("done");
    }
}