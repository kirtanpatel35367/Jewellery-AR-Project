using UnityEngine;
using System.Collections;

/// <summary>
/// JEWELRY MANAGER v2
///
/// FIXES:
///  1. BraceletPlacement / BanglePlacement null errors — now auto-found via
///     FindObjectOfType if not assigned in Inspector. Validated once in Awake.
///  2. CameraManager null guard added everywhere it's used.
///  3. All LogError spam replaced with a single Awake validation report.
/// </summary>
public class JewelryManager : MonoBehaviour
{
    [Header("Jewelry Data")]
    public JewelryCategory[] categories;

    [Header("Face AR Anchors (front camera)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    [Header("Camera Manager")]
    public CameraManager cameraManager;

    [Header("Hand Jewelry Placement (back camera)")]
    public BraceletPlacement braceletPlacement;
    public BanglePlacement banglePlacement;

    // Active face jewelry instances
    private GameObject activeLeftEarring;
    private GameObject activeRightEarring;
    private GameObject activeNecklace;

    // Pending spawns
    private GameObject pendingEarPrefab;
    private GameObject pendingNecklacePrefab;

    // Delay after camera switch before spawning jewelry
    private const float CAMERA_SWITCH_DELAY = 0.6f;

    // ── Awake: auto-find any unassigned components ────────────────────────
    void Awake()
    {
        if (cameraManager == null)
            cameraManager = FindObjectOfType<CameraManager>();

        if (braceletPlacement == null)
            braceletPlacement = FindObjectOfType<BraceletPlacement>();

        if (banglePlacement == null)
            banglePlacement = FindObjectOfType<BanglePlacement>();

        // Report what was found / still missing
        if (braceletPlacement == null)
            Debug.LogError("[JewelryManager] BraceletPlacement not found in scene! " +
                           "Add a GameObject with BraceletPlacement component.");
        else
            Debug.Log("[JewelryManager] BraceletPlacement: " + braceletPlacement.gameObject.name);

        if (banglePlacement == null)
            Debug.LogError("[JewelryManager] BanglePlacement not found in scene! " +
                           "Add a GameObject with BanglePlacement component.");
        else
            Debug.Log("[JewelryManager] BanglePlacement: " + banglePlacement.gameObject.name);

        if (cameraManager == null)
            Debug.LogWarning("[JewelryManager] CameraManager not found — camera switching disabled.");
    }

    // ── Anchor registration (FACE) ────────────────────────────────────────
    public void RegisterEarAnchors(Transform left, Transform right)
    {
        leftEarAnchor = left;
        rightEarAnchor = right;
        Debug.Log("[JewelryManager] Ear anchors registered.");

        if (pendingEarPrefab != null)
        {
            SpawnEarrings(pendingEarPrefab);
            pendingEarPrefab = null;
        }
    }

    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        Debug.Log("[JewelryManager] Necklace anchor registered.");

        if (pendingNecklacePrefab != null)
        {
            SpawnNecklace(pendingNecklacePrefab);
            pendingNecklacePrefab = null;
        }
    }

    // ── Called by JewelryUI on item tap ───────────────────────────────────
    public void EquipJewelryByIndex(int catIdx, int itemIdx)
    {
        if (categories == null || catIdx < 0 || catIdx >= categories.Length) return;

        JewelryCategory cat = categories[catIdx];
        if (cat.items == null || itemIdx < 0 || itemIdx >= cat.items.Length) return;

        JewelryItem item = cat.items[itemIdx];
        if (item == null || item.jewelryPrefab == null)
        {
            Debug.LogWarning($"[JewelryManager] No prefab on " +
                             $"'{(item != null ? item.itemName : "NULL ITEM")}'");
            return;
        }

        bool needsCameraSwitch = NeedsCameraSwitch(cat.type);

        if (cameraManager != null)
            cameraManager.SwitchForJewelryType(cat.type);

        if (needsCameraSwitch)
            StartCoroutine(SpawnAfterCameraSwitch(cat.type, item));
        else
            SpawnByType(cat.type, item);
    }

    bool NeedsCameraSwitch(JewelryType type)
    {
        if (cameraManager == null) return false;

        bool isFaceType = (type == JewelryType.Earrings || type == JewelryType.Necklace);
        bool isHandType = (type == JewelryType.Bracelet ||
                           type == JewelryType.Ring ||
                           type == JewelryType.Bangle);

        if (isFaceType && !cameraManager.IsFaceMode()) return true;
        if (isHandType && !cameraManager.IsHandMode()) return true;
        return false;
    }

    IEnumerator SpawnAfterCameraSwitch(JewelryType type, JewelryItem item)
    {
        Debug.Log($"[JewelryManager] Camera switched — waiting {CAMERA_SWITCH_DELAY}s...");
        yield return new WaitForSeconds(CAMERA_SWITCH_DELAY);
        SpawnByType(type, item);
    }

    void SpawnByType(JewelryType type, JewelryItem item)
    {
        switch (type)
        {
            case JewelryType.Earrings:
                if (leftEarAnchor == null || rightEarAnchor == null)
                {
                    pendingEarPrefab = item.jewelryPrefab;
                    StartCoroutine(WaitAndSpawnEarrings(item.jewelryPrefab));
                }
                else SpawnEarrings(item.jewelryPrefab);
                break;

            case JewelryType.Necklace:
                if (necklaceAnchor == null)
                {
                    pendingNecklacePrefab = item.jewelryPrefab;
                    StartCoroutine(WaitAndSpawnNecklace(item.jewelryPrefab));
                }
                else SpawnNecklace(item.jewelryPrefab);
                break;

            case JewelryType.Bracelet:
                SpawnBracelet(item.jewelryPrefab);
                break;

            case JewelryType.Bangle:
                SpawnBangle(item.jewelryPrefab);
                break;

            case JewelryType.Ring:
                Debug.LogWarning("[JewelryManager] Ring placement not implemented yet.");
                break;
        }
    }

    // ── Wait coroutines (FACE) ────────────────────────────────────────────
    IEnumerator WaitAndSpawnEarrings(GameObject prefab)
    {
        float timeout = 30f, elapsed = 0f;
        while ((leftEarAnchor == null || rightEarAnchor == null) && elapsed < timeout)
        { elapsed += Time.deltaTime; yield return null; }

        if (leftEarAnchor == null || rightEarAnchor == null)
        { Debug.LogWarning("[JewelryManager] Ear anchor timeout."); yield break; }

        if (pendingEarPrefab == prefab)
        { SpawnEarrings(prefab); pendingEarPrefab = null; }
    }

    IEnumerator WaitAndSpawnNecklace(GameObject prefab)
    {
        float timeout = 30f, elapsed = 0f;
        while (necklaceAnchor == null && elapsed < timeout)
        { elapsed += Time.deltaTime; yield return null; }

        if (necklaceAnchor == null)
        { Debug.LogWarning("[JewelryManager] Necklace anchor timeout."); yield break; }

        if (pendingNecklacePrefab == prefab)
        { SpawnNecklace(prefab); pendingNecklacePrefab = null; }
    }

    // ── Spawn: FACE ───────────────────────────────────────────────────────
    void SpawnEarrings(GameObject prefab)
    {
        RemoveEarrings();
        activeLeftEarring = Instantiate(prefab, leftEarAnchor);
        activeLeftEarring.transform.localPosition = Vector3.zero;
        activeLeftEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        activeRightEarring = Instantiate(prefab, rightEarAnchor);
        activeRightEarring.transform.localPosition = Vector3.zero;
        activeRightEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Debug.Log($"[JewelryManager] Earrings spawned: {prefab.name}");
    }

    void SpawnNecklace(GameObject prefab)
    {
        RemoveNecklace();
        activeNecklace = Instantiate(prefab, necklaceAnchor);
        activeNecklace.transform.localPosition = Vector3.zero;
        activeNecklace.transform.localRotation = Quaternion.identity;

        NecklaceFitProfile fit = activeNecklace.GetComponent<NecklaceFitProfile>();
        if (fit != null) fit.Apply();

        Debug.Log($"[JewelryManager] Necklace spawned: {prefab.name}");
    }

    // ── Spawn: HAND ───────────────────────────────────────────────────────
    void SpawnBracelet(GameObject prefab)
    {
        RemoveBangles(); // mutually exclusive with bangle

        if (braceletPlacement == null)
            braceletPlacement = FindObjectOfType<BraceletPlacement>();

        if (braceletPlacement != null)
        {
            braceletPlacement.SpawnBracelet(prefab);
            Debug.Log($"[JewelryManager] Bracelet spawned: {prefab.name}");
        }
        else
        {
            Debug.LogError("[JewelryManager] Cannot spawn bracelet — " +
                           "BraceletPlacement component missing from scene.");
        }
    }

    void SpawnBangle(GameObject prefab)
    {
        RemoveBracelets(); // mutually exclusive with bracelet

        if (banglePlacement == null)
            banglePlacement = FindObjectOfType<BanglePlacement>();

        if (banglePlacement != null)
        {
            banglePlacement.SpawnBangles(prefab);
            Debug.Log($"[JewelryManager] Bangles spawned: {prefab.name}");
        }
        else
        {
            Debug.LogError("[JewelryManager] Cannot spawn bangle — " +
                           "BanglePlacement component missing from scene.");
        }
    }

    // ── Remove ────────────────────────────────────────────────────────────
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

    void RemoveBracelets()
    {
        if (braceletPlacement != null) braceletPlacement.RemoveBracelet();
    }

    void RemoveBangles()
    {
        if (banglePlacement != null) banglePlacement.RemoveBangles();
    }

    public void RemoveAll()
    {
        RemoveEarrings();
        RemoveNecklace();
        RemoveBracelets();
        RemoveBangles();
        pendingEarPrefab = null;
        pendingNecklacePrefab = null;
        Debug.Log("[JewelryManager] All jewelry removed.");
    }

    public int CategoryCount => categories != null ? categories.Length : 0;
}