using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// PLACEMENT MANAGER
/// Handles free-form AR placement on detected planes.
/// Supports Addressables for memory efficiency.
/// </summary>
public class PlacementManager : MonoBehaviour
{
    [Header("AR References")]
    public ARRaycastManager _raycastManager;
    public ARPlaneManager _planeManager;
    
    [Header("Settings")]
    public float scaleSpeed = 0.005f;
    public float rotateSpeed = 0.5f;

    // ── private state ────────────────────────────────────────────────
    private AssetReferenceGameObject _activeReference;
    private string _activeItemName;
    private Sprite _activeThumb;
    private float _activeDefaultScale = 1f;

    private List<PlacedItem> _placedItems = new List<PlacedItem>();
    private PlacedItem _selectedItem;

    private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private float _lastTapTime;
    private const float DOUBLE_TAP_GAP = 0.3f;

    public System.Action OnItemPlaced;

    public struct PlacedItem
    {
        public GameObject go;
        public AssetReferenceGameObject reference;
        public string name;
        public float defaultScale;
    }

    // ══════════════════════════════════════════════════════════════════
    //  UNITY EVENTS
    // ══════════════════════════════════════════════════════════════════

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.touchCount == 0) return;

        Touch t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began)
        {
            // ── Raycast for existing items first (selection) ───────────
            Ray ray = Camera.main.ScreenPointToRay(t.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                PlacedItem tapped = FindPlacedByGO(hit.collider.gameObject);
                if (tapped.go != null)
                    DoSelect(tapped);
                _lastTapTime = Time.time;
                return;
            }

            if (_selectedItem.go != null) { DoDeselect(); return; }

            if (_activeReference == null || !_activeReference.RuntimeKeyIsValid())
            {
                Debug.Log("[PlacementManager] No reference armed.");
                return;
            }

            if (!_raycastManager.Raycast(t.position, _hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
            {
                Debug.Log("[PlacementManager] No plane hit — aim at detected surface.");
                return;
            }

            Pose pose = _hits[0].pose;
            PlacedItem existing = FindPlacedByReference(_activeReference);
            if (existing.go != null)
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

    public void SetActivePrefab(AssetReferenceGameObject reference, string itemName = "",
                                Sprite thumbnail = null, float defaultScale = 1f)
    {
        if (_activeReference != reference && _selectedItem.go != null)
        {
            DoDeselect();
        }

        _activeReference = reference;
        _activeDefaultScale = defaultScale;
        _activeItemName = itemName;
        _activeThumb = thumbnail;

        PlacedItem existing = FindPlacedByReference(reference);
        if (existing.go != null) { DoSelect(existing); return; }

        Debug.Log("[PlacementManager] Armed Reference: " + reference.RuntimeKey);
    }

    public void RemoveSelected() { if (_selectedItem.go != null) DoRemove(_selectedItem); }
    public void RemoveAll()
    {
        foreach (var item in _placedItems) if (item.go) Destroy(item.go);
        _placedItems.Clear();
        DoDeselect();
    }

    // ══════════════════════════════════════════════════════════════════
    //  SPAWN
    // ══════════════════════════════════════════════════════════════════

    void DoSpawn(Vector3 planePos, Quaternion planeRot)
    {
        _activeReference.InstantiateAsync(planePos, Quaternion.identity).Completed += (op) => 
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                StartCoroutine(SpawnRoutine(op.Result, planePos, planeRot));
            }
        };
    }

    IEnumerator SpawnRoutine(GameObject go, Vector3 planePos, Quaternion planeRot)
    {
        bool isNecklace = IsNecklace(_activeItemName);
        go.transform.position = planePos;
        
        // Strip off destructive Face Try-On scripts
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>())
        {
            string tName = comp.GetType().Name;
            if (tName == "NecklaceStableFix" || tName == "NecklaceMotion" || tName == "NecklaceFitProfile")
                Destroy(comp);
        }

        go.transform.localScale = Vector3.one * _activeDefaultScale;
        
        // Ensure it has a collider for selection
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var mesh = go.GetComponentInChildren<MeshRenderer>();
            if (mesh) mesh.gameObject.AddComponent<BoxCollider>();
            else go.AddComponent<BoxCollider>();
        }

        PlacedItem newItem = new PlacedItem {
            go = go,
            reference = _activeReference,
            name = _activeItemName,
            defaultScale = _activeDefaultScale
        };
        _placedItems.Add(newItem);
        DoSelect(newItem);
        OnItemPlaced?.Invoke();

        yield return null;
    }

    // ══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════

    void DoSelect(PlacedItem item) { _selectedItem = item; Debug.Log("Selected: " + item.name); }
    void DoDeselect() { _selectedItem = default; }
    void DoRemove(PlacedItem item)
    {
        _placedItems.Remove(item);
        if (item.go) Destroy(item.go);
        if (_selectedItem.go == item.go) DoDeselect();
    }

    void MoveItemToPlane(GameObject go, Vector3 pos) { go.transform.position = pos; }

    PlacedItem FindPlacedByGO(GameObject go)
    {
        foreach (var p in _placedItems) if (p.go == go || go.transform.IsChildOf(p.go.transform)) return p;
        return default;
    }

    PlacedItem FindPlacedByReference(AssetReferenceGameObject reference)
    {
        foreach (var p in _placedItems) if (p.reference == reference) return p;
        return default;
    }

    bool IsNecklace(string name) => name.ToLower().Contains("necklace");
}
