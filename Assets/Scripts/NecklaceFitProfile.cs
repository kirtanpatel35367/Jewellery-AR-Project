using UnityEngine;

/// <summary>
/// Attach this to EACH necklace prefab root (necklace_gold, egyptian_necklace, etc).
/// It applies per-model correction:
/// - Local offset (position)
/// - Local rotation
/// - Auto scale based on renderer bounds to match a target width in meters
/// </summary>
public class NecklaceFitProfile : MonoBehaviour
{
    [Header("Auto Fit Scale")]
    [Tooltip("If true, the script will auto-scale the model so its width matches TargetWidthMeters.")]
    public bool autoScale = true;

    [Tooltip("Necklace width in meters in AR space (typical: 0.14 to 0.22).")]
    public float targetWidthMeters = 0.18f;

    [Tooltip("Multiply final scale (use 1 normally; use 0.5 or 2 if you want quick tuning).")]
    public float scaleMultiplier = 1f;

    [Header("Placement Correction (relative to NecklaceAnchor)")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Tooltip("Rotation correction in degrees (local Euler).")]
    public Vector3 localRotationOffsetEuler = Vector3.zero;

    [Header("Optional: Use specific child as visual root")]
    [Tooltip("If your prefab has an extra parent/root, drag the visual child here. Leave empty to use this object.")]
    public Transform visualRoot;

    /// <summary>Call after instantiating & parenting under the NecklaceAnchor.</summary>
    public void Apply()
    {
        Transform root = (visualRoot != null) ? visualRoot : transform;

        // Apply position/rotation offsets first
        root.localPosition += localPositionOffset;
        root.localRotation = Quaternion.Euler(localRotationOffsetEuler) * root.localRotation;

        if (!autoScale)
        {
            root.localScale *= scaleMultiplier;
            return;
        }

        // Auto-scale to target width using renderer bounds
        float currentWidth = GetWorldWidth(root);
        if (currentWidth <= 0.00001f)
        {
            // If mesh has no renderers, just apply multiplier
            root.localScale *= scaleMultiplier;
            return;
        }

        // We are already parented to NecklaceAnchor, so root.localScale changes will affect world scale.
        // We compute ratio for width correction:
        float ratio = targetWidthMeters / currentWidth;
        root.localScale = root.localScale * ratio * scaleMultiplier;
    }

    float GetWorldWidth(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return 0f;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        // width across XZ plane can vary by model orientation,
        // so we take the max of X and Z as "width".
        float wX = b.size.x;
        float wZ = b.size.z;
        return Mathf.Max(wX, wZ);
    }
}
