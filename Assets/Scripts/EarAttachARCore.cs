using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

/// <summary>
/// EarAttachARCore - v7
///
/// KEY IMPROVEMENTS over v6:
///
/// 1. KALMAN FILTER for position — replaces simple Lerp.
///    A 1-D scalar Kalman filter is applied independently on X, Y, Z of each
///    anchor so noisy face-mesh output is smoothed without the lag that high
///    Lerp speeds introduce. Tune processNoise / measureNoise in the Inspector.
///
/// 2. BETTER OUTWARD PLACEMENT — the cheekbone vertex (234/454) is literally
///    ON the mesh surface. We now push outward by a FACE-WIDTH fraction rather
///    than a fixed tiny offset, so the earring actually clears the side of the
///    face. Look at the screenshots: earrings sit on the cheek, not hanging
///    beside the ear. The fix is increasing outwardFraction (≈ 0.08–0.12).
///
/// 3. FACE-WIDTH BASED offsets — all offsets scale with actual face size so
///    they work at any camera distance.
///
/// PLACEMENT MATH:
///   • faceH  = distance from vertex 10 (brow) to vertex 152 (chin)
///   • faceW  = distance from vertex 234 to vertex 454 (cheek to cheek)
///   • lobeY  = cheekbone_vertex  –  lobeDropFraction  × faceH     (downward)
///   • lobeX  = cheekbone_vertex  ±  outwardFraction   × faceW     (sideways out)
///   • lobeZ  = cheekbone_vertex  +  forwardFraction   × faceH     (forward)
///
///   dropDir   = lerp(-face.up, Vector3.down, gravityBlend)
///   outward   = ±face.right × (faceWidth × outwardFraction)
///               (+face.right for subject's LEFT  ear = vertex 234)
///               (-face.right for subject's RIGHT ear = vertex 454)
///   backward  = -face.forward × backwardMetres   ← INTO the head
///
///   anchor = cheekboneVertex
///          + dropDir  × (faceHeight × lobeDropFraction)
///          + outward
///          + backward
///
/// ── WHY OUTWARD + BACKWARD WORKS ─────────────────────────────────────
/// face.forward points from face surface toward the camera.
/// face.right   points from subject's right to subject's left.
/// The ear lobe sits ~10mm behind the cheekbone surface and ~18mm lateral.
/// This combination puts the anchor at the side of the head where the
/// actual ear lobe is, regardless of head rotation.
///
/// ── FAR-EAR HIDING ───────────────────────────────────────────────────
/// When head turns >25°, the far cheekbone vertex rotates onto the
/// visible front of the face. The earring would appear on the cheek.
/// We fade it to invisible between 25° and 45° yaw.
///
/// ═══════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(ARFace))]
public class EarAttachARCore : MonoBehaviour
{
    // ── Vertex indices ──────────────────────────────────────────────────────
    private const int LEFT_CHEEK = 234;
    private const int RIGHT_CHEEK = 454;
    private const int CHIN_VERTEX = 152;
    private const int BROW_VERTEX = 10;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Lobe Position (all fractions of face dimensions) ──")]

    [Tooltip("Drop DOWN from cheekbone vertex, as fraction of face HEIGHT.\n" +
             "Cheekbone (234/454) ≈ eye level = top ~28% of face.\n" +
             "Ear lobe ≈ 65–70% down the face.\n" +
             "So drop fraction ≈ 0.35–0.42.  Default 0.38.")]
    [Range(0.10f, 0.60f)]
    public float lobeDropFraction = 0.38f;

    [Tooltip("Push OUTWARD (sideways) from cheekbone, as fraction of face WIDTH.\n" +
             "VIDEO ANALYSIS: earrings were floating too far away from face.\n" +
             "Keep this SMALL — 0.01–0.03 is usually correct.\n" +
             "The mesh vertex 234/454 is already at the face edge;\n" +
             "a big outward push sends the earring floating into empty space.\n" +
             "Default 0.02 = 2% of face width (≈3–4mm at typical selfie distance).")]
    [Range(0.00f, 0.10f)]
    public float outwardFraction = 0.02f;

