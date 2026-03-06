using UnityEngine;

/// <summary>
/// BRACELET PLACEMENT v3 — Single Hand
///
/// FIXES vs v2:
///  • Auto-finds ARHandTracker if not assigned in Inspector (fixes
///    "[JewelryManager] BraceletPlacement not assigned" cascading errors).
///  • Added rotationOffset (Euler) so you can correct for prefab axis
///    mismatches directly in the Inspector without touching the prefab.
///    Common fix: if bracelet appears sideways, set X=90 or Z=90.
///  • Null-guards everywhere so a missing reference never throws.
///  • showDebug defaults OFF so logcat stays clean in production.
/// </summary>
public class BraceletPlacement : MonoBehaviour
{
    [Header("★ REQUIRED — assign in Inspector OR auto-found at runtime ★")]
    public ARHandTracker handTracker;

    [Header("★ HAND SELECTION ★")]
    [Tooltip("Which hand to prefer.")]
    public HandSide preferredHand = HandSide.Right;

    [Tooltip("Auto-switch to the other hand if the preferred one isn't visible.")]
    public bool autoDetectHand = true;

    public enum HandSide { Right, Left }

    [Header("★ PLACEMENT OFFSET ★")]
    [Tooltip("Slide bracelet along wrist→finger axis. Positive=toward palm, negative=toward elbow.")]
    [Range(-0.06f, 0.06f)]
    public float forwardOffset = 0.01f;

    [Tooltip("Slide bracelet toward back-of-hand (+) or palm (−).")]
    [Range(-0.04f, 0.04f)]
    public float upOffset = 0.0f;

    [Header("★ ROTATION CORRECTION ★")]
    [Tooltip("Extra Euler rotation applied AFTER wrist alignment.\n" +
             "Use this to fix prefab axis mismatches.\n" +
             "Ring looks sideways  → try X = 90\n" +
             "Ring looks upside-down → try Z = 180")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("★ SCALE ★")]
    [Tooltip("Multiplier over auto-computed wrist diameter. 1.0 = snug fit.")]
    [Range(0.8f, 2.5f)]
    public float scaleMultiplier = 1.15f;

    [Header("★ SMOOTHING ★")]
    [Range(0.05f, 1f)]
    public float rotationSmoothing = 0.25f;

    [Header("★ KALMAN FILTERING (position) ★")]
    public bool useKalmanFilter = true;
    [Range(0.001f, 0.1f)] public float kalmanQ = 0.005f;
    [Range(0.01f, 1f)] public float kalmanR = 0.05f;

    [Header("★ DEBUG ★")]
    public bool showDebug = false;

    // Runtime
    private GameObject currentBracelet;
    private HandSide activeHand;
    private KalmanFilterVector3 kalmanFilter;
    private Quaternion rotSmooth = Quaternion.identity;
    private bool isTracking = false;
    private bool isInitialized = false;

    void Start()
    {
        // Auto-find tracker if not assigned in Inspector
        if (handTracker == null)
            handTracker = FindObjectOfType<ARHandTracker>();

        if (handTracker == null)
        {
            Debug.LogError("[BraceletPlacement] ARHandTracker not found in scene! " +
                           "Add ARHandTracker to a GameObject in the scene.");
            enabled = false;
            return;
        }

        activeHand = preferredHand;
        kalmanFilter = new KalmanFilterVector3(kalmanQ, kalmanR);
        isInitialized = true;
        Debug.Log("[BraceletPlacement] Initialized. Tracker: " + handTracker.gameObject.name);
    }

    // Called by JewelryManager
    public void SpawnBracelet(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[BraceletPlacement] SpawnBracelet called with null prefab!");
            return;
        }
        RemoveBracelet();

        currentBracelet = Instantiate(prefab);
        currentBracelet.name = "ActiveBracelet";
        currentBracelet.SetActive(false);

        kalmanFilter = new KalmanFilterVector3(kalmanQ, kalmanR);
        rotSmooth = Quaternion.identity;
        activeHand = preferredHand;

