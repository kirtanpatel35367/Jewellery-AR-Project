using UnityEngine;

/// <summary>
/// Attach this to a necklace prefab to define per-model scale, position offset,
/// and rotation correction. JewelryManager calls Apply() automatically after spawning.
/// </summary>
public class NecklaceFitProfile : MonoBehaviour
{
    [Header("Scale Override")]
    public Vector3 scaleOverride = Vector3.one;

    [Header("Position Offset (local)")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("Rotation Offset (Euler)")]
    public Vector3 rotationOffset = Vector3.zero;

    /// <summary>
    /// Called by JewelryManager after the necklace is instantiated.
    /// Applies the per-model corrections to this GameObject's local transform.
    /// </summary>
    public void Apply()
    {
        transform.localScale    = scaleOverride;
        transform.localPosition = positionOffset;
        transform.localRotation = Quaternion.Euler(rotationOffset);

        Debug.Log($"[NecklaceFitProfile] Applied → scale:{scaleOverride} pos:{positionOffset} rot:{rotationOffset}");
    }
}