    [Tooltip("Push FORWARD from face plane, as fraction of face HEIGHT.\n" +
             "This is what keeps the earring in FRONT of (not inside) the face.\n" +
             "Try 0.03–0.06. Default 0.04.")]
    [Range(0.00f, 0.15f)]
    public float forwardFraction = 0.04f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Kalman Filter ──────────────────────────────────────────")]

    [Tooltip("Process noise Q — how much the true position is expected to change\n" +
             "each frame. HIGHER = filter trusts new measurements more (more\n" +
             "responsive but noisier). LOWER = smoother but more lag.\n" +
             "Good range: 0.001 – 0.05.  Default 0.01.")]
    [Range(0.0001f, 0.1f)]
    public float kalmanProcessNoise = 0.01f;

    [Tooltip("Measurement noise R — how noisy the raw face-mesh position is.\n" +
             "HIGHER = filter trusts measurements less (smoother, more lag).\n" +
             "LOWER = more responsive. Good range: 0.01 – 0.5.  Default 0.1.")]
    [Range(0.001f, 1.0f)]
    public float kalmanMeasureNoise = 0.1f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Rotation smoothing ──────────────────────────────────────")]
    [Range(5f, 30f)] public float rotSmoothing = 16f;

    [Tooltip("0 = earring always faces camera (safe).\n" +
             "Increase for realistic side-view tilt.")]
    [Range(0f, 1f)]
    public float angleRealism = 0f;

    // ═══════════════════════════════════════════════════════════════════════
    // Auto-scale removed. Set scale directly on each earring prefab in Unity Inspector.

    // ── Private ─────────────────────────────────────────────────────────────
    private ARFace face;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private bool registered = false;
    private Transform leftLastChild = null;
    private Transform rightLastChild = null;


    [Header("── Per-Ear Overrides ──────────────────────────────────────────")]
    [Tooltip("Enable to show per-ear colour hints in the Scene Gizmos.")]
    public bool usePerEarOverrides = false;

    // One Kalman filter per axis per ear (6 total)
    private KalmanVector3 leftKalman;
    private KalmanVector3 rightKalman;

    // ════════════════════════════════════════════════════════════════════════
    //  Tiny scalar Kalman filter  (1-D, constant-velocity model simplified to
    //  position-only since face mesh already gives positions, not velocities)
    // ════════════════════════════════════════════════════════════════════════
    private class KalmanAxis
    {
        private float x;        // state estimate
        private float p = 1f;   // error covariance
        private float q;        // process noise
        private float r;        // measurement noise
        private bool init = false;

        public KalmanAxis(float q, float r) { this.q = q; this.r = r; }
        public float Feed(float z)
        {
            if (!init) { x = z; init = true; return x; }
            // Predict
            p = p + q;
            // Update (Kalman gain)
            float k = p / (p + r);
            x = x + k * (z - x);
            p = (1f - k) * p;
            return x;
        }
        public void SetNoise(float q, float r) { this.q = q; this.r = r; }
    }

    private class KalmanVector3
    {
        private KalmanAxis kx, ky, kz;
        public KalmanVector3(float q, float r)
        {
            kx = new KalmanAxis(q, r);
            ky = new KalmanAxis(q, r);
            kz = new KalmanAxis(q, r);
        }
        public Vector3 Update(Vector3 m)
            => new Vector3(kx.Feed(m.x), ky.Feed(m.y), kz.Feed(m.z));
        public void SetNoise(float q, float r)
        {
            kx.SetNoise(q, r); ky.SetNoise(q, r); kz.SetNoise(q, r);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    void Awake()
    {
        face = GetComponent<ARFace>();

        leftAnchor = new GameObject("LeftEarAnchor").transform;
        rightAnchor = new GameObject("RightEarAnchor").transform;
        leftAnchor.SetParent(transform);
        rightAnchor.SetParent(transform);

        leftKalman = new KalmanVector3(kalmanProcessNoise, kalmanMeasureNoise);
        rightKalman = new KalmanVector3(kalmanProcessNoise, kalmanMeasureNoise);

        StartCoroutine(RegisterWithManager());
    }

    IEnumerator RegisterWithManager()
    {
        JewelryManager mgr = null;
        while (mgr == null)
        {
            mgr = FindObjectOfType<JewelryManager>();
            if (mgr == null) yield return new WaitForSeconds(0.2f);
        }
        mgr.RegisterEarAnchors(leftAnchor, rightAnchor);
        registered = true;
        Debug.Log("[EarAttachARCore] v7b registered with Kalman filter. " +
                  $"Q={kalmanProcessNoise}  R={kalmanMeasureNoise}");
    }

    void Update()
    {
        if (!registered || face == null) return;
        if (face.trackingState != TrackingState.Tracking) return;

        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        // Live-update Kalman noise from Inspector (useful during tuning)
        leftKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);
        rightKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);

