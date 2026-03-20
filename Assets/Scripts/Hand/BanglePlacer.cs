// BanglePlacer.cs — v31  DEFINITIVE: col[2]=holeAxis + Y correction calibrated
//
// ══════════════════════════════════════════════════════════════════════
// ROOT CAUSE OF "BRACELET ALWAYS FACE-ON" BUG (all v27–v30):
//
//   All previous versions put holeAxis in Matrix column 1 (local Y):
//     col[1] = holeAxis  ← assumed model's hole runs along local Y
//
//   PROOF this was wrong (from frame analysis):
//   In frame j2, arm goes LEFT-RIGHT horizontally (not toward camera).
//   If local Y = holeAxis = horizontal, and camera looks forward (Z axis),
//   camera is perpendicular to hole → should see bracelet from side = ellipse.
//   BUT we still see a full circle → local Y is NOT the hole axis in this model.
//
//   For the hole to always appear face-on to camera regardless of arm angle,
//   the model's hole axis must be LOCAL Z (forward/depth axis).
//   When col[2] = holeAxis = arm direction, and camera also looks along arm:
//   camera looks along holeAxis → face-on circle. ✓ Explains all frames.
//
// FIX: put holeAxis in col[2] (local Z), not col[1]:
//   col[0] = acrossWrist = cross(upRef, holeAxis)   [local X = across wrist]
//   col[1] = upRef = worldUp projected ⊥ arm         [local Y = up direction]
//   col[2] = holeAxis = wristToKnuckle               [local Z = hole axis = arm]
//
//   Now when camera looks from the SIDE of the arm:
//   camera looks perpendicular to col[2] → sees bracelet edge = ellipse ✓
//   When arm points at camera:
//   camera looks along col[2] = holeAxis → sees through hole = circle ✓
//   (same as looking through a real bracelet when it faces you directly)
//
// LANDMARK Y: bboxYCorrection = 0.05 with MINUS sign (v17 formula)
//   Positive value shifts dots DOWN. With ny=1-lm.y base, dots start too high;
//   0.05 correction brings them to correct joint positions.
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
    [Tooltip("Metres from camera to hand. Start at 0.5.")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Physical Size")]
    [Tooltip("Target diameter in metres. Average wrist: 0.058–0.070m.")]
    [Range(0.030f, 0.150f)] public float targetDiameterM = 0.065f;

    [Header("Wrist Position")]
    [Tooltip("0 = wrist landmark, positive = toward palm. 0.15 = wrist crease.")]
    [Range(-0.2f, 0.4f)] public float wristOffsetFactor = 0.15f;

    [Header("Landmark Y Correction")]
    [Tooltip("Positive = shift dots DOWN. With ny=1-lm.y base, dots start high.\n" +
             "Default 0.05. Increase if dots still above joints.\n" +
             "Decrease toward 0 if dots appear below joints.")]
    [Range(0f, 0.20f)] public float bboxYCorrection = 0.05f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 20f;
    [Range(1f, 40f)] public float rotSmooth = 18f;

    [Header("Persistence")]
    [Range(0, 30)] public int hideDelayFrames = 10;

    [Header("Stability")]
    [Range(0, 5)] public int minDetectionFrames = 2;

    // ── private ──────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────

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
            Debug.Log($"[BanglePlacer] measured={measured * 100:F1}cm " +
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
            if (++_lostFrames > hideDelayFrames)
            { _bangle.SetActive(false); _firstFrame = true; }
            return;
        }

        _lostFrames = 0;
        if (++_detFrames < minDetectionFrames) { _bangle.SetActive(false); return; }

        // ── World-space landmark positions ────────────────────────────
        Vector3 wristW = C(0);
        Vector3 indexW = C(5);
        Vector3 midW = C(9);
        Vector3 ringW = C(13);
        Vector3 pinkyW = C(17);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 holeAxis = (knuckleCenter - wristW).normalized;  // arm direction
        if (holeAxis.sqrMagnitude < 0.001f) return;

        // ── Position: slide along arm from wrist toward knuckles ──────
        float armLen = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + holeAxis * (armLen * wristOffsetFactor);

        // ── Orientation frame ─────────────────────────────────────────
        // upRef: world-up projected perpendicular to arm axis.
        // Keeps bracelet naturally level like a real bangle under gravity.
        Vector3 worldUp = Vector3.up;
        float upDotArm = Mathf.Abs(Vector3.Dot(holeAxis, worldUp));

        Vector3 upRef;
        if (upDotArm < 0.95f)
        {
            // Normal: project world-up onto the plane perpendicular to arm
            upRef = (worldUp - Vector3.Dot(worldUp, holeAxis) * holeAxis).normalized;
        }
        else
        {
            // Edge case: arm vertical → fall back to camera-right
            upRef = arCamera.transform.right;
            upRef = (upRef - Vector3.Dot(upRef, holeAxis) * holeAxis).normalized;
        }

        // acrossWrist: completes the orthonormal frame (horizontal across wrist)
        Vector3 acrossWrist = Vector3.Cross(upRef, holeAxis).normalized;

        // ── Build rotation matrix ─────────────────────────────────────
        // CRITICAL MAPPING (determined from model inspection via video analysis):
        //   Model's local Z = hole axis  (the axis the bracelet encircles)
        //   Model's local Y = upward direction on bracelet
        //   Model's local X = across-wrist direction
        //
        //   Matrix4x4 columns = where each local axis points in world space:
        //     col[0] = acrossWrist  →  local X points across wrist
        //     col[1] = upRef        →  local Y points upward (world-up reference)
        //     col[2] = holeAxis     →  local Z = hole = arm direction ← KEY FIX
        //
        //   With this mapping:
        //   • Bracelet ring plane is perpendicular to arm ✓
        //   • When viewed from palm side (perp to arm): see edge = ellipse ✓
        //   • When arm points at camera: see through hole = circle ✓ (physically correct)
        //   • Bracelet stays level (follows world-up) as wrist rotates ✓

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(acrossWrist.x, acrossWrist.y, acrossWrist.z, 0f)); // local X
        m.SetColumn(1, new Vector4(upRef.x, upRef.y, upRef.z, 0f)); // local Y
        m.SetColumn(2, new Vector4(holeAxis.x, holeAxis.y, holeAxis.z, 0f)); // local Z
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
            Debug.Log($"[BanglePlacer v31] holeAxis(col2)={holeAxis:F2} upRef(col1)={upRef:F2}");
        }
    }

    Vector3 C(int i) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera,
        _texW, _texH, baseDepth, false, bboxYCorrection);

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
        _texW = _rawTexW;
        _texH = _rawTexH;
    }
}