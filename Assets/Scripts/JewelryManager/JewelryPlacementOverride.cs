using UnityEngine;

/// <summary>
/// Attach this to a jewelry prefab (root) to override local placement after spawn.
/// Use this mainly for necklaces whose GLB pivot/scale differs.
/// </summary>
public class JewelryPlacementOverride : MonoBehaviour
{
    [Header("Apply to spawned instance")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    [Header("Which transforms to apply")]
    public bool applyPosition = true;
    public bool applyRotation = true;
    public bool applyScale = true;
}
