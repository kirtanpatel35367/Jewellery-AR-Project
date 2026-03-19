using UnityEngine;
using System.Collections;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// JewelryManager — handles face jewelry (Earrings, Necklace)
/// and hand jewelry (Bangle, Ring) with automatic camera switching.
///
/// Face jewelry (Earrings/Necklace) → front/face AR camera
/// Hand jewelry (Bangle/Ring)       → back camera
///
/// FIX v2:
///  - Camera switches on CATEGORY selection (not per-item click).
///  - RemoveAll() no longer forces front camera — it stays on whatever
///    camera mode was last used by the active category.
///  - _currentCameraIsBack tracks actual camera state so repeated
///    same-direction switches are no-ops.
/// </summary>
public class JewelryManager : MonoBehaviour
{
    [Header("Jewelry Data")]
    public JewelryCategory[] categories;

    [Header("Face AR Anchors (filled at runtime)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    [Header("Hand Jewelry")]
    [Tooltip("BanglePlacer component on BangleAnchor GameObject")]
    public BanglePlacer banglePlacer;

    [Tooltip("RingPlacer component on RingAnchor GameObject")]
    public RingPlacer ringPlacer;

    [Header("Camera Switching")]
    [Tooltip("ARCameraManager on the Main Camera — used to switch facing direction")]
    public ARCameraManager arCameraManager;

    // ── Face jewelry state ────────────────────────────────────────────
    private GameObject activeLeftEarring;
    private GameObject activeRightEarring;
    private GameObject activeNecklace;
    private GameObject pendingEarPrefab;
    private GameObject pendingNecklacePrefab;

    // ── Current camera mode ───────────────────────────────────────────
    // Starts as front camera (face AR default).
    private bool _currentCameraIsBack = false;

    // ── Anchor registration ───────────────────────────────────────────

    public void RegisterEarAnchors(Transform left, Transform right)
    {
        leftEarAnchor = left;
        rightEarAnchor = right;
        if (pendingEarPrefab != null) { SpawnEarrings(pendingEarPrefab); pendingEarPrefab = null; }
    }

    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        if (pendingNecklacePrefab != null) { SpawnNecklace(pendingNecklacePrefab); pendingNecklacePrefab = null; }
    }

    // ── Called by JewelryUI when user taps a CATEGORY tab ────────────
    /// <summary>
    /// Switch camera based on category type.
    /// Call this from JewelryUI.OnCategoryTapped BEFORE spawning items.
    /// </summary>
    public void OnCategorySelected(int catIdx)
    {
        if (categories == null || catIdx < 0 || catIdx >= categories.Length) return;
        JewelryCategory cat = categories[catIdx];

        bool needsBack = (cat.type == JewelryType.Bangle || cat.type == JewelryType.Ring);
        if (needsBack)
            SwitchToBackCamera();
        else
            SwitchToFaceCamera();
    }

    // ── Main entry point from JewelryUI (item click) ─────────────────
    /// <summary>
    /// Equip the selected item. Camera is already correct from OnCategorySelected.
    /// This method no longer switches cameras.
    /// </summary>
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
        }
    }

    // ── Camera switching ──────────────────────────────────────────────

    private void SwitchToBackCamera()
    {
        if (_currentCameraIsBack) return;           // already back — no-op
        _currentCameraIsBack = true;
        SetCameraFacing(CameraFacingDirection.World);
        Debug.Log("[JewelryManager] Switched to BACK camera.");
    }

    private void SwitchToFaceCamera()
    {
        if (!_currentCameraIsBack) return;          // already front — no-op
        _currentCameraIsBack = false;
        SetCameraFacing(CameraFacingDirection.User);
        Debug.Log("[JewelryManager] Switched to FACE camera.");
    }

    private void SetCameraFacing(CameraFacingDirection direction)
    {
        if (arCameraManager == null)
        {
            Debug.LogWarning("[JewelryManager] arCameraManager not assigned — cannot switch camera.");
            return;
        }
        arCameraManager.requestedFacingDirection = direction;
    }

    // ── Equip methods ─────────────────────────────────────────────────

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
            pendingEarPrefab = item.jewelryPrefab;
            StartCoroutine(WaitAndSpawnEarrings(item.jewelryPrefab));
        }
        else SpawnEarrings(item.jewelryPrefab);
    }

    void EquipNecklace(JewelryItem item)
    {
        if (necklaceAnchor == null)
        {
            pendingNecklacePrefab = item.jewelryPrefab;
            StartCoroutine(WaitAndSpawnNecklace(item.jewelryPrefab));
        }
        else SpawnNecklace(item.jewelryPrefab);
    }

    // ── Wait coroutines ───────────────────────────────────────────────

    IEnumerator WaitAndSpawnEarrings(GameObject prefab)
    {
        float t = 0f;
        while ((leftEarAnchor == null || rightEarAnchor == null) && t < 30f)
        { t += Time.deltaTime; yield return null; }
        if (leftEarAnchor != null && rightEarAnchor != null && pendingEarPrefab == prefab)
        { SpawnEarrings(prefab); pendingEarPrefab = null; }
    }

    IEnumerator WaitAndSpawnNecklace(GameObject prefab)
    {
        float t = 0f;
        while (necklaceAnchor == null && t < 30f)
        { t += Time.deltaTime; yield return null; }
        if (necklaceAnchor != null && pendingNecklacePrefab == prefab)
        { SpawnNecklace(prefab); pendingNecklacePrefab = null; }
    }

    // ── Spawn ─────────────────────────────────────────────────────────

    void SpawnEarrings(GameObject prefab)
    {
        RemoveEarrings();
        activeLeftEarring = Instantiate(prefab, leftEarAnchor);
        activeLeftEarring.transform.localPosition = Vector3.zero;
        activeLeftEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        activeRightEarring = Instantiate(prefab, rightEarAnchor);
        activeRightEarring.transform.localPosition = Vector3.zero;
        activeRightEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Debug.Log("[JewelryManager] Earrings spawned: " + prefab.name);
    }

    void SpawnNecklace(GameObject prefab)
    {
        RemoveNecklace();
        activeNecklace = Instantiate(prefab, necklaceAnchor);
        activeNecklace.transform.localPosition = Vector3.zero;
        activeNecklace.transform.localRotation = Quaternion.identity;
        RuntimeNecklaceFixer.Apply(activeNecklace);
        NecklaceFitProfile fit = activeNecklace.GetComponent<NecklaceFitProfile>();
        if (fit != null) fit.Apply();
        Debug.Log("[JewelryManager] Necklace spawned: " + prefab.name);
    }

    // ── Remove ────────────────────────────────────────────────────────

    void RemoveEarrings()
    {
        if (activeLeftEarring != null) Destroy(activeLeftEarring);
        if (activeRightEarring != null) Destroy(activeRightEarring);
        activeLeftEarring = activeRightEarring = null;
    }

    void RemoveNecklace()
    {
        if (activeNecklace != null) Destroy(activeNecklace);
        activeNecklace = null;
    }

    /// <summary>
    /// Removes all active jewelry.
    /// Camera is NOT switched — it stays on whatever the active category needed.
    /// If no hand jewelry was active, it was already on front camera anyway.
    /// </summary>
    public void RemoveAll()
    {
        RemoveEarrings();
        RemoveNecklace();
        if (banglePlacer != null) banglePlacer.ClearBangle();
        if (ringPlacer != null) ringPlacer.ClearRing();
        pendingEarPrefab = null;
        pendingNecklacePrefab = null;
        // ── NO camera switch here ──────────────────────────────────────
        // The camera stays on whatever mode the last selected category needed.
        // This prevents unwanted flipping to front camera when user taps
        // "Remove All" while in Bangle/Ring (back-camera) mode.
        Debug.Log("[JewelryManager] All jewelry removed. Camera unchanged.");
    }

    public int CategoryCount => categories != null ? categories.Length : 0;
}