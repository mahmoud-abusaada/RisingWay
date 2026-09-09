using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ListItemAnimator : MonoBehaviour
{
    private Vector3 originalPosition;
    private Transform ball;
    private float rotationSpeed = 30;
    private bool isInPosition = true;

    // Start is called before the first frame update
    void Awake()
    {
        ball = transform.GetChild(0);
    }

    public void resetPosition()
    {
        // ball.GetComponent<Rigidbody>().isKinematic = true;
        ball.localPosition = new Vector3(0, 0.568f, 0);
        ball.rotation = Quaternion.identity;
        ball.Rotate(new Vector3(0, 0, 15));
        ball.localPosition = new Vector3(ball.localPosition.x - 1f, ball.localPosition.y, ball.localPosition.z);
        ball.GetComponent<Rigidbody>().isKinematic = false;
        isInPosition = false;
    }

    float distance;
    // Update is called once per frame
    void FixedUpdate()
    {

        if (!isInPosition)
        {
            if (originalPosition.x - ball.localPosition.x > 0.01f)
            {
                distance = Mathf.Abs(ball.localPosition.x - originalPosition.x);
                distance = distance > 0.1f ? distance : 0.1f;
                ball.GetComponent<Rigidbody>().velocity = new Vector3(distance, 0, 0);
            }
            else
            {
                ball.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
                ball.GetComponent<Rigidbody>().isKinematic = true;
                ball.localPosition = new Vector3(originalPosition.x, ball.localPosition.y, ball.localPosition.z);
                isInPosition = true;
                rotationSpeed = 30;
                // Debug.Log("in position");
            }
            // if (Mathf.Abs(ball.position.x - originalX) > 0.01f)
            // {
            //     float distance = Mathf.Abs(ball.position.x - originalX);
            //     distance = distance > 0.15f ? distance : 0.15f;
            //     ball.position = new Vector3(ball.position.x + Time.deltaTime * distance * 3, ball.position.y, ball.position.z);
            // }
            // else
            // {
            //     ball.position = new Vector3(originalX, ball.position.y, ball.position.z);
            //     isInPosition = true;
            //     rotationSpeed = 30;
            //     Debug.Log("in position");
            // }
        }
        else
        {
            ball.Rotate(new Vector3(0, Time.deltaTime * -rotationSpeed, 0));
        }
    }
}
