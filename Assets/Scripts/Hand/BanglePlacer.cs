// BanglePlacer.cs
// Auto-sizes bangle to user's wrist using hand landmarks.

using UnityEngine;

public class BanglePlacer : MonoBehaviour
{
    [Header("References")]
    public JewelleryLandmarkReader landmarkReader;
    public Camera arCamera;
    public GameObject banglePrefab;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;

    [Header("Depth")]
    [Range(0.2f, 1.5f)] public float baseDepth = 0.5f;
    [Range(0.0f, 0.3f)] public float depthZScale = 0.1f;

    [Header("Position")]
    [Range(0f, 0.5f)] public float wristBias = 0.1f;

    [Header("Size — tune these two values")]
    [Tooltip("Direct scale override. 0 = use auto wrist-relative sizing.\n" +
             "Set this to a fixed value like 0.05 to lock the size.")]
    [Range(0f, 1f)]
    public float fixedScale = 0f;

    [Tooltip("Only used when fixedScale = 0.\n" +
             "Multiplier on detected wrist width. Start at 1.0, adjust up/down.")]
    [Range(0.01f, 5f)]
    public float sizeMultiplier = 1.0f;

    [Header("Smoothing")]
    [Range(1f, 30f)] public float posSmooth = 18f;
    [Range(1f, 30f)] public float rotSmooth = 14f;

    [Header("Debug")]
    [Tooltip("Shows wrist width and final scale in Console every second")]
    public bool debugLogging = true;

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

    // ── Public API ────────────────────────────────────────────────────

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

    // ── Spawn ─────────────────────────────────────────────────────────

    private void SpawnBangle(GameObject prefab)
    {
        if (_bangle != null) Destroy(_bangle);
        _bangle = Instantiate(prefab, transform);
        _bangle.SetActive(false);
        _firstFrame = true;
        _smoothScale = 0f;
    }

    // ── Tracking ──────────────────────────────────────────────────────

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

        Vector3 wristN = landmarkReader.GetLandmark(0);
        Vector3 thumbBaseN = landmarkReader.GetLandmark(1);
        Vector3 indexN = landmarkReader.GetLandmark(5);
        Vector3 midN = landmarkReader.GetLandmark(9);
        Vector3 ringN = landmarkReader.GetLandmark(13);
        Vector3 pinkyN = landmarkReader.GetLandmark(17);

        float depth = Mathf.Clamp(
            baseDepth + wristN.z * depthZScale,
            arCamera.nearClipPlane + 0.05f, 3.0f);

        Vector3 wristW = ConvertAt(wristN, depth);
        Vector3 thumbBaseW = ConvertAt(thumbBaseN, depth);
        Vector3 indexW = ConvertAt(indexN, depth);
        Vector3 midW = ConvertAt(midN, depth);
        Vector3 ringW = ConvertAt(ringN, depth);
        Vector3 pinkyW = ConvertAt(pinkyN, depth);

        Vector3 knuckleCenter = (indexW + midW + ringW + pinkyW) * 0.25f;
        Vector3 wristToKnuckle = (knuckleCenter - wristW).normalized;

        if (wristToKnuckle.sqrMagnitude < 0.001f) { _bangle.SetActive(false); return; }

        float armLength = Vector3.Distance(wristW, knuckleCenter);
        Vector3 targetPos = wristW + wristToKnuckle * (armLength * wristBias);

        // ── Scale decision ────────────────────────────────────────────
        float wristWidth = Vector3.Distance(thumbBaseW, pinkyW);
        float targetScale;

        if (fixedScale > 0f)
        {
            // Use exact fixed value — ignores wrist size
            targetScale = fixedScale;
        }
        else
        {
            // Auto: scale relative to detected wrist width
            targetScale = wristWidth * sizeMultiplier;
        }

        // Debug log every 1 second
        if (debugLogging)
        {
            _logTimer += Time.deltaTime;
            if (_logTimer >= 1f)
            {
                _logTimer = 0f;
                Debug.Log($"[BanglePlacer] wristWidth={wristWidth:F4}  targetScale={targetScale:F4}  " +
                          $"prefabScale={banglePrefab?.transform.localScale}  depth={depth:F2}");
            }
        }

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