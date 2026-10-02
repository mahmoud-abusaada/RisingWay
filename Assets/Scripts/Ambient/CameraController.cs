using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class CameraController : MonoBehaviour
{

    [SerializeField] private Transform player;
    [SerializeField] private Transform moonsParent;
    [SerializeField] private ParticleSystem chanceTakenEffect;

    // Turn easing, per second of game time. These reproduce the old per-physics-step easing
    // exactly (20% of the remaining angle per 0.02s step, 10% in tutorials) but run every
    // rendered frame, so the camera turns smoothly at any frame rate instead of in physics-step
    // increments.
    private const float TURN_RATE = 11.157f;           // -ln(0.8) / 0.02
    private const float TUTORIAL_TURN_RATE = 5.268f;   // -ln(0.9) / 0.02
    private const float TURN_SNAP_DEGREES = 0.01f;

    // Yaw is tracked as a target and a current value, and the rig's rotation is set absolutely.
    // Rotating by fractional increments accumulated floating-point error over a long run.
    private float targetYaw = 0;
    private float currentYaw = 0;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float fov = 45;
    private float zoomSpeed = 0.5f;
    private Camera gameCamera;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        gameCamera = Camera.main;
        // Camera.main.fieldOfView = 85; // To zoom in on start // MOVED TO MainMenu
    }

    public void resetCameraPosition()
    {
        // A chance's revive may have been under way when the run was left.
        chanceTakenEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        chanceTakenEffect.gameObject.SetActive(false);
        transform.position = startPosition;
        transform.localEulerAngles = new Vector3(0, 0, 0);
        moonsParent.localEulerAngles = new Vector3(0, 0, 0);
        targetYaw = 0;
        currentYaw = 0;
        fov = 45;
    }

    public void PlayChanceTakenEffect()
    {
        chanceTakenEffect.gameObject.SetActive(true);
        chanceTakenEffect.Play();
    }

    // Update is called once per frame
    void Update()
    {
        if (Utility.gameStarted && Utility.camFollowPlayer)
        {
            // The player's Rigidbody is interpolated while it is driven, so this is the smoothed
            // position for this frame.
            transform.position = player.position;
        }

        updateTurn();
        updateZoom();
    }

    private void updateTurn()
    {
        if (currentYaw == targetYaw)
            return;

        float rate = PlayerStats.Instance.isTutorialsOn() ? TUTORIAL_TURN_RATE : TURN_RATE;
        float remaining = targetYaw - currentYaw;
        float delta = Mathf.Abs(remaining) <= TURN_SNAP_DEGREES
            ? remaining
            : remaining * (1f - Mathf.Exp(-rate * Time.deltaTime));

        currentYaw += delta;
        transform.localEulerAngles = new Vector3(0, currentYaw, 0);
        if (PlayerStats.Instance.isRealOrbitOn())
            moonsParent.localEulerAngles = new Vector3(moonsParent.localEulerAngles.x, moonsParent.localEulerAngles.y + delta, moonsParent.localEulerAngles.z);
    }

    private void updateZoom()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;
        if (gameCamera.fieldOfView != fov)
        {
            gameCamera.fieldOfView += (fov - gameCamera.fieldOfView) * (1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
            if (Mathf.Abs(gameCamera.fieldOfView - fov) < 0.1f)
                gameCamera.fieldOfView = fov;
        }
    }

    public void setFov(float fov)
    {
        this.fov = fov;
        zoomSpeed = 1f;
    }

    public void resetFov()
    {
        fov = Utility.Constants.DEFAULT_FOV;
        zoomSpeed = 2;
    }

    public void turnRight()
    {
        targetYaw += 90;
    }

    public void turnLeft()
    {
        targetYaw -= 90;
    }
}
