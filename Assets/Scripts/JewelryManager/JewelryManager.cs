using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UPDATED VERSION - Now handles both Earrings AND Necklaces
/// Backward compatible with your existing setup
/// </summary>
public class JewelryManager : MonoBehaviour
{
    // ============ LEGACY SUPPORT (Your old system still works) ============
    [System.Serializable]
    public class EarringItem
    {
        public string name;
        public GameObject prefab;
    }

    [Header("LEGACY: Earring Collection (Still works)")]
    public List<EarringItem> earringCollection = new List<EarringItem>();

    // ============ NEW SYSTEM (Categories) ============
    [Header("NEW: Category System")]
    [Tooltip("All jewelry categories (Earrings, Necklace, etc.)")]
    public JewelryCategory[] categories;

    [Header("Anchor References (Auto-assigned at runtime)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;
    public Transform necklaceAnchor;

    // Current state tracking
    private GameObject currentLeftEarring;
    private GameObject currentRightEarring;
    private GameObject currentNecklace;

    // Track what's currently selected
    private JewelryType currentType = JewelryType.Earrings;

    // ============ ANCHOR REGISTRATION ============

    /// <summary>
    /// Called by EarAnchorController when FacePrefab is spawned
    /// </summary>
    public void RegisterEarAnchors(Transform leftAnchor, Transform rightAnchor)
    {
        leftEarAnchor = leftAnchor;
        rightEarAnchor = rightAnchor;
        Debug.Log($"✓ Ear Anchors registered! Left: {leftAnchor.name}, Right: {rightAnchor.name}");
    }

    /// <summary>
    /// Called by NecklaceAttachARCore when necklace anchor is ready
    /// </summary>
    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        Debug.Log($"✓ Necklace Anchor registered! Anchor: {anchor.name}");
    }

    // ============ LEGACY METHODS (Backward Compatible) ============

    /// <summary>
    /// LEGACY: Works with your old UI buttons
    /// </summary>
    public void EquipEarrings(string earringName)
    {
        Debug.Log($"===== EquipEarrings (LEGACY) called: {earringName} =====");

        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            Debug.LogWarning("⚠ Ear anchors not registered yet! Make sure face is detected.");
            return;
        }

        EarringItem item = earringCollection.Find(x => x.name == earringName);
        if (item == null)
        {
            Debug.LogError($"❌ Earring '{earringName}' not found in collection!");
            return;
        }

        Debug.Log($"✓ Found item - Name: {item.name}, Prefab: {item.prefab.name}");

        RemoveEarrings();
        SpawnEarrings(item.prefab);
    }

    // ============ NEW CATEGORY-BASED METHODS ============

    /// <summary>
    /// NEW: Equip jewelry by category and item index
    /// Called by the new dynamic UI system
    /// </summary>
    public void EquipJewelryByIndex(int categoryIndex, int itemIndex)
    {
        Debug.Log($"===== EquipJewelryByIndex - Category: {categoryIndex}, Item: {itemIndex} =====");

        if (categories == null || categories.Length == 0)
        {
            Debug.LogError("❌ No categories configured! Fill Categories array in Inspector.");
            return;
        }

        if (categoryIndex < 0 || categoryIndex >= categories.Length)
        {
            Debug.LogError($"❌ Invalid category index: {categoryIndex}");
            return;
        }

        JewelryCategory category = categories[categoryIndex];

        if (category.items == null || category.items.Length == 0)
        {
            Debug.LogError($"❌ Category '{category.categoryName}' has no items!");
            return;
        }

        if (itemIndex < 0 || itemIndex >= category.items.Length)
        {
            Debug.LogError($"❌ Invalid item index: {itemIndex}");
            return;
        }

        JewelryItem item = category.items[itemIndex];
        currentType = category.type;

        Debug.Log($"✓ Equipping: {item.itemName} (Type: {category.type})");

        // Remove previous jewelry of same type
        RemoveJewelryByType(category.type);

        // Equip based on type
        switch (category.type)
        {
            case JewelryType.Earrings:
                EquipEarringsByPrefab(item.jewelryPrefab);
                break;

            case JewelryType.Necklace:
                EquipNecklaceByPrefab(item.jewelryPrefab);
                break;

            default:
                Debug.LogWarning($"⚠ Jewelry type {category.type} not yet implemented");
                break;
        }
    }

    /// <summary>
    /// NEW: Equip earrings using a prefab
    /// </summary>
    private void EquipEarringsByPrefab(GameObject prefab)
    {
        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            Debug.LogWarning("⚠ Ear anchors not registered yet! Make sure face is detected.");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("❌ Earring prefab is null!");
            return;
        }

