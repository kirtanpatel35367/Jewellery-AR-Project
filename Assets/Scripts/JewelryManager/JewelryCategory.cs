using UnityEngine;

/// <summary>
/// JewelryType — determines which AR mode is used for each category.
///
///   Earrings   → front/face camera  (face anchor spawn)
///   Necklace   → front/face camera  (face anchor spawn)
///   Bangle     → back camera        (wrist AR via BanglePlacer)
///   Ring       → back camera        (finger AR via RingPlacer)
///   PlaceOnRoom→ back camera        (tap-to-place on AR plane via PlacementManager)
/// </summary>
public enum JewelryType
{
    Earrings,       // face camera — spawns on left/right ear anchors
    Necklace,       // face camera — spawns on necklace anchor
    Bangle,         // back camera — BanglePlacer handles wrist tracking
    Ring,           // back camera — RingPlacer handles finger tracking
    PlaceOnRoom     // back camera — user taps plane to place/move item
}

[System.Serializable]
public class JewelryCategory
{
    [Tooltip("Name shown on the category row in the popup, e.g. 'Earrings' or 'Bangles'")]
    public string categoryName;

    [Tooltip(
        "Earrings / Necklace  = front/face camera\n" +
        "Bangle / Ring        = back camera (body tracking)\n" +
        "PlaceOnRoom          = back camera (tap-to-place on floor/table)\n\n" +
        "⚠ In JewelryARScene set this to 'PlaceOnRoom' for all categories\n" +
        "   you want visible in that scene.")]
    public JewelryType type;

    public JewelryItem[] items;
}