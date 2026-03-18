// BanglePlacer.cs — v18 SELF-CALIBRATING SCALE
//
// ROOT CAUSE (confirmed from video measurement):
//   The bracelet prefab's world-space size is ~10x too large.
//   (It fills 100% of screen width = ~0.64m at 0.5m depth.
//    A real bracelet needs to be ~0.065m = 6.5cm.)
//
//   Previous attempts all failed because:
//   - Manual sizeMultiplier → user can't know what number to enter
//   - prefabDiameterUnits → same problem, requires measuring the model
//   - "preserve prefab scale" → the prefab scale is already wrong for AR world coords
//
// DEFINITIVE FIX:
//   At spawn time, measure the prefab's ACTUAL rendered world-space diameter
//   using Renderer.bounds (works on any mesh, any scale).
//   Then compute exactly: localScale = (targetDiameterM / measuredDiameter) * prefabScale
//   This is fully automatic — works with any prefab, zero manual tuning.
//
// ONLY THING TO TUNE:
//   targetDiameterM  — the physical bracelet diameter you want (default 0.065 = 6.5cm)
//   baseDepth        — how far the hand is from camera (default 0.5m)

using UnityEngine;

public class BanglePlacer : MonoBehaviour
{
    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

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
    private Vector3 _calibratedScale;  // auto-computed at spawn, never changes
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

        // Instantiate at origin with identity so bounds are unaffected by scene transform
        _bangle = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        _bangle.SetActive(true); // must be active to get renderer bounds

        // ── SELF-CALIBRATING SCALE ────────────────────────────────────
        // Measure the prefab's actual rendered world-space size right now.
        // We use the largest horizontal extent (X or Z) as the "diameter".
        float measuredDiameter = MeasurePrefabDiameter(_bangle);

        if (measuredDiameter > 0.0001f)
        {
            // Compute exactly how much to scale so world diameter = targetDiameterM
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
            // Fallback: no renderer found, keep prefab scale as-is
            _calibratedScale = _bangle.transform.localScale;
            Debug.LogWarning("[BanglePlacer] Could not measure prefab bounds — " +
                             "keeping prefab scale. Add a Renderer to the prefab.");
        }

        _bangle.SetActive(false);
        _firstFrame = true;
        _frames = 0;
    }

    // Measures the largest horizontal diameter of the prefab's rendered mesh bounds.
    // Works regardless of how the prefab was modelled or what scale it has.
    static float MeasurePrefabDiameter(GameObject go)
    {
        // Collect all renderers including children
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        // Encapsulate all renderer bounds into one
        Bounds total = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            total.Encapsulate(renderers[i].bounds);

        // Use the max of X and Z size as the "diameter" (bracelet lies flat on XZ plane)
        // This handles both horizontal and vertical prefab orientations
        float diamXZ = Mathf.Max(total.size.x, total.size.z);
        // Fallback to Y if X and Z are both tiny (vertical torus orientation)
        float diam = diamXZ > 0.0001f ? diamXZ : total.size.y;

        return diam;
    }

    void LateUpdate()
    {
        if (!_ready || !_bangle) return;
        UpdateTex();

        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 18)
        { _frames = 0; _bangle.SetActive(false); _firstFrame = true; return; }

        if (++_frames < minDetectionFrames) { _bangle.SetActive(false); return; }

        // World positions of key landmarks at baseDepth from camera
        Vector3 wristW = C(0);
        Vector3 indexW = C(5);
        Vector3 midW = C(9);
        Vector3 ringW = C(13);
        Vector3 pinkyW = C(17);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 wristToKnuckle = (knuckleCenter - wristW).normalized;
        if (wristToKnuckle.sqrMagnitude < 0.001f) { _bangle.SetActive(false); return; }

        // Bracelet sits at wrist, with optional offset
        float armLen = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + wristToKnuckle * (armLen * wristOffsetFactor);

        // Orientation: ring plane perpendicular to wrist axis
        Vector3 palmAcross = (pinkyW - indexW).normalized;
        Vector3 palmNormal = Vector3.Cross(wristToKnuckle, palmAcross).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;
        if (palmNormal.sqrMagnitude < 0.001f) palmNormal = arCamera.transform.forward;
        Quaternion targetRot = Quaternion.LookRotation(wristToKnuckle, palmNormal);

        // Smooth position and rotation — scale is fixed (calibrated at spawn)
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
        // Scale is already set at spawn — do NOT touch it here
        _bangle.SetActive(true);

        _logT += dt;
        if (_logT >= 2f)
        {
            _logT = 0f;
            Debug.Log($"[BanglePlacer] " +
                      $"baseDepth={baseDepth:F2}m  " +
                      $"targetDiam={targetDiameterM * 100:F1}cm  " +
                      $"calibScale={_calibratedScale}  " +
                      $"wrist={wristW:F3}  " +
                      $"camDist={Vector3.Distance(arCamera.transform.position, _smoothPos):F2}m");
        }
    }

    Vector3 C(int i) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera, _texW, _texH, baseDepth);

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