        RemoveEarrings();
        SpawnEarrings(prefab);
    }

    /// <summary>
    /// NEW: Equip necklace using a prefab
    /// </summary>
    private void EquipNecklaceByPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("❌ Necklace prefab is null!");
            return;
        }

        Debug.Log($"🔗 Equipping necklace: {prefab.name}");

        RemoveNecklace();
        SpawnNecklace(prefab);
    }

    // ============ SPAWNING METHODS ============

    /// <summary>
    /// Spawn earrings at ear anchors
    /// </summary>
    void SpawnEarrings(GameObject prefab)
    {
        Debug.Log($"👂 Spawning earrings: {prefab.name}");

        // Left earring
        currentLeftEarring = Instantiate(prefab, leftEarAnchor);
        currentLeftEarring.transform.localPosition = Vector3.zero;
        currentLeftEarring.transform.localRotation = Quaternion.identity;
        currentLeftEarring.transform.localScale = Vector3.one;
        currentLeftEarring.name = "LeftEarring";
        Debug.Log($"✓ Left earring spawned at: {currentLeftEarring.transform.position}");

        // Right earring
        currentRightEarring = Instantiate(prefab, rightEarAnchor);
        currentRightEarring.transform.localPosition = Vector3.zero;
        currentRightEarring.transform.localRotation = Quaternion.identity;
        currentRightEarring.transform.localScale = Vector3.one;
        currentRightEarring.name = "RightEarring";

        Debug.Log($"✓ Right earring spawned at: {currentRightEarring.transform.position}");
        Debug.Log("===== ✓ Earrings equipped successfully! =====");
    }

    /// <summary>
    /// Spawn necklace at neck position
    /// </summary>
    void SpawnNecklace(GameObject prefab)
    {
        Debug.Log($"📿 Spawning necklace: {prefab.name}");

        // Try to find AR Face component for attachment
        UnityEngine.Object arFaceObject = FindObjectOfType(System.Type.GetType("UnityEngine.XR.ARFoundation.ARFace, Unity.XR.ARFoundation"));
        Component arFaceComponent = arFaceObject as Component;

        if (necklaceAnchor != null)
        {
            // Use provided anchor
            currentNecklace = Instantiate(prefab, necklaceAnchor);
            currentNecklace.transform.localPosition = Vector3.zero;
            currentNecklace.transform.localRotation = Quaternion.identity;
            currentNecklace.transform.localScale = Vector3.one;
        }
        else if (arFaceComponent != null)
        {
            // Attach to AR Face
            currentNecklace = Instantiate(prefab, arFaceComponent.transform);
            currentNecklace.transform.localPosition = new Vector3(0, -0.12f, 0.02f);
            currentNecklace.transform.localRotation = Quaternion.identity;
            currentNecklace.transform.localScale = Vector3.one;
            Debug.Log("✓ Necklace attached to ARFace");
        }
        else
        {
            // Just spawn in world
            currentNecklace = Instantiate(prefab);
            Debug.LogWarning("⚠ No anchor or ARFace found. Necklace spawned in world space.");
        }

        currentNecklace.name = "CurrentNecklace";
        Debug.Log($"✓ Necklace spawned at: {currentNecklace.transform.position}");
        Debug.Log("===== ✓ Necklace equipped successfully! =====");
    }

    // ============ REMOVAL METHODS ============

    /// <summary>
    /// Remove earrings
    /// </summary>
    public void RemoveEarrings()
    {
        Debug.Log("🗑 Removing earrings...");

        if (currentLeftEarring != null)
        {
            Destroy(currentLeftEarring);
            Debug.Log("✓ Left earring removed");
        }

        if (currentRightEarring != null)
        {
            Destroy(currentRightEarring);
            Debug.Log("✓ Right earring removed");
        }

        currentLeftEarring = null;
        currentRightEarring = null;
    }

    /// <summary>
    /// Remove necklace
    /// </summary>
    public void RemoveNecklace()
    {
        Debug.Log("🗑 Removing necklace...");

        if (currentNecklace != null)
        {
            Destroy(currentNecklace);
            Debug.Log("✓ Necklace removed");
            currentNecklace = null;
        }
    }

    /// <summary>
    /// Remove jewelry by type
    /// </summary>
    public void RemoveJewelryByType(JewelryType type)
    {
        switch (type)
        {
            case JewelryType.Earrings:
                RemoveEarrings();
                break;

            case JewelryType.Necklace:
                RemoveNecklace();
                break;

            default:
                Debug.LogWarning($"⚠ Remove not implemented for type: {type}");
                break;
        }
    }

    /// <summary>
    /// Remove ALL currently equipped jewelry
    /// </summary>
    public void RemoveAllJewelry()
    {
        Debug.Log("🗑 Removing ALL jewelry...");
        RemoveEarrings();
        RemoveNecklace();
    }

    // ============ UTILITY METHODS ============

    /// <summary>
    /// Get all items from a specific category
    /// </summary>
    public JewelryItem[] GetCategoryItems(int categoryIndex)
    {
        if (categories != null && categoryIndex >= 0 && categoryIndex < categories.Length)
        {
            return categories[categoryIndex].items;
        }
        return new JewelryItem[0];
    }

    /// <summary>
    /// Get category count
    /// </summary>
    public int GetCategoryCount()
    {
        return categories != null ? categories.Length : 0;
    }

    /// <summary>
    /// Check if jewelry is currently equipped
    /// </summary>
    public bool IsJewelryEquipped()
    {
        return currentLeftEarring != null ||
               currentRightEarring != null ||
               currentNecklace != null;
    }
}