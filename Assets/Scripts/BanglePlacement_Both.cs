using UnityEngine;

/// <summary>
/// BANGLE PLACEMENT v2 — Both Hands
///
/// KEY FIXES vs v1:
///  • Jewelry is positioned AT THE WRIST BONE with a configurable forward offset
///    so it sits on the wrist, not floating at the joint centre.
///  • Rotation is built from wrist forward/up axes so the bangle ring
///    is always perpendicular to the arm, facing the right way.
///  • Scale auto-derives from wristSize (hand distance) so bangles
///    grow/shrink naturally with real hand size, with a user multiplier.
///  • Rotation is Slerp-smoothed each frame to eliminate jitter.
///  • Kalman filter smooths position only (rotation handled separately).
/// </summary>
public class BanglePlacement : MonoBehaviour
{
    [Header("★ REQUIRED ★")]
    public ARHandTracker handTracker;

    [Header("★ PLACEMENT OFFSET ★")]
    [Tooltip("Move bangle along wrist→finger axis. 0 = at wrist joint. " +
             "Positive = toward palm. Negative = toward elbow.")]
    [Range(-0.05f, 0.05f)]
    public float forwardOffset = 0.01f;

    [Tooltip("Move bangle toward back-of-hand (positive) or palm (negative).")]
    [Range(-0.03f, 0.03f)]
    public float upOffset = 0.0f;

    [Header("★ SCALE ★")]
    [Tooltip("Multiplier on top of auto-computed wrist size. 1 = snug fit.")]
    [Range(0.8f, 2.5f)]
    public float scaleMultiplier = 1.15f;

    [Header("★ ROTATION SMOOTHING ★")]
    [Range(0.05f, 1f)]
    [Tooltip("Lower = smoother but more lag. 0.25 is a good default.")]
    public float rotationSmoothing = 0.25f;

    [Header("★ KALMAN FILTERING (position) ★")]
    public bool useKalmanFilter = true;
    [Range(0.001f, 0.1f)] public float kalmanQ = 0.005f;
    [Range(0.01f, 1f)] public float kalmanR = 0.05f;

    [Header("★ DEBUG ★")]
    public bool showDebug = false;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private GameObject rightBangle;
    private GameObject leftBangle;

    private KalmanFilterVector3 rightKalman;
    private KalmanFilterVector3 leftKalman;

    private Quaternion rightRotSmooth = Quaternion.identity;
    private Quaternion leftRotSmooth = Quaternion.identity;

    private bool rightTracking = false;
    private bool leftTracking = false;
    private bool isInitialized = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (handTracker == null)
        {
            Debug.LogError("[BanglePlacement] ARHandTracker not assigned!");
            enabled = false;
            return;
        }

        rightKalman = new KalmanFilterVector3(kalmanQ, kalmanR);
        leftKalman = new KalmanFilterVector3(kalmanQ, kalmanR);
        isInitialized = true;
        Debug.Log("[BanglePlacement] Initialized.");
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void SpawnBangles(GameObject prefab)
    {
        if (prefab == null) { Debug.LogError("[BanglePlacement] Prefab is null!"); return; }

        RemoveBangles();

        rightBangle = Instantiate(prefab); rightBangle.name = "RightBangle"; rightBangle.SetActive(false);
        leftBangle = Instantiate(prefab); leftBangle.name = "LeftBangle"; leftBangle.SetActive(false);

        // Fresh Kalman filters and rotation state
        rightKalman = new KalmanFilterVector3(kalmanQ, kalmanR);
        leftKalman = new KalmanFilterVector3(kalmanQ, kalmanR);
        rightRotSmooth = Quaternion.identity;
        leftRotSmooth = Quaternion.identity;

        Debug.Log($"[BanglePlacement] Spawned bangles: {prefab.name}");
    }

