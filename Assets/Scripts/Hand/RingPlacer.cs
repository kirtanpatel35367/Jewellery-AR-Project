// RingPlacer.cs — v26  FIXED: removed adaptive sizing (root cause of tiny ring)
//
// ══════════════════════════════════════════════════════════════════════
// ROOT CAUSE OF "RING DISAPPEARS / TINY" BUG (v25):
//
//   v25 adaptive sizing:
//     segLen = Vector3.Distance(mcp_world, pip_world)
//     fingerDiam = segLen × 0.32
//     scaleFactor = fingerDiam / modelBaseDiameter
//
//   BUG: When the camera is FAR from the hand, ScreenToWorldPoint at
//   fixed baseDepth=0.5m gives SMALL world distances for the finger segment.
//   Example: hand at 80cm camera distance, MCP-to-PIP spans 8% of screen.
//   At depth 0.5m: segLen_world ≈ 0.046m
//   fingerDiam = 0.046 × 0.32 = 0.015m
//   sf = 0.015 / 0.020 = 0.75 → ring 25% smaller ← already too small
//
//   When hand is close (30cm) AND finger occupies less screen:
//   segLen_world could be as small as 0.02m → fingerDiam = 0.006m
//   sf = 0.006 / 0.020 = 0.3 → ring barely visible
//
//   The ring was tiny in frame p4/p5 because adaptive sizing shrank it.
//
// THE FIX — Remove adaptive sizing:
//   Use fixed targetDiameterM calibrated once at spawn.
//   targetDiameterM = 0.019m is the correct physical inner diameter
//   for an average ring on a ring finger.
//   MeasureDiameter() correctly scales the model at spawn time.
//   Scale is NEVER changed at runtime.
//
// KEPT from v24/v25:
//   ✓ Finger-normal face orientation (gem faces palm side)
//   ✓ Depth push into finger (fingerDepthOffset)
//   ✓ Heavy smoothing on finger normal
//   ✓ Correct fingerBias = 0.45 (mid-lower finger shaft)
// ══════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class RingPlacer : MonoBehaviour
{
    public enum FingerTarget { Index, Middle, Ring, Pinky }

    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject ringPrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;
    public ARCameraManager arCameraManager;

    [Header("Finger")]
    public FingerTarget finger = FingerTarget.Ring;

    [Header("Placement")]
    [Tooltip("0 = MCP knuckle, 1 = PIP joint.\n" +
             "0.45 = natural ring position on lower finger shaft.")]
    [Range(0f, 1f)] public float fingerBias = 0.45f;

    [Header("Depth")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Ring Physical Size")]
    [Tooltip("Target inner diameter in metres. Set ONCE at spawn.\n\n" +
             "Average finger ring sizes:\n" +
             "Slim finger: 0.016m | Average: 0.019m | Wide finger: 0.022m\n\n" +
             "Adjust if ring appears too tight or too loose on screen.\n" +
             "Changes take effect when ring prefab is re-assigned.")]
    [Range(0.010f, 0.035f)] public float targetDiameterM = 0.019f;

    [Header("Depth Into Finger")]
    [Tooltip("Pushes ring center INTO finger so it visually encircles it.\n" +
             "0.008m works for average finger. Reduce if ring clips through.")]
    [Range(0f, 0.020f)] public float fingerDepthOffset = 0.008f;

    [Header("Landmark Y Correction")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f, rotSmooth = 16f;

    [Header("Persistence")]
    [Range(0, 30)] public int hideDelayFrames = 10;

    [Header("Stability")]
    [Range(0, 5)] public int minDetectionFrames = 2;

    // [MCP, PIP, DIP, TIP] per finger
    static readonly int[,] FL =
    {
        {  5,  6,  7,  8 },
        {  9, 10, 11, 12 },
        { 13, 14, 15, 16 },
        { 17, 18, 19, 20 },
    };

    private GameObject _ring;
    private Vector3 _calibratedScale;   // fixed at spawn — never changed at runtime

    private Vector3 _sp;
    private Quaternion _sr = Quaternion.identity;
    private Vector3 _sv;
    private Vector3 _smoothFingerNormal = Vector3.forward;
    private bool _first = true, _ready;
    private int _detFrames, _lostFrames;
    private int _rawTexW, _rawTexH, _texW, _texH;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[RingPlacer] missing landmarkReader!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (ringPrefab) SpawnRing(ringPrefab);
        _ready = true;
    }

    public void SetRingPrefab(GameObject p)
    {
        if (p) { ringPrefab = p; SpawnRing(p); } else ClearRing();
    }

    public void ClearRing()
    {
        if (_ring) { Destroy(_ring); _ring = null; }
        _first = true; _detFrames = 0; _lostFrames = 0; ringPrefab = null;
    }

    void SpawnRing(GameObject prefab)
    {
        if (_ring) Destroy(_ring);
        _ring = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        _ring.SetActive(true);

        // ONE-TIME scale calibration at spawn — NEVER changed at runtime
        float measured = MeasureDiameter(_ring);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = _ring.transform.localScale * sf;
            _ring.transform.localScale = _calibratedScale;
            Debug.Log($"[RingPlacer v26] measured={measured * 1000:F0}mm " +
                      $"target={targetDiameterM * 1000:F0}mm  sf={sf:F3}  " +
                      $"finalScale={_calibratedScale}");
        }
        else
        {
            _calibratedScale = _ring.transform.localScale;
            Debug.LogWarning("[RingPlacer] No renderer bounds — using prefab scale.");
        }

        _ring.SetActive(false);
        _first = true; _detFrames = 0; _lostFrames = 0;
    }

    static float MeasureDiameter(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float dxz = Mathf.Max(b.size.x, b.size.z);
        return dxz > 0.0001f ? dxz : b.size.y;
    }

    void LateUpdate()
    {
        if (!_ready || !_ring) return;
        UpdateTex();

        bool detected = landmarkReader.HandDetected && landmarkReader.LandmarkCount >= 21;
        if (!detected)
        {
            _detFrames = 0;
            if (++_lostFrames > hideDelayFrames) { _ring.SetActive(false); _first = true; }
            return;
        }
        _lostFrames = 0;
        if (++_detFrames < minDetectionFrames) { _ring.SetActive(false); return; }

        // Ensure scale is always the calibrated value — defensive guard
        _ring.transform.localScale = _calibratedScale;

        int fi = (int)finger;
        Vector3 mcp = C(FL[fi, 0]);
        Vector3 pip = C(FL[fi, 1]);

        Vector3 fingerAxis = (pip - mcp).normalized;
        if (fingerAxis.sqrMagnitude < 0.001f) { _ring.SetActive(false); return; }

        // ── Finger normal (palm-side direction, smoothed) ─────────────
        Vector3 wristW = C(0);
        Vector3 indexW = C(5);
        Vector3 pinkyW = C(17);
        Vector3 knuckleCenter = (C(5) + C(9) + C(13) + C(17)) * 0.25f;
        Vector3 armAxis = (knuckleCenter - wristW).normalized;
        Vector3 acrossHand = (pinkyW - indexW).normalized;
        Vector3 rawNorm = Vector3.Cross(armAxis, acrossHand).normalized;
        if (landmarkReader.IsLeftHand) rawNorm = -rawNorm;

        float dt = Time.deltaTime;
        if (_first)
            _smoothFingerNormal = rawNorm;
        else
            _smoothFingerNormal = Vector3.Slerp(
                _smoothFingerNormal, rawNorm,
                Mathf.Clamp01(rotSmooth * 0.25f * dt)).normalized;

        // Re-orthogonalise against finger axis
        Vector3 fNorm = _smoothFingerNormal - Vector3.Dot(_smoothFingerNormal, fingerAxis) * fingerAxis;
        if (fNorm.sqrMagnitude < 0.01f)
            fNorm = Vector3.Cross(fingerAxis, Vector3.up);
        fNorm = fNorm.normalized;

        // ── Position: mid-finger shaft + depth push into finger ───────
        Vector3 surface = Vector3.Lerp(mcp, pip, fingerBias);
        Vector3 targetPos = surface + arCamera.transform.forward * fingerDepthOffset;

        // ── Rotation matrix ───────────────────────────────────────────
        // col[2] = fingerAxis  → hole along finger (local Z)
        // col[1] = fNorm       → face tracks palm direction (local Y)
        // col[0] = crossAxis   → across ring (local X)
        Vector3 crossAxis = Vector3.Cross(fNorm, fingerAxis).normalized;

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(crossAxis.x, crossAxis.y, crossAxis.z, 0f));
        m.SetColumn(1, new Vector4(fNorm.x, fNorm.y, fNorm.z, 0f));
        m.SetColumn(2, new Vector4(fingerAxis.x, fingerAxis.y, fingerAxis.z, 0f));
        Quaternion tRot = m.rotation;

        if (_first)
        { _sp = targetPos; _sr = tRot; _sv = Vector3.zero; _first = false; }
        else
        {
            _sp = Vector3.SmoothDamp(_sp, targetPos, ref _sv, 1f / posSmooth, Mathf.Infinity, dt);
            _sr = Quaternion.Slerp(_sr, tRot, rotSmooth * dt);
        }

        _ring.transform.SetPositionAndRotation(_sp, _sr);
        _ring.SetActive(true);
    }

    Vector3 C(int i) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera, _texW, _texH, baseDepth, false, bboxYCorrection);

    void UpdateTex()
    {
        bool got = false;
        if (imageSourceBehaviour != null)
        {
            var src = imageSourceBehaviour.GetImageSource();
            if (src != null && src.isPrepared)
            { _rawTexW = src.textureWidth; _rawTexH = src.textureHeight; got = true; }
        }
        if (!got && _rawTexW == 0) { _rawTexW = 720; _rawTexH = 1280; }
        _texW = _rawTexW; _texH = _rawTexH;
    }
}