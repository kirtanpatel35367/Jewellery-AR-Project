using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// NecklaceAttachARCore
/// ────────────────────
/// Creates one invisible anchor point that tracks the neck/chin area
/// on the detected ARCore face mesh.
///
/// Registers that anchor with JewelryManager so the necklace spawned
/// by the UI is automatically parented to the correct neck position.
///
/// Nothing is ever rendered by this script.
/// </summary>
[RequireComponent(typeof(ARFace))]
public class NecklaceAttachARCore : MonoBehaviour
{
    // Chin / neck vertex indices in the 468-point ARCore face mesh
    private static readonly int[] NECK_VERTS = { 152, 175, 400, 148, 377 };

    [Header("Position  (tweak if necklace sits in wrong place)")]
    public float neckDrop = 0.22f;       // how far below the chin
    public float forward = 0.02f;       // push forward from face plane
    public Vector3 extraOffset = Vector3.zero; // manual fine-tune

    [Header("Smoothing")]
    public float posSmooth = 4f;
    public float rotSmooth = 3f;

    [Header("Rotation follow")]
    public bool lockPitch = true;
    public bool lockRoll = true;
    [Range(0f, 1f)]
    public float yawFollow = 0.05f;   // 0 = ignore head-turn, 1 = full follow

    [Header("Stability")]
[Range(0f, 1f)]
public float faceMovementDamp = 0.85f;  // 0 = full follow, 1 = fully locked

    private ARFace face;
    private Transform neckAnchor;
    private Vector3 vel;
    private bool anchorRegistered = false;

    void Awake()
    {
        face = GetComponent<ARFace>();

        // Anchor is parented to the ARFace transform so it stays in the
        // right coordinate space; smoothing is applied on top in Update().
        neckAnchor = new GameObject("NecklaceAnchor").transform;
        neckAnchor.SetParent(transform);

        RegisterWithManager();
    }

    void OnEnable()
    {
        // Re-register if the ARFace was recycled after tracking loss
        if (!anchorRegistered)
            RegisterWithManager();
    }

    void RegisterWithManager()
    {
        JewelryManager mgr = FindObjectOfType<JewelryManager>();
        if (mgr != null)
        {
            mgr.RegisterNecklaceAnchor(neckAnchor);
            anchorRegistered = true;
            Debug.Log("[NecklaceAttachARCore] Necklace anchor registered.");
        }
        else
        {
            Debug.LogError("[NecklaceAttachARCore] JewelryManager not found!");
        }
    }

void Update()
{
    if (face == null || neckAnchor == null) return;
    if (face.trackingState == TrackingState.None ||
        face.trackingState == TrackingState.Limited) return;

    // Calculate the exact target position based on the face mesh
    Vector3 targetPosition = TargetPos();
    Quaternion targetRotation = TargetRot();
    
    neckAnchor.position = targetPosition;
    neckAnchor.rotation = targetRotation;
}

Vector3 TargetPos()
{
    Vector3 center = Vector3.zero;
    int count = 0;
    foreach (int idx in NECK_VERTS)
    {
        if (idx < face.vertices.Length)
        {
            center += face.transform.TransformPoint(face.vertices[idx]);
            count++;
        }
    }
    if (count == 0) return neckAnchor.position;
    center /= count;

    // Drop in WORLD space (gravity direction) — not face-relative
    // This means tilting your head won't change where "down" is
    return center
         + Vector3.down * neckDrop          // ← WORLD down, not face down
         + face.transform.forward * forward
         + extraOffset;
}

    Quaternion TargetRot()
    {
        Vector3 e = face.transform.eulerAngles;
        return Quaternion.Euler(
            lockPitch ? 0f : e.x,
            Mathf.LerpAngle(0f, e.y, yawFollow),
            lockRoll ? 0f : e.z);
    }

    void OnDestroy()
    {
        if (neckAnchor != null) Destroy(neckAnchor.gameObject);
    }
}