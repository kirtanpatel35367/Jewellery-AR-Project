using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

/// <summary>
/// AR HAND TRACKER v6
///
/// KEY FIXES vs v5:
///  1. Subsystem retried every 2s (not once) — handles dual AR session scenes
///     where HandTracking_System activates after FaceTryOn_System.
///  2. Prefers the RUNNING subsystem when multiple exist.
///  3. Full on-screen debug overlay (showDebug=true) — shows live tracking state,
///     subsystem count, and hand positions so you can diagnose on-device.
///  4. All joint reads have safe fallbacks — never returns garbage data.
///  NOTE: KalmanFilterVector3 lives in its own KalmanFilterVector3.cs file.
/// </summary>
public class ARHandTracker : MonoBehaviour
{
    [Header("Debug — turn OFF for release builds")]
    public bool showDebug = true;

    public struct WristGeometry
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 forward;    // wrist → fingertips
        public Vector3 up;         // back of hand normal
        public float wristSize;  // diameter in metres
        public bool isTracked;
    }

    private WristGeometry rightWrist;
    private WristGeometry leftWrist;
    private XRHandSubsystem handSubsystem;

    private float retryTimer = 0f;
    private const float RETRY_INTERVAL = 2f;
    private const float INIT_DELAY = 1.5f;

    private string debugStatus = "Initializing...";
    private int subsysCount = 0;

    void Start()
    {
        retryTimer = -INIT_DELAY; // first attempt fires after INIT_DELAY seconds
        Debug.Log("[ARHandTracker] v6 starting. First subsystem check in " + INIT_DELAY + "s");
    }

    void Update()
    {
        retryTimer += Time.deltaTime;

        if (handSubsystem == null || !handSubsystem.running)
        {
            rightWrist = default;
            leftWrist = default;

            if (retryTimer >= RETRY_INTERVAL)
            {
                retryTimer = 0f;
                TryAcquireSubsystem();
            }
            return;
        }

        rightWrist = handSubsystem.rightHand.isTracked
            ? ReadWrist(handSubsystem.rightHand) : default;

        leftWrist = handSubsystem.leftHand.isTracked
            ? ReadWrist(handSubsystem.leftHand) : default;

        debugStatus = "Subsystem RUNNING | " +
                      $"R:{(rightWrist.isTracked ? "TRACKED" : "—")}  " +
                      $"L:{(leftWrist.isTracked ? "TRACKED" : "—")}";
    }

    void TryAcquireSubsystem()
    {
        var list = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(list);
        subsysCount = list.Count;

        if (list.Count == 0)
        {
            debugStatus = "ERROR: No XRHandSubsystem found!";
            Debug.LogWarning("[ARHandTracker] No XRHandSubsystem found. " +
                             "Ensure XR Hands package is installed and " +
                             "HandTracking_System is active in scene.");
            return;
        }

        // Prefer a running subsystem (critical in dual-AR-session scenes)
        XRHandSubsystem best = null;
        foreach (var s in list)
        {
            Debug.Log($"[ARHandTracker] Found subsystem: {s.GetType().Name}  running={s.running}");
            if (s.running) { best = s; break; }
        }
        if (best == null) best = list[0]; // fallback

        handSubsystem = best;
        debugStatus = $"Subsystem acquired. running={handSubsystem.running} ({list.Count} total)";
        Debug.Log("[ARHandTracker] " + debugStatus);
    }

    WristGeometry ReadWrist(XRHand hand)
    {
        var w = new WristGeometry();

        if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose))
            return w;

        w.position = wristPose.position;
        w.rotation = wristPose.rotation;
        w.isTracked = true;
        w.wristSize = 0.065f; // safe default

        // Wrist size from palm distance
        if (hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
            w.wristSize = Mathf.Clamp(
                Vector3.Distance(wristPose.position, palmPose.position) * 2f,
                0.045f, 0.12f);

        // Forward: wrist → middle proximal (most reliable)
        if (hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out Pose midPose))
        {
            Vector3 fwd = midPose.position - wristPose.position;
            if (fwd.sqrMagnitude > 0.0001f)
                w.forward = fwd.normalized;
        }

        // Fallback forward from wrist rotation
        if (w.forward.sqrMagnitude < 0.001f)
            w.forward = wristPose.rotation * Vector3.forward;

        // Up: back of hand, orthogonalised against forward
        w.up = wristPose.rotation * Vector3.up;
        if (w.forward.sqrMagnitude > 0.001f)
            w.up = Vector3.ProjectOnPlane(w.up, w.forward).normalized;

        if (w.up.sqrMagnitude < 0.001f)
            w.up = Vector3.up;

        return w;
    }

    public bool IsRightWristTracked() => rightWrist.isTracked;
    public bool IsLeftWristTracked() => leftWrist.isTracked;
    public WristGeometry GetRightWrist() => rightWrist;
    public WristGeometry GetLeftWrist() => leftWrist;

    void OnGUI()
    {
        if (!showDebug) return;
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        int y = 10;
        GUI.Box(new Rect(10, y, 540, 100), "");

        s.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y + 3, 530, 18), "ARHandTracker v6", s);

        s.normal.textColor = (handSubsystem != null && handSubsystem.running) ? Color.green : Color.red;
        GUI.Label(new Rect(15, y + 23, 530, 18),
            $"Subsystems: {subsysCount}  |  Running: {(handSubsystem != null && handSubsystem.running)}", s);

        s.normal.textColor = rightWrist.isTracked ? Color.green : Color.gray;
        GUI.Label(new Rect(15, y + 43, 530, 18),
            $"Right: {(rightWrist.isTracked ? "TRACKED  " + rightWrist.position.ToString("F2") : "not detected")}", s);

        s.normal.textColor = leftWrist.isTracked ? Color.green : Color.gray;
        GUI.Label(new Rect(15, y + 63, 530, 18),
            $"Left:  {(leftWrist.isTracked ? "TRACKED  " + leftWrist.position.ToString("F2") : "not detected")}", s);

        s.normal.textColor = Color.yellow;
        GUI.Label(new Rect(15, y + 83, 530, 14), debugStatus, s);
    }
}