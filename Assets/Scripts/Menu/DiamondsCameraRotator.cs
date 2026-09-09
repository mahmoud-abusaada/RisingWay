using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiamondsCameraRotator : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(new Vector3(0, Time.deltaTime * 5, 0));
    }
}
