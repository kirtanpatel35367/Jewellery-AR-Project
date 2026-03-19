using UnityEngine;
using UnityEngine.UI;

public class InteractivePreviewItem : MonoBehaviour
{
    [Header("Rotation")]
    public float autoRotateSpeed = 20f;
    public float dragRotateSpeedX = 0.25f;
    public float dragRotateSpeedY = 0.15f;
    public float minTiltX = -25f;
    public float maxTiltX = 25f;

    [Header("Zoom")]
    public Camera previewCamera;
    public float zoomSpeedTouch = 0.01f;
    public float zoomSpeedMouse = 0.6f;
    public float minCameraLocalZ = -4.5f;
    public float maxCameraLocalZ = -1.2f;
    public float defaultCameraLocalZ = -2.5f;
    public float zoomReturnSpeed = 4f;

    [Header("UI Area")]
    public RectTransform targetUIArea;
    public Canvas canvas;

    private bool isDraggingThis = false;
    private bool isZoomingThis = false;

    private Vector2 lastPointerPos;
    private float currentTiltX = 0f;

    void Start()
    {
        if (previewCamera != null)
        {
            Vector3 localPos = previewCamera.transform.localPosition;
            localPos.z = defaultCameraLocalZ;
            previewCamera.transform.localPosition = localPos;
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        HandleMouse();
#else
        HandleTouch();
#endif

        if (!isDraggingThis && !isZoomingThis)
        {
            transform.Rotate(0f, autoRotateSpeed * Time.deltaTime, 0f, Space.Self);
        }

        ReturnZoomToDefault();
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0)
        {
            isDraggingThis = false;
            isZoomingThis = false;
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 screenPoint = touch.position;

            if (touch.phase == TouchPhase.Began)
            {
                if (IsInsideTarget(screenPoint))
                {
                    isDraggingThis = true;
                    lastPointerPos = screenPoint;
                }
            }
            else if (touch.phase == TouchPhase.Moved && isDraggingThis)
            {
                Vector2 delta = screenPoint - lastPointerPos;
                RotatePreview(delta);
                lastPointerPos = screenPoint;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDraggingThis = false;
                isZoomingThis = false;
            }
        }
        else if (Input.touchCount == 2 && previewCamera != null)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            bool bothTouchesInside =
                IsInsideTarget(touch0.position) && IsInsideTarget(touch1.position);

            if (!bothTouchesInside)
            {
                isZoomingThis = false;
                return;
            }

            isZoomingThis = true;
            isDraggingThis = false;

            Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

            float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
            float currentMagnitude = (touch0.position - touch1.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;
            ZoomCamera(difference * zoomSpeedTouch);

            if (touch0.phase == TouchPhase.Ended || touch0.phase == TouchPhase.Canceled ||
                touch1.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Canceled)
            {
                isZoomingThis = false;
            }
        }
        else
        {
            isDraggingThis = false;
            isZoomingThis = false;
        }
    }

    void HandleMouse()
    {
        Vector2 screenPoint = Input.mousePosition;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsInsideTarget(screenPoint))
            {
                isDraggingThis = true;
                lastPointerPos = screenPoint;
            }
        }
        else if (Input.GetMouseButton(0) && isDraggingThis)
        {
            Vector2 delta = screenPoint - lastPointerPos;
            RotatePreview(delta);
            lastPointerPos = screenPoint;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDraggingThis = false;
            isZoomingThis = false;
        }

        if (IsInsideTarget(screenPoint) && previewCamera != null)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                isZoomingThis = true;
                ZoomCamera(scroll * zoomSpeedMouse);
            }
            else if (!Input.GetMouseButton(0))
            {
                isZoomingThis = false;
            }
        }
    }

    void RotatePreview(Vector2 delta)
    {
        transform.Rotate(0f, -delta.x * dragRotateSpeedX, 0f, Space.World);

        currentTiltX += delta.y * dragRotateSpeedY;
        currentTiltX = Mathf.Clamp(currentTiltX, minTiltX, maxTiltX);

        Vector3 currentEuler = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(currentTiltX, currentEuler.y, 0f);
    }

    void ZoomCamera(float zoomDelta)
    {
        if (previewCamera == null) return;

        Vector3 localPos = previewCamera.transform.localPosition;
        localPos.z += zoomDelta;
        localPos.z = Mathf.Clamp(localPos.z, minCameraLocalZ, maxCameraLocalZ);
        previewCamera.transform.localPosition = localPos;
    }

    void ReturnZoomToDefault()
    {
        if (previewCamera == null) return;
        if (isDraggingThis || isZoomingThis) return;

        Vector3 localPos = previewCamera.transform.localPosition;
        localPos.z = Mathf.Lerp(localPos.z, defaultCameraLocalZ, Time.deltaTime * zoomReturnSpeed);
        previewCamera.transform.localPosition = localPos;
    }

    bool IsInsideTarget(Vector2 screenPoint)
    {
        if (targetUIArea == null) return false;

        Camera uiCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(targetUIArea, screenPoint, uiCamera);
    }
}