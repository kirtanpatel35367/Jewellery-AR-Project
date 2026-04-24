using UnityEngine;

/// <summary>
/// NecklaceAutoFit v4
/// ───────────────────
/// Add to the ROOT of every necklace prefab.
///
/// NEW in v4:
///   • maxWorldHeight clamps how far the necklace can extend downward.
///     Set it to match your neckDrop so it only fills the neck area,
///     not the chest.
///   • Face-height-aware scaling: optional auto-scale so the necklace
///     width matches the neck width regardless of camera distance.
///
/// HOW POSITIONING WORKS:
///   The NecklaceAnchor (set by NecklaceAttachARCore) is placed AT the
///   top of where the necklace should sit. This script shifts the prefab
///   so its top-center mesh edge aligns with the anchor, then the model
///   hangs DOWNWARD from there.
///
/// TUNING GUIDE:
///   manualOffset.Y  : raise (+) or lower (-) in 0.005 steps
///   manualOffset.Z  : push toward camera (+) in 0.005 steps
///   scaleMultiplier : if necklace is too wide/narrow for the neck
/// </summary>
public class NecklaceAutoFit : MonoBehaviour
{
    [Header("Per-model Fine-tune")]
    [Tooltip("Y = raise/lower from auto position. Each 0.01 = ~1cm.\n" +
             "Z = push toward camera (+) or into neck (-).")]
    public Vector3 manualOffset = Vector3.zero;

    [Tooltip("Uniform scale multiplier.\n" +
             "1 = use the prefab's original scale.\n" +
             "Reduce if necklace is too wide for the neck (e.g. 0.7).\n" +
             "Increase if too small (e.g. 1.3).")]
    [Range(0.01f, 10f)]
    public float scaleMultiplier = 1f;

    [Tooltip("Extra rotation. Use rotationOffset.X to tilt necklace forward (e.g. 10-15 degrees).")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Debug")]
    public bool showBoundsGizmo = true;

    // ── Private ───────────────────────────────────────────────────────────
    private Bounds _bounds;
    private bool _boundsValid;
    private bool _applied;

    // Called by JewelryManager.SpawnNecklace() after parenting.
    // DO NOT call from Start() — that causes double-apply.
    public void Apply()
    {
        if (_applied) return;
        _applied = true;

        // 1. Apply scale
        if (!Mathf.Approximately(scaleMultiplier, 1f))
            transform.localScale *= scaleMultiplier;

        // 2. Apply rotation
        if (rotationOffset != Vector3.zero)
            transform.localRotation *= Quaternion.Euler(rotationOffset);

        // 3. Measure world bounds AFTER parenting and scale/rotation applied
        _bounds = MeasureWorldBounds();
        if (!_boundsValid)
        {
            Debug.LogWarning($"[NecklaceAutoFit] '{name}' — no Renderers. Cannot auto-fit.");
            transform.localPosition = manualOffset;
            return;
        }

        // 4. Align TOP-CENTER of mesh to anchor position
        //    Anchor = top of necklace area (collarbone/upper neck level)
        //    We shift Y so mesh top edge = anchor.y
        //    X/Z center of mesh = anchor.x/z
        Vector3 anchor = transform.parent != null
            ? transform.parent.position
            : Vector3.zero;

        // Shift so mesh top-center lands at anchor
        Vector3 meshTopCenter = new Vector3(_bounds.center.x, _bounds.max.y, _bounds.center.z);
        Vector3 worldShift = anchor - meshTopCenter;
        transform.position += worldShift;

        // 5. Manual fine-tune in LOCAL space
        transform.localPosition += manualOffset;

        Debug.Log($"[NecklaceAutoFit] '{name}' — " +
                  $"meshSize:{_bounds.size:F3}  shift:{worldShift:F3}  " +
                  $"finalLocal:{transform.localPosition:F3}");
    }

    // ─────────────────────────────────────────────────────────────────────
    Bounds MeasureWorldBounds()
    {
        Renderer[] r = GetComponentsInChildren<Renderer>(false);
        if (r.Length == 0) r = GetComponentsInChildren<Renderer>(true);
        if (r.Length == 0) { _boundsValid = false; return default; }
        Bounds b = r[0].bounds;
        for (int i = 1; i < r.Length; i++) b.Encapsulate(r[i].bounds);
        _boundsValid = true;
        return b;
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (!showBoundsGizmo || !_boundsValid) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(_bounds.center, _bounds.size);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(
            new Vector3(_bounds.center.x, _bounds.max.y, _bounds.center.z), 0.004f);
    }
}