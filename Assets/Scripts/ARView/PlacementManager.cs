using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlacementManager — one instance per jewelry prefab rule.
///
/// KEY BEHAVIOUR:
///   • Each jewelry item can only be placed ONCE in the scene.
///     Tapping the plane a second time with the same item selected
///     MOVES the existing instance instead of creating a new one.
///   • Selecting a different jewelry card arms a different prefab,
///     which can then be placed independently (also only once).
///   • Tap a placed item  → select it (gold ring appears)
///   • Drag selected item → rotate 360° Y
///   • Pinch selected     → zoom in / out
///   • Long-press 1.5 s   → remove that item
///   • Double-tap         → deselect
///   • REMOVE ALL         → clears everything
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
    private float _rotateStartAngle = 0f;
    private float _itemStartYRot = 0f;
    private float _lastPinchDist = 0f;
    private float _lastTapTime = 0f;
    private const float DOUBLE_TAP = 0.35f;

    // ── Selection ring ─────────────────────────────────────────────────
    private GameObject _ring;
    private static readonly Color RING_COL = new Color(0.95f, 0.80f, 0.25f, 0.85f);

    // ── Inspector ──────────────────────────────────────────────────────
    [Header("Interaction")]
    public float longPressDuration = 1.5f;
    public float dragThresholdPx = 14f;
    public float minScaleFactor = 0.2f;
    public float maxScaleFactor = 5.0f;

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
        public GameObject sourcePrefab;   // ← tracks which prefab this came from
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
            Debug.LogError("[PlacementManager] ARRaycastManager not found! Add it to XR Origin.");

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
                _rotateStartAngle = TouchAngleAroundItem(t.position, hit);
                _itemStartYRot = hit.go.transform.eulerAngles.y;
            }
        }

        // ── Moved ────────────────────────────────────────────────────────
        if (t.phase == TouchPhase.Moved)
        {
            if ((t.position - _touchStartPos).magnitude > dragThresholdPx)
            { _isDragging = true; _longPressArmed = false; }

            if (_isDragging && _selectedItem != null && _selectedItem.go != null)
            {
                float delta = TouchAngleAroundItem(t.position, _selectedItem) - _rotateStartAngle;
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
            // Did the user tap a placed item?
            PlacedItem tapped = RaycastItems(t.position);
            if (tapped != null)
            {
                if (Time.time - _lastTapTime < DOUBLE_TAP && _selectedItem == tapped)
                    DoDeselect();
                else
                {
                    DoSelect(tapped);
                    _rotateStartAngle = TouchAngleAroundItem(t.position, tapped);
                    _itemStartYRot = tapped.go.transform.eulerAngles.y;
                }
                _lastTapTime = Time.time;
                return;
            }

            // Tap on empty space while something is selected → deselect
            if (_selectedItem != null) { DoDeselect(); return; }

            // No prefab armed
            if (_activePrefab == null)
            {
                Debug.Log("[PlacementManager] No prefab armed — open the menu and tap a jewelry card.");
                return;
            }

            // Raycast to AR plane
            if (!_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon))
            {
                Debug.Log("[PlacementManager] No plane hit — aim camera at the detected surface and tap.");
                return;
            }

            Pose pose = _hits[0].pose;

            // ── ONE-INSTANCE RULE ─────────────────────────────────────
            // Check if this exact prefab is already placed in the scene.
            PlacedItem existing = FindPlacedByPrefab(_activePrefab);
            if (existing != null)
            {
                // Move the existing instance to the new tap position instead
                // of spawning a duplicate.
                MoveItem(existing, pose.position, pose.rotation);
                DoSelect(existing);
                Debug.Log("[PlacementManager] Moved existing: " + existing.itemName);
            }
            else
            {
                // First time placing this prefab — spawn it.
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

        // Auto-select the existing instance of this prefab (if already placed)
        // so the user can immediately manage it without tapping it first.
        PlacedItem existing = FindPlacedByPrefab(prefab);
        if (existing != null)
        {
            DoSelect(existing);
            Debug.Log("[PlacementManager] Re-selected existing: " + itemName);
        }
        else
        {
            Debug.Log("[PlacementManager] Armed: " + (prefab != null ? prefab.name : "NULL") +
                      " — tap the surface to place.");
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
    //  ONE-INSTANCE RULE HELPER
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the already-placed instance that came from the given prefab,
    /// or null if this prefab hasn't been placed yet.
    /// Comparison uses the prefab reference (not name) for accuracy.
    /// </summary>
    PlacedItem FindPlacedByPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        foreach (var item in _placedItems)
            if (item.sourcePrefab == prefab && item.go != null)
                return item;
        return null;
    }

    // ══════════════════════════════════════════════════════════════════
    //  SPAWN & MOVE
    // ══════════════════════════════════════════════════════════════════

    void DoSpawn(Vector3 planePosition, Quaternion planeRotation)
    {
        var go = Instantiate(_activePrefab, Vector3.zero, planeRotation);

        if (!Mathf.Approximately(_activeDefaultScale, 1f))
            go.transform.localScale *= _activeDefaultScale;

        float bottomOffset = GetBottomOffset(go);
        go.transform.position = planePosition + new Vector3(0f, bottomOffset, 0f);

        AddColliders(go);

        var placed = new PlacedItem
        {
            go = go,
            originalScale = go.transform.localScale,
            itemName = _activeItemName,
            thumbnail = _activeThumb,
            sourcePrefab = _activePrefab        // ← store which prefab this is
        };
        _placedItems.Add(placed);
        DoSelect(placed);
        OnItemPlaced?.Invoke();
        Debug.Log("[PlacementManager] Spawned: " + _activePrefab.name + " at " + go.transform.position);
    }

    /// <summary>
    /// Moves an already-placed item to a new plane position.
    /// Keeps its current scale and rotation.
    /// </summary>
    void MoveItem(PlacedItem item, Vector3 planePosition, Quaternion planeRotation)
    {
        if (item?.go == null) return;
        float bottomOffset = GetBottomOffset(item.go);
        item.go.transform.position = planePosition + new Vector3(0f, bottomOffset, 0f);
        // Keep existing Y rotation (user may have rotated it)
        // but snap X/Z to plane rotation
        Vector3 euler = item.go.transform.eulerAngles;
        item.go.transform.rotation = Quaternion.Euler(planeRotation.eulerAngles.x,
                                                       euler.y,
                                                       planeRotation.eulerAngles.z);
        ShowRing(item);
    }

    float GetBottomOffset(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;
        float minY = float.MaxValue;
        foreach (var r in renderers) minY = Mathf.Min(minY, r.bounds.min.y);
        return -minY;
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
            else r.gameObject.AddComponent<BoxCollider>();
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
        float factor = Mathf.Clamp(
            (_selectedItem.go.transform.localScale.x / orig.x) + delta * 0.001f,
            minScaleFactor, maxScaleFactor);

        _selectedItem.go.transform.localScale = orig * factor;
        ShowRing(_selectedItem);
    }

    // ══════════════════════════════════════════════════════════════════
    //  CANVAS CLEANUP — removes empty GraphicRaycaster blockers
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
            {
                if (col.Raycast(ray, out RaycastHit h, 100f) && h.distance < bestD)
                { bestD = h.distance; best = item; }
            }
        }
        return best;
    }

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