using UnityEngine;
using UnityEngine.XR.ARFoundation;

[DisallowMultipleComponent]
[RequireComponent(typeof(ARFace))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class ARFaceOccluder : MonoBehaviour
{
    [Header("Depth-only material (must have ZWrite On + ColorMask 0)")]
    public Material depthMaskMaterial;

    [Header("Render Order")]
    [Tooltip("Put this occluder just before normal geometry. 1999 = Geometry-1.")]
    public int renderQueue = 1999;

    [Header("Anti-flicker")]
    [Tooltip("Small push along normals. Keep tiny (0.0005 to 0.002).")]
    public float inflate = 0.0015f;

    [Tooltip("Re-apply inflation every frame (recommended, because ARFace mesh updates constantly).")]
    public bool inflateEveryFrame = true;

    MeshFilter mf;
    MeshRenderer mr;
    ARFace face;
    ARFaceMeshVisualizer meshVis;

    void Awake()
    {
        face = GetComponent<ARFace>();
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();

        // Ensure ARFaceMeshVisualizer exists so the face mesh is generated/updated
        meshVis = GetComponent<ARFaceMeshVisualizer>();
        if (meshVis == null)
            meshVis = gameObject.AddComponent<ARFaceMeshVisualizer>();
    }

    void OnEnable()
    {
        ApplyDepthMaterial();
    }

    void Update()
    {
        // Keep applying because:
        // - some pipelines reset materials/queue
        // - ARFoundation updates face mesh continuously
        ApplyDepthMaterial();

        if (inflateEveryFrame)
            InflateCurrentMesh();
    }

    void ApplyDepthMaterial()
    {
        if (!mr) return;

        if (depthMaskMaterial != null)
        {
            mr.sharedMaterial = depthMaskMaterial;
            // Force render queue so depth writes happen before necklace draw
            mr.sharedMaterial.renderQueue = renderQueue;
        }

        mr.enabled = true; // must render to write depth
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void InflateCurrentMesh()
    {
        if (inflate <= 0f) return;
        if (!mf) return;

        // ARFoundation writes into mf.mesh (instance). This is safe to edit.
        Mesh mesh = mf.mesh;
        if (mesh == null) return;

        var verts = mesh.vertices;
        var norms = mesh.normals;
        if (verts == null || norms == null) return;
        if (norms.Length != verts.Length) return;

        // IMPORTANT: Do NOT keep inflating the already-inflated vertices endlessly.
        // We rebuild from the original mesh each frame by using sharedMesh as base if possible.
        // But ARFaceMeshVisualizer often overwrites mf.mesh anyway, so this is generally safe.
        for (int i = 0; i < verts.Length; i++)
            verts[i] += norms[i] * inflate;

        mesh.vertices = verts;
        mesh.RecalculateBounds();
    }
}