using UnityEngine;

/// <summary>
/// JEWELRY SELECTION BRIDGE
///
/// Static data holder — survives scene loads without DontDestroyOnLoad.
/// The Main Menu writes to this, and each view scene reads from it.
/// </summary>
public static class JewelrySelectionBridge
{
    /// <summary>The jewelry item the player last tapped in the main menu.</summary>
    public static JewelryItem SelectedItem { get; set; }

    /// <summary>The full category that item belongs to (useful for TryOn scene).</summary>
    public static JewelryCategory SelectedCategory { get; set; }

    /// <summary>Index of the item inside its category array.</summary>
    public static int SelectedItemIndex { get; set; } = -1;

    /// <summary>Which view the user came from (used for back-navigation).</summary>
    public static string ReturnScene { get; set; } = "MainMenu";

    /// <summary>Helper: returns true if a valid item with a reference is selected.</summary>
    public static bool HasValidItem =>
        SelectedItem != null && SelectedItem.jewelryReference != null && SelectedItem.jewelryReference.RuntimeKeyIsValid();
}
