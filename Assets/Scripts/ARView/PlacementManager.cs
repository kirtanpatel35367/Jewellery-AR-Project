using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlacementManager — plane glow removed.
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

            if (_selectedItem != null) { DoDeselect(); return; }

            if (_activePrefab == null)
            {
                Debug.Log("[PlacementManager] No prefab armed — tap a jewelry card first.");
                return;
            }

            if (_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon))
                DoSpawn(_hits[0].pose.position, _hits[0].pose.rotation);
            else
                Debug.Log("[PlacementManager] No plane hit — aim at the detected surface and tap.");
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
        // Spawn at world origin temporarily so bounds are not offset by
        // planePosition — this is what caused the "mid-air" bug.
        var go = Instantiate(_activePrefab, Vector3.zero, planeRotation);

        if (!Mathf.Approximately(_activeDefaultScale, 1f))
            go.transform.localScale *= _activeDefaultScale;

        // With the object at Y=0 we can measure how far its bottom mesh
        // surface is below the pivot cleanly.
        float bottomOffset = GetBottomOffset(go);

        // Place on plane: pivot goes to planePosition, then lift by bottomOffset
        // so the bottom surface lands exactly on the plane surface.
        go.transform.position = planePosition + new Vector3(0f, bottomOffset, 0f);

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
                  " at " + go.transform.position + "  bottomOffset=" + bottomOffset.ToString("F4"));
    }

    /// <summary>
    /// Returns the distance from the pivot (assumed at Y=0) down to the
    /// lowest point of all mesh renderers. Spawning at Vector3.zero first
    /// ensures world-space bounds.min.y directly equals the gap below pivot.
    /// </summary>
    float GetBottomOffset(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;

        float minY = float.MaxValue;
        foreach (var r in renderers)
            minY = Mathf.Min(minY, r.bounds.min.y);

        // minY is negative (below pivot) — negate it to get a positive lift amount.
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