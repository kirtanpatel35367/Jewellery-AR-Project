using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// BANGLE PLACER (v4)
/// Handles wrist-based 3D occlusion and Addressables loading.
/// </summary>
public class BanglePlacer : MonoBehaviour
{
    [Header("Bangle Settings")]
    public AssetReferenceGameObject bangleReference;
    public float wristWidth = 0.05f;
    public float scaleFactor = 1.0f;
    
    private GameObject _instance;

    public void SetBangle(AssetReferenceGameObject reference)
    {
        if (_instance != null) Destroy(_instance);
        bangleReference = reference;
        
        if (reference != null && reference.RuntimeKeyIsValid())
        {
            reference.InstantiateAsync(transform).Completed += (op) =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    _instance = op.Result;
                    _instance.transform.localPosition = Vector3.zero;
                    _instance.transform.localRotation = Quaternion.identity;
                }
            };
        }
    }

    public void Clear()
    {
        if (_instance != null) Destroy(_instance);
    }
}
