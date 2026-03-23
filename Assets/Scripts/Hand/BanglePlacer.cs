// BanglePlacer.cs — v55
// Base: v35 (stable, correct colour, no shader)
//
// ══════════════════════════════════════════════════════════════════════
// BUG IN v54 — Why front/back split still didn't work:
//
//   v54 used this to set front/back positions:
//     _front.transform.localPosition =
//         _root.transform.InverseTransformPoint(_smoothPos + frontOffset);
//
//   THIS IS WRONG. Here's why:
//   - _root is placed at _smoothPos via SetPositionAndRotation
//   - So _root.position == _smoothPos
//   - InverseTransformPoint(_smoothPos + offset) converts world-pos to
//     local-space of _root. Since _root is AT _smoothPos, this gives
//     the LOCAL translation of `offset` which is correct in theory...
//     BUT the _root is also ROTATED to match the bracelet orientation.
//   - InverseTransformPoint applies the inverse rotation, so the local
//     offset is in rotated space — but `frontOffset = camDir * sep`
//     is in WORLD space. This gives wrong offset direction.
//
//   The correct fix: express the offset IN THE ROOT'S LOCAL SPACE.
//   camDir in world space → transform to root local space using
//   Quaternion.Inverse(_smoothRot) * camDir.
//   Then _front.localPosition = localCamDir * wrapSeparation
//
// ALSO FIXED in v55:
//   The front/back switch logic uses the FINAL smoothed viewDot.
//   Previous versions sometimes flickered because viewDot was computed
//   before the smoothed rotation converged. Now computed from _smoothRot.
//
// HOW THE WRAP ILLUSION WORKS:
//
//   _root sits at wrist centre, oriented to match the wrist.
//   In the root's local space:
//     Local Y = palmNormal direction (toward palm)
//     Local Z = armAxis (along forearm)
//     Local X = across wrist
//
//   frontOffset = +localY * wrapSeparation
//     → front instance is shifted toward palm (+Y in local space)
//     → in world: front is on the palm side of the wrist centre
//
//   backOffset  = -localY * wrapSeparation
//     → back instance is shifted toward back of hand (-Y in local)
//     → in world: back is on the dorsal side of the wrist centre
//
//   When palm faces camera:
//     Front (palm-side) is nearer to camera → renderQueue 2999 → on top
//     Back (dorsal-side) is farther → renderer.enabled=false → hidden
//     Visible: FRONT ARC only ✓
//
//   When back of hand faces camera:
//     Back (dorsal-side) is nearer to camera
//     Front (palm-side) is farther → renderer.enabled=false → hidden
//     Visible: BACK ARC only ✓
//
//   X-scale foreshortening: bracelet width compresses as wrist tilts,
//   giving the illusion of curving around the wrist cylinder.
//
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
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Bracelet Size")]
    [Tooltip("Outer diameter in metres. Increase if too small on wrist.")]
    [Range(0.040f, 0.140f)] public float targetDiameterM = 0.090f;

    [Header("Wrist Position")]
    [Tooltip("0=wrist bone, 0.15=wrist crease")]
    [Range(-0.1f, 0.4f)] public float wristOffsetFactor = 0.15f;

    [Header("Landmark Y Correction")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Wrap Illusion")]
    [Tooltip("Separates front/back instances along palmNormal (metres).\n" +
             "Front shifts toward palm. Back shifts toward back-of-hand.\n" +
             "8-12mm recommended. Creates the 'going through the wrist' depth.")]
    [Range(0.003f, 0.030f)] public float wrapSeparation = 0.010f;

    [Tooltip("How much bracelet width compresses at side view.\n" +
             "1.0 = full cylinder foreshortening (most realistic).\n" +
             "0.0 = no compression (flat sticker).")]
    [Range(0f, 1f)] public float edgeCurvature = 0.80f;

    [Tooltip("Curve sharpness. 0.6=gentle. 1.0=linear. 1.5=sharp edges.")]
    [Range(0.3f, 2f)] public float curvePow = 0.65f;

    [Tooltip("Hysteresis dead-band at front/back transition. Prevents flicker.")]
    [Range(0f, 0.20f)] public float switchHysteresis = 0.10f;

    [Header("Adaptive Scale")]
    public bool enableAdaptiveScale = true;

    [Tooltip("Pixel dist wrist→mid-knuckle at your normal filming distance.\n" +
             "Check Console log 'pixelDist=XXX' and set this value.")]
    [Range(50f, 400f)] public float referencePixelDist = 180f;

    [Range(2f, 20f)] public float scaleSmooth = 8f;
    [Range(0.3f, 1f)] public float minScaleMult = 0.5f;
    [Range(1f, 3.5f)] public float maxScaleMult = 3.0f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f;
    [Range(1f, 40f)] public float rotSmooth = 16f;

    [Header("Persistence")]
    [Range(0, 40)] public int hideDelayFrames = 20;
    [Range(0, 5)] public int minDetectionFrames = 2;

    // ── private ──────────────────────────────────────────────────────
    private GameObject _root;
    private GameObject _front;          // offset +Y (toward palm)
    private GameObject _back;           // offset -Y (toward back-of-hand)
    private Renderer[] _frontRends;
    private Renderer[] _backRends;
    private Vector3 _calibratedScale;
    private float _smoothScaleMult = 1f;
    private bool _showingFront = true;

    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private Vector3 _posVelocity;
    private Vector3 _smoothPalmNormal = Vector3.forward;
    private bool _firstFrame = true;
    private bool _ready;
    private int _detFrames, _lostFrames;
    private float _logT;
    private int _rawTexW, _rawTexH, _texW, _texH;

    // ─────────────────────────────────────────────────────────────────
    void Start()
    {
        if (!landmarkReader)
        { Debug.LogError("[BanglePlacer v55] landmarkReader not assigned!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (banglePrefab) SpawnBangle(banglePrefab);
        _ready = true;
    }

    public void SetBanglePrefab(GameObject p)
    { if (p) { banglePrefab = p; SpawnBangle(p); } else ClearBangle(); }

    public void ClearBangle()
    {
        if (_root) { Destroy(_root); _root = null; }
        _front = null; _back = null;
        _frontRends = null; _backRends = null;
        _firstFrame = true; _detFrames = 0; _lostFrames = 0; banglePrefab = null;
    }

    void SpawnBangle(GameObject prefab)
    {
        if (_root) Destroy(_root);

        _root = new GameObject("BangleRoot");
        _root.transform.SetParent(transform, false);

        // Front: local +Y = toward palm side
        _front = Instantiate(prefab, _root.transform);
        _front.name = "BangleFront";
        _front.transform.localRotation = Quaternion.identity;
        _front.transform.localScale = Vector3.one;

        // Back: local -Y = toward back-of-hand side
        _back = Instantiate(prefab, _root.transform);
        _back.name = "BangleBack";
        _back.transform.localRotation = Quaternion.identity;
        _back.transform.localScale = Vector3.one;

        _frontRends = _front.GetComponentsInChildren<Renderer>();
        _backRends = _back.GetComponentsInChildren<Renderer>();

        // Front draws on top of back
        SetRenderQueue(_frontRends, 2999);
        SetRenderQueue(_backRends, 2998);

        // Initial state: front visible
        SetEnabled(_frontRends, true);
        SetEnabled(_backRends, false);
        _showingFront = true;

        // Scale calibration
        float measured = MeasureDiameter(_front);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = Vector3.one * sf;
            Debug.Log($"[BanglePlacer v55] measured={measured * 100:F1}cm " +
                      $"target={targetDiameterM * 100:F1}cm sf={sf:F3}");
        }
        else
        {
            _calibratedScale = Vector3.one;
            Debug.LogWarning("[BanglePlacer v55] No renderer found.");
        }

        _smoothScaleMult = 1f;
        _root.SetActive(false);
        _firstFrame = true; _detFrames = 0; _lostFrames = 0;
    }

    void SetRenderQueue(Renderer[] rends, int q)
    { foreach (var r in rends) if (r?.material != null) r.material.renderQueue = q; }

    void SetEnabled(Renderer[] rends, bool val)
    { if (rends != null) foreach (var r in rends) if (r) r.enabled = val; }

    static float MeasureDiameter(GameObject go)
    {
        var r = go.GetComponentsInChildren<Renderer>();
        if (r.Length == 0) return 0f;
        Bounds b = r[0].bounds;
        for (int i = 1; i < r.Length; i++) b.Encapsulate(r[i].bounds);
        float dxz = Mathf.Max(b.size.x, b.size.z);
        return dxz > 0.0001f ? dxz : b.size.y;
    }

    // ─────────────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (!_ready || !_root) return;
        UpdateTex();

        bool detected = landmarkReader.HandDetected && landmarkReader.LandmarkCount >= 18;
        if (!detected)
        {
            _detFrames = 0;
            if (++_lostFrames > hideDelayFrames)
            { _root.SetActive(false); _firstFrame = true; }
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

        // ── Palm normal ────────────────────────────────────────────────
        Vector3 across = (pinkyW - indexW).normalized;
        Vector3 rawPN = Vector3.Cross(armAxis, across).normalized;
        if (landmarkReader.IsLeftHand) rawPN = -rawPN;

        if (_firstFrame)
            _smoothPalmNormal = rawPN;
        else
            _smoothPalmNormal = Vector3.Slerp(_smoothPalmNormal, rawPN,
                Mathf.Clamp01(rotSmooth * 0.25f * dt)).normalized;

        Vector3 palmNormal = _smoothPalmNormal
            - Vector3.Dot(_smoothPalmNormal, armAxis) * armAxis;
        if (palmNormal.sqrMagnitude < 0.01f)
            palmNormal = Vector3.Cross(armAxis, Vector3.up);
        palmNormal = palmNormal.normalized;

        // ── Bracelet centre ────────────────────────────────────────────
        float armLen = Vector3.Distance(wristW, knuckle);
        Vector3 centre = wristW + armAxis * (armLen * wristOffsetFactor);

        // ── Rotation ───────────────────────────────────────────────────
        // Local Y = palmNormal (palm direction = +Y in bracelet local space)
        // Local Z = armAxis   (along forearm)
        // Local X = crossAxis (across wrist)
        Vector3 crossAxis = Vector3.Cross(palmNormal, armAxis).normalized;
        if (crossAxis.sqrMagnitude < 0.001f)
            crossAxis = Vector3.Cross(armAxis, Vector3.up).normalized;
        // Recalculate palmNormal to be exactly orthogonal to both
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

        // Place root at wrist centre with correct rotation
        _root.transform.SetPositionAndRotation(_smoothPos, _smoothRot);
        _root.SetActive(true);

        // ── ADAPTIVE SCALE ─────────────────────────────────────────────
        float scaleMult = 1f;
        if (enableAdaptiveScale && _texW > 0)
        {
            Vector3 lm0 = landmarkReader.GetLandmark(0);
            Vector3 lm9 = landmarkReader.GetLandmark(9);
            float px = (lm9.x - lm0.x) * _texW;
            float py = (lm9.y - lm0.y) * _texH;
            float pxDist = Mathf.Sqrt(px * px + py * py);
            if (pxDist > 10f)
            {
                float raw = Mathf.Clamp(pxDist / referencePixelDist, minScaleMult, maxScaleMult);
                _smoothScaleMult = Mathf.Lerp(_smoothScaleMult, raw,
                    Mathf.Clamp01(scaleSmooth * dt));
            }
            scaleMult = _smoothScaleMult;
        }

        // ── VIEW DOT — how much palm faces camera ──────────────────────
        // Use smoothed palmNormal (from _smoothRot's Y axis) for stability
        Vector3 smoothedPalmNormal = _smoothRot * Vector3.up;  // local Y = palm direction
        Vector3 camDir = (arCamera.transform.position - _smoothPos).normalized;
        float viewDot = Vector3.Dot(smoothedPalmNormal, camDir);

        // ── FRONT/BACK SWITCH ──────────────────────────────────────────
        // viewDot > 0: palm toward cam → show front
        // viewDot < 0: back toward cam → show back
        bool shouldFront;
        if (_showingFront)
            shouldFront = viewDot > -switchHysteresis;
        else
            shouldFront = viewDot > switchHysteresis;

        if (shouldFront != _showingFront)
        {
            _showingFront = shouldFront;
            SetEnabled(_frontRends, _showingFront);
            SetEnabled(_backRends, !_showingFront);
        }

        // ── X-SCALE FORESHORTENING ─────────────────────────────────────
        // Compress bracelet width as wrist rotates toward side view.
        // |viewDot|: 1=face-on (full width), 0=side-on (compressed)
        float absViewDot = Mathf.Abs(viewDot);
        float compress = Mathf.Pow(absViewDot, curvePow);
        float xScale = Mathf.Lerp(1f, compress, edgeCurvature);
        xScale = Mathf.Max(xScale, 0.08f);

        Vector3 baseScale = _calibratedScale * scaleMult;
        Vector3 arcScale = new Vector3(baseScale.x * xScale, baseScale.y, baseScale.z);

        // ── DEPTH SEPARATION — THE KEY FIX ────────────────────────────
        // Front/back are offset in LOCAL Y (palm normal direction).
        // Since the root is rotated so local Y = palmNormal:
        //   localPos (0, +wrapSeparation, 0) = toward palm in world
        //   localPos (0, -wrapSeparation, 0) = toward back-of-hand in world
        //
        // This is the CORRECT way — no world→local conversion needed.
        // Simply set the local position directly.
        _front.transform.localPosition = new Vector3(0f, wrapSeparation, 0f);
        _front.transform.localScale = arcScale;

        _back.transform.localPosition = new Vector3(0f, -wrapSeparation, 0f);
        _back.transform.localScale = arcScale;

        // ── LOG ────────────────────────────────────────────────────────
        _logT += dt;
        if (_logT >= 2f)
        {
            _logT = 0f;
            Vector3 l0 = landmarkReader.GetLandmark(0);
            Vector3 l9 = landmarkReader.GetLandmark(9);
            float pd = Mathf.Sqrt(
                Mathf.Pow((l9.x - l0.x) * _texW, 2f) +
                Mathf.Pow((l9.y - l0.y) * _texH, 2f));
            Debug.Log($"[BanglePlacer v55] viewDot={viewDot:F2} " +
                      $"front={_showingFront} xScale={xScale:F2} " +
                      $"scaleMult={_smoothScaleMult:F2} pixelDist={pd:F0}");
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
        _texW = _rawTexW; _texH = _rawTexH;
    }

    void OnDestroy()
    { if (_root) Destroy(_root); }
}