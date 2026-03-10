using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections;

/// <summary>
/// FaceMeshOccluder
/// ════════════════════════════════════════════════════════════════════
///
/// HOW TO USE — 3 steps:
///
///   Step 1. Import OccluderShader.shader into your project.
///           Put it anywhere, e.g. Assets/Shaders/OccluderShader.shader
///
///   Step 2. Create a Material using that shader:
///           Right-click in Project → Create → Material
///           Name it "FaceOccluderMat"
///           Set its Shader to Custom/FaceOccluder
///
///   Step 3. Add this script (FaceMeshOccluder.cs) to the same
///           GameObject that has your ARFace component (the face prefab).
///           Drag "FaceOccluderMat" into the OccluderMaterial slot.
///
/// WHAT HAPPENS AT RUNTIME:
///   • As soon as the ARFace mesh is ready, this script assigns
///     the occluder material to the MeshRenderer on the face.
///   • The invisible face mesh now blocks earrings behind it.
///   • All child objects of EarAttachARCore anchors are automatically
///     set to render queue 2001 (just after the occluder at 1990).
///   • Earrings behind the jaw/cheek disappear naturally.
///
/// ════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(ARFace))]
public class FaceMeshOccluder : MonoBehaviour
{
    [Tooltip("Drag the FaceOccluderMat material here.\n" +
             "Create it: Right-click → Material → set Shader to Custom/FaceOccluder")]
    public Material occluderMaterial;

    // Render queue values
    // Face occluder renders at 1990 (just before Geometry 2000)
    // Earrings render at 2001 (just after) so they depth-test against the face
    private const int OCCLUDER_QUEUE = 1990;
    private const int EARRING_QUEUE  = 2001;

    private ARFace face;
    private MeshRenderer faceMeshRenderer;
    private bool occluderApplied = false;

    void Awake()
    {
        face = GetComponent<ARFace>();
        face.updated += OnFaceUpdated;
    }

    void OnDestroy()
    {
        if (face != null) face.updated -= OnFaceUpdated;
    }

    void OnFaceUpdated(ARFaceUpdatedEventArgs args)
    {
        if (occluderApplied) return;
        TryApplyOccluder();
    }

    void Start()
    {
        // Also try immediately in case face mesh is already present
        TryApplyOccluder();
        // And poll for a couple of seconds in case the mesh arrives late
        StartCoroutine(PollUntilApplied());
    }

    IEnumerator PollUntilApplied()
    {
        float elapsed = 0f;
        while (!occluderApplied && elapsed < 10f)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
            TryApplyOccluder();
        }
    }

    void TryApplyOccluder()
    {
        if (occluderApplied) return;
        if (occluderMaterial == null)
        {
            Debug.LogWarning("[FaceMeshOccluder] OccluderMaterial not assigned! " +
                             "Please assign FaceOccluderMat in the Inspector.");
            return;
        }

        // Find the MeshRenderer on the ARFace (or its children)
        faceMeshRenderer = GetComponent<MeshRenderer>();
        if (faceMeshRenderer == null)
            faceMeshRenderer = GetComponentInChildren<MeshRenderer>();

        if (faceMeshRenderer == null)
        {
            // Mesh not ready yet — will retry
            return;
        }

        // Ensure the occluder material has the correct render queue
        occluderMaterial.renderQueue = OCCLUDER_QUEUE;

        // Apply to all material slots on the face mesh
        Material[] mats = new Material[faceMeshRenderer.sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++)
            mats[i] = occluderMaterial;
        faceMeshRenderer.sharedMaterials = mats;

        occluderApplied = true;
        Debug.Log("[FaceMeshOccluder] Occluder applied to face mesh. " +
                  "Face renders at queue " + OCCLUDER_QUEUE + " (depth-only).");

        // Now find all earring anchors in the scene and fix their render queues
        StartCoroutine(FixEarringRenderQueues());
    }

    IEnumerator FixEarringRenderQueues()
    {
        // Wait a frame to let earrings spawn
        yield return null;
        yield return null;

        // Find the EarAttachARCore and get its anchors
        EarAttachARCore earScript = FindObjectOfType<EarAttachARCore>();
        if (earScript == null) yield break;

        // Continuously fix any earring children that get spawned/replaced
        // Runs for the lifetime of the face to handle earring swaps
        while (this != null && earScript != null)
        {
            FixChildRenderers(earScript.transform);
            yield return new WaitForSeconds(0.2f);
        }
    }

    void FixChildRenderers(Transform parent)
    {
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                foreach (Material mat in r.materials)
                {
                    if (mat != null && mat.renderQueue < EARRING_QUEUE)
                    {
                        mat.renderQueue = EARRING_QUEUE;
                    }
                }
            }
        }
    }

    // ── Editor helper ──────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnValidate()
    {
        if (occluderMaterial != null)
            occluderMaterial.renderQueue = OCCLUDER_QUEUE;
    }
#endif
}
