using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

/// <summary>
/// EarAttachARCore - v7 + Yaw Occlusion Hiding
///
/// ONLY CHANGE from your v7:
///   Added camera-relative yaw hiding.
///   Turn RIGHT → LEFT  earring hides.
///   Turn LEFT  → RIGHT earring hides.
///
///   New Inspector fields (under "── Yaw Occlusion Hiding ──"):
///     hideThreshold  = 0.30  (hiding starts at ~17° turn)
///     hideFullAt     = 0.60  (fully hidden at ~37° turn)
///
/// Everything else is exactly your original v7 code.
/// </summary>

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
    [Header("── Yaw Occlusion Hiding ────────────────────────────────────────")]

    [Tooltip("HOW IT WORKS:\n\n" +
             "  yawAmount = dot(face.right, camera.forward)\n\n" +
             "  +1 = fully turned RIGHT → LEFT  earring hides\n" +
             "  -1 = fully turned LEFT  → RIGHT earring hides\n" +
             "   0 = facing camera      → both visible\n\n" +
             "Hiding starts when |yawAmount| reaches this value.\n" +
             "Default 0.30 ≈ 17° head turn.\n" +
             "Lower = hides sooner.  Higher = hides later.")]
    [Range(0.05f, 0.80f)]
    public float hideThreshold = 0.30f;

    [Tooltip("Dot value at which the far earring is FULLY hidden.\n" +
             "Default 0.60 ≈ 37° head turn.\n" +
             "Must be greater than hideThreshold.")]
    [Range(0.20f, 1.00f)]
    public float hideFullAt = 0.60f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Kalman Filter ──────────────────────────────────────────")]

    [Tooltip("Process noise Q — higher = more responsive, more jitter.\n" +
             "Good range: 0.001 – 0.05.  Default 0.01.")]
    [Range(0.0001f, 0.1f)]
    public float kalmanProcessNoise = 0.01f;

    [Tooltip("Measurement noise R — how noisy the raw face-mesh position is.\n" +
             "Higher = smoother. Good range: 0.01 – 0.5.  Default 0.1.")]
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
    [Header("── Per-Ear Overrides ──────────────────────────────────────────")]
    [Tooltip("Enable to show per-ear colour hints in the Scene Gizmos.")]
    public bool usePerEarOverrides = false;

    // ── Private ─────────────────────────────────────────────────────────────
    private ARFace face;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private bool registered = false;
    private Transform leftLastChild = null;
    private Transform rightLastChild = null;

    // Yaw fade state
    private float leftFade = 1f;
    private float rightFade = 1f;

    private KalmanVector3 leftKalman;
    private KalmanVector3 rightKalman;

    // ════════════════════════════════════════════════════════════════════════
    //  Kalman filter — unchanged from v7
    // ════════════════════════════════════════════════════════════════════════
    private class KalmanAxis
    {
        private float x;
        private float p = 1f;
        private float q;
        private float r;
        private bool init = false;

        public KalmanAxis(float q, float r) { this.q = q; this.r = r; }

        public float Feed(float z)
        {
            if (!init) { x = z; init = true; return x; }
            p = p + q;
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
            kx.SetNoise(q, r);
            ky.SetNoise(q, r);
            kz.SetNoise(q, r);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
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
        Debug.Log("[EarAttachARCore] v7+OcclusionHiding registered. " +
                  $"Q={kalmanProcessNoise}  R={kalmanMeasureNoise}  " +
                  $"hideAt={hideThreshold:F2}→{hideFullAt:F2}");
    }

    void Update()
    {
        if (!registered || face == null) return;
        if (face.trackingState != TrackingState.Tracking) return;

        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        leftKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);
        rightKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);

        // ── 1. Face dimensions — unchanged from v7 ─────────────────────────
        Vector3 chinW = face.transform.TransformPoint(face.vertices[CHIN_VERTEX]);
        Vector3 browW = face.transform.TransformPoint(face.vertices[BROW_VERTEX]);
        Vector3 lCheekW = face.transform.TransformPoint(face.vertices[LEFT_CHEEK]);
        Vector3 rCheekW = face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]);

        float faceH = Vector3.Distance(browW, chinW);
        float faceW = Vector3.Distance(lCheekW, rCheekW);
        float drop = faceH * lobeDropFraction;
        float outward = faceW * outwardFraction;
        float fwd = faceH * forwardFraction;

        // ── 2. Raw positions — unchanged from v7 ───────────────────────────
        Vector3 leftRaw = lCheekW
            + face.transform.up * -drop
            + face.transform.right * -outward
            + face.transform.forward * fwd;

        Vector3 rightRaw = rCheekW
            + face.transform.up * -drop
            + face.transform.right * outward
            + face.transform.forward * fwd;

        // ── 3. Kalman filter — unchanged from v7 ───────────────────────────
        leftAnchor.position = leftKalman.Update(leftRaw);
        rightAnchor.position = rightKalman.Update(rightRaw);

        // ── 4. Rotation — unchanged from v7 ───────────────────────────────
        float rt = rotSmoothing * Time.deltaTime;
        leftAnchor.rotation = Quaternion.Slerp(leftAnchor.rotation, ComputeRot(true), rt);
        rightAnchor.rotation = Quaternion.Slerp(rightAnchor.rotation, ComputeRot(false), rt);

        // ── 5. YAW OCCLUSION HIDING ← only new code ───────────────────────
        //
        //  yawAmount = dot(face.right, camera.forward)
        //
        //  face.right     = points from subject's right toward subject's left
        //  camera.forward = points from camera toward the subject
        //
        //  yawAmount > 0  →  face turned RIGHT  →  LEFT  ear is far  →  hide LEFT
        //  yawAmount < 0  →  face turned LEFT   →  RIGHT ear is far  →  hide RIGHT
        //  yawAmount ≈ 0  →  face straight-on   →  both visible
        //
        float yawAmount = 0f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            // NOTE: In ARFoundation the face mesh is mirrored (it faces the camera),
            // so face.transform.right actually points toward the subject's LEFT.
            // Negate so that positive yawAmount = face turned RIGHT (left ear goes far).
            yawAmount = -Vector3.Dot(face.transform.right, cam.transform.forward);
        }

        float hideT = Mathf.Clamp01(
            Mathf.InverseLerp(hideThreshold, hideFullAt, Mathf.Abs(yawAmount))
        );
        float farVis = 1f - hideT;   // 1 = visible, 0 = hidden

        float leftTarget, rightTarget;
        if (yawAmount >= 0f)
        {
            // Face turned RIGHT → subject's LEFT ear goes behind → hide LEFT
            leftTarget = farVis;
            rightTarget = 1f;
        }
        else
        {
            // Face turned LEFT → subject's RIGHT ear goes behind → hide RIGHT
            leftTarget = 1f;
            rightTarget = farVis;
        }

        leftFade = leftTarget;
        rightFade = rightTarget;

        ApplyFade(leftAnchor, leftFade);
        ApplyFade(rightAnchor, rightFade);
    }

    // Instantly shows or hides the earring child using SetActive.
    // fade >= 0.5 = visible, fade < 0.5 = hidden.
    void ApplyFade(Transform anchor, float fade)
    {
        if (anchor.childCount == 0) return;
        Transform child = anchor.GetChild(0);
        if (child == null) return;

        child.gameObject.SetActive(fade >= 0.5f);
    }

    // ── Rotation — unchanged from v7 ───────────────────────────────────────
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

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || face == null) return;
        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[LEFT_CHEEK]), 0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]), 0.003f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[CHIN_VERTEX]), 0.002f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[BROW_VERTEX]), 0.002f);
        Gizmos.color = Color.yellow;
        if (leftAnchor != null) Gizmos.DrawWireSphere(leftAnchor.position, 0.006f);
        if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.006f);

        if (usePerEarOverrides)
        {
            Gizmos.color = Color.green;
            if (leftAnchor != null) Gizmos.DrawWireSphere(leftAnchor.position, 0.008f);
            Gizmos.color = Color.red;
            if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.008f);
        }
    }
}

/// <summary>
/// Placeholder kept for backwards compatibility. No longer used.
/// </summary>
public class EarringScaleTag : MonoBehaviour { }