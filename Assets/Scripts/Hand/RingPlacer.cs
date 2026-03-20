// RingPlacer.cs — v23  CALIBRATED from video analysis
//
// Changes from v22:
//   • bboxYCorrection 0.05 → 0.02  (pixel-measured calibration, dots 2% too high)
//   • fingerBias 0.40 → 0.50  (ring was sitting too close to MCP knuckle; 
//                               0.50 places it mid-way on the finger shaft ✓)
//   • Orientation: col[2]=fingerAxis confirmed CORRECT from video k5 (small oval ✓)
//   • World-up reference retained for stable level orientation

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
             "0.50 = mid shaft between knuckle and PIP (calibrated from video).\n" +
             "Increase toward 0.6–0.7 to move ring up the finger.")]
    [Range(0f, 1f)] public float fingerBias = 0.50f;

    [Header("Depth")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;

    [Header("Ring Physical Size")]
    [Tooltip("Target inner diameter in metres. Average finger: 0.017–0.022m.")]
    [Range(0.010f, 0.040f)] public float targetDiameterM = 0.019f;

    [Header("Landmark Y Correction")]
    [Tooltip("Positive = shift dots DOWN by this fraction of screen height.\n" +
             "0.02 = calibrated from pixel measurement.\n" +
             "Increase if dots above joints. Decrease if below.")]
    [Range(0f, 0.15f)] public float bboxYCorrection = 0.02f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f, rotSmooth = 18f;

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
    private Vector3 _calibratedScale;
    private Vector3 _sp;
    private Quaternion _sr = Quaternion.identity;
    private Vector3 _sv;
    private bool _first = true, _ready;
    private int _detFrames, _lostFrames;
    private int _rawTexW, _rawTexH, _texW, _texH;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[RingPlacer] landmarkReader missing!"); return; }
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

        float measured = MeasureDiameter(_ring);
        if (measured > 0.0001f)
        {
            float sf = targetDiameterM / measured;
            _calibratedScale = _ring.transform.localScale * sf;
            _ring.transform.localScale = _calibratedScale;
            Debug.Log($"[RingPlacer v23] measured={measured * 1000:F0}mm " +
                      $"target={targetDiameterM * 1000:F0}mm sf={sf:F4}");
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

        int fi = (int)finger;
        Vector3 mcp = C(FL[fi, 0]);
        Vector3 pip = C(FL[fi, 1]);

        // Finger axis MCP → PIP
        Vector3 fingerAxis = (pip - mcp).normalized;
        if (fingerAxis.sqrMagnitude < 0.001f) { _ring.SetActive(false); return; }

        // Position: 50% between MCP and PIP (mid finger shaft)
        Vector3 tPos = Vector3.Lerp(mcp, pip, fingerBias);

        // upRef: world-up projected perpendicular to finger axis
        Vector3 worldUp = Vector3.up;
        float upDotFinger = Mathf.Abs(Vector3.Dot(fingerAxis, worldUp));

        Vector3 upRef;
        if (upDotFinger < 0.95f)
        {
            upRef = (worldUp - Vector3.Dot(worldUp, fingerAxis) * fingerAxis).normalized;
        }
        else
        {
            Vector3 cr = arCamera.transform.right;
            upRef = (cr - Vector3.Dot(cr, fingerAxis) * fingerAxis).normalized;
        }

        // acrossRing: completes orthonormal frame
        Vector3 acrossRing = Vector3.Cross(upRef, fingerAxis).normalized;

        // Rotation matrix — ring model's hole = local Z (confirmed from video k5)
        //   col[0] = acrossRing  → local X
        //   col[1] = upRef       → local Y
        //   col[2] = fingerAxis  → local Z = hole = finger direction
        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(acrossRing.x, acrossRing.y, acrossRing.z, 0f));
        m.SetColumn(1, new Vector4(upRef.x, upRef.y, upRef.z, 0f));
        m.SetColumn(2, new Vector4(fingerAxis.x, fingerAxis.y, fingerAxis.z, 0f));
        Quaternion tRot = m.rotation;

        float dt = Time.deltaTime;
        if (_first)
        { _sp = tPos; _sr = tRot; _sv = Vector3.zero; _first = false; }
        else
        {
            _sp = Vector3.SmoothDamp(_sp, tPos, ref _sv, 1f / posSmooth, Mathf.Infinity, dt);
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