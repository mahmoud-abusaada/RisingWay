using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class CameraController : MonoBehaviour
{

    [SerializeField] private Transform player;
    [SerializeField] private Transform moonsParent;
    [SerializeField] private ParticleSystem chanceTakenEffect;

    private float degreesToRotate = 0;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float fov = 45;
    private float zoomSpeed = 0.5f;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        // Camera.main.fieldOfView = 85; // To zoom in on start // MOVED TO MainMenu
    }

    public void resetCameraPosition()
    {
        transform.position = startPosition;
        transform.localEulerAngles = new Vector3(0, 0, 0);
        moonsParent.localEulerAngles = new Vector3(0, 0, 0);
        degreesToRotate = 0;
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
            transform.position = player.position;
        }
    }

    float rotationValue;
    void FixedUpdate()
    {
        if (degreesToRotate != 0)
        {
            rotationValue = degreesToRotate * Time.deltaTime * (PlayerStats.Instance.isTutorialsOn() ? 5 : 10);
            transform.Rotate(new Vector3(0, rotationValue, 0));
            if (PlayerStats.Instance.isRealOrbitOn())
                moonsParent.localEulerAngles = new Vector3(moonsParent.localEulerAngles.x, moonsParent.localEulerAngles.y + rotationValue, moonsParent.localEulerAngles.z);
            degreesToRotate -= rotationValue;
        }

        if (Camera.main.fieldOfView != fov)
        {
            Camera.main.fieldOfView += (fov - Camera.main.fieldOfView) * Time.deltaTime * zoomSpeed;
            if (Mathf.Abs(Camera.main.fieldOfView - fov) < 0.1f)
                Camera.main.fieldOfView = fov;
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
        degreesToRotate += 90;
    }

    public void turnLeft()
    {
        degreesToRotate -= 90;
    }
}