    public void RemoveBangles()
    {
        if (rightBangle != null) { Destroy(rightBangle); rightBangle = null; }
        if (leftBangle != null) { Destroy(leftBangle); leftBangle = null; }
        rightTracking = false;
        leftTracking = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!isInitialized || handTracker == null) return;
        if (rightBangle == null && leftBangle == null) return;

        UpdateHand(
            isRight: true,
            isTracked: handTracker.IsRightWristTracked(),
            wrist: handTracker.GetRightWrist(),
            bangle: rightBangle,
            kalman: rightKalman,
            rotSmooth: ref rightRotSmooth,
            tracking: ref rightTracking);

        UpdateHand(
            isRight: false,
            isTracked: handTracker.IsLeftWristTracked(),
            wrist: handTracker.GetLeftWrist(),
            bangle: leftBangle,
            kalman: leftKalman,
            rotSmooth: ref leftRotSmooth,
            tracking: ref leftTracking);
    }

    void UpdateHand(
        bool isRight,
        bool isTracked,
        ARHandTracker.WristGeometry wrist,
        GameObject bangle,
        KalmanFilterVector3 kalman,
        ref Quaternion rotSmooth,
        ref bool tracking)
    {
        if (bangle == null) return;

        if (!isTracked)
        {
            bangle.SetActive(false);
            tracking = false;
            return;
        }

        // ── Position ──────────────────────────────────────────────────────────
        // Start at wrist joint, then offset along forward (toward fingers) and up (back of hand)
        Vector3 rawPos = wrist.position
                       + wrist.forward * forwardOffset
                       + wrist.up * upOffset;

        Vector3 pos = useKalmanFilter && kalman != null ? kalman.Update(rawPos) : rawPos;

        // ── Rotation ──────────────────────────────────────────────────────────
        // We want the bangle's local Z (its ring axis) to align with the arm/finger direction.
        // LookRotation(forward, up) → local Z = forward, local Y = up
        Quaternion targetRot = Quaternion.identity;
        if (wrist.forward.sqrMagnitude > 0.001f && wrist.up.sqrMagnitude > 0.001f)
            targetRot = Quaternion.LookRotation(wrist.forward, wrist.up);
        else
            targetRot = wrist.rotation;

        // Smooth rotation to kill jitter
        rotSmooth = Quaternion.Slerp(rotSmooth, targetRot, rotationSmoothing);

        // ── Scale ─────────────────────────────────────────────────────────────
        // wristSize is the wrist diameter in metres; use it directly as the ring diameter
        float size = wrist.wristSize > 0f ? wrist.wristSize : 0.065f;
        float scale = size * scaleMultiplier;

        // ── Apply ─────────────────────────────────────────────────────────────
        bangle.transform.position = pos;
        bangle.transform.rotation = rotSmooth;
        bangle.transform.localScale = Vector3.one * scale;
        bangle.SetActive(true);
        tracking = true;
    }

    // ── Debug GUI ─────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!showDebug || (rightBangle == null && leftBangle == null)) return;

        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        int y = 470;
        GUI.Box(new Rect(10, y, 480, 110), "");

        s.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y + 4, 470, 20), "BANGLE PLACEMENT v2", s);

        s.normal.textColor = rightTracking ? Color.green : Color.red;
        GUI.Label(new Rect(15, y + 26, 470, 18), $"Right: {(rightTracking ? "TRACKING ✓" : "waiting...")}", s);

        s.normal.textColor = leftTracking ? Color.green : Color.red;
        GUI.Label(new Rect(15, y + 46, 470, 18), $"Left:  {(leftTracking ? "TRACKING ✓" : "waiting...")}", s);

        s.normal.textColor = Color.white;
        GUI.Label(new Rect(15, y + 68, 470, 18),
            $"Scale×{scaleMultiplier:F2}  fwdOff={forwardOffset:F3}  Kalman:{(useKalmanFilter ? "ON" : "OFF")}", s);
    }

    void OnDestroy() => RemoveBangles();
}