using UnityEngine;

/// <summary>
/// Holds data for ONE jewelry piece shown as a button in the UI.
/// Fill these fields in the JewelryManager Inspector.
/// </summary>
[System.Serializable]
public class JewelryItem
{
    [Tooltip("Name shown on the button, e.g. 'Gold Hoop'")]
    public string itemName;

    [Tooltip("Small preview image shown on the button (optional)")]
    public Sprite thumbnailImage;

    [Tooltip("The 3D model prefab that appears on the face when clicked")]
    public GameObject jewelryPrefab;
}
