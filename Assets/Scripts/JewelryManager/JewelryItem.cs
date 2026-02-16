using UnityEngine;

/// <summary>
/// Represents a single jewelry item (one earring design, one necklace design, etc.)
/// This appears as a clickable button with thumbnail in the UI
/// </summary>
[System.Serializable]
public class JewelryItem
{
    [Tooltip("Display name shown in UI")]
    public string itemName;

    [Tooltip("Small preview image shown on the button")]
    public Sprite thumbnailImage;

    [Tooltip("3D prefab that appears on the face/neck")]
    public GameObject jewelryPrefab;

    [Tooltip("Optional description or price")]
    public string description;

    [Header("Optional: For Different Attachment Methods")]
    [Tooltip("Leave empty to use default attachment")]
    public string customAttachmentScript;
}