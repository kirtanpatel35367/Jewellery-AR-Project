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
        var category = categories[catIdx];
        var item = category.items[itemIdx];
        EquipItem(item, category.categoryName.ToLower());
    }

    public void EquipItem(JewelryItem item, string categoryType = "")
    {
        if (item == null) { Debug.LogError("[JewelryManager] Item is NULL!"); return; }
        if (item.jewelryReference == null || !item.jewelryReference.RuntimeKeyIsValid()) { 
            Debug.LogError("[JewelryManager] Invalid AssetReference for: " + item.itemName);
            return; 
        }

        Debug.Log("[JewelryManager] Attempting to equip: " + item.itemName + " (Category: " + categoryType + ")");
        
        // Use the passed categoryType, or fall back to item name if empty
        string routingType = string.IsNullOrEmpty(categoryType) ? item.itemName.ToLower() : categoryType;

        // 1. Placement AR (Always try this)
        if (placementManager != null)
        {
            placementManager.SetActivePrefab(item.jewelryReference, item.itemName, item.thumbnailImage);
        }

        // 2. Hand AR
        if (routingType.Contains("ring") && ringPlacer != null) ringPlacer.SetRing(item.jewelryReference);
        if (routingType.Contains("bangle") && banglePlacer != null) banglePlacer.SetBangle(item.jewelryReference);

        // 3. Face AR (Necklace)
        if (routingType.Contains("necklace"))
        {
            Debug.Log("[JewelryManager] Routing to NecklaceAnchor");
            _pendingNeckReference = item.jewelryReference;
            if (necklaceAnchor != null) SpawnNecklace();
            else Debug.LogWarning("[JewelryManager] No necklaceAnchor found! Waiting for AR Tracking...");
        }

        // 4. Face AR (Earrings)
        if (routingType.Contains("earring"))
        {
            Debug.Log("[JewelryManager] Routing to EarringAnchors");
            _pendingEarReference = item.jewelryReference;
            if (leftEarAnchor != null && rightEarAnchor != null) SpawnEarrings();
            else Debug.LogWarning("[JewelryManager] No EarAnchors found! Waiting for AR Tracking...");
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
                _currentNecklace.transform.localPosition = Vector3.zero;
                _currentNecklace.transform.localRotation = Quaternion.identity;
                Debug.Log("[JewelryManager] Necklace SPAWNED. Scale: " + _currentNecklace.transform.localScale + " Pos: " + _currentNecklace.transform.localPosition);
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
                _currentLeftEar.transform.localPosition = Vector3.zero;
                _currentLeftEar.transform.localRotation = Quaternion.identity;
                Debug.Log("[JewelryManager] Left Earring SPAWNED. Scale: " + _currentLeftEar.transform.localScale);
            } else {
                Debug.LogError("[JewelryManager] Left Earring FAILED: " + op.OperationException);
            }
        };
        _pendingEarReference.InstantiateAsync(rightEarAnchor).Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded) {
                _currentRightEar = op.Result;
                _currentRightEar.transform.localPosition = Vector3.zero;
                _currentRightEar.transform.localRotation = Quaternion.identity;
                Debug.Log("[JewelryManager] Right Earring SPAWNED. Scale: " + _currentRightEar.transform.localScale);
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
