// RingPlacer.cs — v12 CAMERA-AWARE LANDMARK MAPPING
//
// FIX: Pass isBackCamera to LandmarkToWorld_Hand.Convert so that
// landmark X/Y are correctly un-mirrored for the back (World-facing) camera.
// The flag is read from ARCameraManager each frame so it stays in sync
// with JewelryManager's camera switching.

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

    [Header("Camera Reference (for back-camera landmark fix)")]
    [Tooltip("Assign the same ARCameraManager used by JewelryManager.")]
    public ARCameraManager arCameraManager;

    [Header("Finger")]
    public FingerTarget finger = FingerTarget.Ring;

    [Header("Placement")]
    [Tooltip("0=knuckle (MCP), 1=first joint (PIP). Ring ≈ 0.35")]
    [Range(0f, 1f)] public float fingerBias = 0.35f;

    [Header("Depth")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Ring Physical Size")]
    [Tooltip("The real-world inner diameter of the ring.\n" +
             "Average finger inner diameter: 0.017–0.022m (17–22mm)")]
    [Range(0.010f, 0.040f)] public float targetDiameterM = 0.019f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 20f, rotSmooth = 14f;
    [Range(0, 5)] public int minDetectionFrames = 2;

    private GameObject _ring;
    private Vector3 _calibratedScale;
    private Vector3 _sp; private Quaternion _sr = Quaternion.identity;
    private Vector3 _sv; private bool _first = true, _ready;
    private int _texW, _texH, _frames;

    static readonly int[,] FL = { { 5, 6, 7, 8 }, { 9, 10, 11, 12 }, { 13, 14, 15, 16 }, { 17, 18, 19, 20 } };

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[RingPlacer] missing!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (ringPrefab) SpawnRing(ringPrefab);
        _ready = true;
    }

    public void SetRingPrefab(GameObject p) { if (p) { ringPrefab = p; SpawnRing(p); } else ClearRing(); }
    public void ClearRing() { if (_ring) { Destroy(_ring); _ring = null; } _first = true; _frames = 0; ringPrefab = null; }

    void SpawnRing(GameObject prefab)
    {
        if (_ring) Destroy(_ring);
        _ring = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        _ring.SetActive(true);

        float measured = MeasureDiameter(_ring);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = _ring.transform.localScale * sf;
            _ring.transform.localScale = _calibratedScale;
            Debug.Log($"[RingPlacer] measured={measured:F4}m target={targetDiameterM:F4}m sf={sf:F4}");
        }
        else _calibratedScale = _ring.transform.localScale;

        _ring.SetActive(false);
        _first = true; _frames = 0;
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

    /// <summary>Returns true when ARCameraManager is set to World (back camera).</summary>
    bool IsBackCamera()
    {
        if (arCameraManager == null) return false;
        return arCameraManager.currentFacingDirection == CameraFacingDirection.World;
    }

    void LateUpdate()
    {
        if (!_ready || !_ring) return;
        UpdateTex();
        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 21)
        { _frames = 0; _ring.SetActive(false); _first = true; return; }
        if (++_frames < minDetectionFrames) { _ring.SetActive(false); return; }

        bool backCam = IsBackCamera();

        int fi = (int)finger;
        Vector3 mcp = C(FL[fi, 0], backCam), pip = C(FL[fi, 1], backCam);
        Vector3 tPos = Vector3.Lerp(mcp, pip, fingerBias);
        Vector3 fAxis = (pip - mcp).normalized;
        if (fAxis.sqrMagnitude < 0.001f) { _ring.SetActive(false); return; }

        Vector3 toCam = (arCamera.transform.position - mcp).normalized;
        Vector3 norm = Vector3.Cross(fAxis, toCam).normalized;
        if (landmarkReader.IsLeftHand) norm = -norm;
        if (norm.sqrMagnitude < 0.001f) norm = arCamera.transform.up;
        Quaternion tRot = Quaternion.LookRotation(fAxis, norm);

        float dt = Time.deltaTime;
        if (_first) { _sp = tPos; _sr = tRot; _sv = Vector3.zero; _first = false; }
        else
        {
            _sp = Vector3.SmoothDamp(_sp, tPos, ref _sv, 1f / posSmooth, Mathf.Infinity, dt);
            _sr = Quaternion.Slerp(_sr, tRot, rotSmooth * dt);
        }
        _ring.transform.SetPositionAndRotation(_sp, _sr);
        _ring.SetActive(true);
    }

    Vector3 C(int i, bool backCam) => LandmarkToWorld_Hand.Convert(
        landmarkReader.GetLandmark(i), arCamera, _texW, _texH, baseDepth, backCam);

    void UpdateTex()
    {
        if (imageSourceBehaviour != null) { var s = imageSourceBehaviour.GetImageSource(); if (s != null && s.isPrepared) { _texW = s.textureWidth; _texH = s.textureHeight; return; } }
        if (_texW == 0) { _texW = 720; _texH = 1280; }
    }
}