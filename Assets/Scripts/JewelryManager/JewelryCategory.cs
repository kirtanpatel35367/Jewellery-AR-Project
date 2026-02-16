using UnityEngine;

/// <summary>
/// Represents a category of jewelry (Earrings, Necklace, etc.)
/// Each category contains multiple items
/// </summary>
[System.Serializable]
public class JewelryCategory
{
    [Tooltip("Category name displayed on tab button")]
    public string categoryName;

    [Tooltip("Icon shown on the category button (optional)")]
    public Sprite categoryIcon;

    [Tooltip("All jewelry items in this category")]
    public JewelryItem[] items;

    [Header("Category Type")]
    [Tooltip("What type of jewelry is this?")]
    public JewelryType type = JewelryType.Earrings;
}

/// <summary>
/// Enum to identify what type of jewelry this category contains
/// </summary>
public enum JewelryType
{
    Earrings,
    Necklace,
    Ring,
    Bracelet,
    Other
}