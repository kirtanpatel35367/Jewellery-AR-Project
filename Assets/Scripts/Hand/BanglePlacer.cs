// BanglePlacer.cs — v32  CALIBRATED from video analysis
//
// Changes from v31:
//   • bboxYCorrection 0.05 → 0.02  (pixel-measured: dots 2% too high at 0.05)
//   • wristOffsetFactor 0.15 → 0.20  (bracelet slightly below wrist crease at 0.15)
//   • Orientation: col[2]=holeAxis confirmed CORRECT from video k3/k5 (wraps horizontally ✓)
//   • World-up reference retained: keeps bracelet level like real gravity

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
    [Tooltip("Metres from camera to hand. Start at 0.5.")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Physical Size")]
    [Tooltip("Target diameter in metres. Average wrist 0.058–0.070m.")]
    [Range(0.030f, 0.150f)] public float targetDiameterM = 0.065f;

    [Header("Wrist Position")]
    [Tooltip("0 = wrist landmark, positive = toward palm.\n" +
             "0.20 places bracelet on the wrist crease (calibrated from video).")]
    [Range(-0.2f, 0.4f)] public float wristOffsetFactor = 0.20f;

    [Header("Landmark Y Correction")]
    [Tooltip("Positive = shift dots DOWN by this fraction of screen height.\n" +
             "0.02 = calibrated from pixel measurement (dots were 2% too high at 0.05).\n" +
             "Increase if dots still above joints. Decrease if below.")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 20f;
    [Range(1f, 40f)] public float rotSmooth = 18f;

    [Header("Persistence")]
    [Range(0, 30)] public int hideDelayFrames = 10;

    [Header("Stability")]
    [Range(0, 5)] public int minDetectionFrames = 2;

    private GameObject _bangle;
    private Vector3 _calibratedScale;
    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private Vector3 _posVelocity;
    private bool _firstFrame = true;
    private bool _ready;
    private int _detFrames, _lostFrames;
    private float _logT;
    private int _rawTexW, _rawTexH, _texW, _texH;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[BanglePlacer] landmarkReader missing!"); return; }
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

        float measured = MeasureDiameter(_bangle);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = _bangle.transform.localScale * sf;
            _bangle.transform.localScale = _calibratedScale;
            Debug.Log($"[BanglePlacer v32] measured={measured * 100:F1}cm " +
                      $"target={targetDiameterM * 100:F1}cm sf={sf:F4}");
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

        // ── Landmark world positions ──────────────────────────────────
        Vector3 wristW = C(0);
        Vector3 indexW = C(5);
        Vector3 midW = C(9);
        Vector3 ringW = C(13);
        Vector3 pinkyW = C(17);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 holeAxis = (knuckleCenter - wristW).normalized;
        if (holeAxis.sqrMagnitude < 0.001f) return;

        // ── Position ──────────────────────────────────────────────────
        float armLen = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + holeAxis * (armLen * wristOffsetFactor);

        // ── Orientation ───────────────────────────────────────────────
        // upRef: world-up projected perpendicular to arm — keeps bracelet level
        Vector3 worldUp = Vector3.up;
        float upDotArm = Mathf.Abs(Vector3.Dot(holeAxis, worldUp));

        Vector3 upRef;
        if (upDotArm < 0.95f)
        {
            upRef = (worldUp - Vector3.Dot(worldUp, holeAxis) * holeAxis).normalized;
        }
        else
        {
            // Arm vertical — use camera right as fallback
            Vector3 cr = arCamera.transform.right;
            upRef = (cr - Vector3.Dot(cr, holeAxis) * holeAxis).normalized;
        }

        // acrossWrist: completes the orthonormal frame
        Vector3 acrossWrist = Vector3.Cross(upRef, holeAxis).normalized;

        // ── Rotation matrix ───────────────────────────────────────────
        // Confirmed from video analysis (k3, k5): this model's hole = local Z
        //   col[0] = acrossWrist  → local X (horizontal across wrist)
        //   col[1] = upRef        → local Y (world-up on bracelet plane)
        //   col[2] = holeAxis     → local Z (arm/hole direction) ← key mapping
        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(acrossWrist.x, acrossWrist.y, acrossWrist.z, 0f));
        m.SetColumn(1, new Vector4(upRef.x, upRef.y, upRef.z, 0f));
        m.SetColumn(2, new Vector4(holeAxis.x, holeAxis.y, holeAxis.z, 0f));
        Quaternion targetRot = m.rotation;

        // ── Smooth ────────────────────────────────────────────────────
        float dt = Time.deltaTime;
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
            Debug.Log($"[BanglePlacer v32] holeAxis={holeAxis:F2} upRef={upRef:F2} pos={targetPos:F3}");
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