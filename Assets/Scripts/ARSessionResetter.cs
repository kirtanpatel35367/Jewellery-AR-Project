using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.SceneManagement;

/// <summary>
/// ARSessionResetter
/// ─────────────────────────────────────────────────────────────────────
/// Attach this to the ARSession GameObject in EVERY scene that uses AR
/// (JewelryARScene, FaceTryOn, etc.).
///
/// PROBLEM IT SOLVES:
///   AR Foundation persists camera state (facing direction, tracking mode)
///   across scene loads. If JewelryARScene used the back camera and the
///   user navigates back to Main Menu then opens FaceTryOn, the AR session
///   wrongly starts with the back camera instead of the face camera.
///
/// HOW IT WORKS:
///   1. OnEnable  → forces the correct camera facing for THIS scene.
///   2. OnDestroy → resets + stops the AR session before the scene unloads,
///                  so the next scene gets a clean slate.
///
/// SETUP (per scene):
///   • Select your ARSession GameObject.
///   • Add this component.
///   • Set "Initial Facing Direction" to the camera this scene needs:
///       – FaceTryOn    → User  (front/face camera)
///       – JewelryARScene → World (back camera)
///   • Assign the AR Camera Manager field (drag your AR Camera here).
/// </summary>
[RequireComponent(typeof(ARSession))]
public class ARSessionResetter : MonoBehaviour
{
    [Header("Scene Camera Requirement")]
    [Tooltip("User = front/face camera (FaceTryOn). World = back camera (JewelryARScene).")]
    public CameraFacingDirection initialFacingDirection = CameraFacingDirection.User;

    [Header("References")]
    [Tooltip("Drag the ARCameraManager component (on your AR Camera child) here.")]
    public ARCameraManager arCameraManager;

    private ARSession _arSession;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        _arSession = GetComponent<ARSession>();
    }

    void OnEnable()
    {
        // Force the correct camera facing as soon as this scene's AR wakes up.
        // This overrides any state left over from a previous scene.
        ApplyCameraFacing(initialFacingDirection);
        Debug.Log($"[ARSessionResetter] Scene '{SceneManager.GetActiveScene().name}' " +
                  $"→ camera facing set to: {initialFacingDirection}");
    }

    void OnDestroy()
    {
        // Called when the scene is unloaded (e.g. user taps Back).
        // Reset then disable the AR session so the NEXT scene is not
        // inheriting stale tracking / camera state.
        ResetAndStopSession();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Call this manually (e.g. from a Back button handler) if you want
    /// to guarantee the session is torn down BEFORE scene load begins.
    /// </summary>
    public void ResetAndStopSession()
    {
        if (_arSession == null) return;

        // ARSession.Reset() clears all trackables and restarts tracking
        // with a clean state once the session is re-enabled.
        _arSession.Reset();

        // Disable the session object so it is not active when the next
        // scene queries camera state during its Awake/OnEnable phase.
        _arSession.enabled = false;

        Debug.Log($"[ARSessionResetter] AR session reset & disabled " +
                  $"(scene: '{SceneManager.GetActiveScene().name}').");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    void ApplyCameraFacing(CameraFacingDirection dir)
    {
        if (arCameraManager == null)
        {
            // Try to find it automatically if not assigned in the Inspector.
            arCameraManager = FindObjectOfType<ARCameraManager>();
        }

        if (arCameraManager == null)
        {
            Debug.LogWarning("[ARSessionResetter] ARCameraManager not found — " +
                             "cannot set camera facing. Assign it in the Inspector.");
            return;
        }

        arCameraManager.requestedFacingDirection = dir;
    }
}