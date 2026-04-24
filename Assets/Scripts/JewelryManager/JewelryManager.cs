using UnityEngine;
using System.Collections;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// JewelryManager v4
/// FIX CHANGES:
///   1. SpawnNecklace: NecklaceAutoFit called BEFORE RuntimeNecklaceFixer (order bug fix)
///   2. SpawnNecklace: FindObjectOfType for NecklaceAttachARCore — it is on the ARFace
///      prefab, NOT a child of JewelryManager (GetComponentInChildren was always null)
///   3. WaitAndSpawnNecklace / WaitAndSpawnEarrings: timeout 30s → 60s
///   4. NecklaceAutoFit: Apply() has _applied guard so Start() + JewelryManager
///      calling it twice no longer doubles the scale
/// </summary>
public class JewelryManager : MonoBehaviour
{
    [Header("Jewelry Data")]
    public JewelryCategory[] categories;

    [Header("Face AR Anchors (TryOn scene — filled at runtime by face tracker)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    [Header("Hand Jewelry (TryOn scene)")]
    public BanglePlacer banglePlacer;
    public RingPlacer ringPlacer;

    [Header("Place on Room scene")]
    public PlacementManager placementManager;

    [Header("Camera Switching")]
    public ARCameraManager arCameraManager;

    private GameObject _activeLeftEarring;
    private GameObject _activeRightEarring;
    private GameObject _activeNecklace;
    private GameObject _pendingEarPrefab;
    private JewelryItem _pendingNecklaceItem;
    private bool _currentCameraIsBack = false;

    // ═════════════════════════════════════════════════════════════════
    //  ANCHOR REGISTRATION
    // ═════════════════════════════════════════════════════════════════

    public void RegisterEarAnchors(Transform left, Transform right)
    {
        leftEarAnchor = left;
        rightEarAnchor = right;
        Debug.Log("[JewelryManager] Ear anchors registered.");
        if (_pendingEarPrefab != null)
        {
            SpawnEarrings(_pendingEarPrefab);
            _pendingEarPrefab = null;
        }
    }

    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        Debug.Log("[JewelryManager] Necklace anchor registered.");
        if (_pendingNecklaceItem != null)
        {
            SpawnNecklace(_pendingNecklaceItem);
            _pendingNecklaceItem = null;
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  CATEGORY SELECTED
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
    //  EQUIP
    // ═════════════════════════════════════════════════════════════════

    public void EquipJewelryByIndex(int catIdx, int itemIdx)
    {
        if (categories == null || catIdx < 0 || catIdx >= categories.Length) return;
        JewelryCategory cat = categories[catIdx];
        if (cat.items == null || itemIdx < 0 || itemIdx >= cat.items.Length) return;
        JewelryItem item = cat.items[itemIdx];
        if (item == null || item.jewelryPrefab == null)
        {
            Debug.LogWarning("[JewelryManager] No prefab: " + (item?.itemName ?? "NULL"));
            return;
        }

        if (placementManager != null) { EquipPlaceOnRoom(item); return; }

        switch (cat.type)
        {
            case JewelryType.Earrings: EquipEarrings(item); break;
            case JewelryType.Necklace: EquipNecklace(item); break;
            case JewelryType.Bangle: EquipBangle(item); break;
            case JewelryType.Ring: EquipRing(item); break;
            case JewelryType.PlaceOnRoom: EquipPlaceOnRoom(item); break;
        }
    }

    void EquipPlaceOnRoom(JewelryItem item)
    {
        if (placementManager == null) { Debug.LogError("[JewelryManager] placementManager not assigned!"); return; }
        placementManager.SetActivePrefab(item.jewelryPrefab, item.itemName, item.thumbnailImage, item.placeOnRoomDefaultScale);
    }

    void EquipBangle(JewelryItem item)
    {
        if (banglePlacer == null) { Debug.LogError("[JewelryManager] banglePlacer not assigned!"); return; }
        banglePlacer.SetBanglePrefab(item.jewelryPrefab);
    }

    void EquipRing(JewelryItem item)
    {
        if (ringPlacer == null) { Debug.LogError("[JewelryManager] ringPlacer not assigned!"); return; }
        ringPlacer.SetRingPrefab(item.jewelryPrefab);
    }

    void EquipEarrings(JewelryItem item)
    {
        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            _pendingEarPrefab = item.jewelryPrefab;
            StartCoroutine(WaitAndSpawnEarrings(item.jewelryPrefab));
        }
        else SpawnEarrings(item.jewelryPrefab);
    }

    void EquipNecklace(JewelryItem item)
    {
        if (necklaceAnchor == null)
        {
            _pendingNecklaceItem = item;
            StartCoroutine(WaitAndSpawnNecklace(item));
        }
        else SpawnNecklace(item);
    }

    // ═════════════════════════════════════════════════════════════════
    //  WAIT COROUTINES  — timeout extended to 60s (face detect can be slow)
    // ═════════════════════════════════════════════════════════════════

    IEnumerator WaitAndSpawnEarrings(GameObject prefab)
    {
        float t = 0f;
        while ((leftEarAnchor == null || rightEarAnchor == null) && t < 60f)
        { t += Time.deltaTime; yield return null; }

        if (leftEarAnchor != null && rightEarAnchor != null && _pendingEarPrefab == prefab)
        { SpawnEarrings(prefab); _pendingEarPrefab = null; }
        else
            Debug.LogWarning("[JewelryManager] Ear anchors never registered. " +
                "Is EarAttachARCore on the Face Prefab and enabled?");
    }

    IEnumerator WaitAndSpawnNecklace(JewelryItem item)
    {
        float t = 0f;
        while (necklaceAnchor == null && t < 60f)
        { t += Time.deltaTime; yield return null; }

        if (necklaceAnchor != null && _pendingNecklaceItem == item)
        { SpawnNecklace(item); _pendingNecklaceItem = null; }
        else
            Debug.LogWarning("[JewelryManager] Necklace anchor never registered. " +
                "Is NecklaceAttachARCore on the Face Prefab, ENABLED (checkbox ON)?");
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

        // ── 1. Instantiate parented to anchor ────────────────────────────
        _activeNecklace = Instantiate(item.jewelryPrefab, necklaceAnchor);

        // ── 2. NecklaceAutoFit FIRST — auto-positions by mesh bounds ─────
        // Must run before RuntimeNecklaceFixer so bounds measurement is clean.
        NecklaceAutoFit autoFit = _activeNecklace.GetComponent<NecklaceAutoFit>();
        if (autoFit != null)
        {
            autoFit.Apply();
        }
        else
        {
            // Fallback: prefab has no NecklaceAutoFit yet — use legacy item offsets
            _activeNecklace.transform.localPosition = item.necklaceLocalPositionOffset;
            _activeNecklace.transform.localRotation = Quaternion.Euler(item.necklaceLocalRotationOffsetEuler);
            _activeNecklace.transform.localScale = Vector3.Scale(
                _activeNecklace.transform.localScale, item.necklaceLocalScaleMultiplier);
            Debug.LogWarning("[JewelryManager] '" + item.itemName +
                "' has no NecklaceAutoFit — using legacy offsets. " +
                "Add NecklaceAutoFit to the prefab root for auto-positioning.");
        }

        // ── 3. RuntimeNecklaceFixer AFTER — special per-name tweaks ──────
        RuntimeNecklaceFixer.Apply(_activeNecklace);

        // ── 4. Notify NecklaceAttachARCore ───────────────────────────────
        // FIX: Use FindObjectOfType — NecklaceAttachARCore lives on the ARFace
        // prefab which is a separate scene object, NOT a child of JewelryManager.
        // GetComponentInChildren would always return null here.
        NecklaceAttachARCore attach = FindObjectOfType<NecklaceAttachARCore>();
        if (attach != null) attach.OnNecklaceSpawned(_activeNecklace);

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

    public void RemoveAll()
    {
        RemoveEarrings();
        RemoveNecklace();
        _pendingEarPrefab = null;
        _pendingNecklaceItem = null;
        if (banglePlacer != null) banglePlacer.ClearBangle();
        if (ringPlacer != null) ringPlacer.ClearRing();
        if (placementManager != null) placementManager.RemoveAll();
        Debug.Log("[JewelryManager] All jewelry removed.");
    }

    // ═════════════════════════════════════════════════════════════════
    //  CAMERA
    // ═════════════════════════════════════════════════════════════════

    void SwitchToBackCamera()
    {
        if (_currentCameraIsBack) return;
        _currentCameraIsBack = true;
        SetCameraFacing(CameraFacingDirection.World);
        Debug.Log("[JewelryManager] → BACK camera.");
    }

    void SwitchToFaceCamera()
    {
        if (!_currentCameraIsBack) return;
        _currentCameraIsBack = false;
        SetCameraFacing(CameraFacingDirection.User);
        Debug.Log("[JewelryManager] → FACE camera.");
    }

    void SetCameraFacing(CameraFacingDirection direction)
    {
        if (arCameraManager == null) { Debug.LogWarning("[JewelryManager] arCameraManager not assigned."); return; }
        arCameraManager.requestedFacingDirection = direction;
    }

    public int CategoryCount => categories != null ? categories.Length : 0;
}