        Debug.Log("[BraceletPlacement] Spawned: " + prefab.name);
    }

    public void RemoveBracelet()
    {
        if (currentBracelet != null) { Destroy(currentBracelet); currentBracelet = null; }
        isTracking = false;
    }

    public void SwitchHand()
    {
        preferredHand = (preferredHand == HandSide.Right) ? HandSide.Left : HandSide.Right;
        activeHand = preferredHand;
        Debug.Log("[BraceletPlacement] Switched to " + activeHand);
    }

    void Update()
    {
        if (!isInitialized || currentBracelet == null || handTracker == null) return;

        ResolveActiveHand();

        bool handVisible = false;
        ARHandTracker.WristGeometry wrist = default;

        if (activeHand == HandSide.Right && handTracker.IsRightWristTracked())
        { wrist = handTracker.GetRightWrist(); handVisible = true; }
        else if (activeHand == HandSide.Left && handTracker.IsLeftWristTracked())
        { wrist = handTracker.GetLeftWrist(); handVisible = true; }

        if (!handVisible)
        { currentBracelet.SetActive(false); isTracking = false; return; }

        // Position: wrist joint + forward/up offsets
        Vector3 rawPos = wrist.position
                       + wrist.forward * forwardOffset
                       + wrist.up * upOffset;
        Vector3 pos = (useKalmanFilter && kalmanFilter != null)
            ? kalmanFilter.Update(rawPos) : rawPos;

        // Rotation: align ring Z-axis with arm, then apply correction
        Quaternion baseRot = (wrist.forward.sqrMagnitude > 0.001f && wrist.up.sqrMagnitude > 0.001f)
            ? Quaternion.LookRotation(wrist.forward, wrist.up)
            : wrist.rotation;
        Quaternion targetRot = baseRot * Quaternion.Euler(rotationOffset);
        rotSmooth = Quaternion.Slerp(rotSmooth, targetRot, rotationSmoothing);

        // Scale: auto from wrist size
        float scale = (wrist.wristSize > 0f ? wrist.wristSize : 0.065f) * scaleMultiplier;

        currentBracelet.transform.position = pos;
        currentBracelet.transform.rotation = rotSmooth;
        currentBracelet.transform.localScale = Vector3.one * scale;
        currentBracelet.SetActive(true);
        isTracking = true;
    }

    void ResolveActiveHand()
    {
        if (!autoDetectHand) { activeHand = preferredHand; return; }
        bool r = handTracker.IsRightWristTracked();
        bool l = handTracker.IsLeftWristTracked();
        activeHand = (preferredHand == HandSide.Right)
            ? (r ? HandSide.Right : (l ? HandSide.Left : preferredHand))
            : (l ? HandSide.Left : (r ? HandSide.Right : preferredHand));
    }

    void OnGUI()
    {
        if (!showDebug || currentBracelet == null) return;
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        int y = 360;
        GUI.Box(new Rect(10, y, 500, 120), "");
        s.normal.textColor = Color.cyan;
        GUI.Label(new Rect(15, y + 4, 490, 20), "BRACELET PLACEMENT v3", s);
        s.normal.textColor = isTracking ? Color.green : Color.red;
        GUI.Label(new Rect(15, y + 26, 490, 18), $"Hand: {activeHand}  |  {(isTracking ? "TRACKING" : "waiting...")}", s);
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(15, y + 46, 490, 18), $"Preferred:{preferredHand}  AutoDetect:{autoDetectHand}", s);
        s.normal.textColor = Color.yellow;
        GUI.Label(new Rect(15, y + 68, 490, 18), $"Scale*{scaleMultiplier:F2}  fwdOff={forwardOffset:F3}  rotOff={rotationOffset}", s);
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(15, y + 90, 490, 18), $"Kalman:{useKalmanFilter}  rotSmooth={rotationSmoothing:F2}", s);
    }

    void OnDestroy() => RemoveBracelet();
}