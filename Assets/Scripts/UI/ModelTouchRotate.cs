using UnityEngine;

public class ModelTouchRotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotateSpeed = 0.2f;
    public float inertiaDamping = 5f;
    public float maxTiltAngle = 25f;
    public float idleRotateSpeed = 8f;
    public float idleStartDelay = 2f;

    private float velocityX;
    private float velocityY;
    private float currentTiltX;

    private bool isDragging;
    private Vector2 lastPointerPosition;
    private float idleTimer;

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif

        ApplyInertia();
        HandleIdleRotation();
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            idleTimer = 0f;
            lastPointerPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 currentPointerPosition = Input.mousePosition;
            Vector2 delta = currentPointerPosition - lastPointerPosition;
            RotateModel(delta);
            lastPointerPosition = currentPointerPosition;
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            isDragging = true;
            idleTimer = 0f;
            lastPointerPosition = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            isDragging = false;
        }
        else if (touch.phase == TouchPhase.Moved && isDragging)
        {
            Vector2 currentPointerPosition = touch.position;
            Vector2 delta = currentPointerPosition - lastPointerPosition;
            RotateModel(delta);
            lastPointerPosition = currentPointerPosition;
        }
    }

    void RotateModel(Vector2 delta)
    {
        idleTimer = 0f;

        velocityX = delta.x * rotateSpeed;
        velocityY = -delta.y * rotateSpeed;

        transform.Rotate(Vector3.up, -velocityX, Space.World);

        currentTiltX += velocityY;
        currentTiltX = Mathf.Clamp(currentTiltX, -maxTiltAngle, maxTiltAngle);

        Vector3 euler = transform.localEulerAngles;

        float normalizedX = euler.x;
        if (normalizedX > 180f)
            normalizedX -= 360f;

        normalizedX = currentTiltX;

        transform.localEulerAngles = new Vector3(
            normalizedX,
            transform.localEulerAngles.y,
            0f
        );
    }

    void ApplyInertia()
    {
        if (isDragging)
            return;

        if (Mathf.Abs(velocityX) > 0.01f)
        {
            transform.Rotate(Vector3.up, -velocityX, Space.World);
            velocityX = Mathf.Lerp(velocityX, 0f, Time.deltaTime * inertiaDamping);
        }

        if (Mathf.Abs(velocityY) > 0.01f)
        {
            currentTiltX += velocityY;
            currentTiltX = Mathf.Clamp(currentTiltX, -maxTiltAngle, maxTiltAngle);

            transform.localEulerAngles = new Vector3(
                currentTiltX,
                transform.localEulerAngles.y,
                0f
            );

            velocityY = Mathf.Lerp(velocityY, 0f, Time.deltaTime * inertiaDamping);
        }
    }

    void HandleIdleRotation()
    {
        if (isDragging)
            return;

        idleTimer += Time.deltaTime;

        if (idleTimer >= idleStartDelay && Mathf.Abs(velocityX) < 0.01f)
        {
            transform.Rotate(Vector3.up, idleRotateSpeed * Time.deltaTime, Space.World);
        }
    }
}