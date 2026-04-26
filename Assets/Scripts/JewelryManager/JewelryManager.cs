using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class JewelryManager : MonoBehaviour
{
    [Header("Data")]
    public JewelryCategory[] categories;

    [Header("Placers")]
    public RingPlacer ringPlacer;
    public BanglePlacer banglePlacer;
    public PlacementManager placementManager;

    [Header("Face Anchors")]
    public Transform necklaceAnchor;
    public Transform leftEarAnchor;
    public Transform rightEarAnchor;

    private GameObject _currentNecklace;
    private GameObject _currentLeftEar;
    private GameObject _currentRightEar;

    private AssetReferenceGameObject _pendingNeckReference;
    private AssetReferenceGameObject _pendingEarReference;

    public void OnCategorySelected(int index)
    {
        Debug.Log("Category Selected: " + categories[index].categoryName);
    }

    void Start()
    {
        // Ensure Addressables are ready for Play Asset Delivery
        Addressables.InitializeAsync().Completed += (op) => {
            Debug.Log("[JewelryManager] Addressables Initialized. Status: " + op.Status);
        };
    }

    public void EquipJewelryByIndex(int catIdx, int itemIdx)
    {
        var item = categories[catIdx].items[itemIdx];
        EquipItem(item);
    }

    public void EquipItem(JewelryItem item)
    {
        if (item == null) { Debug.LogError("[JewelryManager] Item is NULL!"); return; }
        if (item.jewelryReference == null || !item.jewelryReference.RuntimeKeyIsValid()) { 
            Debug.LogError("[JewelryManager] Invalid AssetReference for: " + item.itemName);
            return; 
        }

        Debug.Log("[JewelryManager] Attempting to equip: " + item.itemName);
        string typeName = item.itemName.ToLower();

        // 1. Placement AR
        if (placementManager != null)
        {
            Debug.Log("[JewelryManager] Routing to PlacementManager");
            placementManager.SetActivePrefab(item.jewelryReference, item.itemName, item.thumbnailImage);
        }

        // 2. Hand AR
        if (typeName.Contains("ring") && ringPlacer != null) 
        {
            Debug.Log("[JewelryManager] Routing to RingPlacer");
            ringPlacer.SetRing(item.jewelryReference);
        }
        if (typeName.Contains("bangle") && banglePlacer != null) 
        {
            Debug.Log("[JewelryManager] Routing to BanglePlacer");
            banglePlacer.SetBangle(item.jewelryReference);
        }

        // 3. Face AR (Necklace)
        if (typeName.Contains("necklace"))
        {
            Debug.Log("[JewelryManager] Routing to NecklaceAnchor");
            _pendingNeckReference = item.jewelryReference;
            if (necklaceAnchor != null) SpawnNecklace();
            else Debug.LogWarning("[JewelryManager] No necklaceAnchor found in scene!");
        }

        // 4. Face AR (Earrings)
        if (typeName.Contains("earring"))
        {
            Debug.Log("[JewelryManager] Routing to EarringAnchors");
            _pendingEarReference = item.jewelryReference;
            if (leftEarAnchor != null && rightEarAnchor != null) SpawnEarrings();
            else Debug.LogWarning("[JewelryManager] No EarAnchors found in scene!");
        }
    }

    public void RegisterNecklaceAnchor(Transform anchor)
    {
        necklaceAnchor = anchor;
        if (_pendingNeckReference != null) SpawnNecklace();
    }

    public void RegisterEarAnchors(Transform left, Transform right)
    {
        leftEarAnchor = left;
        rightEarAnchor = right;
        if (_pendingEarReference != null) SpawnEarrings();
    }

    private void SpawnNecklace()
    {
        Debug.Log("[JewelryManager] SpawnNecklace called for anchor: " + necklaceAnchor.name);
        if (_currentNecklace != null) Destroy(_currentNecklace);
        _pendingNeckReference.InstantiateAsync(necklaceAnchor).Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded) {
                _currentNecklace = op.Result;
                Debug.Log("[JewelryManager] Necklace SPAWNED SUCCESSFULLY");
            } else {
                Debug.LogError("[JewelryManager] Necklace SPAWN FAILED: " + op.OperationException);
            }
        };
    }

    private void SpawnEarrings()
    {
        Debug.Log("[JewelryManager] SpawnEarrings called");
        if (_currentLeftEar != null) Destroy(_currentLeftEar);
        if (_currentRightEar != null) Destroy(_currentRightEar);

        _pendingEarReference.InstantiateAsync(leftEarAnchor).Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded) {
                _currentLeftEar = op.Result;
                Debug.Log("[JewelryManager] Left Earring SPAWNED");
            } else {
                Debug.LogError("[JewelryManager] Left Earring FAILED: " + op.OperationException);
            }
        };
        _pendingEarReference.InstantiateAsync(rightEarAnchor).Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded) {
                _currentRightEar = op.Result;
                Debug.Log("[JewelryManager] Right Earring SPAWNED");
            } else {
                Debug.LogError("[JewelryManager] Right Earring FAILED: " + op.OperationException);
            }
        };
    }

    public void RemoveAll()
    {
        if (ringPlacer) ringPlacer.Clear();
        if (banglePlacer) banglePlacer.Clear();
        if (placementManager) placementManager.RemoveAll();
        
        if (_currentNecklace) Destroy(_currentNecklace);
        if (_currentLeftEar) Destroy(_currentLeftEar);
        if (_currentRightEar) Destroy(_currentRightEar);
        
        _pendingNeckReference = null;
        _pendingEarReference = null;
    }
}
