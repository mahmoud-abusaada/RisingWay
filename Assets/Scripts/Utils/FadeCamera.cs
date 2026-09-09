using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadeCamera : MonoBehaviour
{
    void Awake()
    {
        if (GetComponent<Renderer>() != null)
        {
            Material material = GetComponent<Renderer>().material;
            material.renderQueue = 2450;
        }
    }
}