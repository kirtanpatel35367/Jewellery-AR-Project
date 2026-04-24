// RingPlacer.cs — v7 (Two-Copy Classic — Definitive)
//
// ══════════════════════════════════════════════════════════════════════════
// APPROACH: Same guaranteed two-copy method as BanglePlacer v59.
//
//   _front — offset slightly toward palm  (+palmNormal × wrapOffset)
//   _back  — offset slightly toward dorsal(-palmNormal × wrapOffset)
//
//   viewDot = Dot(smoothedPalmNormal, camDir)
//   viewDot > 0  → palm side toward camera → show FRONT, hide BACK
//   viewDot < 0  → dorsal side toward camera → show BACK,  hide FRONT
//   |viewDot| ≤ blendZone → side view → show BOTH (smooth crossover)
//
// FIXED: palmNormal = Cross(toCamera, fingerAxis) — was Cross(fingerAxis, toCamera).
//
// No depth-buffer dependency → works with ANY ring material settings.
// ══════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class RingPlacer : MonoBehaviour
{
    public enum FingerTarget { Index, Middle, Ring, Pinky }

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject ringPrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

    [Header("Finger")]
    public FingerTarget finger = FingerTarget.Ring;

    [Header("Placement")]
    [Tooltip("0 = knuckle (MCP), 1 = first joint (PIP). 0.3 = typical ring seat.")]
    [Range(0f, 1f)] public float fingerBias = 0.30f;

    [Header("Depth")]
    [Range(0.3f, 1.5f)] public float baseDepth  = 0.65f;
    [Range(0f,  0.3f)]  public float depthZScale = 0.12f;

    [Header("Ring Size")]
    [Tooltip("Diameter relative to gap between adjacent MCPs. 1.1 = slightly loose.")]
    [Range(0.5f, 2.5f)] public float sizeMultiplier = 1.1f;

    [Header("Orientation Tuning")]
    [Tooltip("Rotate the ring model to sit correctly on the finger.\n" +
             "Most models need (0, 0, 90).")]
    public Vector3 meshRotationOffset = new Vector3(0f, 0f, 90f);

    [Header("Front / Back Split")]
    [Tooltip("Small offset separating the two copies along palmNormal (metres).")]
    [Range(0.001f, 0.015f)] public float wrapOffset = 0.003f;

    [Tooltip("viewDot range where both copies are visible (smooth crossover).")]
    [Range(0f, 0.3f)] public float blendZone = 0.05f;

    [Tooltip("Untick if the wrong face of the ring shows toward the palm.")]
    public bool frontIsPalmSide = true;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth   = 22f;
    [Range(1f, 40f)] public float rotSmooth   = 16f;
    [Range(1f, 20f)] public float scaleSmooth = 8f;

    [Header("Stability")]
    [Range(0, 10)] public int minDetectionFrames = 3;

    // ── Private ───────────────────────────────────────────────────────────

    private GameObject _front;
    private GameObject _back;

    private Vector3    _smoothPos;
    private Quaternion _smoothRot   = Quaternion.identity;
    private float      _smoothScale = -1f;
    private Vector3    _posVelocity = Vector3.zero;
    private bool       _firstFrame  = true;
    private bool       _ready;
    private int        _texW = 0, _texH = 0;
    private int        _detectionFrames = 0;

    private static readonly int[,] FingerLandmarks =
    {
        {  5,  6,  7,  8 },   // Index
        {  9, 10, 11, 12 },   // Middle
        { 13, 14, 15, 16 },   // Ring
        { 17, 18, 19, 20 },   // Pinky
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        if (!landmarkReader)
        { Debug.LogError("[RingPlacer v7] landmarkReader missing!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (ringPrefab != null) SpawnRing(ringPrefab);
        _ready = true;
    }

    void OnDestroy()
    {
        if (_front) Destroy(_front);
        if (_back)  Destroy(_back);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void SetRingPrefab(GameObject prefab)
    {
        if (prefab == null) { ClearRing(); return; }
        ringPrefab = prefab;
        SpawnRing(prefab);
    }

    public void ClearRing()
    {
        if (_front) { Destroy(_front); _front = null; }
        if (_back)  { Destroy(_back);  _back  = null; }
        _firstFrame = true; _detectionFrames = 0; ringPrefab = null;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────

    private void SpawnRing(GameObject prefab)
    {
        if (_front) Destroy(_front);
        if (_back)  Destroy(_back);

        _front      = Instantiate(prefab, transform);
        _front.name = "Ring_Front";
        _front.transform.localRotation = Quaternion.Euler(meshRotationOffset);
        _front.SetActive(false);

        _back       = Instantiate(prefab, transform);
        _back.name  = "Ring_Back";
        _back.transform.localRotation = Quaternion.Euler(meshRotationOffset);
        _back.SetActive(false);

        _firstFrame = true; _smoothScale = -1f; _detectionFrames = 0;
        Debug.Log("[RingPlacer v7] Spawned: " + prefab.name + " on " + finger);
    }

    // ── Tracking ──────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (!_ready || _front == null) return;
        UpdateTextureDimensions();

        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 21)
        {
            _detectionFrames = 0;
            _front.SetActive(false);
            _back.SetActive(false);
            _firstFrame = true;
            return;
        }

        _detectionFrames++;
        if (_detectionFrames < minDetectionFrames)
        {
            _front.SetActive(false);
            _back.SetActive(false);
            return;
        }

        int fi     = (int)finger;
        int mcpIdx = FingerLandmarks[fi, 0];
        int pipIdx = FingerLandmarks[fi, 1];
        int adjIdx = (fi == 0) ? FingerLandmarks[1, 0] : FingerLandmarks[fi - 1, 0];

        Vector3 mcpN   = landmarkReader.GetLandmark(mcpIdx);
        Vector3 pipN   = landmarkReader.GetLandmark(pipIdx);
        Vector3 adjN   = landmarkReader.GetLandmark(adjIdx);
        Vector3 wristN = landmarkReader.GetLandmark(0);

        float   depth = Mathf.Clamp(baseDepth + wristN.z * depthZScale, 0.25f, 1.5f);

        Vector3 mcpW = ConvertAt(mcpN, depth);
        Vector3 pipW = ConvertAt(pipN, depth);
        Vector3 adjW = ConvertAt(adjN, depth);

        Vector3 fingerAxis = (pipW - mcpW).normalized;
        if (fingerAxis.sqrMagnitude < 0.001f)
        {
            _front.SetActive(false); _back.SetActive(false); return;
        }

        Vector3 targetPos   = Vector3.Lerp(mcpW, pipW, fingerBias);
        float   fingerWidth = Vector3.Distance(mcpW, adjW);
        float   targetScale = fingerWidth * sizeMultiplier;

        if (targetScale < 0.0005f || float.IsNaN(targetScale))
        {
            _front.SetActive(false); _back.SetActive(false); return;
        }

        // ── Ring rotation ──────────────────────────────────────────────
        // Build orthonormal frame:
        //   Local Z = fingerAxis   (hole axis, along finger)
        //   Local Y = palmNormal   (toward palm)
        //   Local X = crossAxis    (across finger)
        //
        // FIXED: Cross(toCamera, fingerAxis) — was Cross(fingerAxis, toCamera).
        Vector3 toCamera   = (arCamera.transform.position - mcpW).normalized;
        Vector3 palmNormal = Vector3.Cross(toCamera, fingerAxis).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;
        if (palmNormal.sqrMagnitude < 0.001f) palmNormal = arCamera.transform.up;

        Vector3 crossAxis = Vector3.Cross(palmNormal, fingerAxis).normalized;
        if (crossAxis.sqrMagnitude < 0.001f)
            crossAxis = Vector3.Cross(fingerAxis, Vector3.up).normalized;
        Vector3 orthoUp = Vector3.Cross(fingerAxis, crossAxis).normalized;

        Matrix4x4 mat = Matrix4x4.identity;
        mat.SetColumn(0, new Vector4(crossAxis.x,  crossAxis.y,  crossAxis.z,  0f));
        mat.SetColumn(1, new Vector4(orthoUp.x,    orthoUp.y,    orthoUp.z,    0f));
        mat.SetColumn(2, new Vector4(fingerAxis.x, fingerAxis.y, fingerAxis.z, 0f));
        Quaternion targetRot = mat.rotation * Quaternion.Euler(meshRotationOffset);

        float dt = Time.deltaTime;
        if (_firstFrame || _smoothScale < 0f)
        {
            _smoothPos   = targetPos;
            _smoothRot   = targetRot;
            _smoothScale = targetScale;
            _posVelocity = Vector3.zero;
            _firstFrame  = false;
        }
        else
        {
            _smoothPos   = Vector3.SmoothDamp(_smoothPos, targetPos,
                               ref _posVelocity, 1f / posSmooth, Mathf.Infinity, dt);
            _smoothRot   = Quaternion.Slerp(_smoothRot, targetRot, rotSmooth * dt);
            _smoothScale = Mathf.Lerp(_smoothScale, targetScale, scaleSmooth * dt);
        }

        // ── Position both copies ───────────────────────────────────────
        // palmNormalWorld = Local Y of the smoothed root rotation  
        Vector3 palmNW = _smoothRot * Vector3.up;

        Vector3 frontPos = _smoothPos + palmNW *  wrapOffset;
        Vector3 backPos  = _smoothPos + palmNW * -wrapOffset;

        _front.transform.position   = frontPos;
        _front.transform.rotation   = _smoothRot;
        _front.transform.localScale = Vector3.one * _smoothScale;

        _back.transform.position   = backPos;
        _back.transform.rotation   = _smoothRot;
        _back.transform.localScale = Vector3.one * _smoothScale;

        // ── Front / Back visibility ────────────────────────────────────
        Vector3 camDir    = (arCamera.transform.position - _smoothPos).normalized;
        float   viewDot   = Vector3.Dot(palmNW, camDir);
        float   signedDot = frontIsPalmSide ? viewDot : -viewDot;

        _front.SetActive(signedDot >= -blendZone);
        _back.SetActive(signedDot <=  blendZone);
    }

    // ── Coordinate Conversion ─────────────────────────────────────────────

    private Vector3 ConvertAt(Vector3 norm, float depth)
    {
        Vector3 flat  = new Vector3(norm.x, norm.y, 0f);
        Vector3 world = LandmarkToWorld_Hand.Convert(flat, arCamera, _texW, _texH, 0f);
        Ray     ray   = arCamera.ScreenPointToRay(arCamera.WorldToScreenPoint(world));
        return ray.origin + ray.direction * depth;
    }

    private void UpdateTextureDimensions()
    {
        if (imageSourceBehaviour != null)
        {
            var src = imageSourceBehaviour.GetImageSource();
            if (src != null && src.isPrepared)
            { _texW = src.textureWidth; _texH = src.textureHeight; return; }
        }
        if (_texW == 0) { _texW = 480; _texH = 640; }
    }
}