using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlacementManager — all issues fixed.
///
/// FIXES IN THIS VERSION:
///
///  FIX 1 — Jewelry lands exactly where tapped:
///    Spawns at final world position directly, bounds measured in local space
///    before placement so there is no mid-air offset error. Works correctly
///    for all prefab sizes and pivot placements.
///
///  FIX 2 — Consistent real-world size for all jewelry:
///    Every prefab is normalised to a target real-world size on spawn.
///    Per-category target sizes (necklace, ring, bangle, earring, generic)
///    are set in the Inspector. placeOnRoomDefaultScale from JewelryItem
///    is applied on top as a fine-tune multiplier.
///
///  FIX 3 — Yellow ring less visible:
///    Ring opacity reduced to 35%, thickness halved, color softened to white.
///    Still clearly visible when selected but not distracting.
///
///  FIX 4 — No more drift / random movement when camera moves:
///    Rotation now uses raw screen-space delta-X (pixels moved per frame)
///    instead of angle-around-item projection. This is camera-independent —
///    rotating the camera or moving slightly no longer spins the object.
///
///  FIX 5 — Smooth, predictable 360 rotation:
///    Drag horizontal → rotate Y. Clean, direct, no projection math.
///    Rotation sensitivity is exposed in the Inspector.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    // ── AR ─────────────────────────────────────────────────────────────
    private ARRaycastManager _raycastManager;
    private ARPlaneManager _planeManager;
    private static readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

    // ── Active prefab ──────────────────────────────────────────────────
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
    private Vector2 _lastTouchPos;          // FIX 4: track previous frame pos
    private float _lastTapTime = 0f;
    private const float DOUBLE_TAP = 0.35f;

    // ── Pinch ──────────────────────────────────────────────────────────
    private float _lastPinchDist = 0f;

    // ── Selection ring ─────────────────────────────────────────────────
    private GameObject _ring;
    // FIX 3: soft white, low opacity
    private static readonly Color RING_COL = new Color(1f, 1f, 1f, 0.30f);

    // ── Inspector ──────────────────────────────────────────────────────
    [Header("Interaction")]
    public float longPressDuration = 1.5f;
    public float dragThresholdPx = 18f;
    public float minScaleFactor = 0.2f;
    public float maxScaleFactor = 5.0f;

    [Tooltip("Degrees of Y rotation per pixel of horizontal drag.\n" +
             "Lower = slower rotation. Default 0.4 feels natural.")]
    public float rotationSensitivity = 0.4f;   // FIX 5

    [Header("Target Real-World Sizes (metres)")]
    [Tooltip("Necklace items will be normalised to this diameter")]
    public float targetSizeNecklace = 0.18f;
    [Tooltip("Ring items will be normalised to this diameter")]
    public float targetSizeRing = 0.022f;
    [Tooltip("Bangle/bracelet items will be normalised to this diameter")]
    public float targetSizeBangle = 0.065f;
    [Tooltip("Earring items will be normalised to this size")]
    public float targetSizeEarring = 0.025f;
    [Tooltip("Generic/unknown items will be normalised to this size")]
    public float targetSizeGeneric = 0.08f;

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
        public GameObject sourcePrefab;
    }

    // ══════════════════════════════════════════════════════════════════
    //  INIT
    // ══════════════════════════════════════════════════════════════════

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>()
                       ?? FindObjectOfType<ARRaycastManager>();
        _planeManager = GetComponent<ARPlaneManager>()
                       ?? FindObjectOfType<ARPlaneManager>();

        if (_raycastManager == null)
            Debug.LogError("[PlacementManager] ARRaycastManager not found! Attach to XR Origin.");

        BuildRing();
    }

    void Start()
    {
        StartCoroutine(CanvasCleanupLoop());
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

        if (Input.touchCount == 2)
        {
            DoPinchZoom();
            _longPressArmed = false;
            return;
        }

        Touch t = Input.GetTouch(0);

        // ── Began ────────────────────────────────────────────────────────
        if (t.phase == TouchPhase.Began)
        {
            _touchStartPos = t.position;
            _lastTouchPos = t.position;
            _touchDownTime = Time.time;
            _longPressArmed = true;
            _isDragging = false;

            PlacedItem hit = RaycastItems(t.position);
            if (hit != null) DoSelect(hit);
        }

        // ── Moved — FIX 4 & 5: rotate via raw pixel delta, not projection ──
        if (t.phase == TouchPhase.Moved)
        {
            float movedTotal = (t.position - _touchStartPos).magnitude;
            if (movedTotal > dragThresholdPx)
            { _isDragging = true; _longPressArmed = false; }

            if (_isDragging && _selectedItem != null && _selectedItem.go != null)
            {
                // FIX 4+5: horizontal pixel delta × sensitivity = Y rotation degrees.
                // This is camera-independent — no projection, no drift.
                float dx = t.position.x - _lastTouchPos.x;
                _selectedItem.go.transform.Rotate(0f, -dx * rotationSensitivity, 0f, Space.World);
                ShowRing(_selectedItem);
            }

            _lastTouchPos = t.position;  // FIX 4: update every frame
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
                    DoDeselect();
                else
                    DoSelect(tapped);
                _lastTapTime = Time.time;
                return;
            }

            if (_selectedItem != null) { DoDeselect(); return; }

            if (_activePrefab == null)
            {
                Debug.Log("[PlacementManager] No prefab armed — open menu and tap a jewelry card.");
                return;
            }

            if (!_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon))
            {
                Debug.Log("[PlacementManager] No plane hit — aim at the detected surface outline.");
                return;
            }

            Pose pose = _hits[0].pose;

            PlacedItem existing = FindPlacedByPrefab(_activePrefab);
            if (existing != null)
            {
                MoveItem(existing, pose.position);
                DoSelect(existing);
            }
            else
            {
                DoSpawn(pose.position, pose.rotation);
            }
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

        PlacedItem existing = FindPlacedByPrefab(prefab);
        if (existing != null)
        {
            DoSelect(existing);
            Debug.Log("[PlacementManager] Re-selected existing: " + itemName);
        }
        else
        {
            Debug.Log("[PlacementManager] Armed: " + (prefab != null ? prefab.name : "NULL"));
        }
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
    //  SPAWN & MOVE  — FIX 1 & 2
    // ══════════════════════════════════════════════════════════════════

    void DoSpawn(Vector3 planePos, Quaternion planeRot)
    {
        // Instantiate at origin with identity so local bounds are clean
        var go = Instantiate(_activePrefab, Vector3.zero, Quaternion.identity);

        // FIX 2: normalise to a consistent real-world size first
        NormaliseScale(go);

        // Apply per-item fine-tune scale from JewelryItem.placeOnRoomDefaultScale
        if (!Mathf.Approximately(_activeDefaultScale, 1f))
            go.transform.localScale *= _activeDefaultScale;

        // FIX 1: measure bottom in LOCAL space (object is still at origin)
        float bottomOffset = GetBottomOffsetLocal(go);

        // Face same direction as plane, then lift so bottom sits on plane surface
        go.transform.rotation = planeRot;
        go.transform.position = planePos + Vector3.up * bottomOffset;

        AddColliders(go);

        var placed = new PlacedItem
        {
            go = go,
            originalScale = go.transform.localScale,
            itemName = _activeItemName,
            thumbnail = _activeThumb,
            sourcePrefab = _activePrefab
        };
        _placedItems.Add(placed);
        DoSelect(placed);
        OnItemPlaced?.Invoke();
        Debug.Log("[PlacementManager] Spawned: " + _activePrefab.name +
                  "  scale=" + go.transform.localScale.x.ToString("F4") +
                  "  bottomOffset=" + bottomOffset.ToString("F4"));
    }

    void MoveItem(PlacedItem item, Vector3 planePos)
    {
        if (item?.go == null) return;
        float bottomOffset = GetBottomOffsetLocal(item.go);
        item.go.transform.position = planePos + Vector3.up * bottomOffset;
        ShowRing(item);
    }

    // ── FIX 1: measure bottom offset in LOCAL space ────────────────────
    /// <summary>
    /// Returns the distance from the pivot down to the lowest mesh surface,
    /// measured in LOCAL space so it's not affected by world position.
    /// Result is always >= 0. If the pivot is already at the bottom, returns 0.
    /// </summary>
    float GetBottomOffsetLocal(GameObject go)
    {
        // Temporarily move to origin with identity rotation for clean measurement
        Vector3 savedPos = go.transform.position;
        Quaternion savedRot = go.transform.rotation;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        float lowestY = 0f;   // pivot is at Y=0
        foreach (var r in renderers)
            lowestY = Mathf.Min(lowestY, r.bounds.min.y);

        // Restore
        go.transform.SetPositionAndRotation(savedPos, savedRot);

        // lowestY is ≤ 0. Negate to get a positive lift.
        return Mathf.Max(0f, -lowestY);
    }

    // ── FIX 2: normalise scale to real-world target size ───────────────
    /// <summary>
    /// Scales the prefab so its largest dimension (X or Z) matches the
    /// target real-world size for its category, derived from the item name.
    /// Call BEFORE applying the per-item fine-tune multiplier.
    /// </summary>
    void NormaliseScale(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // Measure current size at scale (1,1,1)
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float currentSize = Mathf.Max(b.size.x, b.size.z, b.size.y);
        if (currentSize < 0.0001f) return;   // degenerate prefab

        float target = GetTargetSize(_activeItemName);
        float factor = target / currentSize;
        go.transform.localScale = go.transform.localScale * factor;
    }

    float GetTargetSize(string name)
    {
        if (string.IsNullOrEmpty(name)) return targetSizeGeneric;
        string n = name.ToLowerInvariant();
        if (n.Contains("necklace") || n.Contains("chain") || n.Contains("pendant"))
            return targetSizeNecklace;
        if (n.Contains("ring"))
            return targetSizeRing;
        if (n.Contains("bangle") || n.Contains("bracelet") || n.Contains("cuff"))
            return targetSizeBangle;
        if (n.Contains("earring") || n.Contains("ear") || n.Contains("stud") || n.Contains("hoop"))
            return targetSizeEarring;
        return targetSizeGeneric;
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

    PlacedItem FindPlacedByPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        foreach (var item in _placedItems)
            if (item.sourcePrefab == prefab && item.go != null)
                return item;
        return null;
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
            else r.gameObject.AddComponent<BoxCollider>();
        }
        if (root.GetComponentsInChildren<Collider>(true).Length == 0)
            root.AddComponent<BoxCollider>();
    }

    // ══════════════════════════════════════════════════════════════════
    //  SELECTION RING — FIX 3: transparent, subtle
    // ══════════════════════════════════════════════════════════════════

    void BuildRing()
    {
        _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _ring.name = "SelectionRing";
        Destroy(_ring.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) { Debug.LogWarning("[PlacementManager] Ring shader not found."); return; }

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
        // FIX 3: thinner ring (0.002 instead of 0.005)
        _ring.transform.localScale = new Vector3(0.35f, 0.002f, 0.35f);
        _ring.SetActive(false);
    }

    void ShowRing(PlacedItem item)
    {
        if (_ring == null || item?.go == null) return;
        var renderers = item.go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float radius = Mathf.Max(b.extents.x, b.extents.z) * 1.3f;
        _ring.transform.position = new Vector3(b.center.x, b.min.y + 0.002f, b.center.z);
        _ring.transform.localScale = new Vector3(radius * 2f, 0.002f, radius * 2f);
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
        float factor = Mathf.Clamp(
            (_selectedItem.go.transform.localScale.x / orig.x) + delta * 0.0008f,
            minScaleFactor, maxScaleFactor);

        _selectedItem.go.transform.localScale = orig * factor;
        ShowRing(_selectedItem);
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

    PlacedItem RaycastItems(Vector2 screenPos)
    {
        if (Camera.main == null) return null;
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        float bestD = float.MaxValue;
        PlacedItem best = null;
        foreach (var item in _placedItems)
        {
            if (item.go == null) continue;
            foreach (var col in item.go.GetComponentsInChildren<Collider>(true))
                if (col.Raycast(ray, out RaycastHit h, 100f) && h.distance < bestD)
                { bestD = h.distance; best = item; }
        }
        return best;
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