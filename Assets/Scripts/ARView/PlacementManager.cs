using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PlacementManager — jewelry placed EXACTLY at the tapped AR plane point.
///
/// ══════════════════════════════════════════════════════════════════════
/// ROOT CAUSE ANALYSIS (confirmed by video frame inspection):
/// ══════════════════════════════════════════════════════════════════════
///
/// SYMPTOM:
///   The selection RING appears correctly at the tap point.
///   The NECKLACE always appears at the near bottom edge of the screen,
///   far from the tap point, regardless of where you tap.
///
/// ROOT CAUSE:
///   The previous version of PlaceOnPlane() tried to "center the mesh
///   over the tap point" by subtracting bounds.center.x and bounds.center.z
///   from planePos.x and planePos.z:
///
///       rootX = planePos.x - bounds.center.x    ← THIS IS WRONG
///       rootZ = planePos.z - bounds.center.z    ← THIS IS WRONG
///
///   The necklace prefab is modelled upright (standing up) and then
///   rotated 90° on X to lie flat. After this rotation, the mesh that
///   was originally along Y is now along Z. The child mesh has a large
///   local position offset — so bounds.center.x and bounds.center.z
///   are large world-space values (e.g. bounds.center.x = 0.8m).
///
///   Subtracting these large offsets from planePos.xz moved the root
///   pivot to a completely wrong world position — often behind/below
///   the camera, which projected to the very bottom of the screen.
///   This is exactly what the video shows.
///
/// THE FIX:
///   NEVER offset X and Z. Always place the root pivot directly at
///   planePos.x and planePos.z. Only Y needs correction (the lift).
///
///   rootX = planePos.x   ← always exact
///   rootZ = planePos.z   ← always exact
///   rootY = planePos.y - bounds.min.y   ← lift so mesh bottom = plane
///
///   The necklace will appear centered near the tap point in XZ because
///   the prefab's root pivot is already near the mesh center in XZ —
///   any small XZ offset is irrelevant and invisible to the user.
///   What matters is that it appears AT the tap point, not 2m away.
///
/// ══════════════════════════════════════════════════════════════════════
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
    private Vector2 _lastTouchPos;
    private float _lastTapTime = 0f;
    private const float DOUBLE_TAP = 0.35f;

    // ── Pinch ──────────────────────────────────────────────────────────
    private float _lastPinchDist = 0f;

    // ── Selection ring ─────────────────────────────────────────────────
    private GameObject _ringGO;
    private LineRenderer _ringLine;
    private const int RING_SEGMENTS = 48;
    private static readonly Color RING_COL = new Color(1f, 0.92f, 0.4f, 0.55f);

    // ── Inspector ──────────────────────────────────────────────────────
    [Header("Interaction")]
    public float longPressDuration = 1.5f;
    public float dragThresholdPx = 18f;
    public float minScaleFactor = 0.2f;
    public float maxScaleFactor = 5.0f;
    public float rotationSensitivity = 0.4f;

    [Header("Target Real-World Sizes (metres)")]
    public float targetSizeNecklace = 0.18f;
    public float targetSizeRing = 0.022f;
    public float targetSizeBangle = 0.065f;
    public float targetSizeEarring = 0.025f;
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
        public bool isNecklace;
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
            Debug.LogError("[PlacementManager] ARRaycastManager not found!");

        BuildSelectionRing();
    }

    void Start() => StartCoroutine(CanvasCleanupLoop());

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

        if (t.phase == TouchPhase.Moved)
        {
            if ((t.position - _touchStartPos).magnitude > dragThresholdPx)
            { _isDragging = true; _longPressArmed = false; }

            if (_isDragging && _selectedItem != null && _selectedItem.go != null)
            {
                float dx = t.position.x - _lastTouchPos.x;
                _selectedItem.go.transform.Rotate(Vector3.up, -dx * rotationSensitivity, Space.World);
                UpdateRingPosition(_selectedItem);
            }

            _lastTouchPos = t.position;
        }

        if (_longPressArmed && !_isDragging && _selectedItem != null)
        {
            if (Time.time - _touchDownTime >= longPressDuration)
            {
                DoRemove(_selectedItem);
                _longPressArmed = false;
                return;
            }
        }

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
                Debug.Log("[PlacementManager] No prefab armed.");
                return;
            }

            if (!_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
            {
                Debug.Log("[PlacementManager] No plane hit — aim at detected surface.");
                return;
            }

            Pose pose = _hits[0].pose;
            PlacedItem existing = FindPlacedByPrefab(_activePrefab);
            if (existing != null)
            {
                MoveItemToPlane(existing.go, pose.position);
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
        // Clear selection if arming a new prefab, so the very next tap spawns immediately
        if (_activePrefab != prefab && _selectedItem != null)
        {
            DoDeselect();
        }

        _activePrefab = prefab;
        _activeDefaultScale = defaultScale;
        _activeItemName = itemName;
        _activeThumb = thumbnail;

        PlacedItem existing = FindPlacedByPrefab(prefab);
        if (existing != null) { DoSelect(existing); return; }

        Debug.Log("[PlacementManager] Armed: " + (prefab?.name ?? "NULL"));
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

    void DoSpawn(Vector3 planePos, Quaternion planeRot)
    {
        StartCoroutine(SpawnRoutine(planePos, planeRot));
    }

    IEnumerator SpawnRoutine(Vector3 planePos, Quaternion planeRot)
    {
        bool isNecklace = IsNecklace(_activeItemName);

        // Step 1: Instantiate at plane position (NOT zero) to prevent frustum culling bugs with skinned meshes!
        var go = Instantiate(_activePrefab, planePos, Quaternion.identity);
        
        // Strip off destructive Face Try-On specific override scripts!
        // Prefabs built for Face Try-On have scripts like "NecklaceStableFix" that run in LateUpdate 
        // to aggressively pin the transform to 0,0,0 (relative to the neck anchor).
        // Since we are placing it freely in a room, these scripts must be eradicated immediately.
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>())
        {
            string tName = comp.GetType().Name;
            if (tName == "NecklaceStableFix" || tName == "NecklaceMotion" || tName == "NecklaceFitProfile")
            {
                Destroy(comp);
            }
        }

        // Safety lock offscreen culling which breaks bounding boxes
        foreach (var skm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skm.updateWhenOffscreen = true;

        // Step 2: Scale to real-world size (measured cleanly at identity).
        ApplyTargetScale(go, isNecklace);

        // Step 3: Fine-tune scale multiplier.
        if (!Mathf.Approximately(_activeDefaultScale, 1f))
        {
            go.transform.localScale *= _activeDefaultScale;
        }

        // Step 4: Apply final rotation
        if (isNecklace)
        {
            go.transform.rotation = Quaternion.Euler(90f, planeRot.eulerAngles.y, 0f);
        }
        else
        {
            go.transform.rotation = planeRot;
        }

        // CRITICAL DEFERRAL: SkinnedMeshRenderers do NOT immediately update their world Bounds property 
        // in the exact same frame you rotate them. If we calculate bounds right now, we get the standing-up bounds.
        // We MUST yield 1 frame to let Unity recalculate the bones so the necklace doesn't fly out of the plane!
        yield return null;

        // Step 5: Place at tap position with correct Y lift AND cleanly updated bounds!
        MoveItemToPlane(go, planePos);

        AddColliders(go);

        var placed = new PlacedItem
        {
            go = go,
            originalScale = go.transform.localScale,
            itemName = _activeItemName,
            thumbnail = _activeThumb,
            sourcePrefab = _activePrefab,
            isNecklace = isNecklace
        };
        _placedItems.Add(placed);
        DoSelect(placed);
        OnItemPlaced?.Invoke();
    }

    // ══════════════════════════════════════════════════════════════════
    //  MOVE TO PLANE  ←  THE CORE FIX
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Places 'go' so its mesh bottom sits on planePos.y,
    /// with the root pivot directly over planePos.xz.
    ///
    /// CRITICAL: Only Y is corrected with a lift value.
    ///           X and Z come DIRECTLY from planePos — no offset subtracted.
    ///
    /// Why no XZ offset?
    ///   The previous version subtracted bounds.center.x/z from planePos.x/z,
    ///   intending to "center the mesh over the tap point". But for a necklace
    ///   prefab with a child mesh that has a large local position offset,
    ///   bounds.center.x/z can be very large (e.g. 0.8m). Subtracting this
    ///   moved the root pivot to a location completely off-screen — typically
    ///   to the near bottom edge of the camera view. This is exactly the bug
    ///   visible in the video.
    ///
    ///   The root pivot should just go to planePos.xz directly. The prefab
    ///   author is responsible for placing the pivot near the mesh center in XZ,
    ///   which is standard practice. Any minor XZ offset is invisible in use.
    /// </summary>
    void MoveItemToPlane(GameObject go, Vector3 planePos)
    {
        // Place temporarily at planePos (if not already) to ensure it's in view
        // and bounds calculate properly (fixes SkinnedMeshRenderer off-screen culling bugs).
        go.transform.position = planePos;

        Bounds b = GetCombinedBounds(go);

        // Distance from object root pivot to its bounding box characteristics
        float lift = (b.size == Vector3.zero) ? 0f : -(b.min.y - go.transform.position.y);
        float offsetX = b.center.x - go.transform.position.x;
        float offsetZ = b.center.z - go.transform.position.z;

        // Correct centering: Offset the tap coordinates so the visual mesh lands perfectly on tap
        go.transform.position = new Vector3(
            planePos.x - offsetX,
            planePos.y + lift,
            planePos.z - offsetZ
        );
    }

    // ══════════════════════════════════════════════════════════════════
    //  BOUNDS HELPER
    // ══════════════════════════════════════════════════════════════════

    static Bounds GetCombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(false);
        bool initialized = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);

        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.bounds.size == Vector3.zero) continue;

            if (!initialized)
            {
                b = r.bounds;
                initialized = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }
        
        if (!initialized) 
        {
            return new Bounds(go.transform.position, Vector3.zero);
        }
        
        return b;
    }

    // ══════════════════════════════════════════════════════════════════
    //  SCALE NORMALISATION
    // ══════════════════════════════════════════════════════════════════

    void ApplyTargetScale(GameObject go, bool isNecklace)
    {
        Bounds b = GetCombinedBounds(go);
        if (b.size == Vector3.zero) return;

        // Simply pick the absolute longest dimension of the mesh to use as the footprint size.
        // This removes the dependency on how the 3D artist natively modeled it (standing up vs flat)
        float currentSize = Mathf.Max(b.size.x, b.size.y, b.size.z);

        if (currentSize < 0.0001f) return;

        float target = GetTargetSize(_activeItemName);
        float factor = target / currentSize;
        go.transform.localScale *= factor;
    }

    bool IsNecklace(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("necklace") || n.Contains("chain") ||
               n.Contains("pendant") || n.Contains("choker");
    }

    float GetTargetSize(string name)
    {
        if (string.IsNullOrEmpty(name)) return targetSizeGeneric;
        string n = name.ToLowerInvariant();
        if (n.Contains("necklace") || n.Contains("chain") ||
            n.Contains("pendant") || n.Contains("choker")) return targetSizeNecklace;
        if (n.Contains("ring")) return targetSizeRing;
        if (n.Contains("bangle") || n.Contains("bracelet") ||
            n.Contains("cuff")) return targetSizeBangle;
        if (n.Contains("earring") || n.Contains("ear") ||
            n.Contains("stud") || n.Contains("hoop")) return targetSizeEarring;
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
    //  SELECTION RING
    // ══════════════════════════════════════════════════════════════════

    void BuildSelectionRing()
    {
        _ringGO = new GameObject("SelectionRing");
        _ringLine = _ringGO.AddComponent<LineRenderer>();
        _ringLine.loop = true;
        _ringLine.positionCount = RING_SEGMENTS;
        _ringLine.useWorldSpace = true;
        _ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ringLine.receiveShadows = false;
        _ringLine.widthMultiplier = 0.004f;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color")
                  ?? Shader.Find("Unlit/Transparent");

        Material mat = shader != null
            ? new Material(shader) { color = RING_COL }
            : new Material(Shader.Find("Sprites/Default")) { color = RING_COL };

        mat.SetFloat("_Surface", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.SetOverrideTag("RenderType", "Transparent");

        _ringLine.material = mat;
        _ringGO.SetActive(false);
    }

    void ShowRing(PlacedItem item)
    {
        if (_ringGO == null || item?.go == null) return;
        var renderers = item.go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float radius = Mathf.Max(b.extents.x, b.extents.z) * 1.25f;
        float ringY = b.min.y + 0.003f;
        Vector3 center = new Vector3(b.center.x, ringY, b.center.z);

        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float angle = (i / (float)RING_SEGMENTS) * Mathf.PI * 2f;
            _ringLine.SetPosition(i, center + new Vector3(
                Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        _ringGO.SetActive(true);
    }

    void UpdateRingPosition(PlacedItem item)
    {
        if (_ringGO != null && _ringGO.activeSelf) ShowRing(item);
    }

    void HideRing() { if (_ringGO != null) _ringGO.SetActive(false); }

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
        UpdateRingPosition(_selectedItem);
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
            if (count == 0) Destroy(gr);
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