// BanglePlacer.cs — v19 CAMERA-AWARE LANDMARK MAPPING
//
// FIX: Pass isBackCamera to LandmarkToWorld_Hand.Convert so that
// landmark X/Y are correctly un-mirrored for the back (World-facing) camera.
// The flag is read from ARCameraManager each frame so it stays in sync
// with JewelryManager's camera switching.

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class BanglePlacer : MonoBehaviour
{
    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

    [Header("Camera Reference (for back-camera landmark fix)")]
    [Tooltip("Assign the same ARCameraManager used by JewelryManager.")]
    public ARCameraManager arCameraManager;

    [Header("Depth — metres from camera to hand")]
    [Tooltip("How far the hand is from the phone.\n" +
             "Bracelet in FRONT of hand (between hand and camera) → DECREASE\n" +
             "Bracelet BEHIND hand → INCREASE\n" +
             "Start at 0.5, typical range 0.35–0.8")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Physical Size")]
    [Tooltip("The real-world diameter you want the bracelet to appear.\n" +
             "Average wrist inner diameter: 0.058–0.070m (5.8–7.0cm)\n" +
             "This is the ONLY size parameter you need to set.")]
    [Range(0.03f, 0.15f)] public float targetDiameterM = 0.065f;

    [Header("Wrist Placement")]
    [Tooltip("0 = exactly at wrist bone landmark.\n" +
             "Positive = slide toward palm. Negative = slide toward forearm.\n" +
             "Keep near 0 for correct placement.")]
    [Range(-0.2f, 0.2f)] public float wristOffsetFactor = 0.0f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 20f;
    [Range(1f, 40f)] public float rotSmooth = 14f;

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
    private int _texW, _texH, _frames;
    private float _logT;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[BanglePlacer] landmarkReader missing!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (arCamera && arCamera.nearClipPlane > 0.15f)
            Debug.LogWarning("[BanglePlacer] Set AR Camera nearClipPlane = 0.1 !");
        if (banglePrefab) SpawnBangle(banglePrefab);
        else Debug.LogWarning("[BanglePlacer] banglePrefab not assigned!");
        _ready = true;
    }

    public void SetBanglePrefab(GameObject p)
    {
        if (p) { banglePrefab = p; SpawnBangle(p); } else ClearBangle();
    }

    public void ClearBangle()
    {
        if (_bangle) { Destroy(_bangle); _bangle = null; }
        _firstFrame = true; _frames = 0; banglePrefab = null;
    }

    void SpawnBangle(GameObject prefab)
    {
        if (_bangle) Destroy(_bangle);

        _bangle = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        _bangle.SetActive(true);

        float measuredDiameter = MeasurePrefabDiameter(_bangle);

        if (measuredDiameter > 0.0001f)
        {
            float scaleFactor = targetDiameterM / measuredDiameter;
            _calibratedScale = _bangle.transform.localScale * scaleFactor;
            _bangle.transform.localScale = _calibratedScale;

            Debug.Log($"[BanglePlacer] Spawned '{prefab.name}' — " +
                      $"measured diameter={measuredDiameter:F4}m  " +
                      $"target={targetDiameterM:F4}m  " +
                      $"scaleFactor={scaleFactor:F4}  " +
                      $"calibratedScale={_calibratedScale}");
        }
        else
        {
            _calibratedScale = _bangle.transform.localScale;
            Debug.LogWarning("[BanglePlacer] Could not measure prefab bounds — " +
                             "keeping prefab scale. Add a Renderer to the prefab.");
        }

        _bangle.SetActive(false);
        _firstFrame = true;
        _frames = 0;
    }

    static float MeasurePrefabDiameter(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;
        Bounds total = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            total.Encapsulate(renderers[i].bounds);
        float diamXZ = Mathf.Max(total.size.x, total.size.z);
        return diamXZ > 0.0001f ? diamXZ : total.size.y;
    }

    /// <summary>Returns true when ARCameraManager is set to World (back camera).</summary>
    bool IsBackCamera()
    {
        if (arCameraManager == null) return false;
        return arCameraManager.currentFacingDirection == CameraFacingDirection.World;
    }

    void LateUpdate()
    {
        if (!_ready || !_bangle) return;
        UpdateTex();

        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 18)
        { _frames = 0; _bangle.SetActive(false); _firstFrame = true; return; }

        if (++_frames < minDetectionFrames) { _bangle.SetActive(false); return; }

        bool backCam = IsBackCamera();

        Vector3 wristW = C(0, backCam);
        Vector3 indexW = C(5, backCam);
        Vector3 midW = C(9, backCam);
        Vector3 ringW = C(13, backCam);
        Vector3 pinkyW = C(17, backCam);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 wristToKnuckle = (knuckleCenter - wristW).normalized;
        if (wristToKnuckle.sqrMagnitude < 0.001f) { _bangle.SetActive(false); return; }

        float armLen = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + wristToKnuckle * (armLen * wristOffsetFactor);

        Vector3 palmAcross = (pinkyW - indexW).normalized;
        Vector3 palmNormal = Vector3.Cross(wristToKnuckle, palmAcross).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;
        if (palmNormal.sqrMagnitude < 0.001f) palmNormal = arCamera.transform.forward;
        Quaternion targetRot = Quaternion.LookRotation(wristToKnuckle, palmNormal);

        float dt = Time.deltaTime;
        if (_firstFrame)
        {
            _smoothPos = targetPos;
            _smoothRot = targetRot;
            _posVelocity = Vector3.zero;
            _firstFrame = false;
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
            Debug.Log($"[BanglePlacer] backCam={backCam}  " +
                      $"baseDepth={baseDepth:F2}m  " +
                      $"targetDiam={targetDiameterM * 100:F1}cm  " +
                      $"calibScale={_calibratedScale}  " +
                      $"wrist={wristW:F3}  " +
                      $"camDist={Vector3.Distance(arCamera.transform.position, _smoothPos):F2}m");
        }
    }

    Vector3 C(int i, bool backCam) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera, _texW, _texH, baseDepth, backCam);

    void UpdateTex()
    {
        if (imageSourceBehaviour != null)
        {
            var s = imageSourceBehaviour.GetImageSource();
            if (s != null && s.isPrepared)
            { _texW = s.textureWidth; _texH = s.textureHeight; return; }
        }
        if (_texW == 0) { _texW = 720; _texH = 1280; }
    }
}