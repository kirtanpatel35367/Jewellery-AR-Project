using UnityEngine;

[System.Serializable]
public class JewelryItem
{
    [Tooltip("Name shown on the button, e.g. 'Gold Hoop'")]
    public string itemName;

    [Tooltip("Small preview image shown on the button (optional)")]
    public Sprite thumbnailImage;

    [Tooltip("The 3D model prefab that appears on the face when clicked")]
    public GameObject jewelryPrefab;

    // ── Necklace ONLY adjustments ─────────────────────────────
    [Header("Necklace Placement Fix (only used for necklaces)")]
    public Vector3 necklaceLocalPositionOffset = Vector3.zero;
    public Vector3 necklaceLocalRotationOffsetEuler = Vector3.zero;
    public Vector3 necklaceLocalScaleMultiplier = Vector3.one;
}
