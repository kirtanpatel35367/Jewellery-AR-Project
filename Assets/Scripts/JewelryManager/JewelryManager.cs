using UnityEngine;
using System.Collections;

/// <summary>
/// JewelryManager — Fixed pending spawn system.
///
/// - Waits for anchors before spawning (handles slow face detection).
/// - IMPORTANT FIX: Do NOT force localScale = Vector3.one so GLB prefabs keep their correct scale.
/// - Necklace FIX: If the spawned necklace prefab has NecklaceFitProfile, apply per-model scale/offset/rotation.
/// </summary>
public class JewelryManager : MonoBehaviour
{
    [Header("Jewelry Data  (fill in all items here)")]
    public JewelryCategory[] categories;

    [Header("AR Anchors — leave empty, filled at runtime")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    private GameObject activeLeftEarring;
    private GameObject activeRightEarring;
    private GameObject activeNecklace;

    private GameObject pendingEarPrefab;
    private GameObject pendingNecklacePrefab;

    // ── Anchor registration ───────────────────────────────────────────────
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
            Debug.LogWarning($"[JewelryManager] No prefab on '{(item != null ? item.itemName : "NULL ITEM")}'");
            return;
        }

        if (cat.type == JewelryType.Earrings)
        {
            if (leftEarAnchor == null || rightEarAnchor == null)
            {
                pendingEarPrefab = item.jewelryPrefab;
                pendingNecklacePrefab = null;
                Debug.Log("[JewelryManager] Ear anchors not ready — waiting...");
                StartCoroutine(WaitAndSpawnEarrings(item.jewelryPrefab));
            }
            else
            {
                SpawnEarrings(item.jewelryPrefab);
            }
        }
        else // Necklace
        {
            if (necklaceAnchor == null)
            {
                pendingNecklacePrefab = item.jewelryPrefab;
                pendingEarPrefab = null;
                Debug.Log("[JewelryManager] Necklace anchor not ready — waiting...");
                StartCoroutine(WaitAndSpawnNecklace(item.jewelryPrefab));
            }
            else
            {
                SpawnNecklace(item.jewelryPrefab);
            }
        }
    }

    // ── Wait coroutines ───────────────────────────────────────────────────
    IEnumerator WaitAndSpawnEarrings(GameObject prefab)
    {
        float timeout = 30f;
        float elapsed = 0f;

        while ((leftEarAnchor == null || rightEarAnchor == null) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            Debug.LogWarning("[JewelryManager] Ear anchor timeout — face not detected.");
            yield break;
        }

        if (pendingEarPrefab == prefab || pendingEarPrefab == null)
        {
            SpawnEarrings(prefab);
            pendingEarPrefab = null;
        }
    }

    IEnumerator WaitAndSpawnNecklace(GameObject prefab)
    {
        float timeout = 30f;
        float elapsed = 0f;

        while (necklaceAnchor == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (necklaceAnchor == null)
        {
            Debug.LogWarning("[JewelryManager] Necklace anchor timeout — face not detected.");
            yield break;
        }

        if (pendingNecklacePrefab == prefab || pendingNecklacePrefab == null)
        {
            SpawnNecklace(prefab);
            pendingNecklacePrefab = null;
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnEarrings(GameObject prefab)
    {
        RemoveEarrings();

        // Left
        activeLeftEarring = Instantiate(prefab, leftEarAnchor);
        activeLeftEarring.transform.localPosition = Vector3.zero;
        activeLeftEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // DO NOT force scale (keeps GLB prefab scale)

        // Right (mirrored rotation)
        activeRightEarring = Instantiate(prefab, rightEarAnchor);
        activeRightEarring.transform.localPosition = Vector3.zero;
        activeRightEarring.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // DO NOT force scale (keeps GLB prefab scale)

        Debug.Log($"[JewelryManager] Earrings spawned: {prefab.name}");
    }

    void SpawnNecklace(GameObject prefab)
    {
        RemoveNecklace();

        activeNecklace = Instantiate(prefab, necklaceAnchor);
        activeNecklace.transform.localPosition = Vector3.zero;
        activeNecklace.transform.localRotation = Quaternion.identity;
        // DO NOT force scale (keeps GLB prefab scale)

        // ✅ Apply per-model fix if present on the prefab
        NecklaceFitProfile fit = activeNecklace.GetComponent<NecklaceFitProfile>();
        if (fit != null)
        {
            fit.Apply();
            Debug.Log($"[JewelryManager] Necklace spawned + fitted: {prefab.name}");
        }
        else
        {
            Debug.LogWarning($"[JewelryManager] Necklace spawned but NO NecklaceFitProfile found on prefab: {prefab.name}");
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

    public void RemoveAll()
    {
        RemoveEarrings();
        RemoveNecklace();
        pendingEarPrefab = null;
        pendingNecklacePrefab = null;
        Debug.Log("[JewelryManager] All removed.");
    }

    public int CategoryCount => categories != null ? categories.Length : 0;
}