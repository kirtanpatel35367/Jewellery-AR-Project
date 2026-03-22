using UnityEngine;

public class ModelViewerControls : MonoBehaviour
{
    public float rotationSpeed = 0.2f;
    public float zoomSpeed = 0.005f;

    public float minScale = 0.2f;
    public float maxScale = 2.0f;

    private float currentScale = 1f;

    void Update()
    {
        // ROTATE MODEL
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                float rotX = -touch.deltaPosition.x * rotationSpeed;
                transform.Rotate(Vector3.up, rotX, Space.World);
            }
        }

        // ZOOM MODEL
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prevDist = (t0.position - t0.deltaPosition - (t1.position - t1.deltaPosition)).magnitude;
            float currentDist = (t0.position - t1.position).magnitude;

            float diff = currentDist - prevDist;

            currentScale += diff * zoomSpeed;

            currentScale = Mathf.Clamp(currentScale, minScale, maxScale);

            transform.localScale = Vector3.one * currentScale;
        }
    }
}