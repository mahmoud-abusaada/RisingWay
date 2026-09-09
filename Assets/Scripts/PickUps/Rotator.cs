using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] private bool lookAtCamera = false;
    public Vector3 lookingAt;

    void Start()
    {
        // if (lookAtCamera)
        //     transform.LookAt(new Vector3((Camera.main.transform.position.x + (transform.position.x - Camera.main.transform.position.x)), (Camera.main.transform.position.y + (transform.position.y - Camera.main.transform.position.y) / 1.75f), Camera.main.transform.position.z));

        // if (lookAtCamera)
        // {
        //     lookingAt = new Vector3(transform.position.x, Camera.main.transform.position.y, Camera.main.transform.position.z);
        //     transform.LookAt(lookingAt);
        // }
    }

    void FixedUpdate()
    {
        transform.Rotate(new Vector3(0, Time.unscaledDeltaTime * 150, 0));
    }
}
