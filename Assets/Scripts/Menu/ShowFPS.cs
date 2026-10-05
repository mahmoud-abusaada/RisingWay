using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShowFPS : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;
    private float deltaTime;

    float fps;
    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
        fpsText.text = "FPS: " + Mathf.Ceil(fps).ToString();
        // Debug.Log("fps position on screen = " + Camera.main.WorldToScreenPoint(fpsText.transform.position));
    }

    /// <summary>
    /// Settings > Graphics > Show FPS. Hidden also stops Update, so the counter costs nothing
    /// (it rebuilds a TextMeshPro mesh every frame while shown).
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (visible)
            drawOnTop();
        enabled = visible;
        fpsText.gameObject.SetActive(visible);
        deltaTime = 1f / 60f; // a sane starting average, not a 1/0 spike
    }

    // Over every menu (Purchase, Upgrade...), which are canvases of their own drawn later: the
    // counter gets its own canvas, sorted above them all. It takes no touches.
    private void drawOnTop()
    {
        if (fpsText.GetComponent<Canvas>() != null)
            return;
        Canvas own = fpsText.gameObject.AddComponent<Canvas>();
        own.overrideSorting = true;
        own.sortingOrder = 30000;
        fpsText.raycastTarget = false;
    }
}
