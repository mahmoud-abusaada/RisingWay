using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToPosition : MonoBehaviour
{
    private Vector3 startPoint, endPoint;
    private float timeElapsed = 0;
    private Action action = null;
    private float duration;
    private bool smoothly;
    public bool isMoving = false;

    public void MoveTransform(Vector3 startPoint, Vector3 endPoint, float duration, bool smoothly, Action action = null)
    {
        this.startPoint = startPoint;
        this.endPoint = endPoint;
        this.duration = duration;
        this.smoothly = smoothly;
        this.action = action;
        isMoving = true;
    }

    /// <summary>Stops a move without calling its action: the run it belonged to is over.</summary>
    public void Cancel()
    {
        isMoving = false;
        action = null;
        timeElapsed = 0;
        velocity = Vector3.zero;
    }

    public float smoothTime = 100f;
    private Vector3 velocity = Vector3.zero;
    void FixedUpdate()
    {
        if (isMoving)
        {
            if (Vector3.Distance(transform.position, endPoint) > 0.01f)
            {
                if (smoothly)
                {
                    float t = timeElapsed / duration;
                    t = t * t * (3f - 2f * t);
                    transform.position = Vector3.Lerp(startPoint, endPoint, t);
                }
                else
                {
                    float value = timeElapsed / duration;
                    // if(value >= )
                    // transform.position = Vector3.Lerp(transform.position, endPoint, timeElapsed / duration);
                    transform.position = Vector3.SmoothDamp(transform.position, endPoint, ref velocity, 0.3f);
                    if (Vector3.Distance(transform.position, endPoint) < 0.05f)
                        endPoint = transform.position;
                }
                timeElapsed += Time.deltaTime;
            }
            else
            {
                timeElapsed = 0;
                isMoving = false;
                action?.Invoke();
            }
        }
    }
}
