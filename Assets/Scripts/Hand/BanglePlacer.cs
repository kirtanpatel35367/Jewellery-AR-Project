// BanglePlacer.cs — v59 (Two-Copy Classic — Definitive)
//
// ══════════════════════════════════════════════════════════════════════════
// WHY ALL OCCLUDER APPROACHES FAILED
// ──────────────────────────────────────────────────────────────────────────
// The bangle prefab materials almost certainly have ZTest=Always or
// are set as transparent (ZWrite Off).  No depth-buffer occluder can
// affect objects that ignore the depth test.
//
// The ONLY approach guaranteed to work with ANY material is to control
// GameObject.SetActive() — no dependency on the GPU pipeline at all.
//
// ══════════════════════════════════════════════════════════════════════════
// HOW THIS WORKS
// ──────────────────────────────────────────────────────────────────────────
// We spawn TWO instances of the bangle prefab:
//   _front  — offset slightly toward the palm    (+palmNormal × wrapOffset)
//   _back   — offset slightly toward the dorsal  (-palmNormal × wrapOffset)
//
// Every frame we compute:
//   smoothedPalmNormal = _smoothRot * Vector3.up  (Local Y of root frame)
//   camDir             = (camera.position - bangCenter).normalized
//   viewDot            = Dot(smoothedPalmNormal, camDir)
//
//   viewDot > 0  → camera sees the PALM side  → show FRONT, hide BACK
//   viewDot < 0  → camera sees the DORSAL side → show BACK,  hide FRONT
//   |viewDot| near 0 → side-on view → show both for smooth crossover
//
// FIXED BUG — palmNormal was INVERTED for right hand:
//   Old: Cross(armAxis, across) → gives (0,0,-1) when palm faces camera.
//   New: Cross(across, armAxis) → gives (0,0,+1) when palm faces camera. ✓
//   The IsLeftHand flip is kept as-is for left-hand correction.
//
// ══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class BanglePlacer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;
    public ARCameraManager arCameraManager;

    [Header("Depth")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Size")]
    [Range(0.040f, 0.180f)] public float targetDiameterM = 0.090f;

    [Header("Wrist Position")]
    [Range(-0.1f, 0.5f)] public float wristOffsetFactor = 0.15f;

    [Header("Orientation Tuning")]
    [Tooltip("Rotate the bangle model to sit horizontal on the wrist.\n" +
             "X=90 lays the ring flat across the wrist (fixes vertical standing bangle).\n" +
             "If still wrong, try (90,0,0), (0,90,0), or (90,90,0) until flat.")]
    public Vector3 meshRotationOffset = new Vector3(90f, 0f, 0f);

    [Header("Front / Back Split")]
    [Tooltip("Small offset separating the two copies along palmNormal.\n" +
             "Keeps them from z-fighting each other. 0.005 m = 5 mm.")]
    [Range(0.001f, 0.020f)] public float wrapOffset = 0.005f;

    [Tooltip("viewDot threshold for switching copies.\n" +
             "0.05 means both copies are visible when side-on (viewDot ∈ [-0.05, 0.05]).\n" +
             "Increase for a wider blend zone.")]
    [Range(0f, 0.3f)] public float blendZone = 0.05f;

    [Tooltip("Controls which copy shows when the palm faces the camera.\n" +
             "UNTICKED (false) = correct for most phones (back-facing camera).\n" +
             "If the back of the bracelet shows when palm faces you, TICK this ON.")]
    public bool frontIsPalmSide = false;

    [Header("Landmark Y Correction")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Adaptive Scale")]
    public bool enableAdaptiveScale = true;
    [Range(50f, 400f)] public float referencePixelDist = 180f;
    [Range(2f, 20f)] public float scaleSmooth = 8f;
    [Range(0.3f, 1.5f)] public float minScaleMult = 0.5f;
    [Range(1f, 4f)] public float maxScaleMult = 3.0f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f;
    [Range(1f, 40f)] public float rotSmooth = 16f;

    [Header("Persistence")]
    [Range(0, 40)] public int hideDelayFrames = 20;
    [Range(0, 5)] public int minDetectionFrames = 2;

    // ── Private ───────────────────────────────────────────────────────────

    private GameObject _root;
    private GameObject _front;   // palm-facing copy
    private GameObject _back;    // dorsal-facing copy

    private Vector3 _calibratedScale = Vector3.one;
    private float _smoothScaleMult = 1f;

    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private Vector3 _posVelocity;
    private bool _firstFrame = true;
    private bool _ready;
    private int _detFrames, _lostFrames;
    private float _logTimer;

    private int _rawTexW, _rawTexH, _texW, _texH;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        if (!landmarkReader)
        { Debug.LogError("[BanglePlacer v59] landmarkReader not assigned!"); return; }
        if (!arCamera) arCamera = Camera.main;

        if (banglePrefab) SpawnBangle(banglePrefab);
        _ready = true;
    }

    void OnDestroy()
    {
        if (_root) Destroy(_root);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void SetBanglePrefab(GameObject p)
    {
        if (p) { banglePrefab = p; SpawnBangle(p); }
        else ClearBangle();
    }

    public void ClearBangle()
    {
        if (_root) { Destroy(_root); _root = null; }
        _front = _back = null;
        _firstFrame = true; _detFrames = 0; _lostFrames = 0; banglePrefab = null;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────

    void SpawnBangle(GameObject prefab)
    {
        if (_root) Destroy(_root);

        _root = new GameObject("BangleRoot");
        _root.transform.SetParent(transform, false);

        // Front copy — will be offset toward palm at runtime
        _front = Instantiate(prefab, _root.transform);
        _front.name = "Bangle_Front";
        _front.transform.localRotation = Quaternion.Euler(meshRotationOffset);
        _front.transform.localScale = Vector3.one;

        // Back copy — will be offset away from palm at runtime
        _back = Instantiate(prefab, _root.transform);
        _back.name = "Bangle_Back";
        _back.transform.localRotation = Quaternion.Euler(meshRotationOffset);
        _back.transform.localScale = Vector3.one;

        // Calibrate scale from prefab bounds
        float measured = MeasureDiameter(_front);
        _calibratedScale = measured > 0.0001f
            ? Vector3.one * (targetDiameterM / measured)
            : Vector3.one;

        if (measured > 0.0001f)
            Debug.Log($"[BanglePlacer v59] measured={measured * 100f:F1}cm " +
                      $"target={targetDiameterM * 100f:F1}cm sf={targetDiameterM / measured:F3}");
        else
            Debug.LogWarning("[BanglePlacer v59] No renderer found on prefab.");

        _smoothScaleMult = 1f;
        _root.SetActive(false);
        _firstFrame = true; _detFrames = 0; _lostFrames = 0;
    }

    // ── Per-frame ─────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (!_ready || !_root) return;
        UpdateTex();

        bool detected = landmarkReader.HandDetected && landmarkReader.LandmarkCount >= 18;
        if (!detected)
        {
            _detFrames = 0;
            if (++_lostFrames > hideDelayFrames)
            {
                _root.SetActive(false);
                _firstFrame = true;
            }
            return;
        }

        _lostFrames = 0;
        if (++_detFrames < minDetectionFrames) { _root.SetActive(false); return; }

        float dt = Time.deltaTime;

        // ── Landmarks ─────────────────────────────────────────────────
        Vector3 wristW = C(0);
        Vector3 indexW = C(5), midW = C(9), ringW = C(13), pinkyW = C(17);

        Vector3 knuckle = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 armAxis = (knuckle - wristW).normalized;
        if (armAxis.sqrMagnitude < 0.001f) return;

        float armLen = Vector3.Distance(wristW, knuckle);

        // ── Palm normal (FIXED) ────────────────────────────────────────
        // Cross(armAxis, across) was wrong — gave (0,0,-1) for right hand.
        // Cross(across, armAxis) gives (0,0,+1) pointing toward camera. ✓
        Vector3 across = (pinkyW - indexW).normalized;
        Vector3 palmNormal = Vector3.Cross(across, armAxis).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;

        // ── Bracelet centre ────────────────────────────────────────────
        Vector3 centre = wristW + armAxis * (armLen * wristOffsetFactor);

        // ── Root rotation ──────────────────────────────────────────────
        // Local X = crossAxis    (across wrist)
        // Local Y = orthoPalm    (toward palm — now correct)
        // Local Z = armAxis      (along forearm toward fingers)
        Vector3 crossAxis = Vector3.Cross(palmNormal, armAxis).normalized;
        if (crossAxis.sqrMagnitude < 0.001f)
            crossAxis = Vector3.Cross(armAxis, Vector3.up).normalized;
        Vector3 orthoPalm = Vector3.Cross(armAxis, crossAxis).normalized;

        Matrix4x4 mat = Matrix4x4.identity;
        mat.SetColumn(0, new Vector4(crossAxis.x, crossAxis.y, crossAxis.z, 0f));
        mat.SetColumn(1, new Vector4(orthoPalm.x, orthoPalm.y, orthoPalm.z, 0f));
        mat.SetColumn(2, new Vector4(armAxis.x, armAxis.y, armAxis.z, 0f));
        Quaternion targetRot = mat.rotation;

        // ── Smoothing ──────────────────────────────────────────────────
        if (_firstFrame)
        {
            _smoothPos = centre; _smoothRot = targetRot;
            _posVelocity = Vector3.zero; _firstFrame = false;
        }
        else
        {
            _smoothPos = Vector3.SmoothDamp(_smoothPos, centre,
                ref _posVelocity, 1f / posSmooth, Mathf.Infinity, dt);
            _smoothRot = Quaternion.Slerp(_smoothRot, targetRot, rotSmooth * dt);
        }

        _root.transform.SetPositionAndRotation(_smoothPos, _smoothRot);
        _root.SetActive(true);

        // ── Adaptive scale ─────────────────────────────────────────────
        float scaleMult = 1f;
        if (enableAdaptiveScale && _texW > 0)
        {
            Vector3 lm0 = landmarkReader.GetLandmark(0);
            Vector3 lm9 = landmarkReader.GetLandmark(9);
            float dist = Mathf.Sqrt(
                Mathf.Pow((lm9.x - lm0.x) * _texW, 2f) +
                Mathf.Pow((lm9.y - lm0.y) * _texH, 2f));
            if (dist > 10f)
            {
                float raw = Mathf.Clamp(dist / referencePixelDist, minScaleMult, maxScaleMult);
                _smoothScaleMult = Mathf.Lerp(_smoothScaleMult, raw, Mathf.Clamp01(scaleSmooth * dt));
            }
            scaleMult = _smoothScaleMult;
        }

        Vector3 finalScale = _calibratedScale * scaleMult;

        // ── Front / Back copy positioning ──────────────────────────────
        // Front: offset in +Local Y (toward palm).
        // Back:  offset in -Local Y (toward dorsal).
        // These are tiny offsets — just enough to separate the copies.
        _front.transform.localPosition = new Vector3(0f, wrapOffset, 0f);
        _back.transform.localPosition = new Vector3(0f, -wrapOffset, 0f);
        _front.transform.localScale = finalScale;
        _back.transform.localScale = finalScale;

        // ── View-dot copy visibility ───────────────────────────────────
        // smoothedPalmNormal = Local Y of root in world space.
        // viewDot > 0 : palm side toward camera → show FRONT copy.
        // viewDot < 0 : dorsal side toward camera → show BACK copy.
        // Both active inside the blendZone for a smooth crossover.
        Vector3 smoothedPN = _smoothRot * Vector3.up;   // Local Y = orthoPalm
        Vector3 camDir = (arCamera.transform.position - _smoothPos).normalized;
        float viewDot = Vector3.Dot(smoothedPN, camDir);

        // frontIsPalmSide toggle: flip logic if wrong side shows
        float signedDot = frontIsPalmSide ? viewDot : -viewDot;

        bool showFront = signedDot >= -blendZone;
        bool showBack = signedDot <= blendZone;

        _front.SetActive(showFront);
        _back.SetActive(showBack);

        // ── Log ───────────────────────────────────────────────────────
        _logTimer += dt;
        if (_logTimer >= 2f)
        {
            _logTimer = 0f;
            Debug.Log($"[BanglePlacer v59] viewDot={viewDot:F2} " +
                      $"front={showFront} back={showBack} scale={scaleMult:F2}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    Vector3 C(int i) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera,
        _texW, _texH, baseDepth, false, bboxYCorrection);

    static float MeasureDiameter(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        float dxz = Mathf.Max(b.size.x, b.size.z);
        return dxz > 0.0001f ? dxz : b.size.y;
    }

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