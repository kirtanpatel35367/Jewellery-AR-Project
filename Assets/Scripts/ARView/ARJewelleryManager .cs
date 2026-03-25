using UnityEngine;
using System.Collections.Generic;

public class ARJewelleryManager : MonoBehaviour
{
    [Header("Database")]
    public List<GameObject> jewelleryPrefabs = new List<GameObject>();
    public List<Sprite> jewelleryThumbnails = new List<Sprite>();
    public List<string> jewelleryNames = new List<string>();

    private GameObject currentPrefab;
    private GameObject spawnedObject;

    // Called from JewelryUI when user taps a card
    public void SelectItem(int index)
    {
        if (index < 0 || index >= jewelleryPrefabs.Count) return;
        currentPrefab = jewelleryPrefabs[index];

        // Destroy old placed object so new selection spawns fresh
        if (spawnedObject != null)
        {
            Destroy(spawnedObject);
            spawnedObject = null;
        }
    }

    // Called by PlacementManager on raycast hit
    public void PlaceOrMove(Vector3 position, Quaternion rotation)
    {
        if (currentPrefab == null) return;

        if (spawnedObject == null)
            spawnedObject = Instantiate(currentPrefab, position, rotation);
        else
            spawnedObject.transform.SetPositionAndRotation(position, rotation);
    }

    public bool HasSelection() => currentPrefab != null;
}