using UnityEngine;

/// <summary>
/// JewelryItem — one entry inside a JewelryCategory.
/// Shared across all three scenes (360 View, Place on Room, Face Try-On).
/// </summary>
[System.Serializable]
public class JewelryItem
{
    [Tooltip("Name shown on the card button, e.g. 'Gold Hoop'")]
    public string itemName;

    [Tooltip("Small preview sprite shown on the card in all three scenes")]
    public Sprite thumbnailImage;

    [Tooltip("The 3D model prefab — placed on face, wrist, finger, or AR plane depending on category type")]
    public GameObject jewelryPrefab;

    // ── Necklace-only fine-tuning ──────────────────────────────────────
    [Header("Necklace Placement Tweaks (ignored for other types)")]
    public Vector3 necklaceLocalPositionOffset = Vector3.zero;
    public Vector3 necklaceLocalRotationOffsetEuler = Vector3.zero;
    public Vector3 necklaceLocalScaleMultiplier = Vector3.one;

    // ── PlaceOnRoom fine-tuning ────────────────────────────────────────
    [Header("PlaceOnRoom Tweaks")]
    [Tooltip("Scale applied when first placed on a detected plane (1 = prefab default)")]
    public float placeOnRoomDefaultScale = 1f;
}