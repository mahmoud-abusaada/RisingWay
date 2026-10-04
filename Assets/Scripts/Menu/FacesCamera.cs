using UnityEngine;

/// <summary>
/// A 3D model in a menu (the power-up and diamond icons) seen square on, wherever it sits on the
/// screen. The menus are canvases drawn by the game's perspective camera, so a model near the
/// edge was seen from the side - the Upgrade menu's icons on the left, the Purchase menu's
/// counters at either end - while the same models in a run, drawn by an orthographic camera,
/// face the player. Each frame the model is turned by the angle between the camera's view axis
/// and the line to the model, so it looks as it would in the middle of the screen.
///
/// Its own turning (Rotator spins it in FixedUpdate) is kept: the correction is taken off before
/// FixedUpdate and put back on after everything else has moved it.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class FacesCamera : MonoBehaviour
{
    private Quaternion own;        // the rotation it would have without the correction
    private Quaternion applied;    // what this left it at last frame
    private bool started;
    private Canvas canvas;
    // Seen a little from above (with the model's own tilt, NebulaSkin.AsTheBox: about 20 degrees
    // in all). Square on, a box showed its underside; at 30 it showed too much of its top.
    private const float FROM_ABOVE = 8f;

    /// <summary>Every 3D model laid out in the UI under <paramref name="root"/> (spinning or not),
    /// except the shop's items, which are a 3D scene of their own.</summary>
    public static void AddUnder(Transform root)
    {
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.GetComponent<FacesCamera>() != null || r.GetComponentInParent<ListItemAnimator>(true) != null)
                continue;
            if (!(r.transform.parent is RectTransform) || r.GetComponent<TMPro.TMP_Text>() != null)
                continue; // a model inside the UI's layout, not a 3D text
            if (r.GetComponentInParent<InGameUI>(true) != null)
                continue; // the run's power-ups: an orthographic camera, and their own look
            // Every icon at the same angle (the box reveal's ball and floor are not icons).
            if (!(r.transform.parent.name == "BubbleParent" && (r.name == "Ball" || r.name == "Floor")))
                NebulaSkin.AsTheBox(r.transform);
            r.gameObject.AddComponent<FacesCamera>();
        }
    }

    void OnEnable()
    {
        own = transform.localRotation;
        applied = own;
        started = true;
    }

    void OnDisable()
    {
        if (started)
            transform.localRotation = own;
    }

    void FixedUpdate()
    {
        if (transform.localRotation != applied)
            own = transform.localRotation; // turned by the last step (several can run in a frame)
        transform.localRotation = own; // Rotator turns the model's own rotation, not the corrected one
        applied = own;
    }

    void LateUpdate()
    {
        if (transform.localRotation != applied)
            own = transform.localRotation; // turned since (Rotator, an animation)
        // The camera that draws its canvas (in a run, the UI camera: orthographic, nothing to fix).
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : Camera.main;
        Transform parent = transform.parent;
        if (cam == null || parent == null)
            return;
        Vector3 toModel = transform.position - cam.transform.position;
        Quaternion fix = cam.orthographic ? Quaternion.identity
            : Quaternion.FromToRotation(cam.transform.forward, toModel.normalized) * Quaternion.AngleAxis(-FROM_ABOVE, cam.transform.right);
        transform.rotation = fix * (parent.rotation * own);
        applied = transform.localRotation;
    }
}
