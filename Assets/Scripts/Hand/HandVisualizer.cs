// HandVisualizer.cs — v7 DEBUG  (remove before release)
// Fixed: no dim swap, nx = 1-lm.x for all cameras (matches LandmarkToWorld v14)

using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class HandVisualizer : MonoBehaviour
{
    public JewelleryLandmarkReader reader;
    public Camera arCamera;
    public ARCameraImageSourceBehaviour imageSourceBehaviour;
    public ARCameraManager arCameraManager;

    [Range(0.3f, 1.5f)] public float baseDepth = 0.5f;
    [Range(0f, 0.25f)] public float bboxYCorrection = 0.07f;
    [Range(0.003f, 0.02f)] public float dotSize = 0.007f;

    private const int N = 21;
    private GameObject[] _dots = new GameObject[N];
    private static readonly int[] GREEN_LM = { 5, 9, 13, 17 };

    private int _texW, _texH;

    void Start()
    {
        for (int i = 0; i < N; i++)
        {
            _dots[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _dots[i].transform.SetParent(transform);
            _dots[i].transform.localScale = Vector3.one * dotSize;
            var col = _dots[i].GetComponent<Collider>();
            if (col) col.enabled = false;
            var mat = _dots[i].GetComponent<Renderer>().material;
            mat.color = i == 0 ? Color.red   // wrist
                      : IsGreen(i) ? Color.green  // MCP knuckles
                      : Color.white;
            _dots[i].SetActive(false);
        }
    }

    void Update()
    {
        UpdateTex();
        bool valid = reader != null && reader.HandDetected && reader.LandmarkCount == N;
        for (int i = 0; i < N; i++)
        {
            _dots[i].SetActive(valid);
            if (valid)
                _dots[i].transform.position = LandmarkToWorld_Hand.Convert(
                    reader.GetLandmark(i), arCamera,
                    _texW, _texH, baseDepth, false, bboxYCorrection);
        }
    }

    void UpdateTex()
    {
        // No dim swap — ARCameraImageSource outputs portrait texture already
        bool got = false;
        if (imageSourceBehaviour != null)
        {
            var src = imageSourceBehaviour.GetImageSource();
            if (src != null && src.isPrepared)
            { _texW = src.textureWidth; _texH = src.textureHeight; got = true; }
        }
        if (!got && _texW == 0) { _texW = 720; _texH = 1280; }
    }

    void OnDestroy() { foreach (var d in _dots) if (d) Destroy(d); }

    static bool IsGreen(int i)
    { foreach (int g in GREEN_LM) if (i == g) return true; return false; }
}   