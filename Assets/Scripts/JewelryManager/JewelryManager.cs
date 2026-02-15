using UnityEngine;
using System.Collections.Generic;

public class JewelryManager : MonoBehaviour
{
    [System.Serializable]
    public class EarringItem
    {
        public string name;
        public GameObject prefab;
    }

    [Header("Earring Collection")]
    public List<EarringItem> earringCollection = new List<EarringItem>();

    [Header("Anchor References (Auto-assigned at runtime)")]
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;

    private GameObject currentLeftEarring;
    private GameObject currentRightEarring;

    // Called by EarAnchorController when FacePrefab is spawned
    public void RegisterAnchors(Transform leftAnchor, Transform rightAnchor)
    {
        leftEarAnchor = leftAnchor;
        rightEarAnchor = rightAnchor;
        Debug.Log($"✓ Anchors registered! Left: {leftAnchor.name}, Right: {rightAnchor.name}");
    }

    public void EquipEarrings(string earringName)
    {
        Debug.Log($"===== EquipEarrings called: {earringName} =====");
        
        if (leftEarAnchor == null || rightEarAnchor == null)
        {
            Debug.LogWarning("⚠ Anchors not registered yet! Make sure face is detected.");
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

    void SpawnEarrings(GameObject prefab)
    {
        Debug.Log($"Spawning earrings: {prefab.name}");

        // Left earring
        currentLeftEarring = Instantiate(prefab, leftEarAnchor);
        currentLeftEarring.transform.localPosition = Vector3.zero;
        currentLeftEarring.transform.localRotation = Quaternion.identity;
        currentLeftEarring.transform.localScale = Vector3.one;
        Debug.Log($"✓ Left earring spawned at world pos: {currentLeftEarring.transform.position}");

        // Right earring
        currentRightEarring = Instantiate(prefab, rightEarAnchor);
        currentRightEarring.transform.localPosition = Vector3.zero;
        currentRightEarring.transform.localRotation = Quaternion.identity;
        currentRightEarring.transform.localScale = Vector3.one;
        Debug.Log($"✓ Right earring spawned at world pos: {currentRightEarring.transform.position}");

        Debug.Log("===== ✓ Earrings spawned successfully! =====");
    }

    public void RemoveEarrings()
    {
        Debug.Log("Removing earrings...");
        
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
}
