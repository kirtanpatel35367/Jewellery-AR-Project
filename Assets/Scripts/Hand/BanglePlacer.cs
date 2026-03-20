// BanglePlacer.cs — v35  FIXED: removed adaptive sizing (root cause of massive bracelet)
//
// ══════════════════════════════════════════════════════════════════════
// ROOT CAUSE OF "BRACELET FILLS HALF THE SCREEN" BUG (v34):
//
//   v34 adaptive sizing formula:
//     palmWidth = Vector3.Distance(indexW_world, pinkyW_world)
//     wristDiam = palmWidth × 0.72
//     scaleFactor = wristDiam / modelBaseDiameter
//
//   BUG: All world positions come from ScreenToWorldPoint(x, y, baseDepth=0.5m).
//   When the camera is CLOSE to the hand (e.g. 30cm), the hand fills the screen.
//   ScreenToWorldPoint at fixed depth=0.5m gives world distances proportional to
//   (actual_screen_span × depth_ratio). A palm spanning 40% of the screen at
//   30cm camera distance projects to a world width of ~0.20m at depth=0.5m,
//   even though the physical palm is only 8cm wide.
//
//   Result: wristDiam = 0.20 × 0.72 = 0.144m → sf = 0.144/0.065 = 2.2
//   → Bracelet becomes 2× the intended size. When hand is very close, 
//   the bracelet grows to fill the screen as seen in frame p2.
//
// THE FIX — Remove adaptive sizing entirely:
//   ScreenToWorldPoint projected distances are NOT physically reliable for sizing.
//   They depend on camera-to-hand distance which we don't know.
//   The correct approach is a fixed physical target size, set once at spawn.
//
//   The spawn-time MeasureDiameter() + targetDiameterM already handles the
//   model's scale correctly. We just need to set targetDiameterM to the
//   correct physical wrist diameter for this bracelet style.
//
//   Average adult wrist: outer bracelet diameter = 60–70mm.
//   Default: 0.065m. User can tune in Inspector per bracelet style.
//
// KEPT from v33/v34:
//   ✓ Palm-normal face orientation (front/back tracking)
//   ✓ Depth push into wrist (wristDepthOffset)
//   ✓ World-up reference for stable level orientation
//   ✓ Heavy smoothing on palm normal
// ══════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class BanglePlacer : MonoBehaviour
{
    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;
    public ARCameraManager arCameraManager;

    [Header("Depth")]
    [Tooltip("Metres from camera to hand plane.")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Physical Size")]
    [Tooltip("Target outer diameter in metres.\n" +
             "This is set ONCE at spawn via MeasureDiameter — bracelet will appear\n" +
             "this wide in world space regardless of prefab's local scale.\n\n" +
             "Average wrist diameter: 0.055–0.070m.\n" +
             "Narrow wrist: 0.055m | Average: 0.065m | Large: 0.075m\n\n" +
             "Adjust this if bracelet appears too big or too small on the wrist.\n" +
             "Changes take effect when bracelet prefab is re-assigned.")]
    [Range(0.030f, 0.120f)] public float targetDiameterM = 0.065f;

    [Header("Wrist Position")]
    [Tooltip("Slide bracelet along arm.\n" +
             "0 = wrist landmark. 0.18 = wrist crease. 0.25 = slightly above crease.")]
    [Range(-0.1f, 0.4f)] public float wristOffsetFactor = 0.18f;

    [Header("Depth Into Wrist")]
    [Tooltip("Pushes bracelet center INTO wrist so both arcs are visible (wrap effect).\n" +
             "0.010m works for average wrist. Reduce if bracelet clips through hand.")]
    [Range(0f, 0.030f)] public float wristDepthOffset = 0.010f;

    [Header("Landmark Y Correction")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f;
    [Range(1f, 40f)] public float rotSmooth = 16f;

    [Header("Persistence")]
    [Range(0, 30)] public int hideDelayFrames = 10;

    [Header("Stability")]
    [Range(0, 5)] public int minDetectionFrames = 2;

    // ── private ──────────────────────────────────────────────────────
    private GameObject _bangle;
    private Vector3 _calibratedScale;   // fixed at spawn — never changed at runtime

    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private Vector3 _posVelocity;
    private Vector3 _smoothPalmNormal = Vector3.forward;
    private bool _firstFrame = true;
    private bool _ready;
    private int _detFrames, _lostFrames;
    private float _logT;
    private int _rawTexW, _rawTexH, _texW, _texH;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[BanglePlacer] missing landmarkReader!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (banglePrefab) SpawnBangle(banglePrefab);
        _ready = true;
    }

    public void SetBanglePrefab(GameObject p)
    {
        if (p) { banglePrefab = p; SpawnBangle(p); } else ClearBangle();
    }

    public void ClearBangle()
    {
        if (_bangle) { Destroy(_bangle); _bangle = null; }
        _firstFrame = true; _detFrames = 0; _lostFrames = 0; banglePrefab = null;
    }

    void SpawnBangle(GameObject prefab)
    {
        if (_bangle) Destroy(_bangle);
        _bangle = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        _bangle.SetActive(true);

        // ONE-TIME scale calibration at spawn — NEVER changed at runtime
        float measured = MeasureDiameter(_bangle);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = _bangle.transform.localScale * sf;
            _bangle.transform.localScale = _calibratedScale;
            Debug.Log($"[BanglePlacer v35] measured={measured * 100:F1}cm " +
                      $"target={targetDiameterM * 100:F1}cm  sf={sf:F3}  " +
                      $"finalScale={_calibratedScale}");
        }
        else
        {
            _calibratedScale = _bangle.transform.localScale;
            Debug.LogWarning("[BanglePlacer] No renderer bounds — using prefab scale.");
        }

        _bangle.SetActive(false);
        _firstFrame = true; _detFrames = 0; _lostFrames = 0;
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
        if (!_ready || !_bangle) return;
        UpdateTex();

        bool detected = landmarkReader.HandDetected && landmarkReader.LandmarkCount >= 18;
        if (!detected)
        {
            _detFrames = 0;
            if (++_lostFrames > hideDelayFrames) { _bangle.SetActive(false); _firstFrame = true; }
            return;
        }
        _lostFrames = 0;
        if (++_detFrames < minDetectionFrames) { _bangle.SetActive(false); return; }

        // Ensure scale is always the calibrated value — defensive guard
        _bangle.transform.localScale = _calibratedScale;

        // ── Landmark world positions ──────────────────────────────────
        Vector3 wristW = C(0);
        Vector3 indexW = C(5);
        Vector3 midW = C(9);
        Vector3 ringW = C(13);
        Vector3 pinkyW = C(17);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 armAxis = (knuckleCenter - wristW).normalized;
        if (armAxis.sqrMagnitude < 0.001f) return;

        // ── Palm normal (smoothed heavily) ────────────────────────────
        Vector3 acrossWrist = (pinkyW - indexW).normalized;
        Vector3 rawPalmNorm = Vector3.Cross(armAxis, acrossWrist).normalized;
        if (landmarkReader.IsLeftHand) rawPalmNorm = -rawPalmNorm;

        float dt = Time.deltaTime;
        if (_firstFrame)
            _smoothPalmNormal = rawPalmNorm;
        else
            _smoothPalmNormal = Vector3.Slerp(
                _smoothPalmNormal, rawPalmNorm,
                Mathf.Clamp01(rotSmooth * 0.25f * dt)).normalized;

        // Re-orthogonalise palmNormal vs armAxis
        Vector3 palmNormal = _smoothPalmNormal - Vector3.Dot(_smoothPalmNormal, armAxis) * armAxis;
        if (palmNormal.sqrMagnitude < 0.01f)
            palmNormal = Vector3.Cross(armAxis, Vector3.up);
        palmNormal = palmNormal.normalized;

        // ── Position ──────────────────────────────────────────────────
        float armLen = Vector3.Distance(wristW, knuckleCenter);
        Vector3 surface = wristW + armAxis * (armLen * wristOffsetFactor);
        Vector3 targetPos = surface + arCamera.transform.forward * wristDepthOffset;

        // ── Rotation matrix ───────────────────────────────────────────
        // col[2] = armAxis     → hole along arm (local Z)
        // col[1] = palmNormal  → face tracks palm direction (local Y)
        // col[0] = crossAxis   → across wrist (local X)
        Vector3 crossAxis = Vector3.Cross(palmNormal, armAxis).normalized;

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(crossAxis.x, crossAxis.y, crossAxis.z, 0f));
        m.SetColumn(1, new Vector4(palmNormal.x, palmNormal.y, palmNormal.z, 0f));
        m.SetColumn(2, new Vector4(armAxis.x, armAxis.y, armAxis.z, 0f));
        Quaternion targetRot = m.rotation;

        // ── Smooth ────────────────────────────────────────────────────
        if (_firstFrame)
        {
            _smoothPos = targetPos; _smoothRot = targetRot;
            _posVelocity = Vector3.zero; _firstFrame = false;
        }
        else
        {
            _smoothPos = Vector3.SmoothDamp(
                _smoothPos, targetPos, ref _posVelocity, 1f / posSmooth, Mathf.Infinity, dt);
            _smoothRot = Quaternion.Slerp(_smoothRot, targetRot, rotSmooth * dt);
        }

        _bangle.transform.SetPositionAndRotation(_smoothPos, _smoothRot);
        _bangle.SetActive(true);

        _logT += dt;
        if (_logT >= 2f)
        {
            _logT = 0f;
            Debug.Log($"[BanglePlacer v35] scale={_calibratedScale.x:F4} " +
                      $"armAxis={armAxis:F2} palmNorm={palmNormal:F2}");
        }
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