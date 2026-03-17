// BanglePlacer.cs
// Fixes: depth pushed further from camera + size relative to wrist landmarks

using UnityEngine;

public class BanglePlacer : MonoBehaviour
{
    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

    [Header("Depth — increase if bangle appears too close/large")]
    [Tooltip("How far from camera the bangle is placed (meters).\n" +
             "Typical hand distance from phone = 0.4 to 0.8m.\n" +
             "Increase this if bangle looks too big/close.")]
    [Range(0.3f, 2.0f)] public float baseDepth = 0.7f;

    [Tooltip("How much MediaPipe Z landmark adjusts depth. Keep at 0 to disable.")]
    [Range(0.0f, 0.5f)] public float depthZScale = 0.0f;

    [Header("Position")]
    [Tooltip("0 = at wrist, 0.1 = slightly toward knuckles")]
    [Range(0f, 0.4f)] public float wristBias = 0.08f;

    [Header("Size")]
    [Tooltip("Direct scale of the bangle GameObject.\n" +
             "The bangle is also multiplied by wrist width so it auto-fits.\n" +
             "If still too big, reduce this value.")]
    [Range(0.01f, 1.0f)] public float sizeMultiplier = 0.08f;

    [Header("Smoothing")]
    [Range(1f, 30f)] public float posSmooth = 18f;
    [Range(1f, 30f)] public float rotSmooth = 14f;

    // ── Private ───────────────────────────────────────────────────────
    private GameObject _bangle;
    private Vector3 _smoothPos;
    private Quaternion _smoothRot = Quaternion.identity;
    private float _smoothScale = 0f;
    private bool _firstFrame = true;
    private bool _ready;
    private int _texW = 0, _texH = 0;
    private float _logTimer = 0f;

    void Start()
    {
        if (!landmarkReader) { Debug.LogError("[BanglePlacer] landmarkReader missing!"); return; }
        if (!arCamera) arCamera = Camera.main;
        if (banglePrefab != null) SpawnBangle(banglePrefab);
        _ready = true;
    }

    public void SetBanglePrefab(GameObject prefab)
    {
        if (prefab == null) { ClearBangle(); return; }
        banglePrefab = prefab;
        SpawnBangle(prefab);
    }

    public void ClearBangle()
    {
        if (_bangle != null) { Destroy(_bangle); _bangle = null; }
        _firstFrame = true;
        banglePrefab = null;
    }

    private void SpawnBangle(GameObject prefab)
    {
        if (_bangle != null) Destroy(_bangle);
        _bangle = Instantiate(prefab, transform);
        _bangle.SetActive(false);
        _firstFrame = true;
        _smoothScale = 0f;
        Debug.Log("[BanglePlacer] Spawned: " + prefab.name
            + "  prefab scale=" + prefab.transform.localScale);
    }

    void LateUpdate()
    {
        if (!_ready || _bangle == null) return;

        UpdateTextureDimensions();

        if (!landmarkReader.HandDetected || landmarkReader.LandmarkCount < 18)
        {
            _bangle.SetActive(false);
            _firstFrame = true;
            return;
        }

        // ── Landmarks ─────────────────────────────────────────────────
        Vector3 wristN = landmarkReader.GetLandmark(0);
        Vector3 thumbBaseN = landmarkReader.GetLandmark(1);
        Vector3 indexN = landmarkReader.GetLandmark(5);
        Vector3 midN = landmarkReader.GetLandmark(9);
        Vector3 ringN = landmarkReader.GetLandmark(13);
        Vector3 pinkyN = landmarkReader.GetLandmark(17);

        // ── Use fixed depth — ignore landmark Z ───────────────────────
        // This is the most important fix: placing bangle at a realistic
        // hand distance prevents it from appearing huge near the camera.
        float depth = baseDepth;

        Vector3 wristW = ConvertAt(wristN, depth);
        Vector3 thumbBaseW = ConvertAt(thumbBaseN, depth);
        Vector3 indexW = ConvertAt(indexN, depth);
        Vector3 midW = ConvertAt(midN, depth);
        Vector3 ringW = ConvertAt(ringN, depth);
        Vector3 pinkyW = ConvertAt(pinkyN, depth);

        // ── Position ──────────────────────────────────────────────────
        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 wristToKnuckle = (knuckleCenter - wristW).normalized;

        if (wristToKnuckle.sqrMagnitude < 0.001f) { _bangle.SetActive(false); return; }

        float armLength = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + wristToKnuckle * (armLength * wristBias);

        // ── Scale ─────────────────────────────────────────────────────
        // wristWidth in world space at this depth = real physical size
        // sizeMultiplier scales up to bangle ring size
        float wristWidth = Vector3.Distance(thumbBaseW, pinkyW);
        float targetScale = wristWidth * sizeMultiplier;

        // ── Rotation ──────────────────────────────────────────────────
        Vector3 palmAcross = (pinkyW - indexW).normalized;
        Vector3 palmNormal = Vector3.Cross(wristToKnuckle, palmAcross).normalized;
        if (landmarkReader.IsLeftHand) palmNormal = -palmNormal;
        if (palmNormal.sqrMagnitude < 0.001f) palmNormal = arCamera.transform.forward;

        Quaternion targetRot = Quaternion.LookRotation(wristToKnuckle, palmNormal);

        // ── Smooth ────────────────────────────────────────────────────
        float dt = Time.deltaTime;
        if (_firstFrame)
        {
            _smoothPos = targetPos;
            _smoothRot = targetRot;
            _smoothScale = targetScale;
            _firstFrame = false;
        }
        else
        {
            _smoothPos = Vector3.Lerp(_smoothPos, targetPos, posSmooth * dt);
            _smoothRot = Quaternion.Slerp(_smoothRot, targetRot, rotSmooth * dt);
            _smoothScale = Mathf.Lerp(_smoothScale, targetScale, posSmooth * dt);
        }

        _bangle.transform.position = _smoothPos;
        _bangle.transform.rotation = _smoothRot;
        _bangle.transform.localScale = Vector3.one * _smoothScale;
        _bangle.SetActive(true);

        // ── Debug log every 2s ────────────────────────────────────────
        _logTimer += dt;
        if (_logTimer >= 2f)
        {
            _logTimer = 0f;
            Debug.Log("[BanglePlacer] depth=" + depth.ToString("F2")
                + " wristWidth=" + wristWidth.ToString("F4")
                + " finalScale=" + _smoothScale.ToString("F4")
                + " pos=" + _smoothPos.ToString("F2"));
        }
    }

    private Vector3 ConvertAt(Vector3 norm, float depth)
    {
        Vector3 flat = new Vector3(norm.x, norm.y, 0f);
        Vector3 world = LandmarkToWorld.Convert(flat, arCamera, _texW, _texH, 0f);
        Ray ray = arCamera.ScreenPointToRay(arCamera.WorldToScreenPoint(world));
        return ray.origin + ray.direction * depth;
    }

    private void UpdateTextureDimensions()
    {
        if (imageSourceBehaviour != null)
        {
            var src = imageSourceBehaviour.GetImageSource();
            if (src != null && src.isPrepared)
            {
                _texW = src.textureWidth;
                _texH = src.textureHeight;
                return;
            }
        }
        if (_texW == 0) { _texW = 480; _texH = 640; }
    }
}