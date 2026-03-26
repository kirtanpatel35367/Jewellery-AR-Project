// RingPlacer.cs  (v4 – ring-visible fix + proper occlusion)
//
// ROOT CAUSE of "ring not visible" in v3:
//   The fallback occluder code (when no occluderMaterial was assigned) created
//   an opaque black Material at render queue 1999 which drew a solid capsule
//   directly OVER the ring.  Fix: occluder is only constructed when you have
//   explicitly assigned occluderMaterial.  Without it the ring renders normally.
//
// OCCLUSION / WRAP-AROUND (optional but recommended):
//   Assign a ZWrite-only material to occluderMaterial.  A capsule mesh is then
//   placed from MCP to PIP.  Because it writes depth (ColorMask 0, ZWrite On,
//   Queue=Geometry-1) the ring at Queue=Geometry correctly clips:
//     Palm toward camera  → front arc visible, back arc hidden  ✓
//     Back of hand        → back arc visible, front arc hidden  ✓
//
// REQUIRED SHADER (create once, name "Custom/FingerOccluder"):
//   Shader "Custom/FingerOccluder" {
//     SubShader {
//       Tags { "Queue"="Geometry-1" "RenderType"="Opaque" }
//       ColorMask 0   ZWrite On   Cull Off
//       Pass { }
//     }
//   }
//
// MediaPipe landmark indices:
//   Index:  5(MCP) 6(PIP) 7(DIP) 8(TIP)
//   Middle: 9(MCP) 10(PIP)11(DIP)12(TIP)
//   Ring:  13(MCP)14(PIP)15(DIP)16(TIP)
//   Pinky: 17(MCP)18(PIP)19(DIP)20(TIP)

using UnityEngine;

public class RingPlacer : MonoBehaviour
{
    public enum FingerTarget { Index, Middle, Ring, Pinky }

    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject ringPrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

    [Header("Finger")]
    public FingerTarget finger = FingerTarget.Ring;

    [Header("Placement")]
    [Tooltip("0 = knuckle (MCP), 1 = first joint (PIP).  0.3 = typical ring seat.")]
    [Range(0f, 1f)] public float fingerBias = 0.30f;

    [Header("Depth")]
    [Range(0.3f, 1.5f)] public float baseDepth = 0.65f;
    [Range(0f, 0.3f)] public float depthZScale = 0.12f;

    [Header("Ring Size")]
    [Tooltip("Diameter relative to gap between adjacent MCPs.  1.1 = slightly loose.")]
    [Range(0.5f, 2.5f)] public float sizeMultiplier = 1.1f;

    [Header("Smoothing")]
    [Range(1f, 40f)] public float posSmooth = 22f;
    [Range(1f, 40f)] public float rotSmooth = 16f;
    [Range(1f, 20f)] public float scaleSmooth = 8f;

    [Header("Stability")]
    [Range(0, 10)] public int minDetectionFrames = 3;

    [Header("Occlusion (optional – assign ZWrite-only material for wrap effect)")]
    [Tooltip("Material: ColorMask 0, ZWrite On, Queue=Geometry-1.\nLeave EMPTY → ring still renders, just no wrap-around occlusion.")]
    public Material occluderMaterial;

    [Range(0.2f, 0.8f)]
    public float occluderRadiusFraction = 0.40f;

    // ── private ───────────────────────────────────────────────────────

    private GameObject _ring;
    private GameObject _occluder;
    private MeshFilter _occMF;

    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private float _smoothScale = -1f;
    private Vector3 _posVelocity = Vector3.zero;
    private bool _firstFrame = true;
    private bool _ready;
    private int _texW = 0, _texH = 0;
    private int _detectionFrames = 0;
    private float _lastOccR = -1f;
    private float _lastOccHH = -1f;

    private static readonly int[,] FingerLandmarks =
    {
        {  5,  6,  7,  8 },
        {  9, 10, 11, 12 },
        { 13, 14, 15, 16 },
        { 17, 18, 19, 20 },
    };

    // ── lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[RingPlacer] landmarkReader missing!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (occluderMaterial != null) BuildOccluder();
        if (ringPrefab != null) SpawnRing(ringPrefab);
        _ready = true;
    }

    void OnDestroy() { if (_occluder) Destroy(_occluder); }

    // ── public API ────────────────────────────────────────────────────

    public void SetRingPrefab(GameObject prefab)
    {
        if (prefab == null) { ClearRing(); return; }
        ringPrefab = prefab;
        SpawnRing(prefab);
    }

    public void ClearRing()
    {
        if (_ring) { Destroy(_ring); _ring = null; }
        if (_occluder) _occluder.SetActive(false);
        _firstFrame = true; _detectionFrames = 0; ringPrefab = null;
    }

    // ── spawn ─────────────────────────────────────────────────────────

    private void SpawnRing(GameObject prefab)
    {
        if (_ring) Destroy(_ring);
        _ring = Instantiate(prefab, transform);
        _ring.SetActive(false);
        _firstFrame = true; _smoothScale = -1f; _detectionFrames = 0;
        Debug.Log("[RingPlacer] Spawned: " + prefab.name + " on " + finger);
    }

    private void BuildOccluder()
    {
        _occluder = new GameObject("[FingerOccluder]");
        _occluder.transform.SetParent(transform, false);
        _occMF = _occluder.AddComponent<MeshFilter>();
        var mr = _occluder.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.material = occluderMaterial;
        _occluder.SetActive(false);
    }

    // ── tracking ──────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (!_ready || _ring == null) return;
        UpdateTextureDimensions();

        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 21)
        {
            _detectionFrames = 0;
            _ring.SetActive(false);
            if (_occluder) _occluder.SetActive(false);
            _firstFrame = true;
            return;
        }

        _detectionFrames++;
        if (_detectionFrames < minDetectionFrames)
        {
            _ring.SetActive(false);
            if (_occluder) _occluder.SetActive(false);
            return;
        }

        int fi = (int)finger;
        int mcpIdx = FingerLandmarks[fi, 0];
        int pipIdx = FingerLandmarks[fi, 1];
        int adjIdx = (fi == 0) ? FingerLandmarks[1, 0] : FingerLandmarks[fi - 1, 0];

        Vector3 mcpN = landmarkReader.GetLandmark(mcpIdx);
        Vector3 pipN = landmarkReader.GetLandmark(pipIdx);
        Vector3 adjN = landmarkReader.GetLandmark(adjIdx);
        Vector3 wristN = landmarkReader.GetLandmark(0);

        float depth = Mathf.Clamp(baseDepth + wristN.z * depthZScale, 0.25f, 1.5f);

        Vector3 mcpW = ConvertAt(mcpN, depth);
        Vector3 pipW = ConvertAt(pipN, depth);
        Vector3 adjW = ConvertAt(adjN, depth);

        Vector3 fingerAxis = (pipW - mcpW).normalized;
        if (fingerAxis.sqrMagnitude < 0.001f)
        {
            _ring.SetActive(false);
            if (_occluder) _occluder.SetActive(false);
            return;
        }

        Vector3 targetPos = Vector3.Lerp(mcpW, pipW, fingerBias);
        float fingerWidth = Vector3.Distance(mcpW, adjW);
        float targetScale = fingerWidth * sizeMultiplier;

        if (targetScale < 0.0005f || float.IsNaN(targetScale))
        {
            _ring.SetActive(false);
            if (_occluder) _occluder.SetActive(false);
            return;
        }

        Vector3 toCamera = (arCamera.transform.position - mcpW).normalized;
        Vector3 palmNormal = Vector3.Cross(fingerAxis, toCamera).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;
        if (palmNormal.sqrMagnitude < 0.001f) palmNormal = arCamera.transform.up;

        Quaternion targetRot = Quaternion.LookRotation(fingerAxis, palmNormal);

        float dt = Time.deltaTime;
        if (_firstFrame || _smoothScale < 0f)
        {
            _smoothPos = targetPos; _smoothRot = targetRot;
            _smoothScale = targetScale; _posVelocity = Vector3.zero;
            _firstFrame = false;
        }
        else
        {
            _smoothPos = Vector3.SmoothDamp(_smoothPos, targetPos,
                               ref _posVelocity, 1f / posSmooth, Mathf.Infinity, dt);
            _smoothRot = Quaternion.Slerp(_smoothRot, targetRot, rotSmooth * dt);
            _smoothScale = Mathf.Lerp(_smoothScale, targetScale, scaleSmooth * dt);
        }

        _ring.transform.position = _smoothPos;
        _ring.transform.rotation = _smoothRot;
        _ring.transform.localScale = Vector3.one * _smoothScale;  // uniform – never collapses
        _ring.SetActive(true);

        if (_occluder != null && occluderMaterial != null)
            UpdateOccluder(mcpW, pipW, fingerAxis, fingerWidth);
    }

    // ── occluder ─────────────────────────────────────────────────────

    private void UpdateOccluder(Vector3 mcpW, Vector3 pipW,
                                Vector3 fingerAxis, float fingerWidth)
    {
        float halfH = Vector3.Distance(mcpW, pipW) * 0.5f;
        float capR = fingerWidth * occluderRadiusFraction;

        if (Mathf.Abs(capR - _lastOccR) > 0.001f || Mathf.Abs(halfH - _lastOccHH) > 0.002f)
        {
            var old = _occMF.sharedMesh;
            _occMF.mesh = MakeCapsule(capR, halfH);
            _lastOccR = capR; _lastOccHH = halfH;
            if (old != null && old.name == "OccluderCapsule") Destroy(old);
        }

        _occluder.transform.position = (mcpW + pipW) * 0.5f;
        _occluder.transform.rotation = Quaternion.FromToRotation(Vector3.up, fingerAxis);
        _occluder.SetActive(true);
    }

    // Y-axis capsule, radius r, half-height hh
    private static Mesh MakeCapsule(float r, float hh, int seg = 12)
    {
        int hSteps = 5;
        var verts = new System.Collections.Generic.List<Vector3>();
        var layers = new System.Collections.Generic.List<int[]>();

        void AddRingLayer(float y, float xzR)
        {
            var layer = new int[seg]; int start = verts.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                verts.Add(new Vector3(Mathf.Cos(a) * xzR, y, Mathf.Sin(a) * xzR));
                layer[i] = start + i;
            }
            layers.Add(layer);
        }

        layers.Add(new[] { verts.Count }); verts.Add(new Vector3(0, hh + r, 0));
        for (int s = 1; s <= hSteps; s++)
        {
            float phi = Mathf.PI * 0.5f * s / hSteps;
            AddRingLayer(hh + Mathf.Cos(phi) * r, Mathf.Sin(phi) * r);
        }
        AddRingLayer(hh, r);
        AddRingLayer(-hh, r);
        for (int s = hSteps - 1; s >= 1; s--)
        {
            float phi = Mathf.PI * 0.5f * s / hSteps;
            AddRingLayer(-(hh + Mathf.Cos(phi) * r), Mathf.Sin(phi) * r);
        }
        layers.Add(new[] { verts.Count }); verts.Add(new Vector3(0, -(hh + r), 0));

        var tris = new System.Collections.Generic.List<int>();
        for (int L = 0; L < layers.Count - 1; L++)
        {
            var top = layers[L]; var bot = layers[L + 1];
            if (top.Length == 1)
            { for (int i = 0; i < seg; i++) { tris.Add(top[0]); tris.Add(bot[i]); tris.Add(bot[(i + 1) % seg]); } }
            else if (bot.Length == 1)
            { for (int i = 0; i < seg; i++) { tris.Add(top[i]); tris.Add(bot[0]); tris.Add(top[(i + 1) % seg]); } }
            else
            {
                for (int i = 0; i < seg; i++)
                {
                    int n = (i + 1) % seg;
                    tris.Add(top[i]); tris.Add(bot[i]); tris.Add(top[n]);
                    tris.Add(top[n]); tris.Add(bot[i]); tris.Add(bot[n]);
                }
            }
        }

        var mesh = new Mesh { name = "OccluderCapsule" };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // ── coordinate conversion (identical to original v1 that worked) ──

    private Vector3 ConvertAt(Vector3 norm, float depth)
    {
        Vector3 flat = new Vector3(norm.x, norm.y, 0f);
        Vector3 world = LandmarkToWorld_Hand.Convert(flat, arCamera, _texW, _texH, 0f);
        Ray ray = arCamera.ScreenPointToRay(arCamera.WorldToScreenPoint(world));
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