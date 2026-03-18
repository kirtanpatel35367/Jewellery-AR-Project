using UnityEngine;

public enum JewelryType
{
    Earrings,   // face camera
    Necklace,   // face camera
    Bangle,     // back camera (wrist)
    Ring        // back camera (finger)
}

[System.Serializable]
public class JewelryCategory
{
    [Tooltip("Name shown on the tab button")]
    public string categoryName;

    [Tooltip("Earrings/Necklace = face camera | Bangle/Ring = back camera")]
    public JewelryType type;

    public JewelryItem[] items;
}