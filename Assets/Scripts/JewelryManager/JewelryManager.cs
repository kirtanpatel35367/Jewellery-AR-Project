using UnityEngine;
using System.Collections;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// JewelryManager — central controller used by SharedJewelryUI across all three scenes.
///
/// Scene routing:
///   TryOn      scene → Earrings/Necklace (face anchors) + Bangle/Ring (body placers)
///   PlaceOnRoom scene → PlaceOnRoom type  (delegates to PlacementManager)
///   360 View   scene → no equip calls; SharedJewelryUI routes to Jewelry3DViewer directly
///
/// FIX v3 changes:
///   • Added JewelryType.PlaceOnRoom case in EquipJewelryByIndex — routes to PlacementManager.
///   • OnCategorySelected now switches to back camera for PlaceOnRoom categories too.
///   • RemoveAll now also calls PlacementManager.RemoveAll if assigned.
///   • PlacementManager reference is optional — null-safe throughout.
/// </summary>
public class JewelryManager : MonoBehaviour
{
    [Header("Jewelry Data")]
    public JewelryCategory[] categories;

    // ── Face AR (TryOn scene) ─────────────────────────────────────────
    [Header("Face AR Anchors (TryOn scene — filled at runtime by face tracker)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    // ── Hand/body AR (TryOn scene) ────────────────────────────────────
    [Header("Hand Jewelry (TryOn scene)")]
    [Tooltip("BanglePlacer component on BangleAnchor GameObject")]
    public BanglePlacer banglePlacer;

    [Tooltip("RingPlacer component on RingAnchor GameObject")]
    public RingPlacer ringPlacer;

    // ── PlaceOnRoom scene ─────────────────────────────────────────────
    [Header("Place on Room scene")]
    [Tooltip("PlacementManager in the JewelryARScene — assign only in that scene")]
    public PlacementManager placementManager;

    // ── Camera switching ──────────────────────────────────────────────
    [Header("Camera Switching")]
    [Tooltip("ARCameraManager on the Main Camera — switches facing direction")]
    public ARCameraManager arCameraManager;

    // ── Internal face AR state ────────────────────────────────────────
    private GameObject _activeLeftEarring;
    private GameObject _activeRightEarring;
    private GameObject _activeNecklace;
    private GameObject _pendingEarPrefab;
    private JewelryItem _pendingNecklaceItem;   // stores full item so SpawnNecklace gets offsets

    // Tracks actual camera state to prevent redundant switches
    private bool _currentCameraIsBack = false;

    // ═════════════════════════════════════════════════════════════════
    //  ANCHOR REGISTRATION (called by face-tracking components at runtime)
    // ═════════════════════════════════════════════════════════════════

    public void RegisterEarAnchors(Transform left, Transform right)
    {
        leftEarAnchor = left;
        rightEarAnchor = right;
        if (_pendingEarPrefab != null)
        {
            SpawnEarrings(_pendingEarPrefab);
            _pendingEarPrefab = null;
        }
    }

    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        if (_pendingNecklaceItem != null)
        {
            SpawnNecklace(_pendingNecklaceItem);
            _pendingNecklaceItem = null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  CATEGORY SELECTED  (called by SharedJewelryUI before showing items)
    //  Switches camera to the correct facing direction for this category.
    // ═════════════════════════════════════════════════════════════════

    public void OnCategorySelected(int catIdx)
    {
        if (categories == null || catIdx < 0 || catIdx >= categories.Length) return;
        JewelryType type = categories[catIdx].type;

        bool needsBack = type == JewelryType.Bangle
                      || type == JewelryType.Ring
                      || type == JewelryType.PlaceOnRoom;

        if (needsBack) SwitchToBackCamera();
        else SwitchToFaceCamera();
    }

    // ═════════════════════════════════════════════════════════════════
    //  EQUIP ITEM  (called by SharedJewelryUI when user taps an item card)
    // ═════════════════════════════════════════════════════════════════

    public void EquipJewelryByIndex(int catIdx, int itemIdx)
    {
        if (categories == null || catIdx < 0 || catIdx >= categories.Length) return;

        JewelryCategory cat = categories[catIdx];
        if (cat.items == null || itemIdx < 0 || itemIdx >= cat.items.Length) return;

        JewelryItem item = cat.items[itemIdx];
        if (item == null || item.jewelryPrefab == null)
        {
            Debug.LogWarning("[JewelryManager] No prefab on item: " + (item?.itemName ?? "NULL"));
            return;
        }

        switch (cat.type)
        {
            case JewelryType.Earrings: EquipEarrings(item); break;
            case JewelryType.Necklace: EquipNecklace(item); break;
            case JewelryType.Bangle: EquipBangle(item); break;
            case JewelryType.Ring: EquipRing(item); break;
            case JewelryType.PlaceOnRoom: EquipPlaceOnRoom(item); break;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  EQUIP METHODS
    // ═════════════════════════════════════════════════════════════════

    // ── PlaceOnRoom — delegates entirely to PlacementManager ─────────
    void EquipPlaceOnRoom(JewelryItem item)
    {
        if (placementManager == null)
        {
            Debug.LogError("[JewelryManager] placementManager not assigned — cannot place on room. " +
                           "Assign it in the JewelryARScene Inspector.");
            return;
        }
        // Apply optional per-item scale before handing off
        placementManager.SetActivePrefab(
            item.jewelryPrefab,
            item.itemName,
            item.thumbnailImage,
            item.placeOnRoomDefaultScale);
        Debug.Log("[JewelryManager] PlaceOnRoom mode armed with: " + item.itemName);
    }

    // ── Bangle (back camera, body tracking) ──────────────────────────
    void EquipBangle(JewelryItem item)
    {
        if (banglePlacer == null) { Debug.LogError("[JewelryManager] banglePlacer not assigned!"); return; }
        banglePlacer.SetBanglePrefab(item.jewelryPrefab);
    }

    // ── Ring (back camera, body tracking) ────────────────────────────
    void EquipRing(JewelryItem item)
    {
        if (ringPlacer == null) { Debug.LogError("[JewelryManager] ringPlacer not assigned!"); return; }
        ringPlacer.SetRingPrefab(item.jewelryPrefab);
    }

    // ── Earrings (face camera) ────────────────────────────────────────
    void EquipEarrings(JewelryItem item)
    {
        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            _pendingEarPrefab = item.jewelryPrefab;
            StartCoroutine(WaitAndSpawnEarrings(item.jewelryPrefab));
        }
        else
        {
            SpawnEarrings(item.jewelryPrefab);
        }
    }

    // ── Necklace (face camera) ────────────────────────────────────────
    void EquipNecklace(JewelryItem item)
    {
        if (necklaceAnchor == null)
        {
            _pendingNecklaceItem = item;
            StartCoroutine(WaitAndSpawnNecklace(item));
        }
        else
        {
            SpawnNecklace(item);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  WAIT COROUTINES  (face anchors arrive async from tracker)
    // ═════════════════════════════════════════════════════════════════

    IEnumerator WaitAndSpawnEarrings(GameObject prefab)
    {
        float t = 0f;
        while ((leftEarAnchor == null || rightEarAnchor == null) && t < 30f)
        { t += Time.deltaTime; yield return null; }

        if (leftEarAnchor != null && rightEarAnchor != null && _pendingEarPrefab == prefab)
        { SpawnEarrings(prefab); _pendingEarPrefab = null; }
    }

    IEnumerator WaitAndSpawnNecklace(JewelryItem item)
    {
        float t = 0f;
        while (necklaceAnchor == null && t < 30f)
        { t += Time.deltaTime; yield return null; }

        if (necklaceAnchor != null && _pendingNecklaceItem == item)
        { SpawnNecklace(item); _pendingNecklaceItem = null; }
    }

    // ═════════════════════════════════════════════════════════════════
    //  SPAWN
    // ═════════════════════════════════════════════════════════════════

    void SpawnEarrings(GameObject prefab)
    {
        RemoveEarrings();
        _activeLeftEarring = Instantiate(prefab, leftEarAnchor);
        _activeLeftEarring.transform.localPosition = Vector3.zero;
        _activeLeftEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        _activeRightEarring = Instantiate(prefab, rightEarAnchor);
        _activeRightEarring.transform.localPosition = Vector3.zero;
        _activeRightEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Debug.Log("[JewelryManager] Earrings spawned: " + prefab.name);
    }

    void SpawnNecklace(JewelryItem item)
    {
        RemoveNecklace();
        _activeNecklace = Instantiate(item.jewelryPrefab, necklaceAnchor);
        _activeNecklace.transform.localPosition = item.necklaceLocalPositionOffset;
        _activeNecklace.transform.localRotation = Quaternion.Euler(item.necklaceLocalRotationOffsetEuler);
        _activeNecklace.transform.localScale = Vector3.Scale(
            _activeNecklace.transform.localScale,
            item.necklaceLocalScaleMultiplier);

        // Optional helper components (only used if present on the prefab)
        RuntimeNecklaceFixer.Apply(_activeNecklace);
        NecklaceFitProfile fit = _activeNecklace.GetComponent<NecklaceFitProfile>();
        if (fit != null) fit.Apply();

        Debug.Log("[JewelryManager] Necklace spawned: " + item.itemName);
    }

    // ═════════════════════════════════════════════════════════════════
    //  REMOVE
    // ═════════════════════════════════════════════════════════════════

    void RemoveEarrings()
    {
        if (_activeLeftEarring != null) Destroy(_activeLeftEarring);
        if (_activeRightEarring != null) Destroy(_activeRightEarring);
        _activeLeftEarring = _activeRightEarring = null;
    }

    void RemoveNecklace()
    {
        if (_activeNecklace != null) Destroy(_activeNecklace);
        _activeNecklace = null;
    }

    /// <summary>
    /// Removes all active jewelry across all modes.
    /// Does NOT switch camera — stays on whatever mode the last category needed.
    /// </summary>
    public void RemoveAll()
    {
        // Face AR
        RemoveEarrings();
        RemoveNecklace();
        _pendingEarPrefab = null;
        _pendingNecklaceItem = null;

        // Body tracking
        if (banglePlacer != null) banglePlacer.ClearBangle();
        if (ringPlacer != null) ringPlacer.ClearRing();

        // PlaceOnRoom — remove all placed objects
        if (placementManager != null) placementManager.RemoveAll();

        Debug.Log("[JewelryManager] All jewelry removed. Camera unchanged.");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CAMERA SWITCHING
    // ═════════════════════════════════════════════════════════════════

    void SwitchToBackCamera()
    {
        if (_currentCameraIsBack) return;   // already back — no-op
        _currentCameraIsBack = true;
        SetCameraFacing(CameraFacingDirection.World);
        Debug.Log("[JewelryManager] Switched to BACK camera.");
    }

    void SwitchToFaceCamera()
    {
        if (!_currentCameraIsBack) return;  // already front — no-op
        _currentCameraIsBack = false;
        SetCameraFacing(CameraFacingDirection.User);
        Debug.Log("[JewelryManager] Switched to FACE camera.");
    }

    void SetCameraFacing(CameraFacingDirection direction)
    {
        if (arCameraManager == null)
        {
            Debug.LogWarning("[JewelryManager] arCameraManager not assigned — cannot switch camera.");
            return;
        }
        arCameraManager.requestedFacingDirection = direction;
    }

    // ═════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════════

    public int CategoryCount => categories != null ? categories.Length : 0;
}