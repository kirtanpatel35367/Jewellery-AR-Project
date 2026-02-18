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
    [Header("── Scale ──────────────────────────────────────────────────")]
    [Tooltip("Target earring size in metres (longest dimension).\n2.2 cm = 0.022.")]
    public float targetSizeMetres = 0.022f;
    public bool autoScale = true;

    // ── Private ─────────────────────────────────────────────────────────────
    private ARFace face;
    private Transform leftAnchor;
    private Transform rightAnchor;
    private bool registered = false;
    private Transform leftLastChild = null;
    private Transform rightLastChild = null;

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

        public float Update(float measurement)
        {
            if (!init) { x = measurement; init = true; return x; }
            // Predict
            p = p + q;
            // Update (Kalman gain)
            float k = p / (p + r);
            x = x + k * (measurement - x);
            p = (1f - k) * p;
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
            kx.SetNoise(q, r); ky.SetNoise(q, r); kz.SetNoise(q, r);
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

        float faceH = Vector3.Distance(chinW, browW);
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

        // ── 5. Deferred scale ─────────────────────────────────────────────
        CheckAndScale(leftAnchor, ref leftLastChild);
        CheckAndScale(rightAnchor, ref rightLastChild);
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

        Bounds b = GetBounds(earring.gameObject);
        float modelSize = Mathf.Max(b.size.x, b.size.y, b.size.z);

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

        // Cheekbone reference points (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[LEFT_CHEEK]), 0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[RIGHT_CHEEK]), 0.003f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[CHIN_VERTEX]), 0.002f);
        Gizmos.DrawWireSphere(face.transform.TransformPoint(face.vertices[BROW_VERTEX]), 0.002f);

        // Kalman-filtered lobe positions (yellow)
        Gizmos.color = Color.yellow;
        if (leftAnchor != null) Gizmos.DrawWireSphere(leftAnchor.position, 0.006f);
        if (rightAnchor != null) Gizmos.DrawWireSphere(rightAnchor.position, 0.006f);
    }
}