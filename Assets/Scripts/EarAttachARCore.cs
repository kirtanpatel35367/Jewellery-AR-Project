using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections;

/// <summary>
/// EarAttachARCore - v8
///
/// KEY IMPROVEMENTS over v7:
///
/// 1. PER-EAR INDEPENDENT CONTROLS
///    Each ear now has its own outward, forward, and drop fraction so you can
///    fine-tune the right-ear gap (or any asymmetry) without disturbing the
///    left ear. A "usePerEarOverrides" toggle lets you share one global value
///    OR override per ear — no code changes needed, just Inspector toggles.
///
/// 2. RIGHT-EAR GAP FIX
///    The gap between the right earring and ear was caused by both ears sharing
///    the same outwardFraction. MediaPipe vertex 454 (right cheekbone) often
///    sits slightly differently to 234 (left), causing visible asymmetry.
///    Solution: rightOutwardFraction can be set independently (try 0.00–0.01).
///
/// 3. ALL v7 FEATURES RETAINED
///    Kalman filter, face-width based offsets, auto-scale, rotation smoothing.
///
/// QUICK TUNING GUIDE:
///   Earring too low on cheek   → decrease lobeDropFraction  (try 0.20–0.28)
///   Right ear gap (too far out) → decrease rightOutwardFraction (try 0.00–0.01)
///   Left ear gap (too far out)  → decrease leftOutwardFraction  (try 0.00–0.01)
///   Earring clipping into face  → increase forwardFraction (try 0.05–0.08)
///   Earring floating in front   → decrease forwardFraction
///
/// VERTEX MAP (MediaPipe 468-point):
///   234 = left outer cheekbone  (subject's left, camera's right)
///   454 = right outer cheekbone (subject's right, camera's left)
///   152 = chin bottom
///    10 = forehead top (mid-brow)
/// </summary>
[RequireComponent(typeof(ARFace))]
public class EarAttachARCore : MonoBehaviour
{
    // ── Vertex indices ──────────────────────────────────────────────────────
    private const int LEFT_CHEEK  = 234;
    private const int RIGHT_CHEEK = 454;
    private const int CHIN_VERTEX = 152;
    private const int BROW_VERTEX = 10;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Global Lobe Position (shared when Per-Ear Overrides OFF) ──")]

    [Tooltip("Drop DOWN from cheekbone vertex, as fraction of face HEIGHT.\n" +
             "Lower value = earring moves UP toward the ear.\n" +
             "Ear lobe sits at roughly 0.22–0.30 of face height below cheekbone.\n" +
             "Default 0.25 (lifted from v7's 0.38).")]
    [Range(0.10f, 0.60f)]
    public float lobeDropFraction = 0.25f;

    [Tooltip("Push OUTWARD (sideways) from cheekbone vertex, as fraction of face WIDTH.\n" +
             "Keep SMALL — vertex 234/454 is already at face edge.\n" +
             "Default 0.02.")]
    [Range(0.00f, 0.10f)]
    public float outwardFraction = 0.02f;

    [Tooltip("Push FORWARD from face plane, as fraction of face HEIGHT.\n" +
             "Keeps earring in front of (not inside) the face surface.\n" +
             "Default 0.05.")]
    [Range(0.00f, 0.15f)]
    public float forwardFraction = 0.05f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Per-Ear Overrides (enable to fix asymmetry / right-ear gap) ──")]

    [Tooltip("Enable to set LEFT and RIGHT ear values independently.\n" +
             "Use this to fix the right-ear gap without moving the left ear.")]
    public bool usePerEarOverrides = false;

    [Header("   Left Ear Overrides")]
    [Range(0.10f, 0.60f)] public float leftDropFraction    = 0.25f;
    [Range(0.00f, 0.10f)] public float leftOutwardFraction = 0.02f;
    [Range(0.00f, 0.15f)] public float leftForwardFraction = 0.05f;

    [Header("   Right Ear Overrides")]
    [Tooltip("RIGHT EAR GAP FIX: reduce this value toward 0.00 to close the\n" +
             "gap between the right earring and the ear.\n" +
             "Start at 0.00 and nudge up until it just touches without clipping.")]
    [Range(0.10f, 0.60f)] public float rightDropFraction    = 0.25f;
    [Range(0.00f, 0.10f)] public float rightOutwardFraction = 0.00f;  // ← gap fix default
    [Range(0.00f, 0.15f)] public float rightForwardFraction = 0.05f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Kalman Filter ──────────────────────────────────────────────")]