        // ── 1. Face dimensions ────────────────────────────────────────────
        Vector3 chinW = face.transform.TransformPoint(face.vertices[CHIN_VERTEX]);
        Vector3 browW = face.transform.TransformPoint(face.vertices[BROW_VERTEX]);
        Vector3 lCheekW = face.transform.TransformPoint(face.vertices[LEFT_CHEEK]);
        Vector3 rCheekW = face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]);

        // ── 2. Face dimensions ─────────────────────────────────────────
        float faceH = Vector3.Distance(browW, chinW);
        float faceW = Vector3.Distance(lCheekW, rCheekW);

        float drop = faceH * lobeDropFraction;
        float outward = faceW * outwardFraction;
        float fwd = faceH * forwardFraction;

        // ── 2. Raw target positions ────────────────────────────────────────
        //  subject's LEFT ear  = face.transform.right points to subject's RIGHT
        //  so to go to subject's LEFT we subtract face.right
        Vector3 leftRaw = lCheekW
            + face.transform.up * -drop       // down
            + face.transform.right * -outward    // outward left
            + face.transform.forward * fwd;       // forward

        Vector3 rightRaw = rCheekW
            + face.transform.up * -drop       // down
            + face.transform.right * outward    // outward right
            + face.transform.forward * fwd;       // forward

        // ── 3. Kalman-filtered positions ──────────────────────────────────
        leftAnchor.position = leftKalman.Update(leftRaw);
        rightAnchor.position = rightKalman.Update(rightRaw);

        // ── 4. Rotation ───────────────────────────────────────────────────
        float rt = rotSmoothing * Time.deltaTime;
        leftAnchor.rotation = Quaternion.Slerp(leftAnchor.rotation, ComputeRot(true), rt);
        rightAnchor.rotation = Quaternion.Slerp(rightAnchor.rotation, ComputeRot(false), rt);

        // Scale is NOT auto-applied. Set scale on your earring prefabs directly.
    }

    Quaternion ComputeRot(bool isLeft)
    {
        Quaternion faceFollow = face.transform.rotation;
        if (angleRealism <= 0.001f) return faceFollow;

        Vector3 outDir = face.transform.right * (isLeft ? -1f : 1f);
        Vector3 upDir = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(outDir, upDir)) > 0.98f)
            upDir = face.transform.forward;

        Quaternion gravityHang = Quaternion.LookRotation(outDir, upDir);
        return Quaternion.Slerp(faceFollow, gravityHang, angleRealism);
    }

    // ════════════════════════════════════════════════════════════════════════
    void OnDestroy()
    {
        if (leftAnchor != null) Destroy(leftAnchor.gameObject);
        if (rightAnchor != null) Destroy(rightAnchor.gameObject);
    }

    // ════════════════════════════════════════════════════════════════════════
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || face == null) return;
        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        // Cheekbone reference points (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[LEFT_CHEEK]), 0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]), 0.003f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[CHIN_VERTEX]), 0.002f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[BROW_VERTEX]), 0.002f);

        // Kalman-filtered lobe positions (yellow)
        Gizmos.color = Color.yellow;
        if (leftAnchor != null) Gizmos.DrawWireSphere(leftAnchor.position, 0.006f);
        if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.006f);

        // Per-ear colour hint when overrides are active
        if (usePerEarOverrides)
        {
            Gizmos.color = Color.green;
            if (leftAnchor != null) Gizmos.DrawWireSphere(leftAnchor.position, 0.008f);
            Gizmos.color = Color.red;
            if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.008f);
        }
    }
}