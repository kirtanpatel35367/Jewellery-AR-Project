using UnityEngine;

/// <summary>
/// JEWELRY 360 VIEW MANAGER
///
/// Attach to an empty GameObject in the 360 View scene.
/// Handles:
///   - Receiving the selected JewelryItem from MainMenuManager (via static ref)
///   - Instantiating the jewelry prefab on a display pivot
///   - Touch controls: one-finger rotate (all axes), two-finger pinch-to-zoom
///   - Auto-spin when idle
/// </summary>
public class JewelryView360Manager : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Empty GO at scene centre — jewelry prefab is spawned here")]
    public Transform displayPivot;

    [Tooltip("Camera looking at the pivot")]
    public Camera viewCamera;

    [Header("Rotation Settings")]
    public float rotationSensitivity = 0.35f;
    [Tooltip("Degrees per second auto-spin on Y when no touch")]
    public float autoSpinSpeed = 18f;

    [Header("Normalization")]
    [Tooltip("Every prefab is scaled so its longest axis equals this world-unit size")]
    public float normalizedSize = 0.5f;

    [Header("Zoom Settings")]
    public float zoomSensitivity = 0.02f;
    [Tooltip("Closest the camera can get — relative to normalizedSize")]
    public float minZoomDistance = 0.4f;
    [Tooltip("Furthest the camera can get")]
    public float maxZoomDistance = 3.0f;
    [Tooltip("Starting camera distance after a model is loaded")]
    public float defaultZoomDistance = 1.2f;

    [Header("Lighting")]
    [Tooltip("Optional directional light to set colour matching the UI gold theme")]
    public Light mainLight;

    // ── private state ────────────────────────────────────────────────
    private GameObject spawnedModel;
    private float currentZoom;
    private Vector3 lastTouchPos;
    private bool isDragging;
    private float idleTimer;
    private const float IDLE_SPIN_DELAY = 2.5f;

    // ════════════════════════════════════════════════════════════════
    void Start()
    {
        // Defaults
        currentZoom = defaultZoomDistance;
        UpdateCameraPosition();

        // Optionally tint light
        if (mainLight != null)
            mainLight.color = new Color(1.00f, 0.97f, 0.82f);

        // Spawn the item passed from main menu
        SpawnSelectedItem();
    }

    // ════════════════════════════════════════════════════════════════
    void SpawnSelectedItem()
    {
        // Clear old model
        if (spawnedModel != null) Destroy(spawnedModel);

        JewelryItem item = JewelrySelectionBridge.SelectedItem;
        if (item == null || item.jewelryPrefab == null)
        {
            Debug.LogWarning("[360View] No item selected or prefab is null.");
            return;
        }

        spawnedModel = Instantiate(item.jewelryPrefab, displayPivot);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;
        spawnedModel.transform.localScale = Vector3.one;

        // Centre the model at the pivot using its bounds
        CentreModel(spawnedModel);

        Debug.Log($"[360View] Spawned: {item.itemName}");
    }

    void CentreModel(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // ── Step 1: measure raw bounds at scale = 1 ──────────────
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // ── Step 2: normalise scale so the longest axis == normalizedSize ──
        // bounds.size is in world space, so we derive the scale factor
        float longestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longestAxis > 0.0001f)
        {
            float scaleFactor = normalizedSize / longestAxis;
            go.transform.localScale = go.transform.localScale * scaleFactor;
        }

        // ── Step 3: re-measure bounds after rescale ───────────────
        bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // ── Step 4: centre the model at the pivot origin ──────────
        Vector3 offset = displayPivot.position - bounds.center;
        go.transform.position += offset;

        // ── Step 5: set camera to the fixed default distance ──────
        // (same for every model — zoom in/out from here)
        currentZoom = defaultZoomDistance;
        UpdateCameraPosition();
    }

    // ════════════════════════════════════════════════════════════════
    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        int touchCount = Input.touchCount;

        // ── Two fingers: pinch zoom ───────────────────────────────
        if (touchCount == 2)
        {
            idleTimer = 0f;
            isDragging = false;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            Vector2 prevT0 = t0.position - t0.deltaPosition;
            Vector2 prevT1 = t1.position - t1.deltaPosition;

            float prevDist = Vector2.Distance(prevT0, prevT1);
            float curDist = Vector2.Distance(t0.position, t1.position);
            float delta = curDist - prevDist;

            currentZoom -= delta * zoomSensitivity;
            currentZoom = Mathf.Clamp(currentZoom, minZoomDistance, maxZoomDistance);
            UpdateCameraPosition();
        }
        // ── One finger: rotate ────────────────────────────────────
        else if (touchCount == 1)
        {
            idleTimer = 0f;
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                lastTouchPos = t.position;
                isDragging = true;
            }
            else if (t.phase == TouchPhase.Moved && isDragging)
            {
                Vector2 delta = t.position - (Vector2)lastTouchPos;
                lastTouchPos = t.position;

                // Horizontal swipe → Y-axis rotation
                displayPivot.Rotate(Vector3.up, -delta.x * rotationSensitivity, Space.World);
                // Vertical swipe → X-axis rotation (clamp tilt)
                displayPivot.Rotate(Vector3.right, delta.y * rotationSensitivity, Space.World);
            }
            else if (t.phase == TouchPhase.Ended)
            {
                isDragging = false;
            }
        }
        // ── Editor/mouse fallback ─────────────────────────────────
#if UNITY_EDITOR
        else if (Input.GetMouseButton(0))
        {
            idleTimer = 0f;
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            displayPivot.Rotate(Vector3.up, -mx * rotationSensitivity * 60f, Space.World);
            displayPivot.Rotate(Vector3.right, my * rotationSensitivity * 60f, Space.World);
        }
        else
        {
            // Mouse scroll zoom in editor
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                currentZoom -= scroll * 1.2f;
                currentZoom = Mathf.Clamp(currentZoom, minZoomDistance, maxZoomDistance);
                UpdateCameraPosition();
            }
            AutoSpin();
        }
#else
        else
        {
            isDragging = false;
            AutoSpin();
        }
#endif
    }

    void AutoSpin()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= IDLE_SPIN_DELAY)
            displayPivot.Rotate(Vector3.up, autoSpinSpeed * Time.deltaTime, Space.World);
    }

    void UpdateCameraPosition()
    {
        if (viewCamera == null) return;
        viewCamera.transform.position = displayPivot.position - viewCamera.transform.forward * currentZoom;
    }

    // ── Public: called by UI when user picks a different item ────
    public void RefreshModel()
    {
        SpawnSelectedItem();
    }
}