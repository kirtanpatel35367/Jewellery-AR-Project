using UnityEngine;

/// <summary>
/// RuntimeNecklaceFixer
/// Applies hard runtime fixes for problematic necklace prefabs
/// after JewelryManager instantiates them.
/// </summary>
public class RuntimeNecklaceFixer : MonoBehaviour
{
    public static void Apply(GameObject go)
    {
        if (go == null) return;

        string n = go.name.ToLower();

        // Always normalize root first
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        // keep existing prefab scale first, do not blindly reset unless needed

        // ---------------- EGYPTIAN ----------------
        if (n.Contains("egyptian"))
        {
            ApplyEgyptianFix(go);
            return;
        }

        // ---------------- GOLD ----------------
        if (n.Contains("gold"))
        {
            ApplyGoldFix(go);
            return;
        }
    }

static void ApplyEgyptianFix(GameObject go)
{
    // Lower it from collarbone/face area to necklace area
    go.transform.localPosition = new Vector3(0f, -0.125f, -0.020f);
    go.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
    go.transform.localScale = Vector3.one * 0.16f;

    DisableMotion(go);

    Debug.Log("[RuntimeNecklaceFixer] Applied Egyptian fix to: " + go.name);
}

static void ApplyGoldFix(GameObject go)
{
    // Raise pendant and reduce overall size
    go.transform.localPosition = new Vector3(0f, -0.030f, -0.010f);
    go.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
    go.transform.localScale = Vector3.one * 0.0011f;

    DisableMotion(go);

    Debug.Log("[RuntimeNecklaceFixer] Applied Gold fix to: " + go.name);
}
    static void DisableMotion(GameObject go)
    {
        NecklaceMotion motion = go.GetComponent<NecklaceMotion>();
        if (motion != null)
        {
            motion.swayAmount = 0f;
            motion.swaySpeed = 0f;
        }
    }
}