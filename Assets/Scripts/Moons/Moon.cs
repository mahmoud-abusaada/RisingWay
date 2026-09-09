using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Internal;

public class Moon : MonoBehaviour
{
    public Material material = null;
    public float speed;
    public float distance;
    public float scale;
    public Vector3 rotation = Vector3.zero;

    public Moon(float speed, float distance, float scale, Material material = null, Vector3? rotation = null)
    {
        this.material = material;
        this.speed = speed;
        this.distance = distance;
        this.scale = scale;
        if (rotation != null)
            this.rotation = (Vector3)rotation;
    }
}