    [Tooltip("Process noise Q — HIGHER = more responsive but noisier.\n" +
             "Good range: 0.001 – 0.05.  Default 0.01.")]
    [Range(0.0001f, 0.1f)]
    public float kalmanProcessNoise = 0.01f;

    [Tooltip("Measurement noise R — HIGHER = smoother but more lag.\n" +
             "Good range: 0.01 – 0.5.  Default 0.1.")]
    [Range(0.001f, 1.0f)]
    public float kalmanMeasureNoise = 0.1f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Rotation Smoothing ─────────────────────────────────────────")]
    [Range(5f, 30f)]
    public float rotSmoothing = 16f;

    [Tooltip("0 = earring always faces camera (safe default).\n" +
             "Increase toward 1 for realistic side-view tilt.")]
    [Range(0f, 1f)]
    public float angleRealism = 0f;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Scale ──────────────────────────────────────────────────────")]
    [Tooltip("Target earring size in metres (longest dimension).  2.2 cm = 0.022.")]
    public float targetSizeMetres = 0.022f;
    public bool autoScale = true;

    // ── Private ─────────────────────────────────────────────────────────────
    private ARFace    face;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private bool      registered    = false;
    private Transform leftLastChild  = null;
    private Transform rightLastChild = null;

    private KalmanVector3 leftKalman;
    private KalmanVector3 rightKalman;

    // ════════════════════════════════════════════════════════════════════════
    //  Scalar Kalman filter — 1-D, position-only
    // ════════════════════════════════════════════════════════════════════════
    private class KalmanAxis
    {
        private float x;
        private float p = 1f;
        private float q;
        private float r;
        private bool  init = false;

        public KalmanAxis(float q, float r) { this.q = q; this.r = r; }

        public float Update(float measurement)
        {
            if (!init) { x = measurement; init = true; return x; }
            p += q;
            float k = p / (p + r);
            x += k * (measurement - x);
            p  = (1f - k) * p;
            return x;
        }

        public void SetNoise(float newQ, float newR) { q = newQ; r = newR; }
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
            => new Vector3(kx.Update(m.x), ky.Update(m.y), kz.Update(m.z));

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

        leftAnchor  = new GameObject("LeftEarAnchor").transform;
        rightAnchor = new GameObject("RightEarAnchor").transform;
        leftAnchor.SetParent(transform);
        rightAnchor.SetParent(transform);

        leftKalman  = new KalmanVector3(kalmanProcessNoise, kalmanMeasureNoise);
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
        Debug.Log("[EarAttachARCore] v8 registered. " +
                  $"PerEarOverrides={usePerEarOverrides}  " +
                  $"Q={kalmanProcessNoise}  R={kalmanMeasureNoise}");
    }

    void Update()
    {
        if (!registered || face == null) return;
        if (face.trackingState != TrackingState.Tracking) return;

        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        // Live-update Kalman noise (Inspector tuning without recompile)
        leftKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);
        rightKalman.SetNoise(kalmanProcessNoise, kalmanMeasureNoise);

