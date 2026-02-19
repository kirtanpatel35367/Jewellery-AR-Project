using UnityEngine;

/// <summary>
/// Fixes ONLY Egyptian Necklace position/rotation/scale
/// without affecting other jewelry.
/// Attach this script to egyptian_necklace PREFAB.
/// </summary>
public class EgyptianNecklaceFix : MonoBehaviour
{
    [Header("Position Fix")]
    public Vector3 positionOffset = new Vector3(0f, -0.18f, 0.06f);

    [Header("Rotation Fix")]
    public Vector3 rotationOffset = new Vector3(90f, 0f, 0f);

    [Header("Scale Fix")]
    public Vector3 scaleMultiplier = new Vector3(1f, 1f, 1f);

    void Start()
    {
        ApplyFix();
    }

    void ApplyFix()
    {
        // Position
        transform.localPosition += positionOffset;

        // Rotation
        transform.localRotation *= Quaternion.Euler(rotationOffset);

        // Scale
        transform.localScale = Vector3.Scale(transform.localScale, scaleMultiplier);
    }
}
