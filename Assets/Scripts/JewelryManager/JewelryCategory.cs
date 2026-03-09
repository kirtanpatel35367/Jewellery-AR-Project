using UnityEngine;

/// <summary>
/// Jewelry type — used by JewelryManager to know
/// where to attach the jewelry and which camera to use.
/// UPDATED: Added Bracelet, Ring, and Bangle support!
/// </summary>
public enum JewelryType
{
    Earrings,   // Front camera - ear anchors
    Necklace,   // Front camera - neck anchor
    Bracelet,   // Back camera - wrist anchor (SINGLE hand)
    Ring,       // Back camera - finger anchor
    Bangle      // Back camera - wrist anchors (BOTH hands)
}

/// <summary>
/// One category tab (e.g. "Earring", "Necklace", "Bracelet", "Ring", "Bangle").
/// Contains an array of JewelryItem entries.
/// Fill these fields in the JewelryManager Inspector.
/// </summary>
[System.Serializable]
public class JewelryCategory
{
    [Tooltip("Name shown on the tab button, e.g. 'Earring', 'Bracelet', 'Bangle'")]
    public string categoryName;

    [Tooltip("Type: Earrings, Necklace, Bracelet (single hand), Ring, or Bangle (both hands)")]
    public JewelryType type;

    [Tooltip("All items inside this category")]
    public JewelryItem[] items;
}