        // ── 1. Face anchor points (world space) ───────────────────────────
        Vector3 chinW    = face.transform.TransformPoint(face.vertices[CHIN_VERTEX]);
        Vector3 browW    = face.transform.TransformPoint(face.vertices[BROW_VERTEX]);
        Vector3 lCheekW  = face.transform.TransformPoint(face.vertices[LEFT_CHEEK]);
        Vector3 rCheekW  = face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]);

        float faceH = Vector3.Distance(chinW, browW);
        float faceW = Vector3.Distance(lCheekW, rCheekW);

        // ── 2. Resolve per-ear values (global or overridden) ──────────────
        float lDrop    = usePerEarOverrides ? leftDropFraction    : lobeDropFraction;
        float lOutward = usePerEarOverrides ? leftOutwardFraction : outwardFraction;
        float lFwd     = usePerEarOverrides ? leftForwardFraction : forwardFraction;

        float rDrop    = usePerEarOverrides ? rightDropFraction    : lobeDropFraction;
        float rOutward = usePerEarOverrides ? rightOutwardFraction : outwardFraction;
        float rFwd     = usePerEarOverrides ? rightForwardFraction : forwardFraction;

        // ── 3. Raw target positions ────────────────────────────────────────
        //  face.transform.right → points toward subject's RIGHT
        //  so subject's LEFT ear needs  -right  (inward then out the left side)
        Vector3 leftRaw = lCheekW
            + face.transform.up      * -(faceH * lDrop)       // ↓ down
            + face.transform.right   * -(faceW * lOutward)     // ← outward left
            + face.transform.forward *  (faceH * lFwd);        // → forward

        Vector3 rightRaw = rCheekW
            + face.transform.up      * -(faceH * rDrop)       // ↓ down
            + face.transform.right   *  (faceW * rOutward)    // → outward right
            + face.transform.forward *  (faceH * rFwd);       // → forward

        // ── 4. Kalman-filtered positions ──────────────────────────────────
        leftAnchor.position  = leftKalman.Update(leftRaw);
        rightAnchor.position = rightKalman.Update(rightRaw);

        // ── 5. Rotation ───────────────────────────────────────────────────
        float rt = rotSmoothing * Time.deltaTime;
        leftAnchor.rotation  = Quaternion.Slerp(leftAnchor.rotation,  ComputeRot(true),  rt);
        rightAnchor.rotation = Quaternion.Slerp(rightAnchor.rotation, ComputeRot(false), rt);

        // ── 6. Auto scale (deferred) ──────────────────────────────────────
        CheckAndScale(leftAnchor,  ref leftLastChild);
        CheckAndScale(rightAnchor, ref rightLastChild);
    }

    // ════════════════════════════════════════════════════════════════════════
    Quaternion ComputeRot(bool isLeft)
    {
        Quaternion faceFollow = face.transform.rotation;
        if (angleRealism <= 0.001f) return faceFollow;

        Vector3 outDir = face.transform.right * (isLeft ? -1f : 1f);
        Vector3 upDir  = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(outDir, upDir)) > 0.98f)
            upDir = face.transform.forward;

        Quaternion gravityHang = Quaternion.LookRotation(outDir, upDir);
        return Quaternion.Slerp(faceFollow, gravityHang, angleRealism);
    }

    // ════════════════════════════════════════════════════════════════════════
    void CheckAndScale(Transform anchor, ref Transform lastChild)
    {
        if (!autoScale) return;
        if (anchor.childCount == 0) { lastChild = null; return; }
        Transform child = anchor.GetChild(0);
        if (child == lastChild) return;
        lastChild = child;
        StartCoroutine(DeferredScale(child));
    }

    IEnumerator DeferredScale(Transform earring)
    {
        yield return null;
        yield return null;
        if (earring == null) yield break;

        earring.localScale = Vector3.one;
        yield return null;
        if (earring == null) yield break;

        Bounds b         = GetBounds(earring.gameObject);
        float  modelSize = Mathf.Max(b.size.x, b.size.y, b.size.z);

        if (modelSize > 0.0001f)
        {
            float s = targetSizeMetres / modelSize;
            earring.localScale = Vector3.one * s;
            Debug.Log($"[EarAttachARCore] '{earring.name}' " +
                      $"model={modelSize * 100f:F1}cm → scale={s:F3} " +
                      $"(target={targetSizeMetres * 100f:F1}cm)");
        }
    }

    Bounds GetBounds(GameObject obj)
    {
        Renderer[] rs = obj.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.01f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ════════════════════════════════════════════════════════════════════════
    void OnDestroy()
    {
        if (leftAnchor  != null) Destroy(leftAnchor.gameObject);
        if (rightAnchor != null) Destroy(rightAnchor.gameObject);
    }

    // ════════════════════════════════════════════════════════════════════════
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || face == null) return;
        int maxIdx = Mathf.Max(LEFT_CHEEK, RIGHT_CHEEK, CHIN_VERTEX, BROW_VERTEX);
        if (face.vertices.Length <= maxIdx) return;

        // Cheekbone reference vertices (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[LEFT_CHEEK]),  0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]), 0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[CHIN_VERTEX]), 0.002f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[BROW_VERTEX]), 0.002f);

        // Kalman-filtered ear lobe targets (yellow)
        Gizmos.color = Color.yellow;
        if (leftAnchor  != null) Gizmos.DrawWireSphere(leftAnchor.position,  0.006f);
        if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.006f);

        // Per-ear colour hint when overrides are active
        if (usePerEarOverrides)
        {
            Gizmos.color = Color.green;
            if (leftAnchor  != null) Gizmos.DrawWireSphere(leftAnchor.position,  0.008f);
            Gizmos.color = Color.red;
            if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.008f);
        }
    }
}