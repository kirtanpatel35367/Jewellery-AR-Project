using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARNecklaceSnapFit : MonoBehaviour
{
    [Header("AR")]
    public ARFace face;

    [Header("Bones (left → right)")]
    public Transform[] chainBones; // 5 or 7 bones recommended

    [Header("Placement")]
    public float neckDrop = 0.055f;
    public float forwardPush = 0.01f;
    public Vector3 localOffset = Vector3.zero;

    [Header("Smoothing")]
    public float posLerp = 18f;

    // Approx jaw indices (ARCore style)
    const int LEFT_JAW  = 234;
    const int RIGHT_JAW = 454;
    const int CHIN      = 152;

    Vector3[] verts;

    void Awake()
    {
        if (!face) face = GetComponentInParent<ARFace>();
    }

    void LateUpdate()
    {
        if (!face) return;
        if (face.trackingState != TrackingState.Tracking) return;

        // ✅ CORRECT way (no face.mesh error)
        verts = face.vertices.ToArray();
        if (verts == null || verts.Length == 0)
        {
            var mf = face.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                verts = mf.sharedMesh.vertices;
        }

        if (verts == null || verts.Length < 200) return;

        // 1️⃣ Get jaw points
        Vector3 L = SafeVert(LEFT_JAW);
        Vector3 R = SafeVert(RIGHT_JAW);
        Vector3 C = SafeVert(CHIN);

        // 2️⃣ Apply drop + forward push
        Vector3 drop = Vector3.down * neckDrop;
        Vector3 push = Vector3.forward * forwardPush;

        L += drop + push + localOffset;
        R += drop + push + localOffset;

        // Center slightly lower (natural curve)
        Vector3 mid = (L + R) * 0.5f;
        mid.y = Mathf.Min(mid.y, (C + drop + push + localOffset).y - 0.01f);

        // 3️⃣ Place bones in curve (Snapchat-like)
        if (chainBones == null || chainBones.Length < 3) return;

        for (int i = 0; i < chainBones.Length; i++)
        {
            float t = i / (float)(chainBones.Length - 1);
            Vector3 p = Bezier(L, mid, R, t);

            Transform b = chainBones[i];
            if (!b) continue;

            b.localPosition = Vector3.Lerp(b.localPosition, p, Time.deltaTime * posLerp);
        }
    }

    Vector3 SafeVert(int idx)
    {
        idx = Mathf.Clamp(idx, 0, verts.Length - 1);
        return verts[idx];
    }

    Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 ab = Vector3.Lerp(a, b, t);
        Vector3 bc = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(ab, bc, t);
    }
}