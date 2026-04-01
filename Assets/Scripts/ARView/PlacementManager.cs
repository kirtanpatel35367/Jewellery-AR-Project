using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlacementManager — fully fixed version.
///
/// FIXES:
///   1. Jewelry placed ON the plane (not sinking) — Y lifted by bounds bottom offset.
///   2. Any placed item selectable by tap — closest-hit raycast across all items.
///   3. Rotate works correctly for every item — angle measured from ITEM screen centre.
///   4. Pinch zoom works on whichever item is selected.
///   5. Plane glows gold when detected — visible soft fill + gold outline.
///   6. ARRaycastManager / ARPlaneManager found scene-wide if not on same GameObject.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    // ── AR ─────────────────────────────────────────────────────────────
    private ARRaycastManager _raycastManager;
    private ARPlaneManager _planeManager;
    private static readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

    // ── Active prefab (armed from menu) ───────────────────────────────
    private GameObject _activePrefab;
    private float _activeDefaultScale = 1f;
    private string _activeItemName = "";
    private Sprite _activeThumb = null;

    // ── Placed items ───────────────────────────────────────────────────
    private readonly List<PlacedItem> _placedItems = new List<PlacedItem>();
    private PlacedItem _selectedItem;

    // ── Touch state ────────────────────────────────────────────────────
    private bool _isDragging = false;
    private bool _longPressArmed = false;
    private float _touchDownTime = 0f;
    private Vector2 _touchStartPos;

    // Rotation — measured from item's screen-space centre, not screen centre
    private float _rotateStartAngle = 0f;
    private float _itemStartYRot = 0f;

    // Pinch zoom
    private float _lastPinchDist = 0f;

    // Double-tap deselect
    private float _lastTapTime = 0f;
    private const float DOUBLE_TAP = 0.35f;

    // ── Selection ring ─────────────────────────────────────────────────
    private GameObject _ring;
    private static readonly Color RING_COL = new Color(0.95f, 0.80f, 0.25f, 0.85f);

    // ── Plane glow ─────────────────────────────────────────────────────
    private Material _planeMat;
    private static readonly Color PLANE_FILL_COL = new Color(0.95f, 0.80f, 0.20f, 0.18f);
    private static readonly Color PLANE_LINE_COL = new Color(0.95f, 0.80f, 0.20f, 0.80f);

    // ── Inspector ──────────────────────────────────────────────────────
    [Header("Interaction")]
    public float longPressDuration = 1.5f;
    public float dragThresholdPx = 14f;
    public float minScaleFactor = 0.2f;
    public float maxScaleFactor = 5.0f;

    [Header("Plane Glow")]
    [Tooltip("Opacity of the gold fill on detected planes (0=off, 0.25=soft glow)")]
    [Range(0f, 0.5f)]
    public float planeFillAlpha = 0.18f;

    // ── Events ─────────────────────────────────────────────────────────
    public System.Action<PlacedItem> OnItemSelected;
    public System.Action<PlacedItem> OnItemRemoved;
    public System.Action OnItemPlaced;

    // ── Data class ─────────────────────────────────────────────────────
    public class PlacedItem
    {
        public GameObject go;
        public Vector3 originalScale;
        public string itemName;
        public Sprite thumbnail;
    }

    // ══════════════════════════════════════════════════════════════════
    //  INIT
    // ══════════════════════════════════════════════════════════════════
    void Awake()
    {
        // Same-GameObject first; scene-wide fallback so any hierarchy works.
        _raycastManager = GetComponent<ARRaycastManager>()
                       ?? FindObjectOfType<ARRaycastManager>();
        _planeManager = GetComponent<ARPlaneManager>()
                       ?? FindObjectOfType<ARPlaneManager>();

        if (_raycastManager == null)
            Debug.LogError("[PlacementManager] ARRaycastManager not found! Add it to XR Origin.");

        _planeMat = MakePlaneMaterial();
        BuildRing();
    }

    void Start()
    {
        if (_planeManager != null)
            _planeManager.planesChanged += OnPlanesChanged;

        ApplyPlaneVisuals();
        StartCoroutine(CanvasCleanupLoop());
    }

    void OnDestroy()
    {
        if (_planeManager != null)
            _planeManager.planesChanged -= OnPlanesChanged;
    }

    // ══════════════════════════════════════════════════════════════════
    //  UPDATE
    // ══════════════════════════════════════════════════════════════════
    void Update()
    {
        if (Input.touchCount == 0)
        {
            _longPressArmed = false;
            _isDragging = false;
            return;
        }

        if (IsOverUI(Input.GetTouch(0).position)) return;

        // ── Two-finger pinch zoom ───────────────────────────────────────
        if (Input.touchCount == 2)
        {
            DoPinchZoom();
            _longPressArmed = false;
            return;
        }

        Touch t = Input.GetTouch(0);

        // ── Began ───────────────────────────────────────────────────────
        if (t.phase == TouchPhase.Began)
        {
            _touchStartPos = t.position;
            _touchDownTime = Time.time;
            _longPressArmed = true;
            _isDragging = false;

            PlacedItem hit = RaycastItems(t.position);
            if (hit != null)
            {
                DoSelect(hit);
                // Measure rotation angle from the ITEM's screen-space centre.
                _rotateStartAngle = TouchAngleAroundItem(t.position, hit);
                _itemStartYRot = hit.go.transform.eulerAngles.y;
            }
        }

        // ── Moved ────────────────────────────────────────────────────────
        if (t.phase == TouchPhase.Moved)
        {
            if ((t.position - _touchStartPos).magnitude > dragThresholdPx)
            {
                _isDragging = true;
                _longPressArmed = false;
            }

            // FIX: rotate the selected item based on angle delta from its
            // own projected screen centre — works correctly for every item.
            if (_isDragging && _selectedItem != null && _selectedItem.go != null)
            {
                float currentAngle = TouchAngleAroundItem(t.position, _selectedItem);
                float delta = currentAngle - _rotateStartAngle;
                Vector3 e = _selectedItem.go.transform.eulerAngles;
                _selectedItem.go.transform.eulerAngles =
                    new Vector3(e.x, _itemStartYRot - delta, e.z);
            }
        }

        // ── Long press → remove ──────────────────────────────────────────
        if (_longPressArmed && !_isDragging && _selectedItem != null)
        {
            if (Time.time - _touchDownTime >= longPressDuration)
            {
                DoRemove(_selectedItem);
                _longPressArmed = false;
                return;
            }
        }

        // ── Ended ────────────────────────────────────────────────────────
        if (t.phase == TouchPhase.Ended && !_isDragging)
        {
            PlacedItem tapped = RaycastItems(t.position);

            if (tapped != null)
            {
                if (Time.time - _lastTapTime < DOUBLE_TAP && _selectedItem == tapped)
                {
                    DoDeselect();
                }
                else
                {
                    DoSelect(tapped);
                    _rotateStartAngle = TouchAngleAroundItem(t.position, tapped);
                    _itemStartYRot = tapped.go.transform.eulerAngles.y;
                }
                _lastTapTime = Time.time;
                return;
            }

            // Tapped empty space: deselect; next tap places
            if (_selectedItem != null) { DoDeselect(); return; }

            // Place new item on plane
            if (_activePrefab == null)
            {
                Debug.Log("[PlacementManager] No prefab armed — tap a jewelry card first.");
                return;
            }

            if (_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon))
                DoSpawn(_hits[0].pose.position, _hits[0].pose.rotation);
            else
                Debug.Log("[PlacementManager] No plane hit — aim at the gold outline and tap.");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════
    public void SetActivePrefab(GameObject prefab, string itemName = "",
                                Sprite thumbnail = null, float defaultScale = 1f)
    {
        _activePrefab = prefab;
        _activeDefaultScale = defaultScale;
        _activeItemName = itemName;
        _activeThumb = thumbnail;
        Debug.Log("[PlacementManager] Armed: " + (prefab != null ? prefab.name : "NULL"));
    }

    public void RemoveItem(PlacedItem item) => DoRemove(item);
    public void RemoveSelected() { if (_selectedItem != null) DoRemove(_selectedItem); }
    public void RemoveAll()
    {
        foreach (var item in _placedItems)
            if (item.go != null) Destroy(item.go);
        _placedItems.Clear();
        _selectedItem = null;
        HideRing();
    }

    public bool HasActivePrefab => _activePrefab != null;
    public IReadOnlyList<PlacedItem> GetPlacedItems() => _placedItems;

    // ══════════════════════════════════════════════════════════════════
    //  SPAWN
    // ══════════════════════════════════════════════════════════════════
    void DoSpawn(Vector3 planePosition, Quaternion planeRotation)
    {
        var go = Instantiate(_activePrefab, planePosition, planeRotation);

        if (!Mathf.Approximately(_activeDefaultScale, 1f))
            go.transform.localScale *= _activeDefaultScale;

        // FIX: lift the object so its bottom mesh surface sits ON the plane.
        // Without this the pivot (usually at mesh centre) lands on the plane
        // and the bottom half sinks below it.
        float yOffset = GetBottomOffset(go);
        go.transform.position = planePosition + new Vector3(0f, yOffset, 0f);

        AddColliders(go);

        var placed = new PlacedItem
        {
            go = go,
            originalScale = go.transform.localScale,
            itemName = _activeItemName,
            thumbnail = _activeThumb
        };
        _placedItems.Add(placed);
        DoSelect(placed);
        OnItemPlaced?.Invoke();
        Debug.Log("[PlacementManager] Placed: " + _activePrefab.name +
                  " at " + go.transform.position + "  yOffset=" + yOffset.ToString("F4"));
    }

    /// <summary>
    /// Returns the Y distance from the spawn position to the bottom of the
    /// mesh bounds, so the object sits fully above the plane.
    /// </summary>
    float GetBottomOffset(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;

        float minY = float.MaxValue;
        foreach (var r in renderers)
            minY = Mathf.Min(minY, r.bounds.min.y);

        // gap = how far the bottom of the mesh is below the pivot
        return go.transform.position.y - minY;
    }

    // ══════════════════════════════════════════════════════════════════
    //  SELECT / DESELECT / REMOVE
    // ══════════════════════════════════════════════════════════════════
    void DoSelect(PlacedItem item)
    {
        _selectedItem = item;
        ShowRing(item);
        OnItemSelected?.Invoke(item);
    }

    void DoDeselect()
    {
        _selectedItem = null;
        HideRing();
    }

    void DoRemove(PlacedItem item)
    {
        if (item == null) return;
        if (item.go != null) Destroy(item.go);
        _placedItems.Remove(item);
        if (_selectedItem == item) { _selectedItem = null; HideRing(); }
        OnItemRemoved?.Invoke(item);
    }

    // ══════════════════════════════════════════════════════════════════
    //  COLLIDERS
    // ══════════════════════════════════════════════════════════════════
    void AddColliders(GameObject root)
    {
        if (root.GetComponentsInChildren<Collider>(true).Length > 0) return;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.GetComponent<Collider>() != null) continue;
            var mf = r.gameObject.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
            else
            {
                r.gameObject.AddComponent<BoxCollider>();
            }
        }

        if (root.GetComponentsInChildren<Collider>(true).Length == 0)
            root.AddComponent<BoxCollider>();
    }

    // ══════════════════════════════════════════════════════════════════
    //  SELECTION RING
    // ══════════════════════════════════════════════════════════════════
    void BuildRing()
    {
        _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _ring.name = "SelectionRing";
        Destroy(_ring.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) { Debug.LogWarning("[PlacementManager] Shader not found for ring."); return; }

        var mat = new Material(shader) { color = RING_COL };
        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_ALPHABLEND_ON");

        var rend = _ring.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        _ring.transform.localScale = new Vector3(0.35f, 0.005f, 0.35f);
        _ring.SetActive(false);
    }

    void ShowRing(PlacedItem item)
    {
        if (_ring == null || item?.go == null) return;
        var renderers = item.go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float radius = Mathf.Max(b.extents.x, b.extents.z) * 1.4f;
        _ring.transform.position = new Vector3(b.center.x, b.min.y + 0.003f, b.center.z);
        _ring.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
        _ring.SetActive(true);
    }

    void HideRing() { if (_ring != null) _ring.SetActive(false); }

    // ══════════════════════════════════════════════════════════════════
    //  PINCH ZOOM
    // ══════════════════════════════════════════════════════════════════
    void DoPinchZoom()
    {
        if (_selectedItem == null || _selectedItem.go == null) return;

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        float dist = Vector2.Distance(t0.position, t1.position);

        if (t1.phase == TouchPhase.Began) { _lastPinchDist = dist; return; }

        float delta = dist - _lastPinchDist;
        _lastPinchDist = dist;

        Vector3 orig = _selectedItem.originalScale;
        float curFactor = _selectedItem.go.transform.localScale.x / orig.x;
        float newFactor = Mathf.Clamp(curFactor + delta * 0.001f, minScaleFactor, maxScaleFactor);

        _selectedItem.go.transform.localScale = orig * newFactor;
        ShowRing(_selectedItem);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PLANE VISUALS — gold glow on detected planes
    // ══════════════════════════════════════════════════════════════════
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        foreach (var p in args.added) ApplyPlaneGlow(p);
        foreach (var p in args.updated) ApplyPlaneGlow(p);
    }

    void ApplyPlaneVisuals()
    {
        if (_planeManager == null) return;
        foreach (var p in _planeManager.trackables) ApplyPlaneGlow(p);
    }

    void ApplyPlaneGlow(ARPlane plane)
    {
        if (plane == null) return;

        // FIX: show a soft gold transparent fill so the user can see
        // the detected surface area clearly.
        foreach (var mr in plane.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = true;
            if (_planeMat != null) mr.material = _planeMat;
        }

        // Gold outline
        foreach (var lr in plane.GetComponentsInChildren<LineRenderer>(true))
        {
            lr.enabled = true;
            lr.startColor = PLANE_LINE_COL;
            lr.endColor = PLANE_LINE_COL;
        }
    }

    Material MakePlaneMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        if (shader == null) return null;

        Color fill = new Color(PLANE_FILL_COL.r, PLANE_FILL_COL.g,
                               PLANE_FILL_COL.b, planeFillAlpha);
        var m = new Material(shader) { color = fill };
        m.SetFloat("_Surface", 1);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.SetFloat("_Mode", 3);
        m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_ALPHABLEND_ON");
        return m;
    }

    // ══════════════════════════════════════════════════════════════════
    //  CANVAS CLEANUP
    // ══════════════════════════════════════════════════════════════════
    IEnumerator CanvasCleanupLoop()
    {
        yield return new WaitForSeconds(0.5f);
        CleanCanvases();
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(2f);
            CleanCanvases();
        }
    }

    void CleanCanvases()
    {
        foreach (var canvas in FindObjectsOfType<Canvas>(true))
        {
            var gr = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (gr == null) continue;
            int count = 0;
            foreach (var s in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                if (s.gameObject.activeInHierarchy && s.interactable) count++;
            if (count == 0)
            {
                Destroy(gr);
                Debug.Log("[PlacementManager] Removed blocker raycaster: " + canvas.name);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Iterate every collider on every placed item and return the closest one
    /// hit by the screen-space ray.  Checks colliders directly instead of
    /// relying on Physics.Raycast + IsChildOf, so root-level and multi-mesh
    /// prefabs are both handled correctly.
    /// </summary>
    PlacedItem RaycastItems(Vector2 screenPos)
    {
        if (Camera.main == null) return null;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        float bestDist = float.MaxValue;
        PlacedItem bestItem = null;

        foreach (var item in _placedItems)
        {
            if (item.go == null) continue;
            foreach (var col in item.go.GetComponentsInChildren<Collider>(true))
            {
                if (col.Raycast(ray, out RaycastHit h, 100f) && h.distance < bestDist)
                {
                    bestDist = h.distance;
                    bestItem = item;
                }
            }
        }
        return bestItem;
    }

    /// <summary>
    /// Angle from the item's projected screen-space position to the touch.
    /// Using item centre (not screen centre) makes rotation feel natural
    /// for every item regardless of where it sits on screen.
    /// </summary>
    float TouchAngleAroundItem(Vector2 touchPos, PlacedItem item)
    {
        if (item?.go == null || Camera.main == null) return 0f;
        Vector3 sc = Camera.main.WorldToScreenPoint(item.go.transform.position);
        Vector2 dir = touchPos - new Vector2(sc.x, sc.y);
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    static bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        foreach (var r in results)
        {
            var go = r.gameObject;
            if (go.GetComponentInParent<UnityEngine.UI.Button>() != null) return true;
            if (go.GetComponentInParent<UnityEngine.UI.ScrollRect>() != null) return true;
            if (go.GetComponentInParent<UnityEngine.UI.Slider>() != null) return true;
            if (go.GetComponentInParent<UnityEngine.UI.Toggle>() != null) return true;
            if (go.GetComponentInParent<UnityEngine.UI.InputField>() != null) return true;
            if (go.name == "Overlay") return true;
        }
        return false;
    }
}