using UnityEngine;

/// <summary>
/// Jewelry type — used by JewelryManager to know
/// where to attach the jewelry (ears vs neck).
/// </summary>
public enum JewelryType
{
    Earrings,
    Necklace
}

/// <summary>
/// One category tab (e.g. "Earring" or "Necklace").
/// Contains an array of JewelryItem entries.
/// Fill these fields in the JewelryManager Inspector.
/// </summary>
[System.Serializable]
public class JewelryCategory
{
    [Tooltip("Name shown on the tab button, e.g. 'Earring'")]
    public string categoryName;

    [Tooltip("Earrings or Necklace")]
    public JewelryType type;

    [Tooltip("All items inside this category")]
    public JewelryItem[] items;
}
